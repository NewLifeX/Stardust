// 应用工作台注册侧五页签的纯逻辑（无 Vue 依赖，request 由调用方注入）。
// 对照 Doc/MVC功能真源：应用历史 / 应用命令 / 应用性能 / 应用服务 / 应用消费。
// 列表都按 appId 取；筛选项与对应 Controller.Search 的查询串一致。
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

type Rec = Record<string, unknown>;

export type ObserveKind = 'appHistory' | 'commands' | 'meter' | 'services' | 'consume';

export interface ObserveQuery {
  client: string;
  action: string;
  /** 历史：'' 全部，'1' 成功，'0' 失败（与 MVC 下拉值一致） */
  success: '' | '1' | '0';
  command: string;
  q: string;
  dtStart: string;
  dtEnd: string;
  clientId: string;
  source: string;
  /** 注册/消费：'' 全部，'true' 启用，'false' 停用 */
  enable: '' | 'true' | 'false';
  nodeId: string;
}

export function emptyObserveQuery(): ObserveQuery {
  return {
    client: '',
    action: '',
    success: '',
    command: '',
    q: '',
    dtStart: '',
    dtEnd: '',
    clientId: '',
    source: '',
    enable: '',
    nodeId: '',
  };
}

function one(raw: Record<string, unknown> | undefined, key: string): string {
  const v = raw?.[key];
  const x = Array.isArray(v) ? v[0] : v;
  return typeof x === 'string' ? x : '';
}

/** 路由 query → 筛选。非法的 success/enable 当「全部」。 */
export function toObserveQuery(raw: Record<string, unknown> | undefined): ObserveQuery {
  const success = one(raw, 'success');
  const enable = one(raw, 'enable');
  return {
    ...emptyObserveQuery(),
    client: one(raw, 'client'),
    action: one(raw, 'action'),
    success: success === '1' || success === '0' ? success : '',
    command: one(raw, 'command'),
    q: one(raw, 'q'),
    dtStart: one(raw, 'dtStart'),
    dtEnd: one(raw, 'dtEnd'),
    clientId: one(raw, 'clientId'),
    source: one(raw, 'source'),
    enable: enable === 'true' || enable === 'false' ? enable : '',
    nodeId: one(raw, 'nodeId'),
  };
}

/** 非空筛选项写成路由 query */
export function observeQueryToRoute(q: ObserveQuery): Record<string, string> {
  const out: Record<string, string> = {};
  for (const [k, v] of Object.entries(q)) {
    if (v) out[k] = v;
  }
  return out;
}

/** AppMeter：多 IP 只取逗号前一段（与 AppMeterController 一致） */
export function firstClientId(raw: string): string {
  const text = raw.trim();
  const i = text.indexOf(',');
  return i > 0 ? text.slice(0, i) : text;
}

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
function boolOrNull(v: unknown): boolean | null {
  if (v === undefined || v === null || v === '') return null;
  if (typeof v === 'boolean') return v;
  if (typeof v === 'number') return v !== 0;
  const s = str(v).trim().toLowerCase();
  if (s === 'true' || s === '1') return true;
  if (s === 'false' || s === '0') return false;
  return null;
}
function listOf(res: unknown): Rec[] {
  if (Array.isArray(res)) return res as Rec[];
  if (res && typeof res === 'object' && Array.isArray((res as { data?: unknown }).data)) {
    return (res as { data: Rec[] }).data;
  }
  return [];
}

export interface HistoryRow {
  id: number;
  client: string;
  node: string;
  action: string;
  success: boolean | null;
  traceId: string;
  remark: string;
  creator: string;
  createTime: string;
  createIp: string;
}

export interface CommandRow {
  id: number;
  command: string;
  argument: string;
  status: string;
  times: string;
  result: string;
  traceId: string;
  expire: string;
  createUser: string;
  updateTime: string;
}

export interface MeterRow {
  id: number;
  clientId: string;
  node: string;
  source: string;
  memory: number;
  cpu: number;
  threads: number;
  handles: number;
  connections: number;
  heap: number;
  gc: number;
  time: string;
}

export interface ServiceRow {
  id: number;
  serviceName: string;
  client: string;
  node: string;
  nodeId: number;
  enable: boolean | null;
  address: string;
}

