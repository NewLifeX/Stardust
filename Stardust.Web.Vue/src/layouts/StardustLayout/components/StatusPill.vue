<script setup lang="ts">
/**
 * 状态徽标 StatusPill —— A6 统一版
 * ---------------------------------------------------------------------------
 * 设计真源：design-handoff/ui_kits/console/Primitives.jsx 的 Badge
 *   padding 3px 9px · radius 999 · 11.5px / 500 · 底色同色 15% · 描边同色 30% · 圆点 6px
 *
 * 状态语义（三态为主，向后兼容七态旧值）：
 *   ok     运行 / 在线 / 健康
 *   warn   告警 / 降级 / 部署中 / 维护
 *   danger 异常 / 离线 / 发布失败
 *   info   中性信息 / 未知
 * 兼容映射：running/online/运行/在线 → ok；warn/告警/alarm → warn；
 *          error/offline/发布失败 → danger；maintenance/deploying/部署中/unknown → info
 *
 * 样式纪律：颜色只取设计令牌（--success-500 / --warning-500 / --danger-500 / --cyan-500），
 *          不硬编码色值；同色派生用 color-mix。
 */
import { computed } from 'vue'

type Tone = 'ok' | 'warn' | 'danger' | 'info'

const props = defineProps<{
  /** 业务状态值（新旧写法都接受） */
  status: string
  /** 直接指定色相，优先级高于 status 映射 */
  tone?: Tone
  /** 是否显示前置圆点（默认显示，与设计一致） */
  dot?: boolean
}>()

/** 业务状态 → 三态色相 */
const TONE_MAP: Record<string, Tone> = {
  // ok
  running: 'ok', online: 'ok', healthy: 'ok', success: 'ok', ok: 'ok',
  运行: 'ok', 在线: 'ok', 正常: 'ok', 健康: 'ok', 已发布: 'ok', 成功: 'ok',
  // warn
  warn: 'warn', warning: 'warn', alarm: 'warn', degraded: 'warn',
  告警: 'warn', 警告: 'warn', 降级: 'warn',
  // danger
  error: 'danger', offline: 'danger', fail: 'danger', failed: 'danger', publishFailed: 'danger',
  异常: 'danger', 离线: 'danger', 发布失败: 'danger', 失败: 'danger',
  // info
  maintenance: 'info', deploying: 'info', unknown: 'info', pending: 'info', disabled: 'info',
  维护: 'info', 部署中: 'info', 未知: 'info', 待处理: 'info', 已禁用: 'info',
}

const tone = computed<Tone>(() => {
  if (props.tone) return props.tone
  return TONE_MAP[String(props.status)] ?? 'info'
})

/** 三态色相 → 设计令牌 */
const TONE_TOKEN: Record<Tone, string> = {
  ok: 'var(--success-500)',
  warn: 'var(--warning-500)',
  danger: 'var(--danger-500)',
  info: 'var(--cyan-500)',
}

const token = computed(() => TONE_TOKEN[tone.value])
</script>

<template>
  <span
    data-spec="status-pill"
    class="sd-pill"
    :style="{
      color: token,
      background: `color-mix(in srgb, ${token} 15%, transparent)`,
      borderColor: `color-mix(in srgb, ${token} 30%, transparent)`,
    }"
  >
    <span
      v-if="dot !== false"
      class="sd-pill__dot"
      :style="{ background: token }"
    />
    <slot>{{ status }}</slot>
  </span>
</template>

<style scoped>
.sd-pill {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 3px 9px;
  border-radius: var(--radius-pill);
  border: 1px solid transparent;
  font-size: 11.5px;
  font-weight: 500;
  line-height: 1.4;
  white-space: nowrap;
}
.sd-pill__dot {
  width: 6px;
  height: 6px;
  border-radius: var(--radius-pill);
  flex-shrink: 0;
}
</style>
