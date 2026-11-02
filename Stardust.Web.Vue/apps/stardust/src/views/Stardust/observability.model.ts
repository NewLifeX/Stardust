import { cssVar } from '@/utils/cssVar';
// 可观测中心 纯逻辑（与 UI 解耦）：类型定义 + KPI 聚合 + 图表/瀑布/调用树构建 + fetch 层
// 真实数据来源（camelCase，与后端实体一致）：
//  - 应用列表     /Registry/App            id/name/displayName
//  - 分钟指标     /Monitors/AppMinuteStat  StatTime/AppId/AppName/Total/Errors/ErrorRate/Cost/MaxCost/MinCost
//  - Trace 埋点   /Monitors/TraceItem      AppId/Name/DisplayName/Total/Errors/Cost/AlarmThreshold/AlarmErrorRate/TraceId/UpdateTime
//  - Trace 采样   /Monitors/SampleData    traceId 过滤，Span：name/appName/cost/startTime/endTime/success/tag/error/spanId/parentId
// 日志/告警：后端暂无统一查询接口 → 页面保留查询条 UI，列表空状态「待接入」（不造假数据）

/** 请求函数结构类型（与 @newlifex/cube-vue request 兼容） */
export interface ReqOptions {
  url: string
  method?: string
  params?: Record<string, unknown>
}
export type Req = (options: ReqOptions) => Promise<{ data?: unknown }>

// ===== 指标 Tab =====

/** 应用选择器选项 */
export interface AppOption {
  id: number
  name: string
  displayName: string
}

/** 分钟统计行（AppMinuteStat） */
export interface MinuteStatRow {
  statTime?: string
  appId?: number
  appName?: string
  total?: number
  errors?: number
  errorRate?: number
  cost?: number
  maxCost?: number
  minCost?: number
  [k: string]: unknown
}

/** KPI 摘要：QPS / P99 / 错误率 / 在线率 */
export interface MetricKpi {
  qps: number
  p99: number
  errorRate: number
  sampleCount: number
}

/** 单行统计周期（秒）：相邻两行 StatTime 差值折算 QPS，无时间差按分钟兜底 */
function statWindowSeconds(rows: MinuteStatRow[]): number {
  if (rows.length < 2) return 60
  const a = Date.parse(String(rows[0].statTime ?? ''))
  const b = Date.parse(String(rows[1].statTime ?? ''))
  if (!Number.isFinite(a) || !Number.isFinite(b)) return 60
  const delta = (b - a) / 1000
  return delta > 0 ? delta : 60
}

/** 统计行 → KPI（按时间升序取最近行作为当前值；P99 以 MaxCost 近似，实体未持久化分位值） */
export function computeMetricKpi(rows: MinuteStatRow[]): MetricKpi {
  const sorted = [...rows].sort((a, b) => String(a.statTime ?? '').localeCompare(String(b.statTime ?? '')))
  const windowSeconds = statWindowSeconds(sorted)
  const last = sorted[sorted.length - 1]
  const lastTotal = Number(last?.total) || 0
  const lastErrors = Number(last?.errors) || 0
  const lastErrorRate = last?.errorRate !== undefined ? Number(last.errorRate) : lastTotal > 0 ? lastErrors / lastTotal : 0
  const p99 = sorted.reduce((m, r) => Math.max(m, Number(r.maxCost) || 0), 0)
  return {
    qps: lastTotal / windowSeconds,
    p99,
    errorRate: Number.isFinite(lastErrorRate) ? lastErrorRate : 0,
    sampleCount: lastTotal,
  }
}

function fmtMinute(v: unknown): string {
  if (v == null) return ''
  return String(v).replace('T', ' ').slice(0, 16)
}

