<script setup lang="ts">
/**
 * DeployFeedbackStrip：操作进行中提示 + 最近反馈条（task-88 2026-11-12 从 ShipPanel.vue 抽出为纯 UI，样式值与原文一致）。
 * 纯 UI：不持有状态、不执行业务回调；「查看运行」只 emit openRun(runId)，由容器打开 PipelineRunDetailDrawer。
 * items 的 action 由 useDeployData.triggerRun/rerunRun 在成功解析「运行编号[N]」后携带（N = AppPipelineRun.Id）。
 */
import Btn from './Btn.vue';
import KIcon from './KIcon.vue';

/** 反馈条目（与 useDeployData.DeployFeedback 结构一致，直属容器传入） */
export interface DeployFeedbackItem {
  key: number;
  ok: boolean;
  text: string;
  at: string;
  /** 成功触发/重跑流水线时的直达动作；其余操作无 */
  action?: { kind: 'openRun'; label: string; runId: number };
}

defineProps<{
  /** 操作进行中文案；空表示空闲不显示 */
  busyText: string;
  /** 最近反馈（最新在前，容器已截断到 6 条） */
  items: DeployFeedbackItem[];
}>();

const emit = defineEmits<{ openRun: [runId: number] }>();

const OK_CLS =
  'border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] text-[var(--el-text-color-secondary)]';
const ERR_CLS =
  'border-[color-mix(in_oklab,var(--el-color-danger)_35%,transparent)] bg-[color-mix(in_oklab,var(--el-color-danger)_10%,transparent)] text-[var(--el-color-danger)]';
</script>

<template>
  <div
    v-if="busyText || items.length"
    class="flex flex-col gap-[6px]"
    data-spec="ship-feedback"
  >
    <div
      v-if="busyText"
      class="rounded-[8px] border border-[color-mix(in_oklab,var(--el-color-primary)_40%,transparent)] bg-[color-mix(in_oklab,var(--el-color-primary)_10%,transparent)] px-3 py-2 text-[12.5px] text-[var(--el-color-primary-light-3)]"
      data-spec="ship-busy"
    >
      {{ busyText }}
    </div>
    <div
      v-for="f in items"
      :key="f.key"
      data-spec="ship-feedback-item"
      class="flex items-start gap-2 rounded-[8px] border px-3 py-2 text-[12.5px]"
      :class="f.ok ? OK_CLS : ERR_CLS"
    >
      <KIcon
        :name="f.ok ? 'Check' : 'Alert'"
        :size="13"
        class="mt-[2px] shrink-0"
      />
      <span class="min-w-0 flex-1 break-words">{{ f.text }}</span>
      <Btn
        v-if="f.action?.kind === 'openRun'"
        size="sm"
        variant="primary"
        :title="`打开运行 #${f.action.runId} 的详情抽屉（状态、步骤、Trace）`"
        data-spec="ship-feedback-action"
        @click="f.action && emit('openRun', f.action.runId)"
      >
        {{ f.action.label }}
      </Btn>
      <span class="shrink-0 font-mono text-[11px] tabular-nums opacity-70">{{ f.at }}</span>
    </div>
  </div>
</template>
