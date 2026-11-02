// 概览页纯逻辑（与 UI 解耦，便于单测）：KPI 聚合、事件派生、相对时间格式化
// 真实数据来源（camelCase）：
//  - /Nodes/Node          节点列表（name/iP/lastActive/cpu/enable）
//  - /Registry/App        应用列表（name/displayName/version/enable/updateTime）
//  - /Registry/AppOnline  在线实例（appName/nodeName 派生在线集合）
//  - /Registry/AppService 注册服务（totalCount）
//  - /Monitors/AppMinuteStat 应用分钟统计（StatTime/AppId/AppName/Total/Errors/ErrorRate/Cost，每 5 分钟一条）
//  - /Monitors/AlarmRecord   报警记录（Name/Category/Action/Status/StartTime/Duration/Content，报警中=1）

export type EventStatus = 'online' | 'warn' | 'offline'

export interface KpiItem {
  label: string
  value: string | number
  unit?: string
  accent: 'primary' | 'success' | 'warning' | 'danger' | undefined
  icon: string
}

export interface EventItem {
  service: string
  detail: string
  status: EventStatus
  time: string
  tag: string
  /** 排序用时间戳（ms） */
  ts: number
}

export interface NodeRow {
  name?: string
  code?: string
  iP?: string
  lastActive?: string
  enable?: boolean
  [k: string]: unknown
}

export interface AppRow {
  name?: string
  version?: string
  enable?: boolean
  updateTime?: string
  [k: string]: unknown
}

/** 相对时间格式化：x 分钟前 / x 小时前 / x 天前（纯函数） */
export function timeAgo(iso: string | undefined, now: number): string {
  if (!iso) return '未知'
  const t = new Date(iso).getTime()
  if (Number.isNaN(t)) return '未知'
  const diffMin = Math.floor((now - t) / 60000)
  if (diffMin < 1) return '刚刚'
  if (diffMin < 60) return `${diffMin} 分钟前`
  const diffHour = Math.floor(diffMin / 60)
  if (diffHour < 24) return `${diffHour} 小时前`
  return `${Math.floor(diffHour / 24)} 天前`
}

/** 节点是否在线：最近一次活跃时间在 2 个心跳周期（默认 120s）内 */
export function isNodeOnline(node: NodeRow, now: number): boolean {
  if (!node.lastActive) return false
  const t = new Date(node.lastActive).getTime()
  return !Number.isNaN(t) && now - t <= 120_000
}

