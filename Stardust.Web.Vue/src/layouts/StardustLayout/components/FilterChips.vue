<script setup lang="ts">
/**
 * 筛选 chips FilterChips —— A6 列表页统一件
 * ---------------------------------------------------------------------------
 * 设计真源：design-handoff/ui_kits/console/Primitives.jsx 的 Badge/分段视觉 +
 *          README §Interactions「筛选 chips」＋ ConfigCenter 段切换器（3px 内衬 + 品牌底）
 *
 * 视觉：胶囊（radius-pill）· 未选中 surface-2/边框 border-1/fg-2；
 *      选中品牌底 #fff 文字（hover 提亮 6%）；可选计数徽标（mono/tabular-nums）。
 * 交互：单选（默认）或多选；支持「全部」兜底项；键盘可达（原生 button）。
 * 样式纪律：颜色全部来自设计令牌，无硬编码色值。
 */
import { computed } from 'vue'

interface ChipOption {
  value: string
  label: string
  count?: number
}

const props = withDefaults(
  defineProps<{
    /** 单选为 string，多选为 string[] */
    modelValue: string | string[]
    options: ChipOption[]
    multiple?: boolean
    /** 是否在头部插入「全部」项（单选常用） */
    allLabel?: string
  }>(),
  {
    multiple: false,
    allLabel: '',
  },
)

const emit = defineEmits<{ 'update:modelValue': [string | string[]] }>()

const selectedSet = computed<Set<string>>(() => {
  if (Array.isArray(props.modelValue)) return new Set(props.modelValue)
  return new Set([String(props.modelValue ?? '')])
})

function isActive(v: string) {
  return selectedSet.value.has(v)
}

function pick(v: string) {
  if (!props.multiple) {
    emit('update:modelValue', v)
    return
  }
  const next = new Set(selectedSet.value)
  if (v === '') next.clear()
  else if (next.has(v)) next.delete(v)
  else next.add(v)
  emit('update:modelValue', Array.from(next))
}
</script>

<template>
  <div
    class="sd-chips"
    data-spec="filter-chips"
    role="group"
  >
    <button
      v-if="allLabel"
      type="button"
      class="sd-chip"
      :class="{ 'is-active': selectedSet.size === 0 || (selectedSet.size === 1 && selectedSet.has('')) }"
      data-spec="chip"
      @click="pick('')"
    >
      {{ allLabel }}
    </button>
    <button
      v-for="o in options"
      :key="o.value"
      type="button"
      class="sd-chip"
      :class="{ 'is-active': isActive(o.value) }"
      data-spec="chip"
      :aria-pressed="isActive(o.value)"
      @click="pick(o.value)"
    >
      <span class="sd-chip__label">{{ o.label }}</span>
      <span
        v-if="o.count !== undefined"
        class="sd-chip__count"
      >{{ o.count }}</span>
    </button>
  </div>
</template>

<style scoped>
.sd-chips {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-2);
}
.sd-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 11px;
  border-radius: var(--radius-pill);
  border: 1px solid var(--border-1);
  background: var(--surface-2);
  color: var(--fg-2);
  font-family: var(--font-sans);
  font-size: 12.5px;
  font-weight: 500;
  line-height: 1.5;
  cursor: pointer;
  transition: background var(--dur-micro) var(--ease-out),
    border-color var(--dur-micro) var(--ease-out), color var(--dur-micro) var(--ease-out);
}
.sd-chip:hover {
  background: var(--surface-3);
  border-color: var(--border-2);
  color: var(--fg-1);
}
.sd-chip.is-active {
  background: var(--brand-500);
  border-color: var(--brand-500);
  color: var(--fg-1);
}
.sd-chip.is-active:hover {
  filter: brightness(1.06);
}
.sd-chip__count {
  font-family: var(--font-mono);
  font-size: 11px;
  font-variant-numeric: tabular-nums;
  color: var(--fg-3);
}
.sd-chip.is-active .sd-chip__count {
  color: color-mix(in srgb, var(--fg-1) 78%, transparent);
}
</style>
