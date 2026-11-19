// 追踪三统计纯逻辑（CARD-MON-07 路径 B，来源旅程 J-MON-11/12/14）。无 Vue 依赖，request 由调用方注入，便于单测。
//
// 数据源（已读后端源码确认，2026-11-08）：
//  - 埋点下拉 GET /Monitors/TraceItem?appId=&pageSize=      （TraceItemController.Search :46-66；返回 TraceItem 实体：ID/Name/Kind/Enable）
//  - 三粒度   GET /Monitors/{TraceDayStat|TraceHourStat|TraceMinuteStat}?appId=&itemId=&type=&dtStart=&dtEnd=&pageSize=
//           （各控制器 Search；DTO 同构 TraceDayStat：ID/StatDate|StatTime/Name/Type/Total/RingRate/Errors/ErrorRate/Cost/MaxCost/MinCost/TotalValue/TraceId）
//           Day=90/Hour=168/Minute=288 对齐旅程默认（有应用无日期时后端 StatDate|StatTime 降序取该 PageSize）；
//           传了 dtStart+dtEnd 且无 itemId 时后端改按 Total 降序 PageSize=100（TraceDayStatController.cs:45-50）——前端固定传 pageSize 以本模块常量覆盖，保持列数稳定。
//  - 追踪     GET /Monitors/{entity}/Trace?id={statId}       （各控制器 Trace Action；需 Detail 权限；
//           成功 JsonRefresh(new { traceId })；失败 JsonRefresh("找不到统计数据"|"找不到追踪数据"|本地化"采样数据不存在") → 拦截器抛错，message=后端原文透传，前端不转写）
//
// 裁定（定稿卡 2026-11-08）：总计行=前端聚合已加载行（与 CARD-MON-04 同口径同注记）；跨应用全局搜索维度不做；
// 黄绿快捷钮/date 单点/上下双图 DataZoom=【替】不做；错误率行底色 ≥0.01 danger、≥0.001 warning。
import { okRes } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

type Rec = Record<string, unknown>;

export type TraceGranularity = 'day' | 'hour' | 'minute';

export interface TraceGranularityDef {
  key: TraceGranularity;
  label: string;
  /** 后端实体名（URL 段与 Trace Action 拼接用） */
  entity: 'TraceDayStat' | 'TraceHourStat' | 'TraceMinuteStat';
  pageSize: number;
  /** 实体时间字段：day=StatDate，hour/minute=StatTime */
  timeField: 'statDate' | 'statTime';
}

export const TRACE_GRANULARITIES: TraceGranularityDef[] = [
  { key: 'day', label: '每天', entity: 'TraceDayStat', pageSize: 90, timeField: 'statDate' },
  { key: 'hour', label: '每小时', entity: 'TraceHourStat', pageSize: 168, timeField: 'statTime' },
  { key: 'minute', label: '每5分钟', entity: 'TraceMinuteStat', pageSize: 288, timeField: 'statTime' },
];

export function traceGranularityOf(key: string): TraceGranularityDef | undefined {
  return TRACE_GRANULARITIES.find((g) => g.key === key);
}

function num(v: unknown): number {
  const n = Number(v);
  return Number.isFinite(n) ? n : 0;
}
function str(v: unknown): string {
  return v === undefined || v === null ? '' : String(v).trim();
}

// ---------- 埋点下拉 ----------

export interface TraceItemOption {
  id: number;
  name: string;
  kind: string;
  enable: boolean;
}

export async function fetchTraceItems(
  request: Req,
  appId: number,
  pageSize = 100,
): Promise<{ options: TraceItemOption[]; error: string }> {
  try {
    const res = await request({ url: '/Monitors/TraceItem', method: 'GET', params: { appId, pageSize } });
    const rows = Array.isArray(res?.data) ? (res.data as Rec[]) : [];
    return {
      options: rows.map((r) => ({
        id: num(r.ID ?? r.Id ?? r.id),
        name: str(r.Name ?? r.name),
        kind: str(r.Kind ?? r.kind),
        enable: r.Enable === undefined && r.enable === undefined ? true : Boolean(r.Enable ?? r.enable),
      })),
      error: '',
    };
  } catch (e) {
    return { options: [], error: e instanceof Error && e.message ? e.message : '埋点列表加载失败' };
  }
}

