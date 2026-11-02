// 跨应用「变更」时间线纯逻辑（无 Vue 依赖，request 由调用方注入，便于单测）。
//
// 数据源（已读后端控制器确认，2026-10-29 停点 20）：
//  - 部署集   GET /Deployment/AppDeploy     （该接口读 projectId，由全局拦截器自动注入）
//  - 应用配置 GET /Configs/AppConfig        （该接口读 projectId）
//  - 流水线   GET /Deployment/AppPipeline   （不带 deployId → 返回全部，不读 projectId）
//  - 部署历史 GET /Deployment/AppDeployHistory（不读 projectId，全局）
//  - 流水线运行 GET /Deployment/AppPipelineRun（不读 projectId，全局）
//  - 配置历史 GET /Configs/ConfigHistory    （不读 projectId，全局）
//
// 关键约束：三张「历史表」本身不读 projectId，全局列表会跨项目返回。必须在前端按
// 「项目内 id 集合」过滤：先用两张读 projectId 的表（部署集、应用配置）建 id→应用 映射，
// 流水线映射再经 DeployId 落到部署集映射；历史行的 deployId/pipelineId/configId 不在
// 本项目集合内的一律丢弃。项目为「全部项目」时 projectId 为空、请求不带过滤参数，
// 映射自然包含全部 id，过滤即全放行——这是正确的。
//
// 合并：部署历史与配置历史用 CreateTime，流水线运行用 UpdateTime（兜底 BuildStartedTime/CreateTime），
// 前端统一 Date 比较后倒序，取前 N 条。取数失败返回 error 字符串，由页面给可见错误提示，不造数据。
import { normalizeRunStatus } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

type Rec = Record<string, unknown>;

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
  return v === undefined || v === null ? '' : String(v).trim();
}
function bool(v: unknown): boolean {
  if (typeof v === 'boolean') return v;
  if (typeof v === 'number') return v !== 0;
  return str(v).toLowerCase() === 'true' || str(v) === '1';
}
function listOf(res: { data?: unknown } | undefined): Rec[] {
  const d = res?.data;
  return Array.isArray(d) ? (d as Rec[]) : [];
}

// ---------- 类型 ----------

/** 变更类型：D=发布/部署（部署历史与流水线），C=配置 */
export type ChangeKind = 'D' | 'C';

export interface ChangeItem {
  /** 唯一 key：`${kind}-${来源行Id}` */
  id: string;
  kind: ChangeKind;
  /** 应用名（来自部署集/配置映射） */
  appName: string;
  /** 应用 Id，用于跳转 */
  appId: number;
  /** 展示时间（YYYY-MM-DD HH:mm，无则 —） */
  time: string;
  /** 排序时间戳（毫秒）：部署/配置取 CreateTime，流水线取 UpdateTime 兜底 */
  sortAt: number;
  /** 摘要（截断） */
  summary: string;
  /** 操作人（部署/配置取 CreateUserId，流水线取 CommitAuthor），没有则空串 */
  operator: string;
  /** 点击跳转目标：/app/<tab>/<appId> */
  path: string;
  /** 成功/失败；流水线进行中状态为 null */
  ok: boolean | null;
}

export interface DeployMapEntry {
  appId: number;
  appName: string;
}
export type ConfigMapEntry = DeployMapEntry;

// ---------- 映射构建 ----------

/** 部署集映射：AppDeploy.Id(deployId) → { appId, appName }。该接口读 projectId */
export function buildDeployMap(rows: Rec[]): Map<number, DeployMapEntry> {
  const m = new Map<number, DeployMapEntry>();
  for (const r of rows) {
    const id = num(pick(r, 'Id', 'id'));
    const appId = num(pick(r, 'AppId', 'appId'));
    if (id > 0 && appId > 0) m.set(id, { appId, appName: str(pick(r, 'AppName', 'appName')) });
  }
  return m;
}

/** 应用配置映射：AppConfig.Id(configId) → { appId, appName（取 Name 字段）}。该接口读 projectId */
export function buildConfigMap(rows: Rec[]): Map<number, ConfigMapEntry> {
  const m = new Map<number, ConfigMapEntry>();
  for (const r of rows) {
    const id = num(pick(r, 'Id', 'id'));
    const appId = num(pick(r, 'AppId', 'appId'));
    if (id > 0 && appId > 0) m.set(id, { appId, appName: str(pick(r, 'Name', 'name')) });
  }
  return m;
}

