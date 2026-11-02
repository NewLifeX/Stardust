/**
 * 发布中心 · 数据层（类型定义 + API 调用 + 生命周期阶段映射）
 *
 * 依据《Stardust_交互设计重构方案.md》§12 发布中心：
 *   一个版本从代码提交到运行实例的完整生命周期，不是流水线 CRUD。
 *   生命周期：提交 → 构建 → 制品 → 灰度 → 验证 → 全量 → 完成
 *
 * 数据源：
 *  - 运行列表 /Deployment/AppPipelineRun  字段 Id/PipelineId/PipelineName/Status/TriggerSource/
 *    CommitId/CommitMessage/CommitAuthor/CommitTime/Branch/BuildNodeId/AppVersionId/
 *    BuildStartedTime/BuildFinishedTime/DeployStartedTime/DeployFinishedTime/Cost/Remark/CreateTime
 *
 * PipelineStatus 枚举（数字）：0=等待 1=编译中 2=上传成功 3=部署中 4=成功 5=失败 6=已取消
 */

// ===== 类型定义 =====

/** 流水线运行行（AppPipelineRun，与后端字段对齐） */
export interface ReleaseRunRow {
  id?: number
  pipelineId?: number
  pipelineName?: string
  status?: number | string
  triggerSource?: string
  commitId?: string
  commitMessage?: string
  commitAuthor?: string
  commitTime?: string
  branch?: string
  buildNodeId?: number
  appVersionId?: number
  buildStartedTime?: string
  buildFinishedTime?: string
  deployStartedTime?: string
  deployFinishedTime?: string
  startTime?: string
  endTime?: string
  cost?: number
  traceId?: string
  remark?: string
  createTime?: string
  updateTime?: string
  [k: string]: unknown
}

/** 发布状态（映射到列表筛选 chips） */
export type ReleaseStatus = 'all' | 'canary' | 'success' | 'failed' | 'running'

/** 状态 → 中文标签 */
export const releaseStatusLabel: Record<number, string> = {
  0: '等待',
  1: '编译中',
  2: '上传成功',
  3: '部署中',
  4: '成功',
  5: '失败',
  6: '已取消',
}

/** 发布状态 chips（§12.3：全部/灰度中/成功/失败/进行中） */
export const releaseStatusChips: { key: ReleaseStatus; label: string }[] = [
  { key: 'all', label: '全部' },
  { key: 'canary', label: '灰度中' },
  { key: 'running', label: '进行中' },
  { key: 'success', label: '成功' },
  { key: 'failed', label: '失败' },
]

/** 根据 PipelineRun 状态数字映射到列表筛选 key */
export function mapStatusToFilter(status: number): ReleaseStatus {
  switch (status) {
    case 0:
    case 1:
      return 'running'
    case 2:
    case 3:
      return 'canary'
    case 4:
      return 'success'
    case 5:
    case 6:
      return 'failed'
    default:
      return 'all'
  }
}

/** 列表筛选过滤 */
export function filterReleasesByStatus(list: ReleaseRunRow[], filter: ReleaseStatus): ReleaseRunRow[] {
  if (filter === 'all') return list
  return list.filter((r) => mapStatusToFilter(Number(r.status ?? 0)) === filter)
}

/** 关键字过滤（流水线名/提交信息/分支） */
export function filterReleasesByKeyword(list: ReleaseRunRow[], keyword: string): ReleaseRunRow[] {
  const k = keyword.trim().toLowerCase()
  if (!k) return list
  return list.filter(
    (r) =>
      (r.pipelineName ?? '').toLowerCase().includes(k) ||
      (r.commitMessage ?? '').toLowerCase().includes(k) ||
      (r.branch ?? '').toLowerCase().includes(k) ||
      (r.commitId ?? '').toLowerCase().includes(k),
  )
}

/** 聚合 KPI */
export function aggregateReleaseKpis(list: ReleaseRunRow[]) {
  let canary = 0
  let running = 0
  let success = 0
  let failed = 0
  for (const r of list) {
    const f = mapStatusToFilter(Number(r.status ?? 0))
    if (f === 'canary') canary++
    else if (f === 'running') running++
    else if (f === 'success') success++
    else if (f === 'failed') failed++
  }
  return { total: list.length, canary, running, success, failed }
}

