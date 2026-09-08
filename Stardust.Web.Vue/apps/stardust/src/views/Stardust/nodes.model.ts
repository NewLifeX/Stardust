// 节点页纯逻辑（与 UI 解耦，便于单测）：camelCase 字段映射、状态判定、KPI 聚合、筛选
// 真实数据来源：
//  - /Nodes/Node         节点列表（name/code/iP/address/projectName/lastActive/cpu/alarmCpuRate）
//  - /Registry/AppOnline 在线实例（nodeName 派生在线集合；同 nodeName 行数 = 该节点服务数）
// 节点详情抽屉 9 Tab 纯函数与类型见文件末尾（规格：Doc/design-spec/03-节点页.md §5）

export type NodeStatus = 'online' | 'warn' | 'offline'

export interface NodeRow {
  id?: number
  name?: string
  code?: string
  iP?: string
  address?: string
  projectName?: string
  version?: string
  enable?: boolean
  lastActive?: string
  lastLogin?: string
  cpu?: number
  alarmCpuRate?: number
  oS?: string
  architecture?: string
  framework?: string
  [k: string]: unknown
}

export interface NodeView {
  id: number
  name: string
  ip: string
  cluster: string
  status: NodeStatus
  cpu: number | null
  memory: number | null
  services: number
  raw: NodeRow
}

/** 节点在线：在线实例集合命中，或最近活跃时间在 2 个心跳周期（120s）内 */
export function isNodeOnline(row: NodeRow, onlineNames: Set<string>, now: number): boolean {
  const name = row.name || row.code || ''
  if (onlineNames.has(name)) return true
  if (!row.lastActive) return false
  const t = new Date(row.lastActive).getTime()
  return !Number.isNaN(t) && now - t <= 120_000
}

/** 告警判定：配置了 CPU 告警阈值且当前 CPU 超阈值 */
export function isNodeAlarm(row: NodeRow): boolean {
  return !!row.alarmCpuRate && (row.cpu ?? 0) > row.alarmCpuRate
}

/** 状态优先级：离线 > 告警 > 在线 */
export function deriveNodeStatus(row: NodeRow, onlineNames: Set<string>, now: number): NodeStatus {
  if (!isNodeOnline(row, onlineNames, now)) return 'offline'
  if (isNodeAlarm(row)) return 'warn'
  return 'online'
}

/** 原始行 → 视图模型（字段大小写归一、状态派生、服务数挂载） */
export function mapNodes(
  rows: NodeRow[],
  onlineNames: Set<string>,
  serviceCounts: Map<string, number>,
  now: number,
): NodeView[] {
  return rows.map((row) => {
    const name = row.name || row.code || '-'
    return {
      id: row.id ?? 0,
      name,
      ip: row.iP || row.address || '-',
      cluster: row.projectName || '默认集群',
      status: deriveNodeStatus(row, onlineNames, now),
      cpu: typeof row.cpu === 'number' ? row.cpu : null,
      memory: null,
      services: serviceCounts.get(row.name || row.code || '') ?? 0,
      raw: row,
    }
  })
}

/** KPI 聚合：总数 / 在线 / 告警 / 离线 */
export function aggregateStats(rows: NodeView[]) {
  const total = rows.length
  const online = rows.filter((r) => r.status === 'online').length
  const alarm = rows.filter((r) => r.status === 'warn').length
  const offline = rows.filter((r) => r.status === 'offline').length
  return { total, online, alarm, offline }
}

/** 状态 → 卡片左边框 Tailwind 类 */
export const statusBorder: Record<NodeStatus, string> = {
  online: 'border-l-[var(--el-color-success)]',
  warn: 'border-l-[var(--el-color-warning)]',
  offline: 'border-l-[var(--el-color-danger)]',
}

/** 状态 → 颜色令牌 */
export function statusColor(s: NodeStatus): string {
  return s === 'online' ? 'var(--el-color-success)' : s === 'warn' ? 'var(--el-color-warning)' : 'var(--el-color-danger)'
}

/** 状态 → 中文标签 */
export const statusLabel: Record<NodeStatus, '在线' | '告警' | '离线'> = {
  online: '在线',
  warn: '告警',
  offline: '离线',
}

/** 节点服务 JSON 解析（容错纯函数） */
export function parseServices(raw: unknown): Record<string, unknown>[] {
  if (typeof raw !== 'string') return []
  try {
    return JSON.parse(raw) || []
  } catch {
    return []
  }
}

