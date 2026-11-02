// 应用页纯逻辑（与 UI 解耦，便于单测）：状态判定、KPI 聚合、状态筛选
// 真实数据来源（camelCase）：
//  - 应用列表 /Registry/App        字段 name/displayName/category/version/enable
//  - 在线聚合 /Registry/AppOnline  (appName 去重计数；同 appName 行数 = 实例数)
// 状态优先级：告警 > 离线 > 在线

/** 七态状态体系（§20.1）：运行/异常/离线/维护/部署中/发布失败/未知 */
export type AppStatus = 'running' | 'error' | 'offline' | 'maintenance' | 'deploying' | 'publishFailed' | 'unknown'

/** 旧三态 → 新七态映射（向后兼容：后端返回 online/warn/offline 时归一化） */
export function normalizeAppStatus(raw: string | undefined | null): AppStatus {
  switch (raw) {
    case 'online':
      return 'running'
    case 'warn':
      return 'error'
    case 'offline':
      return 'offline'
    default:
      return 'unknown'
  }
}

export interface AppListItem {
  id?: number
  name?: string
  displayName?: string
  category?: string
  version?: string
  enable?: boolean
  updateTime?: string
  [k: string]: unknown
}

export interface AppAggRow extends AppListItem {
  status: AppStatus
  instances: number
  services: number
  /** 列表监控指标（AppMinuteStat 聚合：qps / errorRate / p99），无统计数据时为 null */
  metric: AppMetric | null
  /** 最近 14 天日调用次数趋势（AppDayStat 聚合），无数据为空数组 */
  trend?: { date: string; total: number }[]
  /** 构建状态小标：building（构建中）/ failed（失败）；成功或无流水线为 null */
  buildStatus?: 'building' | 'failed' | null
}

/** 应用列表监控指标（方案 §7.3 首屏字段：QPS / 错误率 / P99） */
export interface AppMetric {
  qps: number
  errorRate: number
  p99: number
}

/** 依据应用行 + 是否在线，判定基础状态（运行/离线；异常需告警关联，部署中/发布失败由流水线数据增强） */
export function deriveAppStatus(app: AppListItem, online: boolean): AppStatus {
  if (app.enable === false) return 'offline'
  if (!online) return 'offline'
  // 在线且启用 → 运行；告警/异常需 AlarmRecord 关联，页面主体阶段保守返回 running
  return 'running'
}

/** 根据最近流水线运行记录增强应用状态（部署中/发布失败优先于运行态） */
export function enrichAppStatus(base: AppStatus, runs: PipelineRunRow[] | undefined): AppStatus {
  if (!runs || runs.length === 0) return base
  // 有正在运行的流水线 → 部署中
  const hasRunning = runs.some((r) => r.status === 'Running' || r.status === 'Deploying' || r.status === 'Building')
  if (hasRunning) return 'deploying'
  // 最近一次失败 → 发布失败
  const latest = runs[0]
  if (latest && latest.status === 'Failed') return 'publishFailed'
  return base
}

/** 前端聚合 KPI：总数 / 运行 / 异常（含发布失败）/ 离线 */
export function aggregateKpis(list: AppAggRow[]) {
  const total = list.length
  let running = 0
  let error = 0
  let offline = 0
  for (const r of list) {
    if (r.status === 'running') running++
    else if (r.status === 'error' || r.status === 'publishFailed' || r.status === 'deploying') error++
    else if (r.status === 'offline') offline++
  }
  return { total, running, error, offline }
}

/** 状态筛选：all 返回全部，其余按 status 过滤 */
export function filterByStatus(list: AppAggRow[], filter: 'all' | AppStatus): AppAggRow[] {
  if (filter === 'all') return list
  return list.filter((r) => r.status === filter)
}

/** 关键字过滤（名称/显示名） */
export function filterByKeyword(list: AppAggRow[], keyword: string): AppAggRow[] {
  const k = keyword.trim().toLowerCase()
  if (!k) return list
  return list.filter((r) => (r.name || '').toLowerCase().includes(k) || (r.displayName || '').toLowerCase().includes(k))
}

/** 状态筛选 chips 配置（§20.1 七态，主分组：全部/运行/部署中/异常/离线） */
export const statusChips: { key: 'all' | AppStatus; label: string }[] = [
  { key: 'all', label: '全部' },
  { key: 'running', label: '运行' },
  { key: 'deploying', label: '部署中' },
  { key: 'error', label: '异常' },
  { key: 'offline', label: '离线' },
]

/** chip 计数 */
export function chipCount(stats: { total: number; running: number; error: number; offline: number }, key: 'all' | AppStatus): number {
  if (key === 'all') return stats.total
  if (key === 'running') return stats.running
  if (key === 'error') return stats.error
  if (key === 'offline') return stats.offline
  return 0
}

/** 状态 → 中文标签（七态） */
export const statusLabel: Record<AppStatus, string> = {
  running: '运行',
  error: '异常',
  offline: '离线',
  maintenance: '维护',
  deploying: '部署中',
  publishFailed: '发布失败',
  unknown: '未知',
}

