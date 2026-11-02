<script setup lang="ts">
/**
 * Card：卡片容器，对照原型 kit-ui.jsx 的 Card 与 kit.css 的 .x-card（179–189 行）。
 * padding='pad' 为 16px 18px（默认），'none' 为 0 且裁掉溢出（原型 .nopad，用于贴边表格）。
 * 与原型的已知差异：原型 --elev-1 是一条 4% 白色内高光，项目规则禁自定义 token，这里不加阴影（记入 TODO2.md）。
 * 插槽：默认（正文）。卡片标题 h3 由调用方按原型写（13px、600、flex、gap 8）。
 */
type CardPadding = 'pad' | 'none';

withDefaults(defineProps<{ padding?: CardPadding }>(), { padding: 'pad' });

const PADDING: Record<CardPadding, string> = {
  pad: 'px-[18px] py-4',
  none: 'overflow-hidden p-0',
};
</script>

<template>
  <div
    data-spec="card"
    class="rounded-[10px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color)]"
    :class="PADDING[padding]"
  >
    <slot />
  </div>
</template>
