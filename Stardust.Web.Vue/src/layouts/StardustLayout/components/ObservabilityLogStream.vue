<script setup lang="ts">
/**
 * 日志 Tab 日志流（设计稿 src/page-observability.jsx · LogsTab）
 * filter-bar（搜索 320 + 时间 select + error/warn/info 三 chip 带计数 + Live/Pause 分段）
 *  + 日志流 maxHeight 620。工程无统一日志查询 API → 空态「待接入」，结构与计数保留。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { request } from '@newlifex/cube-vue/core/utils/request'
import * as p from '../observabilityPanels.model'

const props = defineProps<{ appId?: number | null }>()

const rows = ref<Record<string, unknown>[]>([])
const keyword = ref('')
const range = ref('15m')
const live = ref(true)

const ranges = [
  { key: '15m', label: '过去 15 min' },
  { key: '1h', label: '过去 1 h' },
  { key: '6h', label: '过去 6 h' },
]

async function load() {
  const params: Record<string, unknown> = { PageSize: 200, Type: 'log' }
  if (props.appId) params.AppId = props.appId
  const r = await request({ url: '/Monitors/SampleData', method: 'GET', params }).catch(() => null)
  rows.value = (r?.data ?? []) as Record<string, unknown>[]
}

onMounted(() => void load())
watch(() => props.appId, () => void load())

const stream = computed(() => p.buildLogStream(rows.value, 40))
const counts = computed(() => stream.value.counts)
const filtered = computed(() => {
  const kw = keyword.value.trim().toLowerCase()
  return kw ? stream.value.lines.filter((l) => (l.lvl + l.msg).toLowerCase().includes(kw)) : stream.value.lines
})
</script>

<template>
  <div class="ol-wrap">
    <div
      class="ol-bar"
      data-spec="obs-log-bar"
    >
      <input
        v-model="keyword"
        class="ol-input"
        placeholder="查询日志: level:error AND app:payment-service"
        data-spec="obs-log-search"
      >
      <select
        v-model="range"
        class="ol-select"
        data-spec="obs-log-range"
      >
        <option
          v-for="r in ranges"
          :key="r.key"
          :value="r.key"
        >
          {{ r.label }}
        </option>
      </select>
      <span
        class="ol-chip is-active"
        data-spec="obs-log-chip"
        data-chip="error"
      >error <span class="ol-cnt">{{ p.compact(counts.error) }}</span></span>
      <span
        class="ol-chip"
        data-spec="obs-log-chip"
        data-chip="warn"
      >warn <span class="ol-cnt">{{ p.compact(counts.warn) }}</span></span>
      <span
        class="ol-chip"
        data-spec="obs-log-chip"
        data-chip="info"
      >info <span class="ol-cnt">{{ p.compact(counts.info) }}</span></span>
      <span class="ol-spacer" />
      <div
        class="ol-seg"
        data-spec="obs-log-seg"
      >
        <button
          class="ol-seg-btn"
          :class="{ 'is-active': live }"
          type="button"
          data-spec="obs-log-seg-live"
          @click="live = true"
        >
          Live
        </button>
        <button
          class="ol-seg-btn"
          :class="{ 'is-active': !live }"
          type="button"
          data-spec="obs-log-seg-pause"
          @click="live = false"
        >
          Pause
        </button>
      </div>
    </div>

    <section
      class="ol-panel"
      data-spec="obs-log-panel"
    >
      <div
        v-for="(l, i) in filtered"
        :key="i"
        class="ol-line"
        data-spec="obs-log-stream-line"
      >
        <span class="ol-ts">{{ l.ts }}</span>
        <span
          class="ol-lvl"
          :class="'is-' + l.lvl"
        >{{ l.lvl }}</span>
        <span class="ol-msg">{{ l.msg }}</span>
      </div>
      <div
        v-if="!filtered.length"
        class="ol-empty"
        data-spec="obs-log-empty"
      >
        日志流待接入（后端无统一日志查询接口）
      </div>
    </section>
  </div>
</template>

<style scoped>
.ol-wrap {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.ol-bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}
.ol-input {
  width: 320px;
  height: 28px;
  padding: 0 10px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  background: var(--el-bg-color);
  color: var(--el-text-color-regular);
  font-size: 12px;
  outline: none;
}
.ol-select {
  height: 28px;
  padding: 0 8px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  background: var(--el-bg-color);
  color: var(--el-text-color-regular);
  font-size: 12px;
  outline: none;
}
.ol-chip {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  height: 28px;
  padding: 0 10px;
  border: 1px solid var(--el-border-color);
  border-radius: 999px;
  background: var(--el-bg-color);
  color: var(--el-text-color-regular);
  font-size: 12px;
}
.ol-chip.is-active {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
}
.ol-cnt {
  font-size: 11px;
  color: var(--el-text-color-secondary);
  font-variant-numeric: tabular-nums;
}
.ol-spacer {
  flex: 1;
}
.ol-seg {
  display: inline-flex;
  padding: 2px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  background: var(--el-fill-color-light);
}
.ol-seg-btn {
  height: 22px;
  padding: 0 10px;
  border: 0;
  border-radius: 4px;
  background: transparent;
  color: var(--el-text-color-regular);
  font-size: 11px;
  cursor: pointer;
  transition: all 0.2s cubic-bezier(0.16, 1, 0.3, 1);
}
.ol-seg-btn.is-active {
  background: var(--el-bg-color-overlay);
  color: var(--el-color-primary);
}
.ol-panel {
  max-height: 620px;
  overflow-y: auto;
  border: 1px solid var(--el-border-color-light);
  border-radius: 12px;
  background: var(--el-bg-color-overlay);
}
.ol-line {
  display: flex;
  align-items: baseline;
  gap: 10px;
  padding: 5px 16px;
  font-size: 11px;
  font-family: 'JetBrains Mono', ui-monospace, monospace;
}
.ol-line:hover {
  background: var(--el-fill-color-light);
}
.ol-ts {
  flex: 0 0 auto;
  color: var(--el-text-color-secondary);
}
.ol-lvl {
  flex: 0 0 auto;
  padding: 1px 5px;
  border-radius: 4px;
  font-size: 10px;
  text-transform: uppercase;
}
.ol-lvl.is-error {
  color: var(--el-color-danger);
  background: var(--el-color-danger-light-9);
}
.ol-lvl.is-warn {
  color: var(--el-color-warning);
  background: var(--el-color-warning-light-9);
}
.ol-lvl.is-info {
  color: var(--el-color-info);
  background: var(--el-color-info-light-9);
}
.ol-msg {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--el-text-color-regular);
}
.ol-empty {
  padding: 40px 16px;
  text-align: center;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
</style>
