<script setup lang="ts">
/**
 * 指标 Tab 四图（设计稿 src/page-observability.jsx · MetricsTab）
 * 2×2：全局 QPS / 全局错误率 / P50·P95·P99 / Node CPU·集群平均，每图 height 200。
 * 数据源 /Monitors/AppMinuteStat（fail-soft）；颜色运行时解析 --el-* 令牌，零硬编码。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { request } from '@newlifex/cube-vue/core/utils/request'
import EChart from '@/components/EChart.vue'
import * as p from '../observabilityPanels.model'

const props = defineProps<{ appId?: number | null }>()

const rows = ref<p.MinuteStatLite[]>([])
const metric = ref('all')
const range = ref('1h')
const token = ref({ brand: '', danger: '', cyan: '', fg3: '' })

const metrics = [
  { key: 'all', label: '全部' },
  { key: 'qps', label: 'QPS' },
  { key: 'err', label: '错误率' },
  { key: 'p99', label: 'P99' },
]
const ranges = [
  { key: '1h', label: '过去 1 小时' },
  { key: '6h', label: '过去 6 小时' },
  { key: '24h', label: '过去 24 小时' },
]

async function load() {
  const params: Record<string, unknown> = { PageSize: 200 }
  if (props.appId) params.AppId = props.appId
  const r = await request({ url: '/Monitors/AppMinuteStat', method: 'GET', params }).catch(() => null)
  rows.value = (r?.data ?? []) as p.MinuteStatLite[]
}

onMounted(() => {
  token.value = {
    brand: p.resolveToken('--el-color-primary'),
    danger: p.resolveToken('--el-color-danger'),
    cyan: p.resolveToken('--el-color-info'),
    fg3: p.resolveToken('--el-text-color-secondary'),
  }
  void load()
})
watch(() => props.appId, () => void load())

const panels = computed(() => p.buildMetricsPanels(rows.value))
const visible = computed(() => {
  const k = metric.value
  return { qps: k === 'all' || k === 'qps', err: k === 'all' || k === 'err', lat: k === 'all' || k === 'p99', cpu: k === 'all' }
})

const qpsOption = computed(() => p.lineOption([{ name: 'QPS', points: panels.value.qps, color: token.value.brand }], { area: true }))
const errOption = computed(() => p.lineOption([{ name: 'err %', points: panels.value.err, color: token.value.danger }], { area: true, unit: '%' }))
const latOption = computed(() =>
  p.lineOption(
    [
      { name: 'P50', points: panels.value.latency.p50, color: token.value.fg3 },
      { name: 'P95', points: panels.value.latency.p95, color: token.value.cyan },
      { name: 'P99', points: panels.value.latency.p99, color: token.value.brand },
    ],
    { unit: ' ms' },
  ),
)
const cpuOption = computed(() => p.lineOption([{ name: 'CPU %', points: panels.value.cpu, color: token.value.cyan }], { area: true, unit: '%' }))
</script>

<template>
  <div class="om-wrap">
    <div
      class="om-bar"
      data-spec="obs-metrics-bar"
    >
      <span class="om-select-label">指标:</span>
      <select
        v-model="metric"
        class="om-select"
        data-spec="obs-metric-select"
      >
        <option
          v-for="mt in metrics"
          :key="mt.key"
          :value="mt.key"
        >
          {{ mt.label }}
        </option>
      </select>
      <select
        v-model="range"
        class="om-select"
        data-spec="obs-range-select"
      >
        <option
          v-for="rg in ranges"
          :key="rg.key"
          :value="rg.key"
        >
          {{ rg.label }}
        </option>
      </select>
      <span class="om-spacer" />
      <button
        class="om-icon-btn"
        type="button"
        title="刷新"
        data-spec="obs-refresh"
        @click="load()"
      >
        <svg
          width="13"
          height="13"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="1.5"
          stroke-linecap="round"
          stroke-linejoin="round"
        >
          <path d="M21 12a9 9 0 1 1-3-6.7" />
          <path d="M21 3v6h-6" />
        </svg>
      </button>
    </div>

    <div
      class="om-grid"
      data-spec="obs-metrics-grid"
    >
      <section
        v-if="visible.qps"
        class="om-card"
        data-spec="obs-chart"
        data-chart="qps"
      >
        <div class="om-head">
          <span class="om-title">全局 QPS</span>
          <span class="om-sub">{{ panels.qps.length }} 点</span>
        </div>
        <div class="om-body">
          <EChart
            v-if="panels.qps.length"
            :option="qpsOption"
            height="200px"
          />
          <div
            v-else
            class="om-empty"
            data-spec="obs-chart-empty"
          >
            分钟统计待接入
          </div>
        </div>
      </section>

      <section
        v-if="visible.err"
        class="om-card"
        data-spec="obs-chart"
        data-chart="err"
      >
        <div class="om-head">
          <span class="om-title">全局错误率</span>
          <span class="om-sub">err %</span>
        </div>
        <div class="om-body">
          <EChart
            v-if="panels.err.length"
            :option="errOption"
            height="200px"
          />
          <div
            v-else
            class="om-empty"
            data-spec="obs-chart-empty"
          >
            分钟统计待接入
          </div>
        </div>
      </section>

      <section
        v-if="visible.lat"
        class="om-card"
        data-spec="obs-chart"
        data-chart="latency"
      >
        <div class="om-head">
          <span class="om-title">P50 · P95 · P99</span>
          <span class="om-sub">{{ panels.approxLatency ? '近似 · 无分位直方图' : '延迟分位' }}</span>
        </div>
        <div class="om-body">
          <EChart
            v-if="panels.latency.p50.length"
            :option="latOption"
            height="200px"
          />
          <div
            v-else
            class="om-empty"
            data-spec="obs-chart-empty"
          >
            分钟统计待接入
          </div>
        </div>
      </section>

      <section
        v-if="visible.cpu"
        class="om-card"
        data-spec="obs-chart"
        data-chart="cpu"
      >
        <div class="om-head">
          <span class="om-title">Node CPU · 集群平均</span>
          <span class="om-sub">无节点指标接口</span>
        </div>
        <div class="om-body">
          <EChart
            v-if="panels.cpu.length"
            :option="cpuOption"
            height="200px"
          />
          <div
            v-else
            class="om-empty"
            data-spec="obs-chart-empty"
          >
            节点 CPU 待接入
          </div>
        </div>
      </section>
    </div>
  </div>
</template>

<style scoped>
.om-wrap {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.om-bar {
  display: flex;
  align-items: center;
  gap: 8px;
}
.om-select-label {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
.om-select {
  height: 28px;
  padding: 0 8px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  background: var(--el-bg-color);
  color: var(--el-text-color-regular);
  font-size: 12px;
  outline: none;
}
.om-spacer {
  flex: 1;
}
.om-icon-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  background: var(--el-bg-color);
  color: var(--el-text-color-regular);
  cursor: pointer;
  transition: all 0.2s cubic-bezier(0.16, 1, 0.3, 1);
}
.om-icon-btn:hover {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
}
.om-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
}
.om-card {
  display: flex;
  flex-direction: column;
  border: 1px solid var(--el-border-color-light);
  border-radius: 12px;
  background: var(--el-bg-color-overlay);
  overflow: hidden;
}
.om-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  padding: 12px 16px 0;
}
.om-title {
  font-size: 13px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}
.om-sub {
  font-size: 11px;
  color: var(--el-text-color-secondary);
  font-variant-numeric: tabular-nums;
}
.om-body {
  padding: 8px 12px 12px;
}
.om-empty {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 200px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  border: 1px dashed var(--el-border-color-lighter);
  border-radius: 8px;
}
</style>
