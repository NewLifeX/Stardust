<script setup lang="ts">
/**
 * 玻璃卡片 GlassCard
 * 设计真源：design-handoff/ui_kits/console/Primitives.jsx 的 Card
 *   background surface-1 · border border-1 · radius-lg(10) · padding 16 · elev-1
 * variant:
 *   default 标准玻璃；ghost 更透明，适合铺在大面积星云背景上
 * 样式纪律：颜色与圆角一律取自设计令牌，禁止自定义变量与硬编码色值。
 */
withDefaults(defineProps<{
  title?: string
  variant?: 'default' | 'ghost'
}>(), {
  variant: 'default',
})
</script>

<template>
  <div
    data-spec="card"
    class="backdrop-blur rounded-[var(--radius-lg)] border p-4"
    :style="{
      borderColor: 'var(--border-1)',
      background: variant === 'ghost'
        ? 'color-mix(in srgb, var(--surface-1) 55%, transparent)'
        : 'color-mix(in srgb, var(--surface-1) 80%, transparent)',
      boxShadow: 'var(--elev-1)',
    }"
  >
    <div
      v-if="title"
      class="mb-3 flex items-center gap-2"
    >
      <span
        class="h-4 w-1 rounded-full"
        style="background: var(--brand-500)"
      />
      <span
        class="text-[13px] font-semibold tracking-wide"
        style="color: var(--fg-1)"
      >{{ title }}</span>
    </div>
    <slot />
  </div>
</template>
