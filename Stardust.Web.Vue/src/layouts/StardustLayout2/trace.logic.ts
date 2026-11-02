// 调用链页签纯逻辑（无 Vue 依赖，request 由调用方注入，便于单测）。
// 真实数据来源：
//  - 采样片段列表  GET /Monitors/SampleData?appId=      （SampleData 本身无 AppId 列，后端经 TraceData 子查询按应用下钻今日数据）
//  - 整段链路片段  GET /Monitors/SampleData?traceId=    （按追踪标识取该链路全部片段，用于瀑布）
// 调用链列表由前端按 TraceId 聚合采样片段得到（后端没有「一次请求返回 N 条 trace」的接口）。
// 响应字段大小写两种都认（后端 JSON 与旧 MVC 方言）。取数失败返回 null，由组件显示「加载失败」，不造数据。
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
  return v === undefined || v === null ? '' : String(v);
}
function bool(v: unknown): boolean {
  if (typeof v === 'boolean') return v;
  if (typeof v === 'number') return v !== 0;
  return str(v).trim().toLowerCase() === 'true';
}
function listOf(res: { data?: unknown } | undefined): Rec[] {
  const d = res?.data;
  return Array.isArray(d) ? (d as Rec[]) : [];
}

// ---------- 采样片段 ----------

export interface Span {
  id: number;
  traceId: string;
  spanId: string;
  parentId: string;
  /** 操作名（TraceItem.Name） */
  name: string;
  appName: string;
  success: boolean;
  /** Unix 毫秒 */
  startTime: number;
  endTime: number;
  /** 毫秒 */
  cost: number;
  tag: string;
  error: string;
  clientId: string;
}

/** SampleData 行 → Span；EndTime 缺省时用 StartTime+Cost 兜底 */
export function toSpans(rows: Rec[]): Span[] {
  return rows.map((r) => {
    const startTime = num(pick(r, 'StartTime', 'startTime'));
    const endTime = num(pick(r, 'EndTime', 'endTime'));
    return {
      id: num(pick(r, 'Id', 'id')),
      traceId: str(pick(r, 'TraceId', 'traceId')),
      spanId: str(pick(r, 'SpanId', 'spanId')),
      parentId: str(pick(r, 'ParentId', 'parentId')),
      name: str(pick(r, 'Name', 'name')),
      appName: str(pick(r, 'AppName', 'appName')),
      success: bool(pick(r, 'Success', 'success')),
      startTime,
      endTime: endTime > 0 ? endTime : startTime + num(pick(r, 'Cost', 'cost')),
      cost: num(pick(r, 'Cost', 'cost')),
      tag: str(pick(r, 'Tag', 'tag')),
      error: str(pick(r, 'Error', 'error')),
      clientId: str(pick(r, 'ClientId', 'clientId')),
    };
  });
}

// ---------- 调用链列表（按 TraceId 聚合） ----------

export interface TraceSummary {
  traceId: string;
  /** 根片段名称；取不到时退化为 traceId */
  name: string;
  /** 任一片段失败则整链失败 */
  ok: boolean;
  /** 整段墙钟耗时（毫秒）= maxEnd - minStart */
  cost: number;
  /** 最早开始时间（Unix 毫秒），用于排序 */
  startAt: number;
  spanCount: number;
}

/** 片段名兜底：操作名优先，其次应用名 */
export function spanLabel(s: Pick<Span, 'name' | 'appName'>): string {
  return s.name || s.appName || '未命名片段';
}

export function buildTraceList(spans: Span[], limit = 30): TraceSummary[] {
  const groups = new Map<string, Span[]>();
  for (const s of spans) {
    if (!s.traceId) continue;
    const g = groups.get(s.traceId) ?? [];
    g.push(s);
    groups.set(s.traceId, g);
  }
  const out: TraceSummary[] = [];
  for (const [traceId, list] of groups) {
    let minStart = Number.POSITIVE_INFINITY;
    let maxEnd = 0;
    let ok = true;
    for (const s of list) {
      if (s.startTime < minStart) minStart = s.startTime;
      if (s.endTime > maxEnd) maxEnd = s.endTime;
      if (!s.success) ok = false;
    }
    if (!Number.isFinite(minStart)) minStart = 0;
    const ids = new Set(list.map((s) => s.spanId));
    const root =
      list.find((s) => !s.parentId || !ids.has(s.parentId)) ?? [...list].sort((a, b) => a.startTime - b.startTime)[0];
    out.push({
      traceId,
      name: root ? spanLabel(root) : traceId,
      ok,
      cost: maxEnd > minStart ? Math.round(maxEnd - minStart) : 0,
      startAt: minStart,
      spanCount: list.length,
    });
  }
  return out.sort((a, b) => b.startAt - a.startAt).slice(0, limit);
}

// ---------- 瀑布 ----------

export interface WaterfallBar {
  key: string;
  name: string;
  cost: number;
  /** 相对起点偏移百分比 0-100 */
  startOffset: number;
  /** 宽度百分比 */
  widthPct: number;
  ok: boolean;
  /** 沿 ParentId 链的深度，用于缩进 */
  depth: number;
  error: string;
}

