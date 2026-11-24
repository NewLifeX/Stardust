<script setup lang="ts">
/**
 * PipelineConfigCard：流水线配置区块（CARD-DEP-01，J-DEP-07；pipeline 页签区块 A）。
 * 纯 UI：只收 props、只发 create/copy/trigger/edit/remove/retry；Guard 确认、剪贴板与请求都在容器。
 * 列表列（重排 7 列）：名称/分支/编译节点/自动部署/启用/更新时间/操作；无 Id、Token、Secret、DeployNodeIds、TraceId 列（盘点隐藏清单）。
 * 空态两分支：无部署集由容器整卡兜底；这里只处理「部署集存在但无流水线」→「还没有流水线」+「创建流水线」引导（裁定 #9）。
 */
import { ref } from 'vue';
import Btn from './Btn.vue';
import Card from './Card.vue';
import KIcon from './KIcon.vue';
import Tag from './Tag.vue';
import { H3, TD, TH } from './deployView.styles.ts';
import { fmtPipelineTime } from './pipelineConfig.logic.ts';
import type { PipelineRow } from './pipelineConfig.logic.ts';

const props = defineProps<{
  rows: PipelineRow[];
  /** 列表加载失败（返回 null） */
  failed: boolean;
  /** 行操作进行中的 key：copy:{id} / trigger:{id} / del:{id} / save */
  busyKey: string;
}>();

const emit = defineEmits<{
  create: [];
  copy: [row: PipelineRow];
  trigger: [row: PipelineRow];
  /** 指定分支/提交触发。branch/commit 留空表示使用流水线分支最新提交 */
  'trigger-advanced': [row: PipelineRow, params: { branch: string; commit: string }];
  edit: [row: PipelineRow];
  remove: [row: PipelineRow];
  retry: [];
}>();

// 指定分支/提交触发（默认路径不变：直接点「启动」= 分支最新）
const advOpen = ref(false);
const advRow = ref<PipelineRow | null>(null);
const advBranch = ref('');
const advCommit = ref('');

function openAdvanced(row: PipelineRow): void {
  if (!row.enable || props.busyKey) return;
  advRow.value = row;
  advBranch.value = '';
  advCommit.value = '';
  advOpen.value = true;
}

function submitAdvanced(): void {
  const row = advRow.value;
  if (!row) return;
  advOpen.value = false;
  emit('trigger-advanced', row, { branch: advBranch.value.trim(), commit: advCommit.value.trim() });
}

function busyOf(kind: 'copy' | 'trigger' | 'del', id: number): boolean {
  return props.busyKey === `${kind}:${id}`;
}
</script>

