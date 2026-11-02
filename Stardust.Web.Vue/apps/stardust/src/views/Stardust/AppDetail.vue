<script setup lang="ts">
/**
 * 应用详情 AppDetail —— A8
 * ---------------------------------------------------------------------------
 * 设计真源：design-handoff/src/page-app-detail.jsx
 *   形态：独立页面（面包屑 + 标题行 + 7 Tabs），非抽屉。
 *   Tab：概览 | 实例 | 服务 | 配置 | 发布 | 可观测 | 审计
 *   概览区：6 项指标条 + （QPS·错误率图 | 实例网格）+（最近事件 | 依赖）
 *
 * 数据：真实接口 m.fetchAppDetail（config/runs/stats/services/history），空库走空态。
 * 样式纪律：颜色零硬编码，全部取设计令牌；图标为 Lucide 线性 path 内联 SVG。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { request } from '@newlifex/cube-vue/core/utils/request'
import { ElMessage } from 'element-plus'
import * as m from './appsModel'
import StatusPill from '@/layouts/StardustLayout/components/StatusPill.vue'
import EmptyState from '@/layouts/StardustLayout/components/EmptyState.vue'
import EChart from '@/components/EChart.vue'
import { stardustContext, environmentLabel } from '@/stores/stardustContext'

const route = useRoute()
const router = useRouter()

/** 路由参数既可为应用名，也可为数字 id */
const appKey = computed(() => String(route.params.id ?? ''))

type TabKey = 'overview' | 'instances' | 'services' | 'config' | 'releases' | 'obs' | 'audit'
const tab = ref<TabKey>('overview')

const TABS: { key: TabKey; label: string }[] = [
  { key: 'overview', label: '概览' },
  { key: 'instances', label: '实例' },
  { key: 'services', label: '服务' },
  { key: 'config', label: '配置' },
  { key: 'releases', label: '发布' },
  { key: 'obs', label: '可观测' },
  { key: 'audit', label: '审计' },
]

/** 可观测区二级分段（设计稿：Trace / 指标 / 日志 / 告警） */
const obsSeg = ref<'trace' | 'metric' | 'log' | 'alert'>('trace')
const OBS_SEGS: { key: typeof obsSeg.value; label: string }[] = [
  { key: 'trace', label: 'Trace' },
  { key: 'metric', label: '指标' },
  { key: 'log', label: '日志' },
  { key: 'alert', label: '告警' },
]

/** 概览图表时间窗（设计稿：1h / 6h / 24h） */
const range = ref<'1h' | '6h' | '24h'>('1h')

const loading = ref(false)
const detail = ref<m.AppDetail>({ config: [], runs: [], stats: [], services: [], instances: 0, history: [] })

/** 列表行带入的名称/版本/状态（首屏标题，避免二次请求列表） */
const rowName = ref('')
const rowVersion = ref('')
const rowStatus = ref<m.AppStatus>('unknown')

const appName = computed(() => rowName.value || appKey.value)
const statusPill = computed(() => rowStatus.value || 'unknown')

/** ① 指标条 6 项 */
const metrics = computed(() => {
  const sum = m.monitorSummary(detail.value.stats ?? [])
  const inst = detail.value.instances ?? 0
  return [
    { label: '实例', value: String(inst), unit: '', tone: inst > 0 ? 'ok' : 'warn' },
    { label: 'QPS', value: sum.qps ? String(Math.round(sum.qps)) : '0', unit: '', tone: 'info' },
    { label: '错误率', value: (sum.errorRate * 100).toFixed(2), unit: '%', tone: sum.errorRate > 0.01 ? 'danger' : 'ok' },
    { label: 'P99', value: sum.p99 ? String(Math.round(sum.p99)) : '—', unit: 'ms', tone: 'info' },
    { label: '版本', value: rowVersion.value || '—', unit: '', tone: 'info' },
    { label: '重启', value: '0', unit: '次/24h', tone: 'ok' },
  ] as { label: string; value: string; unit: string; tone: 'ok' | 'warn' | 'danger' | 'info' }[]
})

