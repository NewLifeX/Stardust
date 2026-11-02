<script setup lang="ts">
/**
 * Dot：状态点，对照原型 kit.css 的 .x-dot（7px 圆）。
 * 语气：ok 绿且带光晕、err 红且带光晕、warn 黄、off 空心圈、默认实心灰。
 * 原型的光晕用 oklch 字面色，这里改成 color-mix 取 --el-color-* 令牌，透明度与原型一致（ok 60%、err 70%）。
 */
type DotTone = 'default' | 'ok' | 'err' | 'warn' | 'off';

withDefaults(defineProps<{ tone?: DotTone }>(), { tone: 'default' });

const TONE: Record<DotTone, string> = {
  default: 'bg-[var(--el-text-color-placeholder)]',
  ok: 'bg-[var(--el-color-success)] shadow-[0_0_6px_color-mix(in_oklab,var(--el-color-success)_60%,transparent)]',
  err: 'bg-[var(--el-color-danger)] shadow-[0_0_6px_color-mix(in_oklab,var(--el-color-danger)_70%,transparent)]',
  warn: 'bg-[var(--el-color-warning)]',
  off: 'border-[1.5px] border-solid border-[var(--el-text-color-placeholder)] bg-transparent',
};
</script>

<template>
  <i
    data-spec="dot"
    :data-tone="tone"
    :class="['inline-block h-[7px] w-[7px] shrink-0 rounded-full', TONE[tone]]"
  />
</template>
