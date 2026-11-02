/**
 * 节点详情 · 数据层（纯逻辑，设计稿 src/page-nodes.jsx · NodeDetail）
 *
 * 真实数据来源：
 *  - /Nodes/Node              节点记录（Id/Name/IP/Enable/ProjectId/Category/Product/Version/LastActive/cpu/memory/disk）
 *  - /Deployment/AppDeployNode 节点关联的部署应用（AppId/AppName/NodeId/Enable）
 *  - /Nodes/NodeCommand       节点可执行指令（Name/Command/Role/Status/...）
 *
 * 纪律：所有数值均由真实字段推导；字段缺失一律呈现「—」，不编造数字。
 */

/** 节点记录（宽松类型，兼容后端命名差异） */
export interface NodeRow {
  Id?: number
  Name?: string
  Code?: string
  IP?: string
  iP?: string
  Enable?: boolean
  ProjectId?: number
  Category?: string
  Product?: string
  Version?: string
  LastActive?: string
  lastActive?: string
  CreateTime?: string
  UpdateTime?: string
  cpu?: number
  memory?: number
  mem?: number
  disk?: number
  apps?: number
  runtime?: string
  ver?: string
  zone?: string
  [k: string]: unknown
}

/** 资源仪表项 */
export interface ResourceGauge {
  key: string
  label: string
  value: number | null
  tone: 'auto' | 'ok' | 'warn' | 'bad'
  /** 副标题（规格 / 明细） */
  sub: string
  /** 明细行 */
  detail: string
  spark: number[]
}

/** 健康检查行 */
export interface HealthCheck {
  name: string
  ok: boolean | null
  value: string
  threshold: string
  at: string
}

/** 运行时规格行 */
export interface SpecRow {
  k: string
  v: string
  mono: boolean
}

/** 时间线项 */
export interface TimelineItem {
  tone: 'brand' | 'success' | 'warning' | 'danger' | 'info'
  title: string
  time: string
}

/** 相对时间（中文） */
export function relTime(iso: string | undefined, now: number): string {
  if (!iso) return '—'
  const t = new Date(iso).getTime()
  if (Number.isNaN(t)) return '—'
  const sec = Math.max(0, Math.round((now - t) / 1000))
  if (sec < 60) return '刚刚'
  if (sec < 3600) return Math.floor(sec / 60) + ' 分钟前'
  if (sec < 86400) return Math.floor(sec / 3600) + ' 小时前'
  return Math.floor(sec / 86400) + ' 天前'
}

/** 绝对时间（YYYY-MM-DD HH:mm） */
export function absTime(iso: string | undefined): string {
  if (!iso) return '—'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return '—'
  const p = (n: number) => String(n).padStart(2, '0')
  return d.getFullYear() + '-' + p(d.getMonth() + 1) + '-' + p(d.getDate()) + ' ' + p(d.getHours()) + ':' + p(d.getMinutes())
}

/** 心跳是否新鲜（2 个心跳周期内） */
export function isNodeAlive(node: NodeRow, now: number): boolean {
  const raw = node.LastActive ?? node.lastActive
  if (!raw) return node.Enable === true
  const t = new Date(raw).getTime()
  return !Number.isNaN(t) && now - t <= 120000
}

/** 节点状态标签 */
export function nodeStatus(node: NodeRow, now: number): { key: 'online' | 'warn' | 'offline'; text: string } {
  if (node.Enable === false) return { key: 'offline', text: '已禁用' }
  return isNodeAlive(node, now) ? { key: 'online', text: '在线' } : { key: 'warn', text: '心跳异常' }
}

/** 数值归一：接受 0-100 或 0-1 两种口径 */
function pctOf(v: unknown): number | null {
  const n = Number(v)
  if (!Number.isFinite(n)) return null
  if (n > 0 && n <= 1) return Number((n * 100).toFixed(1))
  return Number(n.toFixed(1))
}

/** 三资源仪表：CPU / 内存 / 磁盘（真实字段缺失则为 null） */
export function buildResourceGauges(node: NodeRow): ResourceGauge[] {
  const cpu = pctOf(node.cpu ?? node.Cpu)
  const mem = pctOf(node.memory ?? node.mem ?? node.Memory)
  const disk = pctOf(node.disk ?? node.Disk)
  const mk = (key: string, label: string, value: number | null, sub: string, detail: string, spark: number[]): ResourceGauge => ({
    key,
    label,
    value,
    tone: 'auto',
    sub,
    detail,
    spark,
  })
  return [
    mk('cpu', 'CPU', cpu, String(node.CpuCores ?? node.cpuCores ?? '—') + ' vCPU', valueDetail(cpu, '使用率'), []),
    mk('mem', '内存', mem, String(node.MemTotal ?? node.memTotal ?? '—'), valueDetail(mem, '已用'), []),
    mk('disk', '磁盘', disk, String(node.DiskTotal ?? node.diskTotal ?? '—'), valueDetail(disk, '已用'), []),
  ]
}

