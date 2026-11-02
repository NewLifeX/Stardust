/**
 * 右侧全局实时数据面板 —— 数据层
 * ---------------------------------------------------------------------------
 * 设计真源：design-handoff/ui_kits/console/RightPanel.jsx
 *   四段：KPI 迷你卡组（2×2）/ 集群健康条 / 最近事件流 / 快捷入口（4 列）
 *   设计稿 props：{ kpis, health, events, shortcuts }
 *
 * 本文件为纯逻辑层（无样式）：自持取数 + 构造视图模型，不依赖其它页面的模型层，
 * 以保证面板可在任意控制台路由常驻而不产生耦合。
 *
 * 数据源（均为工程既有、已验证可用的注册中心端点）：
 *   /Registry/Node       节点清单（节点总数 / 在线判定）
 *   /Registry/App        应用清单（在线应用 / 停用告警）
 *   /Registry/AppOnline  在线实例（在线应用去重口径）
 *   /Registry/AppService 服务清单（注册服务数）
 *
 * 诚实标注：注册中心数据**不含告警级别**，故「集群健康」的 warn 段无法从数据推导，
 *           实际只产出 在线 / 离线 两段（warn 恒为 0）；图例仍按设计稿渲染三段。
 */

export interface ReqOptions {
  url: string
  method?: string
  params?: Record<string, unknown>
}
export type Req = (options: ReqOptions) => Promise<{ data?: unknown }>

/** KPI 迷你卡的色调档（对应设计稿 tone） */
export type PanelTone = 'brand' | 'cyan' | 'warning' | 'success'

export interface PanelKpi {
  label: string
  value: string | number
  tone: PanelTone
}

/** 集群健康占比（百分比，三项合计 100） */
export interface PanelHealth {
  online: number
  warn: number
  offline: number
}

export type PanelEventKind = 'ok' | 'warn' | 'danger' | 'info'

export interface PanelEvent {
  /** 相对时间文案（如「2 分钟前」） */
  t: string
  kind: PanelEventKind
  msg: string
  /** 条目类型标签（应用 / 节点 / 发布 / 配置） */
  tag: string
}

export interface PanelShortcut {
  label: string
  path: string
}

export interface RightPanelData {
  kpis: PanelKpi[]
  health: PanelHealth
  events: PanelEvent[]
  shortcuts: PanelShortcut[]
}

/** 快捷入口（对齐设计稿四项，映射到本工程真实路由） */
export const panelShortcuts: PanelShortcut[] = [
  { label: '配置中心', path: '/stardust/config' },
  { label: '注册发现', path: '/stardust/service' },
  { label: '网关', path: '/stardust/gateway' },
  { label: '可观测', path: '/stardust/observability' },
]

/* ===== 取值归一化（兼容 PascalCase / camelCase） ===== */
function pick(obj: Record<string, unknown> | undefined, ...keys: string[]): unknown {
  if (!obj) return undefined
  for (const k of keys) {
    if (obj[k] !== undefined && obj[k] !== null) return obj[k]
  }
  return undefined
}
function str(v: unknown): string {
  return v == null ? '' : String(v)
}
function num(v: unknown): number {
  const n = Number(v)
  return Number.isFinite(n) ? n : 0
}
function parseTime(v: unknown): number {
  const s = str(v)
  if (!s) return 0
  const d = new Date(s.includes('T') ? s : s.replace(' ', 'T'))
  return Number.isNaN(d.getTime()) ? 0 : d.getTime()
}
function isDisabled(v: unknown): boolean {
  if (v === undefined || v === null || v === '') return false
  return v === false || v === 0 || v === '0' || v === 'false'
}

/** 节点在线判定：有可解析心跳时间且距今 < 5 分钟；无时间字段时按在线处理（避免误判全离线） */
const ONLINE_WINDOW_MS = 5 * 60 * 1000

export function isNodeAlive(row: Record<string, unknown>, now: number): boolean {
  const ts = parseTime(pick(row, 'updateTime', 'UpdateTime', 'heartbeat', 'Heartbeat', 'pingTime', 'PingTime'))
  if (!ts) return true
  return now - ts < ONLINE_WINDOW_MS
}

/** 相对时间文案 */
export function relTime(ts: number, now: number): string {
  if (!ts) return '—'
  const d = Math.max(0, now - ts)
  const min = Math.floor(d / 60000)
  if (min < 1) return '刚刚'
  if (min < 60) return `${min} 分钟前`
  const h = Math.floor(min / 60)
  if (h < 24) return `${h} 小时前`
  return `${Math.floor(h / 24)} 天前`
}

