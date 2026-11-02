<script setup lang="ts">
/**
 * 自动刷新脉冲点 RefreshDot —— A6 列表页统一件
 * ---------------------------------------------------------------------------
 * 设计真源：README §Interactions「自动刷新脉冲点」＋ LoginPage ticker 的绿色呼吸点
 *          （6px 圆点 + 同色辉光）。
 *
 * 视觉：6px 圆点 + 8px 辉光，1.6s 呼吸；文字 11px mono/tabular。
 * 无障碍：prefers-reduced-motion 下停止动画（保留静态点）。
 * 样式纪律：颜色取自设计令牌（--success-500 / --cyan-500）。
 */
withDefaults(
  defineProps<{
    /** 是否正在刷新（刷新中转为品牌/青色） */
    refreshing?: boolean
    /** 展示文案，例如「自动刷新 · 15s」 */
    label?: string
  }>(),
  { refreshing: false, label: '自动刷新' },
)
</script>

<template>
  <span
    class="sd-refresh"
    data-spec="refresh-dot"
    :class="{ 'is-refreshing': refreshing }"
  >
    <span class="sd-refresh__dot" />
    <span
      v-if="label"
      class="sd-refresh__label"
    >{{ label }}</span>
  </span>
</template>

<style scoped>
.sd-refresh {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-family: var(--font-mono);
  font-size: 11px;
  font-variant-numeric: tabular-nums;
  color: var(--fg-3);
}
.sd-refresh__dot {
  width: 6px;
  height: 6px;
  border-radius: var(--radius-pill);
  background: var(--success-500);
  box-shadow: 0 0 8px var(--success-500);
  animation: sdRefreshPulse 1600ms var(--ease-in-out) infinite;
}
.sd-refresh.is-refreshing .sd-refresh__dot {
  background: var(--cyan-500);
  box-shadow: 0 0 8px var(--cyan-500);
}
@keyframes sdRefreshPulse {
  0%,
  100% {
    opacity: 1;
    transform: scale(1);
  }
  50% {
    opacity: 0.45;
    transform: scale(0.82);
  }
}
@media (prefers-reduced-motion: reduce) {
  .sd-refresh__dot {
    animation: none;
  }
}
</style>