// ---------- 三粒度查询 ----------

export interface TraceStatQuery {
  appId: number;
  itemId: number;
  dtStart?: string;
  dtEnd?: string;
  /** 种类过滤（Api/Http/Db/Mq/Redis/Other，AppDayStat 列链触发） */
  type?: string;
  pageSize?: number;
}

export function buildTraceStatParams(
  key: TraceGranularity,
  q: TraceStatQuery,
): { url: string; params: Record<string, unknown> } {
  const g = traceGranularityOf(key);
  if (!g) throw new Error(`未知粒度：${key}`);
  const params: Record<string, unknown> = { appId: q.appId, pageSize: q.pageSize ?? g.pageSize };
  // itemId<=0 不传（后端 ToInt(-1) 视为不过滤）——AppDayStat C17 列链按天+种类查时不选埋点
  if (q.itemId > 0) params.itemId = q.itemId;
  if (q.dtStart) params.dtStart = q.dtStart;
  if (q.dtEnd) params.dtEnd = q.dtEnd;
  if (q.type) params.type = q.type;
  return { url: `/Monitors/${g.entity}`, params };
}

export interface TraceStatRow {
  id: number;
  /** day=日期（yyyy-MM-dd），hour/minute=时间（yyyy-MM-dd HH:mm[:ss]）——后端原样字符串 */
  statTime: string;
  name: string;
  type: string;
  total: number;
  ringRate: number;
  errors: number;
  errorRate: number;
  cost: number;
  maxCost: number;
  minCost: number;
  totalValue: number;
  traceId: string;
  /** 错误率行底色：≥0.01 danger、≥0.001 warning、其余无色 */
  rateClass: 'danger' | 'warning' | '';
}

export function traceRateClass(rate: number): 'danger' | 'warning' | '' {
  if (rate >= 0.01) return 'danger';
  if (rate >= 0.001) return 'warning';
  return '';
}

/**
 * 环比显示口径：后端 RingRate 是倍率（今天/昨天），1.2 表示超 20%（Model.xml 语义）。
 * 0 = 昨日无数据不适用 → '—'；1 → '0%'；其余带符号百分数两位以内。
 */
export function fmtRingRate(rate: number): string {
  if (!Number.isFinite(rate) || rate === 0) return '—';
  const pct = Math.round((rate - 1) * 10000) / 100;
  if (pct === 0) return '0%';
  return `${pct >= 0 ? '+' : ''}${pct}%`;
}

function toTraceStatRow(r: Rec): TraceStatRow {
  const rate = num(r.ErrorRate ?? r.errorRate);
  return {
    id: num(r.ID ?? r.Id ?? r.id),
    statTime: str(r.StatDate ?? r.StatTime ?? r.statDate ?? r.statTime),
    name: str(r.Name ?? r.name),
    type: str(r.Type ?? r.type),
    total: num(r.Total ?? r.total),
    ringRate: num(r.RingRate ?? r.ringRate),
    errors: num(r.Errors ?? r.errors),
    errorRate: rate,
    cost: num(r.Cost ?? r.cost),
    maxCost: num(r.MaxCost ?? r.maxCost),
    minCost: num(r.MinCost ?? r.minCost),
    totalValue: num(r.TotalValue ?? r.totalValue),
    traceId: str(r.TraceId ?? r.traceId),
    rateClass: traceRateClass(rate),
  };
}

