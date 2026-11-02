/**
 * 配置中心 · 数据层（类型定义 + API 调用）
 *
 * 依据《Stardust_交互设计重构方案.md》§11 配置中心：
 *   当前生效版本 → 创建草稿 → 修改 → Diff → 影响范围 → 发布 → 实例生效状态
 *
 * 数据源：
 *  - 配置列表   /Configs/AppConfig      字段 Id/Name/Version/NextVersion/AppSecret
 *  - 配置数据   /Configs/ConfigData     configId → 键值对
 *  - 配置历史   /Configs/ConfigHistory  configId → 历史版本
 *  - 配置发布   POST /Configs/AppConfig/Publish?configId=<Id>
 *  - 配置 Diff  GET  /Configs/ConfigHistory/Diff?id=<historyId>
 *  - 配置回滚   POST /Configs/ConfigHistory/Rollback?id=<historyId>
 *
 * 样式纪律：纯逻辑层，无样式。
 */

// ===== 类型定义 =====

/** 配置列表行（AppConfig） */
export interface ConfigListItem {
  id?: number
  name?: string
  displayName?: string
  category?: string
  version?: number
  nextVersion?: number
  appSecret?: string
  enable?: boolean
  isGlobal?: boolean
  readonly?: boolean
  remark?: string
  publishTime?: string
  [k: string]: unknown
}

/** 配置状态：待发布（有草稿）/ 已生效 / 发布中 */
export type ConfigStatus = 'pending' | 'effective' | 'publishing'

/** 状态 → 中文标签 */
export const configStatusLabel: Record<ConfigStatus, string> = {
  pending: '待发布',
  effective: '已生效',
  publishing: '发布中',
}

/** 配置状态 chips（§11.2：全部/待发布/已生效/发布中） */
export const configStatusChips: { key: 'all' | ConfigStatus; label: string }[] = [
  { key: 'all', label: '全部' },
  { key: 'pending', label: '待发布' },
  { key: 'effective', label: '已生效' },
  { key: 'publishing', label: '发布中' },
]

/** 根据 Version / NextVersion 推导配置状态（NextVersion > Version → 待发布；相等 → 已生效） */
export function deriveConfigStatus(row: ConfigListItem): ConfigStatus {
  const v = Number(row.version ?? 0)
  const nv = Number(row.nextVersion ?? 0)
  if (nv > v) return 'pending'
  return 'effective'
}

/** 配置键值对行（ConfigData） */
export interface ConfigDataRow {
  key?: string
  name?: string
  field?: string
  value?: unknown
  content?: unknown
  description?: string
  [k: string]: unknown
}

/** 键值对（编辑器表格/文本模式共用） */
export interface ConfigKv {
  key: string
  value: string
  description: string
}

const SECRET_KEY = /secret/i

/** 配置数据行 → 键值对列表（剔除空 key；secret 脱敏） */
export function configKv(rows: ConfigDataRow[]): ConfigKv[] {
  return rows
    .map((r) => {
      const key = String(r.key ?? r.name ?? r.field ?? '').trim()
      const raw = r.value !== undefined ? r.value : r.content
      const value = raw !== undefined && raw !== null ? String(raw) : ''
      const description = String(r.description ?? r.remark ?? '').trim()
      return { key, value: SECRET_KEY.test(key) && value ? '****' : value, description }
    })
    .filter((kv) => kv.key !== '')
}

/** 键值对 → 文本（key=value 每行一条，Properties 格式） */
export function configText(kvs: ConfigKv[]): string {
  return kvs.map((kv) => `${kv.key}=${kv.value}`).join('\n')
}

/** 配置历史行（ConfigHistory） */
export interface ConfigHistoryRow {
  id?: number
  configId?: number
  action?: string
  success?: boolean
  time?: string
  [k: string]: unknown
}

/** 配置历史 → 展示行（按时间倒序、过滤 Insert/Update/Delete 等中间操作） */
export function historyRows(rows: ConfigHistoryRow[]): ConfigHistoryRow[] {
  return rows
    .filter((r) => r.action !== 'Insert' && r.action !== 'Update' && r.action !== 'Delete')
    .sort((a, b) => String(b.time ?? '').localeCompare(String(a.time ?? '')))
}

/** 配置 Diff 项（旧值/新值对比） */
export interface ConfigDiffItem {
  key: string
  oldValue: string
  newValue: string
  /** 0=未变 1=新增 2=修改 3=删除 */
  type: 0 | 1 | 2 | 3
}

/** Diff 视图模式 */
export type DiffViewMode = 'table' | 'text' | 'diff'

// ===== 请求层（request 依赖注入；与 appsModel 同构） =====

/** 请求函数结构类型（与 @newlifex/cube-vue request 兼容） */
export interface ReqOptions {
  url: string
  method?: string
  params?: Record<string, unknown>
}
export type Req = (options: ReqOptions) => Promise<{ data?: unknown }>

/** 实例计数（从 /Registry/AppOnline 按 appName 去重计数） */
export interface InstanceCount {
  appName: string
  count: number
}

/** 配置列表 + 实例计数聚合行 */
export interface ConfigListRow extends ConfigListItem {
  status: ConfigStatus
  instances: number
}

