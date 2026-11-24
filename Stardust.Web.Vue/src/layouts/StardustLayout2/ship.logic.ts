// 发布页纯逻辑（无 Vue 依赖，request 由调用方注入，便于单测）。
// 对照原型 page-ship.jsx 的 ShipPanel。原型的版本、历史、灰度动画是演示数据，这里只接真实接口：
//  - 部署集：GET /Deployment/AppDeploy?appId=
//  - 版本：GET /Deployment/AppDeployVersion?deployId=
//  - 节点：GET /Deployment/AppDeployNode?deployId=
//  - 按节点结果：GET /Deployment/AppPipelineStep?runId=（只有流水线发起的部署才有步骤）
//  - 发起部署：GET /Deployment/AppDeployVersion/UseVersion?id=（响应结构未核实，见 useVersion）
// 响应字段大小写两种都认（后端 JSON 与旧 MVC 方言）。取数失败返回 null，由页面显示「加载失败」，不造数据。
import { mergeRunHistory, normalizeRunStatus, okRes, triggerLabel, isRunActive } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import type { PipelineRunRow, Req, RunHistoryRow } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

type Rec = Record<string, unknown>;

/** 与 Tag.vue 的 tone 保持一致 */
export type ShipTone = 'default' | 'ok' | 'warn' | 'err' | 'brand';

function pick(r: Rec, ...keys: string[]): unknown {
  for (const k of keys) {
    const v = r[k];
    if (v !== undefined && v !== null) return v;
  }
  return undefined;
}
function num(v: unknown): number {
  const n = Number(v);
  return Number.isFinite(n) ? n : 0;
}
function str(v: unknown): string {
  return v === undefined || v === null ? '' : String(v);
}
function bool(v: unknown): boolean {
  if (typeof v === 'boolean') return v;
  if (typeof v === 'number') return v !== 0;
  const s = str(v).trim().toLowerCase();
  return s === 'true' || s === '1';
}
function listOf(res: { data?: unknown } | undefined): Rec[] {
  const d = res?.data;
  return Array.isArray(d) ? (d as Rec[]) : [];
}

// ---------- 部署集 ----------

export interface DeployInfo {
  id: number;
  appId: number;
  /** 部署集名称（D4 空态主句「部署集「{name}」暂无节点」用） */
  name: string;
  version: string;
  enable: boolean;
  autoPublish: boolean;
  /** 开启后各节点可以运行不同版本，此时节点版本与部署集版本不一致不一定是异常（列表接口是否返回该字段未核实，缺失按 false） */
  multiVersion: boolean;
  /** task-90 部署集级日志目录：节点未配置时跟随（212 新后端字段；旧构建响应缺键为空串，resolveEffectiveLogDir 兜底） */
  logDirectory?: string;
}

/** CARD-DEP-08 D2：同应用其余部署集摘要。count=其余集数（rows-1），nodes=本应用全部集的启用节点数之和（Σ Nodes，D3 修正后口径可信） */
export interface DeployOthers {
  count: number;
  nodes: number;
}
export type DeployLookup =
  | { status: 'ok'; deploy: DeployInfo; others: DeployOthers }
  | { status: 'none' }
  | { status: 'failed' };

export async function fetchDeploy(request: Req, appId: number): Promise<DeployLookup> {
  if (!appId || appId <= 0) return { status: 'none' };
  try {
    const res = await request({ url: '/Deployment/AppDeploy', method: 'GET', params: { appId, PageSize: 20 } });
    const rows = listOf(res);
    // 后端按 appId 过滤（应用部署.Biz.cs Search 的 _.AppId == appId），但列表输出去掉了 AppId 字段
    // （AppDeployController 的 ListFields.RemoveField("AppId", …)），行内通常没有 appId 可比对。
    // 此时直接采用后端过滤结果的第一个（Id 倒序，即最新部署集）；行内带 appId 时仍严格比对，避免拿错应用。
    // （task-89 的「appId 恒在→严格过滤」定谳不采：新掩码契约下行键可被投影/掩码，缺席容忍优先，22:5x 审计背书）
    const hasAppId = rows.some((r) => pick(r, 'AppId', 'appId') !== undefined);
    // 本应用行集：行内带 appId 时严格按行过滤（其余应用行不进结果、不进 others）；
    // 全部行缺 appId（掩码契约）时信任后端的 appId 查询语义，全部行即本应用行。
    const own = hasAppId ? rows.filter((r) => num(pick(r, 'AppId', 'appId')) === appId) : rows;
    const hit = own[0];
    if (!hit) return { status: 'none' };
    return {
      status: 'ok',
      deploy: {
        id: num(pick(hit, 'Id', 'id')),
        appId,
        name: str(pick(hit, 'Name', 'name')),
        version: str(pick(hit, 'Version', 'version')),
        enable: bool(pick(hit, 'Enable', 'enable')),
        autoPublish: bool(pick(hit, 'AutoPublish', 'autoPublish')),
        multiVersion: bool(pick(hit, 'MultiVersion', 'multiVersion')),
        logDirectory: str(pick(hit, 'LogDirectory', 'logDirectory')),
      },
      others: { count: Math.max(0, own.length - 1), nodes: own.reduce((s: number, r) => s + num(pick(r, 'Nodes', 'nodes')), 0) },
    };
  } catch {
    return { status: 'failed' };
  }
}