/** 图1：调用次数（线）+ 错误数（次轴红） */
export function buildRequestChart(rows: MinuteStatRow[]): Record<string, unknown> {
  const sorted = [...rows].sort((a, b) => String(a.statTime ?? '').localeCompare(String(b.statTime ?? '')))
  return {
    tooltip: { trigger: 'axis' },
    legend: { data: ['调用次数', '错误数'], bottom: 0 },
    grid: { left: 48, right: 48, top: 24, bottom: 48, containLabel: true },
    xAxis: { type: 'category', boundaryGap: false, data: sorted.map((r) => fmtMinute(r.statTime)) },
    yAxis: [
      { type: 'value', name: '调用次数' },
      { type: 'value', name: '错误数', splitLine: { show: false } },
    ],
    series: [
      { name: '调用次数', type: 'line', smooth: true, showSymbol: false, data: sorted.map((r) => Number(r.total ?? 0)) },
      { name: '错误数', type: 'line', smooth: true, showSymbol: false, yAxisIndex: 1, data: sorted.map((r) => Number(r.errors ?? 0)), itemStyle: { color: cssVar('--danger-500', '#E5746F') } },
    ],
  }
}

/** 图2：平均耗时（线）+ 最大耗时（次轴） */
export function buildCostChart(rows: MinuteStatRow[]): Record<string, unknown> {
  const sorted = [...rows].sort((a, b) => String(a.statTime ?? '').localeCompare(String(b.statTime ?? '')))
  return {
    tooltip: { trigger: 'axis' },
    legend: { data: ['平均耗时(ms)', '最大耗时(ms)'], bottom: 0 },
    grid: { left: 48, right: 48, top: 24, bottom: 48, containLabel: true },
    xAxis: { type: 'category', boundaryGap: false, data: sorted.map((r) => fmtMinute(r.statTime)) },
    yAxis: [
      { type: 'value', name: '平均耗时(ms)' },
      { type: 'value', name: '最大耗时(ms)', splitLine: { show: false } },
    ],
    series: [
      { name: '平均耗时(ms)', type: 'line', smooth: true, showSymbol: false, data: sorted.map((r) => Number(r.cost ?? 0)) },
      { name: '最大耗时(ms)', type: 'line', smooth: true, showSymbol: false, yAxisIndex: 1, data: sorted.map((r) => Number(r.maxCost ?? 0)) },
    ],
  }
}

// ===== Trace Tab =====

/** Trace 埋点行（TraceItem 列表） */
export interface TraceItemRow {
  id?: number
  appId?: number
  name?: string
  displayName?: string
  total?: number
  errors?: number
  cost?: number
  alarmThreshold?: number
  alarmErrorRate?: number
  traceId?: string
  updateTime?: string
  [k: string]: unknown
}

/** Trace 列表展示行（埋点 + 应用名 join） */
export interface TraceListRow {
  raw: TraceItemRow
  traceId: string
  appId: number
  appName: string
  endpoint: string
  total: number
  errors: number
  cost: number
  ok: boolean
  updateTime: string
}

/** 埋点行 → 列表展示行（join 应用名；状态：errors>0 或无成功标记视为异常） */
export function toTraceListRows(items: TraceItemRow[], appMap: Map<number, AppOption>): TraceListRow[] {
  return items.map((it) => {
    const app = appMap.get(Number(it.appId))
    const errors = Number(it.errors) || 0
    const total = Number(it.total) || 0
    return {
      raw: it,
      traceId: String(it.traceId ?? ''),
      appId: Number(it.appId) || 0,
      appName: app?.displayName || app?.name || String(it.appId ?? '-'),
      endpoint: it.displayName || it.name || '-',
      total,
      errors,
      cost: Number(it.cost) || 0,
      ok: errors <= 0,
      updateTime: String(it.updateTime ?? '').replace('T', ' ').slice(0, 19),
    }
  })
}

/** Span 行（SampleData 单条采样） */
export interface SpanRow {
  id?: number | string
  name?: string
  appName?: string
  clientId?: string
  cost?: number
  startTime?: number
  endTime?: number
  success?: boolean
  tag?: string
  error?: string
  spanId?: string | null
  parentId?: string | null
  [k: string]: unknown
}

