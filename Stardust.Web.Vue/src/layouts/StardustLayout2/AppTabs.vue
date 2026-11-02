<script setup lang="ts">
/**
 * AppTabs：应用工作台页签条，对照原型 kit.css 的 .x-tabs（422–433 行）。
 * 条：flex、间距 2px、下边 1px 分割线、外边距 16px 0 20px、可换行。
 * 页签：高 34、左右 13、字号 13、字重 500；激活态品牌色字 + 2px 品牌色下边 + 10% 品牌色底。
 * 纯 UI：不读路由，页签文案由调用方生成，点击只发 select(key)，页签切换不换页面。
 */
import type { AppTab } from './palette.items';

defineProps<{
  items: ReadonlyArray<{ key: AppTab; label: string }>;
  active: AppTab;
}>();
const emit = defineEmits<{ select: [key: AppTab] }>();
</script>

<template>
  <div
    role="tablist"
    data-spec="app-tabs"
    class="mb-5 mt-4 flex flex-wrap gap-[2px] border-b border-[var(--el-border-color-light)]"
  >
    <button
      v-for="t in items"
      :key="t.key"
      type="button"
      role="tab"
      data-spec="app-tab"
      :data-key="t.key"
      :aria-selected="t.key === active"
      :class="[
        '-mb-px inline-flex h-[34px] cursor-pointer items-center gap-[6px] rounded-t-[6px] border-0 border-b-2 border-solid px-[13px] text-[13px] font-medium outline-none transition-colors focus-visible:bg-[var(--el-fill-color-light)]',
        t.key === active
          ? 'border-b-[var(--el-color-primary)] bg-[color-mix(in_oklab,var(--el-color-primary)_10%,transparent)] text-[var(--el-color-primary-light-3)]'
          : 'border-b-transparent bg-transparent text-[var(--el-text-color-secondary)] hover:bg-[var(--el-fill-color-light)] hover:text-[var(--el-text-color-primary)]',
      ]"
      @click="emit('select', t.key)"
    >
      {{ t.label }}
    </button>
  </div>
</template>
