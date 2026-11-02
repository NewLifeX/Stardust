/**
 * A19 可观测性四屏补缺模型（设计稿 src/page-observability.jsx）
 *
 * 纯函数层：不依赖 Vue、不硬编码颜色（颜色由组件 getComputedStyle 解析令牌后注入）。
 * 设计稿用 Mock.genSeries 造数；本层一律吃后端真实行，缺数据即返回空序列，绝不伪造。
 */

export interface MinuteStatLite {
  StatTime?: string
  statTime?: string
  AppName?: string
  appName?: string
  Total?: number
  total?: number
  Errors?: number
  errors?: number
  ErrorRate?: number
  errorRate?: number
  Cost?: number
  cost?: number
  MaxCost?: number
  maxCost?: number
  MinCost?: number
  minCost?: number
}

const num = (v: unknown): number => {
  const n = Number(v)
  return Number.isFinite(n) ? n : 0
}
const str = (v: unknown): string => (v === null || v === undefined ? '' : String(v))

const pick = (r: Record<string, unknown>, keys: string[]): unknown => {
  for (const k of keys) {
    const v = r[k]
    if (v !== undefined && v !== null && v !== '') return v
  }
  return undefined
}

/** 运行时解析 CSS 令牌（canvas 不认 var()）；取不到返回空串 */
export function resolveToken(name: string): string {
  if (typeof document === 'undefined') return ''
  try {
    return getComputedStyle(document.documentElement).getPropertyValue(name).trim()
  } catch {
    return ''
  }
}

export interface Point {
  t: string
  v: number
}

export function timeOf(r: MinuteStatLite): string {
  return str(r.StatTime ?? r.statTime)
}

export function sortByTime(rows: MinuteStatLite[]): MinuteStatLite[] {
  return [...rows].sort((a, b) => timeOf(a).localeCompare(timeOf(b)))
}

export function labelOf(r: MinuteStatLite): string {
  const hit = timeOf(r).match(/(\d{2}:\d{2})/)
  return hit ? hit[1] : timeOf(r).slice(-5)
}

/** 全局 QPS（Total 视作每分钟请求数） */
export function buildGlobalQpsSeries(rows: MinuteStatLite[]): Point[] {
  return sortByTime(rows).map((r) => ({ t: labelOf(r), v: +(num(r.Total ?? r.total) / 60).toFixed(2) }))
}

/** 全局错误率（百分比） */
export function buildGlobalErrorSeries(rows: MinuteStatLite[]): Point[] {
  return sortByTime(rows).map((r) => {
    const er = r.ErrorRate ?? r.errorRate
    const total = num(r.Total ?? r.total)
    const v = er === undefined || er === null
      ? (total ? (num(r.Errors ?? r.errors) / total) * 100 : 0)
      : num(er) * (num(er) <= 1 ? 100 : 1)
    return { t: labelOf(r), v: +v.toFixed(3) }
  })
}

export interface LatencySeries {
  p50: Point[]
  p95: Point[]
  p99: Point[]
  approx: boolean
}

/**
 * P50 · P95 · P99。后端仅 Cost/MaxCost/MinCost，无分位直方图 →
 * P50=Cost、P95=Cost 与 MaxCost 插值、P99=MaxCost，属**近似**（组件已标注）。
 */
export function buildLatencySeries(rows: MinuteStatLite[]): LatencySeries {
  const p50: Point[] = []
  const p95: Point[] = []
  const p99: Point[] = []
  let approx = false
  for (const r of sortByTime(rows)) {
    const avg = num(r.Cost ?? r.cost)
    const max = num(r.MaxCost ?? r.maxCost)
    const min = num(r.MinCost ?? r.minCost)
    if (max > 0 && max !== avg) approx = true
    const mid = avg + (max - avg) * 0.8
    p50.push({ t: labelOf(r), v: +avg.toFixed(1) })
    p95.push({ t: labelOf(r), v: +(max > 0 ? Math.max(avg, mid) : avg).toFixed(1) })
    p99.push({ t: labelOf(r), v: +(max > 0 ? max : Math.max(avg, min)).toFixed(1) })
  }
  return { p50, p95, p99, approx }
}

/** Node CPU · 集群平均：后端无该指标 → 恒空（组件显示待接入） */
export function buildNodeCpuSeries(): Point[] {
  return []
}

export interface MetricsPanels {
  qps: Point[]
  err: Point[]
  latency: LatencySeries
  cpu: Point[]
  hasData: boolean
  approxLatency: boolean
}

