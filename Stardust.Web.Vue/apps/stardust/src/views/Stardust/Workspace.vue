<script setup lang="ts">
/**
 * 工作台（设计稿 src/page-workspace.jsx · 设计方屏 app-01-workspace.png）
 *
 * 结构：页头（时间范围分段 + 刷新）→ 6 项指标条 → 应用星座面板（三视图）
 *       → 需要关注四栏（异常应用 / 异常节点 / 进行中发布 / 待处理告警）
 *       → 应用运行态势表（含 60 分钟 sparkline）+ 最近事件
 *
 * 逻辑全部抽 overview.model.ts 纯函数；数据全部来自真实接口。
 * 样式纪律：颜色一律 --el-* / --sd-* 令牌，禁 emoji，图标用 1.5px 线性内联 SVG。
 */
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { request } from '@newlifex/cube-vue/core/utils/request'
import { stardustContext } from '@/stores/stardustContext'
import ConstellationHealth from './ConstellationHealth.vue'
import {
  isNodeOnline,
  buildEvents,
  buildAttention,
  buildAppHealth,
  buildTopology,
  appMetrics,
  buildWorkspaceMetrics,
  buildSatellites,
  aggregateHealth,
  buildMatrix,
  buildSpark,
  type MinuteStatRow,
  type AlarmRow,
  type AppRow,
  type NodeRow,
  type Topology,
  type WsMetric,
  type WsSatellite,
  type WsHealth,
  type WsMatrix,
  type AppHealthRow,
  type EventItem,
  type AttentionItem,
} from './overview.model'

const router = useRouter()
const loading = ref(true)
const lastUpdate = ref<Date | null>(null)

const ranges = ['1h', '6h', '24h', '7d']
const range = ref('1h')

const nodes = ref<NodeRow[]>([])
const apps = ref<AppRow[]>([])
const instCount = ref<Map<string, number>>(new Map())
const minuteRows = ref<MinuteStatRow[]>([])
const alarmRows = ref<AlarmRow[]>([])
const runRows = ref<Record<string, unknown>[]>([])

const metrics = ref<WsMetric[]>([])
const satellites = ref<WsSatellite[]>([])
const health = ref<WsHealth>({ score: 0, online: 0, warn: 0, alert: 0, total: 0 })
const matrix = ref<WsMatrix>({ cols: [], rows: [], max: 0, edgeCount: 0 })
const topology = ref<Topology>({
  center: { label: '星辰核心', sub: 'Stardust 控制台', online: true, route: '/stardust/overview' },
  nodes: [],
  edges: [],
})
const events = ref<EventItem[]>([])
const attention = ref<AttentionItem[]>([])
const appHealth = ref<AppHealthRow[]>([])

/** 运行态势表行 */
interface WsTableRow {
  name: string
  label: string
  status: AppHealthRow['status']
  qps: number
  spark: number[]
  errorRate: number
  p99: number
  inst: number
  version: string
}
const tableRows = ref<WsTableRow[]>([])

const initialLoading = computed(() => loading.value && metrics.value.length === 0)

const subText = computed(() => {
  const c = stardustContext as unknown as Record<string, unknown>
  const p = String(c.projectName || c.project || '全部项目')
  const e = String(c.environmentName || c.environment || '全部环境')
  const t = lastUpdate.value
    ? lastUpdate.value.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit', second: '2-digit' })
    : '尚未加载'
  return p + ' · ' + e + ' · 更新于 ' + t
})

const badApps = computed(() => appHealth.value.filter((a) => a.status !== 'online'))
const badNodes = computed(() => nodes.value.filter((n) => !isNodeOnline(n, Date.now())))
const activeRuns = computed(() =>
  runRows.value.filter((r) => {
    const s = Number(r.status ?? 0)
    return s === 2 || s === 3
  }),
)
const pendingAlarms = computed(() => alarmRows.value)

