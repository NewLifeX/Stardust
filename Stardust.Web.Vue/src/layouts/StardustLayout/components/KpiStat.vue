<script setup lang="ts">
/**
 * KPI 指标卡 KpiStat
 * 设计真源：design-handoff/README §5 字阶（KPI 值 26–28 / 700）+ Primitives Card
 *   数值 28px/700 等宽数字 · 标签 12px fg-3 · 图标块 36px 圆角 8
 * 样式纪律：颜色一律取自设计令牌，禁止硬编码色值。
 */
import { computed } from 'vue'
import GlassCard from './GlassCard.vue'

type Accent = 'primary' | 'success' | 'warning' | 'danger'

const props = defineProps<{
  label: string
  value: string | number
  unit?: string
  accent?: Accent
  icon?: string
}>()

const valueColor = computed(() => {
  switch (props.accent) {
    case 'primary': return 'var(--el-color-primary)'
    case 'success': return 'var(--el-color-success)'
    case 'warning': return 'var(--el-color-warning)'
    case 'danger': return 'var(--el-color-danger)'
    default: return 'var(--fg-1)'
  }
})

const iconBg = computed(() => {
  switch (props.accent) {
    case 'primary': return 'var(--el-color-primary-light-9)'
    case 'success': return 'var(--el-color-success-light-9)'
    case 'warning': return 'var(--el-color-warning-light-9)'
    case 'danger': return 'var(--el-color-danger-light-9)'
    default: return 'var(--surface-2)'
  }
})
</script>

<template>
  <GlassCard class="flex items-center justify-between gap-3 px-4 py-3">
    <div class="flex min-w-0 flex-col justify-center gap-1">
      <div class="flex items-baseline gap-1">
        <span
          data-spec="kpi-value"
          class="font-bold leading-none tabular-nums"
          style="font-size: 28px"
          :style="{ color: valueColor }"
        >{{ value }}</span>
        <span
          v-if="unit"
          class="text-xs"
          style="color: var(--fg-3)"
        >{{ unit }}</span>
      </div>
      <span
        class="text-xs"
        style="color: var(--fg-3)"
      >{{ label }}</span>
    </div>
    <div
      v-if="icon"
      class="flex h-9 w-9 shrink-0 items-center justify-center rounded-[var(--radius-md)]"
      :style="{ backgroundColor: iconBg }"
    >
      <svg
        class="h-5 w-5"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="1.5"
        stroke-linecap="round"
        stroke-linejoin="round"
        :style="{ color: valueColor }"
      >
        <path :d="icon" />
      </svg>
    </div>
  </GlassCard>
</template>
