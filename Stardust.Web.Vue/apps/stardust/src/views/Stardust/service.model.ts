/**
 * 服务中心 · 数据层（类型定义 + API 调用）
 *
 * 依据《Stardust_交互设计重构方案.md》§10 服务中心：
 *   有哪些服务 / 谁提供 / 谁消费 / 当前是否健康 / 谁调用谁
 *
 * 数据源（camelCase 与 PascalCase 双兼容，NewLife XCode 列表默认下发 PascalCase）：
 *  - 应用列表     /Registry/App            id/name/displayName（AppId → 中文名 join）
 *  - 应用服务     /Registry/AppService      AppId/ServiceName/Client/Address/Version/Enable/PingCount（提供者=应用提供服务）
 *  - 应用消费     /Registry/AppConsume      AppId/ServiceName/Enable（消费者=应用消费服务；调用方/下游）
 *
 * 纪律：所有 fetch 包 try-catch，API 失败返回空数组/默认值，绝不 throw（避免全局错误弹窗）。
 * 样式纪律：纯逻辑层，无样式；服务名/应用名禁止裸缩写，详情带中文说明。
 */

// ===== 请求类型（与 @newlifex/cube-vue request 兼容）=====
export interface ReqOptions {
  url: string
  method?: string
  params?: Record<string, unknown>
}
export type Req = (options: ReqOptions) => Promise<{ data?: unknown }>

// ===== 状态（复用 StatusPill 七态）=====
export type HealthStatus = 'running' | 'error' | 'offline'

export const healthStatusLabel: Record<HealthStatus, string> = {
  running: '健康',
  error: '异常',
  offline: '离线',
}

/** 状态筛选 chips（§10.2：全部/健康/异常/离线） */
export const serviceStatusChips: { key: 'all' | HealthStatus; label: string }[] = [
  { key: 'all', label: '全部' },
  { key: 'running', label: '健康' },
  { key: 'error', label: '异常' },
  { key: 'offline', label: '离线' },
]

// ===== 应用名映射 =====
export interface AppInfo {
  id: number
  name: string
  displayName: string
}

/** 应用 → 中文名（禁止裸缩写：优先 displayName，其次 name，最后兜底「应用(id)」） */
export function appLabel(app: AppInfo | undefined, fallback = '未知应用'): string {
  if (!app) return fallback
  return app.displayName || app.name || `应用(${app.id})`
}

// ===== 原始行 =====
/** AppService 行（提供者记录：一个应用实例对外提供某服务） */
export interface AppServiceRow {
  id?: number
  appId?: number
  serviceId?: number
  serviceName?: string
  client?: string
  nodeId?: number
  enable?: boolean
  pingCount?: number
  version?: string
  address?: string
  originAddress?: string
  externalAddress?: string
  updateTime?: string
  [k: string]: unknown
}

/** AppConsume 行（消费者记录：一个应用实例消费某下游服务） */
export interface AppConsumeRow {
  id?: number
  appId?: number
  serviceId?: number
  serviceName?: string
  client?: string
  nodeId?: number
  enable?: boolean
  pingCount?: number
  [k: string]: unknown
}

// ===== 归一化取值（兼容 PascalCase / camelCase）=====
function pick(obj: Record<string, unknown> | undefined, ...keys: string[]): unknown {
  if (!obj) return undefined
  for (const k of keys) {
    if (obj[k] !== undefined && obj[k] !== null) return obj[k]
  }
  return undefined
}

function num(v: unknown): number {
  const n = Number(v)
  return Number.isFinite(n) ? n : 0
}

function str(v: unknown): string {
  return v == null ? '' : String(v)
}

function bool(v: unknown): boolean {
  if (v === undefined || v === null || v === '') return true
  return Boolean(v)
}

/** AppService 原始行 → 归一化 */
function normService(r: Record<string, unknown>): AppServiceRow {
  return {
    id: num(pick(r, 'id', 'Id')),
    appId: num(pick(r, 'appId', 'AppId')),
    serviceId: num(pick(r, 'serviceId', 'ServiceId')),
    serviceName: str(pick(r, 'serviceName', 'ServiceName')),
    client: str(pick(r, 'client', 'Client')),
    nodeId: num(pick(r, 'nodeId', 'NodeId')),
    enable: bool(pick(r, 'enable', 'Enable')),
    pingCount: num(pick(r, 'pingCount', 'PingCount')),
    version: str(pick(r, 'version', 'Version')),
    address: str(pick(r, 'address', 'Address', 'externalAddress', 'ExternalAddress')),
    updateTime: str(pick(r, 'updateTime', 'UpdateTime')),
  }
}