function statusColor(status: AppHealthRow['status']): string {
  if (status === 'alarm') return 'var(--el-color-warning)'
  if (status === 'offline') return 'var(--el-color-danger)'
  return 'var(--el-color-success)'
}
function statusText(status: AppHealthRow['status']): string {
  if (status === 'alarm') return '告警'
  if (status === 'offline') return '离线'
  return '在线'
}

/** 发布进度：按 PipelineRun 状态折算百分比 */
function runProgress(r: Record<string, unknown>): number {
  const s = Number(r.status ?? 0)
  if (s === 4) return 100
  if (s === 3) return 71
  if (s === 2) return 57
  if (s === 1) return 29
  return 14
}
function runName(r: Record<string, unknown>): string {
  return String(r.pipelineName ?? '') || '流水线 #' + String(r.pipelineId ?? '-')
}
function runVersion(r: Record<string, unknown>): string {
  return r.appVersionId != null ? 'v' + String(r.appVersionId) : '—'
}

/** sparkline 路径（viewBox 140×26） */
function sparkPath(points: number[]): string {
  if (points.length < 2) return ''
  const max = Math.max(...points, 1)
  const min = Math.min(...points, 0)
  const span = max - min || 1
  return points
    .map((p, i) => {
      const x = (i / (points.length - 1)) * 140
      const y = 26 - ((p - min) / span) * 22 - 2
      return (i === 0 ? 'M ' : 'L ') + x.toFixed(1) + ' ' + y.toFixed(1)
    })
    .join(' ')
}

function goApp(): void {
  router.push('/stardust/apps')
}
function go(route: string): void {
  router.push(route)
}

async function reloadAll(): Promise<void> {
  loading.value = true
  try {
    // fail-soft：任一接口失败只让对应区块留空，不丢弃整页数据
    const safe = (url: string, params: Record<string, unknown>): Promise<{ data?: unknown } | null> =>
      request({ url, method: 'GET', params }).catch(() => null)
    const [nr, ar, or, mr, alr, crv, srv, plr] = await Promise.all([
      safe('/Nodes/Node', { PageSize: 200 }),
      safe('/Registry/App', { PageSize: 200 }),
      safe('/Registry/AppOnline', { PageSize: 500 }),
      safe('/Monitors/AppMinuteStat', { PageSize: 500 }),
      safe('/Monitors/AlarmRecord', { PageSize: 100, status: 1 }),
      safe('/Registry/AppConsume', { PageSize: 500 }),
      safe('/Registry/AppService', { PageSize: 500 }),
      safe('/Deployment/AppPipelineRun', { PageSize: 50 }),
    ])

    nodes.value = (nr?.data ?? []) as NodeRow[]
    apps.value = (ar?.data ?? []) as AppRow[]
    const onlineList = (or?.data ?? []) as Record<string, unknown>[]
    const online = new Set<string>()
    const count = new Map<string, number>()
    for (const r of onlineList) {
      const n = String(r.appName ?? '')
      if (!n) continue
      online.add(n)
      count.set(n, (count.get(n) ?? 0) + 1)
    }
    instCount.value = count
    const rows = (mr?.data ?? []) as MinuteStatRow[]
    minuteRows.value = rows
    alarmRows.value = (alr?.data ?? []) as AlarmRow[]
    runRows.value = (plr?.data ?? []) as Record<string, unknown>[]

    const now = Date.now()
    const activeReleases = runRows.value.filter((r) => {
      const s = Number(r.status ?? 0)
      return s === 2 || s === 3
    }).length

    let qpsSum = 0
    let weighted = 0
    for (const a of apps.value) {
      const mm = appMetrics(rows, a.name)
      qpsSum += mm.qps
      weighted += mm.errorRate * mm.qps
    }
    const errPct = qpsSum > 0 ? (weighted / qpsSum) * 100 : 0

    metrics.value = buildWorkspaceMetrics(
      apps.value,
      online,
      rows,
      alarmRows.value.length,
      activeReleases,
      onlineList.length,
    )
    satellites.value = buildSatellites(apps.value, online, rows, alarmRows.value, count, 8)
    health.value = aggregateHealth(satellites.value, alarmRows.value.length, errPct)
    topology.value = buildTopology(
      apps.value,
      online,
      (crv?.data ?? []) as AppRow[],
      (srv?.data ?? []) as AppRow[],
    )
    matrix.value = buildMatrix(topology.value, rows)
    attention.value = buildAttention(alarmRows.value, nodes.value, apps.value, online, now)
    appHealth.value = buildAppHealth(apps.value, online, alarmRows.value)
    events.value = buildEvents(nodes.value, apps.value, now)

    const statusOf = new Map<string, AppHealthRow['status']>()
    for (const h of appHealth.value) statusOf.set(h.name, h.status)
    tableRows.value = apps.value
      .map((a) => {
        const name = a.name ?? '-'
        const mm = appMetrics(rows, name)
        return {
          name,
          label: (a.displayName as string | undefined) || name,
          status: statusOf.get(name) ?? 'online',
          qps: mm.qps,
          spark: buildSpark(rows, name, 24),
          errorRate: mm.errorRate,
          p99: mm.p99,
          inst: count.get(name) ?? 0,
          version: String(a.version ?? '') || '-',
        }
      })
      .sort((a, b) => b.qps - a.qps)
      .slice(0, 8)

    lastUpdate.value = new Date()
  } catch (e: unknown) {
    console.error('[workspace] reload failed', e)
    ElMessage.error(e instanceof Error ? e.message : '加载工作台数据失败')
  } finally {
    loading.value = false
  }
}

