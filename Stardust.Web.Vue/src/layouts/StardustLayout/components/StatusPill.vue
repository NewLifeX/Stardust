<script setup lang="ts">
/**
 * 状态徽标 StatusPill（在线/告警/离线 三态）
 * 样式纪律：颜色一律 --el-* 语义令牌，禁止自定义 CSS 变量与硬编码色值。
 */
import { computed } from 'vue'

type Status = 'online' | 'warn' | 'offline' | '在线' | '告警' | '离线'

const props = defineProps<{
  status: Status
}>()

const token = computed(() => {
  switch (props.status) {
    case 'online':
    case '在线':
      return 'var(--el-color-success)'
    case 'warn':
    case '告警':
      return 'var(--el-color-warning)'
    case 'offline':
    case '离线':
      return 'var(--el-color-danger)'
    default:
      return 'var(--el-text-color-regular)'
  }
})

const defaultText: Record<Status, string> = {
  online: '在线',
  warn: '告警',
  offline: '离线',
  '在线': '在线',
  '告警': '告警',
  '离线': '离线',
}
</script>

<template>
  <span
    class="inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-xs font-medium"
    :style="{ color: token, backgroundColor: `color-mix(in srgb, ${token} 15%, transparent)` }"
  >
    <span
      class="w-1.5 h-1.5 rounded-full"
      :style="{ backgroundColor: token }"
    />
    <slot>{{ defaultText[status] }}</slot>
  </span>
</template>