/** KPI 聚合：节点总数 / 在线应用 / 告警 / 注册服务 */
export function buildKpis(
  nodes: NodeRow[],
  apps: AppRow[],
  onlineAppNames: Set<string>,
  serviceTotal: number,
  now: number,
): KpiItem[] {
  const offlineNodes = nodes.filter((n) => !isNodeOnline(n, now)).length
  const offlineApps = apps.filter((a) => a.enable === false || !onlineAppNames.has(a.name ?? '')).length
  return [
    { label: '节点总数', value: nodes.length, accent: 'primary', icon: 'M5 12h14M12 5l7 7-7 7' },
    { label: '在线应用', value: onlineAppNames.size, accent: 'success', icon: 'M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z' },
    { label: '告警', value: offlineNodes + offlineApps, accent: 'warning', icon: 'M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z' },
    { label: '注册服务', value: serviceTotal, accent: undefined, icon: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10' },
  ]
}

export interface HealthSegment {
  label: string
  value: number
  color: string
}

/** 集群健康分段：在线 / 告警 / 离线 百分比（无节点时全 0） */
export function buildHealth(nodes: NodeRow[], now: number): HealthSegment[] {
  const total = nodes.length
  if (!total) {
    return [
      { label: '在线', value: 0, color: 'var(--el-color-success)' },
      { label: '告警', value: 0, color: 'var(--el-color-warning)' },
      { label: '离线', value: 0, color: 'var(--el-color-danger)' },
    ]
  }
  const online = nodes.filter((n) => isNodeOnline(n, now)).length
  const onlinePct = Math.round((online / total) * 100)
  const offlinePct = 100 - onlinePct
  return [
    { label: '在线', value: onlinePct, color: 'var(--el-color-success)' },
    { label: '告警', value: 0, color: 'var(--el-color-warning)' },
    { label: '离线', value: offlinePct, color: 'var(--el-color-danger)' },
  ]
}

/** 事件派生：离线节点 + 近期变更应用，按时间倒序取前 8 条 */
export function buildEvents(nodes: NodeRow[], apps: AppRow[], now: number): EventItem[] {
  const events: EventItem[] = []
  for (const n of nodes) {
    if (!isNodeOnline(n, now)) {
      events.push({
        service: n.name || n.code || '-',
        detail: '节点离线',
        status: 'offline',
        time: timeAgo(n.lastActive, now),
        tag: '节点',
        ts: new Date(n.lastActive ?? '').getTime() || 0,
      })
    }
  }
  for (const a of apps) {
    if (a.updateTime) {
      events.push({
        service: a.name || '-',
        detail: `版本 ${a.version || '-'} 活跃`,
        status: a.enable === false ? 'warn' : 'online',
        time: timeAgo(a.updateTime, now),
        tag: '应用',
        ts: new Date(a.updateTime).getTime() || 0,
      })
    }
  }
  return events.sort((a, b) => b.ts - a.ts).slice(0, 8)
}

/** 星图轨道节点：核心 + 8 类业务节点（对照 Doc/preview_home.png） */
export interface OrbitItem {
  label: string
  sub: string
  icon: string
  route: string
  left: number
  top: number
  main?: boolean
  color?: string
}

export const orbitItems: OrbitItem[] = [
  { label: '节点', sub: '集群·服务', icon: 'M4 7v10c0 2.21 3.582 4 8 4s8-1.79 8-4V7M4 7c0 2.21 3.582 4 8 4s8-1.79 8-4M4 7c0-2.21 3.582-4 8-4s8 1.79 8 4m0 5c0 2.21-3.582 4-8 4s-8-1.79-8-4', route: '/stardust/nodes', left: 50.0, top: 10.0, main: true, color: 'var(--el-color-success)' },
  { label: '配置', sub: '全局配置', icon: 'M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z M15 12a3 3 0 11-6 0 3 3 0 016 0z', route: '/stardust/settings', left: 78.3, top: 21.7, main: false },
  { label: '应用', sub: '配置·监控·发布', icon: 'M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4', route: '/stardust/apps', left: 90.0, top: 50.0, main: true, color: 'var(--el-color-primary)' },
  { label: '监控', sub: '实时监控', icon: 'M13 7h8m0 0v8m0-8l-8 8-4-4-6 6', route: '/Monitors/Trace', left: 78.3, top: 78.3, main: false },
  { label: '发布', sub: '流水线', icon: 'M15.59 14.37a6 6 0 01-5.84 7.18 6 6 0 01-5.84-7.18 6 6 0 0112 0zm0 0H3.75m0 0l-.08-.83a6 6 0 0111.89-3.18M3.75 14.37l-.08.83M20 2v6m0 0l3-3m-3 3l-3-3', route: '/Deployment/AppPipeline', left: 50.0, top: 90.0, main: false },
  { label: '概览', sub: '健康·事件', icon: 'M11.049 2.927c.3-.921 1.603-.921 1.902 0l1.519 4.674a1 1 0 00.95.69h4.915c.969 0 1.371 1.24.588 1.81l-3.976 2.888a1 1 0 00-.363 1.118l1.518 4.674c.3.922-.755 1.688-1.538 1.118l-3.976-2.888a1 1 0 00-1.176 0l-3.976 2.888c-.783.57-1.838-.197-1.538-1.118l1.518-4.674a1 1 0 00-.363-1.118l-3.976-2.888c-.784-.57-.38-1.81.588-1.81h4.914a1 1 0 00.951-.69l1.519-4.674z', route: '/stardust/overview', left: 21.7, top: 78.3, main: false },
  { label: '设置', sub: '全局配置', icon: 'M12 6V4m0 2a2 2 0 100 4m0-4a2 2 0 110 4m-6 8a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4m6 6v10m6-2a2 2 0 100-4m0 4a2 2 0 110-4m0 4v2m0-6V4', route: '/stardust/settings', left: 10.0, top: 50.0, main: true, color: 'var(--el-color-success)' },
  { label: '服务', sub: '服务注册', icon: 'M19.428 15.428a2 2 0 00-1.022-.547l-2.387-.477a6 6 0 00-3.86.517l-.318.158a6 6 0 01-3.86.517L6.05 15.21a2 2 0 00-1.806.547M8 4h8l-1 1v5.172a2 2 0 00.586 1.414l5 5c1.26 1.26.367 3.414-1.415 3.414H4.828c-1.782 0-2.674-2.154-1.414-3.414l5-5A2 2 0 009 10.172V5L8 4z', route: '/Registry/AppService', left: 21.7, top: 21.7, main: true, color: 'var(--el-color-primary)' },
]

/** 主枢纽节点（加强连线） */
export const mainOrbitItems = orbitItems.filter((i) => i.main)

/** 底部快捷入口 */
export interface ShortcutItem {
  label: string
  sub: string
  to: string
  icon: string
}

export const shortcuts: ShortcutItem[] = [
  { label: '配置中心', sub: '全局配置', to: '/stardust/settings', icon: 'M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z M15 12a3 3 0 11-6 0 3 3 0 016 0z' },
  { label: '注册发现', sub: '服务网格', to: '/stardust/apps', icon: 'M21 12a9 9 0 01-9 9m9-9a9 9 0 00-9-9m9 9H3m9 9c1.657 0 3-4.03 3-9s-1.343-9-3-9m0 18c-1.657 0-3-4.03-3-9s1.343-9 3-9m-9 9h18' },
  { label: '网关', sub: '流量入口', to: '/stardust/apps', icon: 'M13 10V3L4 14h7v7l9-11h-7z' },
  { label: '链路追踪', sub: '调用链·追踪', to: '/Trace/Index', icon: 'M4 6h16M4 12h16M4 18h7' },
]

/* ============ 运行概览（对照 Doc/preview_home.png 交互排版，颜色主题保留现有令牌） ============ */

/** 应用分钟统计行（/Monitors/AppMinuteStat，每 5 分钟一条） */
export interface MinuteStatRow {
  ID?: number
  StatTime?: string
  AppId?: number
  AppName?: string
  Total?: number
  Errors?: number
  ErrorRate?: number
  TotalCost?: number
  Cost?: number
  MaxCost?: number
  MinCost?: number
  [k: string]: unknown
}

/** 报警记录行（/Monitors/AlarmRecord，Alarming=1 / Recovered=2） */
export interface AlarmRow {
  Id?: number
  Name?: string
  Category?: string
  Action?: string
  Status?: number
  StartTime?: string
  EndTime?: string
  Duration?: number
  Content?: string
  [k: string]: unknown
}

/** 顶部统计卡片（图片：系统状态 / 运行实例 / QPS / 错误率 / P99 / 未处理告警） */
export interface SystemKpis {
  status: KpiItem
  instances: KpiItem
  qps: KpiItem
  errorRate: KpiItem
  p99: KpiItem
  alarms: KpiItem
}

/** 需要关注条目（告警中记录 + 离线节点 + 未在线应用），可点击跳转目标资源 */
export interface AttentionItem {
  key: string
  title: string
  desc: string
  time: string
  level: 'danger' | 'warning' | 'info'
  /** 条目类型：告警 / 节点 / 应用（决定跳转按钮文案与目标） */
  type: 'alarm' | 'node' | 'app'
  /** 点击条目跳转的目标路由 */
  targetRoute: string
  /** 右侧快捷按钮文案（查看应用 / 查看节点 / 查看 Trace） */
  actionLabel: string
}

/** 应用运行态势行 */
export interface AppHealthRow {
  name: string
  displayName?: string
  version?: string
  /** 在线 / 告警 / 离线 */
  status: 'online' | 'alarm' | 'offline'
}

/** 流量趋势点（QPS 折线图） */
export interface TrafficPoint {
  time: string
  qps: number
}

/** 服务拓扑节点（中心 + 外围应用；label 为完整中文说明，禁止裸缩写） */
export interface TopoNode {
  id?: number
  /** 原始应用名（跳转 / Inspector 用） */
  name?: string
  label: string
  sub: string
  online: boolean
  route: string
}

/** 服务拓扑依赖边：from 调用方应用名 → to 被调用方（提供服务的）应用名 */
export interface TopoEdge {
  from: string
  to: string
}

export interface Topology {
  center: TopoNode
  nodes: TopoNode[]
  /** 应用间有向依赖边（来源 AppConsume + AppService 关联；为空时由 UI 提示待接入） */
  edges: TopoEdge[]
}

/** 时间串 → HH:mm */
function toHM(iso: string | undefined): string {
  if (!iso) return '--:--'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return '--:--'
  return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

/** 持续秒数 → 中文时长（x 分钟 / x 小时） */
export function formatDuration(seconds: number | undefined): string {
  if (!seconds || seconds <= 0) return '持续中'
  const min = Math.floor(seconds / 60)
  if (min < 60) return `${min} 分钟`
  const hour = Math.floor(min / 60)
  if (hour < 24) return `${hour} 小时 ${min % 60} 分钟`
  return `${Math.floor(hour / 24)} 天`
}

/** 分钟统计桶：按统计时间分组，组内跨应用求和 */
export interface MinuteBucket {
  time: string
  total: number
  errors: number
  costs: number[]
}

/** 聚合分钟统计为按统计时间升序的桶（默认最近 24 个统计点 ≈ 2 小时） */
export function buildMinuteBuckets(rows: MinuteStatRow[], max = 24): MinuteBucket[] {
  const map = new Map<string, MinuteBucket>()
  for (const r of rows) {
    if (!r.StatTime) continue
    const key = String(r.StatTime).slice(0, 16)
    const bucket = map.get(key) ?? { time: key, total: 0, errors: 0, costs: [] as number[] }
    bucket.total += Number(r.Total ?? 0)
    bucket.errors += Number(r.Errors ?? 0)
    if (r.Cost != null) bucket.costs.push(Number(r.Cost))
    map.set(key, bucket)
  }
  return [...map.values()]
    .sort((a, b) => (a.time < b.time ? -1 : a.time > b.time ? 1 : 0))
    .slice(-max)
}

/** 顶部统计卡片：全部基于真实数据计算，QPS = 最近桶 Total / 300s，P99 用平均耗时（逼近 TP99） */
export function buildSystemKpis(
  nodes: NodeRow[],
  onlineNodes: number,
  buckets: MinuteBucket[],
  alarmingCount: number,
): SystemKpis {
  const last = buckets[buckets.length - 1]
  const qpsRaw = last ? last.total / 300 : 0
  const qpsText = qpsRaw >= 1000 ? `${(qpsRaw / 1000).toFixed(1)}k` : qpsRaw.toFixed(1)
  const errRate = last && last.total > 0 ? last.errors / last.total : 0
  const p99 = last && last.costs.length > 0 ? Math.round(last.costs.reduce((s, c) => s + c, 0) / last.costs.length) : 0
  const offlineNodes = nodes.length - onlineNodes
  const systemOk = nodes.length > 0 && offlineNodes === 0 && alarmingCount === 0

  return {
    status: {
      label: '系统状态',
      value: systemOk ? '正常' : '异常',
      accent: systemOk ? 'success' : 'danger',
      icon: 'M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z',
    },
    instances: {
      label: '运行实例',
      value: onlineNodes,
      accent: 'primary',
      icon: 'M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4',
    },
    qps: {
      label: 'QPS',
      value: qpsText,
      unit: '/s',
      accent: 'primary',
      icon: 'M13 7h8m0 0v8m0-8l-8 8-4-4-6 6',
    },
    errorRate: {
      label: '错误率',
      value: errRate > 0 ? (errRate * 100).toFixed(2) : '0.00',
      unit: '%',
      accent: errRate >= 0.01 ? 'danger' : errRate > 0 ? 'warning' : 'success',
      icon: 'M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z',
    },
    p99: {
      label: 'P99 耗时',
      value: p99,
      unit: 'ms',
      accent: p99 >= 500 ? 'warning' : 'primary',
      icon: 'M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z',
    },
    alarms: {
      label: '未处理告警',
      value: alarmingCount,
      accent: alarmingCount > 0 ? 'danger' : 'success',
      icon: 'M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9',
    },
  }
}

/** 需要关注：报警中告警 → 离线节点 → 未在线应用，取前 6 */
export function buildAttention(
  alarms: AlarmRow[],
  nodes: NodeRow[],
  apps: AppRow[],
  onlineAppNames: Set<string>,
  now: number,
): AttentionItem[] {
  const items: AttentionItem[] = []
  for (const a of alarms) {
    items.push({
      key: `alarm-${a.Id ?? a.Name ?? Math.random()}`,
      title: a.Action || a.Name || '-',
      desc: a.Category || '告警中',
      time: formatDuration(a.Duration),
      level: 'danger',
      type: 'alarm',
      targetRoute: '/Monitors/AppTracer',
      actionLabel: '查看 Trace',
    })
  }
  for (const n of nodes) {
    if (!isNodeOnline(n, now)) {
      items.push({
        key: `node-${n.name ?? n.code ?? Math.random()}`,
        title: n.name || n.code || '-',
        desc: '节点离线',
        time: timeAgo(n.lastActive, now),
        level: 'danger',
        type: 'node',
        targetRoute: n.id != null ? `/Nodes/Node/${n.id}` : '/stardust/nodes',
        actionLabel: '查看节点',
      })
    }
  }
  for (const a of apps) {
    const name = a.name ?? ''
    if (a.enable === false || (name && !onlineAppNames.has(name))) {
      items.push({
        key: `app-${name || Math.random()}`,
        title: name || '-',
        desc: '应用未在线',
        time: timeAgo(a.updateTime, now),
        level: 'warning',
        type: 'app',
        targetRoute: a.id != null ? `/Registry/App/${a.id}` : '/stardust/apps',
        actionLabel: '查看应用',
      })
    }
  }
  return items.slice(0, 6)
}

/** 应用运行态势：在线 / 告警（有报警中记录）/ 离线 */
export function buildAppHealth(
  apps: AppRow[],
  onlineAppNames: Set<string>,
  alarms: AlarmRow[],
): AppHealthRow[] {
  const alarming = new Set<string>()
  for (const a of alarms) {
    if (a.Action) alarming.add(a.Action)
  }
  return apps.map((a) => {
    const name = a.name ?? '-'
    let status: AppHealthRow['status'] = 'online'
    if (a.enable === false || !onlineAppNames.has(name)) status = 'offline'
    if (alarming.has(name)) status = 'alarm'
    return { name, displayName: (a.displayName as string | undefined) ?? undefined, version: a.version, status }
  })
}

/** 整体流量趋势：按桶输出 QPS 序列（Total/300s），供 ECharts 折线图 */
export function buildTrafficSeries(buckets: MinuteBucket[]): TrafficPoint[] {
  return buckets.map((b) => ({ time: toHM(b.time), qps: Number((b.total / 300).toFixed(1)) }))
}

/** 单应用运行指标（拓扑 Inspector 用）：按 AppName 取最近一条分钟统计，QPS=Total/300s（5分钟桶）；P99 用平均耗时逼近 */
export function appMetrics(rows: MinuteStatRow[], appName: string | undefined): { qps: number; errorRate: number; p99: number } {
  if (!appName) return { qps: 0, errorRate: 0, p99: 0 }
  const mine = rows
    .filter((r) => r.AppName === appName)
    .sort((a, b) => String(a.StatTime ?? '').localeCompare(String(b.StatTime ?? '')))
  const last = mine[mine.length - 1]
  if (!last) return { qps: 0, errorRate: 0, p99: 0 }
  const total = Number(last.Total ?? 0)
  const errors = Number(last.Errors ?? 0)
  const errRate = last.ErrorRate != null ? Number(last.ErrorRate) : total > 0 ? errors / total : 0
  const cost = Number(last.Cost ?? 0)
  return { qps: Number((total / 300).toFixed(1)), errorRate: errRate, p99: Math.round(cost) }
}

/** 服务拓扑：中心为网关（应用列表含 gateway 时）或星尘核心，外围为真实应用节点（label 必带中文，禁止裸缩写） */
export function buildTopology(
  apps: AppRow[],
  onlineAppNames: Set<string>,
  consumes: AppRow[] = [],
  services: AppRow[] = [],
): Topology {
  const list = apps.slice(0, 12)
  const gateway = list.find(
    (a) => /gateway|网关/i.test(a.name ?? '') || /gateway|网关/i.test(String(a.displayName ?? '')),
  )
  const center: TopoNode = gateway
    ? {
        id: gateway.id as number | undefined,
        name: gateway.name,
        label: '网关',
        sub: gateway.name ?? 'gateway',
        online: onlineAppNames.has(gateway.name ?? ''),
        route: '/stardust/apps',
      }
    : { label: '星尘核心', sub: 'Stardust 控制台', online: true, route: '/stardust/overview' }
  const nodes: TopoNode[] = list
    .filter((a) => a !== gateway)
    .map((a) => {
      const name = a.name ?? '-'
      const displayName = (a.displayName as string | undefined) ?? ''
      // 禁止裸英文：无中文 displayName 时 label 用「应用（name）」，sub 保留原始英文名对照
      const label = displayName && displayName !== name ? displayName : `应用（${name}）`
      const sub = name
      return {
        id: a.id as number | undefined,
        name,
        label,
        sub,
        online: onlineAppNames.has(name),
        route: '/stardust/apps',
      }
    })

  // 应用间有向依赖边：AppConsume(Client 调用方 → ServiceName) 关联 AppService(ServiceName → appName 提供方)
  const appNames = new Set(list.map((a) => a.name).filter(Boolean) as string[])
  const providerByService = new Map<string, string>()
  for (const s of services) {
    const svc = String(s.serviceName ?? s.ServiceName ?? '')
    const provider = String(s.appName ?? s.AppName ?? '')
    if (svc && provider) providerByService.set(svc, provider)
  }
  const seen = new Set<string>()
  const edges: TopoEdge[] = []
  for (const c of consumes) {
    const caller = String(c.Client ?? c.client ?? '')
    const svc = String(c.ServiceName ?? c.serviceName ?? '')
    const provider = providerByService.get(svc)
    if (!caller || !provider || caller === provider) continue
    // 仅当两端都在拓扑可见应用集合内时连线，避免出现悬空节点
    if (!appNames.has(caller) || !appNames.has(provider)) continue
    const key = `${caller}->${provider}`
    if (seen.has(key)) continue
    seen.add(key)
    edges.push({ from: caller, to: provider })
  }

  return { center, nodes, edges }
}

// ===== 工作台（Workspace · 设计稿 src/page-workspace.jsx） =====

/** 工作台指标条单项 */
export interface WsMetric {
  key: string
  label: string
  value: string
  unit: string
  delta: string
  dir: 'up' | 'down' | 'flat' | 'none'
  tone: 'brand' | 'cyan' | 'warning' | 'success' | 'danger' | 'info'
  /** delta 是否为正面信号（决定配色） */
  good: boolean
}

/** 星座卫星（应用） */
export interface WsSatellite {
  name: string
  label: string
  qps: number
  inst: number
  status: 'online' | 'warn' | 'alert' | 'offline'
  angle: number
  /** 相对面板中心的百分比偏移（与 SVG viewBox 0-100 同系） */
  dx: number
  dy: number
}

/** 星座健康度汇总 */
export interface WsHealth {
  score: number
  online: number
  warn: number
  alert: number
  total: number
}

/** 调用矩阵单元格 */
export interface WsMatrixCell {
  value: number
  ratio: number
  hit: boolean
}

/** 调用矩阵行 */
export interface WsMatrixRow {
  key: string
  label: string
  cells: WsMatrixCell[]
}

/** 调用矩阵（调用方 × 被调方） */
export interface WsMatrix {
  cols: { key: string; label: string }[]
  rows: WsMatrixRow[]
  max: number
  edgeCount: number
}

/** 星座/拓扑轨道半径（百分比，与 SVG 视口同系） */
export const WS_ORBIT_RX = 20
export const WS_ORBIT_RY = 26

/** 工作台指标条：QPS / 错误率 / P99 / 实例 / 未处理告警 / 发布中 */
export function buildWorkspaceMetrics(
  apps: AppRow[],
  onlineAppNames: Set<string>,
  rows: MinuteStatRow[],
  alarmingCount: number,
  activeReleases: number,
  instTotal: number,
): WsMetric[] {
  let qps = 0
  let qpsSum = 0
  let weighted = 0
  let maxP99 = 0
  for (const a of apps) {
    const m = appMetrics(rows, a.name)
    qps += m.qps
    qpsSum += m.qps
    weighted += m.errorRate * m.qps
    if (m.p99 > maxP99) maxP99 = m.p99
  }
  const errPct = qpsSum > 0 ? (weighted / qpsSum) * 100 : 0
  const onlineApps = apps.filter((a) => a.enable !== false && onlineAppNames.has(a.name ?? '')).length
  return [
    {
      key: 'qps',
      label: 'QPS',
      value: qps >= 1000 ? (qps / 1000).toFixed(1) + 'k' : qps.toFixed(1),
      unit: '/s',
      delta: onlineApps > 0 ? '实时采样' : '无采样',
      dir: 'flat',
      tone: 'brand',
      good: true,
    },
    {
      key: 'err',
      label: '错误率',
      value: errPct.toFixed(2),
      unit: '%',
      delta: errPct > 1 ? '超阈值 1%' : '阈值内',
      dir: errPct > 1 ? 'down' : 'up',
      tone: errPct > 1 ? 'warning' : 'success',
      good: errPct <= 1,
    },
    {
      key: 'p99',
      label: 'P99',
      value: String(maxP99),
      unit: 'ms',
      delta: maxP99 >= 500 ? '偏高' : '正常',
      dir: maxP99 >= 500 ? 'down' : 'up',
      tone: maxP99 >= 500 ? 'warning' : 'success',
      good: maxP99 < 500,
    },
    {
      key: 'inst',
      label: '实例',
      value: String(instTotal),
      unit: '个',
      delta: onlineApps > 0 ? '在线应用 ' + onlineApps : '无在线',
      dir: 'flat',
      tone: instTotal > 0 ? 'success' : 'warning',
      good: instTotal > 0,
    },
    {
      key: 'alarm',
      label: '未处理告警',
      value: String(alarmingCount),
      unit: '项',
      delta: alarmingCount > 0 ? '待处理' : '无告警',
      dir: 'flat',
      tone: alarmingCount > 0 ? 'danger' : 'success',
      good: alarmingCount === 0,
    },
    {
      key: 'release',
      label: '发布中',
      value: String(activeReleases),
      unit: '项',
      delta: activeReleases > 0 ? '进行中' : '空闲',
      dir: 'flat',
      tone: activeReleases > 0 ? 'info' : 'success',
      good: true,
    },
  ]
}

/** 星座卫星：异常优先、其余按 QPS 降序，取前 limit 个，沿椭圆轨道均布 */
export function buildSatellites(
  apps: AppRow[],
  onlineAppNames: Set<string>,
  rows: MinuteStatRow[],
  alarms: AlarmRow[],
  instCount: Map<string, number>,
  limit = 8,
): WsSatellite[] {
  const alarming = new Set<string>()
  for (const a of alarms) if (a.Action) alarming.add(a.Action)
  const decorated = apps.map((a) => {
    const name = a.name ?? '-'
    const m = appMetrics(rows, name)
    let status: WsSatellite['status'] = 'online'
    if (a.enable === false || !onlineAppNames.has(name)) status = 'offline'
    if (alarming.has(name)) status = 'alert'
    return {
      name,
      label: (a.displayName as string | undefined) || name,
      qps: m.qps,
      inst: instCount.get(name) ?? 0,
      status,
      angle: 0,
      dx: 0,
      dy: 0,
    }
  })
  const rank = (s: WsSatellite['status']): number =>
    s === 'alert' ? 0 : s === 'warn' ? 1 : s === 'offline' ? 2 : 3
  const picked = decorated
    .sort((a, b) => rank(a.status) - rank(b.status) || b.qps - a.qps)
    .slice(0, limit)
  const n = picked.length
  return picked.map((s, i) => {
    const angle = n > 0 ? (i / n) * Math.PI * 2 - Math.PI / 2 : 0
    return {
      ...s,
      angle,
      dx: Number((Math.cos(angle) * WS_ORBIT_RX).toFixed(2)),
      dy: Number((Math.sin(angle) * WS_ORBIT_RY).toFixed(2)),
    }
  })
}

/** 星座健康度（0-100）：未处理告警与加权错误率扣分（设计稿同式） */
export function aggregateHealth(
  satellites: WsSatellite[],
  alarmingCount: number,
  weightedErrPct: number,
): WsHealth {
  const score = Math.round(Math.max(0, Math.min(100, 100 - alarmingCount * 4 - weightedErrPct * 8)))
  return {
    score,
    online: satellites.filter((s) => s.status === 'online').length,
    warn: satellites.filter((s) => s.status === 'warn').length,
    alert: satellites.filter((s) => s.status === 'alert' || s.status === 'offline').length,
    total: satellites.length,
  }
}

/**
 * 调用矩阵：行 = 调用方，列 = 被调方，单元格 = 调用方 QPS 按出度均摊
 * 说明：后端未提供「按边 QPS」，故用调用方最新 QPS 作边权代理并除以出度，
 *       避免同一调用方的多条边权重相同（此为已知偏差，已记入方案文档）。
 */
export function buildMatrix(topology: Topology, rows: MinuteStatRow[], limit = 10): WsMatrix {
  const labelOf = (name: string): string => {
    const hit = topology.nodes.find((n) => n.name === name)
    return hit ? hit.label : name
  }
  const edges = topology.edges ?? []
  const outCount = new Map<string, number>()
  for (const e of edges) outCount.set(e.from, (outCount.get(e.from) ?? 0) + 1)
  const rowKeys: string[] = []
  const colKeys: string[] = []
  for (const e of edges) {
    if (rowKeys.indexOf(e.from) < 0) rowKeys.push(e.from)
    if (colKeys.indexOf(e.to) < 0) colKeys.push(e.to)
  }
  const rowList = rowKeys.slice(0, limit)
  const colList = colKeys.slice(0, limit)
  const hasEdge = (a: string, b: string): boolean => edges.some((e) => e.from === a && e.to === b)
  const valueOf = (name: string): number => {
    const deg = outCount.get(name) ?? 1
    const qps = appMetrics(rows, name).qps
    return Math.round(deg > 0 ? qps / deg : qps)
  }
  let max = 0
  const rowsOut: WsMatrixRow[] = rowList.map((r) => {
    const cells: WsMatrixCell[] = colList.map((c) => {
      const hit = hasEdge(r, c)
      const value = hit ? valueOf(r) : 0
      if (value > max) max = value
      return { value, ratio: 0, hit }
    })
    return { key: r, label: labelOf(r), cells }
  })
  const safeMax = max > 0 ? max : 1
  for (const r of rowsOut) {
    for (const c of r.cells) c.ratio = c.hit ? c.value / safeMax : 0
  }
  return {
    cols: colList.map((c) => ({ key: c, label: labelOf(c) })),
    rows: rowsOut,
    max,
    edgeCount: edges.length,
  }
}

/** 单应用 60 分钟 QPS 迷你序列（按分钟桶求和后除以 300s，取末尾 len 点） */
export function buildSpark(rows: MinuteStatRow[], appName: string, len = 24): number[] {
  const buckets = new Map<string, number>()
  for (const r of rows) {
    if (r.AppName !== appName || !r.StatTime) continue
    const key = String(r.StatTime).slice(0, 16)
    buckets.set(key, (buckets.get(key) ?? 0) + Number(r.Total ?? 0))
  }
  const arr = [...buckets.entries()]
    .sort((a, b) => (a[0] < b[0] ? -1 : a[0] > b[0] ? 1 : 0))
    .map(([, t]) => Number((t / 300).toFixed(2)))
  return arr.slice(-len)
}