/** 状态筛选 + 关键字过滤（名称/IP） */
export function filterNodes(rows: NodeView[], filter: string, keyword: string): NodeView[] {
  let arr = rows
  if (filter === '在线') arr = arr.filter((r) => r.status === 'online')
  if (filter === '告警') arr = arr.filter((r) => r.status === 'warn')
  if (filter === '离线') arr = arr.filter((r) => r.status === 'offline')
  const k = keyword.trim().toLowerCase()
  if (k) arr = arr.filter((r) => r.name.toLowerCase().includes(k) || r.ip.toLowerCase().includes(k))
  return arr
}

// ============ 节点详情抽屉（9 Tab）纯函数与类型 ============
// 规格：Doc/design-spec/03-节点页.md §5；字段一律 camelCase；容错；无 any

/** 概览 Tab：键值项 */
export interface NodeKv {
  k: string
  v: string
}

/** 环图项：value 为百分比（0-100），null 表示无数据 */
export interface NodeRing {
  label: string
  value: number | null
  color: string
}

/** 历史事件行（status 供 UI 映射圆点色） */
export type EventStatus = 'login' | 'active'

export interface EventRow {
  status: EventStatus
  text: string
  time: string
}

/** 指令行（重启/更新=绿、采集=蓝，由 role 映射） */
export type CommandRole = 'restart' | 'update' | 'collect'

export interface CommandRow {
  role: CommandRole
  name: string
}

/** 性能曲线点 */
export interface PerfPoint {
  time: string
  cpu: number | null
  memory: number | null
}

/** 框架推荐行 */
export interface FrameworkRow {
  name: string
  match: number
}

/** 应用在线行（/Registry/AppOnline） */
export interface AppOnlineRow {
  appName?: string
  nodeName?: string
  name?: string
  iP?: string
  version?: string
}

/** 应用发布行 */
export interface AppDeployRow {
  appName: string
  version: string
  time: string
}

/** 服务行：提供(绿)/消费(蓝)；meta 为附加信息（端口/实例等，可空） */
export type ServiceRole = 'provider' | 'consumer'

export interface SvcRow {
  role: ServiceRole
  name: string
  meta: string
}

/** 概览 Tab：六项键值（节点名称/IP 地址/集群/系统/运行时长/Agent 版本） */
export function overviewInfo(row: NodeRow): NodeKv[] {
  return [
    { k: '节点名称', v: row.name || '-' },
    { k: 'IP 地址', v: row.iP || row.address || '-' },
    { k: '集群', v: row.projectName || '默认集群' },
    { k: '系统', v: [row.oS, row.architecture].filter(Boolean).join(' ') || '-' },
    { k: '运行时长', v: formatDuration(row.lastActive) },
    { k: 'Agent 版本', v: row.version || '-' },
  ]
}

/** 在线 Tab：三环（CPU 取行值 success 色；内存/磁盘暂无数据 → null） */
export function rings(row: NodeRow): NodeRing[] {
  return [
    { label: 'CPU', value: typeof row.cpu === 'number' ? row.cpu : null, color: 'var(--el-color-success)' },
    { label: '内存', value: null, color: 'var(--el-color-primary)' },
    { label: '磁盘', value: null, color: 'var(--el-color-info)' },
  ]
}

/** 历史 Tab：由 lastLogin/lastActive 派生两条事件行（新→旧，非法时间戳容错显示 '-'） */
export function historyRows(row: NodeRow): EventRow[] {
  const items: Array<{ status: EventStatus; text: string; ts: number | null }> = [
    { status: 'login', text: '节点最近登录', ts: toTime(row.lastLogin) },
    { status: 'active', text: '节点心跳上报', ts: toTime(row.lastActive) },
  ]
  return items
    .sort((a, b) => (b.ts ?? 0) - (a.ts ?? 0))
    .map((it) => ({ status: it.status, text: it.text, time: formatTime(it.ts ?? undefined) }))
}

/** 指令 Tab：静态三行 */
export function commandRows(): CommandRow[] {
  return [
    { role: 'restart', name: '重启节点服务' },
    { role: 'update', name: '更新节点 Agent' },
    { role: 'collect', name: '采集节点指标' },
  ]
}

