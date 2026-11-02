/**
 * 角色管理 · 数据层（类型定义 + 权限矩阵 + API 调用）
 *
 * 依据《Stardust_交互设计重构方案.md》§17.2 用户/角色：
 *   权限分三层 —— 功能权限（各模块查看/编辑/删除）+ 项目数据权限（可访问项目）+ 操作权限（发布/重启/回滚等）
 *   角色 = 权限集合；用户关联角色，角色决定权限。
 *
 * 数据源事实：
 *  - 后端无 /Admin/Role（探测 404）→ 角色列表用预置角色（运维人员 / 开发人员），
 *    权限矩阵 UI 完整保留，并提示「角色管理待接入」。
 *  - 项目数据权限来源 /Platform/GalaxyProject（已存在），失败时回退空列表。
 *
 * 纪律：所有 fetch 函数 try-catch，失败返回空数组/默认值，绝不 throw；禁止裸英文缩写。
 */

// ===== 请求层类型（与 service.model 同构） =====
export interface ReqOptions {
  url: string
  method?: string
  params?: Record<string, unknown>
}
export type Req = (options: ReqOptions) => Promise<{ data?: unknown }>

// ===== 第一层：功能权限（按模块 × 查看/编辑/删除） =====

/** 功能权限模块（与侧栏一级导航一致） */
export interface PermissionModule {
  key: string
  /** 模块中文名（禁止裸缩写） */
  label: string
}

export const permissionModules: PermissionModule[] = [
  { key: 'workbench', label: '工作台' },
  { key: 'apps', label: '应用' },
  { key: 'nodes', label: '节点' },
  { key: 'services', label: '服务' },
  { key: 'configs', label: '配置' },
  { key: 'releases', label: '发布' },
  { key: 'observability', label: '可观测' },
  { key: 'gateway', label: '网关' },
  { key: 'platform', label: '平台管理' },
]

/** 模块操作三档 */
export type ModuleAction = 'view' | 'edit' | 'delete'
export const moduleActionLabels: Record<ModuleAction, string> = {
  view: '查看',
  edit: '编辑',
  delete: '删除',
}

/** 拼接功能权限 key：<模块>.<操作>，如 apps.view */
export function functionalKey(moduleKey: string, action: ModuleAction): string {
  return `${moduleKey}.${action}`
}

// ===== 第三层：操作权限（高危/原子操作） =====

export interface OperationPermission {
  key: string
  /** 操作中文名 */
  label: string
  /** 操作中文说明 */
  desc: string
}

export const operationPermissions: OperationPermission[] = [
  { key: 'viewProd', label: '查看生产', desc: '访问生产环境只读数据' },
  { key: 'deployProd', label: '发布生产', desc: '向生产环境执行应用发布' },
  { key: 'deploy', label: '发布应用', desc: '将应用版本发布到目标环境' },
  { key: 'restart', label: '重启实例', desc: '重启运行中的应用实例' },
  { key: 'rollback', label: '回滚版本', desc: '将应用回滚到历史版本' },
  { key: 'silenceAlert', label: '静默告警', desc: '临时屏蔽指定告警规则' },
  { key: 'viewLogs', label: '查看日志', desc: '在线查看应用运行日志' },
  { key: 'viewTrace', label: '查看调用链', desc: '查看分布式调用链 Trace' },
]

/** 操作权限 key → 中文名（用户 Drawer 只读展示用） */
export const operationLabel: Record<string, string> = Object.fromEntries(
  operationPermissions.map((p) => [p.key, p.label]),
)

// ===== 角色实体 =====

export interface RoleRow {
  id: number
  /** 角色中文名，如 运维人员 */
  name: string
  /** 角色编码，如 ops */
  code: string
  /** 角色描述 */
  description: string
  /** 关联用户数 */
  userCount: number
  /** 创建时间 */
  createdAt: string
  /** 功能权限 key 集合（apps.view 等） */
  functional: string[]
  /** 操作权限 key 集合 */
  operation: string[]
  /** 可访问项目名称列表（项目数据权限） */
  dataProjects: string[]
}