/** AppConsume 原始行 → 归一化 */
function normConsume(r: Record<string, unknown>): AppConsumeRow {
  return {
    id: num(pick(r, 'id', 'Id')),
    appId: num(pick(r, 'appId', 'AppId')),
    serviceId: num(pick(r, 'serviceId', 'ServiceId')),
    serviceName: str(pick(r, 'serviceName', 'ServiceName')),
    client: str(pick(r, 'client', 'Client')),
    nodeId: num(pick(r, 'nodeId', 'NodeId')),
    enable: bool(pick(r, 'enable', 'Enable')),
    pingCount: num(pick(r, 'pingCount', 'PingCount')),
  }
}

// ===== 服务中心聚合行 =====
/** 调用方/被调用方一项（应用 + 说明，禁止裸缩写） */
export interface ServiceRef {
  appId: number
  /** 中文展示名（displayName 或 name 兜底） */
  label: string
  address?: string
  version?: string
}

/** 服务中心列表行（按 ServiceName 聚合） */
export interface ServiceCenterRow {
  serviceName: string
  /** 提供者应用数（去重 AppId） */
  providerCount: number
  /** 提供者实例数（AppService 记录数） */
  instanceCount: number
  /** 消费者应用数（去重 AppId） */
  consumerCount: number
  status: HealthStatus
  /** 最近版本（任一提供者版本） */
  version: string
  /** 对外地址集合 */
  addresses: string[]
  /** 提供者应用列表 */
  providers: ServiceRef[]
  /** 调用方（谁消费了本服务） */
  callers: ServiceRef[]
  /** 被调用方（本服务的提供者又调用了哪些下游服务） */
  callees: ServiceRef[]
}

/** 由提供者记录推导服务状态：无提供者=离线；全部停用=离线；有心跳=健康；启用无心跳=异常 */
function deriveStatus(rows: AppServiceRow[]): HealthStatus {
  if (rows.length === 0) return 'offline'
  const enabled = rows.filter((r) => r.enable)
  if (enabled.length === 0) return 'offline'
  const alive = enabled.some((r) => (r.pingCount ?? 0) > 0)
  return alive ? 'running' : 'error'
}

/** 服务名 → 中文兜底（禁止裸英文缩写：无中文名时「服务（xxx）」） */
export function serviceLabel(name: string): string {
  if (!name) return '未知服务'
  return name.includes('（') || /[\u4e00-\u9fa5]/.test(name) ? name : `服务（${name}）`
}

// ===== 聚合：AppService + AppConsume + AppMap → 服务中心行 =====
export function buildServiceRows(
  svcRows: AppServiceRow[],
  consumeRows: AppConsumeRow[],
  appMap: Map<number, AppInfo>,
): ServiceCenterRow[] {
  // 按 ServiceName 分组提供者
  const byService = new Map<string, AppServiceRow[]>()
  for (const r of svcRows) {
    const name = (r.serviceName ?? '').trim()
    if (!name) continue
    if (!byService.has(name)) byService.set(name, [])
    byService.get(name)!.push(r)
  }

  // 每个应用（提供者）消费了哪些下游服务：appId -> Set<serviceName>
  const appConsumes = new Map<number, Set<string>>()
  for (const c of consumeRows) {
    const cid = c.appId ?? 0
    if (!cid) continue
    if (!appConsumes.has(cid)) appConsumes.set(cid, new Set<string>())
    appConsumes.get(cid)!.add((c.serviceName ?? '').trim())
  }

  const rows: ServiceCenterRow[] = []
  for (const [serviceName, provRows] of byService) {
    // 提供者应用去重
    const providerApps = new Map<number, AppServiceRow>()
    for (const p of provRows) {
      const pid = p.appId ?? 0
      if (!providerApps.has(pid)) providerApps.set(pid, p)
    }
    const providers: ServiceRef[] = [...providerApps.entries()].map(([appId, r]) => ({
      appId,
      label: appLabel(appMap.get(appId)),
      address: r.address || r.client,
      version: r.version,
    }))

    // 消费者：AppConsume 中 ServiceName == 本服务 的应用去重
    const callerApps = new Map<number, AppConsumeRow>()
    for (const c of consumeRows) {
      const cid = c.appId ?? 0
      if ((c.serviceName ?? '').trim() === serviceName && cid) {
        if (!callerApps.has(cid)) callerApps.set(cid, c)
      }
    }
    const callers: ServiceRef[] = [...callerApps.entries()].map(([appId, c]) => ({
      appId,
      label: appLabel(appMap.get(appId)),
      address: c.client,
    }))

    // 被调用方：提供者应用们各自消费的下游服务去重汇总
    const calleeSet = new Map<string, ServiceRef>()
    for (const appId of providerApps.keys()) {
      const down = appConsumes.get(appId)
      if (!down) continue
      for (const ds of down) {
        if (!ds || ds === serviceName) continue
        if (!calleeSet.has(ds)) calleeSet.set(ds, { appId: 0, label: serviceLabel(ds) })
      }
    }

    const version = provRows.map((p) => p.version).find((v) => v) ?? ''
    const addresses = [...new Set(provRows.map((p) => p.address || p.client).filter((a): a is string => Boolean(a)))]

    rows.push({
      serviceName,
      providerCount: providerApps.size,
      instanceCount: provRows.length,
      consumerCount: callerApps.size,
      status: deriveStatus(provRows),
      version,
      addresses,
      providers,
      callers,
      callees: [...calleeSet.values()],
    })
  }
  return rows.sort((a, b) => a.serviceName.localeCompare(b.serviceName))
}

