// 应用工作台「监控大屏」页签纯逻辑（F02）。无 Vue 依赖，request 由调用方注入。
// 真源（backend-dev 未广播 LOGIN-READY 前，字段以源码为准，登录后 curl 复核）：
//  - 字段：Stardust.Data/Monitors/应用每日统计.cs（ID/StatDate/AppId/Names/Total/RingRate/Errors/
//    ErrorRate/TotalCost/Cost/MaxCost/MinCost/Apis/Https/Dbs/Mqs/Redis/Others/…）
//    Stardust.Data/Monitors/应用分钟统计.cs（ID/StatTime/AppId/Total/Errors/ErrorRate/TotalCost/
//    Cost/MaxCost/MinCost/…）
//  - 行为：AppDayStatController.Search 读 monitorId（别名）或 appId，appId 有效且未给时间时
//    默认 StatDate 倒序、PageSize=30；RetryStat 同样走 SelectKeys（GetRequest("Keys") 逗号串）；
//    AppMinuteStatController.Search：appId + 时间窗 → PageSize=288（24h×5min）、按 Total 倒序，
//    需要客户端按时间重排；AppMinuteStat 是只读控制器（WebReadOnlyEntityController）。
// 图表：ECharts option 组装为纯函数，颜色由调用方注入（canvas 无法解析 var(--el-*)）。
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

type Rec = Record<string, unknown>;

function num(v: unknown): number {
  const n = Number(v);
  return Number.isFinite(n) ? n : 0;
}
function str(v: unknown): string {
  return v === undefined || v === null ? '' : String(v);
}
function pick(r: Rec, ...keys: string[]): unknown {
  for (const k of keys) {
    const v = r[k];
    if (v !== undefined && v !== null) return v;
  }
  return undefined;
}
function listOf(res: unknown): Rec[] {
  if (Array.isArray(res)) return res as Rec[];
  if (res && typeof res === 'object' && Array.isArray((res as { data?: unknown }).data)) {
    return (res as { data: Rec[] }).data;
  }
  return [];
}

export interface DayStatRow {
  id: number;
  statDate: string;
  names: number;
  total: number;
  ringRate: number;
  errors: number;
  errorRate: number;
  cost: number;
  maxCost: number;
  minCost: number;
  apis: number;
  https: number;
  dbs: number;
  mqs: number;
  redis: number;
  others: number;
}

export interface MinuteStatRow {
  id: number;
  statTime: string;
  total: number;
  errors: number;
  cost: number;
  maxCost: number;
  minCost: number;
}

export function toDayStatRow(r: Rec): DayStatRow {
  return {
    id: num(pick(r, 'Id', 'id', 'ID')),
    statDate: str(pick(r, 'StatDate', 'statDate')).slice(0, 10),
    names: num(pick(r, 'Names', 'names')),
    total: num(pick(r, 'Total', 'total')),
    ringRate: num(pick(r, 'RingRate', 'ringRate')),
    errors: num(pick(r, 'Errors', 'errors')),
    errorRate: num(pick(r, 'ErrorRate', 'errorRate')),
    cost: num(pick(r, 'Cost', 'cost')),
    maxCost: num(pick(r, 'MaxCost', 'maxCost')),
    minCost: num(pick(r, 'MinCost', 'minCost')),
    apis: num(pick(r, 'Apis', 'apis')),
    https: num(pick(r, 'Https', 'https')),
    dbs: num(pick(r, 'Dbs', 'dbs')),
    mqs: num(pick(r, 'Mqs', 'mqs')),
    redis: num(pick(r, 'Redis', 'redis')),
    others: num(pick(r, 'Others', 'others')),
  };
}

export function toMinuteStatRow(r: Rec): MinuteStatRow {
  return {
    id: num(pick(r, 'Id', 'id', 'ID')),
    statTime: str(pick(r, 'StatTime', 'statTime')),
    total: num(pick(r, 'Total', 'total')),
    errors: num(pick(r, 'Errors', 'errors')),
    cost: num(pick(r, 'Cost', 'cost')),
    maxCost: num(pick(r, 'MaxCost', 'maxCost')),
    minCost: num(pick(r, 'MinCost', 'minCost')),
  };
}

