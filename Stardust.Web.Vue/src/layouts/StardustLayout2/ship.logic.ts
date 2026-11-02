// 发布页纯逻辑（无 Vue 依赖，request 由调用方注入，便于单测）。
// 对照原型 page-ship.jsx 的 ShipPanel。原型的版本、历史、灰度动画是演示数据，这里只接真实接口：
//  - 部署集：GET /Deployment/AppDeploy?appId=
//  - 版本：GET /Deployment/AppDeployVersion?deployId=
//  - 节点：GET /Deployment/AppDeployNode?deployId=
//  - 按节点结果：GET /Deployment/AppPipelineStep?runId=（只有流水线发起的部署才有步骤）
//  - 发起部署：GET /Deployment/AppDeployVersion/UseVersion?id=（响应结构未核实，见 useVersion）
// 响应字段大小写两种都认（后端 JSON 与旧 MVC 方言）。取数失败返回 null，由页面显示「加载失败」，不造数据。
import { normalizeRunStatus, okRes, triggerLabel, isRunActive } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import type { Req, RunHistoryRow } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

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
  version: string;
  enable: boolean;
  autoPublish: boolean;
  /** 开启后各节点可以运行不同版本，此时节点版本与部署集版本不一致不一定是异常（列表接口是否返回该字段未核实，缺失按 false） */
  multiVersion: boolean;
}
export type DeployLookup = { status: 'ok'; deploy: DeployInfo } | { status: 'none' } | { status: 'failed' };

export async function fetchDeploy(request: Req, appId: number): Promise<DeployLookup> {
  if (!appId || appId <= 0) return { status: 'none' };
  try {
    const res = await request({ url: '/Deployment/AppDeploy', method: 'GET', params: { appId, PageSize: 20 } });
    const hit = listOf(res).find((r) => num(pick(r, 'AppId', 'appId')) === appId);
    if (!hit) return { status: 'none' };
    return {
      status: 'ok',
      deploy: {
        id: num(pick(hit, 'Id', 'id')),
        appId,
        version: str(pick(hit, 'Version', 'version')),
        enable: bool(pick(hit, 'Enable', 'enable')),
        autoPublish: bool(pick(hit, 'AutoPublish', 'autoPublish')),
        multiVersion: bool(pick(hit, 'MultiVersion', 'multiVersion')),
      },
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
      return { id, version, disabled: reason !== '', reason, current, label };
    })
    .sort((a, b) => b.id - a.id);
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
  version: string;
  mode: number;
  modeLabel: string;
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
      mode,
      modeLabel: modeLabel(mode),
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