export function buildWaterfall(spans: Span[]): WaterfallBar[] {
  if (spans.length === 0) return [];
  let min = Number.POSITIVE_INFINITY;
  let max = Number.NEGATIVE_INFINITY;
  for (const s of spans) {
    const st = s.startTime;
    const et = s.endTime > 0 ? s.endTime : st + s.cost;
    if (st < min) min = st;
    if (et > max) max = et;
  }
  if (!Number.isFinite(min)) min = 0;
  if (max <= min) max = min + 1;
  const total = max - min;
  const ids = new Set(spans.map((s) => s.spanId));
  const depthMap = new Map<string, number>();
  const depthOf = (s: Span): number => {
    if (!s.parentId || !ids.has(s.parentId)) return 0;
    if (depthMap.has(s.spanId)) return depthMap.get(s.spanId) ?? 0;
    const parent = spans.find((x) => x.spanId === s.parentId);
    const d = parent ? depthOf(parent) + 1 : 0;
    depthMap.set(s.spanId, d);
    return d;
  };
  return [...spans]
    .sort((a, b) => a.startTime - b.startTime)
    .map((s, i) => {
      const st = s.startTime;
      const et = s.endTime > 0 ? s.endTime : st + s.cost;
      return {
        key: s.spanId || `span-${i}`,
        name: spanLabel(s),
        cost: et - st > 0 ? Math.round(et - st) : s.cost,
        startOffset: ((st - min) / total) * 100,
        widthPct: Math.max(0.5, ((et - st) / total) * 100),
        ok: s.success,
        depth: depthOf(s),
        error: s.error,
      };
    });
}

// ---------- 诊断框（从真实片段推导，不造数据） ----------

export interface TraceDiag {
  /** 整段墙钟耗时（毫秒） */
  cost: number;
  spanCount: number;
  errorCount: number;
  slowestName: string;
  slowestCost: number;
  /** 最耗时片段占整段耗时百分比 0-100 */
  slowestPct: number;
}

export function summarizeTrace(spans: Span[]): TraceDiag {
  let min = Number.POSITIVE_INFINITY;
  let max = Number.NEGATIVE_INFINITY;
  let errorCount = 0;
  let slowest: Span | null = null;
  for (const s of spans) {
    const st = s.startTime;
    const et = s.endTime > 0 ? s.endTime : st + s.cost;
    if (st < min) min = st;
    if (et > max) max = et;
    if (!s.success) errorCount++;
    const dur = et - st > 0 ? et - st : s.cost;
    if (!slowest || dur > (slowest.endTime - slowest.startTime > 0 ? slowest.endTime - slowest.startTime : slowest.cost)) {
      slowest = s;
    }
  }
  if (!Number.isFinite(min)) min = 0;
  const cost = max > min ? Math.round(max - min) : 0;
  const slowCost = slowest
    ? Math.round(
        slowest.endTime > slowest.startTime ? slowest.endTime - slowest.startTime : slowest.cost,
      )
    : 0;
  return {
    cost,
    spanCount: spans.length,
    errorCount,
    slowestName: slowest ? spanLabel(slowest) : '',
    slowestCost: slowCost,
    slowestPct: cost > 0 ? Math.round((slowCost / cost) * 100) : 0,
  };
}

// ---------- 展示小工具 ----------

/** TraceId 太长：保留前 16 位加省略号 */
export function shortId(id: string, len = 16): string {
  if (!id) return '—';
  return id.length > len ? `${id.slice(0, len)}…` : id;
}

// ---------- 取数（request 注入；失败返回 null） ----------

/** 本地日期字符串 YYYY-MM-DD。后端 SampleDataController.Search 的应用下钻分支要求 time 参数：
 *  不传时 time=MinValue → Meta.CreateShard 抛「分表策略要求指定时间」，整页假失败（2026-11-02 浏览器实证）。 */
export function localDateStr(d = new Date()): string {
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
}

/** GET /Monitors/SampleData?appId=&time=：该应用当日采样片段（分表下钻，time 必传） */
export async function fetchAppSamples(request: Req, appId: number, pageSize = 100): Promise<Span[] | null> {
  if (!appId || appId <= 0) return [];
  try {
    const res = await request({
      url: '/Monitors/SampleData',
      method: 'GET',
      params: { appId, time: localDateStr(), PageSize: pageSize },
    });
    return toSpans(listOf(res));
  } catch {
    return null;
  }
}

/** 列表：采样片段按 TraceId 聚合成调用链 */
export async function fetchTraceSummaries(request: Req, appId: number): Promise<TraceSummary[] | null> {
  const spans = await fetchAppSamples(request, appId);
  if (spans === null) return null;
  return buildTraceList(spans);
}

/** 详情：GET /Monitors/SampleData?traceId= 整段链路片段（后端自动把 PageSize 提到 500） */
export async function fetchTraceSpans(request: Req, traceId: string, pageSize = 500): Promise<Span[] | null> {
  if (!traceId) return [];
  try {
    const res = await request({ url: '/Monitors/SampleData', method: 'GET', params: { traceId, PageSize: pageSize } });
    return toSpans(listOf(res));
  } catch {
    return null;
  }
}
