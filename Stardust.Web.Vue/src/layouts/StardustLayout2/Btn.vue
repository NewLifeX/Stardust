<script setup lang="ts">
/**
 * Btn：按钮，对照原型 kit-ui.jsx 的 Btn 与 kit.css 的 .x-btn（161–177 行）。
 * 不用 ElButton：原型的尺寸与变体自成一套，直接按 --el-* 令牌写。
 * 纯 UI：disabled、onClick 等原生属性透传给 <button>。
 * 插槽：icon（图标，调用方传 KIcon 并自定尺寸）、默认（文字）。
 */
type BtnVariant = 'default' | 'primary' | 'danger' | 'ghost';
type BtnSize = 'md' | 'sm';

withDefaults(
  defineProps<{
    variant?: BtnVariant;
    size?: BtnSize;
  }>(),
  { variant: 'default', size: 'md' },
);

// 公共部分：居中、间距 6、不换行、1px 边框、字重 500；过渡只含背景、边框色、透明度（120ms，--ease-out）
// disabled 沿用原型：透明度 0.4 且不响应指针（所以禁用时没有悬浮态）
const BASE =
  'inline-flex items-center justify-center gap-[6px] whitespace-nowrap border font-medium transition-[background-color,border-color,opacity] duration-[120ms] ease-[cubic-bezier(0.16,1,0.3,1)] disabled:pointer-events-none disabled:opacity-40';

// 尺寸：md 高 32、内边距 13、13px、圆角 8；sm 高 26、内边距 9、12px、圆角 6（--el-border-radius-base）
const SIZE: Record<BtnSize, string> = {
  md: 'h-[32px] px-[13px] text-[13px] rounded-[8px]',
  sm: 'h-[26px] px-[9px] text-[12px] rounded-base',
};

// 变体：只有 default 有 :active 底色（surface-3）；其余三个在原型里被各自的底色规则盖过，所以不写
const VARIANT: Record<BtnVariant, string> = {
  default:
    'border-[var(--el-border-color)] bg-[var(--el-bg-color)] text-[var(--el-text-color-primary)] hover:border-[var(--el-border-color-dark)] hover:bg-[var(--el-bg-color-overlay)] active:bg-[var(--el-fill-color-light)]',
  primary:
    'border-transparent bg-[var(--el-color-primary)] text-[var(--el-color-white)] hover:bg-[var(--el-color-primary-light-3)]',
  danger:
    'border-[color-mix(in_oklab,var(--el-color-danger)_40%,transparent)] bg-transparent text-[var(--el-color-danger)] hover:bg-[color-mix(in_oklab,var(--el-color-danger)_12%,transparent)]',
  ghost:
    'border-transparent bg-transparent text-[var(--el-text-color-regular)] hover:bg-[var(--el-bg-color-overlay)] hover:text-[var(--el-text-color-primary)]',
};
</script>

<template>
  <button
    type="button"
    data-spec="btn"
    :class="[BASE, SIZE[size], VARIANT[variant]]"
  >
    <slot name="icon" />
    <slot />
  </button>
</template>