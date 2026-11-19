<script setup lang="ts">
/**
 * PipelineWebhookCard：Webhook 接入说明卡（CARD-DEP-04，J-DEP-10；pipeline 页签区块 D）。
 * 纯 UI：地址复制（CopyWebhook 由容器完成，本卡只发 copy(row)）；接入指引消费 GetFields 字段说明，
 * 接口失败降级为静态说明（pipelineWebhook.logic.WEBHOOK_FIELD_FALLBACK）。
 * 说明内容：POST + 原始 JSON body + Secret 时需 X-Hub-Signature-256 头（Secret 值不做查看/设置，裁定 #6）；
 * 结果语义速查五类（reason 逐字）；异步说明（接受即返回，运行后台推进，前端 5s 轮询可见）。
 */
import Btn from './Btn.vue';
import Card from './Card.vue';
import KIcon from './KIcon.vue';
import { H3 } from './deployView.styles.ts';
import { WEBHOOK_FIELD_FALLBACK, WEBHOOK_RESULT_GROUPS } from './pipelineWebhook.logic.ts';
import type { WebhookFieldDoc } from './pipelineWebhook.logic.ts';
import type { PipelineRow } from './pipelineConfig.logic.ts';

defineProps<{
  /** 该部署集下的流水线（多流水线全量展示，逐条复制） */
  rows: PipelineRow[];
  /** GetFields 字段说明；null = 接口失败已降级静态说明 */
  fields: WebhookFieldDoc[] | null;
  fieldsLoading: boolean;
  /** 复制请求进行中的流水线 Id（busy） */
  copyingId: number;
}>();

const emit = defineEmits<{ copy: [row: PipelineRow] }>();

const LIST_CLS = 'm-0 list-disc pl-[18px] text-[12px] leading-[1.9] text-[var(--el-text-color-secondary)]';
</script>

<template>
  <Card
    data-spec="pipeline-webhook-card"
  >
    <div class="flex flex-wrap items-center gap-3">
      <h3 :class="H3">
        Webhook 接入
        <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">把 Git 平台的 push 事件接到流水线</span>
      </h3>
    </div>

    <div class="flex flex-col gap-3">
      <div
        v-if="!rows.length"
        class="text-[12px] text-[var(--el-text-color-secondary)]"
      >
        还没有流水线，先在上方「流水线配置」创建一条，再复制 WebHook 地址。
      </div>
      <div
        v-else
        class="flex flex-col gap-[6px]"
      >
        <div
          v-for="r in rows"
          :key="r.id"
          class="flex flex-wrap items-center gap-2 rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-3 py-2"
        >
          <b class="text-[12.5px] font-medium text-[var(--el-text-color-primary)]">{{ r.name }}</b>
          <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">{{ r.enable ? '已启用' : '未启用' }}</span>
          <span class="flex-1" />
          <Btn
            size="sm"
            variant="default"
            :disabled="!!copyingId"
            :title="copyingId === r.id ? '复制中…' : '复制该流水线的 WebHook 触发地址'"
            data-spec="pipeline-webhook-copy"
            @click="emit('copy', r)"
          >
            <template #icon>
              <span :class="copyingId === r.id ? 'inline-flex animate-spin' : 'inline-flex'">
                <KIcon
                  name="Copy"
                  :size="12"
                />
              </span>
            </template>
            {{ copyingId === r.id ? '复制中…' : '复制 WebHook 地址' }}
          </Btn>
        </div>
      </div>

      <div>
        <div class="mb-1 text-[12px] font-medium text-[var(--el-text-color-primary)]">
          接入指引（鉴权参数）
        </div>
        <div
          v-if="fieldsLoading"
          class="text-[11.5px] text-[var(--el-text-color-secondary)]"
          data-spec="pipeline-webhook-fields-loading"
        >
          字段说明加载中…
        </div>
        <ul
          v-else
          :class="LIST_CLS"
          data-spec="pipeline-webhook-fields"
        >
          <li
            v-for="f in fields ?? WEBHOOK_FIELD_FALLBACK"
            :key="f.name"
          >
            <b class="font-mono text-[var(--el-text-color-primary)]">{{ f.displayName }}</b>
            （{{ f.name }}）：{{ f.description }}
          </li>
          <li
            v-if="!fields"
            data-spec="pipeline-webhook-fields-fallback"
          >
            以上为内置说明（字段说明接口暂不可用，已降级）。
          </li>
        </ul>
      </div>

      <div>
        <div class="mb-1 text-[12px] font-medium text-[var(--el-text-color-primary)]">
          请求说明
        </div>
        <ul :class="LIST_CLS">
          <li>
            对外地址：<code class="font-mono">POST /Deployment/Pipeline/Webhook?token={{ '{token}' }}</code>，body 为原始 JSON 字符串。
          </li>
          <li>流水线配置了 Secret 时，Git 平台需发送请求头 <code class="font-mono">X-Hub-Signature-256</code>；Secret 值本身不做查看/设置。</li>
          <li>webhook 是异步执行的：接受后立即返回，运行后台推进，前端每 5 秒轮询可见（上方「流水线运行」区块）。</li>
        </ul>
      </div>

      <div>
        <div class="mb-1 text-[12px] font-medium text-[var(--el-text-color-primary)]">
          结果语义速查（reason 字符串原样）
        </div>
        <div
          class="flex flex-col gap-[6px]"
          data-spec="pipeline-webhook-results"
        >
          <div
            v-for="g in WEBHOOK_RESULT_GROUPS"
            :key="g.result"
            class="rounded-[8px] border px-3 py-2 text-[12px]"
            :class="g.tone === 'ok'
              ? 'border-[color-mix(in_oklab,var(--el-color-success)_35%,transparent)] bg-[color-mix(in_oklab,var(--el-color-success)_8%,transparent)]'
              : g.tone === 'err'
                ? 'border-[color-mix(in_oklab,var(--el-color-danger)_35%,transparent)] bg-[color-mix(in_oklab,var(--el-color-danger)_8%,transparent)]'
                : 'border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)]'"
          >
            <div class="flex flex-wrap items-center gap-2">
              <code class="font-mono text-[var(--el-text-color-primary)]">{{ g.result }}</code>
              <b class="font-medium text-[var(--el-text-color-primary)]">{{ g.label }}</b>
              <span class="text-[var(--el-text-color-secondary)]">{{ g.detail }}</span>
            </div>
            <div
              v-if="g.reasons.length"
              class="mt-[3px] font-mono text-[11px] text-[var(--el-text-color-secondary)]"
            >
              reason：{{ g.reasons.join(' · ') }}
            </div>
          </div>
        </div>
      </div>
    </div>
  </Card>
</template>