// ---------- 版本 ----------

export interface VersionOption {
  id: number;
  version: string;
  disabled: boolean;
  reason: string;
  current: boolean;
  label: string;
  /** C18 下载包链接：后端 Url 原文（trim 后；缺字段为空串） */
  url: string;
}

/** UseVersion 要求版本已启用、有版本号、有文件地址；Url 字段缺失时视为未知，不置灰 */
export function toVersionOptions(rows: Rec[], currentVersion: string): VersionOption[] {
  return rows
    .map((r) => {
      const id = num(pick(r, 'Id', 'id'));
      const version = str(pick(r, 'Version', 'version'));
      const enable = bool(pick(r, 'Enable', 'enable'));
      const url = pick(r, 'Url', 'url');
      let reason = '';
      if (!version) reason = '没有版本号';
      else if (!enable) reason = '版本未启用';
      else if (url !== undefined && str(url).trim() === '') reason = '没有文件地址';
      const current = !!version && version === currentVersion;
      const label = `${version || `#${id}`}${current ? ' · 当前' : ''}${reason ? ` · ${reason}` : ''}`;
      return { id, version, disabled: reason !== '', reason, current, label, url: str(url ?? '').trim() };
    })
    .sort((a, b) => b.id - a.id);
}

/** C18 下载包链接：Url 已落库（相对 `/cube/file?id=…` 或绝对地址）。SPA 与 6681 不同源，相对路径须拼 API 基址
 *  （参照 pipelineWebhook.logic.ts:49 的 base ?? env 兜底；单测环境无 env 时相对路径降级原样返回，同源部署仍可达）。
 *  返回 null = 无 Url，不渲染链接。 */
