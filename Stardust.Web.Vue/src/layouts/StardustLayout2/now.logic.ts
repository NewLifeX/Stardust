// 「此刻」页纯逻辑（无 Vue 依赖，request 由调用方注入，便于单测）。
//
// 数据源（已读后端控制器与实体确认，2026-10-30）：
//  - 告警     GET /Monitors/AlarmRecord?status=1   （AlarmStatuses.Alaraming=1，报警中）
//             实体字段 Id/Name/Category/Action/Status/StartTime/Duration(秒)/Times/Content/UpdateTime
//             注意：AlarmRecord 实体【没有 ProjectId 字段】（已读 Stardust.Data/Monitors/报警记录.cs 确认），
//             后端 Search 只按 groupId/category/status/dtStart/dtEnd/Q 过滤，projectId 对该接口无实际
//             过滤效果；这里仍按页面规范显式带上，与全局拦截器行为一致（不会报错）。
//  - 节点舰队 GET /Nodes/Node（全部节点，含 ProjectId）+ GET /Nodes/NodeOnline（在线节点会话，含 ProjectId）
//             在线判定：NodeOnline.NodeID ∈ Node.ID；高负载：在线行 CpuRate >= 85（原型 x-ev thr=85）。
//  - 应用     复用 appsModel.fetchAppList（/Registry/App + /Registry/AppOnline + /Registry/AppService +
//             /Monitors/AppMinuteStat），取异常优先排序后的前 6 个。
//  - 变更     复用 changes.logic.fetchChanges(request, 5)（已内部处理 projectId 与项目过滤）。
//
// 错误策略：四段取数并行、各自 try/catch，失败段落记入 errors（页面给可见横幅 + 重试），
// 成功段落照常渲染——不因为一段失败把整页打空，也不静默伪装空态。
import { fetchAppList, type AppAggRow, type Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import { fetchChanges, type ChangeItem } from './changes.logic.ts';

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
function listOf(res: { data?: unknown } | undefined): Rec[] {
  const d = res?.data;
  return Array.isArray(d) ? (d as Rec[]) : [];
}
/** 宽松解析时间字符串（兼容 'YYYY-MM-DD HH:mm:ss' 与 ISO），失败返回 0 */
function toMs(v: unknown): number {
  const s = str(v);
  if (!s) return 0;
  const t = Date.parse(s.replace(' ', 'T'));
  return Number.isFinite(t) ? t : 0;
}

// ---------- 类型 ----------

/** 告警严重度：danger=下线类（红条），warn=其余（黄条） */
export type IncidentSeverity = 'danger' | 'warn';

/** 归一后的告警卡片（一个 AlarmRecord 一行） */
export interface Incident {
  id: number;
  /** 卡片标题：Name 字段；缺省给兜底文案 */
  title: string;
  /** 类别：应用下线/节点下线/错误数过高等 */
  category: string;
  /** 报警对象：应用名或节点名 */
  action: string;
  /** 持续时长文案（由 Duration 秒数格式化），如 "5 分钟" */
  sinceText: string;
  /** StartTime 相对当前时间文案，如 "3 分钟前"；解析失败为空串 */
  relText: string;
  /** 真实告警详情 Content（最长 2000），放进诊断块；没有为空串 */
  content: string;
  /** 通知次数 */
  times: number;
  severity: IncidentSeverity;
}

/** 舰队方块状态：ok 正常 / high 高负载（CPU≥85）/ off 离线（无在线会话） */
export type FleetStatus = 'ok' | 'high' | 'off';

export interface FleetNode {
  id: number;
  name: string;
  ip: string;
  status: FleetStatus;
  /** 在线时的 CPU 百分比（NodeOnline.CpuRate），用于 title；离线为 0 */
  cpu: number;
}

/** 右栏应用行（取 fetchAppList 结果的前 6） */
export interface NowApp {
  id: number;
  name: string;
  displayName: string;
  category: string;
  version: string;
  instances: number;
  /** 健康点语气：running=ok / error·publishFailed=err / offline=off / 其余 warn */
  health: 'ok' | 'err' | 'warn' | 'off';
}

/** 分段取数的错误信息（键为段落名）；页面逐段渲染可见横幅 */
export interface NowErrors {
  alarms?: string;
  nodes?: string;
  apps?: string;
  changes?: string;
}

export interface NowData {
  incidents: Incident[];
  nodes: FleetNode[];
  onlineNodes: number;
  totalNodes: number;
  instanceTotal: number;
  apps: NowApp[];
  changes: ChangeItem[];
}

export interface NowResult {
  data: NowData;
  errors: NowErrors;
}

// ---------- 格式化 ----------

/** Duration 秒 → "N 秒 / N 分钟 / N 小时 N 分 / N 天 N 小时"；<=0 为 "刚刚" */
export function fmtDuration(sec: number): string {
  if (!Number.isFinite(sec) || sec <= 0) return '刚刚';
  if (sec < 60) return `${Math.floor(sec)} 秒`;
  if (sec < 3600) return `${Math.floor(sec / 60)} 分钟`;
  if (sec < 86400) return `${Math.floor(sec / 3600)} 小时 ${Math.floor((sec % 3600) / 60)} 分`;
  return `${Math.floor(sec / 86400)} 天 ${Math.floor((sec % 86400) / 3600)} 小时`;
}

/** StartTime 相对时间 → "刚刚 / N 分钟前 / N 小时前 / N 天前"；无法解析或时间在未来为空串 */
export function relTimeText(startMs: number, nowMs: number): string {
  if (!startMs || !nowMs || nowMs < startMs) return '';
  const diff = (nowMs - startMs) / 1000;
  if (diff < 60) return '刚刚';
  if (diff < 3600) return `${Math.floor(diff / 60)} 分钟前`;
  if (diff < 86400) return `${Math.floor(diff / 3600)} 小时前`;
  return `${Math.floor(diff / 86400)} 天前`;
}

/**
 * 严重度派生：类别含「下线」判 danger（红条），其余判 warn（黄条）。
 * 实体没有 severity 字段，这是按 Category 语义的诚实归一（注释为决策依据）。
 */
export function severityOf(category: string): IncidentSeverity {
  return category.includes('下线') ? 'danger' : 'warn';
}

// ---------- 告警归一 ----------

/** 一行 AlarmRecord → Incident；缺字段给防御性兜底，不抛错 */
export function toIncident(r: Rec, nowMs: number): Incident {
  const duration = num(pick(r, 'Duration', 'duration'));
  const startMs = toMs(pick(r, 'StartTime', 'startTime'));
  const category = str(pick(r, 'Category', 'category'));
  const title = str(pick(r, 'Name', 'name'));
  return {
    id: num(pick(r, 'Id', 'id')),
    title: title || '未命名告警',
    category,
    action: str(pick(r, 'Action', 'action')),
    sinceText: fmtDuration(duration),
    relText: relTimeText(startMs, nowMs),
    content: str(pick(r, 'Content', 'content')),
    times: num(pick(r, 'Times', 'times')),
    severity: severityOf(category),
  };
}

/** 报警中行 → Incident[]（按 Id 倒序，新的在前；接口本身通常已按 UpdateTime 倒序，这里稳定排序兜底） */
export function toIncidents(rows: Rec[], nowMs: number = Date.now()): Incident[] {
  return rows
    .map((r) => toIncident(r, nowMs))
    .sort((a, b) => b.id - a.id);
}

/** 是否有待处理告警（Hero 大标题与空态的判定） */
export function hasIncidents(incidents: Incident[]): boolean {
  return incidents.length > 0;
}

// ---------- 节点舰队 ----------

/** NodeOnline 行 CPU 阈值（原型 x-ev thr=85） */
export const CPU_HIGH_RATE = 85;

/**
 * 全部节点 + 在线会话 → 舰队方块。
 * 在线集合按 NodeID（兼容 NodeId/nodeId）匹配 Node.ID(ID/id)；
 * 高负载：在线且 CpuRate（兼容 cpuRate/Cpu/cpu）>= 85；其余在线为 ok；无在线会话为 off。
 */
export function buildFleet(nodes: Rec[], online: Rec[]): FleetNode[] {
  const onlineIds = new Set<number>();
  const cpuById = new Map<number, number>();
  for (const r of online) {
    const nid = num(pick(r, 'NodeID', 'NodeId', 'nodeId'));
    if (nid > 0) {
      onlineIds.add(nid);
      cpuById.set(nid, num(pick(r, 'CpuRate', 'cpuRate', 'Cpu', 'cpu')));
    }
  }
  return nodes.map((r) => {
    const id = num(pick(r, 'ID', 'Id', 'id'));
    const cpu = id > 0 ? cpuById.get(id) ?? 0 : 0;
    const status: FleetStatus = !onlineIds.has(id) ? 'off' : cpu >= CPU_HIGH_RATE ? 'high' : 'ok';
    return {
      id,
      name: str(pick(r, 'Name', 'name')) || '未命名节点',
      ip: str(pick(r, 'IP', 'ip')),
      status,
      cpu,
    };
  });
}

// ---------- 右栏应用 ----------

/** 应用健康点语气（与 apps.logic.dotToneOf 同口径） */
export function healthOf(status: AppAggRow['status']): NowApp['health'] {
  switch (status) {
    case 'running':
      return 'ok';
    case 'error':
    case 'publishFailed':
      return 'err';
    case 'offline':
      return 'off';
    default:
      return 'warn';
  }
}

/**
 * 特征应用：异常/发布失败优先，保持其余原顺序，截断前 limit 个。
 * 与原型 page-now.jsx 的 sort(warn-first).slice(0,6) 同口径。
 */
export function pickFeaturedApps(rows: AppAggRow[], limit = 6): NowApp[] {
  return rows
    .slice()
    .sort((a, b) => {
      const aw = a.status === 'error' || a.status === 'publishFailed' ? 1 : 0;
      const bw = b.status === 'error' || b.status === 'publishFailed' ? 1 : 0;
      return bw - aw;
    })
    .slice(0, limit)
    .map((a) => ({
      id: a.id ?? 0,
      name: a.name ?? '',
      displayName: a.displayName ?? '',
      category: a.category ?? '',
      version: a.version ?? '',
      instances: a.instances ?? 0,
      health: healthOf(a.status),
    }));
}

// ---------- 端到端取数 ----------

/** 显式 projectId：非空时合并进 params（与全局拦截器同向，单元测试用于断言透传） */
function withProject(params: Record<string, unknown>, projectId?: number | null): Record<string, unknown> {
  return projectId ? { ...params, projectId } : { ...params };
}

async function fetchAlarms(request: Req, projectId?: number | null): Promise<Rec[]> {
  const res = await request({
    url: '/Monitors/AlarmRecord',
    method: 'GET',
    params: withProject({ status: 1, PageSize: 50 }, projectId),
  });
  return listOf(res);
}

async function fetchFleet(request: Req, projectId?: number | null): Promise<FleetNode[]> {
  const [nodeRes, onlineRes] = await Promise.all([
    request({ url: '/Nodes/Node', method: 'GET', params: withProject({ PageSize: 500 }, projectId) }),
    request({ url: '/Nodes/NodeOnline', method: 'GET', params: withProject({ PageSize: 500 }, projectId) }),
  ]);
  return buildFleet(listOf(nodeRes), listOf(onlineRes));
}

function errText(e: unknown, fallback: string): string {
  return e instanceof Error && e.message ? e.message : fallback;
}

/**
 * 端到端取数：告警 / 舰队 / 应用 / 变更 四路并行，各自独立 try/catch。
 * 失败段落进 errors（页面逐段可见横幅 + 重试），成功段落照常返回；不造演示数据。
 * projectId 非空时告警与节点请求显式带 params.projectId（AlarmRecord 实体无 ProjectId 字段，
 * 该参数对告警无过滤效果，仅与页面规范保持一致；变更由 fetchChanges 内部处理）。
 */
export async function fetchNow(
  request: Req,
  opts: { projectId?: number | null; nowMs?: number; appLimit?: number; changeLimit?: number } = {},
): Promise<NowResult> {
  const { projectId, nowMs = Date.now(), appLimit = 6, changeLimit = 5 } = opts;
  const errors: NowErrors = {};
  const data: NowData = {
    incidents: [],
    nodes: [],
    onlineNodes: 0,
    totalNodes: 0,
    instanceTotal: 0,
    apps: [],
    changes: [],
  };

  const alarmTask = fetchAlarms(request, projectId)
    .then((rows) => {
      data.incidents = toIncidents(rows, nowMs);
    })
    .catch((e) => {
      errors.alarms = errText(e, '告警加载失败');
    });

  const fleetTask = fetchFleet(request, projectId)
    .then((nodes) => {
      data.nodes = nodes;
      data.totalNodes = nodes.length;
      data.onlineNodes = nodes.filter((n) => n.status !== 'off').length;
    })
    .catch((e) => {
      errors.nodes = errText(e, '节点舰队加载失败');
    });

  const appsTask = fetchAppList(request)
    .then(({ rows }) => {
      data.apps = pickFeaturedApps(rows, appLimit);
      data.instanceTotal = rows.reduce((s, a) => s + (a.instances ?? 0), 0);
    })
    .catch((e) => {
      errors.apps = errText(e, '应用列表加载失败');
    });

  const changesTask = fetchChanges(request, changeLimit)
    .then((r) => {
      data.changes = r.items;
      if (r.error) errors.changes = r.error;
    })
    .catch((e) => {
      errors.changes = errText(e, '最近变更加载失败');
    });

  await Promise.all([alarmTask, fleetTask, appsTask, changesTask]);
  return { data, errors };
}
