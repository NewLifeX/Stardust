<script setup lang="ts">
// 顶栏居中分段导航（纯 UI）。高亮项与计数由父级传入，点击只抛 go 事件，不碰路由。
import { NAV_ITEMS, type NavKey } from './nav.logic';

defineProps<{ active: NavKey | null; nowCount?: number }>();
const emit = defineEmits<{ go: [path: string] }>();
</script>

<template>
  <nav
    data-spec="nav-segment"
    class="absolute left-1/2 flex -translate-x-1/2 gap-[2px] rounded-[10px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color)] p-[3px] max-[1180px]:static max-[1180px]:translate-x-0"
  >
    <button
      v-for="item in NAV_ITEMS"
      :key="item.key"
      type="button"
      :data-spec="`nav-${item.key}`"
      :aria-current="active === item.key ? 'page' : undefined"
      class="flex h-[26px] items-center gap-[7px] whitespace-nowrap rounded-base px-[13px] text-[12.5px] font-medium transition-colors duration-[120ms] ease-[cubic-bezier(0.16,1,0.3,1)]"
      :class="active === item.key
        ? 'bg-[var(--el-fill-color-light)] text-[var(--el-text-color-primary)] shadow-1'
        : 'text-[var(--el-text-color-secondary)] hover:bg-[var(--el-bg-color-overlay)] hover:text-[var(--el-text-color-primary)]'"
      @click="emit('go', item.path)"
    >
      <span
        v-if="active === item.key"
        class="h-[5px] w-[5px] rounded-full bg-[var(--el-color-primary)] shadow-[0_0_6px_var(--el-color-primary)]"
      />
      {{ item.label }}
      <em
        v-if="item.key === 'now' && (nowCount ?? 0) > 0"
        class="rounded-full bg-[color-mix(in_oklab,var(--el-color-danger)_18%,transparent)] px-[5px] py-[2px] font-mono text-[10.5px] not-italic leading-none text-[var(--el-color-danger)] max-[900px]:hidden"
      >{{ nowCount }}</em>
    </button>
  </nav>
</template>
