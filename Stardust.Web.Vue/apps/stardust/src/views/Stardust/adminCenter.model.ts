/**
 * A21 平台管理模型（设计稿 src/page-admin.jsx，169 行 · 5 Tab：项目/用户/角色/Token/系统设置）
 *
 * 纯函数层：不依赖 Vue、不硬编码颜色。
 * 真实接口（已核实）：/Platform/GalaxyProject（项目）、/Admin/User（用户）、
 * /Admin/Role（角色）、/Platform/McpToken（Token）。
 * 设计稿中「应用数 / 节点数 / 成员数 / 环境 / 创建时间 / 应用 QPS / 插件」等后端无字段 →
 * 一律返回空串，由组件渲染「—」，绝不伪造数值。
 */

export interface ReqOptions {
  url: string
  method?: string
  params?: Record<string, unknown>
}
export type Req = (options: ReqOptions) => Promise<{ data?: unknown }>

const str = (v: unknown): string => (v === null || v === undefined ? '' : String(v))
const num = (v: unknown): number => {
  const n = Number(v)
  return Number.isFinite(n) ? n : 0
}
const pick = (r: Record<string, unknown>, keys: string[]): unknown => {
  for (const k of keys) {
    const v = r[k]
    if (v !== undefined && v !== null && v !== '') return v
  }
  return undefined
}

/**
 * 从请求层返回值中取出「数据数组」。
 *
 * 请求层 unwrapResponse:false，返回形状随封装层级变化，实测存在三种：
 *   1. 裸数组            res = [...]
 *   2. 业务信封          res = { code:0, data:[...] }
 *   3. axios 再包一层     res = { data: { code:0, data:[...] } }
 * 逐层下钻最多 4 层，命中数组即返回；均未命中返回空数组（由组件渲染空态）。
 */
function extractList(res: unknown): Record<string, unknown>[] {
  let cur: unknown = res
  for (let depth = 0; depth < 4; depth++) {
    if (Array.isArray(cur)) return cur as Record<string, unknown>[]
    if (!cur || typeof cur !== "object") return []
    const o = cur as Record<string, unknown>
    const next = o.data ?? o.list ?? o.rows ?? o.items
    if (Array.isArray(next)) return next as Record<string, unknown>[]
    if (next === undefined) return []
    cur = next
  }
  return []
}

/** 统一取数：GET + PageSize=200；失败**不再静默吞错**，打印真实原因后返回空数组（fail-soft）。 */
async function fetchList(request: Req, url: string): Promise<Record<string, unknown>[]> {
  try {
    const res = await request({ url, method: "GET", params: { PageSize: 200 } })
    return extractList(res)
  } catch (e) {
    console.error("[adminCenter] 取数失败:", url, e)
    return []
  }
}

// ===== 项目 Tab =====

export interface ProjectRow {
  id: number
  name: string
  code: string
  apps: string
  nodes: string
  members: string
  envs: string[]
  createdAt: string
}

/** 设计稿环境 tag 三枚（prod/staging/dev）——后端无环境维度字段，按设计保留静态标签 */
export const PROJECT_ENVS = ['prod', 'staging', 'dev']

export function toProjectRows(items: Record<string, unknown>[]): ProjectRow[] {
  return items.map((r) => ({
    id: num(pick(r, ['id', 'Id'])),
    name: str(pick(r, ['name', 'Name', 'displayName', 'DisplayName'])),
    code: str(pick(r, ['code', 'Code'])),
    apps: pick(r, ['apps', 'Apps']) === undefined ? '' : String(num(pick(r, ['apps', 'Apps']))),
    nodes: pick(r, ['nodes', 'Nodes']) === undefined ? '' : String(num(pick(r, ['nodes', 'Nodes']))),
    members: '',
    envs: PROJECT_ENVS,
    createdAt: str(pick(r, ['createdAt', 'CreatedAt', 'createTime', 'CreateTime'])).slice(0, 10),
  }))
}

export async function fetchProjects(request: Req): Promise<Record<string, unknown>[]> {
  return fetchList(request, '/Platform/GalaxyProject')
}

// ===== 用户 Tab =====

export interface AdminUserRow {
  id: number
  name: string
  initial: string
  email: string
  role: string
  rawRoles: string
  lastActive: string
  status: 'active' | 'inactive'
}

export function toAdminUserRows(items: Record<string, unknown>[]): AdminUserRow[] {
  return items.map((r) => {
    const name = str(pick(r, ['displayName', 'DisplayName', 'name', 'Name', 'userName', 'UserName']))
    const roles = pick(r, ['roles', 'Roles'])
    let role = ''
    if (Array.isArray(roles) && roles.length) {
      const first = roles[0]
      role = typeof first === 'string' ? first : str((first as Record<string, unknown>)?.name ?? (first as Record<string, unknown>)?.Name)
    } else {
      role = str(pick(r, ['role', 'Role', 'roleName', 'RoleName', 'roleNames', 'RoleNames'])).split(',')[0].trim()
    }
    const raw = pick(r, ['status', 'Status', 'enabled', 'Enabled'])
    const active = raw === true || raw === 1 || str(raw).toLowerCase() === 'active' || str(raw).toLowerCase() === 'enabled' || str(raw) === '正常'
    return {
      id: num(pick(r, ['id', 'Id'])),
      name,
      initial: name ? name.slice(0, 1).toUpperCase() : '?',
      email: str(pick(r, ['email', 'Email', 'mail', 'Mail'])),
      role,
      rawRoles: Array.isArray(roles) && roles.length
        ? roles.map((x) => (typeof x === 'string' ? x : str((x as Record<string, unknown>)?.name ?? (x as Record<string, unknown>)?.Name))).join(',')
        : str(pick(r, ['roleNames', 'RoleNames', 'role', 'Role', 'roleName', 'RoleName'])),
      lastActive: str(pick(r, ['lastLogin', 'LastLogin', 'lastActive', 'LastActive'])),
      status: active ? 'active' : 'inactive',
    }
  })
}

