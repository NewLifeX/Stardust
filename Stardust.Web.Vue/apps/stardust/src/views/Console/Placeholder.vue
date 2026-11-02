<script setup lang="ts">
// M1 占位页：六条新路由共用，M3 起逐页替换为真实页面。
import { computed } from 'vue';
import { useRoute } from 'vue-router';

const route = useRoute();
const title = computed(() => String(route.meta?.title ?? ''));
</script>

<template>
  <section
    class="flex flex-col gap-2 py-6"
    data-spec="console-placeholder"
  >
    <span class="font-mono text-[11px] uppercase tracking-[0.08em] text-[var(--el-text-color-secondary)]">{{ route.path }}</span>
    <h1 class="m-0 text-[22px] font-semibold text-[var(--el-text-color-primary)]">
      {{ title }}
    </h1>
    <p class="m-0 text-[13px] text-[var(--el-text-color-secondary)]">
      这一页正在按原型重构，当前是占位内容。
    </p>
  </section>
</template>