export function buildMetricsPanels(rows: MinuteStatLite[]): MetricsPanels {
  const latency = buildLatencySeries(rows)
  return {
    qps: buildGlobalQpsSeries(rows),
    err: buildGlobalErrorSeries(rows),
    latency,
    cpu: buildNodeCpuSeries(),
    hasData: rows.length > 0,
    approxLatency: latency.approx,
  }
}

export interface LineSeriesInput {
  name: string
  points: Point[]
  color?: string
}

/** 折线 option；颜色由调用方传入已解析令牌值，空串则不设色交 ECharts 默认色板 */
export function lineOption(series: LineSeriesInput[], opts: { area?: boolean; unit?: string } = {}): Record<string, unknown> {
  return {
    grid: { left: 8, right: 12, top: 16, bottom: 4, containLabel: true },
    tooltip: { trigger: 'axis', valueFormatter: (v: unknown) => (v === null || v === undefined ? '—' : String(v) + (opts.unit ?? '')) },
    xAxis: { type: 'category', data: series.length ? series[0].points.map((p) => p.t) : [], boundaryGap: false, axisLine: { show: false }, axisTick: { show: false }, axisLabel: { fontSize: 10 } },
    yAxis: { type: 'value', splitLine: { show: true }, axisLabel: { fontSize: 10 } },
    series: series.map((s) => ({
      name: s.name,
      type: 'line',
      smooth: true,
      showSymbol: false,
      data: s.points.map((p) => p.v),
      ...(s.color ? { lineStyle: { color: s.color, width: 1.5 }, itemStyle: { color: s.color } } : {}),
      ...(opts.area && s.color ? { areaStyle: { color: s.color, opacity: 0.12 } } : {}),
    })),
  }
}

// ===== Traces =====

export interface TraceRow {
  id: string
  endpoint: string
  svc: string
  status: 'ok' | 'warn' | 'error'
  dur: number
  at: string
}

export function toTraceRows(items: Record<string, unknown>[]): TraceRow[] {
  return items.map((r) => {
    const dur = num(pick(r, ['cost', 'Cost', 'duration', 'Duration', 'elapsed', 'Elapsed']))
    const err = num(pick(r, ['error', 'Error', 'errorCount', 'ErrorCount']))
    const rawAt = str(pick(r, ['startTime', 'StartTime', 'at', 'At', 'createTime', 'CreateTime']))
    return {
      id: str(pick(r, ['traceId', 'TraceId', 'id', 'Id'])),
      endpoint: str(pick(r, ['name', 'Name', 'endpoint', 'Endpoint', 'path', 'Path'])),
      svc: str(pick(r, ['appName', 'AppName', 'svc', 'Svc', 'service', 'Service'])),
      status: (err > 0 ? 'error' : dur >= 500 ? 'warn' : 'ok') as TraceRow['status'],
      dur: +dur.toFixed(1),
      at: rawAt.length > 11 ? rawAt.slice(11, 19) : rawAt,
    }
  })
}

export interface WaterfallRow {
  svc: string
  op: string
  depth: number
  leftPct: number
  widthPct: number
  ms: number
  err: boolean
}

export function buildWaterfallRows(spans: Record<string, unknown>[]): { rows: WaterfallRow[]; total: number } {
  const parsed = spans.map((s) => {
    const tone = str(pick(s, ['tone', 'Tone', 'level', 'Level']))
    const tag = str(pick(s, ['tag', 'Tag']))
    return {
      svc: str(pick(s, ['svc', 'Svc', 'appName', 'AppName', 'service', 'Service'])),
      op: str(pick(s, ['op', 'Op', 'name', 'Name', 'operation', 'Operation'])),
      d: num(pick(s, ['d', 'D', 'offset', 'Offset', 'start', 'Start'])),
      dur: num(pick(s, ['dur', 'Dur', 'duration', 'Duration', 'cost', 'Cost'])),
      err: tone === 'err' || tone === 'error' || tag === 'error',
    }
  })
  const total = parsed.reduce((acc, s) => Math.max(acc, s.d + s.dur), 0)
  return {
    rows: parsed.map((s, i) => ({
      svc: s.svc,
      op: s.op,
      depth: Math.floor(i / 3),
      leftPct: total > 0 ? +((s.d / total) * 100).toFixed(3) : 0,
      widthPct: total > 0 ? +((s.dur / total) * 100).toFixed(3) : 0,
      ms: +s.dur.toFixed(1),
      err: s.err,
    })),
    total: +total.toFixed(1),
  }
}