export function versionDownloadHref(rawUrl: unknown, base?: string): string | null {
  const u = str(rawUrl ?? '').trim();
  if (!u) return null;
  if (/^https?:\/\//i.test(u)) return u;
  const b = String(base ?? import.meta.env?.VITE_APP_BASE_API ?? '').replace(/\/+$/, '');
  return `${b}${u.startsWith('/') ? u : `/${u}`}`;
}

export async function fetchVersions(request: Req, deployId: number, currentVersion: string): Promise<VersionOption[] | null> {
  if (!deployId || deployId <= 0) return [];
  try {
    const res = await request({ url: '/Deployment/AppDeployVersion', method: 'GET', params: { deployId, PageSize: 50 } });
    return toVersionOptions(listOf(res), currentVersion);
  } catch {
    return null;
  }
}

// ---------- 节点 ----------

export interface ShipNode {
  id: number;
  nodeId: number;
  name: string;
  ip: string;
  enable: boolean;
  /** 部署版本（Version）：install 发布下发时回填的包版本（2026-11-12 方案C；此前无写入者恒空） */
  version: string;
  /** 应用程序版本（AppVersion）：在线实例上报的应用入口程序集 FileVersion，随心跳自动更新（应用节点.Biz.cs Fill(AppOnline)） */
  appVersion: string;
  mode: number;
  modeLabel: string;
  /** task-90 节点级日志目录：空（trim 后）=跟随部署集（212 新后端字段；旧构建响应缺键为空串） */
  logDirectory?: string;
  /** 延迟（Delay，单位秒）：发布下去后延迟执行的时间，用于滚动发布；0=不延迟（task-92；实体 Stardust.Data/Deployment/应用节点.cs:162） */
  delay: number;
  /** 进程（ProcessId）：0=未上报/未运行，展示层显示「—」（应用节点.cs:180） */
  processId: number;
  /** 进程时间（StartTime）：进程启动时间；已格式化 yyyy-MM-dd HH:mm，未上报（0001-01-01）为「—」（应用节点.cs:207） */
  startTime: string;
  /** 编译时间（Compile）：客户端编译时间，格式化同上（应用节点.cs:216） */
  compile: string;
  /** 最后活跃（LastActive）：最后一次上报心跳的时间，格式化同上（应用节点.cs:234） */
  lastActive: string;
}

/** AppDeployNode.Mode：运行模式（不是 AppDeployVersion.Mode 的包模式） */
const MODE_LABELS: Record<number, string> = { 10: 'Standard', 11: 'Shadow', 12: 'Hosted', 13: 'Task' };
export function modeLabel(mode: number): string {
  return MODE_LABELS[mode] ?? (mode ? `模式 ${mode}` : '—');
}

export function toShipNodes(rows: Rec[]): ShipNode[] {
  return rows.map((r) => {
    const nodeId = num(pick(r, 'NodeId', 'nodeId'));
    const mode = num(pick(r, 'Mode', 'mode'));
    return {
      id: num(pick(r, 'Id', 'id')),
      nodeId,
      name: str(pick(r, 'NodeName', 'nodeName')) || `节点 ${nodeId}`,
      ip: str(pick(r, 'IP', 'iP', 'ip')),
      enable: bool(pick(r, 'Enable', 'enable')),
      version: str(pick(r, 'Version', 'version')),
      appVersion: str(pick(r, 'AppVersion', 'appVersion')),
      mode,
      modeLabel: modeLabel(mode),
      logDirectory: str(pick(r, 'LogDirectory', 'logDirectory')),
      // task-92 五列：延迟/进程/进程时间/编译时间/最后活跃（键已在 6681 响应体，见 Doc/用户旅程/evidence/deploy-nodes-cols/）
      delay: num(pick(r, 'Delay', 'delay')),
      processId: num(pick(r, 'ProcessId', 'processId')),
      startTime: fmtTime(str(pick(r, 'StartTime', 'startTime'))),
      compile: fmtTime(str(pick(r, 'Compile', 'compile'))),
      lastActive: fmtTime(str(pick(r, 'LastActive', 'lastActive'))),
    };
  });
}

export async function fetchShipNodes(request: Req, deployId: number): Promise<ShipNode[] | null> {
  if (!deployId || deployId <= 0) return [];
  try {
    const res = await request({ url: '/Deployment/AppDeployNode', method: 'GET', params: { deployId, PageSize: 200 } });
    return toShipNodes(listOf(res));
  } catch {
    return null;
  }
}

// ---------- 流水线步骤（按节点的部署结果） ----------

export type StepStatus = 'Pending' | 'Running' | 'Success' | 'Failed' | 'Skipped' | 'Cancelled' | '';
const STEP_STATUSES = ['Pending', 'Running', 'Success', 'Failed', 'Skipped', 'Cancelled'] as const;

export function normalizeStepStatus(v: unknown): StepStatus {
  const s = str(v).trim().toLowerCase();
  return STEP_STATUSES.find((x) => x.toLowerCase() === s) ?? '';
}

export interface StepRow {
  id: number;
  runId: number;
  stepType: string;
  stepIndex: number;
  nodeId: number;
  commandId: number;
  status: StepStatus;
  message: string;
}

export function toStepRows(rows: Rec[]): StepRow[] {
  return rows.map((r) => ({
    id: num(pick(r, 'Id', 'id')),
    runId: num(pick(r, 'RunId', 'runId')),
    stepType: str(pick(r, 'StepType', 'stepType')),
    stepIndex: num(pick(r, 'StepIndex', 'stepIndex')),
    nodeId: num(pick(r, 'NodeId', 'nodeId')),
    commandId: num(pick(r, 'CommandId', 'commandId')),
    status: normalizeStepStatus(pick(r, 'Status', 'status')),
    message: str(pick(r, 'Message', 'message')),
  }));
}

/** 后端 /Deployment/AppPipelineStep 只认 runId，必须带 */
export async function fetchRunSteps(request: Req, runId: number): Promise<StepRow[] | null> {
  if (!runId || runId <= 0) return [];
  try {
    const res = await request({ url: '/Deployment/AppPipelineStep', method: 'GET', params: { runId, PageSize: 200 } });
    return toStepRows(listOf(res));
  } catch {
    return null;
  }
}

/** 运行是否已到终态（成功、失败、已取消） */
export function isRunTerminal(status: unknown): boolean {
  const s = normalizeRunStatus(status);
  return s === 'Success' || s === 'Failed' || s === 'Cancelled';
}

/** 格子状态：old/cur/off 来自节点版本比对；其余来自部署步骤 */
export type NodeCell = 'none' | 'old' | 'cur' | 'off' | 'wait' | 'ing' | 'ok' | 'fail' | 'skip' | 'cancel' | 'unrun' | 'noreply';

export const CELL_LABELS: Record<NodeCell, string> = {
  none: '无部署记录',
  old: '旧版本',
  cur: '当前版本',
  off: '节点已停用',
  wait: '等待下发',
  ing: '部署中',
  ok: '已部署',
  fail: '部署失败',
  skip: '已跳过',
  cancel: '已取消',
  unrun: '未执行',
  noreply: '未回包',
};

/** run 已终态而步骤仍是 Running，说明节点没有回包（后端失败后会丢弃其余节点的回包），不能显示「进行中」 */
export function nodeCell(step: StepRow | undefined, runTerminal: boolean): NodeCell {
  if (!step) return 'none';
  switch (step.status) {
    case 'Success':
      return 'ok';
    case 'Failed':
      return 'fail';
    case 'Skipped':
      return 'skip';
    case 'Cancelled':
      return 'cancel';
    case 'Running':
      return runTerminal ? 'noreply' : 'ing';
    case 'Pending':
      return runTerminal ? 'unrun' : 'wait';
    default:
      return 'none';
  }
}

export interface NodeResult {
  key: string;
  nodeId: number;
  name: string;
  ip: string;
  cell: NodeCell;
  label: string;
  message: string;
}

export interface NodeResults {
  items: NodeResult[];
  /** 有部署步骤且全部被跳过：后端此时可能把 run 标成成功，界面要单独说明 */
  allSkipped: boolean;
  deploySteps: number;
}

export function buildNodeResults(nodes: ShipNode[], steps: StepRow[], runStatus: unknown): NodeResults {
  const terminal = isRunTerminal(runStatus);
  const deploy = steps.filter((s) => s.stepType.toLowerCase() === 'deploy');
  const latest = new Map<number, StepRow>();
  for (const s of deploy) {
    const old = latest.get(s.nodeId);
    if (!old || s.stepIndex > old.stepIndex || (s.stepIndex === old.stepIndex && s.id > old.id)) latest.set(s.nodeId, s);
  }
  const make = (nodeId: number, name: string, ip: string): NodeResult => {
    const step = latest.get(nodeId);
    const cell = nodeCell(step, terminal);
    return { key: `n${nodeId}`, nodeId, name, ip, cell, label: CELL_LABELS[cell], message: step?.message ?? '' };
  };
  const items = nodes.map((n) => make(n.nodeId, n.name, n.ip));
  const known = new Set(nodes.map((n) => n.nodeId));
  for (const nodeId of latest.keys()) {
    if (!known.has(nodeId)) items.push(make(nodeId, `节点 ${nodeId}`, ''));
  }
  return { items, allSkipped: deploy.length > 0 && deploy.every((s) => s.status === 'Skipped'), deploySteps: deploy.length };
}

/** 没有选中运行记录时：只比对节点版本与部署集版本，不假装有部署结果 */
export function buildVersionCells(nodes: ShipNode[], deployVersion: string): NodeResult[] {
  return nodes.map((n) => {
    const cell: NodeCell = !n.enable ? 'off' : n.version && n.version === deployVersion ? 'cur' : 'old';
    return { key: `n${n.nodeId}`, nodeId: n.nodeId, name: n.name, ip: n.ip, cell, label: CELL_LABELS[cell], message: n.version };
  });
}

// ---------- 发起部署 ----------

export interface ReleaseGate {
  deploy: DeployInfo | null;
  version: VersionOption | undefined;
  enabledNodes: number;
  busy: boolean;
}

/** 返回空串表示可以发起；否则是按钮置灰时要显示的原因 */
export function releaseBlock(g: ReleaseGate): string {
  if (!g.deploy) return '该应用还没有部署集';
  if (!g.deploy.enable) return '部署集未启用';
  // AutoPublish 不再阻断：是否向节点下发由后端决定，界面在点击前用 useVersionNotice 说明后果
  if (g.enabledNodes <= 0) return '没有启用的目标节点';
  if (!g.version) return '请选择版本';
  if (g.version.disabled) return `该版本不可用：${g.version.reason}`;
  if (g.busy) return '正在发起，请稍候';
  return '';
}

/** 发起部署：UseVersion 先改部署集版本，再向各启用节点下发 install（fire-and-forget）。
 *  响应结构未核实：控制器返回 JsonRefresh，按 okRes（code 0/200/1 或 success）判定；判不出来时按失败提示，让用户去节点页确认。 */
export async function useVersion(request: Req, versionId: number): Promise<{ ok: boolean; message: string }> {
  try {
    const res = await request({ url: '/Deployment/AppDeployVersion/UseVersion', method: 'GET', params: { id: versionId } });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? r.message || '已下发部署命令' : r.message || '未能确认下发结果，请到节点页核对' };
  } catch (e) {
    return { ok: false, message: e instanceof Error && e.message ? e.message : '请求失败' };
  }
}