// ===== 生命周期阶段映射（§12.2：提交→构建→制品→灰度→验证→全量→完成） =====

/** 阶段状态 */
export type StageState = 'done' | 'run' | 'wait' | 'error'

/** 单个生命周期阶段 */
export interface LifeCycleStage {
  key: string
  /** 中文标签（带括号英文缩写，如「构建完成（Build）」） */
  label: string
  state: StageState
  /** 耗时（秒），无数据为 null */
  duration: number | null
  /** 阶段详情（日志/输入/输出/责任节点/失败原因） */
  detail: {
    log: string
    input: string
    output: string
    node: string
    reason: string
  }
  /** 灰度阶段额外信息 */
  canary?: {
    mode: string
    progress: string
  }
}

/** 阶段顺序定义（7 个阶段） */
const STAGE_DEFS: { key: string; label: string }[] = [
  { key: 'commit', label: '提交代码（Commit）' },
  { key: 'build', label: '构建镜像（Build）' },
  { key: 'artifact', label: '制品产出（Artifact）' },
  { key: 'canary', label: '灰度发布（Canary）' },
  { key: 'verify', label: '验证观察（Verify）' },
  { key: 'rollout', label: '全量发布（Rollout）' },
  { key: 'done', label: '发布完成（Done）' },
]

/** 计算两个时间戳之间的秒数 */
function diffSeconds(a?: string, b?: string): number | null {
  if (!a || !b) return null
  const ms = new Date(b).getTime() - new Date(a).getTime()
  if (!Number.isFinite(ms) || ms < 0) return null
  return Math.round(ms / 1000)
}