export interface LogLine {
  ts: string
  lvl: 'error' | 'warn' | 'info'
  msg: string
}

export function toLogLines(items: Record<string, unknown>[]): LogLine[] {
  return items.map((r) => {
    const lvl = str(pick(r, ['lvl', 'Lvl', 'level', 'Level'])).toLowerCase()
    const rawTs = str(pick(r, ['ts', 'Ts', 'time', 'Time', 'createTime', 'CreateTime']))
    return {
      ts: rawTs.length > 11 ? rawTs.slice(11, 23) : rawTs,
      lvl: (lvl === 'error' || lvl === 'warn' ? lvl : 'info') as LogLine['lvl'],
      msg: str(pick(r, ['msg', 'Msg', 'message', 'Message', 'content', 'Content'])),
    }
  })
}

// ===== Logs =====

export interface LogStream {
  lines: LogLine[]
  counts: { error: number; warn: number; info: number }
}

export function buildLogStream(rows: Record<string, unknown>[], limit = 40): LogStream {
  const all = toLogLines(rows)
  const counts = { error: 0, warn: 0, info: 0 }
  for (const l of all) counts[l.lvl] += 1
  return { lines: all.slice(0, limit), counts }
}

export function compact(n: number): string {
  if (n >= 10000) return (n / 1000).toFixed(0) + 'k'
  if (n >= 1000) return (n / 1000).toFixed(1) + 'k'
  return String(n)
}

// ===== Alerts =====

export interface AlertCard {
  label: string
  value: number
  badge: string
  tone: 'danger' | 'info' | 'success' | 'plain'
}

export interface AlertTableRow {
  level: string
  tone: 'danger' | 'warn' | 'info'
  rule: string
  obj: string
  val: string
  thr: string
  dur: string
  at: string
  state: 'pending' | 'ack' | 'resolved'
}

export function levelTone(level: string): 'danger' | 'warn' | 'info' {
  const l = level.toLowerCase()
  if (l === 'critical' || l === 'high' || l === 'danger' || l === '严重') return 'danger'
  if (l === 'warning' || l === 'warn' || l === 'medium' || l === '中') return 'warn'
  return 'info'
}

export function stateOf(r: Record<string, unknown>): AlertTableRow['state'] {
  const raw = str(pick(r, ['state', 'State', 'status', 'Status'])).toLowerCase()
  if (raw === 'ack' || raw === 'processing' || raw === '1') return 'ack'
  if (raw === 'resolved' || raw === 'recovered' || raw === '2' || raw === 'closed') return 'resolved'
  return 'pending'
}

export function toAlertRows(rows: Record<string, unknown>[]): AlertTableRow[] {
  return rows.map((r) => {
    const level = str(pick(r, ['level', 'Level', 'severity', 'Severity'])) || 'info'
    return {
      level,
      tone: levelTone(level),
      rule: str(pick(r, ['rule', 'Rule', 'ruleName', 'RuleName', 'name', 'Name'])),
      obj: str(pick(r, ['obj', 'Obj', 'target', 'Target', 'appName', 'AppName', 'nodeName', 'NodeName'])),
      val: str(pick(r, ['val', 'Val', 'value', 'Value', 'currentValue', 'CurrentValue'])),
      thr: str(pick(r, ['thr', 'Thr', 'threshold', 'Threshold'])),
      dur: str(pick(r, ['dur', 'Dur', 'duration', 'Duration', 'last', 'Last'])),
      at: str(pick(r, ['at', 'At', 'time', 'Time', 'createTime', 'CreateTime'])),
      state: stateOf(r),
    }
  })
}

export function buildAlertCards(rows: Record<string, unknown>[], silenced = 0): AlertCard[] {
  const c = alertStateCounts(rows)
  return [
    { label: '未处理', value: c.pending, badge: 'critical', tone: 'danger' },
    { label: '处理中', value: c.ack, badge: 'ack', tone: 'info' },
    { label: '今日已恢复', value: c.resolved, badge: 'resolved', tone: 'success' },
    { label: '静默中', value: silenced, badge: 'silence', tone: 'plain' },
  ]
}

export function alertStateCounts(rows: Record<string, unknown>[]): { pending: number; ack: number; resolved: number } {
  const all = toAlertRows(rows)
  return {
    pending: all.filter((r) => r.state === 'pending').length,
    ack: all.filter((r) => r.state === 'ack').length,
    resolved: all.filter((r) => r.state === 'resolved').length,
  }
}