// ---------- 运行列表 ----------

export function fmtTime(s: string | undefined): string {
  if (!s) return '—';
  // DateTime.MinValue（0001-01-01）是「未到达/空值」语义：统一占位「—」（实现规范 §3.3；走查缺陷3，2026-11-07）
  if (s.startsWith('0001-01-01')) return '—';
  return s.replace('T', ' ').slice(0, 16);
}

export function runTone(status: unknown): { tone: ShipTone; label: string } {
  switch (normalizeRunStatus(status)) {
    case 'Success':
      return { tone: 'ok', label: '成功' };
    case 'Failed':
      return { tone: 'err', label: '失败' };
    case 'Cancelled':
      return { tone: 'default', label: '已取消' };
    case 'Building':
      return { tone: 'brand', label: '编译中' };
    case 'Deploying':
      return { tone: 'brand', label: '部署中' };
    case 'UploadSucceeded':
      return { tone: 'brand', label: '待部署' };
    case 'Pending':
      return { tone: 'warn', label: '排队中' };
    default:
      return { tone: 'default', label: '未知' };
  }
}

export interface ShipRunRow {
  key: string;
  id: number;
  version: string;
  pipeline: string;
  trigger: string;
  author: string;
  message: string;
  tone: ShipTone;
  statusLabel: string;
  time: string;
  active: boolean;
  status: string;
}

