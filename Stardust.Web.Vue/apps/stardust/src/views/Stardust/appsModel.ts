// 应用页纯逻辑（与 UI 解耦，便于单测）：状态判定、KPI 聚合、状态筛选
// 真实数据来源（camelCase）：
//  - 应用列表 /Registry/App        字段 name/displayName/category/version/enable
//  - 在线聚合 /Registry/AppOnline  (appName 去重计数；同 appName 行数 = 实例数)
// 状态优先级：告警 > 离线 > 在线

export type AppStatus = 'online' | 'warn' | 'offline'

export interface AppListItem {
  id?: number
  name?: string
  displayName?: string
  category?: string
  version?: string
  enable?: boolean
  [k: string]: unknown
}

export interface AppAggRow extends AppListItem {
  status: AppStatus
  instances: number
  services: number
}

/** 依据应用行 + 是否在线，判定状态（告警优先于离线优先于在线） */
export function deriveAppStatus(app: AppListItem, online: boolean): AppStatus {
  if (app.enable === false) return 'offline'
  if (!online) return 'offline'
  // 告警态需 AlarmRecord 关联，页面主体阶段保守：Enable 且在线视为 online
  return 'online'
}

/** 前端聚合 KPI：总数 / 在线 / 告警 / 离线 */
export function aggregateKpis(list: AppAggRow[]) {
  const total = list.length
  let online = 0
  let warn = 0
  let offline = 0
  for (const r of list) {
    if (r.status === 'online') online++
    else if (r.status === 'warn') warn++
    else offline++
  }
  return { total, online, warn, offline }
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

/** 状态筛选 chips 配置 */
export const statusChips: { key: 'all' | AppStatus; label: string }[] = [
  { key: 'all', label: '全部' },
  { key: 'online', label: '在线' },
  { key: 'warn', label: '告警' },
  { key: 'offline', label: '离线' },
]

/** chip 计数 */
export function chipCount(stats: { total: number; online: number; warn: number; offline: number }, key: 'all' | AppStatus): number {
  return key === 'all' ? stats.total : key === 'online' ? stats.online : key === 'warn' ? stats.warn : stats.offline
}

/** 状态 → 中文标签 */
export const statusLabel: Record<AppStatus, '在线' | '告警' | '离线'> = {
  online: '在线',
  warn: '告警',
  offline: '离线',
}

/** 把原始应用列表 + 在线实例计数，映射为带 status/instances 的聚合行 */
export function toAggRows(apps: AppListItem[], instanceCounts: Map<string, number>): AppAggRow[] {
  return apps.map((a) => {
    const name = a.name ?? ''
    const instances = instanceCounts.get(name) ?? 0
    return { ...a, instances, services: 0, status: deriveAppStatus(a, instances > 0) }
  })
}

// ===== 抽屉 UI 辅助（样式仅 --el-* 令牌 + color-mix，无硬编码色值） =====

/** 状态 → 头部 banner 三态样式：在线 success / 告警 warning / 离线 danger；底 12%、边 30% */
export function bannerFor(status: AppStatus): { color: string; background: string; borderColor: string; text: string } {
  const tone = status === 'online' ? 'success' : status === 'warn' ? 'warning' : 'danger'
  const c = `var(--el-color-${tone})`
  return {
    color: c,
    background: `color-mix(in srgb, ${c} 12%, transparent)`,
    borderColor: `color-mix(in srgb, ${c} 30%, transparent)`,
    text: status === 'online' ? '● 在线 · 实例健康' : status === 'warn' ? '▲ 告警 · 需关注' : '■ 离线 · 实例不可用',
  }
}

/** 抽屉六页签配置 */
export const drawerTabs: { key: 'overview' | 'instances' | 'config' | 'publish' | 'monitor' | 'service'; label: string }[] = [
  { key: 'overview', label: '概览' },
  { key: 'instances', label: '实例' },
  { key: 'config', label: '配置' },
  { key: 'publish', label: '发布' },
  { key: 'monitor', label: '监控' },
  { key: 'service', label: '服务' },
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

/** 运行行 → 构建/上传/分发/部署四步状态（done=已完成 / run=进行中 / wait=未开始） */
export function pipelineSteps(run: PipelineRunRow): PipelineStep[] {
  const done =
    run.status === 'Failed' || run.status === 'Cancelled'
      ? failedProgress(run)
      : STATUS_PROGRESS[run.status as PipelineStatus] ?? 0
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
}

/** 并行抓取抽屉四页签数据：配置 / 发布 / 监控 / 服务 */
export async function fetchAppDetail(request: Req, appName?: string, appId?: number): Promise<AppDetail> {
  const p: Record<string, unknown> = { AppName: appName, AppId: appId }
  const [cfg, run, st, svc] = await Promise.all([
    request({ url: '/Configs/AppConfig', method: 'GET', params: { ...p, PageSize: 200 } }),
    request({ url: '/Deployment/AppPipelineRun', method: 'GET', params: { ...p, PageSize: 20 } }),
    request({ url: '/Monitors/AppMinuteStat', method: 'GET', params: { ...p, PageSize: 120 } }),
    request({ url: '/Registry/AppService', method: 'GET', params: { ...p, PageSize: 500 } }),
  ])
  return {
    config: (cfg?.data ?? []) as ConfigRow[],
    runs: (run?.data ?? []) as PipelineRunRow[],
    stats: (st?.data ?? []) as MinuteStatRow[],
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

/** 列表页：并行抓取应用 / 在线 / 服务并聚合实例数与服务数 */
export async function fetchAppList(request: Req): Promise<{ rows: AppAggRow[]; online: OnlineRow[] }> {
  const [ar, or, sr] = await Promise.all([
    request({ url: '/Registry/App', method: 'GET', params: { PageSize: 200 } }),
    request({ url: '/Registry/AppOnline', method: 'GET', params: { PageSize: 500 } }),
    request({ url: '/Registry/AppService', method: 'GET', params: { PageSize: 500 } }),
  ])
  const online = (or?.data ?? []) as OnlineRow[]
  const inst = new Map<string, number>()
  for (const r of online) if (r.appName) inst.set(r.appName, (inst.get(r.appName) ?? 0) + 1)
  const svc = new Map<number, number>()
  for (const r of (sr?.data ?? []) as { appId?: number }[]) if (r.appId) svc.set(r.appId, (svc.get(r.appId) ?? 0) + 1)
  const rows = toAggRows((ar?.data ?? []) as AppListItem[], inst).map((r) => ({ ...r, services: svc.get(r.id ?? 0) ?? 0 }))
  return { rows, online }
}

/** 定时刷新分钟统计（抽屉打开时周期拉取） */
export async function fetchMinuteStats(request: Req, appName?: string, appId?: number, pageSize = 120): Promise<MinuteStatRow[]> {
  const res = await request({ url: '/Monitors/AppMinuteStat', method: 'GET', params: { AppName: appName, AppId: appId, PageSize: pageSize } })
  return (res?.data ?? []) as MinuteStatRow[]
}