/** 流水线映射：AppPipeline.Id(pipelineId) → DeployId。该接口不读 projectId（全局） */
export function buildPipelineMap(rows: Rec[]): Map<number, number> {
  const m = new Map<number, number>();
  for (const r of rows) {
    const pid = num(pick(r, 'Id', 'id'));
    const deployId = num(pick(r, 'DeployId', 'deployId'));
    if (pid > 0 && deployId > 0) m.set(pid, deployId);
  }
  return m;
}

// ---------- 时间工具 ----------

/** 宽松解析时间字符串（兼容 'YYYY-MM-DD HH:mm:ss' 与 ISO），失败返回 0 */
function toMs(v: unknown): number {
  const s = str(v);
  if (!s) return 0;
  const t = Date.parse(s.replace(' ', 'T'));
  return Number.isFinite(t) ? t : 0;
}
function fmtTime(ms: number): string {
  if (!ms) return '—';
  const d = new Date(ms);
  const p = (n: number): string => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`;
}
function truncate(s: string, len = 80): string {
  return s.length > len ? `${s.slice(0, len)}…` : s;
}
/** 操作人：数字 id 显示 #id，字符串原样；空则空串（组件再决定是否显示 —） */
function operatorOf(userId: unknown, author = ''): string {
  if (author) return author;
  const n = num(userId);
  if (n > 0) return `#${n}`;
  const s = str(userId);
  return s && s !== '0' ? s : '';
}

// ---------- 三类历史归一 ----------

// 部署动作中文标签（与 DeployHistory 同口径）
const DEPLOY_ACTION: Record<string, string> = {
  install: '安装',
  start: '启动',
  stop: '停止',
  restart: '重启',
  uninstall: '卸载',
  deploy: '部署',
};

function deployActionLabel(action: string): string {
  const key = action.toLowerCase().split('/').pop() ?? '';
  return DEPLOY_ACTION[key] || action || '—';
}

/** 流水线状态 → 中文文案 */
const RUN_STATUS_TEXT: Record<string, string> = {
  Pending: '等待构建',
  Building: '构建中',
  UploadSucceeded: '构建完成待部署',
  Deploying: '部署中',
  Success: '成功',
  Failed: '失败',
  Cancelled: '已取消',
};

/**
 * 部署历史（AppDeployHistory，全局不读 projectId）：只保留 deployId ∈ deployMap 的行。
 * appName 取 deployMap[deployId]，跳转 /app/history/<appId>。
 */
export function toDeployChanges(rows: Rec[], deployMap: Map<number, DeployMapEntry>): ChangeItem[] {
  const out: ChangeItem[] = [];
  for (const r of rows) {
    const deployId = num(pick(r, 'DeployId', 'deployId'));
    const entry = deployMap.get(deployId);
    if (!entry) continue;
    const action = str(pick(r, 'Action', 'action'));
    const remark = str(pick(r, 'Remark', 'remark'));
    const sortAt = toMs(pick(r, 'CreateTime', 'createTime'));
    out.push({
      id: `d-${num(pick(r, 'Id', 'id'))}`,
      kind: 'D',
      appName: entry.appName,
      appId: entry.appId,
      time: fmtTime(sortAt),
      sortAt,
      summary: truncate([deployActionLabel(action), remark].filter(Boolean).join(' · ')),
      operator: operatorOf(pick(r, 'CreateUserId', 'createUserId')),
      path: `/app/history/${entry.appId}`,
      ok: bool(pick(r, 'Success', 'success')),
    });
  }
  return out;
}

/**
 * 流水线运行（AppPipelineRun，全局不读 projectId）：
 * 只保留 pipelineMap 里 deployId ∈ deployMap 的运行（即本项目的流水线）。
 * appName 经 pipelineMap[pipelineId]→deployId→deployMap 两跳取得，跳转 /app/pipeline/<appId>。
 */