/** 由原始行构造面板数据 */
export function buildRightPanel(
  nodes: Record<string, unknown>[],
  apps: Record<string, unknown>[],
  onlineRows: Record<string, unknown>[],
  serviceRows: Record<string, unknown>[],
  alarmRows: Record<string, unknown>[],
  now: number,
): RightPanelData {
  /* --- 在线 / 离线节点 --- */
  let alive = 0
  let dead = 0
  const offlineNodes: Record<string, unknown>[] = []
  for (const n of nodes) {
    if (isNodeAlive(n, now)) alive++
    else {
      dead++
      offlineNodes.push(n)
    }
  }

  /* --- 在线应用（按应用名去重） --- */
  const onlineApps = new Set<string>()
  for (const r of onlineRows) {
    const name = str(pick(r, 'appName', 'AppName', 'name', 'Name'))
    if (name) onlineApps.add(name)
  }

  /* --- 注册服务数（按服务名去重） --- */
  const services = new Set<string>()
  for (const r of serviceRows) {
    const name = str(pick(r, 'serviceName', 'ServiceName'))
    if (name) services.add(name)
  }

  /* --- 停用应用 --- */
  const disabledApps = apps.filter((a) => isDisabled(pick(a, 'enable', 'Enable')))

  const kpis: PanelKpi[] = [
    { label: '节点总数', value: nodes.length, tone: 'brand' },
    { label: '在线应用', value: onlineApps.size, tone: 'cyan' },
    { label: '异常告警', value: alarmRows.length, tone: 'warning' },
    { label: '注册服务', value: services.size, tone: 'success' },
  ]

  /* --- 集群健康（warn 无法从注册中心数据推导，恒为 0） --- */
  const total = alive + dead
  const health: PanelHealth =
    total === 0
      ? { online: 0, warn: 0, offline: 0 }
      : {
          online: Math.round((alive / total) * 100),
          warn: 0,
          offline: 100 - Math.round((alive / total) * 100),
        }

  /* --- 最近事件：由节点离线 + 应用停用派生（无告警数据源，故不伪造告警事件） --- */
  const events: PanelEvent[] = []
  for (const n of offlineNodes) {
    const name = str(pick(n, 'name', 'Name', 'nodeName', 'NodeName')) || '未知节点'
    events.push({
      t: relTime(parseTime(pick(n, 'updateTime', 'UpdateTime', 'heartbeat', 'Heartbeat')), now),
      kind: 'danger',
      msg: `${name} 心跳超时，已摘除`,
      tag: '节点',
    })
  }
  for (const a of disabledApps) {
    const name = str(pick(a, 'displayName', 'DisplayName', 'name', 'Name')) || '未知应用'
    events.push({
      t: relTime(parseTime(pick(a, 'updateTime', 'UpdateTime', 'createTime', 'CreateTime')), now),
      kind: 'warn',
      msg: `${name} 已停用`,
      tag: '应用',
    })
  }
  if (nodes.length > 0 && events.length === 0) {
    events.push({ t: '刚刚', kind: 'ok', msg: `全部 ${nodes.length} 个节点心跳正常`, tag: '节点' })
  }

  return { kpis, health, events: events.slice(0, 12), shortcuts: panelShortcuts }
}

/** 一次性取数并构造（全部 try-catch，失败回落空集，绝不 throw） */
export async function fetchRightPanel(request: Req): Promise<RightPanelData> {
  const now = Date.now()
  const get = async (url: string, params: Record<string, unknown> = { PageSize: 500 }): Promise<Record<string, unknown>[]> => {
    try {
      const res = await request({ url, method: 'GET', params })
      const list = res?.data
      return Array.isArray(list) ? (list as Record<string, unknown>[]) : []
    } catch {
      return []
    }
  }
  const [nodes, apps, onlineRows, serviceRows, alarmRows] = await Promise.all([
    get('/Nodes/Node', { PageSize: 200 }),
    get('/Registry/App', { PageSize: 200 }),
    get('/Registry/AppOnline'),
    get('/Registry/AppService'),
    get('/Monitors/AlarmRecord', { PageSize: 100, status: 1 }),
  ])
  return buildRightPanel(nodes, apps, onlineRows, serviceRows, alarmRows, now)
}