/** ② 左：QPS · 错误率 双序列（真实 stats 派生；空则空态） */
const chartOption = computed<Record<string, unknown>>(() => {
  const stats = [...(detail.value.stats ?? [])].sort((a, b) =>
    String(a.statTime ?? '').localeCompare(String(b.statTime ?? '')),
  )
  const take = range.value === '1h' ? 60 : range.value === '6h' ? 120 : stats.length
  const rows = stats.slice(-take)
  return {
    grid: { left: 44, right: 44, top: 28, bottom: 28, containLabel: true },
    tooltip: { trigger: 'axis' },
    legend: { data: ['QPS', '错误率 %'], right: 0, top: 0, textStyle: { fontSize: 11 } },
    xAxis: { type: 'category', boundaryGap: false, data: rows.map((r) => m.fmtMinute(r.statTime)) },
    yAxis: [
      { type: 'value', name: 'QPS', nameTextStyle: { fontSize: 10 } },
      { type: 'value', name: '错误率 %', nameTextStyle: { fontSize: 10 }, splitLine: { show: false } },
    ],
    series: [
      {
        name: 'QPS',
        type: 'line',
        smooth: true,
        showSymbol: false,
        data: rows.map((r) => Number(r.total ?? 0)),
        lineStyle: { width: 1.5 },
        areaStyle: { opacity: 0.12 },
      },
      {
        name: '错误率 %',
        type: 'line',
        smooth: true,
        showSymbol: false,
        yAxisIndex: 1,
        data: rows.map((r) => Number(((Number(r.errors ?? 0) / Math.max(Number(r.total ?? 1), 1)) * 100).toFixed(3))),
        lineStyle: { width: 1.5 },
      },
    ],
  }
})

/** ② 右：实例网格（每格 = 1 个实例，按状态着色） */
const instCells = computed(() =>
  (detail.value.instances ?? 0) > 0
    ? Array.from({ length: detail.value.instances ?? 0 }).map((_, i) => ({
        key: 'i' + i,
        label: String(i + 1).padStart(3, '0'),
        status: (i === 0 && statusPill.value !== 'running' ? statusPill.value : 'running') as string,
      }))
    : [],
)

const GRID_LEGEND = [
  { label: '在线', status: 'running' },
  { label: '告警', status: 'warn' },
  { label: '异常', status: 'error' },
  { label: '未运行', status: 'offline' },
]

function cellTone(status: string): string {
  const map: Record<string, string> = {
    running: 'var(--success-500)',
    warn: 'var(--warning-500)',
    error: 'var(--danger-500)',
    offline: 'var(--fg-4)',
  }
  return map[status] ?? 'var(--fg-4)'
}

/** ③ 左：最近事件（真实 runs + config history 派生） */
const events = computed(() => m.recentEvents(detail.value.runs ?? [], detail.value.history ?? []))

/** ③ 右：依赖（服务分组派生；空则空态） */
const deps = computed(() => {
  const groups = m.servicesGroups(detail.value.services ?? [])
  const up = groups.provide ?? []
  const down = groups.consume ?? []
  return {
    up: up.slice(0, 6).map((s) => ({ name: s.name, qps: Number(s.qps ?? 0) })),
    down: down.slice(0, 6).map((s) => ({ name: s.name, qps: Number(s.qps ?? 0), err: Number(s.errorRate ?? 0) })),
  }
})

/** 审计：真实 history 派生 */
const auditRows = computed(() =>
  (detail.value.history ?? []).map((h) => ({
    id: h.id,
    title: h.remark || h.summary || `配置发布 v${h.version ?? '-'}`,
    time: h.createTime || h.time || '',
    operator: h.creator || h.operator || '',
  })),
)

async function load() {
  if (!appKey.value) return
  loading.value = true
  try {
    const isId = /^\d+$/.test(appKey.value)
    const d = await m.fetchAppDetail(
      request,
      rowName.value || (isId ? undefined : appKey.value),
      isId ? Number(appKey.value) : undefined,
    )
    detail.value = d
  } catch {
    ElMessage.warning('应用详情加载失败，已显示空态')
  } finally {
    loading.value = false
  }
}

