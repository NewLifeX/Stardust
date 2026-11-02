<script setup lang="ts">
/**
 * Traces Tab 主从并排（设计稿 src/page-observability.jsx · TracesTab）
 * filter-bar（搜索 260 + 服务 select + 仅异常/仅慢查询 chip + 「N traces / 1h」）
 *  + two-col g-2-1：左 6 列表格（选中行 active）+ 右 sticky（waterfall grid 180/60/1fr/60 + 关联日志 240）。
 * 数据源 /Monitors/TraceItem + /Monitors/SampleData，fail-soft。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { request } from '@newlifex/cube-vue/core/utils/request'
import EmptyState from '@/layouts/StardustLayout/components/EmptyState.vue'
import StatusPill from '@/layouts/StardustLayout/components/StatusPill.vue'
import * as p from '../observabilityPanels.model'

const props = defineProps<{ appId?: number | null }>()

const items = ref<Record<string, unknown>[]>([])
const spans = ref<Record<string, unknown>[]>([])
const logRows = ref<Record<string, unknown>[]>([])
const selected = ref('')
const keyword = ref('')
const onlyError = ref(false)
const onlySlow = ref(false)

async function loadDetail() {
  const id = selected.value
  if (!id) {
    spans.value = []
    logRows.value = []
    return
  }
  const s = await request({ url: '/Monitors/SampleData', method: 'GET', params: { TraceId: id, PageSize: 200 } }).catch(() => null)
  spans.value = (s?.data ?? []) as Record<string, unknown>[]
  const l = await request({ url: '/Monitors/SampleData', method: 'GET', params: { TraceId: id, Type: 'log', PageSize: 100 } }).catch(() => null)
  logRows.value = (l?.data ?? []) as Record<string, unknown>[]
}

async function load() {
  const params: Record<string, unknown> = { PageSize: 100 }
  if (props.appId) params.AppId = props.appId
  const r = await request({ url: '/Monitors/TraceItem', method: 'GET', params }).catch(() => null)
  items.value = (r?.data ?? []) as Record<string, unknown>[]
  if (items.value.length && !selected.value) selected.value = p.toTraceRows(items.value)[0]?.id ?? ''
  await loadDetail()
}

onMounted(() => void load())
watch(() => props.appId, () => void load())

const allRows = computed(() => p.toTraceRows(items.value))
const rows = computed(() => {
  let list = allRows.value
  const kw = keyword.value.trim().toLowerCase()
  if (kw) list = list.filter((r) => (r.id + r.endpoint + r.svc).toLowerCase().includes(kw))
  if (onlyError.value) list = list.filter((r) => r.status === 'error')
  if (onlySlow.value) list = list.filter((r) => r.dur >= 200)
  return list
})
const current = computed(() => allRows.value.find((r) => r.id === selected.value) ?? null)
const waterfall = computed(() => p.buildWaterfallRows(spans.value))
const logs = computed(() => p.toLogLines(logRows.value))

function pick(id: string) {
  if (id === selected.value) return
  selected.value = id
  void loadDetail()
}
</script>

<template>
  <div class="ot-wrap">
    <div
      class="ot-bar"
      data-spec="obs-trace-bar"
    >
      <input
        v-model="keyword"
        class="ot-input"
        placeholder="Trace ID / 端点"
        data-spec="obs-trace-search"
      >
      <select
        class="ot-select"
        data-spec="obs-trace-svc"
      >
        <option>服务: 全部</option>
      </select>
      <button
        class="ot-chip"
        :class="{ 'is-active': onlyError }"
        type="button"
        data-spec="obs-trace-chip"
        data-chip="error"
        @click="onlyError = !onlyError"
      >
        仅异常
      </button>
      <button
        class="ot-chip"
        :class="{ 'is-active': onlySlow }"
        type="button"
        data-spec="obs-trace-chip"
        data-chip="slow"
        @click="onlySlow = !onlySlow"
      >
        仅慢查询
      </button>
      <span class="ot-spacer" />
      <span
        class="ot-count"
        data-spec="obs-trace-count"
      >{{ rows.length }} traces / 1h</span>
    </div>

    <div
      class="ot-split"
      data-spec="obs-trace-split"
    >
      <section class="ot-panel">
        <table
          class="ot-table"
          data-spec="obs-trace-table"
        >
          <thead>
            <tr>
              <th>Trace ID</th>
              <th>端点</th>
              <th>服务</th>
              <th>状态</th>
              <th class="ot-num">
                耗时
              </th>
              <th>时间</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="r in rows"
              :key="r.id"
              class="ot-row"
              :class="{ 'is-active': r.id === selected }"
              data-spec="obs-trace-row"
              @click="pick(r.id)"
            >
              <td class="ot-mono ot-link">
                {{ r.id }}
              </td>
              <td class="ot-mono ot-xs">
                {{ r.endpoint }}
              </td>
              <td class="ot-dim">
                {{ r.svc }}
              </td>
              <td><StatusPill :status="r.status" /></td>
              <td
                class="ot-num ot-mono"
                :class="r.status === 'error' ? 'is-err' : r.status === 'warn' ? 'is-warn' : ''"
              >
                {{ r.dur }} ms
              </td>
              <td class="ot-dim ot-mono ot-xs">
                {{ r.at }}
              </td>
            </tr>
          </tbody>
        </table>
        <div
          v-if="!rows.length"
          class="ot-empty"
          data-spec="obs-trace-empty"
        >
          <EmptyState title="无 Trace 数据" />
        </div>
      </section>

      <aside class="ot-right">
        <section
          class="ot-panel"
          data-spec="obs-trace-detail"
        >
          <div class="ot-head">
            <span class="ot-title">Trace {{ current ? current.id : '—' }}</span>
            <span
              class="ot-sub"
              data-spec="obs-trace-detail-sub"
            >{{ current ? current.endpoint + ' · ' + current.dur + ' ms · ' + waterfall.rows.length + ' spans' : '未选中' }}</span>
          </div>
          <div
            v-if="waterfall.rows.length"
            class="ot-wf"
            data-spec="obs-waterfall"
          >
            <div class="ot-axis">
              <span>0 ms</span>
              <span class="ot-spacer" />
              <span>{{ waterfall.total }} ms</span>
            </div>
            <div
              v-for="(s, i) in waterfall.rows"
              :key="i"
              class="ot-wf-row"
              data-spec="obs-wf-row"
            >
              <div class="ot-wf-svc">
                <span class="ot-depth">{{ '│ '.repeat(s.depth) }}</span>
                <span class="ot-mono ot-xs">{{ s.svc }}</span>
              </div>
              <div class="ot-dim ot-mono ot-xs">
                {{ s.op.split('.')[0] }}
              </div>
              <div class="ot-wf-track">
                <div
                  class="ot-wf-bar"
                  :class="{ 'is-err': s.err }"
                  data-spec="obs-wf-bar"
                  :style="{ left: s.leftPct + '%', width: s.widthPct + '%' }"
                />
              </div>
              <div class="ot-wf-ms ot-mono">
                {{ s.ms }}ms
              </div>
            </div>
          </div>
          <div
            v-else
            class="ot-empty-sm"
            data-spec="obs-waterfall-empty"
          >
            Span 数据待接入
          </div>
        </section>

        <section
          class="ot-panel"
          data-spec="obs-trace-logs"
        >
          <div class="ot-head">
            <span class="ot-title">关联日志</span>
            <span class="ot-sub">{{ logs.length }} 条</span>
          </div>
          <div class="ot-logs">
            <div
              v-for="(l, i) in logs"
              :key="i"
              class="ot-log-line"
              data-spec="obs-log-line"
            >
              <span class="ot-log-ts">{{ l.ts }}</span>
              <span
                class="ot-log-lvl"
                :class="'is-' + l.lvl"
              >{{ l.lvl }}</span>
              <span class="ot-log-msg">{{ l.msg }}</span>
            </div>
            <div
              v-if="!logs.length"
              class="ot-empty-sm"
              data-spec="obs-trace-logs-empty"
            >
              关联日志待接入
            </div>
          </div>
        </section>
      </aside>
    </div>
  </div>
</template>

<style scoped>
.ot-wrap {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.ot-bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}
.ot-input {
  width: 260px;
  height: 28px;
  padding: 0 10px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  background: var(--el-bg-color);
  color: var(--el-text-color-regular);
  font-size: 12px;
  outline: none;
}
.ot-select {
  height: 28px;
  padding: 0 8px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  background: var(--el-bg-color);
  color: var(--el-text-color-regular);
  font-size: 12px;
  outline: none;
}
.ot-chip {
  height: 28px;
  padding: 0 10px;
  border: 1px solid var(--el-border-color);
  border-radius: 999px;
  background: var(--el-bg-color);
  color: var(--el-text-color-regular);
  font-size: 12px;
  cursor: pointer;
  transition: all 0.2s cubic-bezier(0.16, 1, 0.3, 1);
}
.ot-chip.is-active {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
}
.ot-spacer {
  flex: 1;
}
.ot-count {
  font-size: 11px;
  color: var(--el-text-color-secondary);
  font-variant-numeric: tabular-nums;
}
.ot-split {
  display: grid;
  grid-template-columns: 2fr 1fr;
  gap: 16px;
  align-items: flex-start;
}
.ot-panel {
  border: 1px solid var(--el-border-color-light);
  border-radius: 12px;
  background: var(--el-bg-color-overlay);
  overflow: hidden;
}
.ot-right {
  position: sticky;
  top: 0;
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.ot-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
}
.ot-table th {
  padding: 8px 12px;
  text-align: left;
  font-weight: 500;
  font-size: 11px;
  color: var(--el-text-color-secondary);
  border-bottom: 1px solid var(--el-border-color-lighter);
}
.ot-row {
  cursor: pointer;
  transition: background-color 0.15s cubic-bezier(0.16, 1, 0.3, 1);
}
.ot-row:hover {
  background: var(--el-fill-color-light);
}
.ot-row.is-active {
  background: var(--el-color-primary-light-9);
}
.ot-row td {
  padding: 7px 12px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  color: var(--el-text-color-regular);
}
.ot-num {
  text-align: right;
  font-variant-numeric: tabular-nums;
}
.ot-mono {
  font-family: 'JetBrains Mono', ui-monospace, monospace;
}
.ot-link {
  color: var(--el-color-primary);
}
.ot-dim {
  color: var(--el-text-color-secondary);
}
.ot-xs {
  font-size: 11px;
}
.is-err {
  color: var(--el-color-danger);
}
.is-warn {
  color: var(--el-color-warning);
}
.ot-empty {
  padding: 24px;
}
.ot-empty-sm {
  padding: 16px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
.ot-head {
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 12px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}
.ot-title {
  font-size: 13px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}
.ot-sub {
  font-size: 11px;
  color: var(--el-text-color-secondary);
}
.ot-wf {
  padding: 12px;
}
.ot-axis {
  display: flex;
  align-items: center;
  margin-bottom: 8px;
  font-size: 11px;
  color: var(--el-text-color-secondary);
  font-variant-numeric: tabular-nums;
}
.ot-wf-row {
  display: grid;
  grid-template-columns: 180px 60px 1fr 60px;
  align-items: center;
  gap: 6px;
  height: 22px;
}
.ot-wf-svc {
  display: flex;
  align-items: center;
  gap: 2px;
  overflow: hidden;
  white-space: nowrap;
  color: var(--el-text-color-regular);
}
.ot-depth {
  color: var(--el-border-color);
  font-family: 'JetBrains Mono', ui-monospace, monospace;
  font-size: 11px;
}
.ot-wf-track {
  position: relative;
  height: 8px;
  border-radius: 4px;
  background: var(--el-fill-color-light);
}
.ot-wf-bar {
  position: absolute;
  top: 0;
  height: 8px;
  border-radius: 4px;
  background: var(--el-color-primary);
}
.ot-wf-bar.is-err {
  background: var(--el-color-danger);
}
.ot-wf-ms {
  text-align: right;
  font-size: 11px;
  color: var(--el-text-color-secondary);
}
.ot-logs {
  max-height: 240px;
  overflow-y: auto;
}
.ot-log-line {
  display: flex;
  align-items: baseline;
  gap: 8px;
  padding: 4px 16px;
  font-size: 11px;
  font-family: 'JetBrains Mono', ui-monospace, monospace;
}
.ot-log-line:hover {
  background: var(--el-fill-color-light);
}
.ot-log-ts {
  flex: 0 0 auto;
  color: var(--el-text-color-secondary);
}
.ot-log-lvl {
  flex: 0 0 auto;
  padding: 1px 5px;
  border-radius: 4px;
  font-size: 10px;
  text-transform: uppercase;
}
.ot-log-lvl.is-error {
  color: var(--el-color-danger);
  background: var(--el-color-danger-light-9);
}
.ot-log-lvl.is-warn {
  color: var(--el-color-warning);
  background: var(--el-color-warning-light-9);
}
.ot-log-lvl.is-info {
  color: var(--el-color-info);
  background: var(--el-color-info-light-9);
}
.ot-log-msg {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--el-text-color-regular);
}
</style>