function valueDetail(v: number | null, label: string): string {
  return v == null ? label + ' 无采样' : label + ' ' + v + '%'
}

/** 健康检查（6 项，设计稿同序）：无数据项 ok=null，呈现「—」而非伪通过 */
export function buildHealthChecks(node: NodeRow, now: number): HealthCheck[] {
  const cpu = pctOf(node.cpu ?? node.Cpu)
  const mem = pctOf(node.memory ?? node.mem ?? node.Memory)
  const disk = pctOf(node.disk ?? node.Disk)
  const alive = isNodeAlive(node, now)
  const ver = String(node.Version ?? node.ver ?? '')
  const at = relTime(node.LastActive ?? node.lastActive, now)
  const row = (name: string, v: number | null, threshold: string): HealthCheck => ({
    name,
    ok: v == null ? null : v < 80,
    value: v == null ? '—' : v + '%',
    threshold,
    at,
  })
  return [
    row('CPU 使用率', cpu, '< 80%'),
    row('内存使用率', mem, '< 80%'),
    row('磁盘使用率', disk, '< 80%'),
    {
      name: '心跳新鲜度',
      ok: (node.LastActive ?? node.lastActive) ? alive : null,
      value: at,
      threshold: '≤ 120 秒',
      at,
    },
    {
      name: 'Agent 版本',
      ok: ver ? true : null,
      value: ver || '—',
      threshold: '—',
      at,
    },
    {
      name: '节点启用状态',
      ok: node.Enable == null ? null : node.Enable !== false,
      value: node.Enable == null ? '—' : node.Enable ? '启用' : '禁用',
      threshold: '启用',
      at,
    },
  ]
}

/** 运行时规格（真实字段缺失呈现「—」） */
export function buildRuntimeSpecs(node: NodeRow): SpecRow[] {
  const g = (k: string, ...keys: string[]): string => {
    for (const key of keys) {
      const v = node[key]
      if (v != null && v !== '') return String(v)
    }
    return '—'
  }
  return [
    { k: '节点名称', v: g('node', 'Name', 'name'), mono: true },
    { k: 'IP 地址', v: g('ip', 'IP', 'iP'), mono: true },
    { k: '分类', v: g('cat', 'Category'), mono: false },
    { k: '产品', v: g('prod', 'Product'), mono: false },
    { k: '版本', v: g('ver', 'Version', 'ver'), mono: true },
    { k: '运行时', v: g('rt', 'runtime', 'Runtime'), mono: true },
    { k: '区域', v: g('zone', 'zone', 'Zone'), mono: false },
    { k: '创建时间', v: absTime(g('ct', 'CreateTime')), mono: true },
    { k: '更新时间', v: absTime(g('ut', 'UpdateTime')), mono: true },
  ]
}

/** 最近操作时间线：由真实时间戳/版本派生 */
export function buildNodeTimeline(node: NodeRow, now: number): TimelineItem[] {
  const out: TimelineItem[] = []
  const last = node.LastActive ?? node.lastActive
  if (last) out.push({ tone: isNodeAlive(node, now) ? 'success' : 'warning', title: isNodeAlive(node, now) ? 'Agent 心跳正常' : 'Agent 心跳超时', time: relTime(last, now) })
  if (node.Enable === false) out.push({ tone: 'danger', title: '节点已被禁用', time: relTime(node.UpdateTime, now) })
  if (node.Version || node.ver) out.push({ tone: 'info', title: 'Agent 版本 ' + String(node.Version ?? node.ver), time: relTime(node.UpdateTime, now) })
  if (node.UpdateTime) out.push({ tone: 'brand', title: '节点配置更新', time: relTime(node.UpdateTime, now) })
  if (node.CreateTime) out.push({ tone: 'info', title: '加入集群', time: absTime(node.CreateTime) })
  return out
}

/** 资源占比预警文案（设计稿 CPU 触达阈值提示） */
export function resourceWarning(node: NodeRow): string {
  const cpu = pctOf(node.cpu ?? node.Cpu)
  const mem = pctOf(node.memory ?? node.mem ?? node.Memory)
  const disk = pctOf(node.disk ?? node.Disk)
  const parts: string[] = []
  if (cpu != null && cpu >= 80) parts.push('CPU ' + cpu + '%（阈值 80%）')
  if (mem != null && mem >= 80) parts.push('内存 ' + mem + '%（阈值 80%）')
  if (disk != null && disk >= 80) parts.push('磁盘 ' + disk + '%（阈值 80%）')
  return parts.join(' · ')
}

/** 部署应用行（/Deployment/AppDeployNode） */
export interface DeployAppRow {
  id: number
  appId: number
  appName: string
  enable: boolean
}

/** 指令行（/Nodes/NodeCommand） */
export interface CommandRow {
  id: number
  name: string
  role: string
  status: number
  result: string
}
