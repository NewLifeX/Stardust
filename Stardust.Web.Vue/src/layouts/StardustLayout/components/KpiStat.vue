<script setup lang="ts">
/**
 * KPI 指标卡 KpiStat（盒装卡片 + 大号彩色数值 + 图标 + 下方标签）
 * 样式纪律：颜色一律 --el-* 令牌，禁止自定义 CSS 变量与硬编码色值。
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
    default: return 'var(--el-text-color-primary)'
  }
})

const iconBg = computed(() => {
  switch (props.accent) {
    case 'primary': return 'var(--el-color-primary-light-9)'
    case 'success': return 'var(--el-color-success-light-9)'
    case 'warning': return 'var(--el-color-warning-light-9)'
    case 'danger': return 'var(--el-color-danger-light-9)'
    default: return 'var(--el-fill-color-light)'
  }
})
</script>

<template>
  <GlassCard class="flex items-center justify-between gap-3 px-4 py-3 transition-shadow hover:shadow-[var(--el-box-shadow)]">
    <div class="flex min-w-0 flex-col justify-center gap-1">
      <div class="flex items-baseline gap-1">
        <span
          data-spec="kpi-value"
          class="text-[30px] font-bold tabular-nums leading-none"
          :style="{ color: valueColor }"
        >{{ value }}</span>
        <span
          v-if="unit"
          class="text-xs text-[var(--el-text-color-regular)]"
        >{{ unit }}</span>
      </div>
      <span class="text-xs text-[var(--el-text-color-regular)]">{{ label }}</span>
    </div>
    <div
      v-if="icon"
      class="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg"
      :style="{ backgroundColor: iconBg }"
    >
      <svg
        class="h-5 w-5"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="1.7"
        stroke-linecap="round"
        stroke-linejoin="round"
        :style="{ color: valueColor }"
      >
        <path :d="icon" />
      </svg>
    </div>
  </GlassCard>
</template>