export interface ConsumeRow extends ServiceRow {
  scope: string;
}

export function toHistoryRows(rows: Rec[]): HistoryRow[] {
  return rows.map((r) => ({
    id: num(pick(r, 'Id', 'id')),
    client: str(pick(r, 'Client', 'client')),
    node: str(pick(r, 'NodeName', 'nodeName')),
    action: str(pick(r, 'Action', 'action')),
    success: boolOrNull(pick(r, 'Success', 'success')),
    traceId: str(pick(r, 'TraceId', 'traceId')),
    remark: str(pick(r, 'Remark', 'remark')),
    creator: str(pick(r, 'Creator', 'creator', 'CreateUser', 'createUser')),
    createTime: str(pick(r, 'CreateTime', 'createTime')),
    createIp: str(pick(r, 'CreateIP', 'createIP', 'createIp')),
  }));
}

export function toCommandRows(rows: Rec[]): CommandRow[] {
  return rows.map((r) => ({
    id: num(pick(r, 'Id', 'id')),
    command: str(pick(r, 'Command', 'command')),
    argument: str(pick(r, 'Argument', 'argument')),
    status: str(pick(r, 'Status', 'status')),
    times: str(pick(r, 'Times', 'times')),
    result: str(pick(r, 'Result', 'result')),
    traceId: str(pick(r, 'TraceId', 'traceId')),
    expire: str(pick(r, 'Expire', 'expire')),
    createUser: str(pick(r, 'CreateUser', 'createUser')),
    updateTime: str(pick(r, 'UpdateTime', 'updateTime')),
  }));
}

export function toMeterRows(rows: Rec[]): MeterRow[] {
  return rows.map((r) => ({
    id: num(pick(r, 'Id', 'id')),
    clientId: str(pick(r, 'ClientId', 'clientId')),
    node: str(pick(r, 'NodeName', 'nodeName')) || (num(pick(r, 'NodeId', 'nodeId')) ? `#${num(pick(r, 'NodeId', 'nodeId'))}` : ''),
    source: str(pick(r, 'Source', 'source')),
    memory: num(pick(r, 'Memory', 'memory')),
    cpu: num(pick(r, 'CpuUsage', 'cpuUsage')),
    threads: num(pick(r, 'Threads', 'threads')),
    handles: num(pick(r, 'Handles', 'handles')),
    connections: num(pick(r, 'Connections', 'connections')),
    heap: num(pick(r, 'HeapSize', 'heapSize')),
    gc: num(pick(r, 'GCCount', 'gcCount')),
    time: str(pick(r, 'Time', 'time')) || str(pick(r, 'CreateTime', 'createTime')),
  }));
}

export function toServiceRows(rows: Rec[]): ServiceRow[] {
  return rows.map((r) => ({
    id: num(pick(r, 'Id', 'id')),
    serviceName: str(pick(r, 'ServiceName', 'serviceName')),
    client: str(pick(r, 'Client', 'client')),
    node: str(pick(r, 'NodeName', 'nodeName')),
    nodeId: num(pick(r, 'NodeId', 'nodeId')),
    enable: boolOrNull(pick(r, 'Enable', 'enable')),
    address: str(pick(r, 'Address', 'address')),
  }));
}

export function toConsumeRows(rows: Rec[]): ConsumeRow[] {
  return toServiceRows(rows).map((row, i) => ({
    ...row,
    scope: str(pick(rows[i] ?? {}, 'Scope', 'scope')),
  }));
}

const URL: Record<ObserveKind, string> = {
  appHistory: '/Registry/AppHistory',
  commands: '/Registry/AppCommand',
  meter: '/Registry/AppMeter',
  services: '/Registry/AppService',
  consume: '/Registry/AppConsume',
};

/**
 * 查询参数。appId 非法返回 null（调用方不发请求）。
 * 性能页 PageSize 固定 20：AppMeterController 只在 PageSize==20 时改成约 24 小时的条数。
 */