/** 把原始应用列表 + 在线实例计数，映射为带 status/instances 的聚合行 */
export function toAggRows(apps: AppListItem[], instanceCounts: Map<string, number>): AppAggRow[] {
  return apps.map((a) => {
    const name = a.name ?? ''
    const instances = instanceCounts.get(name) ?? 0
    return { ...a, instances, services: 0, status: deriveAppStatus(a, instances > 0), metric: null, trend: [] }
  })
}

// ===== 抽屉 UI 辅助（样式仅 --el-* 令牌 + color-mix，无硬编码色值） =====

/** 状态 → 头部 banner 样式（七态：运行 success / 异常 danger / 离线 danger / 维护 info / 部署中 primary / 发布失败 danger / 未知 info）；底 12%、边 30% */
export function bannerFor(status: AppStatus): { color: string; background: string; borderColor: string; text: string } {
  const toneMap: Record<AppStatus, string> = {
    running: 'success',
    error: 'danger',
    offline: 'danger',
    maintenance: 'info',
    deploying: 'primary',
    publishFailed: 'danger',
    unknown: 'info',
  }
  const textMap: Record<AppStatus, string> = {
    running: '● 运行 · 实例健康',
    error: '▲ 异常 · 需关注',
    offline: '■ 离线 · 实例不可用',
    maintenance: '⏸ 维护 · 暂停服务',
    deploying: '⏺ 部署中 · 正在发布',
    publishFailed: '● 发布失败 · 需重试',
    unknown: '? 未知 · 状态待确认',
  }
  const tone = toneMap[status]
  const c = `var(--el-color-${tone})`
  return {
    color: c,
    background: `color-mix(in srgb, ${c} 12%, transparent)`,
    borderColor: `color-mix(in srgb, ${c} 30%, transparent)`,
    text: textMap[status],
  }
}

/** 抽屉八页签配置（§7.5 + 修订 2026-10-13：概览/实例/服务/配置/发布/流水线/可观测/审计） */
export const drawerTabs: { key: 'overview' | 'instances' | 'service' | 'config' | 'publish' | 'pipeline' | 'monitor' | 'audit'; label: string }[] = [
  { key: 'overview', label: '概览' },
  { key: 'instances', label: '实例' },
  { key: 'service', label: '服务' },
  { key: 'config', label: '配置' },
  { key: 'publish', label: '发布' },
  { key: 'pipeline', label: '流水线' },
  { key: 'monitor', label: '可观测' },
  { key: 'audit', label: '审计' },
]

/** 角色药丸样式表：实例（运行/离线）、服务（提供/消费）；15% 底 */
export const rolePills: Record<string, { color: string; background: string }> = {
  运行: { color: 'var(--el-color-success)', background: 'color-mix(in srgb, var(--el-color-success) 15%, transparent)' },
  离线: { color: 'var(--el-color-primary)', background: 'color-mix(in srgb, var(--el-color-primary) 15%, transparent)' },
  提供: { color: 'var(--el-color-success)', background: 'color-mix(in srgb, var(--el-color-success) 15%, transparent)' },
  消费: { color: 'var(--el-color-primary)', background: 'color-mix(in srgb, var(--el-color-primary) 15%, transparent)' },
}

/** 角色 → 药丸样式（未知角色回退中性灰） */
export function pillStyleFor(role: string): { color: string; background: string } {
  return rolePills[role] ?? { color: 'var(--el-text-color-regular)', background: 'color-mix(in srgb, var(--el-text-color-regular) 15%, transparent)' }
}

// ===== 抽屉纯函数与类型（配置 / 流水线 / 监控 / 服务 / 在线实例） =====
// 实体字段（codegraph）：
//  - AppConfig       Name/DisplayName/Category/Enable/Version/PublishTime/CanBeQuoted/Quotes/IsGlobal/Readonly/Remark
//  - AppPipelineRun  PipelineId/Status/TriggerSource/CommitId/CommitMessage/CommitAuthor/Branch/BuildNodeId/
//                    AppVersionId/BuildStartedTime/BuildFinishedTime/DeployStartedTime/DeployFinishedTime/PipelineName
//  - AppMinuteStat   StatTime/AppId/Total/Errors/ErrorRate/TotalCost/Cost/MaxCost/MinCost
//  - AppService      AppId/ServiceId/ServiceName/Client/NodeId/Enable/PingCount/Version/Address/Weight/Scope/Tag/Healthy
//  - AppOnline       AppId/Name/NodeId/Client/Scope/Version/PingCount/ProcessName/MachineName/StartTime/IP

/** 在线实例行（/Registry/AppOnline 单行 = 一个在线会话/实例） */
export interface OnlineRow {
  appName?: string
  name?: string
  client?: string
  scope?: string
  version?: string
  nodeName?: string
  machineName?: string
  processName?: string
  startTime?: string
  createTime?: string
  ip?: string
  [k: string]: unknown
}

/** 按应用名筛出该应用的实例行（同 appName 行数 = 实例数） */
export function instancesRows(onlineRows: OnlineRow[], appName: string): OnlineRow[] {
  return onlineRows.filter((r) => (r.appName ?? r.name) === appName)
}

/** 配置项行（AppConfig 或以 key/value 承载的配置明细） */
export interface ConfigRow {
  key?: string
  name?: string
  field?: string
  value?: unknown
  content?: unknown
  [k: string]: unknown
}