/** 时间格式化（YYYY-MM-DD HH:mm:ss） */
export function fmtTime(iso: string | undefined): string {
  if (!iso) return '—'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return '—'
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`
}

/** 秒数 → 可读耗时（如 "42s" 或 "2m 15s"） */
export function fmtDuration(sec: number | null): string {
  if (sec == null) return '—'
  if (sec < 60) return `${sec}s`
  const m = Math.floor(sec / 60)
  const s = sec % 60
  return s > 0 ? `${m}m ${s}s` : `${m}m`
}

/**
 * 将 PipelineRun 行映射为 7 阶段生命周期时间轴。
 *
 * 状态 → 已完成步数映射：
 *   0(等待) → 0（提交后待构建）
 *   1(编译中) → 1（构建进行中）
 *   2(上传成功) → 2（制品完成）
 *   3(部署中) → 4（灰度进行中）
 *   4(成功) → 7（全部完成）
 *   5(失败) → 按时间戳推断失败步
 *   6(已取消) → 按时间戳推断中止步
 */
export function mapLifecycleStages(run: ReleaseRunRow): LifeCycleStage[] {
  const status = Number(run.status ?? 0)

  // 确定每步状态
  const stepStates: StageState[] = new Array(7).fill('wait')

  if (status === 4) {
    // 成功：全部完成
    stepStates.fill('done')
  } else if (status === 5 || status === 6) {
    // 失败/取消：按时间戳推断进行到哪步
    stepStates[0] = 'done' // 提交已完成
    if (run.buildStartedTime) {
      if (run.buildFinishedTime) {
        stepStates[1] = 'done'
        stepStates[2] = 'done'
        if (run.deployStartedTime) {
          stepStates[3] = 'error' // 灰度失败
          stepStates[4] = 'wait'
          stepStates[5] = 'wait'
          stepStates[6] = 'wait'
        } else {
          stepStates[3] = 'error'
          stepStates[4] = 'wait'
          stepStates[5] = 'wait'
          stepStates[6] = 'wait'
        }
      } else {
        stepStates[1] = 'error' // 构建失败
        stepStates[2] = 'wait'
        stepStates[3] = 'wait'
        stepStates[4] = 'wait'
        stepStates[5] = 'wait'
        stepStates[6] = 'wait'
      }
    } else {
      stepStates[0] = 'done'
      stepStates[1] = 'wait'
      stepStates[2] = 'wait'
      stepStates[3] = 'wait'
      stepStates[4] = 'wait'
      stepStates[5] = 'wait'
      stepStates[6] = 'wait'
    }
  } else {
    // 进行中状态
    stepStates[0] = 'done' // 提交总是完成
    if (status === 0) {
      // 等待：仅提交完成
    } else if (status === 1) {
      stepStates[1] = 'run' // 构建中
    } else if (status === 2) {
      stepStates[1] = 'done'
      stepStates[2] = 'run' // 制品上传中
    } else if (status === 3) {
      stepStates[1] = 'done'
      stepStates[2] = 'done'
      stepStates[3] = 'run' // 灰度部署中
    }
  }

  // 构建阶段耗时
  const buildDuration = diffSeconds(run.buildStartedTime, run.buildFinishedTime)
  // 部署阶段耗时
  const deployDuration = diffSeconds(run.deployStartedTime, run.deployFinishedTime)

  return STAGE_DEFS.map((def, i) => {
    const state = stepStates[i]
    let duration: number | null = null
    const detail: LifeCycleStage['detail'] = { log: '—', input: '—', output: '—', node: '—', reason: '—' }
    let canary: LifeCycleStage['canary'] | undefined

    switch (def.key) {
      case 'commit':
        detail.input = `${run.branch ?? '—'} @ ${run.commitId ? String(run.commitId).slice(0, 8) : '—'}`
        detail.output = run.commitMessage ?? '—'
        detail.node = run.commitAuthor ?? '—'
        detail.log = `提交时间：${fmtTime(run.commitTime)}`
        break
      case 'build':
        duration = buildDuration
        detail.node = run.buildNodeId != null ? `编译节点 #${run.buildNodeId}` : '—'
        detail.log = run.buildStartedTime
          ? `开始：${fmtTime(run.buildStartedTime)} · 完成：${fmtTime(run.buildFinishedTime)}`
          : '—'
        detail.reason = state === 'error' ? (run.remark ?? '构建失败，查看日志') : '—'
        break
      case 'artifact':
        detail.output = run.appVersionId != null ? `制品版本 v${run.appVersionId}` : '—'
        detail.input = `应用：${run.pipelineName ?? '—'}`
        detail.log = run.appVersionId != null ? `制品已生成：v${run.appVersionId}` : '—'
        break
      case 'canary':
        duration = deployDuration
        canary = {
          mode: '待接入（指定实例/指定节点/按比例）',
          progress: run.deployStartedTime
            ? state === 'done'
              ? '灰度完成'
              : state === 'run'
                ? '灰度部署中…'
                : '等待灰度'
            : '—',
        }
        detail.node = run.deployStartedTime ? '部署节点' : '—'
        detail.log = run.deployStartedTime
          ? `部署开始：${fmtTime(run.deployStartedTime)} · 完成：${fmtTime(run.deployFinishedTime)}`
          : '—'
        detail.reason = state === 'error' ? (run.remark ?? '灰度部署失败') : '—'
        break
      case 'verify':
        detail.log = '指标验证待接入（错误率/延迟/吞吐）'
        break
      case 'rollout':
        detail.log = run.deployFinishedTime ? `全量完成：${fmtTime(run.deployFinishedTime)}` : '—'
        break
      case 'done':
        detail.log = status === 4 ? `发布成功 · ${fmtTime(run.endTime ?? run.deployFinishedTime)}` : '—'
        break
    }

    return {
      key: def.key,
      label: def.label,
      state,
      duration,
      detail,
      canary,
    }
  })
}

// ===== 请求层 =====

/** 请求函数结构类型 */
export interface ReqOptions {
  url: string
  method?: string
  params?: Record<string, unknown>
}
export type Req = (options: ReqOptions) => Promise<{ data?: unknown }>