export function buildRunRows(runs: RunHistoryRow[], versions: VersionOption[]): ShipRunRow[] {
  const byId = new Map(versions.map((v) => [v.id, v.version]));
  return runs.map((r, i) => {
    const t = runTone(r.status);
    return {
      key: `r${r.id ?? i}`,
      id: r.id ?? 0,
      version: (r.appVersionId ? byId.get(r.appVersionId) : '') || '—',
      pipeline: r.pipelineName || '—',
      trigger: triggerLabel(r.triggerSource ?? ''),
      author: r.commitAuthor || '—',
      message: r.commitMessage || '',
      tone: t.tone,
      statusLabel: t.label,
      time: fmtTime(r.finishedTime || r.buildStartedTime),
      active: isRunActive(r.status),
      status: r.status ?? '',
    };
  });
}

// ---------- 点击前的后果说明 ----------

export interface Notice {
  tone: 'info' | 'warn';
  text: string;
}

/** 点击「应用此版本」前要让用户看到的后果。
 *  UseVersion 只有在部署集已启用且开了自动发布时，才会向所有启用节点下发 install；其余情况只改版本号。 */
export function useVersionNotice(deploy: DeployInfo | null, enabledNodes: number): Notice | null {
  if (!deploy) return null;
  if (!deploy.enable) return { tone: 'info', text: '部署集未启用：应用后只切换生效版本号，不会下发到任何节点。' };
  if (!deploy.autoPublish) {
    return {
      tone: 'info',
      text: '部署集没有开启「自动发布」：应用后只切换生效版本号，不会下发到节点；要装到节点，请在下方「部署节点」里逐台安装。',
    };
  }
  if (enabledNodes <= 0) return { tone: 'info', text: '已开启自动发布，但没有启用的节点：应用后只切换生效版本号。' };
  return { tone: 'warn', text: `该应用已开启自动发布：应用后将向 ${enabledNodes} 个启用节点同时下发安装。` };
}

// ---------- 节点操作 ----------

export type NodeAction = 'install' | 'start' | 'stop' | 'restart' | 'uninstall';

export interface NodeActionInfo {
  act: NodeAction;
  label: string;
  /** 后端会把节点 Enable 置 true（install、start、restart）还是 false（stop、uninstall） */
  enables: boolean;
  danger: boolean;
}

export const NODE_ACTIONS: NodeActionInfo[] = [
  { act: 'install', label: '安装', enables: true, danger: false },
  { act: 'start', label: '启动', enables: true, danger: false },
  { act: 'restart', label: '重启', enables: true, danger: false },
  { act: 'stop', label: '停止', enables: false, danger: true },
  { act: 'uninstall', label: '卸载', enables: false, danger: true },
];

export function actionInfo(act: string): NodeActionInfo | undefined {
  return NODE_ACTIONS.find((a) => a.act === act);
}

/** 点击节点操作前的后果说明：会改节点启用状态的操作必须写明 */
export function operateNotice(a: NodeActionInfo, node: Pick<ShipNode, 'name' | 'ip' | 'enable'>): Notice {
  const who = `节点 ${node.name}${node.ip ? `（${node.ip}）` : ''}`;
  if (a.enables && !node.enable) return { tone: 'warn', text: `${who}当前已停用，执行「${a.label}」后会被重新启用。` };
  if (!a.enables && node.enable) return { tone: 'warn', text: `执行「${a.label}」后，${who}会被标为停用，不再参与自动发布。` };
  return { tone: 'info', text: `对${who}执行「${a.label}」。命令发出后不等节点回包，是否生效请看节点版本与部署历史。` };
}

/** 对单个部署节点下发操作：GET /Deployment/AppDeployNode/Operate?act=&id=（id 是 AppDeployNode.Id，不是节点 Id）。
 *  后端只代表命令已发出（fire-and-forget）；节点行不存在时返回 code 500。响应文案带上节点名与动作，便于页面直接做反馈。 */
export async function operateNode(
  request: Req,
  act: NodeAction,
  node: Pick<ShipNode, 'id' | 'name'>,
): Promise<{ ok: boolean; message: string }> {
  const info = actionInfo(act);
  if (!info) return { ok: false, message: `不支持的操作：${act}` };
  if (!node.id || node.id <= 0) return { ok: false, message: `${node.name}：缺少部署节点编号，无法执行「${info.label}」` };
  try {
    const res = await request({ url: '/Deployment/AppDeployNode/Operate', method: 'GET', params: { act, id: node.id } });
    const r = okRes(res);
    if (r.ok) return { ok: true, message: `${node.name}：已下发「${info.label}」命令（只表示命令已发出，是否生效请看节点版本与部署历史）` };
    return { ok: false, message: `${node.name}：「${info.label}」未成功${r.message ? `：${r.message}` : '，未能确认下发结果，请到部署历史核对'}` };
  } catch (e) {
    return { ok: false, message: `${node.name}：「${info.label}」失败：${e instanceof Error && e.message ? e.message : '请求失败'}` };
  }
}

// ---------- 部署历史（AppDeployHistory） ----------

export interface HistoryRow {
  id: number;
  deployId: number;
  nodeId: number;
  node: string;
  action: string;
  actionLabel: string;
  /** 只表示下发时没抛异常，不是节点执行结果 */
  success: boolean;
  remark: string;
  traceId: string;
  at: string;
  time: string;
  ip: string;
}

