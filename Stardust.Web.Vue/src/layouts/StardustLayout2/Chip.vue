<script setup lang="ts">
/**
 * Chip：筛选胶囊，对照原型 kit.css 的 .x-chip（26px 高、药丸形、12px/500）。
 * on 为选中态：文字取 primary-light-3，描边 45%、底 14%（原型 oklch(0.62 0.19 285) 的透明度），改用 color-mix。
 * 原生 button，点击事件由父级用 @click 接收。
 */
withDefaults(defineProps<{ on?: boolean }>(), { on: false });
</script>

<template>
  <button
    type="button"
    data-spec="chip"
    :aria-pressed="on"
    :class="[
      'inline-flex h-[26px] cursor-pointer items-center gap-[6px] whitespace-nowrap rounded-full border px-[11px] text-[12px] font-medium transition-colors',
      on
        ? 'border-[color-mix(in_oklab,var(--el-color-primary)_45%,transparent)] bg-[color-mix(in_oklab,var(--el-color-primary)_14%,transparent)] text-[var(--el-color-primary-light-3)]'
        : 'border-[var(--el-border-color-light)] bg-[var(--el-bg-color)] text-[var(--el-text-color-secondary)] hover:border-[var(--el-border-color)] hover:text-[var(--el-text-color-primary)]',
    ]"
  >
    <slot />
  </button>
</template>