onMounted(reloadAll)

watch(
  () => stardustContext.revision,
  () => {
    void reloadAll()
  },
)
</script>

<template>
  <div class="ws">
    <!-- 页头 -->
    <header class="ws-header">
      <div class="ws-head-l">
        <h1 class="ws-title">
          工作台
        </h1>
        <p
          class="ws-sub"
          data-spec="ws-sub"
        >
          {{ subText }}
        </p>
      </div>
      <div class="ws-actions">
        <div class="ws-seg">
          <button
            v-for="r in ranges"
            :key="r"
            type="button"
            class="ws-seg-btn"
            :class="{ 'is-active': range === r }"
            :data-spec="'ws-range-' + r"
            @click="range = r"
          >
            {{ r }}
          </button>
        </div>
        <button
          type="button"
          class="ws-refresh"
          data-spec="ws-refresh"
          :disabled="loading"
          @click="reloadAll"
        >
          <svg
            class="ws-ico"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="1.5"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <path d="M21 12a9 9 0 1 1-2.64-6.36" />
            <path d="M21 3v6h-6" />
          </svg>
          刷新
        </button>
      </div>
    </header>

    <!-- 指标条 -->
    <div
      class="ws-metrics"
      data-spec="ws-metrics"
    >
      <template v-if="initialLoading">
        <div
          v-for="i in 6"
          :key="i"
          class="ws-metric is-skeleton"
        />
      </template>
      <div
        v-for="mt in metrics"
        v-else
        :key="mt.key"
        class="ws-metric"
        data-spec="ws-metric"
        :class="'tone-' + mt.tone"
      >
        <div
          class="ws-metric-label"
          data-spec="ws-metric-label"
        >
          {{ mt.label }}
        </div>
        <div class="ws-metric-row">
          <span
            class="ws-metric-value"
            data-spec="ws-metric-value"
          >{{ mt.value }}</span>
          <span
            v-if="mt.unit"
            class="ws-metric-unit"
          >{{ mt.unit }}</span>
        </div>
        <div
          class="ws-metric-delta"
          data-spec="ws-metric-delta"
          :class="mt.good ? 'is-good' : 'is-bad'"
        >
          {{ mt.delta }}
        </div>
      </div>
    </div>

    <!-- 应用星座 -->
    <div
      class="ws-section-label"
      data-spec="ws-section-label"
    >
      应用星座 · Application Constellation
    </div>
    <ConstellationHealth
      :health="health"
      :satellites="satellites"
      :matrix="matrix"
      :topology="topology"
      @select-app="goApp"
    />

    <!-- 需要关注 -->
    <div
      class="ws-section-label"
      data-spec="ws-section-label"
    >
      需要关注
    </div>
    <div class="ws-four">
      <!-- 异常应用 -->
      <section
        class="ws-panel"
        data-spec="ws-attn"
      >
        <div class="ws-panel-head">
          <span class="ws-attn-title">异常应用</span>
          <span class="ws-attn-sub">{{ badApps.length }} 项</span>
        </div>
        <div class="ws-panel-body">
          <div
            v-if="badApps.length === 0"
            class="ws-attn-empty"
            data-spec="ws-attn-empty"
          >
            暂无异常
          </div>
          <button
            v-for="a in badApps.slice(0, 4)"
            v-else
            :key="a.name"
            type="button"
            class="ws-attn-row"
            data-spec="ws-attn-row"
            @click="goApp()"
          >
            <span
              class="ws-dot"
              :style="{ background: statusColor(a.status) }"
            />
            <span class="ws-attn-main">
              <span class="ws-attn-name">{{ a.displayName || a.name }}</span>
              <span class="ws-attn-desc">版本 {{ a.version || '-' }}</span>
            </span>
          </button>
        </div>
      </section>

      <!-- 异常节点 -->
      <section
        class="ws-panel"
        data-spec="ws-attn"
      >
        <div class="ws-panel-head">
          <span class="ws-attn-title">异常节点</span>
          <span class="ws-attn-sub">{{ badNodes.length }} 项</span>
        </div>
        <div class="ws-panel-body">
          <div
            v-if="badNodes.length === 0"
            class="ws-attn-empty"
            data-spec="ws-attn-empty"
          >
            暂无异常
          </div>
          <button
            v-for="n in badNodes.slice(0, 4)"
            v-else
            :key="String(n.name ?? n.code)"
            type="button"
            class="ws-attn-row"
            data-spec="ws-attn-row"
            @click="go('/stardust/nodes')"
          >
            <span
              class="ws-dot"
              :style="{ background: 'var(--el-color-danger)' }"
            />
            <span class="ws-attn-main">
              <span class="ws-attn-name">{{ n.name || n.code || '-' }}</span>
              <span class="ws-attn-desc">{{ n.iP || '-' }} · 节点离线</span>
            </span>
          </button>
        </div>
      </section>

      <!-- 进行中发布 -->
      <section
        class="ws-panel"
        data-spec="ws-attn"
      >
        <div class="ws-panel-head">
          <span class="ws-attn-title">进行中发布</span>
          <span class="ws-attn-sub">{{ activeRuns.length }} 项</span>
        </div>
        <div class="ws-panel-body">
          <div
            v-if="activeRuns.length === 0"
            class="ws-attn-empty"
            data-spec="ws-attn-empty"
          >
            无进行中发布
          </div>
          <button
            v-for="r in activeRuns.slice(0, 3)"
            v-else
            :key="String(r.id ?? r.Id ?? Math.random())"
            type="button"
            class="ws-attn-row is-block"
            data-spec="ws-attn-row"
            @click="go('/stardust/release')"
          >
            <span class="ws-attn-main">
              <span class="ws-attn-name">{{ runName(r) }}</span>
              <span class="ws-attn-desc">{{ runVersion(r) }} · 部署中</span>
            </span>
            <span
              class="ws-gauge"
              data-spec="ws-gauge"
            ><span :style="{ width: runProgress(r) + '%' }" /></span>
          </button>
        </div>
      </section>

      <!-- 待处理告警 -->
      <section
        class="ws-panel"
        data-spec="ws-attn"
      >
        <div class="ws-panel-head">
          <span class="ws-attn-title">待处理告警</span>
          <span class="ws-attn-sub">{{ pendingAlarms.length }} 项</span>
        </div>
        <div class="ws-panel-body">
          <div
            v-if="pendingAlarms.length === 0"
            class="ws-attn-empty"
            data-spec="ws-attn-empty"
          >
            暂无告警
          </div>
          <button
            v-for="(al, i) in pendingAlarms.slice(0, 4)"
            v-else
            :key="'al-' + i"
            type="button"
            class="ws-attn-row"
            data-spec="ws-attn-row"
            @click="go('/stardust/observability')"
          >
            <span
              class="ws-dot"
              :style="{ background: 'var(--el-color-danger)' }"
            />
            <span class="ws-attn-main">
              <span class="ws-attn-name">{{ al.Action || al.Name || '-' }}</span>
              <span class="ws-attn-desc">{{ al.Category || '告警中' }}</span>
            </span>
          </button>
        </div>
      </section>
    </div>

    <!-- 运行态势表 + 最近事件 -->
    <div class="ws-two">
      <section class="ws-panel ws-span2">
        <div class="ws-panel-head">
          <span class="ws-attn-title">应用运行态势</span>
          <span class="ws-attn-sub">按 QPS 排序</span>
        </div>
        <div class="ws-panel-body is-flush">
          <div
            v-if="tableRows.length === 0"
            class="ws-attn-empty"
            data-spec="ws-attn-empty"
          >
            暂无应用数据
          </div>
          <table
            v-else
            class="ws-table"
          >
            <thead>
              <tr>
                <th data-spec="ws-th">
                  应用
                </th>
                <th data-spec="ws-th">
                  状态
                </th>
                <th
                  class="num"
                  data-spec="ws-th"
                >
                  QPS
                </th>
                <th data-spec="ws-th">
                  60 分钟趋势
                </th>
                <th
                  class="num"
                  data-spec="ws-th"
                >
                  错误率
                </th>
                <th
                  class="num"
                  data-spec="ws-th"
                >
                  P99
                </th>
                <th
                  class="num"
                  data-spec="ws-th"
                >
                  实例
                </th>
                <th data-spec="ws-th">
                  版本
                </th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="r in tableRows"
                :key="r.name"
                data-spec="ws-row"
                @click="goApp()"
              >
                <td class="link">
                  {{ r.label }}
                </td>
                <td>
                  <span class="ws-status">
                    <i
                      class="ws-dot"
                      :style="{ background: statusColor(r.status) }"
                    />{{ statusText(r.status) }}
                  </span>
                </td>
                <td class="num">
                  {{ r.qps.toFixed(1) }}
                </td>
                <td>
                  <svg
                    class="ws-spark"
                    data-spec="ws-spark"
                    viewBox="0 0 140 26"
                    preserveAspectRatio="none"
                    aria-hidden="true"
                  >
                    <path
                      :d="sparkPath(r.spark)"
                      fill="none"
                      stroke="var(--el-color-primary)"
                      stroke-width="1.5"
                      vector-effect="non-scaling-stroke"
                    />
                  </svg>
                </td>
                <td
                  class="num"
                  :style="{ color: r.errorRate >= 0.01 ? 'var(--el-color-danger)' : 'inherit' }"
                >
                  {{ (r.errorRate * 100).toFixed(2) }}%
                </td>
                <td class="num">
                  {{ r.p99 }} ms
                </td>
                <td class="num">
                  {{ r.inst }}
                </td>
                <td>
                  <span class="ws-tag">{{ r.version }}</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <section class="ws-panel">
        <div class="ws-panel-head">
          <span class="ws-attn-title">最近事件</span>
          <span class="ws-attn-sub">{{ events.length }} 条</span>
        </div>
        <div class="ws-panel-body is-scroll">
          <div
            v-if="events.length === 0"
            class="ws-attn-empty"
            data-spec="ws-attn-empty"
          >
            暂无事件
          </div>
          <div
            v-for="(ev, i) in events"
            v-else
            :key="'ev-' + i"
            class="ws-event"
            data-spec="ws-event"
          >
            <span
              class="ws-event-dot"
              :style="{
                background: ev.status === 'online'
                  ? 'var(--el-color-success)'
                  : ev.status === 'warn'
                    ? 'var(--el-color-warning)'
                    : 'var(--el-color-danger)',
              }"
            />
            <div class="ws-event-main">
              <div class="ws-event-top">
                <span class="ws-event-name">{{ ev.service }}</span>
                <span class="ws-event-time">{{ ev.time }}</span>
              </div>
              <div class="ws-event-detail">
                <span class="ws-tag">{{ ev.tag }}</span>{{ ev.detail }}
              </div>
            </div>
          </div>
        </div>
      </section>
    </div>
  </div>