/** 性能 Tab：/Nodes/NodeData 响应数组 → 曲线点（字段容错多候选键；非法值 → null；非数组 → 空） */
export function perfRows(data: unknown): PerfPoint[] {
  if (!Array.isArray(data)) return []
  const out: PerfPoint[] = []
  for (const it of data) {
    if (!it || typeof it !== 'object') continue
    const rec = it as Record<string, unknown>
    const time = firstKey(rec, ['time', 'createTime', 'updateTime'])
    out.push({
      time: time !== '' ? time : '-',
      cpu: numericOrNull(rec, ['cpu', 'cpuRate', 'cpuUsage']),
      memory: numericOrNull(rec, ['memory', 'memoryRate', 'memoryUsage', 'memRate']),
    })
  }
  return out
}

/** 多候选键取值：命中且非空 → 字符串（时间戳数字也转文本），否则 '' */
function firstKey(rec: Record<string, unknown>, keys: string[]): string {
  for (const k of keys) {
    const v = rec[k]
    if (typeof v === 'string' && v.trim()) return v.trim()
    if (typeof v === 'number' && Number.isFinite(v)) return formatTime(v)
  }
  return ''
}

/** 多候选键取数值：有限数字或可解析数字串 → 数值，否则 null */
function numericOrNull(rec: Record<string, unknown>, keys: string[]): number | null {
  for (const k of keys) {
    const v = rec[k]
    if (typeof v === 'number' && Number.isFinite(v)) return v
    if (typeof v === 'string' && v.trim() !== '') {
      const n = Number(v)
      if (Number.isFinite(n)) return n
    }
  }
  return null
}

/** 框架推荐 Tab：静态两行（名称 + 匹配度） */
export function frameworkRows(): FrameworkRow[] {
  return [
    { name: 'ASP.NET Core', match: 96 },
    { name: 'Node.js', match: 82 },
  ]
}

/** 应用在线 Tab：按 nodeName 过滤在线实例（容错空输入） */
export function appOnlineRows(onlineRows: AppOnlineRow[], nodeName: string): AppOnlineRow[] {
  if (!nodeName) return []
  return (onlineRows ?? []).filter((r) => r.nodeName === nodeName)
}

/** 应用发布 Tab：暂无发布记录数据，返回空数组 */
export function appDeployRows(): AppDeployRow[] {
  return []
}

/** 服务 Tab：解析节点 Services JSON → 映射 SvcRow；提供在前/消费在后；解析失败返回空数组 */
export function serviceRows(servicesJson: unknown): SvcRow[] {
  const rows: SvcRow[] = []
  for (const it of parseServices(servicesJson)) {
    const name = asString(it.name ?? it.serviceName ?? it.appName)
    if (!name) continue
    rows.push({
      role: toServiceRole(it.role ?? it.type),
      name,
      meta: serviceMeta(it),
    })
  }
  // 提供(0)在前、消费(1)在后；同组保持原顺序（sort 稳定）
  return rows.sort((a, b) => rankRole(a.role) - rankRole(b.role))
}

/** 服务附加信息：端口/实例等拼接为可读文本，无则空串 */
function serviceMeta(it: Record<string, unknown>): string {
  const parts: string[] = []
  const port = asString(it.port ?? it.address)
  if (port) parts.push(`端口 ${port}`)
  const instances = asNumber(it.instances ?? it.count)
  if (instances > 0) parts.push(`${instances} 实例`)
  return parts.join(' ')
}

function rankRole(role: ServiceRole): number {
  return role === 'provider' ? 0 : 1
}

function toServiceRole(v: unknown): ServiceRole {
  const s = asString(v).toLowerCase()
  if (s === 'consumer' || s === 'consume' || s.includes('client') || s.includes('消费')) return 'consumer'
  return 'provider'
}

function asString(v: unknown): string {
  if (typeof v === 'string') return v.trim()
  if (v === undefined || v === null) return ''
  return String(v).trim()
}

function asNumber(v: unknown): number {
  if (typeof v === 'number') return Number.isFinite(v) ? v : 0
  if (typeof v === 'string') {
    const n = Number.parseInt(v, 10)
    return Number.isNaN(n) ? 0 : n
  }
  return 0
}

