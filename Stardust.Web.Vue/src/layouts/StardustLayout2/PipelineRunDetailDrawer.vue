<script setup lang="ts">
/**
 * PipelineRunDetailDrawer：运行详情抽屉（CARD-DEP-02，J-DEP-08 S8 收编；裁定 #3 抽屉形态、#4 轮询统一 5s）。
 * 数据源 = WebApi Detail（GET /Deployment/AppPipelineRun/Detail?id=，run 全字段 + steps[]），由容器拉好传入。
 * 只读：无编辑/删除操作（§6.3 只读历史）；「（轮询中）」随状态 0-3 显示，轮询节奏由容器（useDeployData）持有。
 * 「查看全部步骤」打开 CARD-DEP-03 步骤明细抽屉（零新增请求，消费同一份 detail.steps）。
 */
import Btn from './Btn.vue';
import Drawer from './Drawer/Drawer.vue';
import KIcon from './KIcon.vue';
import Tag from './Tag.vue';
import { runElapsedSeconds } from './ship.logic.ts';
import type { RunDetail } from './ship.logic.ts';

export type RunDetailState = 'loading' | 'ready' | 'missing' | 'failed';

defineProps<{
  open: boolean;
  /** 容器状态机：loading 初载 / ready 就绪 / missing 不存在 / failed 请求失败 */
  state: RunDetailState;
  detail: RunDetail | null;
  /** 取消请求进行中 */
  cancelling?: boolean;
}>();

const emit = defineEmits<{ close: []; openSteps: []; retry: []; cancel: [] }>();

const ROW_CLS = 'flex items-start justify-between gap-3 border-b border-[var(--el-border-color-light)] py-[7px]';
const LABEL_CLS = 'shrink-0 text-[12px] text-[var(--el-text-color-secondary)]';
const VALUE_CLS = 'min-w-0 break-words text-right text-[12.5px] text-[var(--el-text-color-primary)]';
</script>