/** 瀑布条节点（时间轴视图）：服务名 + 中文说明 + 耗时，禁止裸缩写 */
export interface WaterfallNode {
  key: string
  name: string
  appName: string
  cost: number
  startOffset: number
  widthPct: number
  ok: boolean
  depth: number
}

/** 调用树节点 */
export interface CallTreeNode {
  key: string
  name: string
  appName: string
  cost: number
  ok: boolean
  children: CallTreeNode[]
}

/** Span 中文名兜底：无 displayName 时「服务（应用名）」，节点禁止裸缩写 */
function spanLabel(s: SpanRow): string {
  const n = (s.name || '').trim()
  const app = (s.appName || '').trim()
  if (n && app && n !== app) return `${n}（${app}）`
  return n || app || '未知操作'
}

/** 跨度归一化：所有 Span 的相对起始偏移与宽度百分比 */
function normalizeSpanTimes(spans: SpanRow[]): { min: number; max: number } {
  let min = Number.POSITIVE_INFINITY
  let max = Number.NEGATIVE_INFINITY
  for (const s of spans) {
    const st = Number(s.startTime) || 0
    const et = Number(s.endTime) || st + (Number(s.cost) || 0)
    if (st < min) min = st
    if (et > max) max = et
  }
  if (!Number.isFinite(min)) min = 0
  if (!Number.isFinite(max) || max <= min) max = min + 1
  return { min, max }
}

/** Span 列表 → 瀑布时间轴节点（按开始时间排序，深度由 parentId 链计算） */
export function buildWaterfall(spans: SpanRow[]): WaterfallNode[] {
  if (spans.length === 0) return []
  const { min, max } = normalizeSpanTimes(spans)
  const total = max - min
  const depthMap = new Map<string, number>()
  const spanIds = new Set(spans.map((s) => String(s.spanId ?? '')))
  // 深度：沿 parentId 链回溯
  const depthOf = (s: SpanRow): number => {
    const pid = String(s.parentId ?? '')
    if (!pid || !spanIds.has(pid)) return 0
    if (depthMap.has(pid)) return (depthMap.get(pid) ?? 0) + 1
    const parent = spans.find((x) => String(x.spanId ?? '') === pid)
    if (!parent) return 0
    const d = depthOf(parent) + 1
    depthMap.set(pid, d - 1)
    return d
  }
  return [...spans]
    .sort((a, b) => (Number(a.startTime) || 0) - (Number(b.startTime) || 0))
    .map((s, i) => {
      const st = Number(s.startTime) || 0
      const et = Number(s.endTime) || st + (Number(s.cost) || 0)
      const cost = Number(s.cost) || Math.max(0, et - st)
      return {
        key: String(s.id ?? i),
        name: `${spanLabel(s)} ${cost}ms`,
        appName: s.appName || '',
        cost,
        startOffset: ((st - min) / total) * 100,
        widthPct: Math.max(0.5, ((et - st) / total) * 100),
        ok: s.success !== false,
        depth: depthOf(s),
      }
    })
}

/** Span 列表 → 调用树（按 ParentId/SpanId 嵌套） */
export function buildCallTree(spans: SpanRow[]): CallTreeNode[] {
  if (spans.length === 0) return []
  const spanSet = new Set(spans.map((s) => String(s.spanId ?? '')))
  const map = new Map<string, CallTreeNode>()
  const makeNode = (s: SpanRow, key: string): CallTreeNode => ({
    key,
    name: `${spanLabel(s)} ${Number(s.cost) || 0}ms`,
    appName: s.appName || '',
    cost: Number(s.cost) || 0,
    ok: s.success !== false,
    children: [],
  })
  spans.forEach((s, i) => map.set(String(s.id ?? i), makeNode(s, String(s.id ?? i))))
  const roots: CallTreeNode[] = []
  spans.forEach((s, i) => {
    const node = map.get(String(s.id ?? i))!
    const pid = String(s.parentId ?? '')
    if (pid && spanSet.has(pid)) {
      const parentKey = String(spans.find((x) => String(x.spanId ?? '') === pid)?.id ?? '')
      const parent = map.get(parentKey)
      if (parent) parent.children.push(node)
      else roots.push(node)
    } else {
      roots.push(node)
    }
  })
  return roots
}