/** 键值对（抽屉展示/复制用） */
export interface ConfigKv {
  key: string
  value: string
}

/** 配置行 → 键值对列表（空 key 行剔除） */
const SECRET_KEY = /secret/i
export function configKv(rows: ConfigRow[]): ConfigKv[] {
  return rows
    .map((r) => {
      const key = String(r.key ?? r.name ?? r.field ?? '').trim()
      const raw = r.value !== undefined ? r.value : r.content
      const value = raw !== undefined && raw !== null ? String(raw) : ''
      return { key, value: SECRET_KEY.test(key) && value ? '****' : value }
    })
    .filter((kv) => kv.key !== '')
}

/** 配置行 → 可复制的文本（key=value 每行一条） */
export function configText(rows: ConfigRow[]): string {
  return configKv(rows)
    .map((kv) => `${kv.key}=${kv.value}`)
    .join('\n')
}

/** 配置历史行（ConfigHistory） */
export interface HistoryRow {
  id?: number
  configId?: number
  action?: string
  success?: boolean
  time?: string
  [k: string]: unknown
}

/** 配置历史 → 展示行（按时间倒序、取 Publish/Rollback 等动作） */
export function historyRows(rows: HistoryRow[]): HistoryRow[] {
  return rows
    .filter((r) => r.action !== 'Insert' && r.action !== 'Update' && r.action !== 'Delete')
    .sort((a, b) => String(b.time ?? '').localeCompare(String(a.time ?? '')))
}

/** 流水线运行状态（PipelineStatus 枚举） */
export type PipelineStatus = 'Pending' | 'Building' | 'UploadSucceeded' | 'Deploying' | 'Success' | 'Failed' | 'Cancelled'

/**
 * 后端数字枚举 → 状态名（Stardust/Models/PipelineStatus.cs：0=Pending 1=Building 2=UploadSucceeded 3=Deploying 4=Success 5=Failed 6=Cancelled）。
 * API JSON 序列化把枚举输出为数字，字符串状态为旧 MVC 方言（连接字符串字段直出），两者都要能识别。
 */
const RUN_STATUS_BY_CODE: Record<number, PipelineStatus> = {
  0: 'Pending',
  1: 'Building',
  2: 'UploadSucceeded',
  3: 'Deploying',
  4: 'Success',
  5: 'Failed',
  6: 'Cancelled',
}

/** 运行状态归一：数字/数字字符串/字符串（容错空白）→ PipelineStatus，未知返回空串 */
export function normalizeRunStatus(status: unknown): PipelineStatus | '' {
  if (status == null || status === '') return ''
  if (typeof status === 'number') return RUN_STATUS_BY_CODE[status] ?? ''
  const s = String(status).trim()
  if (s === '') return ''
  if (/^\d+$/.test(s)) return RUN_STATUS_BY_CODE[Number(s)] ?? ''
  if (Object.prototype.hasOwnProperty.call(RUN_STATUS_BY_CODE, s)) return s as PipelineStatus
  const hit = Object.values(RUN_STATUS_BY_CODE).find((v) => v.toLowerCase() === s.toLowerCase())
  return hit ?? ''
}

/** 流水线运行行（AppPipelineRun） */
export interface PipelineRunRow {
  pipelineId?: number
  status?: PipelineStatus | string
  triggerSource?: string
  commitId?: string
  commitMessage?: string
  commitAuthor?: string
  branch?: string
  buildNodeId?: number
  appVersionId?: number
  buildStartedTime?: string
  buildFinishedTime?: string
  deployStartedTime?: string
  deployFinishedTime?: string
  pipelineName?: string
  createTime?: string
  [k: string]: unknown
}

export type PipelineStepKey = 'build' | 'upload' | 'dispatch' | 'deploy'
export type PipelineStepState = 'done' | 'run' | 'wait'

export interface PipelineStep {
  key: PipelineStepKey
  label: string
  state: PipelineStepState
}

/** 流水线四步：构建 → 上传 → 分发 → 部署 */
const STEP_ORDER: { key: PipelineStepKey; label: string }[] = [
  { key: 'build', label: '构建' },
  { key: 'upload', label: '上传' },
  { key: 'dispatch', label: '分发' },
  { key: 'deploy', label: '部署' },
]

/** 正常流转状态 → 当前步之前已完成步数 */
const STATUS_PROGRESS: Record<PipelineStatus, number> = {
  Pending: 0,
  Building: 0,
  UploadSucceeded: 1,
  Deploying: 2,
  Success: 4,
  Failed: 0,
  Cancelled: 0,
}

/** 失败/取消：按时间戳推断已进行到哪一步（中止步高亮为 run） */
function failedProgress(run: PipelineRunRow): number {
  if (!run.buildStartedTime) return 0
  if (!run.buildFinishedTime) return 1
  if (!run.deployStartedTime) return 2
  if (!run.deployFinishedTime) return 3
  return 4
}

/** 运行行 → 构建/上传/分发/部署四步状态（done=已完成 / run=进行中 / wait=未开始）。
 * status 兼容后端数字枚举与旧字符串方言（normalizeRunStatus 先归一再查表）；无法识别的状态不做任何步进（全 wait） */