/** 本地日期 → 'YYYY-MM-DD'（后端 dtStart/dtEnd 用） */
export function localDateStr(d: Date): string {
  const p = (n: number): string => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
}

/** 本地时间 → 'YYYY-MM-DD HH:mm:ss'（分钟统计 dtStart/dtEnd 用） */
export function localDateTimeStr(d: Date): string {
  const p = (n: number): string => String(n).padStart(2, '0');
  return `${localDateStr(d)} ${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
}

export interface DayStatParams {
  appId: number;
  dtStart: string;
  dtEnd: string;
}

/** 日统计查询参数：默认最近 7 天（含今天） */
export function buildDayStatParams(appId: number, days = 7, now = new Date()): DayStatParams {
  const end = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const start = new Date(end);
  start.setDate(end.getDate() - Math.max(days - 1, 0));
  return { appId, dtStart: localDateStr(start), dtEnd: localDateStr(end) };
}

/** 日统计列表：GET /api/Monitors/AppDayStat?appId=&dtStart=&dtEnd=&pageSize=30，按日期升序 */
export async function fetchDayStats(request: Req, appId: number, days = 7, now = new Date()): Promise<DayStatRow[]> {
  const p = buildDayStatParams(appId, days, now);
  const res = await request({
    url: '/Monitors/AppDayStat',
    method: 'GET',
    params: { ...p, pageSize: Math.max(days + 2, 30) },
  });
  return listOf(res)
    .map(toDayStatRow)
    .sort((a, b) => a.statDate.localeCompare(b.statDate));
}

export interface MinuteStatParams {
  appId: number;
  dtStart: string;
  dtEnd: string;
}

/** 分钟统计查询参数：最近 hours 小时（默认 24h，与后端 PageSize=288 对齐） */
export function buildMinuteStatParams(appId: number, hours = 24, now = new Date()): MinuteStatParams {
  const start = new Date(now.getTime() - Math.max(hours, 1) * 3600_000);
  return { appId, dtStart: localDateTimeStr(start), dtEnd: localDateTimeStr(now) };
}

/** 分钟统计列表：GET /api/Monitors/AppMinuteStat?appId=&dtStart=&dtEnd=&pageSize=288，按时间升序（后端按 Total 倒序返回，必须重排） */
export async function fetchMinuteStats(request: Req, appId: number, hours = 24, now = new Date()): Promise<MinuteStatRow[]> {
  const p = buildMinuteStatParams(appId, hours, now);
  const res = await request({
    url: '/Monitors/AppMinuteStat',
    method: 'GET',
    params: { ...p, pageSize: 288 },
  });
  return listOf(res)
    .map(toMinuteStatRow)
    .sort((a, b) => a.statTime.localeCompare(b.statTime));
}

/** 批量重新统计：POST /api/Monitors/AppDayStat/RetryStat?keys=（SelectKeys 读 query，逗号串）。返回 JsonRefresh 文案。 */
export async function retryDayStat(request: Req, ids: number[]): Promise<string> {
  if (ids.length === 0) return '';
  const res = (await request({
    url: '/Monitors/AppDayStat/RetryStat',
    method: 'POST',
    params: { keys: ids.join(',') },
  })) as Rec;
  return typeof res?.message === 'string' ? res.message : '已提交重算';
}

/** 分钟轴标签 '2026-11-03 10:05:00' → '10:05' */
export function minuteLabel(t: string): string {
  const m = /\d{2}:\d{2}/.exec(t);
  return m ? m[0] : t;
}

export interface ChartPalette {
  /** 主色（调用量） */
  primary: string;
  /** 次色（错误数，语义上偏警示） */
  danger: string;
  /** 耗时图主色 */
  cost: string;
  /** 耗时图上下界 */
  costRange: string;
  axis: string;
  splitLine: string;
}

/** 调用/错误双序列折线 option（左轴次数；错误数同一轴，0 时也画） */
export function dayCallsChartOption(rows: DayStatRow[], c: ChartPalette): Record<string, unknown> {
  return {
    grid: { left: 40, right: 16, top: 32, bottom: 24 },
    tooltip: { trigger: 'axis' },
    legend: { top: 0, textStyle: { color: c.axis } },
    xAxis: { type: 'category', data: rows.map((r) => r.statDate), axisLabel: { color: c.axis }, axisLine: { lineStyle: { color: c.splitLine } } },
    yAxis: { type: 'value', axisLabel: { color: c.axis }, splitLine: { lineStyle: { color: c.splitLine } } },
    series: [
      { name: '调用次数', type: 'line', smooth: true, showSymbol: false, data: rows.map((r) => r.total), lineStyle: { color: c.primary }, itemStyle: { color: c.primary } },
      { name: '错误数', type: 'line', smooth: true, showSymbol: false, data: rows.map((r) => r.errors), lineStyle: { color: c.danger }, itemStyle: { color: c.danger } },
    ],
  };
}

/** 耗时 option：平均/最大/最小三条 */
export function dayCostChartOption(rows: DayStatRow[], c: ChartPalette): Record<string, unknown> {
  return {
    grid: { left: 40, right: 16, top: 32, bottom: 24 },
    tooltip: { trigger: 'axis', valueFormatter: (v: unknown) => `${v} ms` },
    legend: { top: 0, textStyle: { color: c.axis } },
    xAxis: { type: 'category', data: rows.map((r) => r.statDate), axisLabel: { color: c.axis }, axisLine: { lineStyle: { color: c.splitLine } } },
    yAxis: { type: 'value', axisLabel: { color: c.axis }, splitLine: { lineStyle: { color: c.splitLine } } },
    series: [
      { name: '平均耗时', type: 'line', smooth: true, showSymbol: false, data: rows.map((r) => r.cost), lineStyle: { color: c.cost }, itemStyle: { color: c.cost } },
      { name: '最大耗时', type: 'line', smooth: true, showSymbol: false, data: rows.map((r) => r.maxCost), lineStyle: { color: c.costRange, type: 'dashed' }, itemStyle: { color: c.costRange } },
      { name: '最小耗时', type: 'line', smooth: true, showSymbol: false, data: rows.map((r) => r.minCost), lineStyle: { color: c.costRange, type: 'dotted' }, itemStyle: { color: c.costRange } },
    ],
  };
}

/** 最近 24h 分钟序列 option：调用/错误双序列 */
export function minuteChartOption(rows: MinuteStatRow[], c: ChartPalette): Record<string, unknown> {
  return {
    grid: { left: 40, right: 16, top: 32, bottom: 24 },
    tooltip: { trigger: 'axis' },
    legend: { top: 0, textStyle: { color: c.axis } },
    xAxis: { type: 'category', data: rows.map((r) => minuteLabel(r.statTime)), axisLabel: { color: c.axis }, axisLine: { lineStyle: { color: c.splitLine } } },
    yAxis: { type: 'value', axisLabel: { color: c.axis }, splitLine: { lineStyle: { color: c.splitLine } } },
    series: [
      { name: '调用次数', type: 'line', smooth: true, showSymbol: false, data: rows.map((r) => r.total), lineStyle: { color: c.primary }, itemStyle: { color: c.primary } },
      { name: '错误数', type: 'line', smooth: true, showSymbol: false, data: rows.map((r) => r.errors), lineStyle: { color: c.danger }, itemStyle: { color: c.danger } },
    ],
  };
}

/** 大屏顶部汇总数字（调用总数 / 错误总数 / 平均耗时均值 / 错误率） */
export interface DayStatSummary {
  total: number;
  errors: number;
  avgCost: number;
  errorRate: number;
}

export function dayStatSummary(rows: DayStatRow[]): DayStatSummary {
  const total = rows.reduce((s, r) => s + r.total, 0);
  const errors = rows.reduce((s, r) => s + r.errors, 0);
  const withCost = rows.filter((r) => r.total > 0);
  const avgCost = withCost.length > 0 ? Math.round(withCost.reduce((s, r) => s + r.cost, 0) / withCost.length) : 0;
  const errorRate = total > 0 ? (errors / total) * 100 : 0;
  return { total, errors, avgCost, errorRate: Math.round(errorRate * 100) / 100 };
}
