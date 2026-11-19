// CARD-MON-11 追踪数据纯逻辑（工作台「监控中心」页签 · 追踪数据区块）。
// 数据真实接口（TraceDataController.cs 现行，零后端改动）：
//   GET  /Monitors/TraceData?appId=&itemId=&name=&searchTag=&dtStart=&dtEnd=&Q=&pageSize=100
//   POST /Monitors/TraceData/Trace?id=  → {traceId}（雪花 Int64 全链 string）
//   POST /Monitors/TraceData/Exclude?id= → JsonRefresh("成功！")（应用采样排除名单）
// 口径：
//   - 雪花铁律：Id/traceId 一律 string 原样透传，禁止 Number() 往返（C12/C13 教训）。
//   - 日期缺省前端补今天规范值（后端 <2000 另有回退保护，前端仍传规范值）。
//   - searchTag 仅 true 落参（后端取逗号首段 ToBoolean，前端如实不过度传）。
//   - 双图显示条件对齐后端 MVC 旧条件：itemId 查询值非空 && 本页行数 > 1。
//   - 左图「调用与错误」按 Name 聚合 Top10（Total 降序）双柱状；右图「平均耗时」按
//     StatHour 升序折线，组内 Total 加权平均（Σ(total×cost)/Σtotal，全零回退算术均值）。
//   - 总计行明示「本页合计」，不冒充分页总量。
import { okRes, type Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import { localDateStr } from './daystat.logic.ts';
import { shortStatTime } from './traceStat.logic.ts';

type Rec = Record<string, unknown>;

function num(v: unknown): number {
  const n = Number(v);
  return Number.isFinite(n) ? n : 0;
}

function str(v: unknown): string {
  return v == null ? '' : String(v);
}

export interface TraceDataQuery {
  appId: number;
  /** 跟踪项 id：>0 才落参（后端 ToInt(-1) 视为不过滤） */
  itemId?: number;
  /** 操作名精确过滤 */
  name?: string;
  /** 仅 true 落参（后端取逗号首段 ToBoolean） */
  searchTag?: boolean;
  /** yyyy-MM-dd，缺省补今天 */
  dtStart?: string;
  dtEnd?: string;
  /** 关键字（后端参数名大写 Q） */
  q?: string;
  /** 默认 100（对齐后端 appId>0 时 20→100 放大） */
  pageSize?: number;
}

export interface TraceDataRow {
  /** 雪花 Int64 全链 string，禁止 Number() 往返 */
  id: string;
  /** 后端原样字符串 'yyyy-MM-dd HH:mm:ss' */
  statMinute: string;
  statHour: string;
  clientId: string;
  name: string;
  total: number;
  errors: number;
  totalCost: number;
  cost: number;
  maxCost: number;
  minCost: number;
  /** Unix 毫秒（实体 Int64），展示经 fmtUnixMs */
  startTime: number;
  endTime: number;
}

function toTraceDataRow(r: Rec): TraceDataRow {
  return {
    id: str(r.Id ?? r.id),
    statMinute: str(r.StatMinute ?? r.statMinute),
    statHour: str(r.StatHour ?? r.statHour),
    clientId: str(r.ClientId ?? r.clientId),
    name: str(r.Name ?? r.name),
    total: num(r.Total ?? r.total),
    errors: num(r.Errors ?? r.errors),
    totalCost: num(r.TotalCost ?? r.totalCost),
    cost: num(r.Cost ?? r.cost),
    maxCost: num(r.MaxCost ?? r.maxCost),
    minCost: num(r.MinCost ?? r.minCost),
    startTime: num(r.StartTime ?? r.startTime),
    endTime: num(r.EndTime ?? r.endTime),
  };
}

/** 参数组装（纯函数）：appId 必传；可选项仅非空落参，searchTag 仅 true 落参 */
export function buildTraceDataParams(q: TraceDataQuery): { url: string; params: Record<string, unknown> } {
  const params: Record<string, unknown> = { appId: q.appId, pageSize: q.pageSize ?? 100 };
  if (q.itemId != null && q.itemId > 0) params.itemId = q.itemId;
  if (q.name) params.name = q.name;
  if (q.searchTag === true) params.searchTag = true;
  if (q.dtStart) params.dtStart = q.dtStart;
  if (q.dtEnd) params.dtEnd = q.dtEnd;
  if (q.q) params.Q = q.q;
  return { url: '/Monitors/TraceData', params };
}

export async function fetchTraceData(
  request: Req,
  q: TraceDataQuery,
): Promise<{ rows: TraceDataRow[]; total: number; error: string }> {
  try {
    const full: TraceDataQuery = {
      ...q,
      dtStart: q.dtStart || localDateStr(new Date()),
      dtEnd: q.dtEnd || localDateStr(new Date()),
    };
    const p = buildTraceDataParams(full);
    const res = await request({ url: p.url, method: 'GET', params: p.params });
    const data = (res as Rec | null | undefined)?.data;
    const rows = Array.isArray(data) ? (data as Rec[]).map(toTraceDataRow) : [];
    const total = num((res as Rec | null | undefined)?.total);
    return { rows, total, error: '' };
  } catch (e) {
    return { rows: [], total: 0, error: e instanceof Error && e.message ? e.message : '追踪数据加载失败' };
  }
}

// ---------- 双图（条件与聚合） ----------

export interface TraceDataChartPalette {
  primary: string;
  danger: string;
  axis: string;
  splitLine: string;
}

/** 双图显示条件（对齐后端 MVC 旧条件 appId>0 && itemId>0 && 本页 >1 行） */
export function showTraceDataCharts(itemId: number, rows: TraceDataRow[]): boolean {
  return itemId > 0 && rows.length > 1;
}

const CHART_GRID = { left: 40, right: 16, top: 32, bottom: 24 } as const;

function baseAxis(c: TraceDataChartPalette, labels: string[]): Record<string, unknown> {
  return {
    type: 'category',
    data: labels,
    axisLabel: { color: c.axis },
    axisLine: { lineStyle: { color: c.splitLine } },
  };
}

function baseYAxis(c: TraceDataChartPalette): Record<string, unknown> {
  return {
    type: 'value',
    axisLabel: { color: c.axis },
    splitLine: { lineStyle: { color: c.splitLine } },
  };
}

/** 左图「调用与错误」：按 Name 聚合 Total/Errors，Total 降序 Top10 双柱状 */
export function traceDataNameBar(rows: TraceDataRow[], c: TraceDataChartPalette): Record<string, unknown> {
  const agg = new Map<string, { total: number; errors: number }>();
  for (const r of rows) {
    const cur = agg.get(r.name) ?? { total: 0, errors: 0 };
    cur.total += r.total;
    cur.errors += r.errors;
    agg.set(r.name, cur);
  }
  const top = [...agg.entries()].sort((a, b) => b[1].total - a[1].total).slice(0, 10);
  return {
    grid: { ...CHART_GRID },
    tooltip: { trigger: 'axis' },
    legend: { top: 0, textStyle: { color: c.axis } },
    xAxis: baseAxis(c, top.map(([name]) => name)),
    yAxis: baseYAxis(c),
    series: [
      { name: '调用次数', type: 'bar', data: top.map(([, v]) => v.total), itemStyle: { color: c.primary } },
      { name: '错误数', type: 'bar', data: top.map(([, v]) => v.errors), itemStyle: { color: c.danger } },
    ],
  };
}

/** 右图「平均耗时」：按 StatHour 升序分组，组内 Total 加权平均（全零回退算术均值） */
export function traceDataCostLine(rows: TraceDataRow[], c: TraceDataChartPalette): Record<string, unknown> {
  const groups = new Map<string, { wSum: number; total: number; sum: number; n: number }>();
  for (const r of rows) {
    const key = r.statHour || r.statMinute;
    const cur = groups.get(key) ?? { wSum: 0, total: 0, sum: 0, n: 0 };
    cur.wSum += r.total * r.cost;
    cur.total += r.total;
    cur.sum += r.cost;
    cur.n += 1;
    groups.set(key, cur);
  }
  const sorted = [...groups.entries()].sort((a, b) => (a[0] < b[0] ? -1 : a[0] > b[0] ? 1 : 0));
  const data = sorted.map(([, g]) => (g.total > 0 ? Math.round(g.wSum / g.total) : Math.round(g.sum / Math.max(g.n, 1))));
  return {
    grid: { ...CHART_GRID },
    tooltip: { trigger: 'axis' },
    legend: { top: 0, textStyle: { color: c.axis } },
    xAxis: baseAxis(c, sorted.map(([h]) => shortStatTime(h))),
    yAxis: baseYAxis(c),
    series: [
      { name: '平均耗时(ms)', type: 'line', smooth: true, showSymbol: false, data, lineStyle: { color: c.primary }, itemStyle: { color: c.primary } },
    ],
  };
}

// ---------- 总计行 ----------

export interface TraceDataTotalRow {
  label: string;
  total: number;
  errors: number;
  totalCost: number;
  /** 平均耗时=ΣTotalCost/ΣTotal（本页加权，Total 全零为 0） */
  avgCost: number;
}

export function traceDataTotalRow(rows: TraceDataRow[]): TraceDataTotalRow | null {
  if (rows.length === 0) return null;
  const total = rows.reduce((s, r) => s + r.total, 0);
  const errors = rows.reduce((s, r) => s + r.errors, 0);
  const totalCost = rows.reduce((s, r) => s + r.totalCost, 0);
  return {
    label: `本页合计（已加载 ${rows.length} 行）`,
    total,
    errors,
    totalCost,
    avgCost: total > 0 ? Math.round(totalCost / total) : 0,
  };
}

// ---------- 行操作（追踪 / 屏蔽） ----------

export type TraceResolveResult = { ok: true; traceId: string } | { ok: false; message: string };

/** 追踪：POST Trace?id=（雪花 string 透传）→ {traceId}，失败透传后端原文 */
export async function resolveTraceDataTrace(request: Req, id: string): Promise<TraceResolveResult> {
  if (!id) return { ok: false, message: '缺少追踪数据编号' };
  try {
    const res = await request({ url: '/Monitors/TraceData/Trace', method: 'POST', params: { id } });
    const r = okRes(res);
    if (!r.ok) return { ok: false, message: r.message || '追踪解析失败' };
    const obj = (r.data ?? res ?? {}) as Rec;
    const traceId = str(obj.traceId);
    if (!traceId) return { ok: false, message: '追踪解析失败：未返回追踪标识' };
    return { ok: true, traceId };
  } catch (e) {
    return { ok: false, message: e instanceof Error && e.message ? e.message : '追踪解析失败' };
  }
}

export type ExcludeResult = { ok: true; message: string } | { ok: false; message: string };

/** 屏蔽：POST Exclude?id= → JsonRefresh("成功！")；拦截器剥壳直回字符串时按成功处理 */
export async function excludeTraceData(request: Req, id: string): Promise<ExcludeResult> {
  if (!id) return { ok: false, message: '缺少追踪数据编号' };
  try {
    const res = await request({ url: '/Monitors/TraceData/Exclude', method: 'POST', params: { id } });
    if (typeof res === 'string') return { ok: true, message: res || '成功！' };
    const r = okRes(res);
    if (!r.ok) return { ok: false, message: r.message || '屏蔽失败' };
    return { ok: true, message: typeof r.data === 'string' && r.data ? r.data : '成功！' };
  } catch (e) {
    return { ok: false, message: e instanceof Error && e.message ? e.message : '屏蔽失败' };
  }
}

// ---------- 展示格式化 ----------

/** Unix 毫秒 → 'MM-dd HH:mm:ss'；0/负值显示 —（不格式化出 1970） */
export function fmtUnixMs(ms: number): string {
  if (!Number.isFinite(ms) || ms <= 0) return '—';
  const d = new Date(ms);
  const p = (n: number) => String(n).padStart(2, '0');
  return `${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
}