export function pipelineSteps(run: PipelineRunRow): PipelineStep[] {
  const status = normalizeRunStatus(run.status)
  if (!status) return STEP_ORDER.map((s) => ({ ...s, state: 'wait' as PipelineStepState }))
  const done = status === 'Failed' || status === 'Cancelled' ? failedProgress(run) : STATUS_PROGRESS[status]
  return STEP_ORDER.map((s, i) => ({ ...s, state: i < done ? 'done' : i === done ? 'run' : 'wait' }))
}

/** 分钟统计行（AppMinuteStat；online/instances 由调用方合并，用于在线率） */
export interface MinuteStatRow {
  statTime?: string
  appId?: number
  total?: number
  errors?: number
  errorRate?: number
  totalCost?: number
  cost?: number
  maxCost?: number
  minCost?: number
  online?: number
  instances?: number
  [k: string]: unknown
}

/** 监控摘要：qps / p99 / 错误率 / 在线率 / 时间序列 */
export interface MonitorSummary {
  qps: number
  p99: number
  errorRate: number
  onlineRate: number
  series: { time: string; qps: number; errorRate: number; latency: number }[]
}

/** 分钟级时间戳 → 图表轴标签（HH:mm；无法解析时原样返回） */
export function fmtMinute(ts?: string): string {
  const raw = String(ts ?? '')
  if (!raw) return ''
  const d = new Date(raw.includes('T') ? raw : raw.replace(' ', 'T'))
  if (Number.isNaN(d.getTime())) return raw
  const hh = String(d.getHours()).padStart(2, '0')
  const mm = String(d.getMinutes()).padStart(2, '0')
  return `${hh}:${mm}`
}

/** 单行统计周期（秒）：用相邻两行 StatTime 差值折算 qps，无时间差信息时按分钟兜底 */
function statWindowSeconds(rows: MinuteStatRow[]): number {
  if (rows.length < 2) return 60
  const a = Date.parse(String(rows[0].statTime ?? ''))
  const b = Date.parse(String(rows[1].statTime ?? ''))
  if (!Number.isFinite(a) || !Number.isFinite(b)) return 60
  const delta = (b - a) / 1000
  return delta > 0 ? delta : 60
}

function safeRate(errors: number, total: number): number {
  return total > 0 ? errors / total : 0
}

/** 统计行 → 摘要（按时间升序取最近行作为当前值；实体未持久化分位值，p99 以 MaxCost 近似） */
export function monitorSummary(statRows: MinuteStatRow[]): MonitorSummary {
  const rows = [...statRows].sort((a, b) => String(a.statTime ?? '').localeCompare(String(b.statTime ?? '')))
  const windowSeconds = statWindowSeconds(rows)
  const last = rows[rows.length - 1]
  const lastTotal = Number(last?.total) || 0
  const lastErrors = Number(last?.errors) || 0
  const lastErrorRate = last?.errorRate !== undefined ? Number(last.errorRate) : safeRate(lastErrors, lastTotal)
  const instances = Number(last?.instances) || 0
  const online = Number(last?.online) || 0
  const p99 = rows.reduce((m, r) => Math.max(m, Number(r.maxCost) || 0), 0)
  return {
    qps: lastTotal / windowSeconds,
    p99,
    errorRate: Number.isFinite(lastErrorRate) ? lastErrorRate : 0,
    onlineRate: instances > 0 ? Math.min(online / instances, 1) : 1,
    series: rows.map((r) => ({
      time: String(r.statTime ?? ''),
      qps: (Number(r.total) || 0) / windowSeconds,
      errorRate: r.errorRate !== undefined ? Number(r.errorRate) : safeRate(Number(r.errors) || 0, Number(r.total) || 0),
      latency: Number(r.cost) || Number(r.maxCost) || 0,
    })),
  }
}

/** 服务行（AppService） */
export interface ServiceRow {
  appName?: string
  serviceName?: string
  client?: string
  address?: string
  version?: string
  weight?: number
  pingCount?: number
  scope?: string
  tag?: string
  healthy?: boolean
  direction?: string
  [k: string]: unknown
}

/** 服务分组：提供 / 消费 */
export interface ServicesGroups {
  provide: ServiceRow[]
  consume: ServiceRow[]
}

/** 行是否属于消费组（direction/scope 标记消费） */
function isConsumeRow(r: ServiceRow): boolean {
  const d = String(r.direction ?? r.scope ?? '').toLowerCase()
  return d === 'consume' || d === 'consumer' || d === '消费'
}

/** 服务行 → 提供/消费分组（AppService 默认是应用提供的服务，标记消费的行归入消费组） */
export function servicesGroups(rows: ServiceRow[]): ServicesGroups {
  const provide: ServiceRow[] = []
  const consume: ServiceRow[] = []
  for (const r of rows) {
    if (isConsumeRow(r)) consume.push(r)
    else provide.push(r)
  }
  return { provide, consume }
}

// ===== fetch 层（request 依赖注入；响应 data 以 unknown + 索引签名收敛，无 any） =====

/** 请求函数结构类型（与 @newlifex/cube-vue request 兼容） */
export interface ReqOptions {
  url: string
  method?: string
  params?: Record<string, unknown>
}