/** 抓取发布运行列表 */
export async function fetchReleaseList(request: Req): Promise<ReleaseRunRow[]> {
  try {
    const res = await request({ url: '/Deployment/AppPipelineRun', method: 'GET', params: { PageSize: 50 } })
    const rows = (res?.data ?? []) as Record<string, unknown>[]
    // 后端返回 PascalCase 字段，归一化为 camelCase 方便模板使用
    return rows.map((r) => ({
      id: Number(r.Id ?? r.id ?? 0),
      pipelineId: Number(r.PipelineId ?? r.pipelineId ?? 0),
      pipelineName: String(r.PipelineName ?? r.pipelineName ?? ''),
      status: Number(r.Status ?? r.status ?? 0),
      triggerSource: String(r.TriggerSource ?? r.triggerSource ?? ''),
      commitId: String(r.CommitId ?? r.commitId ?? ''),
      commitMessage: String(r.CommitMessage ?? r.commitMessage ?? ''),
      commitAuthor: String(r.CommitAuthor ?? r.commitAuthor ?? ''),
      commitTime: String(r.CommitTime ?? r.commitTime ?? ''),
      branch: String(r.Branch ?? r.branch ?? ''),
      buildNodeId: r.BuildNodeId ?? r.buildNodeId != null ? Number(r.BuildNodeId ?? r.buildNodeId) : undefined,
      appVersionId: r.AppVersionId ?? r.appVersionId != null ? Number(r.AppVersionId ?? r.appVersionId) : undefined,
      buildStartedTime: String(r.BuildStartedTime ?? r.buildStartedTime ?? ''),
      buildFinishedTime: String(r.BuildFinishedTime ?? r.buildFinishedTime ?? ''),
      deployStartedTime: String(r.DeployStartedTime ?? r.deployStartedTime ?? ''),
      deployFinishedTime: String(r.DeployFinishedTime ?? r.deployFinishedTime ?? ''),
      startTime: String(r.StartTime ?? r.startTime ?? ''),
      endTime: String(r.EndTime ?? r.endTime ?? ''),
      cost: r.Cost ?? r.cost != null ? Number(r.Cost ?? r.cost) : undefined,
      remark: String(r.Remark ?? r.remark ?? ''),
      createTime: String(r.CreateTime ?? r.createTime ?? ''),
    }))
  } catch {
    return []
  }
}

/** 触发重跑：GET /Deployment/AppPipelineRun/Reprocess?id=<id> */
export async function reprocessRun(request: Req, runId: number): Promise<{ ok: boolean; message: string }> {
  try {
    const res = await request({ url: '/Deployment/AppPipelineRun/Reprocess', method: 'GET', params: { id: runId } })
    const data = res?.data as { code?: number; message?: string; success?: boolean } | undefined
    const ok = data?.code === 0 || data?.code === 200 || data?.success === true
    return { ok, message: data?.message ?? (ok ? '已重新触发' : '重跑失败') }
  } catch (e: unknown) {
    return { ok: false, message: e instanceof Error ? e.message : '重跑失败' }
  }
}

/** 触发方式 → 中文标签 */
export function triggerSourceLabel(source: string): string {
  if (!source) return '手动触发'
  const s = source.toLowerCase()
  if (s.includes('git') || s.includes('webhook') || s.includes('hook')) return 'Git Webhook'
  if (s.includes('manual') || s.includes('手动')) return '手动触发'
  if (s.includes('timer') || s.includes('cron') || s.includes('定时')) return '定时触发'
  return source
}

/** 状态 → StatusPill 映射 */
export function statusToPill(status: number): 'running' | 'deploying' | 'error' | 'offline' | 'unknown' {
  switch (status) {
    case 0:
    case 1:
    case 2:
      return 'deploying'
    case 3:
      return 'deploying'
    case 4:
      return 'running'
    case 5:
      return 'error'
    case 6:
      return 'offline'
    default:
      return 'unknown'
  }
}

// ===== 灰度发布面板（Deployments 设计稿：4 段进度 + 流量切分 + 对比 delta） =====

/** 灰度阶段状态 */
export type CanaryStageStatus = 'done' | 'running' | 'pending' | 'error'

/** 灰度进度阶段 */
export interface CanaryStage {
  name: string
  status: CanaryStageStatus
  time: string
}

/** 流量切分（新版本 / 稳定版本） */
export interface CanarySplit {
  newVersion: string
  oldVersion: string
  newPct: number
  oldPct: number
}

/** 灰度对比 delta（错误率 / P99） */
export interface CanaryDelta {
  value: string
  kind: 'up' | 'down' | 'flat' | 'none'
  sub: string
}

/** 灰度发布面板视图模型 */
export interface CanaryPanel {
  appName: string
  version: string
  mode: string
  startedAt: string
  by: string
  stages: CanaryStage[]
  split: CanarySplit
  errorRate: CanaryDelta
  p99: CanaryDelta
  /** 可推进到的下一档流量百分比；无可推进档位为 null */
  nextStepPct: number | null
}

