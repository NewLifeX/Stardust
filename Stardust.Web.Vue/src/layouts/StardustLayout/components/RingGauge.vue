<script setup lang="ts">
/**
 * 环形仪表（设计稿 src/page-nodes.jsx · RingGauge）
 * 用于节点 CPU / 内存 / 磁盘占用。无数据时显示「—」并退化为描边色。
 * 颜色只用 --el-* 令牌；tone='auto' 时按阈值自动着色（<60 正常 / <80 告警 / ≥80 异常）。
 */
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{
    /** 0-100；null 表示无数据 */
    value: number | null
    size?: number
    tone?: 'auto' | 'ok' | 'warn' | 'bad'
    label?: string
  }>(),
  { size: 76, tone: 'auto', label: '' },
)

const pct = computed(() => {
  if (props.value == null || !Number.isFinite(props.value)) return 0
  return Math.max(0, Math.min(100, props.value))
})

const radius = computed(() => (props.size - 8) / 2)
const circumference = computed(() => 2 * Math.PI * radius.value)
const dash = computed(() => (pct.value / 100) * circumference.value)

const tone = computed(() => {
  if (props.value == null || !Number.isFinite(props.value)) return 'none'
  if (props.tone !== 'auto') return props.tone
  if (props.value >= 80) return 'bad'
  if (props.value >= 60) return 'warn'
  return 'ok'
})

const display = computed(() =>
  props.value == null || !Number.isFinite(props.value) ? '—' : Math.round(props.value) + '%',
)
</script>

<template>
  <div
    class="sd-ring"
    :class="'is-' + tone"
    :style="{ width: size + 'px', height: size + 'px' }"
    :title="label || undefined"
  >
    <svg
      :viewBox="'0 0 ' + size + ' ' + size"
      :width="size"
      :height="size"
      aria-hidden="true"
    >
      <circle
        class="sd-ring-track"
        :cx="size / 2"
        :cy="size / 2"
        :r="radius"
        fill="none"
        stroke-width="6"
      />
      <circle
        class="sd-ring-value"
        :cx="size / 2"
        :cy="size / 2"
        :r="radius"
        fill="none"
        stroke-width="6"
        stroke-linecap="round"
        :stroke-dasharray="dash + ' ' + circumference"
        :transform="'rotate(-90 ' + size / 2 + ' ' + size / 2 + ')'"
      />
    </svg>
    <span class="sd-ring-text">{{ display }}</span>
  </div>
</template>

<style scoped>
.sd-ring {
  position: relative;
  flex-shrink: 0;
}
.sd-ring svg {
  display: block;
}
.sd-ring-track {
  stroke: var(--el-fill-color);
}
.sd-ring-value {
  stroke: var(--el-color-primary);
  transition: stroke-dasharray 320ms cubic-bezier(0.16, 1, 0.3, 1);
}
.sd-ring.is-ok .sd-ring-value {
  stroke: var(--el-color-success);
}
.sd-ring.is-warn .sd-ring-value {
  stroke: var(--el-color-warning);
}
.sd-ring.is-bad .sd-ring-value {
  stroke: var(--el-color-danger);
}
.sd-ring.is-none .sd-ring-value {
  stroke: var(--el-border-color);
}
.sd-ring-text {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
  font-size: 13px;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
  color: var(--el-text-color-primary);
}
</style>