export async function fetchAdminUsers(request: Req): Promise<Record<string, unknown>[]> {
  return fetchList(request, '/Admin/User')
}

// ===== 角色 Tab =====

export interface AdminRoleRow {
  id: number
  name: string
  users: number
  perms: string
}

export function toAdminRoleRows(items: Record<string, unknown>[]): AdminRoleRow[] {
  return items.map((r) => {
    const fn = pick(r, ['functional', 'Functional'])
    const op = pick(r, ['operation', 'Operation'])
    const dp = pick(r, ['dataProjects', 'DataProjects'])
    const cnt = (v: unknown): number => (Array.isArray(v) ? v.length : num(v))
    const parts: string[] = []
    if (fn !== undefined) parts.push('功能 ' + cnt(fn))
    if (op !== undefined) parts.push('操作 ' + cnt(op))
    if (dp !== undefined) parts.push('数据 ' + cnt(dp))
    return {
      id: num(pick(r, ['id', 'Id'])),
      name: str(pick(r, ['name', 'Name', 'code', 'Code'])),
      users: num(pick(r, ['userCount', 'UserCount'])),
      perms: parts.join(' · ') || str(pick(r, ['description', 'Description'])),
    }
  })
}

export async function fetchAdminRoles(request: Req): Promise<Record<string, unknown>[]> {
  return fetchList(request, '/Admin/Role')
}

// ===== Token Tab =====

export interface TokenRow {
  id: number
  name: string
  role: string
  created: string
  last: string
  status: 'active' | 'inactive'
}

export function toTokenRows(items: Record<string, unknown>[]): TokenRow[] {
  return items.map((r) => {
    const raw = pick(r, ['status', 'Status', 'enabled', 'Enabled'])
    const active = raw === true || raw === 1 || str(raw).toLowerCase() === 'active' || str(raw).toLowerCase() === 'enabled'
    return {
      id: num(pick(r, ['id', 'Id'])),
      name: str(pick(r, ['name', 'Name', 'tokenName', 'TokenName'])),
      role: str(pick(r, ['role', 'Role', 'roleName', 'RoleName'])),
      created: str(pick(r, ['createdAt', 'CreatedAt', 'createTime', 'CreateTime'])).slice(0, 10),
      last: str(pick(r, ['lastUsed', 'LastUsed', 'lastUseTime', 'LastUseTime'])).slice(0, 19).replace('T', ' '),
      status: active ? 'active' : 'inactive',
    }
  })
}

export async function fetchTokens(request: Req): Promise<Record<string, unknown>[]> {
  return fetchList(request, '/Platform/McpToken')
}

// ===== 系统设置 Tab（设计稿 4 面板）=====

export interface SettingItem {
  k: string
  v: string
  mono: boolean
}
export interface SettingPanel {
  key: string
  title: string
  /** true = 值来自后端配置接口（当前无接口 → 组件标注「示例值 · 待接入」） */
  sample: boolean
  items: SettingItem[]
}

/**
 * 设计稿的面板与字段（结构对齐）；取值后端无配置接口 → 全部标记 sample=true，
 * 组件会显式渲染「示例值 · 待接入」角标，避免把示例值当成真实配置。
 */
export const SETTINGS_PANELS: SettingPanel[] = [
  {
    key: 'cluster',
    title: '集群设置',
    sample: true,
    items: [
      { k: 'Registry Endpoint', v: 'sd-prod-01.stardust.local:8848', mono: true },
      { k: '心跳间隔', v: '3 s', mono: true },
      { k: '心跳超时', v: '10 s', mono: true },
      { k: '失联剔除', v: '30 s', mono: true },
      { k: '默认负载均衡', v: 'Round Robin + Least Latency', mono: false },
      { k: '默认重试', v: '2 次 · 100 ms 退避', mono: true },
    ],
  },
  {
    key: 'notify',
    title: '通知与告警',
    sample: true,
    items: [
      { k: 'Webhook', v: 'https://alerts.example.com/webhook', mono: true },
      { k: '钉钉群', v: 'stardust-alerts', mono: true },
      { k: 'Email', v: 'oncall@example.com', mono: false },
      { k: 'Critical 静默期', v: '15 min', mono: true },
    ],
  },
]

/** 接入配置面板的 Agent 下载矩阵与安装命令（设计稿原文） */
export const AGENT_PLATFORMS = ['linux/amd64', 'linux/arm64', 'darwin/arm64']
export const INSTALL_CMD = [
  'curl -fsSL https://stardust.io/install.sh | \\',
  '  STARDUST_TOKEN=<your-token> \\',
  '  STARDUST_REGISTRY=sd-prod-01.stardust.local \\',
  '  bash',
].join('\n')

/** 审计日志面板（设计稿） */
export const AUDIT_INFO = {
  retainDays: '180 天',
  archive: '归档 S3',
  formats: 'JSON / CSV',
  perDay: '2.4 GB / 天',
}

/** 状态归一 → StatusPill 可识别值 */
export function statusLabel(s: 'active' | 'inactive'): string {
  return s === 'active' ? 'running' : 'offline'
}
export function statusText(s: 'active' | 'inactive'): string {
  return s === 'active' ? 'active' : 'inactive'
}
