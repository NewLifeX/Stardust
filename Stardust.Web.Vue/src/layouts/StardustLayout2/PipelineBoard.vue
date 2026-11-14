<script setup lang="ts">
/**
 * PipelineBoard：应用「流水线」页签上的流水线配置。
 * 纯 UI：新建 / 编辑 / 手动触发 / 复制 Webhook 都只发事件，请求与二次确认在容器里。
 * 列表没有 deployNodeIds 和 token，这里不展示凭据。
 */
import Btn from './Btn.vue';
import Card from './Card.vue';
import KIcon from './KIcon.vue';
import Tag from './Tag.vue';
import { H3 } from './deployView.styles.ts';
import type { PipelineCard } from './pipelineForm.logic.ts';

const props = defineProps<{
  cards: PipelineCard[];
  failed: boolean;
  busyKey: string;
  /** 物理节点编号 → 名称；对不上时显示「节点 #id」 */
  buildLabels: Record<number, string>;
  copyingId: number;
}>();

const emit = defineEmits<{
  create: [];
  edit: [card: PipelineCard];
  trigger: [card: PipelineCard];
  copy: [card: PipelineCard];
}>();

function buildLabel(id: number): string {
  if (!id) return '未指定编译节点';
  return props.buildLabels[id] || `节点 #${id}`;
}

function triggerTitle(card: PipelineCard): string {
  if (!card.enable) return '流水线已停用，启用后才能手动触发';
  if (!card.buildNodeId) return '还没有编译节点，无法触发';
  return '在编译节点上拉取该分支并编译；开了自动部署时成功后会安装到部署节点';
}
</script>

<template>
  <Card
    padding="none"
    data-spec="pipeline-board"
  >
    <div class="px-[18px] pb-2 pt-4">
      <div class="flex flex-wrap items-center gap-3">
        <h3 :class="H3">
          流水线
          <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">监听分支的 Webhook，或在这里手动触发编译；Token 不在列表里显示</span>
        </h3>
        <span class="flex-1" />
        <Btn
          size="sm"
          variant="primary"
          :disabled="!!busyKey"
          data-spec="pipeline-create"
          title="新建一条流水线，关联本部署集的编译节点和部署节点"
          @click="emit('create')"
        >
          <template #icon>
            <KIcon
              name="Plus"
              :size="12"
            />
          </template>
          新建流水线
        </Btn>
      </div>
    </div>

    <div
      v-if="failed"
      class="px-[18px] pb-4 text-[12.5px] text-[var(--el-color-danger)]"
      data-spec="pipeline-board-failed"
    >
      流水线加载失败，请点上方「刷新」重试
    </div>
    <div
      v-else-if="!cards.length"
      class="px-[18px] pb-4 text-[12.5px] text-[var(--el-text-color-secondary)]"
      data-spec="pipeline-board-empty"
    >
      还没有流水线。新建后可以手动触发，或把 Webhook 配到代码仓库。
    </div>
    <ul
      v-else
      class="m-0 list-none px-[10px] pb-3"
    >
      <li
        v-for="card in cards"
        :key="card.id"
        class="flex flex-wrap items-center gap-2 border-t border-[var(--el-border-color-light)] px-2 py-3"
        data-spec="pipeline-card"
      >
        <div class="min-w-0 flex-1">
          <div class="flex flex-wrap items-center gap-2">
            <b class="text-[13px] font-medium text-[var(--el-text-color-primary)]">{{ card.name || `流水线 #${card.id}` }}</b>
            <Tag :tone="card.enable ? 'ok' : 'default'">
              {{ card.enable ? '启用' : '停用' }}
            </Tag>
            <Tag
              v-if="card.autoDeploy"
              tone="brand"
            >
              自动部署
            </Tag>
          </div>
          <p class="m-0 mt-1 text-[11.5px] text-[var(--el-text-color-secondary)]">
            分支 {{ card.branch || '—' }} · 编译节点 {{ buildLabel(card.buildNodeId) }}
          </p>
        </div>
        <Btn
          size="sm"
          variant="primary"
          :disabled="!!busyKey || !card.enable || !card.buildNodeId"
          :title="triggerTitle(card)"
          data-spec="pipeline-trigger"
          @click="emit('trigger', card)"
        >
          <template #icon>
            <span :class="busyKey === `pipe:${card.id}` ? 'inline-flex animate-spin' : 'inline-flex'">
              <KIcon
                name="Play"
                :size="12"
              />
            </span>
          </template>
          {{ busyKey === `pipe:${card.id}` ? '触发中…' : '手动触发' }}
        </Btn>
        <Btn
          size="sm"
          :disabled="!!busyKey || copyingId === card.id"
          :aria-busy="copyingId === card.id"
          data-spec="pipeline-copy"
          title="向服务端取 Webhook 地址并复制。地址里有鉴权 Token，不会显示在列表上"
          @click="emit('copy', card)"
        >
          <template #icon>
            <span :class="copyingId === card.id ? 'inline-flex animate-spin' : 'inline-flex'">
              <KIcon
                name="Copy"
                :size="12"
              />
            </span>
          </template>
          {{ copyingId === card.id ? '复制中…' : '复制 Webhook' }}
        </Btn>
        <Btn
          size="sm"
          :disabled="!!busyKey"
          data-spec="pipeline-edit"
          title="修改名称、分支、编译节点、部署节点和是否自动部署"
          @click="emit('edit', card)"
        >
          编辑
        </Btn>
      </li>
    </ul>
  </Card>
</template>
