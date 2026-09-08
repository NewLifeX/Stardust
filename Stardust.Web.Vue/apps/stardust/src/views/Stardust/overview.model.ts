// 概览页纯逻辑（与 UI 解耦，便于单测）：KPI 聚合、事件派生、相对时间格式化
// 真实数据来源（camelCase）：
//  - /Nodes/Node          节点列表（name/iP/lastActive/cpu/enable）
//  - /Registry/App        应用列表（name/displayName/version/enable/updateTime）
//  - /Registry/AppOnline  在线实例（appName/nodeName 派生在线集合）
//  - /Registry/AppService 注册服务（totalCount）

export type EventStatus = 'online' | 'warn' | 'offline'

export interface KpiItem {
  label: string
  value: number
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
  { label: '日志中心', sub: '调用链·日志', to: '/Trace/Index', icon: 'M4 6h16M4 12h16M4 18h7' },
]
