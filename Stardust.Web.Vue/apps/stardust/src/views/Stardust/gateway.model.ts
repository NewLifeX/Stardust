/**
 * 网关中心 · 数据层（类型定义 + API 调用）
 *
 * 依据《Stardust_交互设计重构方案.md》§16 网关中心：
 *   信息架构（§16.1）：网关 → 集群 / 路由 / 节点
 *   集群（§16.2）：集群名 + 节点表格（节点 / 状态 / QPS / 健康）
 *   路由详情（§16.3）：路由路径 + 目标列表 + 负载策略 / 健康检查 / TLS / WebSocket
 *
 * 数据源（NewLife XCode 列表默认下发 PascalCase，本层双兼容）：
 *  - 网关集群 /Gateway/GatewayCluster  Name/LoadBalance/HealthPath/HealthInterval/Enable
 *  - 网关路由 /Gateway/GatewayRoute     Name/Path/Domain/Methods/ClusterId/WebSocket/Enable/Priority
 *  - 网关节点 /Gateway/GatewayNode     Name/Address/Weight/IsHealthy/Enable/ClusterId
 *
 * 说明：网关实体无 QPS 实时字段 → QPS 列显示「—（待接入）」，不算 bug。
 * 纪律：所有 fetch 包 try-catch，API 失败返回空数组，绝不 throw。
 */

// ===== 请求类型（与 @newlifex/cube-vue request 兼容）=====
export interface ReqOptions {
  url: string
  method?: string
  params?: Record<string, unknown>
}
export type Req = (options: ReqOptions) => Promise<{ data?: unknown }>

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

// ===== 集群 =====
/** 网关集群行（GatewayCluster） */
export interface ClusterRow {
  id: number
  name: string
  loadBalance: string
  healthPath: string
  healthInterval: number
  healthTimeout: number
  enable: boolean
  projectName: string
}

/** 负载均衡算法 → 中文 */
export function loadBalanceLabel(v: string): string {
  switch ((v || '').toLowerCase()) {
    case 'roundrobin':
      return '轮询（RoundRobin）'
    case 'leastconnection':
      return '最少连接（LeastConnection）'
    case 'iphash':
      return 'IP 哈希（IPHash）'
    case '':
    case undefined:
      return '—'
    default:
      return v
  }
}

// ===== 节点 =====
/** 网关节点行（GatewayNode） */
export interface NodeRow {
  id: number
  clusterId: number
  clusterName: string
  name: string
  address: string
  weight: number
  enable: boolean
  isHealthy: boolean
}

/** 节点运行态 → 七态 StatusPill：启用且健康=运行；启用不健康=异常；停用=离线 */
export function nodeStatus(n: NodeRow): 'running' | 'error' | 'offline' {
  if (!n.enable) return 'offline'
  return n.isHealthy ? 'running' : 'error'
}

// ===== 路由 =====
/** 网关路由行（GatewayRoute） */
export interface RouteRow {
  id: number
  name: string
  enable: boolean
  priority: number
  clusterId: number
  clusterName: string
  domain: string
  path: string
  methods: string
  stripPrefix: boolean
  webSocket: boolean
  staticRoot: string
  remark: string
}

/** 路由状态 → 七态 */
export function routeStatus(r: RouteRow): 'running' | 'offline' {
  return r.enable ? 'running' : 'offline'
}

// ===== 归一化 =====
function normCluster(r: Record<string, unknown>): ClusterRow {
  return {
    id: num(pick(r, 'id', 'Id')),
    name: str(pick(r, 'name', 'Name')),
    loadBalance: str(pick(r, 'loadBalance', 'LoadBalance')),
    healthPath: str(pick(r, 'healthPath', 'HealthPath')),
    healthInterval: num(pick(r, 'healthInterval', 'HealthInterval')),
    healthTimeout: num(pick(r, 'healthTimeout', 'HealthTimeout')),
    enable: bool(pick(r, 'enable', 'Enable')),
    projectName: str(pick(r, 'projectName', 'ProjectName')),
  }
}

function normNode(r: Record<string, unknown>): NodeRow {
  return {
    id: num(pick(r, 'id', 'Id')),
    clusterId: num(pick(r, 'clusterId', 'ClusterId')),
    clusterName: str(pick(r, 'clusterName', 'ClusterName')),
    name: str(pick(r, 'name', 'Name')),
    address: str(pick(r, 'address', 'Address')),
    weight: num(pick(r, 'weight', 'Weight')),
    enable: bool(pick(r, 'enable', 'Enable')),
    isHealthy: bool(pick(r, 'isHealthy', 'IsHealthy')),
  }
}

function normRoute(r: Record<string, unknown>): RouteRow {
  return {
    id: num(pick(r, 'id', 'Id')),
    name: str(pick(r, 'name', 'Name')),
    enable: bool(pick(r, 'enable', 'Enable')),
    priority: num(pick(r, 'priority', 'Priority')),
    clusterId: num(pick(r, 'clusterId', 'ClusterId')),
    clusterName: str(pick(r, 'clusterName', 'ClusterName')),
    domain: str(pick(r, 'domain', 'Domain')),
    path: str(pick(r, 'path', 'Path')),
    methods: str(pick(r, 'methods', 'Methods')),
    stripPrefix: bool(pick(r, 'stripPrefix', 'StripPrefix')),
    webSocket: bool(pick(r, 'webSocket', 'WebSocket')),
    staticRoot: str(pick(r, 'staticRoot', 'StaticRoot')),
    remark: str(pick(r, 'remark', 'Remark')),
  }
}

// ===== fetch 层（全部 try-catch，失败返回空，不 throw）=====

/** 抓取网关集群 */
export async function fetchClusters(request: Req): Promise<ClusterRow[]> {
  try {
    const res = await request({ url: '/Gateway/GatewayCluster', method: 'GET', params: { PageSize: 500 } })
    return ((res?.data ?? []) as Record<string, unknown>[]).map(normCluster)
  } catch {
    return []
  }
}

/** 抓取网关节点 */
export async function fetchNodes(request: Req): Promise<NodeRow[]> {
  try {
    const res = await request({ url: '/Gateway/GatewayNode', method: 'GET', params: { PageSize: 1000 } })
    return ((res?.data ?? []) as Record<string, unknown>[]).map(normNode)
  } catch {
    return []
  }
}

/** 抓取网关路由 */
export async function fetchRoutes(request: Req): Promise<RouteRow[]> {
  try {
    const res = await request({ url: '/Gateway/GatewayRoute', method: 'GET', params: { PageSize: 1000 } })
    return ((res?.data ?? []) as Record<string, unknown>[]).map(normRoute)
  } catch {
    return []
  }
}

/** 布尔 → 中文（是/否） */
export function yn(v: boolean): string {
  return v ? '是' : '否'
}