export function toPipelineChanges(
  rows: Rec[],
  pipelineMap: Map<number, number>,
  deployMap: Map<number, DeployMapEntry>,
): ChangeItem[] {
  const out: ChangeItem[] = [];
  for (const r of rows) {
    const pipelineId = num(pick(r, 'PipelineId', 'pipelineId'));
    const deployId = pipelineMap.get(pipelineId);
    if (!deployId) continue;
    const entry = deployMap.get(deployId);
    if (!entry) continue;

    const status = normalizeRunStatus(pick(r, 'Status', 'status'));
    const commitMessage = str(pick(r, 'CommitMessage', 'commitMessage'));
    const branch = str(pick(r, 'Branch', 'branch'));
    const statusText = RUN_STATUS_TEXT[status] || '运行中';
    const summary = truncate(commitMessage || branch || statusText);

    const sortAt =
      toMs(pick(r, 'UpdateTime', 'updateTime')) ||
      toMs(pick(r, 'BuildStartedTime', 'buildStartedTime')) ||
      toMs(pick(r, 'CreateTime', 'createTime'));

    const ok: boolean | null = status === 'Success' ? true : status === 'Failed' || status === 'Cancelled' ? false : null;

    out.push({
      id: `p-${num(pick(r, 'Id', 'id'))}`,
      kind: 'D',
      appName: entry.appName,
      appId: entry.appId,
      time: fmtTime(sortAt),
      sortAt,
      summary,
      operator: operatorOf(pick(r, 'CommitAuthor', 'commitAuthor'), str(pick(r, 'CommitAuthor', 'commitAuthor'))),
      path: `/app/pipeline/${entry.appId}`,
      ok,
    });
  }
  return out;
}

/** 配置动作中文文案 */
const CONFIG_ACTION_TEXT: Record<string, string> = {
  Publish: '发布配置',
  Rollback: '回滚配置',
};

/**
 * 配置历史（ConfigHistory，全局不读 projectId）：只保留 configId ∈ configMap 的行。
 * 跳转 /app/config/<appId>。
 */
export function toConfigChanges(rows: Rec[], configMap: Map<number, ConfigMapEntry>): ChangeItem[] {
  const out: ChangeItem[] = [];
  for (const r of rows) {
    const configId = num(pick(r, 'ConfigId', 'configId'));
    const entry = configMap.get(configId);
    if (!entry) continue;
    const action = str(pick(r, 'Action', 'action'));
    const sortAt = toMs(pick(r, 'CreateTime', 'createTime'));
    out.push({
      id: `c-${num(pick(r, 'Id', 'id'))}`,
      kind: 'C',
      appName: entry.appName,
      appId: entry.appId,
      time: fmtTime(sortAt),
      sortAt,
      summary: truncate(CONFIG_ACTION_TEXT[action] || action || '配置变更'),
      operator: operatorOf(pick(r, 'CreateUserID', 'CreateUserId', 'createUserId', 'createUserID')),
      path: `/app/config/${entry.appId}`,
      ok: bool(pick(r, 'Success', 'success')),
    });
  }
  return out;
}

// ---------- 合并 ----------

/** 三类合并：按 sortAt 倒序（同刻按来源 id 倒序），取前 limit 条 */
export function mergeChanges(lists: ChangeItem[], limit = 50): ChangeItem[] {
  return lists
    .slice()
    .sort((a, b) => {
      if (b.sortAt !== a.sortAt) return b.sortAt - a.sortAt;
      const aid = Number(a.id.split('-')[1]) || 0;
      const bid = Number(b.id.split('-')[1]) || 0;
      return bid - aid;
    })
    .slice(0, limit);
}

// ---------- 取数（request 注入） ----------

async function fetchRows(request: Req, url: string, pageSize: number): Promise<Rec[]> {
  const res = await request({ url, method: 'GET', params: { PageSize: pageSize } });
  return listOf(res);
}

/**
 * 端到端取数：并行拉 6 个接口，建三张映射、按项目 id 集合过滤三张历史、合并倒序。
 * 任一请求失败 → 返回 error（不静默空态）。
 */
export async function fetchChanges(
  request: Req,
  limit = 50,
): Promise<{ items: ChangeItem[]; error: string }> {
  try {
    const [deployRows, configRows, pipelineRows, deployHistRows, runRows, configHistRows] = await Promise.all([
      fetchRows(request, '/Deployment/AppDeploy', 500),
      fetchRows(request, '/Configs/AppConfig', 500),
      fetchRows(request, '/Deployment/AppPipeline', 500),
      fetchRows(request, '/Deployment/AppDeployHistory', 100),
      fetchRows(request, '/Deployment/AppPipelineRun', 100),
      fetchRows(request, '/Configs/ConfigHistory', 100),
    ]);
    const deployMap = buildDeployMap(deployRows);
    const configMap = buildConfigMap(configRows);
    const pipelineMap = buildPipelineMap(pipelineRows);
    const items = mergeChanges(
      [
        ...toDeployChanges(deployHistRows, deployMap),
        ...toPipelineChanges(runRows, pipelineMap, deployMap),
        ...toConfigChanges(configHistRows, configMap),
      ],
      limit,
    );
    return { items, error: '' };
  } catch (e) {
    return { items: [], error: e instanceof Error && e.message ? e.message : '变更加载失败' };
  }
}
