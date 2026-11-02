<script setup lang="ts">
/**
 * 空态 EmptyState —— A6 列表页统一件
 * ---------------------------------------------------------------------------
 * 设计真源：design-handoff README §错误/空/加载「文件树 dev 环境：折叠展示『暂无展开内容』」＋
 *          Primitives「空态文案」的统一化要求。
 *
 * 视觉：居中等宽留白（padding 40/24）· 图标块 44px 圆角 --radius-lg · 主标题 fg-2 ·
 *      提示 fg-3 · 可选操作按钮（品牌描边 ghost）。
 * 用法：<EmptyState title="暂无应用" hint="注册一个应用后会出现在这里" icon="…" />
 * 样式纪律：颜色全部来自设计令牌。
 */
withDefaults(
  defineProps<{
    /** 主文案 */
    title: string
    /** 辅助说明 */
    hint?: string
    /** Lucide path d，线性 1.5px 内联 SVG */
    icon?: string
  }>(),
  { hint: '', icon: '' },
)
</script>

<template>
  <div
    class="sd-empty"
    data-spec="empty-state"
  >
    <div
      v-if="icon"
      class="sd-empty__icon"
    >
      <svg
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="1.5"
        stroke-linecap="round"
        stroke-linejoin="round"
        aria-hidden="true"
      >
        <path :d="icon" />
      </svg>
    </div>
    <div class="sd-empty__title">
      {{ title }}
    </div>
    <div
      v-if="hint"
      class="sd-empty__hint"
    >
      {{ hint }}
    </div>
    <div
      v-if="$slots.action"
      class="sd-empty__action"
    >
      <slot name="action" />
    </div>
  </div>
</template>

<style scoped>
.sd-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  padding: 40px 24px;
  text-align: center;
}
.sd-empty__icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 44px;
  height: 44px;
  margin-bottom: var(--space-1);
  border-radius: var(--radius-lg);
  border: 1px solid var(--border-1);
  background: var(--surface-2);
  color: var(--fg-3);
}
.sd-empty__icon svg {
  width: 20px;
  height: 20px;
}
.sd-empty__title {
  font-size: 13.5px;
  font-weight: 500;
  color: var(--fg-2);
}
.sd-empty__hint {
  max-width: 420px;
  font-size: 12.5px;
  line-height: 1.6;
  color: var(--fg-3);
}
.sd-empty__action {
  margin-top: var(--space-2);
}
</style>
