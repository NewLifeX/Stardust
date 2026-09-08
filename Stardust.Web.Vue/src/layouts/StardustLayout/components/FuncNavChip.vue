<script setup lang="ts">
defineProps<{
  icon?: string
  label: string
  to: string
}>()
</script>

<template>
  <router-link
    :to="to"
    class="group inline-flex items-center gap-2 rounded-xl bg-[var(--el-bg-color)] border border-[var(--el-border-color)] px-3 py-2 text-[var(--el-text-color-regular)] transition-colors hover:border-[var(--el-color-primary)] hover:text-[var(--el-text-color-primary)]"
  >
    <svg
      v-if="icon"
      class="w-4 h-4"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="2"
      stroke-linecap="round"
      stroke-linejoin="round"
      aria-hidden="true"
    >
      <path :d="icon" />
    </svg>
    <span class="text-sm">{{ label }}</span>
  </router-link>
</template>