/** 预置角色（后端无 /Admin/Role 时展示）——运维人员：全功能查看+编辑，含生产发布/重启 */
const presetOpsRole: RoleRow = {
  id: 1,
  name: '运维人员',
  code: 'ops',
  description: '负责生产环境发布与实例运维，拥有全部模块查看/编辑及生产操作权限',
  userCount: 0,
  createdAt: '',
  functional: permissionModules.flatMap((m) => [functionalKey(m.key, 'view'), functionalKey(m.key, 'edit')]),
  operation: ['viewProd', 'deployProd', 'deploy', 'restart', 'rollback', 'silenceAlert', 'viewLogs', 'viewTrace'],
  dataProjects: [],
}

/** 预置角色——开发人员：全模块查看，可看日志/Trace，无生产发布/重启权限 */
const presetDevRole: RoleRow = {
  id: 2,
  name: '开发人员',
  code: 'dev',
  description: '日常开发与排障，可查看各模块、查看日志与调用链，无生产发布与实例重启权限',
  userCount: 0,
  createdAt: '',
  functional: permissionModules.map((m) => functionalKey(m.key, 'view')),
  operation: ['viewLogs', 'viewTrace', 'viewProd'],
  dataProjects: [],
}

/** 预置角色表 */
export const presetRoles: RoleRow[] = [presetOpsRole, presetDevRole]

/** 角色列表来源标记：后端是否真实提供（探测期为 false，便于页面显示「待接入」提示） */
export const ROLES_BACKEND_READY = false

/**
 * 抓取角色列表。
 * 后端 /Admin/Role 探测 404 → 永远返回预置角色；预留真实接口路径，后端就绪后可直接切换。
 */
export async function fetchRoleList(request: Req): Promise<RoleRow[]> {
  try {
    const res = await request({ url: '/Admin/Role', method: 'GET', params: { PageSize: 200 } })
    const raw = (res?.data ?? []) as Record<string, unknown>[]
    if (Array.isArray(raw) && raw.length) {
      return raw.map((r, i) => ({
        id: Number(r.Id ?? r.id ?? i + 1),
        name: String(r.Name ?? r.name ?? '未命名角色'),
        code: String(r.Code ?? r.code ?? ''),
        description: String(r.Description ?? r.description ?? ''),
        userCount: Number(r.UserCount ?? r.userCount ?? 0),
        createdAt: String(r.CreateTime ?? r.createTime ?? ''),
        functional: Array.isArray(r.Functional) ? (r.Functional as string[]) : [],
        operation: Array.isArray(r.Operation) ? (r.Operation as string[]) : [],
        dataProjects: Array.isArray(r.DataProjects) ? (r.DataProjects as string[]) : [],
      }))
    }
  } catch {
    /* 后端无此接口 → 落到预置角色 */
  }
  return presetRoles
}

// ===== 第二层：项目数据权限（来源 GalaxyProject） =====

export interface ProjectOption {
  id: number
  name: string
}

/** 抓取可访问项目列表（项目数据权限多选来源） */
export async function fetchProjectOptions(request: Req): Promise<ProjectOption[]> {
  try {
    const res = await request({ url: '/Platform/GalaxyProject', method: 'GET', params: { PageSize: 500 } })
    const raw = (res?.data ?? []) as Record<string, unknown>[]
    return raw
      .map((r) => ({ id: Number(r.Id ?? r.id ?? 0), name: String(r.Name ?? r.name ?? '') }))
      .filter((p) => p.name !== '')
  } catch {
    return []
  }
}

// ===== 汇总工具 =====

/** 角色功能权限数（按模块去重计数） */
export function functionalCount(role: RoleRow): number {
  return role.functional.length
}

/** 时间格式化（YYYY-MM-DD HH:mm） */
export function fmtTime(iso: string | undefined): string {
  if (!iso) return '—'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return '—'
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`
}
