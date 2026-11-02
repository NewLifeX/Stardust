<script setup lang="ts">
/**
 * PageHead：页头，对照原型 kit-ui.jsx 的 PageHead 与 kit.css 的 .x-ph / .x-eyebrow（146–153、 .x-ph 行）。
 * 左侧：eyebrow（mono 11px、字距 0.08em、大写）、h1（26px、600、字距 -0.02em）、sub（12.5px）；右侧 actions 插槽。
 * 插槽：crumb（面包屑，放在页头上方）、actions（右端操作区）。
 */
defineProps<{
  eyebrow?: string;
  title: string;
  sub?: string;
}>();
</script>

<template>
  <div data-spec="page-head">
    <slot name="crumb" />
    <div class="mb-5 flex flex-wrap items-end gap-[14px]">
      <div class="min-w-0">
        <div
          v-if="eyebrow"
          class="font-mono text-[11px] uppercase tracking-[0.08em] text-[var(--el-text-color-secondary)]"
        >
          {{ eyebrow }}
        </div>
        <h1 class="mb-0 mt-[2px] text-[26px] font-semibold tracking-[-0.02em] text-[var(--el-text-color-primary)]">
          {{ title }}
        </h1>
        <div
          v-if="sub"
          class="mt-[3px] text-[12.5px] text-[var(--el-text-color-secondary)]"
        >
          {{ sub }}
        </div>
      </div>
      <span class="flex-1" />
      <slot name="actions" />
    </div>
  </div>
</template>