/** 列表跳转时由 sessionStorage 带入上下文 */
function hydrateFromSession() {
  try {
    const raw = sessionStorage.getItem('stardust-app-ctx')
    if (!raw) return
    const o = JSON.parse(raw) as { name?: string; version?: string; status?: m.AppStatus }
    if (o.name && o.name === appKey.value) {
      rowName.value = o.name
      rowVersion.value = o.version ?? ''
      rowStatus.value = o.status ?? 'unknown'
    }
  } catch {
    /* 上下文缺失不影响主流程 */
  }
}

onMounted(() => {
  hydrateFromSession()
  void load()
})

watch(appKey, () => {
  rowName.value = ''
  rowVersion.value = ''
  tab.value = 'overview'
  hydrateFromSession()
  void load()
})

function goBack() {
  void router.push('/stardust/apps')
}

function notWired(label: string) {
  ElMessage.info(`${label}：当前版本未接入`)
}
</script>

<template>
  <div
    class="sd-detail sd-page-stack"
    data-spec="app-detail"
  >
    <!-- ── 面包屑 ─────────────────────────────────────────────────────── -->
    <nav
      class="sd-crumb"
      data-spec="detail-crumb"
    >
      <button
        type="button"
        class="sd-crumb__link"
        @click="goBack"
      >
        应用
      </button>
      <span class="sd-crumb__sep">›</span>
      <span class="sd-crumb__cur">{{ appName || '—' }}</span>
    </nav>

    <!-- ── 标题行 ─────────────────────────────────────────────────────── -->
    <header
      class="sd-head"
      data-spec="detail-head"
    >
      <div class="sd-head__main">
        <div class="sd-head__title">
          <span
            class="sd-head__name"
            data-spec="detail-name"
          >{{ appName || '应用详情' }}</span>
          <StatusPill :status="statusPill" />
          <span
            v-if="rowVersion"
            class="sd-tag sd-tag--brand"
          >{{ rowVersion }}</span>
          <span class="sd-tag">{{ stardustContext.projectName || '默认' }}</span>
        </div>
        <div class="sd-head__sub">
          {{ stardustContext.projectName || '默认' }} · {{ environmentLabel(stardustContext.environment) }} ·
          实例 {{ detail.instances ?? 0 }} 个
        </div>
      </div>
      <div class="sd-head__actions">
        <button
          type="button"
          class="sd-btn"
          data-spec="detail-act-log"
          @click="notWired('查看日志')"
        >
          <svg
            class="sd-ico"
            viewBox="0 0 24 24"
            aria-hidden="true"
          >
            <path d="M4 17l6-5-6-5M12 19h8" />
          </svg>
          查看日志
        </button>
        <button
          type="button"
          class="sd-btn"
          data-spec="detail-act-rollback"
          @click="notWired('回滚')"
        >
          <svg
            class="sd-ico"
            viewBox="0 0 24 24"
            aria-hidden="true"
          >
            <path d="M3 12a9 9 0 1 0 3-6.7M3 4v5h5" />
          </svg>
          回滚
        </button>
        <button
          type="button"
          class="sd-btn sd-btn--primary"
          data-spec="detail-act-publish"
          @click="notWired('发布')"
        >
          <svg
            class="sd-ico"
            viewBox="0 0 24 24"
            aria-hidden="true"
          >
            <path d="M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 0 0-2.91-.09zM12 15l-3-3a22 22 0 0 1 2-3.95A12.88 12.88 0 0 1 22 2c0 2.72-.78 7.5-6 11a22.35 22.35 0 0 1-4 2z" />
          </svg>
          发布
        </button>
      </div>
    </header>

    <!-- ── Tabs ───────────────────────────────────────────────────────── -->
    <div
      class="sd-tabs"
      data-spec="detail-tabs"
    >
      <button
        v-for="t in TABS"
        :key="t.key"
        type="button"
        class="sd-tab"
        :class="{ 'is-active': tab === t.key }"
        data-spec="detail-tab"
        @click="tab = t.key"
      >
        {{ t.label }}
      </button>
    </div>

    <!-- ── 概览 ───────────────────────────────────────────────────────── -->
    <template v-if="tab === 'overview'">
      <div
        class="sd-metrics"
        data-spec="detail-metrics"
      >
        <div
          v-for="k in metrics"
          :key="k.label"
          class="sd-metric"
          data-spec="detail-metric"
        >
          <div class="sd-metric__label">
            {{ k.label }}
          </div>
          <div
            class="sd-metric__value"
            :class="'is-' + k.tone"
          >
            {{ k.value }}<span
              v-if="k.unit"
              class="sd-metric__unit"
            >{{ k.unit }}</span>
          </div>
        </div>
      </div>

      <div
        class="sd-two"
        style="grid-template-columns: 2fr 1fr"
      >
        <section
          class="sd-panel"
          data-spec="detail-chart"
        >
          <header class="sd-panel__head">
            <div>
              <div class="sd-panel__title">
                QPS · 错误率
              </div>
              <div class="sd-panel__sub">
                按分钟聚合
              </div>
            </div>
            <div class="sd-seg">
              <button
                v-for="r in (['1h', '6h', '24h'] as const)"
                :key="r"
                type="button"
                class="sd-seg__btn"
                :class="{ 'is-active': range === r }"
                data-spec="detail-range"
                @click="range = r"
              >
                {{ r }}
              </button>
            </div>
          </header>
          <div class="sd-panel__body">
            <EChart
              v-if="(detail.stats ?? []).length"
              :option="chartOption"
              height="200px"
            />
            <EmptyState
              v-else
              :icon="'M3 3v18h18M7 15l4-4 3 3 5-6'"
              title="暂无监控数据"
              hint="该应用上报分钟级统计后，此处显示 QPS 与错误率曲线"
            />
          </div>
        </section>

        <section
          class="sd-panel"
          data-spec="detail-inst-grid"
        >
          <header class="sd-panel__head">
            <div>
              <div class="sd-panel__title">
                实例网格
              </div>
              <div class="sd-panel__sub">
                {{ detail.instances ?? 0 }} 实例
              </div>
            </div>
          </header>
          <div class="sd-panel__body">
            <div
              v-if="instCells.length"
              class="sd-grid"
            >
              <div
                v-for="c in instCells"
                :key="c.key"
                class="sd-grid__cell"
                :title="c.label"
                :style="{
                  background: `color-mix(in srgb, ${cellTone(c.status)} 30%, var(--surface-2))`,
                  borderColor: `color-mix(in srgb, ${cellTone(c.status)} 40%, var(--border-1))`,
                }"
              >
                {{ c.label }}
              </div>
            </div>
            <EmptyState
              v-else
              :icon="'M4 4h16v6H4zM4 14h16v6H4z'"
              title="暂无在线实例"
              hint="实例上线后此处按状态着色"
            />
            <div class="sd-legend">
              <span
                v-for="l in GRID_LEGEND"
                :key="l.label"
                class="sd-legend__item"
              >
                <span
                  class="sd-legend__swatch"
                  :style="{ background: cellTone(l.status) }"
                />
                {{ l.label }}
              </span>
            </div>
          </div>
        </section>
      </div>

      <div
        class="sd-two"
        style="grid-template-columns: 1fr 1fr"
      >
        <section
          class="sd-panel"
          data-spec="detail-events"
        >
          <header class="sd-panel__head">
            <div class="sd-panel__title">
              最近事件
            </div>
          </header>
          <div class="sd-panel__body sd-scroll">
            <ol
              v-if="events.length"
              class="sd-timeline"
            >
              <li
                v-for="(e, i) in events.slice(0, 8)"
                :key="i"
                class="sd-timeline__item"
              >
                <span
                  class="sd-timeline__dot"
                  :class="'is-' + (e.type === 'config' ? 'info' : 'brand')"
                />
                <span class="sd-timeline__text">{{ e.text }}</span>
                <span class="sd-timeline__time sd-mono">{{ e.time }}</span>
              </li>
            </ol>
            <EmptyState
              v-else
              :icon="'M12 8v5l3 2M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z'"
              title="暂无事件"
              hint="发布与配置变更会记录在此"
            />
          </div>
        </section>

        <section
          class="sd-panel"
          data-spec="detail-deps"
        >
          <header class="sd-panel__head">
            <div>
              <div class="sd-panel__title">
                依赖
              </div>
              <div class="sd-panel__sub">
                调用方 + 被调用方
              </div>
            </div>
          </header>
          <div class="sd-panel__body">
            <div class="sd-section-label">
              调用方 (upstream)
            </div>
            <div
              v-if="deps.up.length"
              class="sd-dep-list"
            >
              <div
                v-for="d in deps.up"
                :key="'u' + d.name"
                class="sd-dep"
              >
                <span class="sd-tag sd-tag--brand">{{ d.name }}</span>
                <span class="sd-dep__num sd-mono">{{ d.qps.toLocaleString() }} QPS</span>
              </div>
            </div>
            <div
              v-else
              class="sd-dep-empty"
            >
              无
            </div>

            <div
              class="sd-section-label"
              style="margin-top: 12px"
            >
              被调用方 (downstream)
            </div>
            <div
              v-if="deps.down.length"
              class="sd-dep-list"
            >
              <div
                v-for="d in deps.down"
                :key="'d' + d.name"
                class="sd-dep"
              >
                <span class="sd-tag">{{ d.name }}</span>
                <span class="sd-dep__num sd-mono">{{ d.qps.toLocaleString() }} QPS · {{ d.err.toFixed(2) }}%</span>
              </div>
            </div>
            <div
              v-else
              class="sd-dep-empty"
            >
              无
            </div>
          </div>
        </section>
      </div>
    </template>

    <!-- ── 实例 ───────────────────────────────────────────────────────── -->
    <section
      v-else-if="tab === 'instances'"
      class="sd-panel"
      data-spec="detail-instances"
    >
      <div class="sd-panel__body">
        <el-table
          :data="detail.stats ?? []"
          data-spec="detail-inst-table"
        >
          <el-table-column
            prop="appName"
            label="应用"
            min-width="160"
          />
          <el-table-column
            prop="statTime"
            label="统计时间"
            min-width="150"
          />
          <el-table-column
            prop="total"
            label="调用次数"
            width="120"
            align="right"
          />
          <el-table-column
            prop="errors"
            label="错误数"
            width="110"
            align="right"
          />
          <template #empty>
            <EmptyState
              :icon="'M4 4h16v6H4zM4 14h16v6H4z'"
              title="暂无实例数据"
              hint="实例上报后此处显示明细"
            />
          </template>
        </el-table>
      </div>
    </section>

    <!-- ── 服务 ───────────────────────────────────────────────────────── -->
    <section
      v-else-if="tab === 'services'"
      class="sd-panel"
      data-spec="detail-services"
    >
      <div class="sd-panel__body">
        <el-table
          :data="detail.services ?? []"
          data-spec="detail-svc-table"
        >
          <el-table-column
            prop="name"
            label="服务"
            min-width="180"
          />
          <el-table-column
            prop="protocol"
            label="协议"
            width="110"
          />
          <el-table-column
            prop="instances"
            label="实例"
            width="100"
            align="right"
          />
          <el-table-column
            prop="qps"
            label="QPS"
            width="110"
            align="right"
          />
          <el-table-column
            prop="errorRate"
            label="错误率 %"
            width="120"
            align="right"
          />
          <el-table-column
            prop="p99"
            label="P99 (ms)"
            width="120"
            align="right"
          />
          <template #empty>
            <EmptyState
              :icon="'M12 2v20M2 12h20'"
              title="该应用暂无注册服务"
              hint="服务注册后此处显示协议与指标"
            />
          </template>
        </el-table>
      </div>
    </section>

    <!-- ── 配置 ───────────────────────────────────────────────────────── -->
    <section
      v-else-if="tab === 'config'"
      class="sd-panel"
      data-spec="detail-config"
    >
      <header class="sd-panel__head">
        <div>
          <div class="sd-panel__title">
            {{ appName }} · 配置
          </div>
          <div class="sd-panel__sub">
            {{ (detail.config ?? []).length }} 个文件
          </div>
        </div>
        <button
          type="button"
          class="sd-btn sd-btn--sm"
          @click="router.push('/stardust/config')"
        >
          去配置中心
        </button>
      </header>
      <div class="sd-panel__body">
        <el-table
          :data="detail.config ?? []"
          data-spec="detail-cfg-table"
        >
          <el-table-column
            prop="name"
            label="文件"
            min-width="180"
          />
          <el-table-column
            prop="version"
            label="当前版本"
            width="120"
          />
          <el-table-column
            prop="createTime"
            label="最近修改"
            min-width="150"
          />
          <template #empty>
            <EmptyState
              :icon="'M4 4h16v16H4zM8 4v16'"
              title="暂无配置文件"
              hint="接入配置中心后此处显示文件与版本"
            />
          </template>
        </el-table>
      </div>
    </section>

    <!-- ── 发布 ───────────────────────────────────────────────────────── -->
    <section
      v-else-if="tab === 'releases'"
      class="sd-panel"
      data-spec="detail-releases"
    >
      <div class="sd-panel__body">
        <el-table
          :data="detail.runs ?? []"
          data-spec="detail-rel-table"
        >
          <el-table-column
            prop="version"
            label="版本"
            width="120"
          />
          <el-table-column
            label="状态"
            width="130"
          >
            <template #default="{ row }">
              <StatusPill :status="m.normalizeRunStatus(row.status) || 'unknown'" />
            </template>
          </el-table-column>
          <el-table-column
            prop="createTime"
            label="开始时间"
            min-width="160"
          />
          <el-table-column
            prop="creator"
            label="发起人"
            width="130"
          />
          <template #empty>
            <EmptyState
              :icon="'M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2'"
              title="暂无发布记录"
              hint="触发发布后此处显示流水线运行历史"
            />
          </template>
        </el-table>
      </div>
    </section>

    <!-- ── 可观测 ─────────────────────────────────────────────────────── -->
    <section
      v-else-if="tab === 'obs'"
      class="sd-panel"
      data-spec="detail-obs"
    >
      <header class="sd-panel__head">
        <div class="sd-seg">
          <button
            v-for="s in OBS_SEGS"
            :key="s.key"
            type="button"
            class="sd-seg__btn"
            :class="{ 'is-active': obsSeg === s.key }"
            data-spec="detail-obs-seg"
            @click="obsSeg = s.key"
          >
            {{ s.label }}
          </button>
        </div>
      </header>
      <div class="sd-panel__body">
        <template v-if="obsSeg === 'trace'">
          <el-table
            :data="detail.stats ?? []"
            data-spec="detail-trace-table"
          >
            <el-table-column
              prop="statTime"
              label="时间"
              min-width="150"
            />
            <el-table-column
              prop="total"
              label="调用次数"
              width="120"
              align="right"
            />
            <el-table-column
              prop="errors"
              label="错误数"
              width="110"
              align="right"
            />
            <template #empty>
              <EmptyState
                :icon="'M3 3v18h18M7 15l4-4 3 3 5-6'"
                title="暂无 Trace 数据"
                hint="上报链路数据后此处显示明细"
              />
            </template>
          </el-table>
        </template>
        <EmptyState
          v-else
          :icon="'M3 3v18h18M7 15l4-4 3 3 5-6'"
          :title="(OBS_SEGS.find((s) => s.key === obsSeg)?.label || '') + ' 视图待接入'"
          hint="可跳转可观测中心查看全局视图"
        />
      </div>
    </section>

    <!-- ── 审计 ───────────────────────────────────────────────────────── -->
    <section
      v-else
      class="sd-panel"
      data-spec="detail-audit"
    >
      <div class="sd-panel__body sd-scroll">
        <ol
          v-if="auditRows.length"
          class="sd-timeline"
        >
          <li
            v-for="a in auditRows.slice(0, 12)"
            :key="a.id"
            class="sd-timeline__item"
          >
            <span class="sd-timeline__dot is-brand" />
            <span class="sd-timeline__text">
              {{ a.title }}<span
                v-if="a.operator"
                class="sd-timeline__who"
              > · {{ a.operator }}</span>
            </span>
            <span class="sd-timeline__time sd-mono">{{ a.time }}</span>
          </li>
        </ol>
        <EmptyState
          v-else
          :icon="'M12 8v5l3 2M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z'"
          title="暂无审计记录"
          hint="配置与发布操作会记录在此"
        />
      </div>
    </section>
  </div>
