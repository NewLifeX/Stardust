/**
 * 用户管理 · 数据层（类型定义 + API 调用）
 *
 * 依据《Stardust_交互设计重构方案.md》§17.2 用户/角色：
 *   用户关联角色，角色决定权限；列表展示 用户名/姓名/角色/所属项目/状态/最后登录/操作。
 *
 * 数据源事实：
 *  - 后端无 /Admin/User（探测 404）→ 用户列表为空，显示「用户管理待接入」空状态，但完整 UI（搜索/KPI/表格/Drawer）保留。
 *  - 角色下拉来源 role.model.fetchRoleList（预置 运维/开发）；项目数据权限来源 fetchProjectOptions。
 *
 * 纪律：所有 fetch 函数 try-catch，失败返回空数组/默认值，绝不 throw。
 */
import { fetchRoleList, operationLabel, type Req, type RoleRow } from './role.model'

// ===== 用户状态 =====
export type UserStatus = 'enabled' | 'disabled'

export const userStatusLabel: Record<UserStatus, string> = {
  enabled: '启用',
  disabled: '禁用',
}

/** 状态筛选 chips（全部/启用/禁用） */
export const userStatusChips: { key: 'all' | UserStatus; label: string }[] = [
  { key: 'all', label: '全部' },
  { key: 'enabled', label: '启用' },
  { key: 'disabled', label: '禁用' },
]

// ===== 用户行 =====
export interface UserRow {
  id: number
  /** 用户名（登录账号） */
  name: string
  /** 姓名（中文显示名） */
  displayName: string
  /** 邮箱 */
  email: string
  /** 手机号 */
  phone: string
  /** 状态 */
  status: UserStatus
  /** 关联角色名列表 */
  roles: string[]
  /** 所属项目名列表（项目数据权限） */
  projects: string[]
  /** 创建时间 */
  createdAt: string
  /** 最后登录时间 */
  lastLogin: string
}

/** 空用户（新建用户 Drawer 初始态） */
export const emptyUser: UserRow = {
  id: 0,
  name: '',
  displayName: '',
  email: '',
  phone: '',
  status: 'enabled',
  roles: [],
  projects: [],
  createdAt: '',
  lastLogin: '',
}

// ===== 抓取 =====

/** 后端是否就绪（探测期 false，页面据此显示待接入提示） */
export const USERS_BACKEND_READY = false

/** 抓取用户列表。后端 /Admin/User 404 → 返回空数组，由页面渲染「待接入」空状态。 */
export async function fetchUserList(request: Req): Promise<UserRow[]> {
  try {
    const res = await request({ url: '/Admin/User', method: 'GET', params: { PageSize: 500 } })
    const raw = (res?.data ?? []) as Record<string, unknown>[]
    return raw.map((r, i) => {
      const enabled = r.Enable ?? r.enable ?? true
      return {
        id: Number(r.Id ?? r.id ?? i + 1),
        name: String(r.Name ?? r.name ?? ''),
        displayName: String(r.DisplayName ?? r.displayName ?? r.Name ?? r.name ?? ''),
        email: String(r.Mail ?? r.mail ?? r.Email ?? r.email ?? ''),
        phone: String(r.Mobile ?? r.mobile ?? r.Phone ?? r.phone ?? ''),
        status: enabled ? 'enabled' : 'disabled',
        roles: Array.isArray(r.Roles) ? (r.Roles as string[]) : [],
        projects: Array.isArray(r.Projects) ? (r.Projects as string[]) : [],
        createdAt: String(r.CreateTime ?? r.createTime ?? ''),
        lastLogin: String(r.LoginTime ?? r.loginTime ?? r.LastLogin ?? ''),
      }
    })
  } catch {
    return []
  }
}

/** 抓取角色下拉（复用 role.model，失败时落到预置角色） */
export async function fetchUserRoleOptions(request: Req): Promise<RoleRow[]> {
  return fetchRoleList(request)
}

// ===== 过滤与汇总 =====

/** 关键字过滤（用户名/姓名/邮箱） */
export function filterUsers(list: UserRow[], keyword: string, status: 'all' | UserStatus): UserRow[] {
  const k = keyword.trim().toLowerCase()
  let rows = list
  if (status !== 'all') rows = rows.filter((u) => u.status === status)
  if (!k) return rows
  return rows.filter(
    (u) =>
      u.name.toLowerCase().includes(k) ||
      u.displayName.toLowerCase().includes(k) ||
      u.email.toLowerCase().includes(k),
  )
}

/** KPI：总数 / 启用 / 禁用 */
export function summarizeUsers(list: UserRow[]) {
  let enabled = 0
  let disabled = 0
  for (const u of list) {
    if (u.status === 'enabled') enabled++
    else disabled++
  }
  return { total: list.length, enabled, disabled }
}

/** 用户角色继承的操作权限中文名（只读展示；角色无操作权限时为空） */
export function inheritedOperations(roles: RoleRow[]): string[] {
  const set = new Set<string>()
  for (const role of roles) {
    for (const key of role.operation) {
      set.add(operationLabel[key] ?? key)
    }
  }
  return [...set]
}

/** 时间格式化（YYYY-MM-DD HH:mm） */
export function fmtTime(iso: string | undefined): string {
  if (!iso) return '—'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return '—'
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`
}