export function buildObserveParams(
  kind: ObserveKind,
  appId: number,
  query: ObserveQuery,
): Record<string, string | number> | null {
  if (!appId || appId <= 0) return null;
  const p: Record<string, string | number> = { appId };
  if (kind === 'meter') {
    p.PageSize = 20;
    const clientId = firstClientId(query.clientId);
    if (clientId) p.clientId = clientId;
    if (query.source.trim()) p.source = query.source.trim();
  } else {
    p.PageSize = 50;
  }
  if (kind === 'appHistory') {
    if (query.client.trim()) p.client = query.client.trim();
    if (query.action.trim()) p.action = query.action.trim();
    if (query.success) p.success = query.success;
    if (query.nodeId.trim()) p.nodeId = query.nodeId.trim();
  }
  if (kind === 'commands' && query.command.trim()) p.command = query.command.trim();
  if ((kind === 'services' || kind === 'consume') && query.client.trim()) p.client = query.client.trim();
  if ((kind === 'services' || kind === 'consume') && query.enable) p.enable = query.enable;
  if (kind !== 'meter' && query.q.trim()) p.Q = query.q.trim();
  if (query.dtStart) p.dtStart = query.dtStart;
  if (query.dtEnd) p.dtEnd = query.dtEnd;
  return p;
}

export type ObserveResult =
  | { kind: 'appHistory'; rows: HistoryRow[] }
  | { kind: 'commands'; rows: CommandRow[] }
  | { kind: 'meter'; rows: MeterRow[] }
  | { kind: 'services'; rows: ServiceRow[] }
  | { kind: 'consume'; rows: ConsumeRow[] };

export async function fetchObserve(
  request: Req,
  kind: ObserveKind,
  appId: number,
  query: ObserveQuery,
): Promise<ObserveResult | null> {
  const params = buildObserveParams(kind, appId, query);
  if (!params) {
    if (kind === 'appHistory') return { kind, rows: [] };
    if (kind === 'commands') return { kind, rows: [] };
    if (kind === 'meter') return { kind, rows: [] };
    if (kind === 'services') return { kind, rows: [] };
    return { kind: 'consume', rows: [] };
  }
  try {
    const res = await request({ url: URL[kind], method: 'GET', params });
    const rows = listOf(res);
    if (kind === 'appHistory') return { kind, rows: toHistoryRows(rows) };
    if (kind === 'commands') return { kind, rows: toCommandRows(rows) };
    if (kind === 'meter') return { kind, rows: toMeterRows(rows) };
    if (kind === 'services') return { kind, rows: toServiceRows(rows) };
    return { kind: 'consume', rows: toConsumeRows(rows) };
  } catch {
    return null;
  }
}

export interface MeterChart {
  clientId: string;
  points: MeterRow[];
}

/** 只有一个实例时才画曲线；多个实例让用户点「实例」列再看（与 MVC 推断唯一 clientId 一致）。 */
export function meterChart(rows: MeterRow[]): MeterChart | null {
  const ids = [...new Set(rows.map((r) => r.clientId).filter((id) => id !== ''))];
  if (ids.length !== 1) return null;
  const points = [...rows].sort((a, b) => a.time.localeCompare(b.time) || a.id - b.id);
  return { clientId: ids[0], points };
}

/** 折线点串。各序列按自身最小最大值拉伸，只看走势。 */
export function sparkPoints(values: number[], width: number, height: number): string {
  if (!values.length || width <= 0 || height <= 0) return '';
  const min = Math.min(...values);
  const max = Math.max(...values);
  const span = max - min || 1;
  return values
    .map((v, i) => {
      const x = values.length === 1 ? width / 2 : (i / (values.length - 1)) * width;
      const y = height - ((v - min) / span) * (height - 2) - 1;
      return `${x.toFixed(1)},${y.toFixed(1)}`;
    })
    .join(' ');
}

export function formatBytes(n: number): string {
  if (!Number.isFinite(n) || n < 0) return '—';
  if (n === 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let v = n;
  let i = 0;
  while (v >= 1024 && i < units.length - 1) {
    v /= 1024;
    i += 1;
  }
  const text = v >= 10 || i === 0 ? String(Math.round(v)) : v.toFixed(1);
  return `${text} ${units[i]}`;
}

export function formatCpu(n: number): string {
  if (!Number.isFinite(n)) return '—';
  return Number.isInteger(n) ? `${n}%` : `${n.toFixed(1)}%`;
}