</template>

<style scoped>
.sd-detail {
  min-width: 0;
}
.sd-crumb {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--fg-3);
}
.sd-crumb__link {
  padding: 0;
  border: none;
  background: none;
  font-family: inherit;
  font-size: 12px;
  color: var(--fg-3);
  cursor: pointer;
}
.sd-crumb__link:hover {
  color: var(--brand-300);
}
.sd-crumb__sep {
  color: var(--fg-4);
}
.sd-crumb__cur {
  color: var(--fg-1);
}
.sd-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--space-4);
  flex-wrap: wrap;
}
.sd-head__main {
  min-width: 0;
}
.sd-head__title {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}
.sd-head__name {
  font-size: 20px;
  font-weight: 600;
  letter-spacing: -0.01em;
  color: var(--fg-1);
}
.sd-head__sub {
  margin-top: 6px;
  font-size: 12px;
  color: var(--fg-3);
}
.sd-head__actions {
  display: flex;
  align-items: center;
  gap: var(--space-2);
}
.sd-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 7px 13px;
  border-radius: var(--radius-md);
  border: 1px solid var(--border-2);
  background: var(--surface-2);
  color: var(--fg-1);
  font-family: var(--font-sans);
  font-size: 12.5px;
  font-weight: 500;
  line-height: 1;
  cursor: pointer;
  transition: border-color var(--dur-micro) var(--ease-out), background var(--dur-micro) var(--ease-out);
}
.sd-btn:hover {
  border-color: var(--brand-500);
}
.sd-btn--sm {
  padding: 5px 10px;
  font-size: 11.5px;
}
.sd-btn--primary {
  background: var(--sd-gradient-btn);
  border-color: transparent;
  box-shadow: var(--sd-glow-btn);
}
.sd-btn--primary:hover {
  filter: brightness(1.08);
}
.sd-ico {
  width: 13px;
  height: 13px;
  fill: none;
  stroke: currentColor;
  stroke-width: 1.5;
  stroke-linecap: round;
  stroke-linejoin: round;
}
.sd-tag {
  display: inline-flex;
  align-items: center;
  padding: 2px 8px;
  border-radius: var(--radius-sm);
  background: var(--surface-2);
  border: 1px solid var(--border-1);
  font-family: var(--font-mono);
  font-size: 11px;
  color: var(--fg-2);
  white-space: nowrap;
}
.sd-tag--brand {
  background: color-mix(in srgb, var(--brand-500) 15%, transparent);
  border-color: color-mix(in srgb, var(--brand-500) 30%, transparent);
  color: var(--brand-300);
}
.sd-tabs {
  display: flex;
  align-items: center;
  gap: var(--space-1);
  border-bottom: 1px solid var(--border-1);
  overflow-x: auto;
}
.sd-tab {
  position: relative;
  padding: 9px 14px;
  border: none;
  background: none;
  color: var(--fg-3);
  font-family: var(--font-sans);
  font-size: 13px;
  font-weight: 500;
  white-space: nowrap;
  cursor: pointer;
  transition: color var(--dur-micro) var(--ease-out);
}
.sd-tab:hover {
  color: var(--fg-1);
}
.sd-tab.is-active {
  color: var(--fg-1);
}
.sd-tab.is-active::after {
  content: '';
  position: absolute;
  left: 10px;
  right: 10px;
  bottom: -1px;
  height: 2px;
  border-radius: var(--radius-pill);
  background: var(--brand-500);
}
/* 指标条：对齐设计稿 .metric-strip（6 等分单条 + 分隔线） */
.sd-metrics {
  display: grid;
  grid-template-columns: repeat(6, 1fr);
  gap: 0;
  background: var(--surface-1);
  border: 1px solid var(--border-1);
  border-radius: 10px;
  overflow: hidden;
}
@media (max-width: 1100px) {
  .sd-metrics {
    grid-template-columns: repeat(3, 1fr);
  }
  .sd-metric:nth-child(3n) {
    border-right: none;
  }
}
.sd-metric {
  padding: 14px 18px;
  border-right: 1px solid var(--border-1);
  min-width: 0;
}
.sd-metric:last-child {
  border-right: none;
}
.sd-metric__label {
  font-size: 11px;
  color: var(--fg-3);
  text-transform: uppercase;
  letter-spacing: 0.06em;
}
.sd-metric__value {
  margin-top: 4px;
  font-family: var(--font-mono);
  font-size: 22px;
  font-weight: 600;
  line-height: 1.1;
  letter-spacing: -0.01em;
  font-variant-numeric: tabular-nums;
  color: var(--fg-1);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.sd-metric__value.is-ok {
  color: var(--success-500);
}
.sd-metric__value.is-warn {
  color: var(--warning-500);
}
.sd-metric__value.is-danger {
  color: var(--danger-500);
}
.sd-metric__value.is-info {
  color: var(--cyan-500);
}
.sd-metric__unit {
  margin-left: 4px;
  font-size: 11px;
  font-weight: 400;
  color: var(--fg-3);
}
.sd-two {
  display: grid;
  gap: var(--space-3);
}
@media (max-width: 1100px) {
  .sd-two {
    grid-template-columns: 1fr !important;
  }
}
.sd-panel {
  display: flex;
  flex-direction: column;
  min-width: 0;
  border-radius: var(--radius-lg);
  border: 1px solid var(--border-1);
  background: color-mix(in srgb, var(--surface-1) 80%, transparent);
  box-shadow: var(--elev-1);
  overflow: hidden;
}
.sd-panel__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
  padding: 12px 16px;
  border-bottom: 1px solid var(--border-1);
}
.sd-panel__title {
  font-size: 13px;
  font-weight: 600;
  color: var(--fg-1);
}
.sd-panel__sub {
  margin-top: 2px;
  font-size: 11px;
  color: var(--fg-3);
}
.sd-panel__body {
  padding: 12px 16px;
  min-width: 0;
}
.sd-scroll {
  max-height: 320px;
  overflow-y: auto;
}
.sd-seg {
  display: inline-flex;
  padding: 3px;
  border-radius: var(--radius-md);
  background: var(--surface-2);
  border: 1px solid var(--border-1);
}
.sd-seg__btn {
  padding: 4px 10px;
  border: none;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--fg-2);
  font-family: var(--font-mono);
  font-size: 11.5px;
  cursor: pointer;
  transition: background var(--dur-micro) var(--ease-out), color var(--dur-micro) var(--ease-out);
}
.sd-seg__btn.is-active {
  background: var(--brand-500);
  color: var(--fg-1);
}
.sd-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}
.sd-grid__cell {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 30px;
  height: 24px;
  border-radius: var(--radius-xs);
  border: 1px solid var(--border-1);
  font-family: var(--font-mono);
  font-size: 9.5px;
  color: var(--fg-2);
}
.sd-legend {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  margin-top: 12px;
  font-size: 11px;
  color: var(--fg-3);
}
.sd-legend__item {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}
.sd-legend__swatch {
  width: 8px;
  height: 8px;
  border-radius: 2px;
}
.sd-timeline {
  display: flex;
  flex-direction: column;
  gap: 10px;
  margin: 0;
  padding: 0;
  list-style: none;
}
.sd-timeline__item {
  display: flex;
  align-items: flex-start;
  gap: var(--space-2);
  font-size: 12px;
}
.sd-timeline__dot {
  width: 6px;
  height: 6px;
  margin-top: 5px;
  flex-shrink: 0;
  border-radius: var(--radius-pill);
}
.sd-timeline__dot.is-brand {
  background: var(--brand-500);
}
.sd-timeline__dot.is-info {
  background: var(--cyan-500);
}
.sd-timeline__text {
  flex: 1;
  min-width: 0;
  color: var(--fg-2);
  line-height: 1.5;
}
.sd-timeline__who {
  color: var(--fg-3);
}
.sd-timeline__time {
  flex-shrink: 0;
  font-size: 11px;
  color: var(--fg-4);
}
.sd-mono {
  font-family: var(--font-mono);
  font-variant-numeric: tabular-nums;
}
.sd-section-label {
  margin-bottom: 8px;
  font-size: 11px;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--fg-3);
}
.sd-dep-list {
  display: flex;
  flex-direction: column;
}
.sd-dep {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
  padding: 5px 0;
}
.sd-dep__num {
  font-size: 11px;
  color: var(--fg-3);
  white-space: nowrap;
}
.sd-dep-empty {
  font-size: 12px;
  color: var(--fg-4);
}
</style>