// ===== 过滤 =====
export function filterServices(
  list: ServiceCenterRow[],
  keyword: string,
  status: 'all' | HealthStatus,
): ServiceCenterRow[] {
  const kw = keyword.trim().toLowerCase()
  return list.filter((r) => {
    if (status !== 'all' && r.status !== status) return false
    if (!kw) return true
    if (r.serviceName.toLowerCase().includes(kw)) return true
    if (r.providers.some((p) => p.label.toLowerCase().includes(kw))) return true
    return false
  })
}

/** KPI 聚合：总数 / 健康 / 异常 / 离线 */
export function summarizeServices(list: ServiceCenterRow[]) {
  let running = 0
  let error = 0
  let offline = 0
  for (const r of list) {
    if (r.status === 'running') running++
    else if (r.status === 'error') error++
    else offline++
  }
  return { total: list.length, running, error, offline }
}

// ===== fetch 层（全部 try-catch，失败返回空，不 throw）=====

/** 抓取应用列表（AppId → 中文名映射） */
export async function fetchAppMap(request: Req): Promise<Map<number, AppInfo>> {
  try {
    const res = await request({ url: '/Registry/App', method: 'GET', params: { PageSize: 500 } })
    const list = (res?.data ?? []) as Record<string, unknown>[]
    const map = new Map<number, AppInfo>()
    for (const a of list) {
      const id = num(pick(a, 'id', 'Id'))
      if (!id) continue
      map.set(id, {
        id,
        name: str(pick(a, 'name', 'Name')),
        displayName: str(pick(a, 'displayName', 'DisplayName', 'name', 'Name')),
      })
    }
    return map
  } catch {
    return new Map()
  }
}

/** 抓取应用服务（提供者） */
export async function fetchAppServices(request: Req): Promise<AppServiceRow[]> {
  try {
    const res = await request({ url: '/Registry/AppService', method: 'GET', params: { PageSize: 1000 } })
    const list = (res?.data ?? []) as Record<string, unknown>[]
    return list.map(normService)
  } catch {
    return []
  }
}

/** 抓取应用消费（消费者/下游） */
export async function fetchAppConsumes(request: Req): Promise<AppConsumeRow[]> {
  try {
    const res = await request({ url: '/Registry/AppConsume', method: 'GET', params: { PageSize: 1000 } })
    const list = (res?.data ?? []) as Record<string, unknown>[]
    return list.map(normConsume)
  } catch {
    return []
  }
}

/** 一次性聚合服务中心列表（应用映射 + 服务 + 消费） */
export async function fetchServiceCenter(request: Req): Promise<ServiceCenterRow[]> {
  try {
    const [appMap, svcRows, consumeRows] = await Promise.all([
      fetchAppMap(request),
      fetchAppServices(request),
      fetchAppConsumes(request),
    ])
    return buildServiceRows(svcRows, consumeRows, appMap)
  } catch {
    return []
  }
}

// ===== 服务拓扑（设计稿 page-services.jsx · SimpleTopology）=====
/** 拓扑着色档：ok 正常 / warn 降级 / danger 异常 */
export type TopoStatus = 'ok' | 'warn' | 'danger'
/** 节点类别：edge 入口（只消费不提供）/ svc 业务服务 / data 数据节点 */
export type TopoKind = 'edge' | 'svc' | 'data'

export interface TopoNode {
  id: string
  label: string
  x: number
  y: number
  w: number
  h: number
  kind: TopoKind
  status: TopoStatus
}

export interface TopoLink {
  /** 消费方应用 id */
  a: string
  /** 提供方应用 id */
  b: string
  /** 连线权重（两应用间共享的服务数；后端暂无按边 QPS） */
  weight: number
}

export interface Topology {
  nodes: TopoNode[]
  links: TopoLink[]
  width: number
  height: number
}

/** 节点尺寸（设计稿：edge 130 / svc 150 / data 140，高 40） */
const TOPO_W: Record<TopoKind, number> = { edge: 130, svc: 150, data: 140 }
const TOPO_H = 40
const TOPO_LEVEL_GAP = 220
const TOPO_ROW_GAP = 90
const TOPO_PAD_X = 60
const TOPO_PAD_Y = 40
const EDGE_SEP = "\u0000"