/** 动作名里有 install/start 等就翻成中文（兼容 deploy/install/Deploy 这类流水线写的记录），其余原样显示 */
export function actionLabel(action: string): string {
  if (!action) return '—';
  const seg = action
    .toLowerCase()
    .split('/')
    .find((s) => actionInfo(s));
  return (seg && actionInfo(seg)?.label) || action;
}

export function toHistoryRows(rows: Rec[]): HistoryRow[] {
  return rows
    .map((r) => {
      const nodeId = num(pick(r, 'NodeId', 'nodeId'));
      const action = str(pick(r, 'Action', 'action'));
      const at = str(pick(r, 'CreateTime', 'createTime'));
      return {
        id: num(pick(r, 'Id', 'id')),
        deployId: num(pick(r, 'DeployId', 'deployId')),
        nodeId,
        node: str(pick(r, 'NodeName', 'nodeName')) || (nodeId > 0 ? `节点 ${nodeId}` : '—'),
        action,
        actionLabel: actionLabel(action),
        success: bool(pick(r, 'Success', 'success')),
        remark: str(pick(r, 'Remark', 'remark')),
        traceId: str(pick(r, 'TraceId', 'traceId')),
        at,
        time: at ? at.replace('T', ' ').slice(0, 19) : '—',
        ip: str(pick(r, 'CreateIP', 'createIP', 'createIp')),
      };
    })
    .sort((a, b) => (a.at === b.at ? b.id - a.id : a.at < b.at ? 1 : -1));
}

/** GET /Deployment/AppDeployHistory?deployId=。HTTP 上 nodeId 不参与过滤，按节点看要用 filterHistoryByNode 在前端过滤当前页。 */
export async function fetchDeployHistory(request: Req, deployId: number, pageSize = 30): Promise<HistoryRow[] | null> {
  if (!deployId || deployId <= 0) return [];
  try {
    const res = await request({ url: '/Deployment/AppDeployHistory', method: 'GET', params: { deployId, PageSize: pageSize } });
    return toHistoryRows(listOf(res));
  } catch {
    return null;
  }
}

/** nodeId 不大于 0 时返回全部；流水线写的记录多数 NodeId 为 0，按节点过滤时不会出现 */
export function filterHistoryByNode(rows: HistoryRow[], nodeId: number): HistoryRow[] {
  return nodeId > 0 ? rows.filter((r) => r.nodeId === nodeId) : rows;
}

// ---------- 手动刷新的反馈（AGENTS.md §11） ----------

/** 刷新按钮至少旋转的时间：请求再快也要让用户看见确实刷新了 */
export const REFRESH_MIN_SPIN_MS = 300;

/** 请求已用 elapsedMs，还要再转多久才满 minMs；已满返回 0，时钟异常（负数、NaN）按没用时间算 */
export function refreshRemainMs(elapsedMs: number, minMs = REFRESH_MIN_SPIN_MS): number {
  const used = Number.isFinite(elapsedMs) && elapsedMs > 0 ? elapsedMs : 0;
  return Math.max(0, minMs - used);
}

/** 一次刷新结束后各区块的加载结果 */
export interface RefreshOutcome {
  loadError: string;
  versionsFailed: boolean;
  nodesFailed: boolean;
  historyFailed: boolean;
}

export interface RefreshFeedback {
  ok: boolean;
  text: string;
}

/** 刷新后要给用户的提示；全部成功返回 null（成功时按钮停止旋转即是反馈） */
export function refreshFeedback(o: RefreshOutcome): RefreshFeedback | null {
  if (o.loadError) return { ok: false, text: `刷新失败：${o.loadError}` };
  if (o.versionsFailed || o.nodesFailed || o.historyFailed) {
    return { ok: false, text: '刷新未完全成功：版本、节点或部署历史有一项加载失败，请看对应区块的提示' };
  }
  return null;
}

// ---------- 流水线运行：筛选 / 详情 / 重跑（CARD-DEP-02，J-DEP-08，2026-11-07） ----------

/** 运行状态下拉（逐字对齐旧壳 0-6 中文：全部/等待/编译中/上传成功/部署中/成功/失败/已取消）。
 *  2 UploadSucceeded 实际不出现（代码从不赋值），仅作枚举完整性保留（侦察 ①1.2）。 */
export const RUN_STATUS_FILTERS: { value: number; label: string }[] = [
  { value: -1, label: '全部' },
  { value: 0, label: '等待' },
  { value: 1, label: '编译中' },
  { value: 2, label: '上传成功' },
  { value: 3, label: '部署中' },
  { value: 4, label: '成功' },
  { value: 5, label: '失败' },
  { value: 6, label: '已取消' },
];

export interface RunFilter {
  /** -1 = 全部；0-6 对齐 PipelineStatus */
  status: number;
  /** YYYY-MM-DD；空=不限（后端按 CreateTime 过滤） */
  dtStart: string;
  dtEnd: string;
}

export const EMPTY_RUN_FILTER: RunFilter = { status: -1, dtStart: '', dtEnd: '' };

