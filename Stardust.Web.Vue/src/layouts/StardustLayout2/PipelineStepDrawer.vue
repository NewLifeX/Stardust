<script setup lang="ts">
/**
 * PipelineStepDrawer：步骤明细抽屉（CARD-DEP-03，J-DEP-09）。
 * 数据源 = CARD-DEP-02 运行详情的 detail.steps（StepIndex 升序，零新增请求）；格子区维持既有 runId 查询不动。
 * 九字段列表：RunId、StepType（Build/Deploy 原样）、StepIndex、NodeId（节点名兜底「节点 {Id}」）、
 * CommandId、Status（字符串 Running/Success/Failed/Skipped/Cancelled 原样展示不翻译）、Message、StartedTime、FinishedTime；
 * 隐藏 Id/CreateTime（盘点裁剪清单）。只读：无任何操作列（§6.3）。
 * 单条详情 = 行展开显示 Message 全文（后端 CutMessage 截断 2000）；Message 空显「—」。
 */
import { ref } from 'vue';
import Btn from './Btn.vue';
import Drawer from './Drawer/Drawer.vue';
import KIcon from './KIcon.vue';
import { TD, TH } from './deployView.styles.ts';
import { nodeNameOf } from './ship.logic.ts';
import type { RunDetail, ShipNode, StepDetailRow } from './ship.logic.ts';

const props = defineProps<{
  open: boolean;
  detail: RunDetail | null;
  nodes: ShipNode[];
}>();

const emit = defineEmits<{ close: [] }>();

const expanded = ref<number>(0);

function toggle(row: StepDetailRow): void {
  expanded.value = expanded.value === row.id ? 0 : row.id;
}

function nodeLabel(id: number): string {
  return nodeNameOf(props.nodes, id);
}

function statusTone(s: string): string {
  if (s === 'Success') return 'text-[var(--el-color-success)]';
  if (s === 'Failed') return 'text-[var(--el-color-danger)]';
  if (s === 'Running') return 'text-[var(--el-color-primary)]';
  if (s === 'Skipped' || s === 'Cancelled') return 'text-[var(--el-text-color-secondary)]';
  return 'text-[var(--el-color-warning)]';
}
</script>

<template>
  <Drawer
    :open="open"
    title="流水线步骤明细"
    :sub="detail ? `运行 #${detail.id} · 共 ${detail.steps.length} 步` : ''"
    @close="emit('close')"
  >
    <div
      v-if="!detail || !detail.steps.length"
      class="py-6 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
      data-spec="pipeline-steps-empty"
    >
      这次运行没有步骤记录（只有流水线发起的部署才有）
    </div>

    <div
      v-else
      data-spec="pipeline-steps-drawer"
    >
      <p class="m-0 mb-[10px] text-[11.5px] text-[var(--el-text-color-secondary)]">
        状态为后端原样字符串（不翻译）；点行展开查看消息全文。
      </p>
      <table class="w-full border-collapse">
        <thead>
          <tr>
            <th :class="TH">
              类型
            </th>
            <th :class="TH">
              序号
            </th>
            <th :class="TH">
              节点
            </th>
            <th :class="TH">
              状态
            </th>
            <th :class="TH">
              消息
            </th>
            <th :class="TH">
              开始时间
            </th>
            <th :class="TH">
              完成时间
            </th>
          </tr>
        </thead>
        <tbody>
          <template
            v-for="s in detail.steps"
            :key="s.id"
          >
            <tr
              data-spec="pipeline-steps-row"
              class="group cursor-pointer transition-colors duration-[120ms] hover:bg-[var(--el-bg-color-overlay)]"
              :title="`RunId ${s.runId} · CommandId ${s.commandId || '—'}`"
              @click="toggle(s)"
            >
              <td :class="TD">
                <b class="font-medium text-[var(--el-text-color-primary)]">{{ s.stepType }}</b>
              </td>
              <td
                :class="[TD, 'font-mono tabular-nums text-[var(--el-text-color-secondary)]']"
              >
                {{ s.stepIndex }}
              </td>
              <td :class="[TD, 'text-[var(--el-text-color-secondary)]']">
                {{ nodeLabel(s.nodeId) }}
              </td>
              <td
                :class="[TD, 'font-mono text-[11.5px]', statusTone(s.status)]"
              >
                {{ s.status || '—' }}
              </td>
              <td :class="[TD, 'max-w-[180px] truncate text-[var(--el-text-color-secondary)]']">
                {{ s.message || '—' }}
              </td>
              <td
                :class="[TD, 'font-mono tabular-nums text-[11.5px] text-[var(--el-text-color-secondary)]']"
              >
                {{ s.startText }}
              </td>
              <td
                :class="[TD, 'font-mono tabular-nums text-[11.5px] text-[var(--el-text-color-secondary)]']"
              >
                {{ s.endText }}
              </td>
            </tr>
            <tr
              v-if="expanded === s.id"
              :key="`x-${s.id}`"
            >
              <td
                :colspan="7"
                class="border-b border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[14px] py-[10px]"
                data-spec="pipeline-steps-expand"
              >
                <div class="mb-1 flex items-center gap-3 text-[11.5px] text-[var(--el-text-color-secondary)]">
                  <span>RunId {{ s.runId }}</span>
                  <span>CommandId {{ s.commandId || '—' }}</span>
                  <span class="inline-flex items-center gap-1">
                    <KIcon
                      :name="s.message ? 'ScrollText' : 'Inbox'"
                      :size="12"
                    />
                    消息全文
                  </span>
                </div>
                <p class="m-0 whitespace-pre-wrap break-words font-mono text-[11.5px] text-[var(--el-text-color-primary)]">
                  {{ s.message || '—' }}
                </p>
              </td>
            </tr>
          </template>
        </tbody>
      </table>
    </div>

    <template #actions>
      <Btn
        variant="ghost"
        data-spec="pipeline-steps-close"
        @click="emit('close')"
      >
        关闭
      </Btn>
    </template>
  </Drawer>
</template>
