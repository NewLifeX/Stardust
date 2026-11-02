<script setup lang="ts">
/**
 * 星尘控制台 · 发布中心（§12）
 *
 * 设计定位：一个版本从代码提交到运行实例的完整生命周期，不是流水线 CRUD。
 * 生命周期：提交 → 构建 → 制品 → 灰度 → 验证 → 全量 → 完成
 * 页面结构：列表（搜索+chips+表格）+ 详情 Drawer（垂直步骤条时间轴，每阶段可展开）
 * 上下文联动：watch stardustContext.revision 切换项目/环境后自动刷新
 * 样式纪律：--el-* 令牌 + Tailwind 布局，禁止自定义 token/硬编码色值
 */
import { ref, computed, onMounted, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { request } from '@newlifex/cube-vue/core/utils/request'
import { stardustContext } from '@/stores/stardustContext'
import StatusPill from '@/layouts/StardustLayout/components/StatusPill.vue'
import KpiStat from '@/layouts/StardustLayout/components/KpiStat.vue'
import * as m from './release.model'

// ===== 列表状态 =====
const list = ref<m.ReleaseRunRow[]>([])
const loading = ref(false)
const keyword = ref('')
const filter = ref<m.ReleaseStatus>('all')

const filtered = computed(() =>
  m.filterReleasesByStatus(m.filterReleasesByKeyword(list.value, keyword.value), filter.value),
)
const stats = computed(() => m.aggregateReleaseKpis(list.value))

// ===== 详情 Drawer 状态 =====
const drawerVisible = ref(false)
const current = ref<m.ReleaseRunRow | null>(null)
const stages = ref<m.LifeCycleStage[]>([])
const expandedStage = ref<string | null>(null)

/** 加载发布列表 */
async function load() {
  loading.value = true
  try {
    list.value = await m.fetchReleaseList(request)
  } catch (e: unknown) {
    ElMessage.error(e instanceof Error ? e.message : '加载发布列表失败')
    list.value = []
  } finally {
    loading.value = false
  }
}

/** 打开详情 Drawer */
function openDetail(row: m.ReleaseRunRow) {
  current.value = row
  drawerVisible.value = true
  expandedStage.value = null
  stages.value = m.mapLifecycleStages(row)
  void loadDetailData(row)
}

/** 展开/收起阶段详情 */
function toggleStage(key: string) {
  expandedStage.value = expandedStage.value === key ? null : key
}

/** 重新发布（重跑） */
async function onReprocess(row: m.ReleaseRunRow) {
  if (row.id == null) return
  try {
    await ElMessageBox.confirm('确认基于本次运行重新触发流水线？', '重跑流水线', {
      type: 'warning',
      confirmButtonText: '确认',
      cancelButtonText: '取消',
    })
  } catch {
    return
  }
  const r = await m.reprocessRun(request, row.id)
  if (r.ok) {
    ElMessage.success(r.message)
    await load()
  } else {
    ElMessage.error(r.message)
  }
}

/** 回滚（后端暂无专用回滚 API，提示待接入） */
function onRollback(row: m.ReleaseRunRow) {
  ElMessage.info(`回滚到上一版本待接入 · 当前版本 v${row.appVersionId ?? '—'}`)
}

/** 新建发布 */
function onCreate() {
  ElMessage.info('新建发布待接入后端接口')
}

// ===== 发布详情面板（设计稿 ReleaseDetailPanel） =====
const instTotal = ref(0)
const statRows = ref<m.MinuteStatLite[]>([])
const appsAll = ref<Record<string, unknown>[]>([])

/** 流水线名 → 注册应用名（命中则用注册名，未命中沿用流水线名） */
function appNameOf(run: m.ReleaseRunRow): string {
  const n = String(run.pipelineName ?? '')
  const hit = appsAll.value.find((a) => String(a.name ?? '') === n || String(a.displayName ?? '') === n)
  return hit ? String(hit.name ?? n) : n
}

const instProgress = computed(() =>
  current.value ? m.buildInstanceProgress(current.value, instTotal.value) : { total: 0, newCount: 0, oldCount: 0, cells: [] as ('online' | 'warn' | 'off')[] },
)
const thresholds = computed(() => (current.value ? m.buildThresholds(statRows.value, appNameOf(current.value)) : []))
const releaseLogs = computed(() => (current.value ? m.buildReleaseLogs(current.value) : []))
const nextStage = computed(() => (current.value ? m.nextStageLabel(current.value) : '—'))
const canAdvance = computed(() => {
  const s = Number(current.value?.status ?? 0)
  return s !== 4 && s !== 5 && s !== 6
})

/** 详情辅助数据：注册应用 / 在线实例 / 分钟统计（fail-soft，任一失败仅让对应区块留空） */
async function loadDetailData(run: m.ReleaseRunRow): Promise<void> {
  const safe = (url: string, params: Record<string, unknown>) => request({ url, method: 'GET', params }).catch(() => null)
  const [ar, orr, mr] = await Promise.all([
    safe('/Registry/App', { PageSize: 200 }),
    safe('/Registry/AppOnline', { PageSize: 500 }),
    safe('/Monitors/AppMinuteStat', { PageSize: 500 }),
  ])
  appsAll.value = (ar?.data ?? []) as Record<string, unknown>[]
  const r0 = run as unknown as Record<string, unknown>
  const cands = [appNameOf(run), String(r0.pipelineName ?? ''), String(r0.PipelineName ?? ''), String(r0.appName ?? ''), String(r0.AppName ?? '')]
    .filter((v) => String(v).trim() !== '')
    .map((v) => String(v).trim().toLowerCase())
  instTotal.value = ((orr?.data ?? []) as Record<string, unknown>[]).filter((r) => cands.indexOf(String(r.appName ?? r.AppName ?? '').trim().toLowerCase()) >= 0).length
  statRows.value = (mr?.data ?? []) as m.MinuteStatLite[]
}

function onAdvance() {
  ElMessage.warning('推进阶段待接入后端发布控制接口')
}
function onPause() {
  ElMessage.warning('暂停待接入后端发布控制接口')
}
function onRollbackStep() {
  ElMessage.warning('回滚一步待接入后端发布控制接口')
}

// ===== 灰度发布面板（Deployments 设计稿） =====
const activeCanary = computed(() => m.pickActiveCanary(list.value))
const canary = computed(() => (activeCanary.value ? m.buildCanaryPanel(activeCanary.value) : null))

/** 灰度阶段圆点样式 */
function canaryDotClass(state: m.CanaryStageStatus): string {
  switch (state) {
    case 'done':
      return 'is-done'
    case 'running':
      return 'is-run'
    case 'error':
      return 'is-error'
    default:
      return 'is-pending'
  }
}

/** 灰度阶段连接线样式（仅上一段完成时点亮） */
function canaryLineClass(state: m.CanaryStageStatus): string {
  return state === 'done' ? 'is-done' : ''
}

/** delta 值配色 */
function deltaClass(kind: m.CanaryDelta['kind']): string {
  if (kind === 'down') return 'is-good'
  if (kind === 'up') return 'is-bad'
  return 'is-none'
}

/** 灰度控制动作：后端暂未提供灰度控制端点 */
function onCanaryAction(action: string) {
  ElMessage.warning('后端暂未提供灰度控制接口（' + action + '）')
}

/** 状态 → StatusPill */
function statusPill(status: number) {
  return m.statusToPill(status)
}

/** 状态中文标签 */
function statusLabel(status: number): string {
  return m.releaseStatusLabel[status] ?? String(status)
}

/** 阶段图标符号 */
function stageIcon(state: m.StageState): string {
  if (state === 'done') return '✓'
  if (state === 'run') return '●'
  if (state === 'error') return '●'
  return '○'
}

/** 阶段圆点颜色 */
function stageDotClass(state: m.StageState): string {
  switch (state) {
    case 'done':
      return 'bg-[var(--el-color-success)]'
    case 'run':
      return 'bg-[var(--el-color-primary)]'
    case 'error':
      return 'bg-[var(--el-color-danger)]'
    default:
      return 'bg-[var(--el-border-color)]'
  }
}

/** 阶段标签颜色 */
function stageTextClass(state: m.StageState): string {
  switch (state) {
    case 'done':
      return 'text-[var(--el-color-success)]'
    case 'run':
      return 'text-[var(--el-color-primary)]'
    case 'error':
      return 'text-[var(--el-color-danger)]'
    default:
      return 'text-[var(--el-text-color-placeholder)]'
  }
}

/** 上下文联动：项目/环境切换后重新加载 */
watch(
  () => stardustContext.revision,
  () => {
    drawerVisible.value = false
    current.value = null
    keyword.value = ''
    filter.value = 'all'
    void load()
  },
)

onMounted(() => {
  void load()
})
</script>

<template>
  <div class="flex h-full flex-col gap-5">
    <!-- 页头 -->
    <div class="flex items-center justify-between">
      <div class="flex flex-col gap-1">
        <h1 class="text-xl font-semibold text-[var(--el-text-color-primary)]">
          发布中心
        </h1>
        <p class="text-xs text-[var(--el-text-color-regular)]">
          版本生命周期 — 从提交到全量发布的完整追踪
        </p>
      </div>
      <el-button
        type="primary"
        size="small"
        @click="onCreate"
      >
        新建发布
      </el-button>
    </div>

    <!-- KPI -->
    <div class="grid grid-cols-4 gap-4">
      <KpiStat
        label="发布总数"
        :value="stats.total"
        accent="primary"
      />
      <KpiStat
        label="灰度中"
        :value="stats.canary"
        accent="warning"
      />
      <KpiStat
        label="成功"
        :value="stats.success"
        accent="success"
      />
      <KpiStat
        label="失败"
        :value="stats.failed"
        accent="danger"
      />
    </div>

    <!-- 灰度发布面板（Deployments：4 段进度 + 流量切分 + 对比 delta） -->
    <div
      v-if="canary"
      data-spec="canary-panel"
      class="sd-canary-card"
    >
      <div class="sd-canary-head">
        <div class="min-w-0">
          <div class="sd-canary-title">
            {{ canary.appName }} <span class="sd-canary-ver">{{ canary.version }}</span>
          </div>
          <div class="sd-canary-sub">
            {{ canary.mode }} · {{ canary.startedAt }}启动 · {{ canary.by }}
          </div>
        </div>
        <div class="sd-canary-actions">
          <el-button
            size="small"
            plain
            @click="onCanaryAction('暂停')"
          >
            暂停
          </el-button>
          <el-button
            size="small"
            type="danger"
            plain
            @click="onCanaryAction('回滚')"
          >
            回滚
          </el-button>
          <el-button
            v-if="canary.nextStepPct != null"
            size="small"
            type="primary"
            @click="onCanaryAction('推进到 ' + canary.nextStepPct + '%')"
          >
            <svg
              class="sd-canary-ico"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="1.5"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <path d="M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 0 0-2.91-.09z" />
              <path d="m12 15-3-3a22 22 0 0 1 2-3.95A12.88 12.88 0 0 1 22 2c0 2.72-.78 7.5-6 11a22.35 22.35 0 0 1-4 2z" />
              <path d="M9 12H4s.55-3.03 2-4c1.62-1.08 5 0 5 0" />
              <path d="M12 15v5s3.03-.55 4-2c1.08-1.62 0-5 0-5" />
            </svg>
            推进到 {{ canary.nextStepPct }}%
          </el-button>
        </div>
      </div>

      <!-- 4 段水平进度 -->
      <div
        class="sd-canary-stages"
        data-spec="canary-stages"
      >
        <div
          v-for="(s, i) in canary.stages"
          :key="s.name"
          class="sd-canary-stage"
          :data-spec="'canary-stage-' + i"
        >
          <div class="sd-canary-rail">
            <div
              class="sd-canary-dot"
              :class="canaryDotClass(s.status)"
            >
              <span v-if="s.status === 'done'">✓</span>
              <span
                v-else-if="s.status === 'running'"
                class="sd-canary-pulse"
              />
              <span v-else-if="s.status === 'error'">●</span>
              <span v-else>{{ i + 1 }}</span>
            </div>
            <div
              v-if="i < canary.stages.length - 1"
              class="sd-canary-line"
              :class="canaryLineClass(s.status)"
            />
          </div>
          <div class="sd-canary-name">
            {{ s.name }}
          </div>
          <div class="sd-canary-time">
            {{ s.time }}
          </div>
        </div>
      </div>

      <!-- 流量切分 + 对比 delta -->
      <div class="sd-canary-metrics">
        <div class="sd-canary-split">
          <div class="sd-canary-metric-label">
            流量切分
          </div>
          <div
            class="sd-canary-bar"
            data-spec="canary-bar"
          >
            <div
              class="sd-canary-seg-new"
              :style="{ width: canary.split.newPct + '%' }"
            />
            <div
              class="sd-canary-seg-old"
              :style="{ width: canary.split.oldPct + '%' }"
            />
          </div>
          <div class="sd-canary-legend">
            <span><i class="sd-canary-key is-new" />{{ canary.split.newVersion }} {{ canary.split.newPct }}%</span>
            <span><i class="sd-canary-key is-old" />{{ canary.split.oldVersion }} {{ canary.split.oldPct }}%</span>
          </div>
        </div>
        <div
          class="sd-canary-delta"
          data-spec="canary-delta-error"
        >
          <div class="sd-canary-metric-label">
            错误率变化
          </div>
          <div
            class="sd-canary-delta-value"
            :class="deltaClass(canary.errorRate.kind)"
          >
            {{ canary.errorRate.value }}
          </div>
          <div class="sd-canary-delta-sub">
            {{ canary.errorRate.sub }}
          </div>
        </div>
        <div
          class="sd-canary-delta"
          data-spec="canary-delta-p99"
        >
          <div class="sd-canary-metric-label">
            P99 变化
          </div>
          <div
            class="sd-canary-delta-value"
            :class="deltaClass(canary.p99.kind)"
          >
            {{ canary.p99.value }}
          </div>
          <div class="sd-canary-delta-sub">
            {{ canary.p99.sub }}
          </div>
        </div>
      </div>
    </div>

    <!-- 过滤 + 搜索 -->
    <div class="flex items-center justify-between gap-4">
      <div class="flex items-center gap-2">
        <button
          v-for="c in m.releaseStatusChips"
          :key="c.key"
          class="rounded-[18px] px-3.5 py-[7px] text-[13px] transition"
          :class="filter === c.key
            ? 'bg-[var(--el-color-primary)] text-white'
            : 'text-[var(--el-text-color-regular)] hover:bg-[var(--el-bg-color-overlay)]'"
          @click="filter = c.key"
        >
          {{ c.label }}
        </button>
      </div>
      <el-input
        v-model="keyword"
        placeholder="搜索应用 / 版本 / 提交信息"
        clearable
        size="small"
        class="w-64"
      />
    </div>

    <!-- 发布列表表格 -->
    <div class="flex flex-col gap-3">
      <el-table
        v-loading="loading"
        :data="filtered"
        class="sd-release-table"
        @row-click="(row: m.ReleaseRunRow) => openDetail(row)"
      >
        <el-table-column
          label="应用 / 流水线"
          min-width="200"
        >
          <template #default="{ row }">
            <div class="flex items-center gap-2.5">
              <div class="flex h-[34px] w-[34px] shrink-0 items-center justify-center rounded-lg bg-[var(--el-color-primary)]/15 text-sm font-semibold text-[var(--el-color-primary)]">
                {{ String(row.pipelineName || 'P').charAt(0) }}
              </div>
              <div class="min-w-0">
                <div class="truncate text-[14px] font-semibold text-[var(--el-text-color-primary)]">
                  {{ row.pipelineName || `流水线 #${row.pipelineId}` }}
                </div>
                <div class="truncate text-xs text-[var(--el-text-color-placeholder)]">
                  {{ row.branch || '—' }} · {{ row.commitMessage ? row.commitMessage.slice(0, 30) : '—' }}
                </div>
              </div>
            </div>
          </template>
        </el-table-column>
        <el-table-column
          label="版本"
          width="110"
          align="center"
        >
          <template #default="{ row }">
            <span
              v-if="row.appVersionId != null"
              class="rounded bg-[var(--el-fill-color-light)] px-1.5 py-0.5 text-xs font-mono text-[var(--el-text-color-regular)]"
            >
              v{{ row.appVersionId }}
            </span>
            <span
              v-else
              class="text-xs text-[var(--el-text-color-placeholder)]"
            >—</span>
          </template>
        </el-table-column>
        <el-table-column
          label="触发方式"
          width="120"
          align="center"
        >
          <template #default="{ row }">
            <span class="text-xs text-[var(--el-text-color-regular)]">{{ m.triggerSourceLabel(row.triggerSource || '') }}</span>
          </template>
        </el-table-column>
        <el-table-column
          label="状态"
          width="100"
        >
          <template #default="{ row }">
            <StatusPill :status="statusPill(Number(row.status ?? 0))">
              {{ statusLabel(Number(row.status ?? 0)) }}
            </StatusPill>
          </template>
        </el-table-column>
        <el-table-column
          label="耗时"
          width="80"
          align="right"
        >
          <template #default="{ row }">
            <span class="tabular-nums text-xs text-[var(--el-text-color-regular)]">
              {{ row.cost != null ? m.fmtDuration(row.cost) : '—' }}
            </span>
          </template>
        </el-table-column>
        <el-table-column
          label="触发时间"
          width="150"
        >
          <template #default="{ row }">
            <span class="text-xs tabular-nums text-[var(--el-text-color-placeholder)]">
              {{ m.fmtTime(row.buildStartedTime || row.createTime || row.commitTime) }}
            </span>
          </template>
        </el-table-column>
        <el-table-column
          label="操作"
          width="150"
          align="center"
          fixed="right"
        >
          <template #default="{ row }">
            <div class="flex items-center justify-center gap-2">
              <el-button
                size="small"
                type="primary"
                plain
                @click.stop="openDetail(row)"
              >
                详情
              </el-button>
              <el-button
                v-if="Number(row.status) === 5"
                size="small"
                type="danger"
                plain
                @click.stop="onRollback(row)"
              >
                回滚
              </el-button>
            </div>
          </template>
        </el-table-column>
        <template #empty>
          <div class="flex flex-col items-center gap-2 py-8">
            <span class="text-sm text-[var(--el-text-color-regular)]">暂无发布记录</span>
            <span class="text-xs text-[var(--el-text-color-placeholder)]">可切换项目，或从代码提交触发流水线后查看</span>
          </div>
        </template>
      </el-table>
    </div>

    <!-- 发布详情 Drawer：生命周期时间轴 -->
    <el-drawer
      v-model="drawerVisible"
      :title="current?.pipelineName || '发布详情'"
      size="600px"
      class="stardust-drawer"
      destroy-on-close
    >
      <template #header>
        <div class="flex min-w-0 flex-1 items-center justify-between gap-3">
          <div class="flex min-w-0 flex-col">
            <span class="truncate text-lg font-bold text-[var(--el-text-color-primary)]">
              {{ current?.pipelineName || '发布详情' }}
            </span>
            <span class="truncate text-xs text-[var(--el-text-color-secondary)]">
              v{{ current?.appVersionId ?? '—' }} · {{ m.triggerSourceLabel(current?.triggerSource || '') }} · {{ m.fmtTime(current?.commitTime) }}
            </span>
          </div>
          <div class="flex shrink-0 items-center gap-2">
            <StatusPill
              v-if="current"
              :status="statusPill(Number(current.status ?? 0))"
            >
              {{ statusLabel(Number(current.status ?? 0)) }}
            </StatusPill>
            <button
              class="sd-close"
              aria-label="关闭"
              @click="drawerVisible = false"
            >
              ×
            </button>
          </div>
        </div>
      </template>

      <div
        v-if="current"
        class="flex flex-col gap-5"
      >
        <!-- 提交信息摘要 -->
        <div class="sd-summary-card">
          <div class="sd-summary-row">
            <span class="sd-summary-label">提交人</span>
            <span class="sd-summary-value">{{ current.commitAuthor || '—' }}</span>
          </div>
          <div class="sd-summary-row">
            <span class="sd-summary-label">提交信息</span>
            <span class="sd-summary-value">{{ current.commitMessage || '—' }}</span>
          </div>
          <div class="sd-summary-row">
            <span class="sd-summary-label">提交 SHA</span>
            <span class="sd-summary-value font-mono">{{ current.commitId ? String(current.commitId).slice(0, 12) : '—' }}</span>
          </div>
          <div class="sd-summary-row">
            <span class="sd-summary-label">分支</span>
            <span class="sd-summary-value">{{ current.branch || '—' }}</span>
          </div>
        </div>

        <!-- 生命周期时间轴（垂直步骤条） -->
        <div class="sd-timeline">
          <div
            v-for="(s, i) in stages"
            :key="s.key"
            class="sd-stage"
          >
            <!-- 左侧圆点 + 连接线 -->
            <div class="sd-stage-rail">
              <div
                class="sd-stage-dot"
                :class="stageDotClass(s.state)"
              >
                {{ stageIcon(s.state) }}
              </div>
              <div
                v-if="i < stages.length - 1"
                class="sd-stage-line"
              />
            </div>

            <!-- 右侧内容 -->
            <div class="sd-stage-body">
              <div
                class="sd-stage-header"
                @click="toggleStage(s.key)"
              >
                <div class="flex items-center gap-2">
                  <span
                    class="sd-stage-label"
                    :class="stageTextClass(s.state)"
                  >{{ s.label }}</span>
                  <span
                    v-if="s.duration != null"
                    class="sd-stage-duration"
                  >{{ m.fmtDuration(s.duration) }}</span>
                </div>
                <span class="sd-stage-toggle">{{ expandedStage === s.key ? '−' : '+' }}</span>
              </div>

              <!-- 灰度阶段额外信息 -->
              <div
                v-if="s.canary"
                class="sd-canary-info"
              >
                <span class="sd-canary-label">灰度方式：</span>
                <span class="sd-canary-value">{{ s.canary.mode }}</span>
                <span class="sd-canary-label ml-3">进度：</span>
                <span class="sd-canary-value">{{ s.canary.progress }}</span>
              </div>

              <!-- 展开详情 -->
              <div
                v-if="expandedStage === s.key"
                class="sd-stage-detail"
              >
                <div class="sd-detail-row">
                  <span class="sd-detail-label">日志</span>
                  <span class="sd-detail-value">{{ s.detail.log }}</span>
                </div>
                <div class="sd-detail-row">
                  <span class="sd-detail-label">输入</span>
                  <span class="sd-detail-value">{{ s.detail.input }}</span>
                </div>
                <div class="sd-detail-row">
                  <span class="sd-detail-label">输出</span>
                  <span class="sd-detail-value">{{ s.detail.output }}</span>
                </div>
                <div class="sd-detail-row">
                  <span class="sd-detail-label">责任节点</span>
                  <span class="sd-detail-value">{{ s.detail.node }}</span>
                </div>
                <div
                  v-if="s.detail.reason !== '—'"
                  class="sd-detail-row"
                >
                  <span
                    class="sd-detail-label"
                    style="color: var(--el-color-danger)"
                  >失败原因</span>
                  <span
                    class="sd-detail-value"
                    style="color: var(--el-color-danger)"
                  >{{ s.detail.reason }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- 发布详情面板（设计稿 ReleaseDetailPanel） -->
        <div class="rd-grid">
          <section
            class="rd-panel"
            data-spec="rd-inst-panel"
          >
            <div class="rd-head">
              <span class="rd-title">实例进度</span>
              <span class="rd-sub">{{ instProgress.newCount }}/{{ instProgress.total }} 已推进</span>
            </div>
            <div class="rd-body">
              <div
                v-if="instProgress.total === 0"
                class="rd-empty"
                data-spec="rd-inst-empty"
              >
                实例数据待接入（未匹配到该应用的在线实例）
              </div>
              <div
                v-else
                class="rd-inst-grid"
                data-spec="rd-inst-grid"
              >
                <div
                  v-for="(c, i) in instProgress.cells"
                  :key="i"
                  class="rd-inst-cell"
                  :class="'is-' + c"
                  data-spec="rd-inst-cell"
                >
                  {{ String(i + 1).padStart(2, '0') }}
                </div>
              </div>
              <div class="rd-legend">
                <span>新版本 <b class="rd-mono is-good">{{ instProgress.newCount }}</b></span>
                <span>旧版本 <b class="rd-mono">{{ instProgress.oldCount }}</b></span>
                <span class="rd-spacer" />
                <span class="rd-mono">耗时 {{ current?.cost != null ? m.fmtDuration(current.cost) : '—' }}</span>
              </div>
            </div>
          </section>

          <section
            class="rd-panel"
            data-spec="rd-ctrl-panel"
          >
            <div class="rd-head">
              <span class="rd-title">发布控制</span>
            </div>
            <div class="rd-body">
              <div class="rd-dim">
                下一阶段：<b class="rd-mono">{{ nextStage }}</b>
              </div>
              <div class="rd-actions">
                <button
                  type="button"
                  class="rd-btn is-primary"
                  data-spec="rd-advance"
                  :disabled="!canAdvance"
                  @click="onAdvance"
                >
                  推进下一阶段
                </button>
                <button
                  type="button"
                  class="rd-btn"
                  data-spec="rd-pause"
                  :disabled="Number(current?.status) !== 3"
                  @click="onPause"
                >
                  暂停
                </button>
                <button
                  type="button"
                  class="rd-btn is-danger"
                  data-spec="rd-rollback"
                  @click="onRollbackStep"
                >
                  回滚一步
                </button>
              </div>
              <div class="rd-section-label">
                监控阈值
              </div>
              <dl class="rd-dl">
                <template
                  v-for="t in thresholds"
                  :key="t.name"
                >
                  <dt data-spec="rd-th-k">
                    {{ t.name }}
                  </dt>
                  <dd data-spec="rd-th-v">
                    <span class="rd-mono">当前 {{ t.current }}</span> / 阈值 <span class="rd-mono">{{ t.threshold }}</span>
                    · {{ t.pass === null ? '无数据' : t.pass ? '通过' : '超限' }}
                  </dd>
                </template>
              </dl>
            </div>
          </section>
        </div>

        <div class="rd-grid mt-4">
          <section
            class="rd-panel"
            data-spec="rd-err-panel"
          >
            <div class="rd-head">
              <span class="rd-title">错误率 · 灰度阶段</span>
              <span class="rd-sub">新版本 / 旧版本</span>
            </div>
            <div class="rd-body">
              <div
                class="rd-empty"
                data-spec="rd-err-empty"
              >
                按版本拆分的错误率序列待接入（后端无版本维度指标）
              </div>
            </div>
          </section>
          <section
            class="rd-panel"
            data-spec="rd-log-panel"
          >
            <div class="rd-head">
              <span class="rd-title">发布日志</span>
              <span class="rd-sub">{{ releaseLogs.length }} 条</span>
            </div>
            <div class="rd-body is-scroll">
              <div
                v-if="releaseLogs.length === 0"
                class="rd-empty"
                data-spec="rd-log-empty"
              >
                暂无日志
              </div>
              <div
                v-for="(l, i) in releaseLogs"
                v-else
                :key="i"
                class="rd-log-line"
                data-spec="rd-log-line"
              >
                <span class="rd-log-ts">{{ l.ts }}</span>
                <span
                  class="rd-log-lvl"
                  :class="'is-' + l.lvl"
                >{{ l.lvl }}</span>
                <span class="rd-log-msg">{{ l.msg }}</span>
              </div>
            </div>
          </section>
        </div>

        <!-- 底部操作 -->
        <div class="flex items-center justify-end gap-2 border-t border-[var(--el-border-color-light)] pt-3">
          <el-button
            size="small"
            plain
            @click="ElMessage.info('查看日志待接入')"
          >
            查看日志
          </el-button>
          <el-button
            size="small"
            type="primary"
            plain
            @click="onReprocess(current)"
          >
            重新发布
          </el-button>
          <el-button
            v-if="Number(current.status) === 5"
            size="small"
            type="danger"
            plain
            @click="onRollback(current)"
          >
            回滚
          </el-button>
        </div>
      </div>
    </el-drawer>
  </div>
</template>

<style scoped>
/* 发布表格 */
.sd-release-table {
  --el-table-border-color: var(--el-border-color-lighter);
  --el-table-header-bg-color: color-mix(in srgb, var(--el-bg-color-overlay) 60%, transparent);
  --el-table-tr-bg-color: color-mix(in srgb, var(--el-bg-color) 55%, transparent);
  --el-table-row-hover-bg-color: color-mix(in srgb, var(--el-color-primary) 10%, transparent);
  --el-table-bg-color: transparent;
  border-radius: 12px;
  overflow: hidden;
}
.sd-release-table :deep(.el-table__inner-wrapper::before) {
  display: none;
}
.sd-release-table :deep(th.el-table__cell) {
  background: color-mix(in srgb, var(--el-bg-color-overlay) 60%, transparent);
  color: var(--el-text-color-secondary);
  font-weight: 600;
}
.sd-release-table :deep(td.el-table__cell) {
  border-bottom-color: var(--el-border-color-lighter);
  cursor: pointer;
}

/* 摘要卡片 */
.sd-summary-card {
  display: flex;
  flex-direction: column;
  gap: 8px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 10px;
  padding: 14px 16px;
  background: color-mix(in srgb, var(--el-bg-color) 70%, transparent);
}
.sd-summary-row {
  display: flex;
  align-items: baseline;
  gap: 12px;
}
.sd-summary-label {
  flex-shrink: 0;
  width: 64px;
  font-size: 11px;
  color: var(--el-text-color-secondary);
}
.sd-summary-value {
  flex: 1;
  font-size: 13px;
  color: var(--el-text-color-primary);
  word-break: break-all;
}

/* 生命周期时间轴 */
.sd-timeline {
  display: flex;
  flex-direction: column;
}
.sd-stage {
  display: flex;
  gap: 12px;
}
.sd-stage-rail {
  display: flex;
  flex-direction: column;
  align-items: center;
  flex-shrink: 0;
  width: 24px;
}
.sd-stage-dot {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 22px;
  height: 22px;
  border-radius: 50%;
  font-size: 11px;
  font-weight: 700;
  color: var(--el-bg-color);
  flex-shrink: 0;
}
.sd-stage-line {
  flex: 1;
  width: 2px;
  min-height: 16px;
  background: var(--el-border-color);
}
.sd-stage-body {
  flex: 1;
  padding-bottom: 16px;
  min-width: 0;
}
.sd-stage-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 4px 0;
  cursor: pointer;
}
.sd-stage-label {
  font-size: 13px;
  font-weight: 600;
}
.sd-stage-duration {
  font-size: 11px;
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
  color: var(--el-text-color-secondary);
}
.sd-stage-toggle {
  font-size: 16px;
  color: var(--el-text-color-placeholder);
  line-height: 1;
}

/* 灰度信息 */
.sd-canary-info {
  margin-top: 4px;
  padding: 6px 10px;
  border-radius: 6px;
  background: color-mix(in srgb, var(--el-color-warning) 8%, transparent);
  font-size: 11px;
}
.sd-canary-label {
  color: var(--el-text-color-secondary);
}
.sd-canary-value {
  color: var(--el-text-color-primary);
}

/* 阶段详情展开 */
.sd-stage-detail {
  margin-top: 8px;
  display: flex;
  flex-direction: column;
  gap: 6px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 8px;
  padding: 10px 12px;
  background: color-mix(in srgb, var(--el-bg-color) 60%, transparent);
}
.sd-detail-row {
  display: flex;
  align-items: baseline;
  gap: 10px;
}
.sd-detail-label {
  flex-shrink: 0;
  width: 60px;
  font-size: 11px;
  color: var(--el-text-color-secondary);
}
.sd-detail-value {
  flex: 1;
  font-size: 12px;
  color: var(--el-text-color-primary);
  word-break: break-all;
}

/* 发布详情面板 */
.rd-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
  align-items: start;
}
.rd-panel {
  border: 1px solid var(--el-border-color-light);
  border-radius: 12px;
  overflow: hidden;
  background: color-mix(in srgb, var(--el-bg-color) 70%, transparent);
}
.rd-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  padding: 12px 16px;
  border-bottom: 1px solid var(--el-border-color-light);
}
.rd-title {
  font-size: 13px;
  font-weight: 500;
  color: var(--el-text-color-primary);
}
.rd-sub {
  font-size: 11px;
  color: var(--el-text-color-placeholder);
}
.rd-body {
  padding: 16px;
}
.rd-body.is-scroll {
  max-height: 280px;
  overflow-y: auto;
}
.rd-inst-grid {
  display: grid;
  grid-template-columns: repeat(10, minmax(0, 1fr));
  gap: 6px;
}
.rd-inst-cell {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 30px;
  border-radius: 6px;
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
  font-size: 10.5px;
  font-variant-numeric: tabular-nums;
}
.rd-inst-cell.is-online {
  background: color-mix(in srgb, var(--el-color-success) 24%, transparent);
  color: var(--el-color-success);
  border: 1px solid color-mix(in srgb, var(--el-color-success) 45%, transparent);
}
.rd-inst-cell.is-warn {
  background: color-mix(in srgb, var(--el-color-warning) 24%, transparent);
  color: var(--el-color-warning);
  border: 1px solid color-mix(in srgb, var(--el-color-warning) 45%, transparent);
}
.rd-inst-cell.is-off {
  background: var(--el-fill-color-light);
  color: var(--el-text-color-placeholder);
  border: 1px solid var(--el-border-color-lighter);
}
.rd-legend {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-top: 12px;
  font-size: 11px;
  color: var(--el-text-color-secondary);
}
.rd-spacer {
  flex: 1;
}
.rd-mono {
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
}
.rd-mono.is-good {
  color: var(--el-color-success);
}
.rd-dim {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
.rd-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 12px;
}
.rd-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  height: 30px;
  padding: 0 12px;
  border-radius: 8px;
  border: 1px solid var(--el-border-color);
  background: var(--el-bg-color);
  font-size: 12px;
  color: var(--el-text-color-regular);
  transition: border-color 120ms, color 120ms;
}
.rd-btn:hover:not(:disabled) {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
}
.rd-btn.is-primary {
  background: var(--el-color-primary);
  border-color: var(--el-color-primary);
  color: var(--el-color-white);
}
.rd-btn.is-danger:hover:not(:disabled) {
  border-color: var(--el-color-danger);
  color: var(--el-color-danger);
}
.rd-btn:disabled {
  opacity: 0.55;
}
.rd-section-label {
  margin-top: 16px;
  font-size: 11px;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--el-text-color-placeholder);
}
.rd-dl {
  display: grid;
  grid-template-columns: 96px 1fr;
  gap: 6px 12px;
  margin: 8px 0 0;
}
.rd-dl dt {
  font-size: 11.5px;
  color: var(--el-text-color-secondary);
}
.rd-dl dd {
  margin: 0;
  font-size: 11.5px;
  color: var(--el-text-color-regular);
}
.rd-log-line {
  display: flex;
  align-items: baseline;
  gap: 10px;
  padding: 6px 0;
  border-bottom: 1px solid var(--el-border-color-lighter);
  font-size: 11.5px;
}
.rd-log-line:last-child {
  border-bottom: none;
}
.rd-log-ts {
  flex-shrink: 0;
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
  color: var(--el-text-color-placeholder);
}
.rd-log-lvl {
  flex-shrink: 0;
  padding: 1px 6px;
  border-radius: 4px;
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
  font-size: 10px;
  text-transform: uppercase;
}
.rd-log-lvl.is-info {
  background: color-mix(in srgb, var(--el-color-info) 18%, transparent);
  color: var(--el-color-info);
}
.rd-log-lvl.is-warn {
  background: color-mix(in srgb, var(--el-color-warning) 18%, transparent);
  color: var(--el-color-warning);
}
.rd-log-lvl.is-danger {
  background: color-mix(in srgb, var(--el-color-danger) 18%, transparent);
  color: var(--el-color-danger);
}
.rd-log-msg {
  flex: 1;
  min-width: 0;
  color: var(--el-text-color-regular);
  word-break: break-all;
}
.rd-empty {
  padding: 24px 12px;
  text-align: center;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

/* 灰度发布面板 */
.sd-canary-card {
  border: 1px solid var(--el-border-color-light);
  border-radius: 12px;
  background: color-mix(in srgb, var(--el-bg-color) 70%, transparent);
  overflow: hidden;
}
.sd-canary-head {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 16px;
  border-bottom: 1px solid var(--el-border-color-light);
}
.sd-canary-title {
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
  font-size: 14px;
  color: var(--el-text-color-primary);
}
.sd-canary-ver {
  color: var(--el-text-color-placeholder);
}
.sd-canary-sub {
  margin-top: 3px;
  font-size: 11.5px;
  color: var(--el-text-color-placeholder);
}
.sd-canary-actions {
  margin-left: auto;
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}
.sd-canary-ico {
  width: 13px;
  height: 13px;
  margin-right: 4px;
}
.sd-canary-stages {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 12px;
  padding: 18px 24px;
}
.sd-canary-rail {
  display: flex;
  align-items: center;
  gap: 8px;
}
.sd-canary-dot {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 22px;
  height: 22px;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 600;
  color: var(--el-color-white);
  flex-shrink: 0;
}
.sd-canary-dot.is-done {
  background: var(--el-color-success);
}
.sd-canary-dot.is-run {
  background: var(--el-color-primary);
}
.sd-canary-dot.is-error {
  background: var(--el-color-danger);
}
.sd-canary-dot.is-pending {
  background: color-mix(in srgb, var(--el-bg-color-overlay) 80%, transparent);
  color: var(--el-text-color-placeholder);
  border: 1px solid var(--el-border-color);
}
.sd-canary-pulse {
  width: 6px;
  height: 6px;
  border-radius: 999px;
  background: var(--el-color-white);
}
.sd-canary-line {
  flex: 1;
  height: 1px;
  background: var(--el-border-color-light);
}
.sd-canary-line.is-done {
  background: var(--el-color-success);
}
.sd-canary-name {
  margin-top: 8px;
  font-size: 13px;
  font-weight: 500;
  color: var(--el-text-color-primary);
}
.sd-canary-time {
  margin-top: 3px;
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
  font-size: 11.5px;
  color: var(--el-text-color-placeholder);
}
.sd-canary-metrics {
  display: flex;
  align-items: flex-start;
  gap: 22px;
  padding: 14px;
  margin: 0 24px 18px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 10px;
  background: color-mix(in srgb, var(--el-bg-color) 70%, transparent);
}
.sd-canary-metric-label {
  font-size: 11px;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  color: var(--el-text-color-placeholder);
}
.sd-canary-bar {
  margin-top: 8px;
  display: flex;
  width: 240px;
  height: 8px;
  border-radius: 999px;
  overflow: hidden;
  background: color-mix(in srgb, var(--el-bg-color-overlay) 80%, transparent);
}
.sd-canary-seg-new {
  background: var(--el-color-primary);
}
.sd-canary-seg-old {
  background: var(--cyan-600, var(--el-color-info));
}
.sd-canary-legend {
  margin-top: 8px;
  display: flex;
  gap: 14px;
  font-size: 12px;
  color: var(--el-text-color-regular);
}
.sd-canary-key {
  display: inline-block;
  width: 6px;
  height: 6px;
  border-radius: 999px;
  margin-right: 6px;
}
.sd-canary-key.is-new {
  background: var(--el-color-primary);
}
.sd-canary-key.is-old {
  background: var(--cyan-600, var(--el-color-info));
}
.sd-canary-delta {
  border-left: 1px solid var(--el-border-color-light);
  padding-left: 22px;
}
.sd-canary-delta-value {
  margin-top: 4px;
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
  font-size: 22px;
  color: var(--el-text-color-primary);
}
.sd-canary-delta-value.is-good {
  color: var(--el-color-success);
}
.sd-canary-delta-value.is-bad {
  color: var(--el-color-danger);
}
.sd-canary-delta-value.is-none {
  color: var(--el-text-color-placeholder);
}
.sd-canary-delta-sub {
  margin-top: 2px;
  font-size: 11px;
  color: var(--el-text-color-placeholder);
}

/* Drawer 通用 */
.stardust-drawer :deep(.el-drawer__header) {
  margin-bottom: 0;
  padding: 16px 20px;
  border-bottom: 1px solid var(--el-border-color-light);
}
.stardust-drawer :deep(.el-drawer__body) {
  padding: 20px;
  background: color-mix(in srgb, var(--el-bg-color) 92%, transparent);
}
.stardust-drawer :deep(.el-drawer__close-btn) {
  display: none;
}
.sd-close {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border-radius: 50%;
  font-size: 18px;
  line-height: 1;
  color: var(--el-text-color-regular);
  transition: background-color 0.2s, color 0.2s;
}
.sd-close:hover {
  background: var(--el-fill-color-light);
  color: var(--el-text-color-primary);
}
</style>