export type Req = (options: ReqOptions) => Promise<{ data?: unknown }>

/** 应用详情抽屉四路数据 */
export interface AppDetail {
  config: ConfigRow[]
  runs: PipelineRunRow[]
  stats: MinuteStatRow[]
  services: ServiceRow[]
  /** 在线实例数（取分钟统计上报的最大值，无数据为 0） */
  instances: number
  /** 配置变更历史（最近事件 / 审计用） */
  history: HistoryRow[]
}

/** 并行抓取抽屉四页签数据：配置 / 发布 / 监控 / 服务 */
export async function fetchAppDetail(request: Req, appName?: string, appId?: number): Promise<AppDetail> {
  const p: Record<string, unknown> = { AppName: appName, AppId: appId }
  const [cfg, run, st, svc, onl] = await Promise.all([
    request({ url: '/Configs/AppConfig', method: 'GET', params: { ...p, PageSize: 200 } }),
    request({ url: '/Deployment/AppPipelineRun', method: 'GET', params: { ...p, PageSize: 20 } }),
    request({ url: '/Monitors/AppMinuteStat', method: 'GET', params: { ...p, PageSize: 120 } }),
    request({ url: '/Registry/AppService', method: 'GET', params: { ...p, PageSize: 500 } }),
    request({ url: '/Registry/AppOnline', method: 'GET', params: { ...p, PageSize: 500 } }),
  ])
  const statRows = (st?.data ?? []) as MinuteStatRow[]
  let instances = 0
  for (const r of statRows) {
    const n = Number(r.instances ?? 0)
    if (Number.isFinite(n) && n > instances) instances = n
  }
  const onlineRows = (onl?.data ?? []) as OnlineRow[]
  let instCount = 0
  for (const r of onlineRows) {
    const rn = String(r.appName ?? r.name ?? '')
    const rid = Number((r as { appId?: number }).appId ?? 0)
    if (appName != null && rn === appName) instCount++
    else if (appId != null && rid === appId) instCount++
  }
  if (appName == null && appId == null) instCount = onlineRows.length
  if (instCount > 0) instances = instCount
  const cfgRows = (cfg?.data ?? []) as ConfigRow[]
  const firstCfg = cfgRows[0] as { id?: number } | undefined
  const history = await fetchConfigHistory(request, firstCfg?.id)
  return {
    config: (cfg?.data ?? []) as ConfigRow[],
    runs: (run?.data ?? []) as PipelineRunRow[],
    stats: statRows,
    instances,
    history,
    services: (svc?.data ?? []) as ServiceRow[],
  }
}

/** 抓取配置历史（无 configId 或失败时返回空数组） */
export async function fetchConfigHistory(request: Req, configId: unknown): Promise<HistoryRow[]> {
  if (configId == null) return []
  try {
    const res = await request({ url: '/Configs/ConfigHistory', method: 'GET', params: { configId, PageSize: 50 } })
    return (res?.data ?? []) as HistoryRow[]
  } catch {
    return []
  }
}

/** 配置差异：返回 changedKeys 数组 */
export async function configDiff(request: Req, id: number): Promise<string[]> {
  const res = await request({ url: '/Configs/ConfigHistory/Diff', method: 'GET', params: { id } })
  const data = (res?.data ?? {}) as { changedKeys?: string[] }
  return data.changedKeys ?? []
}

/** 配置回滚：返回后端消息（data 为字符串或 message 字段，缺省空串） */
export async function configRollback(request: Req, id: number): Promise<string> {
  const res = await request({ url: '/Configs/ConfigHistory/Rollback', method: 'POST', params: { id } })
  const data = res?.data
  if (typeof data === 'string') return data
  const obj = (data ?? {}) as { message?: unknown }
  return typeof obj.message === 'string' ? obj.message : ''
}

/** 分钟统计 → 按应用聚合最近一条指标（qps=Total/60 分钟窗口，p99 以 MaxCost 近似，与 monitorSummary 口径一致） */
export function appMetricMap(rows: MinuteStatRow[]): Map<number, AppMetric> {
  const byApp = new Map<number, MinuteStatRow[]>()
  for (const r of rows) {
    const id = Number(r.appId)
    if (!id) continue
    const arr = byApp.get(id) ?? []
    arr.push(r)
    byApp.set(id, arr)
  }
  const map = new Map<number, AppMetric>()
  for (const [id, arr] of byApp) {
    const sorted = [...arr].sort((a, b) => String(a.statTime ?? '').localeCompare(String(b.statTime ?? '')))
    const last = sorted[sorted.length - 1]
    const total = Number(last?.total) || 0
    const errors = Number(last?.errors) || 0
    map.set(id, {
      qps: total / 60,
      errorRate: last?.errorRate !== undefined ? Number(last.errorRate) : total > 0 ? errors / total : 0,
      p99: Number(last?.maxCost) || 0,
    })
  }
  return map
}

