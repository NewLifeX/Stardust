<script setup lang="ts">
/**
 * 区块标题 SectionTitle
 * size=sm（默认，带左侧品牌条，用于概览/节点/应用卡片）；
 * size=md + divider（设置卡标题：15px/600 + 下分隔线，对照 design-spec 05）。
 */
withDefaults(defineProps<{
  title: string
  size?: 'sm' | 'md'
  divider?: boolean
}>(), {
  size: 'sm',
  divider: false,
})
</script>

<template>
  <div
    class="flex items-center gap-2"
    :class="divider ? 'mb-4 border-b border-[var(--el-border-color-light)] pb-2.5' : ''"
  >
    <span
      v-if="size === 'sm'"
      class="w-1 h-4 bg-[var(--el-color-primary)] rounded-full"
    />
    <h3
      class="text-[var(--el-text-color-primary)] font-semibold"
      :class="size === 'md' ? 'text-[15px]' : 'text-sm tracking-wider'"
      data-spec="card-title"
    >
      {{ title }}
    </h3>
  </div>
</template>