/** 时间戳 → 'YYYY-MM-DD HH:mm'；缺失/非法 → '-' */
function formatTime(ts: string | number | undefined): string {
  const t = toTime(ts)
  if (t === null) return '-'
  const d = new Date(t)
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`
}

/** 距 now 时长：<60s→Xs；<60m→Xm；<24h→Xh Ym；否则 Xd；缺失/非法 → '-' */
function formatDuration(ts: string | number | undefined, now: number = Date.now()): string {
  const t = toTime(ts)
  if (t === null) return '-'
  const s = Math.max(0, Math.floor((now - t) / 1000))
  if (s < 60) return `${s}s`
  const m = Math.floor(s / 60)
  if (m < 60) return `${m}m`
  const h = Math.floor(m / 60)
  if (h < 24) return `${h}h ${m % 60}m`
  return `${Math.floor(h / 24)}d`
}

/** 字符串/时间戳 → epoch 毫秒；缺失/非法 → null */
function toTime(ts: string | number | undefined): number | null {
  if (ts === undefined || ts === null || ts === '') return null
  const t = typeof ts === 'number' ? ts : new Date(ts).getTime()
  return Number.isNaN(t) ? null : t
}

// ============ 节点页 UI 辅助导出（Nodes.vue 引用；配色仅用官方 --el-* 令牌 + color-mix 派生，零自定义 token） ============

/** 抽屉 9 Tab：name 对应模板分支，label 为胶囊文案 */
export const drawerTabs: Array<{ name: string; label: string }> = [
  { name: 'overview', label: '概览' },
  { name: 'online', label: '在线' },
  { name: 'history', label: '历史' },
  { name: 'commands', label: '指令' },
  { name: 'perf', label: '性能' },
  { name: 'framework', label: '框架推荐' },
  { name: 'appOnline', label: '应用在线' },
  { name: 'appDeploy', label: '应用发布' },
  { name: 'services', label: '服务' },
]

/** 状态横幅三态：12% 底 / 30% 边 / 同色字；text 为横幅文案 */
export function bannerFor(status: NodeStatus): { style: Record<string, string>; text: string } {
  const color = statusColor(status)
  return {
    style: {
      color,
      backgroundColor: `color-mix(in srgb, ${color} 12%, transparent)`,
      border: `1px solid color-mix(in srgb, ${color} 30%, transparent)`,
    },
    text: status === 'online'
      ? '● 在线 · 心跳正常'
      : status === 'warn'
        ? '▲ 告警 · CPU 超阈 · 需关注'
        : '■ 离线 · 心跳丢失',
  }
}

/** 药丸样式：15% 底 + 同色字 */
export function pillStyleFor(color: string): Record<string, string> {
  return {
    color,
    backgroundColor: `color-mix(in srgb, ${color} 15%, transparent)`,
  }
}

/** 指令药丸：重启/更新=绿(success)、采集=蓝(primary) */
export const commandPill: Record<CommandRole, { label: string; color: string }> = {
  restart: { label: '重启', color: 'var(--el-color-success)' },
  update: { label: '更新', color: 'var(--el-color-success)' },
  collect: { label: '采集', color: 'var(--el-color-primary)' },
}

/** 服务药丸：提供=绿(success)、消费=蓝(primary) */
export const servicePill: Record<ServiceRole, { label: string; color: string }> = {
  provider: { label: '提供', color: 'var(--el-color-success)' },
  consumer: { label: '消费', color: 'var(--el-color-primary)' },
}

/** 性能折线：点数组 → polyline points 串数组（null 断线分段）；svg viewBox 0 0 320 110 */
export function lineSegs(points: PerfPoint[], key: 'cpu' | 'memory'): string[] {
  const W = 320
  const H = 110
  const n = points.length
  if (n === 0) return []
  const segs: string[] = []
  let cur: string[] = []
  const flush = () => {
    if (cur.length >= 2) segs.push(cur.join(' '))
    cur = []
  }
  points.forEach((p, i) => {
    const v = p[key]
    if (v == null) {
      flush()
      return
    }
    const x = (i / (n - 1)) * W
    const y = H - (Math.min(100, Math.max(0, v)) / 100) * H
    cur.push(`${x.toFixed(1)},${y.toFixed(1)}`)
  })
  flush()
  return segs
}

/** 在线索引：nodeName 派生在线集合；同 nodeName 行数 = 该节点服务数 */
export function buildOnlineIndex(rows: AppOnlineRow[]): { onlineNames: Set<string>; counts: Map<string, number> } {
  const onlineNames = new Set<string>()
  const counts = new Map<string, number>()
  for (const r of rows ?? []) {
    const name = r.nodeName || r.name || ''
    if (!name) continue
    onlineNames.add(name)
    counts.set(name, (counts.get(name) ?? 0) + 1)
  }
  return { onlineNames, counts }
}