export function isRunFilterActive(f: RunFilter): boolean {
  return f.status >= 0 || !!f.dtStart || !!f.dtEnd;
}

/** 后端 Search 参数：只带生效项（status=-1 不带、空日期不带），与 Search(pipelineId,status,dtStart,dtEnd) 对齐 */
export function runFilterParams(f: RunFilter): { status?: number; dtStart?: string; dtEnd?: string } {
  const p: { status?: number; dtStart?: string; dtEnd?: string } = {};
  if (f.status >= 0) p.status = f.status;
  if (f.dtStart) p.dtStart = f.dtStart;
  if (f.dtEnd) p.dtEnd = f.dtEnd;
  return p;
}

/** 按筛选取运行列表：逐流水线带 status/dtStart/dtEnd 查询（appsModel.fetchRunHistory 不带筛选参数，故在此拼装），合并去重。 */
export async function fetchRunHistoryFiltered(
  request: Req,
  pipelines: { id?: number; name?: string }[],
  filter: RunFilter,
): Promise<RunHistoryRow[]> {
  const params = runFilterParams(filter);
  const batches: PipelineRunRow[][] = [];
  for (const p of pipelines) {
    if (!p.id) continue;
    try {
      const res = await request({ url: '/Deployment/AppPipelineRun', method: 'GET', params: { pipelineId: p.id, ...params, PageSize: 20 } });
      batches.push(listOf(res) as PipelineRunRow[]);
    } catch {
      batches.push([]);
    }
  }
  return mergeRunHistory(batches);
}

// ---------- 运行详情（Api Detail：run 全字段 + steps[]，侦察 ①1.1） ----------

/** 详情里的步骤行：九字段全集（含格子不展示的 CommandId/StartedTime/FinishedTime，J-DEP-09 补承载） */
export interface StepDetailRow extends StepRow {
  startText: string;
  endText: string;
}

export function toStepDetailRows(rows: Rec[]): StepDetailRow[] {
  return toStepRows(rows)
    .map((s, i) => {
      const raw = rows[i] as Rec;
      return {
        ...s,
        startText: fmtTime(str(pick(raw, 'StartedTime', 'startedTime'))),
        endText: fmtTime(str(pick(raw, 'FinishedTime', 'finishedTime'))),
      };
    })
    .sort((a, b) => a.stepIndex - b.stepIndex || a.id - b.id);
}

export interface RunDetail {
  id: number;
  pipelineId: number;
  /** 容器按流水线列表回填；找不到为空串 */
  pipelineName: string;
  status: string;
  statusLabel: string;
  tone: ShipTone;
  triggerSource: string;
  branch: string;
  commitId: string;
  commitMessage: string;
  commitAuthor: string;
  commitTime: string;
  buildNodeId: number;
  appVersionId: number;
  /** 容器按版本列表回填 */
  versionLabel: string;
  buildStartedTime: string;
  buildFinishedTime: string;
  deployStartedTime: string;
  deployFinishedTime: string;
  traceId: string;
  /** 后端返回才渲染链接（侦察：WebApi Detail 投影列 TraceId，TraceUrl 未核实——有则用） */
  traceUrl: string;
  remark: string;
  createTime: string;
  updateTime: string;
  steps: StepDetailRow[];
  active: boolean;
}

export function toRunDetail(d: Rec): RunDetail {
  const status = normalizeRunStatus(pick(d, 'Status', 'status'));
  const t = runTone(status);
  return {
    id: num(pick(d, 'Id', 'id')),
    pipelineId: num(pick(d, 'PipelineId', 'pipelineId')),
    pipelineName: '',
    status,
    statusLabel: t.label,
    tone: t.tone,
    triggerSource: str(pick(d, 'TriggerSource', 'triggerSource')),
    branch: str(pick(d, 'Branch', 'branch')),
    commitId: str(pick(d, 'CommitId', 'commitId')),
    commitMessage: str(pick(d, 'CommitMessage', 'commitMessage')),
    commitAuthor: str(pick(d, 'CommitAuthor', 'commitAuthor')),
    commitTime: fmtTime(str(pick(d, 'CommitTime', 'commitTime'))),
    buildNodeId: num(pick(d, 'BuildNodeId', 'buildNodeId')),
    appVersionId: num(pick(d, 'AppVersionId', 'appVersionId')),
    versionLabel: '',
    buildStartedTime: fmtTime(str(pick(d, 'BuildStartedTime', 'buildStartedTime'))),
    buildFinishedTime: fmtTime(str(pick(d, 'BuildFinishedTime', 'buildFinishedTime'))),
    deployStartedTime: fmtTime(str(pick(d, 'DeployStartedTime', 'deployStartedTime'))),
    deployFinishedTime: fmtTime(str(pick(d, 'DeployFinishedTime', 'deployFinishedTime'))),
    traceId: str(pick(d, 'TraceId', 'traceId')),
    traceUrl: str(pick(d, 'TraceUrl', 'traceUrl')),
    remark: str(pick(d, 'Remark', 'remark')),
    createTime: fmtTime(str(pick(d, 'CreateTime', 'createTime'))),
    updateTime: fmtTime(str(pick(d, 'UpdateTime', 'updateTime'))),
    steps: toStepDetailRows((d.steps as Rec[]) ?? []),
    active: isRunActive(status),
  };
}