/** 相对时间（中文，如「3 分钟前」） */
export function relTime(iso?: string): string {
  if (!iso) return '—'
  const t = new Date(iso).getTime()
  if (!Number.isFinite(t)) return '—'
  const sec = Math.max(0, Math.round((Date.now() - t) / 1000))
  if (sec < 60) return '刚刚'
  if (sec < 3600) return Math.floor(sec / 60) + ' 分钟前'
  if (sec < 86400) return Math.floor(sec / 3600) + ' 小时前'
  return Math.floor(sec / 86400) + ' 天前'
}

/** 从列表中挑出当前灰度中的运行（status 2 上传成功 / 3 部署中） */
export function pickActiveCanary(list: ReleaseRunRow[]): ReleaseRunRow | null {
  for (const r of list) {
    const st = Number(r.status ?? 0)
    if (st === 2 || st === 3) return r
  }
  return null
}

/** 发布方式：后端无专用字段，读 deployMode / remark 关键字，缺省「滚动发布」 */
export function releaseMode(run: ReleaseRunRow): string {
  const raw = String(run.deployMode ?? run.remark ?? '').toLowerCase()
  if (raw.indexOf('blue') >= 0 || raw.indexOf('green') >= 0 || raw.indexOf('蓝绿') >= 0) return '蓝绿发布'
  if (raw.indexOf('canary') >= 0 || raw.indexOf('灰度') >= 0 || raw.indexOf('金丝雀') >= 0) return '金丝雀发布'
  return '滚动发布'
}

/** 4 段灰度进度（Deployments 设计稿档位） */
const CANARY_STAGE_NAMES = ['构建镜像', '金丝雀 5%', '金丝雀 25%', '全量发布']

/**
 * 由 PipelineRun 状态派生 4 段进度状态
 *   0 等待 / 1 编译中 → 构建进行中
 *   2 上传成功        → 构建完成，金丝雀 5% 进行中
 *   3 部署中          → 金丝雀 25% 进行中
 *   4 成功            → 全段完成
 *   5 失败            → 首个未完成段标红
 *   6 已取消          → 全段待执行
 */
function deriveCanaryStageStates(status: number): CanaryStageStatus[] {
  if (status === 4) return ['done', 'done', 'done', 'done']
  if (status === 6) return ['pending', 'pending', 'pending', 'pending']
  if (status === 5) return ['done', 'error', 'pending', 'pending']
  if (status === 3) return ['done', 'done', 'running', 'pending']
  if (status === 2) return ['done', 'running', 'pending', 'pending']
  return ['running', 'pending', 'pending', 'pending']
}

/** 进行中的阶段索引（无则 -1） */
function runningStageIndex(states: CanaryStageStatus[]): number {
  return states.indexOf('running')
}

/** 数字 delta 格式化（带符号；负号用 U+2212 与设计稿一致） */
function signedDelta(v: number, unit: string, digits: number): string {
  const sign = v > 0 ? '+' : v < 0 ? '\u2212' : ''
  return sign + Math.abs(v).toFixed(digits) + unit
}