</template>

<style scoped>
.ws {
  display: flex;
  flex-direction: column;
  gap: 16px;
  overflow-y: auto;
}
.ws-header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 16px;
}
.ws-title {
  font-size: 18px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}
.ws-sub {
  margin-top: 2px;
  font-size: 12px;
  color: var(--el-text-color-regular);
}
.ws-actions {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-shrink: 0;
}
.ws-seg {
  display: flex;
  gap: 2px;
  padding: 3px;
  border-radius: 8px;
  border: 1px solid var(--el-border-color-light);
  background: color-mix(in srgb, var(--el-bg-color-overlay) 70%, transparent);
}
.ws-seg-btn {
  padding: 5px 12px;
  border-radius: 6px;
  font-size: 12px;
  color: var(--el-text-color-regular);
  transition: background-color 120ms cubic-bezier(0.16, 1, 0.3, 1), color 120ms;
}
.ws-seg-btn.is-active {
  background: var(--el-color-primary);
  color: var(--el-color-white);
}
.ws-refresh {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  height: 32px;
  padding: 0 12px;
  border-radius: 8px;
  border: 1px solid var(--el-border-color);
  background: var(--el-bg-color);
  font-size: 12px;
  color: var(--el-text-color-regular);
  transition: border-color 120ms, color 120ms;
}
.ws-refresh:hover:not(:disabled) {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
}
.ws-refresh:disabled {
  opacity: 0.6;
}
.ws-ico {
  width: 14px;
  height: 14px;
}

