<script setup lang="ts">
import { computed } from 'vue';
import { KICONS, type KIconName } from './icons';

const props = withDefaults(defineProps<{ name: KIconName; size?: number }>(), { size: 16 });
const body = computed(() => KICONS[props.name] ?? '');
</script>

<template>
  <!-- body 只来自 icons.ts 里的本地常量，不含外部输入 -->
  <!-- eslint-disable-next-line vue/no-v-html -->
  <svg
    class="inline-block shrink-0 align-middle"
    :width="size"
    :height="size"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="1.5"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
    v-html="body"
  />
</template>