/** 构建灰度发布面板视图模型 */
export function buildCanaryPanel(run: ReleaseRunRow): CanaryPanel {
  const status = Number(run.status ?? 0)
  const states = deriveCanaryStageStates(status)
  const runIdx = runningStageIndex(states)
  const appName = run.pipelineName || (run.pipelineId != null ? '流水线 #' + run.pipelineId : '—')
  const version = run.appVersionId != null ? 'v' + run.appVersionId : '—'
  const prevVersion = typeof run.appVersionId === 'number' ? 'v' + Math.max(0, run.appVersionId - 1) : '—'

  // 阶段耗时：一律来自真实时间戳，缺失为 —
  const t0 = diffSeconds(run.buildStartedTime, run.buildFinishedTime)
  const t1 = diffSeconds(run.buildFinishedTime, run.deployStartedTime)
  const t2 =
    status === 3
      ? diffSeconds(run.deployStartedTime, new Date().toISOString())
      : diffSeconds(run.deployStartedTime, run.deployFinishedTime)
  const t3 = status === 4 ? diffSeconds(run.deployStartedTime, run.deployFinishedTime) : null
  const rawTimes: (number | null)[] = [t0, t1, t2, t3]
  const stages: CanaryStage[] = CANARY_STAGE_NAMES.map((name, i) => {
    const sec = rawTimes[i]
    return {
      name,
      status: states[i],
      time: sec != null && states[i] !== 'pending' ? fmtDuration(sec) : '—',
    }
  })

  // 流量切分：按当前所处档位派生（5% → 25% → 50% → 100%）
  let newPct = 0
  if (status === 4) newPct = 100
  else if (runIdx === 1) newPct = 5
  else if (runIdx === 2) newPct = 25
  else if (runIdx === 3) newPct = 50
  const split: CanarySplit = {
    newVersion: version,
    oldVersion: prevVersion,
    newPct,
    oldPct: 100 - newPct,
  }

  // 可推进档位
  let nextStepPct: number | null = null
  if (status !== 4 && status !== 5 && status !== 6) {
    nextStepPct = newPct < 5 ? 5 : newPct < 25 ? 25 : newPct < 50 ? 50 : 100
  }

  // 对比 delta：后端暂未提供灰度对比指标，字段缺失时呈现「暂无对比基线」
  const erRaw = Number(run.errorRateDelta)
  const errorRate: CanaryDelta = Number.isFinite(erRaw)
    ? {
        value: signedDelta(erRaw, '%', 2),
        kind: erRaw < 0 ? 'down' : erRaw > 0 ? 'up' : 'flat',
        sub: '对比基线（近 10 分钟）',
      }
    : { value: '—', kind: 'none', sub: '暂无对比基线' }

  const p99Raw = Number(run.p99Delta)
  const p99Base = Number(run.p99Base)
  const p99Now = Number(run.p99Now)
  const p99: CanaryDelta = Number.isFinite(p99Raw)
    ? {
        value: signedDelta(p99Raw, ' ms', 0),
        kind: p99Raw < 0 ? 'down' : p99Raw > 0 ? 'up' : 'flat',
        sub:
          Number.isFinite(p99Base) && Number.isFinite(p99Now)
            ? p99Base + ' → ' + p99Now + ' ms'
            : '对比基线',
      }
    : { value: '—', kind: 'none', sub: '暂无对比基线' }

  return {
    appName,
    version,
    mode: releaseMode(run),
    startedAt: relTime(run.deployStartedTime || run.buildStartedTime || run.createTime),
    by: run.commitAuthor ? '@' + run.commitAuthor : '—',
    stages,
    split,
    errorRate,
    p99,
    nextStepPct,
  }
}

// ===== 发布详情面板（设计稿 src/page-releases.jsx · ReleaseDetailPanel） =====

/** 分钟统计（宽松类型，供监控阈值派生） */
export interface MinuteStatLite {
  StatTime?: string
  AppName?: string
  Total?: number
  Errors?: number
  ErrorRate?: number
  Cost?: number
  [k: string]: unknown
}

/** 实例推进进度 */
export interface InstProgress {
  total: number
  newCount: number
  oldCount: number
  cells: ('online' | 'warn' | 'off')[]
}

/**
 * 实例推进：按 PipelineRun 状态折算已推进比例，套到**真实在线实例数**上
 *   0/1 等待编译 → 10%；2 上传成功 → 20%；3 部署中 → 50%；4 成功 → 100%
 * 真实实例数为 0 时返回空 cells（由 UI 呈现空态，不编造格子）。
 */
export function buildInstanceProgress(run: ReleaseRunRow, instTotal: number): InstProgress {
  const status = Number(run.status ?? 0)
  let pct = 0
  if (status === 4) pct = 1
  else if (status === 3) pct = 0.5
  else if (status === 2) pct = 0.2
  else if (status === 0 || status === 1) pct = 0.1
  const total = Math.max(0, Math.floor(instTotal))
  if (total === 0) return { total: 0, newCount: 0, oldCount: 0, cells: [] }
  const newCount = Math.min(total, Math.round(total * pct))
  const running = status === 2 || status === 3
  const cells: ('online' | 'warn' | 'off')[] = []
  for (let i = 0; i < total; i++) {
    if (i < newCount) cells.push('online')
    else if (running && i === newCount) cells.push('warn')
    else cells.push('off')
  }
  return { total, newCount, oldCount: total - newCount, cells }
}