/** 抓取配置列表 + 在线实例计数 */
export async function fetchConfigList(request: Req): Promise<ConfigListRow[]> {
  try {
    const [cfgRes, onlineRes] = await Promise.all([
      request({ url: '/Configs/AppConfig', method: 'GET', params: { PageSize: 200 } }),
      request({ url: '/Registry/AppOnline', method: 'GET', params: { PageSize: 500 } }),
    ])
    const rawRows = (cfgRes?.data ?? []) as ConfigListItem[]
    // 按 appName 统计实例数
    const onlineRows = (onlineRes?.data ?? []) as { appName?: string; name?: string }[]
    const instMap = new Map<string, number>()
    for (const r of onlineRows) {
      const n = r.appName ?? r.name ?? ''
      if (n) instMap.set(n, (instMap.get(n) ?? 0) + 1)
    }
    return rawRows.map((r) => ({
      ...r,
      status: deriveConfigStatus(r),
      instances: instMap.get(r.name ?? '') ?? 0,
    }))
  } catch {
    return []
  }
}

/** 抓取配置数据（键值对） */
export async function fetchConfigData(request: Req, configId: number): Promise<ConfigDataRow[]> {
  try {
    const res = await request({ url: '/Configs/ConfigData', method: 'GET', params: { configId, PageSize: 500 } })
    return (res?.data ?? []) as ConfigDataRow[]
  } catch {
    return []
  }
}

/** 抓取配置历史 */
export async function fetchConfigHistory(request: Req, configId: number): Promise<ConfigHistoryRow[]> {
  try {
    const res = await request({ url: '/Configs/ConfigHistory', method: 'GET', params: { configId, PageSize: 50 } })
    return (res?.data ?? []) as ConfigHistoryRow[]
  } catch {
    return []
  }
}

/** 配置发布：POST /Configs/AppConfig/Publish?configId=<Id> */
export async function publishConfig(request: Req, configId: number): Promise<{ ok: boolean; message: string }> {
  try {
    const res = await request({ url: '/Configs/AppConfig/Publish', method: 'POST', params: { configId } })
    const data = res?.data
    if (typeof data === 'string') return { ok: true, message: data || '发布成功' }
    const obj = (data ?? {}) as { code?: number; message?: string; success?: boolean }
    const ok = obj.code === 0 || obj.code === 200 || obj.success === true
    return { ok, message: obj.message ?? (ok ? '发布成功' : '发布失败') }
  } catch (e: unknown) {
    return { ok: false, message: e instanceof Error ? e.message : '发布失败' }
  }
}

/** 配置 Diff：GET /Configs/ConfigHistory/Diff?id=<historyId> → changedKeys */
export async function fetchConfigDiff(request: Req, historyId: number): Promise<string[]> {
  try {
    const res = await request({ url: '/Configs/ConfigHistory/Diff', method: 'GET', params: { id: historyId } })
    const data = (res?.data ?? {}) as { changedKeys?: string[] }
    return data.changedKeys ?? []
  } catch {
    return []
  }
}

/** 配置回滚：POST /Configs/ConfigHistory/Rollback?id=<historyId> */
export async function rollbackConfig(request: Req, historyId: number): Promise<string> {
  try {
    const res = await request({ url: '/Configs/ConfigHistory/Rollback', method: 'POST', params: { id: historyId } })
    const data = res?.data
    if (typeof data === 'string') return data
    const obj = (data ?? {}) as { message?: unknown }
    return typeof obj.message === 'string' ? obj.message : '已回滚'
  } catch (e: unknown) {
    return e instanceof Error ? e.message : '回滚失败'
  }
}

/** 关键字过滤（名称/显示名/Key） */
export function filterConfigByKeyword(list: ConfigListRow[], keyword: string): ConfigListRow[] {
  const k = keyword.trim().toLowerCase()
  if (!k) return list
  return list.filter(
    (r) =>
      (r.name ?? '').toLowerCase().includes(k) ||
      (r.displayName ?? '').toLowerCase().includes(k) ||
      (r.category ?? '').toLowerCase().includes(k),
  )
}

/** 状态筛选 */
export function filterConfigByStatus(list: ConfigListRow[], filter: 'all' | ConfigStatus): ConfigListRow[] {
  if (filter === 'all') return list
  return list.filter((r) => r.status === filter)
}

/** 聚合 KPI：总数 / 待发布 / 已生效 */
export function aggregateConfigKpis(list: ConfigListRow[]) {
  let pending = 0
  let effective = 0
  for (const r of list) {
    if (r.status === 'pending') pending++
    else effective++
  }
  return { total: list.length, pending, effective }
}

/** 发布结果统计（§11.5：总实例/已成功/进行中/失败） */
export interface PublishResult {
  total: number
  success: number
  running: number
  failed: number
}

/** 空发布结果（后端无发布结果 API 时的占位） */
export const emptyPublishResult: PublishResult = { total: 0, success: 0, running: 0, failed: 0 }

/** 时间格式化（YYYY-MM-DD HH:mm） */
export function fmtTime(iso: string | undefined): string {
  if (!iso) return '—'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return '—'
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`
}
