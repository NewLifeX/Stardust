<script setup lang="ts">
/**
 * Tag：小徽章，对照原型 kit-ui.jsx 的 Tag 与 kit.css 的 .x-tag（191–202 行）。
 * 语气：default 灰、ok 成功、warn 警告、err 危险、brand 品牌；原型还有 info（青）与 fill（实心红），
 * 项目里没有对应的 --el-* 色，用到时再补，不提前加。
 * 着色统一用 color-mix：文字取语气色，描边 28%、底 10%（brand 为 32% 与 12%），与原型的 oklch 透明度一致。
 */
type TagTone = 'default' | 'ok' | 'warn' | 'err' | 'brand';

withDefaults(defineProps<{ tone?: TagTone }>(), { tone: 'default' });

const BASE =
  'inline-flex items-center gap-[5px] whitespace-nowrap rounded-[6px] border px-[7px] py-[2px] font-mono text-[10.5px] tracking-[0.02em]';

const TONE: Record<TagTone, string> = {
  default:
    'border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] text-[var(--el-text-color-secondary)]',
  ok: 'border-[color-mix(in_oklab,var(--el-color-success)_28%,transparent)] bg-[color-mix(in_oklab,var(--el-color-success)_10%,transparent)] text-[var(--el-color-success)]',
  warn: 'border-[color-mix(in_oklab,var(--el-color-warning)_28%,transparent)] bg-[color-mix(in_oklab,var(--el-color-warning)_10%,transparent)] text-[var(--el-color-warning)]',
  err: 'border-[color-mix(in_oklab,var(--el-color-danger)_28%,transparent)] bg-[color-mix(in_oklab,var(--el-color-danger)_10%,transparent)] text-[var(--el-color-danger)]',
  brand:
    'border-[color-mix(in_oklab,var(--el-color-primary)_32%,transparent)] bg-[color-mix(in_oklab,var(--el-color-primary)_12%,transparent)] text-[var(--el-color-primary-light-3)]',
};
</script>

<template>
  <span
    data-spec="tag"
    :class="[BASE, TONE[tone]]"
  >
    <slot />
  </span>
</template>