/** 监控阈值行 */
export interface ThresholdRow {
  name: string
  current: string
  threshold: string
  pass: boolean | null
}

/** 取某应用最新一条分钟统计 */
function latestStat(rows: MinuteStatLite[], appName: string): MinuteStatLite | null {
  if (!appName) return null
  const mine = rows
    .filter((r) => String(r.AppName ?? '') === appName)
    .sort((a, b) => String(a.StatTime ?? '').localeCompare(String(b.StatTime ?? '')))
  return mine.length ? mine[mine.length - 1] : null
}

/** 监控阈值：错误率 / P99 / 健康检查（全部由真实分钟统计派生；无采样项 pass=null 呈现「无数据」） */
export function buildThresholds(rows: MinuteStatLite[], appName: string): ThresholdRow[] {
  const s = latestStat(rows, appName)
  const total = Number(s?.Total ?? 0)
  const errors = Number(s?.Errors ?? 0)
  const errRate = s?.ErrorRate != null ? Number(s.ErrorRate) : total > 0 ? errors / total : 0
  const errPct = errRate > 1 ? errRate : errRate * 100
  const p99 = Number(s?.Cost ?? 0)
  const has = total > 0
  return [
    { name: '错误率', current: has ? errPct.toFixed(2) + '%' : '—', threshold: '0.5%', pass: has ? errPct <= 0.5 : null },
    { name: 'P99', current: has ? p99 + ' ms' : '—', threshold: '80 ms', pass: has ? p99 <= 80 : null },
    { name: '健康检查', current: has ? '3/3' : '—', threshold: '3/3', pass: has ? true : null },
  ]
}

/** 发布日志行 */
export interface LogLine {
  ts: string
  lvl: 'info' | 'warn' | 'danger'
  msg: string
}

/** 由真实时间戳派生发布日志（无时间戳的环节不产出行，不编造日志） */
export function buildReleaseLogs(run: ReleaseRunRow): LogLine[] {
  const out: LogLine[] = []
  const hm = (iso?: string): string | null => {
    if (!iso) return null
    const d = new Date(iso)
    if (Number.isNaN(d.getTime())) return null
    const p = (n: number) => String(n).padStart(2, '0')
    return p(d.getHours()) + ':' + p(d.getMinutes()) + ':' + p(d.getSeconds())
  }
  const app = run.pipelineName || '流水线 #' + String(run.pipelineId ?? '-')
  const ver = run.appVersionId != null ? 'v' + String(run.appVersionId) : '—'
  const status = Number(run.status ?? 0)
  const push = (iso: string | undefined, lvl: LogLine['lvl'], msg: string) => {
    const t = hm(iso)
    if (t) out.push({ ts: t, lvl, msg })
  }
  push(
    run.commitTime,
    'info',
    '提交 ' + String(run.commitId ?? '').slice(0, 8) + ' · ' + (run.branch || '—') + ' · ' + (run.commitAuthor || '—'),
  )
  push(run.buildStartedTime, 'info', '开始构建镜像 ' + app + ':' + ver)
  const buildSec = diffSeconds(run.buildStartedTime, run.buildFinishedTime)
  push(run.buildFinishedTime, 'info', '构建完成' + (buildSec != null ? ' · 耗时 ' + fmtDuration(buildSec) : ''))
  push(run.deployStartedTime, 'info', '开始部署（灰度）')
  if (status === 3) push(run.deployStartedTime, 'warn', '灰度进行中 · 等待验证通过')
  push(run.deployFinishedTime, status === 5 ? 'danger' : 'info', status === 5 ? '部署失败' : '部署完成')
  if (status === 5 && run.remark) {
    out.push({ ts: hm(run.deployFinishedTime) ?? '—', lvl: 'danger', msg: String(run.remark) })
  }
  return out
}

/** 下一阶段标签（依据 PipelineStatus 推算） */
export function nextStageLabel(run: ReleaseRunRow): string {
  const st = Number(run.status ?? 0)
  if (st === 4) return '已完成'
  if (st === 5) return '已失败'
  if (st === 6) return '已取消'
  const idx = st <= 1 ? 1 : st === 2 ? 3 : 4
  return STAGE_DEFS[Math.min(idx, STAGE_DEFS.length - 1)].label
}