<template>
  <Card
    padding="none"
    data-spec="pipeline-config"
  >
    <div class="px-[18px] pb-2 pt-4">
      <div class="flex flex-wrap items-center gap-3">
        <h3 :class="H3">
          流水线配置
          <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">绑定分支与节点，Webhook 或手动触发编译部署</span>
        </h3>
        <span class="flex-1" />
        <Btn
          size="sm"
          variant="primary"
          :disabled="!!busyKey"
          data-spec="pipeline-config-create"
          @click="emit('create')"
        >
          <template #icon>
            <KIcon
              name="Plus"
              :size="12"
            />
          </template>
          创建流水线
        </Btn>
      </div>
    </div>

    <div
      v-if="failed"
      class="flex items-center gap-3 px-[18px] pb-4 text-[12.5px] text-[var(--el-color-danger)]"
      data-spec="pipeline-config-failed"
    >
      流水线配置加载失败
      <Btn
        size="sm"
        data-spec="pipeline-config-retry"
        @click="emit('retry')"
      >
        重试
      </Btn>
    </div>

    <div
      v-else-if="!rows.length"
      class="flex flex-wrap items-center gap-3 px-[18px] pb-4 text-[12.5px] text-[var(--el-text-color-secondary)]"
      data-spec="pipeline-config-empty"
    >
      还没有流水线
      <Btn
        size="sm"
        variant="default"
        :disabled="!!busyKey"
        data-spec="pipeline-config-empty-create"
        @click="emit('create')"
      >
        创建流水线
      </Btn>
    </div>

    <table
      v-else
      class="w-full border-collapse"
    >
      <thead>
        <tr>
          <th :class="TH">
            名称
          </th>
          <th :class="TH">
            分支
          </th>
          <th :class="TH">
            编译节点
          </th>
          <th :class="TH">
            自动部署
          </th>
          <th :class="TH">
            启用
          </th>
          <th :class="TH">
            更新时间
          </th>
          <th :class="[TH, 'text-right']">
            操作
          </th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="r in rows"
          :key="r.id"
          data-spec="pipeline-config-row"
          class="group"
        >
          <td :class="TD">
            <b class="font-medium text-[var(--el-text-color-primary)]">{{ r.name }}</b>
          </td>
          <td
            :class="[TD, 'font-mono text-[11.5px] text-[var(--el-text-color-secondary)]']"
          >
            {{ r.branch || '—' }}
          </td>
          <td
            :class="[TD, 'text-[var(--el-text-color-secondary)]']"
          >
            {{ r.buildNodeId > 0 ? `#${r.buildNodeId}` : '—' }}
          </td>
          <td :class="TD">
            <Tag :tone="r.autoDeploy ? 'ok' : 'default'">
              {{ r.autoDeploy ? '自动' : '手动' }}
            </Tag>
          </td>
          <td :class="TD">
            <span :class="r.enable ? 'text-[var(--el-color-success)]' : 'text-[var(--el-text-color-placeholder)]'">{{ r.enable ? '是' : '否' }}</span>
          </td>
          <td
            :class="[TD, 'font-mono tabular-nums text-[var(--el-text-color-secondary)]']"
          >
            {{ fmtPipelineTime(r.updateTime) }}
          </td>
          <td :class="[TD, 'text-right']">
            <span class="inline-flex justify-end gap-1">
              <Btn
                size="sm"
                variant="ghost"
                :disabled="!!busyKey"
                :title="busyOf('copy', r.id) ? '复制中…' : '复制 WebHook 触发地址'"
                data-spec="pipeline-config-copywebhook"
                @click="emit('copy', r)"
              >
                <template #icon>
                  <span :class="busyOf('copy', r.id) ? 'inline-flex animate-spin' : 'inline-flex'">
                    <KIcon
                      name="Copy"
                      :size="12"
                    />
                  </span>
                </template>
                {{ busyOf('copy', r.id) ? '复制中…' : '复制 WebHook' }}
              </Btn>
              <Btn
                size="sm"
                variant="ghost"
                :disabled="!r.enable || !!busyKey"
                :title="!r.enable ? '流水线未启用，先编辑打开「启用」' : busyOf('trigger', r.id) ? '触发中…' : '手动触发一次运行（使用当前分支最新提交）'"
                data-spec="pipeline-config-trigger"
                @click="emit('trigger', r)"
              >
                <template #icon>
                  <span :class="busyOf('trigger', r.id) ? 'inline-flex animate-spin' : 'inline-flex'">
                    <KIcon
                      name="Play"
                      :size="12"
                    />
                  </span>
                </template>
                {{ busyOf('trigger', r.id) ? '触发中…' : '启动' }}
              </Btn>
              <Btn
                size="sm"
                variant="ghost"
                :disabled="!r.enable || !!busyKey"
                :title="!r.enable ? '流水线未启用，先编辑打开「启用」' : '指定分支或提交触发（留空则用分支最新）'"
                data-spec="pipeline-config-trigger-advanced"
                @click="openAdvanced(r)"
              >
                指定提交
              </Btn>
              <Btn
                size="sm"
                variant="ghost"
                :disabled="!!busyKey"
                data-spec="pipeline-config-edit"
                @click="emit('edit', r)"
              >
                编辑
              </Btn>
              <Btn
                size="sm"
                variant="ghost"
                :disabled="!!busyKey"
                :title="busyOf('del', r.id) ? '删除中…' : '删除该流水线配置'"
                data-spec="pipeline-config-remove"
                @click="emit('remove', r)"
              >
                {{ busyOf('del', r.id) ? '删除中…' : '删除' }}
              </Btn>
            </span>
          </td>
        </tr>
      </tbody>
    </table>

    <el-dialog
      v-model="advOpen"
      title="指定分支或提交触发"
      width="420px"
      append-to-body
      data-spec="pipeline-trigger-advanced-dialog"
    >
      <div class="flex flex-col gap-3">
        <el-input
          v-model="advBranch"
          size="small"
          clearable
          placeholder="分支（留空 = 流水线分支）"
          data-spec="pipeline-trigger-branch"
        />
        <el-input
          v-model="advCommit"
          size="small"
          clearable
          placeholder="提交 SHA（留空 = 分支最新）"
          data-spec="pipeline-trigger-commit"
        />
      </div>
      <template #footer>
        <div class="flex justify-end gap-2">
          <Btn
            size="sm"
            @click="advOpen = false"
          >
            取消
          </Btn>
          <Btn
            size="sm"
            variant="primary"
            data-spec="pipeline-trigger-advanced-confirm"
            @click="submitAdvanced"
          >
            触发
          </Btn>
        </div>
      </template>
    </el-dialog>
  </Card>
</template>