/** 耗时（秒）：终点=部署完成，缺省编译完成；起点=编译开始；任一缺失返回 null */
export function runElapsedSeconds(d: RunDetail): number | null {
  const end = d.deployFinishedTime !== '—' ? d.deployFinishedTime : d.buildFinishedTime !== '—' ? d.buildFinishedTime : '';
  const start = d.buildStartedTime !== '—' ? d.buildStartedTime : '';
  if (!end || !start) return null;
  const e = new Date(end.replace(' ', 'T')).getTime();
  const s = new Date(start.replace(' ', 'T')).getTime();
  if (!Number.isFinite(e) || !Number.isFinite(s)) return null;
  return Math.max(0, Math.round((e - s) / 1000));
}

/** 详情取数：GET /Deployment/AppPipelineRun/Detail?id=（WebApi JSON，含 steps[]）。
 *  记录不存在（后端 code 500「Run record does not exist.」）返回 'missing'；请求异常（网络等）返回 null。 */
export async function fetchRunDetail(request: Req, id: number): Promise<RunDetail | 'missing' | null> {
  if (!id || id <= 0) return 'missing';
  try {
    const res = await request({ url: '/Deployment/AppPipelineRun/Detail', method: 'GET', params: { id } });
    const r = okRes(res);
    const d = r.data as Rec | undefined;
    if (!r.ok || !d) return 'missing';
    return toRunDetail(d);
  } catch (e) {
    const st = num((e as { status?: unknown; response?: { status?: unknown } } | null)?.status)
      || num((e as { response?: { status?: unknown } } | null)?.response?.status);
    return st === 500 ? 'missing' : null;
  }
}

/** 重跑：GET /Deployment/AppPipelineRun/Reprocess?id=（成功 message「流水线已重新排队，运行编号[N]」） */
export async function reprocessRun(request: Req, id: number): Promise<{ ok: boolean; message: string }> {
  if (!id || id <= 0) return { ok: false, message: '缺少运行编号' };
  try {
    const res = await request({ url: '/Deployment/AppPipelineRun/Reprocess', method: 'GET', params: { id } });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? r.message || '已重新排队流水线' : r.message || '重跑失败' };
  } catch (e) {
    return { ok: false, message: e instanceof Error && e.message ? `重跑失败：${e.message}` : '重跑失败' };
  }
}

/** 取消运行：GET /Deployment/AppPipelineRun/Cancel?id=（逻辑取消：阻止后续自动部署，不中断节点上已开始的进程） */
export async function cancelRun(request: Req, id: number): Promise<{ ok: boolean; message: string }> {
  if (!id || id <= 0) return { ok: false, message: '缺少运行编号' };
  try {
    const res = await request({ url: '/Deployment/AppPipelineRun/Cancel', method: 'GET', params: { id } });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? r.message || '已取消运行' : r.message || '取消失败' };
  } catch (e) {
    return { ok: false, message: e instanceof Error && e.message ? `取消失败：${e.message}` : '取消失败' };
  }
}

/** 节点名兜底：节点列表没有该 Id 时显示「节点 {Id}」（J-DEP-09） */
export function nodeNameOf(nodes: ShipNode[], id: number): string {
  const hit = nodes.find((n) => n.nodeId === id);
  return hit?.name || (id > 0 ? `节点 ${id}` : '—');
}

// ---------- D5 应用联想（CARD-DEP-08，J-REG-01 §8 行 9 移交项，候选 A 定稿） ----------

/** 联想候选：注册应用精简档（AppController.AppSearch 返回 Id/Name/DisplayName/Category） */
export interface AppOption {
  id: number;
  name: string;
  displayName: string;
}

/** GET /Registry/App/AppSearch?key=：enable 在服务端固定 true、PageSize 20（AppController.cs:75），
 *  参数名是 key（定稿卡「?name=」为笔误，已由 lead 转台账登记）。空关键字不发请求；失败返回 []，由组件静默不弹候选。 */
export async function fetchAppOptions(request: Req, key: string): Promise<AppOption[]> {
  const kw = key.trim();
  if (!kw) return [];
  try {
    const res = await request({ url: '/Registry/App/AppSearch', method: 'GET', params: { key: kw } });
    return listOf(res)
      .map((r) => ({
        id: num(pick(r, 'Id', 'id')),
        name: str(pick(r, 'Name', 'name')),
        displayName: str(pick(r, 'DisplayName', 'displayName')),
      }))
      .filter((o) => o.id > 0);
  } catch {
    return [];
  }
}

/** 联想条目文案：有显示名时「name（displayName）」，与旧壳 /Registry/App 联想框一致（App/index.vue:212） */
export function toLovItem(o: AppOption): { value: string; id: number } {
  return { value: o.displayName ? `${o.name}（${o.displayName}）` : o.name, id: o.id };
}