/* 指标条：单条 6 等分 + border-right 分隔 */
.ws-metrics {
  display: grid;
  grid-template-columns: repeat(6, minmax(0, 1fr));
  border: 1px solid var(--el-border-color-light);
  border-radius: 10px;
  overflow: hidden;
  background: color-mix(in srgb, var(--el-bg-color) 70%, transparent);
}
.ws-metric {
  padding: 14px 18px;
  border-right: 1px solid var(--el-border-color-light);
}
.ws-metric:last-child {
  border-right: none;
}
.ws-metric.is-skeleton {
  height: 84px;
  background: var(--el-fill-color);
}
.ws-metric-label {
  font-size: 11px;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--el-text-color-secondary);
}
.ws-metric-row {
  margin-top: 6px;
  display: flex;
  align-items: baseline;
  gap: 4px;
}
.ws-metric-value {
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
  font-size: 22px;
  font-weight: 600;
  line-height: 1;
  color: var(--el-text-color-primary);
  font-variant-numeric: tabular-nums;
}
.ws-metric.tone-brand .ws-metric-value {
  color: var(--el-color-primary);
}
.ws-metric.tone-success .ws-metric-value {
  color: var(--el-color-success);
}
.ws-metric.tone-warning .ws-metric-value {
  color: var(--el-color-warning);
}
.ws-metric.tone-danger .ws-metric-value {
  color: var(--el-color-danger);
}
.ws-metric.tone-cyan .ws-metric-value {
  color: var(--el-color-info);
}
.ws-metric-unit {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
.ws-metric-delta {
  margin-top: 6px;
  font-size: 11px;
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
}
.ws-metric-delta.is-good {
  color: var(--el-color-success);
}
.ws-metric-delta.is-bad {
  color: var(--el-color-danger);
}

.ws-section-label {
  margin-top: 4px;
  font-size: 13px;
  font-weight: 500;
  letter-spacing: 0.02em;
  color: var(--el-text-color-regular);
}

/* 需要关注四栏 */
.ws-four {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 16px;
}
.ws-two {
  display: grid;
  grid-template-columns: 2fr 1fr;
  gap: 16px;
  align-items: start;
}
.ws-span2 {
  min-width: 0;
}
.ws-panel {
  border: 1px solid var(--el-border-color-light);
  border-radius: 12px;
  overflow: hidden;
  background: color-mix(in srgb, var(--el-bg-color) 70%, transparent);
}
.ws-panel-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  padding: 12px 16px;
  border-bottom: 1px solid var(--el-border-color-light);
}
.ws-attn-title {
  font-size: 13px;
  font-weight: 500;
  color: var(--el-text-color-primary);
}
.ws-attn-sub {
  font-size: 11px;
  color: var(--el-text-color-placeholder);
}
.ws-panel-body {
  padding: 0;
}
.ws-panel-body.is-scroll {
  max-height: 520px;
  overflow-y: auto;
}
.ws-attn-row {
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
  padding: 10px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  text-align: left;
  transition: background-color 120ms;
}
.ws-attn-row:last-child {
  border-bottom: none;
}
.ws-attn-row:hover {
  background: color-mix(in srgb, var(--el-color-primary) 8%, transparent);
}
.ws-attn-row.is-block {
  flex-direction: column;
  align-items: stretch;
  gap: 6px;
}
.ws-attn-main {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
  flex: 1;
}
.ws-attn-name {
  font-size: 13px;
  font-weight: 500;
  color: var(--el-text-color-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.ws-attn-desc {
  font-size: 11px;
  color: var(--el-text-color-secondary);
}
.ws-attn-empty {
  padding: 24px 12px;
  text-align: center;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
.ws-dot {
  display: inline-block;
  width: 6px;
  height: 6px;
  border-radius: 999px;
  flex-shrink: 0;
}
.ws-gauge {
  display: block;
  height: 4px;
  border-radius: 999px;
  overflow: hidden;
  background: var(--el-fill-color);
}
.ws-gauge > span {
  display: block;
  height: 100%;
  background: var(--el-color-primary);
}

/* 运行态势表 */
.ws-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 12.5px;
}
.ws-table th {
  padding: 10px 16px;
  text-align: left;
  font-size: 11px;
  font-weight: 600;
  color: var(--el-text-color-secondary);
  background: color-mix(in srgb, var(--el-bg-color-overlay) 60%, transparent);
  border-bottom: 1px solid var(--el-border-color-lighter);
}
.ws-table th.num,
.ws-table td.num {
  text-align: right;
  font-variant-numeric: tabular-nums;
}
.ws-table td {
  padding: 10px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  color: var(--el-text-color-regular);
}
.ws-table tbody tr {
  cursor: pointer;
  transition: background-color 120ms;
}
.ws-table tbody tr:hover {
  background: color-mix(in srgb, var(--el-color-primary) 8%, transparent);
}
.ws-table td.link {
  color: var(--el-text-color-primary);
  font-weight: 500;
}
.ws-status {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
}
.ws-spark {
  display: block;
  width: 140px;
  height: 26px;
}
.ws-tag {
  display: inline-block;
  padding: 2px 6px;
  border-radius: 4px;
  background: var(--el-fill-color-light);
  font-size: 11px;
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
  color: var(--el-text-color-regular);
}

/* 最近事件 */
.ws-event {
  display: flex;
  gap: 10px;
  padding: 10px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}
.ws-event:last-child {
  border-bottom: none;
}
.ws-event-dot {
  width: 6px;
  height: 6px;
  border-radius: 999px;
  margin-top: 6px;
  flex-shrink: 0;
}
.ws-event-main {
  min-width: 0;
  flex: 1;
}
.ws-event-top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}
.ws-event-name {
  font-size: 12.5px;
  font-weight: 500;
  color: var(--el-text-color-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.ws-event-time {
  font-size: 11px;
  color: var(--el-text-color-secondary);
  flex-shrink: 0;
}
.ws-event-detail {
  margin-top: 3px;
  font-size: 11.5px;
  color: var(--el-text-color-regular);
}
</style>