/** 列表页：并行抓取应用 / 在线 / 服务 / 分钟统计 / 日统计并聚合实例数与服务数 */
export async function fetchAppList(request: Req): Promise<{ rows: AppAggRow[]; online: OnlineRow[] }> {
  // 日统计趋势：失败不阻塞列表，降级为空趋势
  const dayStatP = request({ url: '/Monitors/AppDayStat', method: 'GET', params: { PageSize: 500 } })
    .then((r) => (r?.data ?? []) as Record<string, unknown>[])
    .catch(() => [] as Record<string, unknown>[])
  const [ar, or, sr, mr, dr] = await Promise.all([
    request({ url: '/Registry/App', method: 'GET', params: { PageSize: 200 } }),
    request({ url: '/Registry/AppOnline', method: 'GET', params: { PageSize: 500 } }),
    request({ url: '/Registry/AppService', method: 'GET', params: { PageSize: 500 } }),
    request({ url: '/Monitors/AppMinuteStat', method: 'GET', params: { PageSize: 1000 } }),
    dayStatP,
  ])
  const online = (or?.data ?? []) as OnlineRow[]
  const inst = new Map<string, number>()
  for (const r of online) if (r.appName) inst.set(r.appName, (inst.get(r.appName) ?? 0) + 1)
  const svc = new Map<number, number>()
  for (const r of (sr?.data ?? []) as { appId?: number }[]) if (r.appId) svc.set(r.appId, (svc.get(r.appId) ?? 0) + 1)
  const metricMap = appMetricMap((mr?.data ?? []) as MinuteStatRow[])
  // 日统计按 appId 分组、按日期升序取最近 14 天
  const trendMap = new Map<number, { date: string; total: number }[]>()
  {
    const byApp = new Map<number, { date: string; total: number }[]>()
    for (const r of dr) {
      const appId = Number(r.AppId ?? r.appId ?? 0)
      if (!appId) continue
      const date = String(r.StatDate ?? r.statDate ?? '').slice(0, 10)
      if (!date) continue
      const total = Number(r.Total ?? r.total ?? 0) || 0
      const arr = byApp.get(appId) ?? []
      arr.push({ date, total })
      byApp.set(appId, arr)
    }
    for (const [id, pts] of byApp) {
      pts.sort((a, b) => a.date.localeCompare(b.date))
      trendMap.set(id, pts.slice(-14))
    }
  }
  const rows = toAggRows((ar?.data ?? []) as AppListItem[], inst).map((r) => ({
    ...r,
    services: svc.get(r.id ?? 0) ?? 0,
    metric: metricMap.get(r.id ?? 0) ?? null,
    trend: trendMap.get(r.id ?? 0) ?? [],
  }))
  return { rows, online }
}

/** 定时刷新分钟统计（抽屉打开时周期拉取） */
export async function fetchMinuteStats(request: Req, appName?: string, appId?: number, pageSize = 120): Promise<MinuteStatRow[]> {
  const res = await request({ url: '/Monitors/AppMinuteStat', method: 'GET', params: { AppName: appName, AppId: appId, PageSize: pageSize } })
  return (res?.data ?? []) as MinuteStatRow[]
}

// ===== 审计日志与概览摘要（§7.5 审计 Tab / §7.6 概览 KPI） =====

/** 审计日志行（操作日志：时间/操作人/操作类型/详情） */
export interface AuditRow {
  time?: string
  operator?: string
  action?: string
  detail?: string
  [k: string]: unknown
}

/** 概览摘要区块数据（§7.6：实例健康/服务依赖/最近发布/最近告警） */
export interface OverviewSummary {
  onlineInstances: number
  totalInstances: number
  provideServices: number
  consumeServices: number
  latestVersion: string
  latestReleaseTime: string
  latestAlertLevel: string
  latestAlertTime: string
}

/** 从抽屉详情数据推导概览摘要 */
export function overviewSummary(runs: PipelineRunRow[], services: ServiceRow[], instances: number): OverviewSummary {
  const svc = servicesGroups(services)
  const latestRun = runs[0]
  return {
    onlineInstances: instances,
    totalInstances: instances,
    provideServices: svc.provide.length,
    consumeServices: svc.consume.length,
    latestVersion: latestRun?.appVersionId != null ? `v${latestRun.appVersionId}` : '—',
    latestReleaseTime: latestRun ? String(latestRun.deployFinishedTime || latestRun.buildFinishedTime || latestRun.createTime || '').slice(0, 16) : '—',
    latestAlertLevel: '—',
    latestAlertTime: '—',
  }
}

/** 从流水线运行记录推导最近事件时间线（部署/发布事件） */
export function recentEvents(runs: PipelineRunRow[], configHistory: HistoryRow[]): { time: string; text: string; type: 'deploy' | 'config' }[] {
  const events: { time: string; text: string; type: 'deploy' | 'config' }[] = []
  for (const r of runs.slice(0, 3)) {
    const statusText = r.status === 'Success' ? '发布成功' : r.status === 'Failed' ? '发布失败' : r.status
    events.push({
      time: String(r.deployFinishedTime || r.buildFinishedTime || r.createTime || '').slice(0, 16),
      text: `${r.pipelineName || '流水线'} ${statusText}`,
      type: 'deploy',
    })
  }
  for (const h of configHistory.slice(0, 2)) {
    events.push({
      time: String(h.time || '').slice(0, 16),
      text: `配置${h.action || '变更'}`,
      type: 'config',
    })
  }
  return events
    .filter((e) => e.time && e.time !== '')
    .sort((a, b) => b.time.localeCompare(a.time))
    .slice(0, 5)
}