/** 汇总 Trace 详情头部：总耗时 / Span 数 / 错误数 */
export function summarizeSpans(spans: SpanRow[]): { totalCost: number; spanCount: number; errorCount: number } {
  let max = 0
  let errorCount = 0
  for (const s of spans) {
    const cost = Number(s.cost) || 0
    const et = Number(s.endTime) || 0
    const st = Number(s.startTime) || 0
    const spanTotal = et > st ? et - st : cost
    if (spanTotal > max) max = spanTotal
    if (s.success === false || (s.error && s.error.length > 0)) errorCount++
  }
  return { totalCost: Math.round(max), spanCount: spans.length, errorCount }
}

// ===== 告警 Tab =====

/** 告警级别 */
export type AlertLevel = 'high' | 'medium' | 'low'
/** 告警状态（§15.3：未处理/处理中/已恢复） */
export type AlertStatus = 'pending' | 'processing' | 'recovered'

/** 告警事件行（后端暂无事件列表 API；预留结构，页面空状态「待接入」） */
export interface AlertRow {
  id: string
  level: AlertLevel
  target: string
  title: string
  currentValue: string
  threshold: string
  duration: string
  instanceCount: number
  traceCount: number
  logCount: number
  status: AlertStatus
  time: string
}

/** 告警统计：未处理/处理中/已恢复 */
export interface AlertSummary {
  pending: number
  processing: number
  recovered: number
}

export function summarizeAlerts(rows: AlertRow[]): AlertSummary {
  let pending = 0
  let processing = 0
  let recovered = 0
  for (const r of rows) {
    if (r.status === 'pending') pending++
    else if (r.status === 'processing') processing++
    else recovered++
  }
  return { pending, processing, recovered }
}

// ===== fetch 层（request 依赖注入；响应 data 以 unknown + 索引签名收敛，无 any） =====

/** 抓取应用选择器选项（/Registry/App） */
export async function fetchAppOptions(request: Req): Promise<AppOption[]> {
  const res = await request({ url: '/Registry/App', method: 'GET', params: { PageSize: 500 } })
  const list = (res?.data ?? []) as { id?: number; Id?: number; name?: string; Name?: string; displayName?: string; DisplayName?: string }[]
  return list.map((a) => ({
    id: Number(a.id ?? a.Id ?? 0),
    name: String(a.name ?? a.Name ?? ''),
    displayName: String(a.displayName ?? a.DisplayName ?? a.name ?? a.Name ?? ''),
  }))
}

/** 抓取分钟指标（/Monitors/AppMinuteStat，按应用过滤） */
export async function fetchMinuteStats(request: Req, appId?: number, pageSize = 1000): Promise<MinuteStatRow[]> {
  const p: Record<string, unknown> = { PageSize: pageSize }
  if (appId != null && appId > 0) p.AppId = appId
  const res = await request({ url: '/Monitors/AppMinuteStat', method: 'GET', params: p })
  return (res?.data ?? []) as MinuteStatRow[]
}

/** 抓取 Trace 埋点列表（/Monitors/TraceItem） */
export async function fetchTraceItems(request: Req, pageSize = 200): Promise<TraceItemRow[]> {
  const res = await request({ url: '/Monitors/TraceItem', method: 'GET', params: { PageSize: pageSize } })
  return (res?.data ?? []) as TraceItemRow[]
}

/** 抓取 Trace 采样（Span 列表，/Monitors/SampleData?traceId=） */
export async function fetchTraceSpans(request: Req, traceId: string): Promise<SpanRow[]> {
  if (!traceId) return []
  const res = await request({ url: '/Monitors/SampleData', method: 'GET', params: { traceId, pageSize: 10000 } })
  const list = res?.data ?? res ?? []
  return (Array.isArray(list) ? list : []) as SpanRow[]
}