/** 服务健康态 → 拓扑着色档 */
function topoStatusOf(s: HealthStatus): TopoStatus {
  if (s === 'offline') return 'danger'
  if (s === 'error') return 'warn'
  return 'ok'
}

/**
 * 由服务聚合行推导调用拓扑。
 *  - 节点 = 出现在「提供者」或「调用方」中的应用（按中文展示名去重）
 *  - 类别 = 不提供任何服务者为 edge（入口），否则 svc
 *  - 状态 = 该应用所提供服务的健康态取最坏值（danger > warn > ok）
 *  - 连线 = 调用方应用 → 提供者应用；权重 = 两者共享的服务数
 *  - 布局 = 按最长路径分层（左入口 → 右下游），层内按名称稳定排序
 */
export function buildTopology(rows: ServiceCenterRow[]): Topology {
  const provideStatus = new Map<string, Set<HealthStatus>>()
  const edgeWeight = new Map<string, number>()

  for (const r of rows) {
    const provLabels = r.providers.map((p) => p.label)
    for (const p of provLabels) {
      if (!provideStatus.has(p)) provideStatus.set(p, new Set())
      provideStatus.get(p)!.add(r.status)
    }
    for (const c of r.callers) {
      for (const p of provLabels) {
        if (p === c.label) continue
        const k = c.label + EDGE_SEP + p
        edgeWeight.set(k, (edgeWeight.get(k) ?? 0) + 1)
      }
    }
  }

  const ids = new Set<string>()
  for (const k of provideStatus.keys()) ids.add(k)
  for (const k of edgeWeight.keys()) {
    const parts = k.split(EDGE_SEP)
    ids.add(parts[0])
    ids.add(parts[1])
  }
  if (ids.size === 0) return { nodes: [], links: [], width: 900, height: 500 }

  // 邻接表（消费 → 提供）
  const adj = new Map<string, Set<string>>()
  for (const k of edgeWeight.keys()) {
    const [a, b] = k.split(EDGE_SEP)
    if (!adj.has(a)) adj.set(a, new Set())
    adj.get(a)!.add(b)
  }

  // 最长路径分层（限次松弛，天然抗环）
  const depth = new Map<string, number>()
  for (const id of ids) depth.set(id, 0)
  for (let iter = 0; iter < ids.size; iter++) {
    let changed = false
    for (const [a, set] of adj) {
      const da = depth.get(a) ?? 0
      for (const b of set) {
        if ((depth.get(b) ?? 0) < da + 1) {
          depth.set(b, da + 1)
          changed = true
        }
      }
    }
    if (!changed) break
  }

  // 层内分组 + 稳定排序
  const levels = new Map<number, string[]>()
  for (const id of ids) {
    const d = depth.get(id) ?? 0
    if (!levels.has(d)) levels.set(d, [])
    levels.get(d)!.push(id)
  }
  for (const arr of levels.values()) arr.sort((x, y) => x.localeCompare(y))

  const nodes: TopoNode[] = []
  let maxX = 0
  let maxY = 0
  for (const [d, arr] of [...levels.entries()].sort((x, y) => x[0] - y[0])) {
    arr.forEach((id, i) => {
      const provides = provideStatus.get(id)
      const kind: TopoKind = provides ? "svc" : "edge"
      let status: TopoStatus = "ok"
      if (provides) {
        for (const s of provides) {
          const t = topoStatusOf(s)
          if (t === "danger") status = "danger"
          else if (t === "warn" && status !== "danger") status = "warn"
        }
      }
      const x = TOPO_PAD_X + d * TOPO_LEVEL_GAP
      const y = TOPO_PAD_Y + i * TOPO_ROW_GAP
      const w = TOPO_W[kind]
      nodes.push({ id, label: id, x, y, w, h: TOPO_H, kind, status })
      if (x + w > maxX) maxX = x + w
      if (y + TOPO_H > maxY) maxY = y + TOPO_H
    })
  }

  const links: TopoLink[] = [...edgeWeight.entries()].map(([k, weight]) => {
    const [a, b] = k.split(EDGE_SEP)
    return { a, b, weight }
  })

  return {
    nodes,
    links,
    width: Math.max(900, maxX + TOPO_PAD_X),
    height: Math.max(500, maxY + TOPO_PAD_Y),
  }
}

/** 拓扑徽章计数（设计稿：N healthy / N degraded / N down） */
export function topologySummary(t: Topology): { healthy: number; degraded: number; down: number } {
  let healthy = 0
  let degraded = 0
  let down = 0
  for (const n of t.nodes) {
    if (n.status === 'danger') down++
    else if (n.status === 'warn') degraded++
    else healthy++
  }
  return { healthy, degraded, down }
}