/** 实例 Inspector 推导运行时（从进程名/框架字段推导 .NET / Node.js 等） */
export function detectRuntime(row: OnlineRow): string {
  const proc = String(row.processName || '').toLowerCase()
  const fw = String(row.scope || row.client || '').toLowerCase()
  if (proc.includes('dotnet') || proc.includes('star') || proc.includes('xunit') || proc.includes('testhost')) return '.NET'
  if (proc.includes('node') || fw.includes('node')) return 'Node.js'
  if (proc.includes('python')) return 'Python'
  if (proc.includes('java')) return 'Java'
  return '—'
}

// ===== 流水线页签（§4.7，修订 2026-10-13）：列表 / 触发 / 编辑 / 运行历史 =====

/** 流水线行（AppPipeline，DeployId 对应应用发布集 AppDeploy.Id） */
export interface PipelineRow {
  id?: number
  name?: string
  deployId?: number
  branch?: string
  buildNodeId?: number
  autoDeploy?: boolean
  enable?: boolean
  [k: string]: unknown
}

/** 运行历史展示行（PipelineRunRow + 归一化展示字段） */
export interface RunHistoryRow {
  id?: number
  pipelineId?: number
  pipelineName?: string
  status?: string
  triggerSource?: string
  commitAuthor?: string
  commitMessage?: string
  appVersionId?: number
  buildStartedTime?: string
  finishedTime?: string
}

/** 解析后端业务响应：code 0/200/success 视为成功 */
export function okRes(res: unknown): { ok: boolean; message: string; data: unknown } {
  const r = (res ?? {}) as { code?: number; message?: string; success?: boolean; data?: unknown }
  const ok = r.code === 0 || r.code === 200 || r.code === 1 || r.success === true
  return { ok, message: r.message ?? '', data: r.data }
}

/** 抓取某应用部署集下的流水线列表：GET /Deployment/AppPipeline?deployId= */
export async function fetchPipelines(request: Req, deployId: number): Promise<PipelineRow[]> {
  if (!deployId || deployId <= 0) return []
  try {
    const res = await request({ url: '/Deployment/AppPipeline', method: 'GET', params: { deployId, PageSize: 50 } })
    const rows = (res?.data ?? []) as Record<string, unknown>[]
    return rows.map((r) => ({
      id: Number(r.Id ?? r.id ?? 0) || undefined,
      name: String(r.Name ?? r.name ?? ''),
      deployId: Number(r.DeployId ?? r.deployId ?? 0) || undefined,
      branch: String(r.Branch ?? r.branch ?? ''),
      buildNodeId: Number(r.BuildNodeId ?? r.buildNodeId ?? 0) || undefined,
      autoDeploy: Boolean(r.AutoDeploy ?? r.autoDeploy ?? false),
      enable: Boolean(r.Enable ?? r.enable ?? false),
      ...r,
    }))
  } catch {
    return []
  }
}

/** 查应用部署集 Id（.AppRegistryId -> AppDeploy），无则让后端按 appId 自动建：GET /Deployment/AppDeploy?appId= */
export async function resolveDeployId(request: Req, appId: number): Promise<number> {
  if (!appId || appId <= 0) return 0
  try {
    const res = await request({ url: '/Deployment/AppDeploy', method: 'GET', params: { appId, PageSize: 20 } })
    const rows = (res?.data ?? []) as Record<string, unknown>[]
    const hit = rows.find((r) => Number(r.AppId ?? r.appId ?? 0) === appId)
    return hit ? Number(hit.Id ?? hit.id ?? 0) : 0
  } catch {
    return 0
  }
}

/** 合并运行历史多路来源：多条 pipelineId 查询结果，按 Id 去重、时间倒序 */
export function mergeRunHistory(byPipeline: PipelineRunRow[][]): RunHistoryRow[] {
  const seen = new Set<number>()
  const out: RunHistoryRow[] = []
  for (const r of byPipeline.flat()) {
    const id = Number(r.id ?? (r as Record<string, unknown>).Id ?? 0)
    if (id && seen.has(id)) continue
    if (id) seen.add(id)
    const raw = r as Record<string, unknown>
    out.push({
      id: id || undefined,
      pipelineId: Number(r.pipelineId ?? raw.PipelineId ?? 0) || undefined,
      pipelineName: String(raw.PipelineName ?? raw.pipelineName ?? r.pipelineName ?? ''),
      status: normalizeRunStatus(raw.Status ?? raw.status ?? r.status) as unknown as string,
      triggerSource: String(raw.TriggerSource ?? raw.triggerSource ?? r.triggerSource ?? ''),
      commitAuthor: String(raw.CommitAuthor ?? raw.commitAuthor ?? r.commitAuthor ?? ''),
      commitMessage: String(raw.CommitMessage ?? raw.commitMessage ?? r.commitMessage ?? ''),
      appVersionId: Number(r.appVersionId ?? raw.AppVersionId ?? 0) || undefined,
      buildStartedTime: String(raw.BuildStartedTime ?? raw.buildStartedTime ?? r.buildStartedTime ?? ''),
      finishedTime: String(raw.DeployFinishedTime ?? raw.deployFinishedTime ?? raw.BuildFinishedTime ?? raw.buildFinishedTime ?? ''),
    })
  }
  return out.sort((a, b) => String(b.finishedTime || b.buildStartedTime || '').localeCompare(String(a.finishedTime || a.buildStartedTime || '')))
}

