<script setup lang="ts">
/**
 * 玻璃卡片 GlassCard
 * 样式纪律（cube-vue 技能红线）：颜色一律 --el-* 令牌，摆放用 Tailwind 工具类，禁止自定义 CSS 变量与硬编码色值。
 */
withDefaults(defineProps<{
  title?: string
  /** 玻璃卡片变体：default 标准玻璃；ghost 更通透，适合铺在大面积星云背景上 */
  variant?: 'default' | 'ghost'
}>(), {
  variant: 'default',
})
</script>

<template>
  <div
    data-spec="card"
    class="backdrop-blur border border-[var(--el-border-color)] rounded-xl p-4"
    :class="[
      variant === 'ghost'
        ? 'bg-[var(--el-bg-color)]/55'
        : 'bg-[var(--el-bg-color)]/80',
    ]"
  >
    <div
      v-if="title"
      class="flex items-center gap-2 mb-3"
    >
      <span class="w-1 h-4 bg-[var(--el-color-primary)] rounded-full" />
      <span class="text-sm tracking-wide text-[var(--el-text-color-primary)] font-medium">{{ title }}</span>
    </div>
    <slot />
  </div>
</template>