export async function fetchTraceStats(
  request: Req,
  key: TraceGranularity,
  q: TraceStatQuery,
): Promise<{ rows: TraceStatRow[]; error: string }> {
  try {
    const p = buildTraceStatParams(key, q);
    const res = await request({ url: p.url, method: 'GET', params: p.params });
    const rows = Array.isArray(res?.data) ? (res.data as Rec[]) : [];
    return { rows: rows.map(toTraceStatRow), error: '' };
  } catch (e) {
    return { rows: [], error: e instanceof Error && e.message ? e.message : '追踪统计加载失败' };
  }
}

// ---------- 图表（单图 260px 调用/错误双序列；裁定不做上下双图+DataZoom） ----------

export interface ChartPalette {
  /** 主色（调用量） */
  primary: string;
  /** 次色（错误数，语义上偏警示） */
  danger: string;
  axis: string;
  splitLine: string;
}

/** x 轴短标签：hour/minute 截 HH:mm（'2026-11-08 09:05:00'→'09:05'），day 日期原样 */
export function shortStatTime(t: string): string {
  const m = /\s(\d{2}:\d{2})/.exec(t);
  return m ? m[1] : t;
}

export function traceStatChartOption(rows: TraceStatRow[], c: ChartPalette): Record<string, unknown> {
  return {
    grid: { left: 40, right: 16, top: 32, bottom: 24 },
    tooltip: { trigger: 'axis' },
    legend: { top: 0, textStyle: { color: c.axis } },
    xAxis: { type: 'category', data: rows.map((r) => shortStatTime(r.statTime)), axisLabel: { color: c.axis }, axisLine: { lineStyle: { color: c.splitLine } } },
    yAxis: { type: 'value', axisLabel: { color: c.axis }, splitLine: { lineStyle: { color: c.splitLine } } },
    series: [
      { name: '调用次数', type: 'line', smooth: true, showSymbol: false, data: rows.map((r) => r.total), lineStyle: { color: c.primary }, itemStyle: { color: c.primary } },
      { name: '错误数', type: 'line', smooth: true, showSymbol: false, data: rows.map((r) => r.errors), lineStyle: { color: c.danger }, itemStyle: { color: c.danger } },
    ],
  };
}

// ---------- 总计行（前端聚合，与 CARD-MON-04 同口径） ----------
export interface TraceStatTotalRow {
  label: string;
  total: number;
  errors: number;
  /** ΣErrors/ΣTotal（非行 errorRate 均值） */
  errorRate: number;
  /** 平均耗时=行均值 */
  cost: number;
  maxCost: number;
  minCost: number;
}

export function traceStatTotalRow(rows: TraceStatRow[]): TraceStatTotalRow | null {
  if (rows.length === 0) return null;
  const total = rows.reduce((s, r) => s + r.total, 0);
  const errors = rows.reduce((s, r) => s + r.errors, 0);
  return {
    label: `总计（已加载 ${rows.length} 行）`,
    total,
    errors,
    errorRate: total > 0 ? errors / total : 0,
    cost: Math.round(rows.reduce((s, r) => s + r.cost, 0) / rows.length),
    maxCost: rows.reduce((m, r) => Math.max(m, r.maxCost), 0),
    minCost: rows.reduce((m, r) => (m === 0 ? r.minCost : Math.min(m, r.minCost)), 0),
  };
}

// ---------- 追踪解析（Trace Action） ----------

/** 后端三失败文案（原文透传，前端不转写）：找不到统计数据 / 找不到追踪数据 / 采样数据不存在 */
export const TRACE_FAILED_LABELS = ['找不到统计数据', '找不到追踪数据', '采样数据不存在'] as const;

export type TraceResolveResult = { ok: true; traceId: string } | { ok: false; message: string };

export async function resolveTrace(
  request: Req,
  key: TraceGranularity,
  id: number,
): Promise<TraceResolveResult> {
  const g = traceGranularityOf(key);
  if (!g) return { ok: false, message: `未知粒度：${key}` };
  if (!id || id <= 0) return { ok: false, message: '缺少统计编号' };
  try {
    const res = await request({ url: `/Monitors/${g.entity}/Trace`, method: 'GET', params: { id } });
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