<template>
  <Drawer
    :open="open"
    title="流水线运行详情"
    :sub="detail ? `运行编号 #${detail.id}` : ''"
    @close="emit('close')"
  >
    <div
      v-if="state === 'loading'"
      class="py-6 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
      data-spec="pipeline-run-detail-loading"
    >
      运行详情加载中…
    </div>

    <div
      v-else-if="state === 'missing'"
      class="py-6 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
      data-spec="pipeline-run-detail-missing"
    >
      运行记录不存在或已被删除
    </div>

    <div
      v-else-if="state === 'failed'"
      class="flex flex-col items-center gap-3 py-6 text-[12.5px] text-[var(--el-color-danger)]"
      data-spec="pipeline-run-detail-failed"
    >
      运行详情加载失败
      <Btn
        size="sm"
        data-spec="pipeline-run-detail-retry"
        @click="emit('retry')"
      >
        重试
      </Btn>
    </div>

    <template v-else-if="detail">
      <div class="mb-[10px] flex items-center gap-2">
        <Tag :tone="detail.tone">
          {{ detail.statusLabel }}
        </Tag>
        <span
          v-if="detail.active"
          class="inline-flex items-center gap-1 text-[11.5px] text-[var(--el-text-color-secondary)]"
          data-spec="pipeline-run-detail-polling"
        >
          <span class="inline-flex animate-spin">
            <KIcon
              name="Rotate"
              :size="11"
            />
          </span>
          （轮询中）
        </span>
        <span class="flex-1" />
        <Btn
          v-if="detail.active"
          size="sm"
          variant="default"
          :disabled="cancelling"
          :title="cancelling ? '取消中…' : '取消本次运行（不中断节点上已开始的进程）'"
          data-spec="pipeline-run-detail-cancel"
          @click="emit('cancel')"
        >
          {{ cancelling ? '取消中…' : '取消运行' }}
        </Btn>
        <Btn
          size="sm"
          variant="default"
          data-spec="pipeline-run-detail-steps"
          @click="emit('openSteps')"
        >
          查看全部步骤
        </Btn>
      </div>

      <div
        class="flex flex-col"
        data-spec="pipeline-run-detail-rows"
      >
        <div :class="ROW_CLS">
          <span :class="LABEL_CLS">所属流水线</span>
          <span :class="VALUE_CLS">{{ detail.pipelineName || '—' }}</span>
        </div>
        <div :class="ROW_CLS">
          <span :class="LABEL_CLS">触发源</span>
          <span :class="VALUE_CLS">{{ detail.triggerSource || '—' }}</span>
        </div>
        <div :class="ROW_CLS">
          <span :class="LABEL_CLS">分支</span>
          <span
            :class="[VALUE_CLS, 'font-mono']"
          >
            {{ detail.branch || '—' }}
          </span>
        </div>
        <div :class="ROW_CLS">
          <span :class="LABEL_CLS">提交 SHA</span>
          <span
            :class="[VALUE_CLS, 'font-mono']"
          >
            {{ detail.commitId || '—' }}
          </span>
        </div>
        <div :class="ROW_CLS">
          <span :class="LABEL_CLS">提交信息</span>
          <span :class="VALUE_CLS">{{ detail.commitMessage || '—' }}</span>
        </div>
        <div :class="ROW_CLS">
          <span :class="LABEL_CLS">提交人</span>
          <span :class="VALUE_CLS">{{ detail.commitAuthor || '—' }}</span>
        </div>
        <div :class="ROW_CLS">
          <span :class="LABEL_CLS">提交时间</span>
          <span
            :class="[VALUE_CLS, 'font-mono tabular-nums']"
          >
            {{ detail.commitTime }}
          </span>
        </div>
        <div :class="ROW_CLS">
          <span :class="LABEL_CLS">编译开始</span>
          <span
            :class="[VALUE_CLS, 'font-mono tabular-nums']"
          >
            {{ detail.buildStartedTime }}
          </span>
        </div>
        <div :class="ROW_CLS">
          <span :class="LABEL_CLS">编译完成</span>
          <span
            :class="[VALUE_CLS, 'font-mono tabular-nums']"
          >
            {{ detail.buildFinishedTime }}
          </span>
        </div>
        <div :class="ROW_CLS">
          <span :class="LABEL_CLS">部署开始</span>
          <span
            :class="[VALUE_CLS, 'font-mono tabular-nums']"
          >
            {{ detail.deployStartedTime }}
          </span>
        </div>
        <div :class="ROW_CLS">
          <span :class="LABEL_CLS">部署完成</span>
          <span
            :class="[VALUE_CLS, 'font-mono tabular-nums']"
          >
            {{ detail.deployFinishedTime }}
          </span>
        </div>
        <div :class="ROW_CLS">
          <span :class="LABEL_CLS">耗时（秒）</span>
          <span
            :class="[VALUE_CLS, 'font-mono tabular-nums']"
          >
            {{ runElapsedSeconds(detail) ?? '—' }}
          </span>
        </div>
        <div :class="ROW_CLS">
          <span :class="LABEL_CLS">产物版本</span>
          <span :class="VALUE_CLS">{{ detail.versionLabel || '—' }}</span>
        </div>
        <div :class="ROW_CLS">
          <span :class="LABEL_CLS">编译节点</span>
          <span
            :class="[VALUE_CLS, 'font-mono']"
          >
            {{ detail.buildNodeId > 0 ? `#${detail.buildNodeId}` : '—' }}
          </span>
        </div>
        <div :class="ROW_CLS">
          <span :class="LABEL_CLS">追踪 TraceId</span>
          <span :class="VALUE_CLS">
            <a
              v-if="detail.traceUrl"
              :href="detail.traceUrl"
              target="_blank"
              rel="noreferrer"
              class="text-[var(--el-color-primary)] underline decoration-dotted"
            >{{ detail.traceId || detail.traceUrl }}</a>
            <template v-else>{{ detail.traceId || '—' }}</template>
          </span>
        </div>
        <div :class="[ROW_CLS, 'border-b-0']">
          <span :class="LABEL_CLS">备注与错误信息</span>
          <span :class="VALUE_CLS">{{ detail.remark || '（空）' }}</span>
        </div>
        <div :class="ROW_CLS">
          <span :class="LABEL_CLS">创建时间</span>
          <span
            :class="[VALUE_CLS, 'font-mono tabular-nums']"
          >
            {{ detail.createTime }}
          </span>
        </div>
        <div :class="[ROW_CLS, 'border-b-0']">
          <span :class="LABEL_CLS">更新时间</span>
          <span
            :class="[VALUE_CLS, 'font-mono tabular-nums']"
          >
            {{ detail.updateTime }}
          </span>
        </div>
      </div>
    </template>
  </Drawer>
</template>
