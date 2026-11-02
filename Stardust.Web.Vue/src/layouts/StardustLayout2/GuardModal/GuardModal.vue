<script setup lang="ts">
/**
 * GuardModal：生产环境二次确认，对照原型 kit-shell.jsx 的 GuardModal 与 kit.css 的 .x-mask / .x-modal / .x-warnbar。
 * 纯 UI：只发 cancel 与 confirm，不自己关闭、不执行回调。
 * 点遮罩等于取消（原型 Modal 的 onClick={onClose}），点弹窗本体不冒泡。
 * Esc 不在这里处理：要和命令面板、抽屉、控制台按原型顺序逐层关，统一放在外壳。
 * 不用 ElDialog：Esc 层级和 z-index 由外壳管，遮罩写法与 CommandPalette 一致。
 */
import Btn from '../Btn.vue';

defineProps<{ label: string }>();

const emit = defineEmits<{
  cancel: [];
  confirm: [];
}>();
</script>

<template>
  <div
    data-spec="guard-modal"
    class="fixed inset-0 z-[60] flex animate-x-fade items-start justify-center bg-[color-mix(in_oklab,var(--el-color-black)_48%,transparent)] px-4 pb-4 pt-[9vh] backdrop-blur-[8px] motion-reduce:animate-none [.dark_&]:bg-[color-mix(in_oklab,var(--el-color-black)_72%,transparent)]"
    @click="emit('cancel')"
  >
    <div
      role="dialog"
      aria-modal="true"
      :aria-label="label"
      class="max-h-[78vh] w-[min(660px,96vw)] animate-x-pop overflow-auto rounded-[16px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-6 py-[22px] text-[var(--el-text-color-primary)] shadow-[var(--el-box-shadow)] motion-reduce:animate-none"
      @click.stop
    >
      <span
        data-spec="guard-warnbar"
        class="mb-[10px] inline-flex items-center gap-[6px] rounded-base border border-[color-mix(in_oklab,var(--el-color-danger)_30%,transparent)] bg-[color-mix(in_oklab,var(--el-color-danger)_14%,transparent)] px-2 py-[3px] font-mono text-[10.5px] uppercase tracking-[0.08em] text-[var(--el-color-danger)]"
      >
        <span class="inline-block h-[7px] w-[7px] shrink-0 rounded-full bg-[var(--el-color-danger)] shadow-[0_0_6px_color-mix(in_oklab,var(--el-color-danger)_70%,transparent)]" />
        Production · 生产环境
      </span>
      <h2
        data-spec="guard-title"
        class="mb-1 mt-0 text-[19px] font-semibold tracking-[-0.02em]"
      >
        {{ label }}
      </h2>
      <p class="m-0 text-[12.5px] text-[var(--el-text-color-secondary)]">
        该操作会立即影响线上流量,并记入审计日志。
      </p>
      <div class="mt-[18px] flex justify-end gap-2">
        <Btn
          variant="ghost"
          data-spec="guard-cancel"
          @click="emit('cancel')"
        >
          取消
        </Btn>
        <Btn
          variant="primary"
          data-spec="guard-confirm"
          @click="emit('confirm')"
        >
          确认执行
        </Btn>
      </div>
    </div>
  </div>
</template>