/** 抓取流水线运行历史（页签专用）：后端 Search 只认 pipelineId/status（AppName 参数无效，已实测），
 * 逐流水线按 PipelineId 查询后合并去重 */
export async function fetchRunHistory(
  request: Req,
  pipelines: PipelineRow[] = [],
): Promise<RunHistoryRow[]> {
  const tasks: Promise<PipelineRunRow[]>[] = []
  for (const p of pipelines.slice(0, 8)) {
    if (!p.id) continue
    tasks.push(
      request({ url: '/Deployment/AppPipelineRun', method: 'GET', params: { pipelineId: p.id, PageSize: 5 } })
        .then((res) => (res?.data ?? []) as PipelineRunRow[])
        .catch(() => [] as PipelineRunRow[]),
    )
  }
  const all = await Promise.all(tasks)
  return mergeRunHistory(all)
}

/** 手动触发流水线（发布/手动触发共用）：GET /Deployment/AppPipeline/Trigger?id= */
export async function triggerPipeline(request: Req, pipelineId: number): Promise<{ ok: boolean; message: string }> {
  const res = await request({ url: '/Deployment/AppPipeline/Trigger', method: 'GET', params: { id: pipelineId } })
  const r = okRes(res)
  return { ok: r.ok, message: r.ok ? r.message || '流水线已触发' : r.message || '触发失败' }
}

/** 编辑流水线提交字段（仅前端可见可编辑字段） */
export interface PipelineEditForm {
  branch?: string
  buildNodeId?: number
  autoDeploy?: boolean
  enable?: boolean
}

/** 编辑流水线：PUT /Deployment/AppPipeline（Cube 实体更新端点为 [HttpPut] 集合路由，已实测） */
export async function updatePipeline(request: Req, pipeline: PipelineRow, form: PipelineEditForm): Promise<{ ok: boolean; message: string }> {
  const res = await request({
    url: '/Deployment/AppPipeline',
    method: 'PUT',
    data: {
      Id: pipeline.id,
      Name: pipeline.name,
      DeployId: pipeline.deployId,
      Branch: form.branch ?? '',
      BuildNodeId: Number(form.buildNodeId ?? 0),
      AutoDeploy: Boolean(form.autoDeploy),
      Enable: Boolean(form.enable),
    },
  } as ReqOptions & { data: Record<string, unknown> })
  const r = okRes(res)
  return { ok: r.ok, message: r.ok ? r.message || '保存成功' : r.message || '保存失败' }
}

/** 触发方式 → 中文标签 */
export function triggerLabel(source: string): string {
  const s = (source || '').toLowerCase()
  if (s.includes('webhook')) return 'Webhook'
  if (s.includes('manual') || s.includes('手动')) return '手动'
  if (s.includes('reprocess')) return '重跑'
  return source || '—'
}

/** 运行状态 → 药丸语义（成功绿 / 失败红 / 进行中蓝 / 其余灰）。status 兼容数字枚举（后端 JSON）与字符串方言 */
export function runStatusStyle(status: unknown): { color: string; background: string; label: string } {
  const s = normalizeRunStatus(status)
  if (s === 'Success') return { color: 'var(--el-color-success)', background: 'color-mix(in srgb, var(--el-color-success) 15%, transparent)', label: '成功' }
  if (s === 'Failed') return { color: 'var(--el-color-danger)', background: 'color-mix(in srgb, var(--el-color-danger) 15%, transparent)', label: '失败' }
  if (s === 'Cancelled') return { color: 'var(--el-text-color-secondary)', background: 'color-mix(in srgb, var(--el-text-color-secondary) 15%, transparent)', label: '已取消' }
  if (s === 'Building') return { color: 'var(--el-color-primary)', background: 'color-mix(in srgb, var(--el-color-primary) 15%, transparent)', label: '编译中' }
  if (s === 'Deploying') return { color: 'var(--el-color-primary)', background: 'color-mix(in srgb, var(--el-color-primary) 15%, transparent)', label: '部署中' }
  if (s === 'UploadSucceeded') return { color: 'var(--el-color-primary)', background: 'color-mix(in srgb, var(--el-color-primary) 15%, transparent)', label: '待部署' }
  if (s === 'Pending') return { color: 'var(--el-color-warning)', background: 'color-mix(in srgb, var(--el-color-warning) 15%, transparent)', label: '排队中' }
  return { color: 'var(--el-text-color-secondary)', background: 'color-mix(in srgb, var(--el-text-color-secondary) 15%, transparent)', label: '未知' }
}

/** 运行是否仍在进行中（构建/部署链未到终态），供触发后轮询刷新判断 */
export function isRunActive(status: unknown): boolean {
  const s = normalizeRunStatus(status)
  return s === 'Pending' || s === 'Building' || s === 'UploadSucceeded' || s === 'Deploying'
}
