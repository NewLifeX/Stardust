<script setup lang="ts">
/**
 * DeployHistory：发布页「部署历史」，对应 ShipPanel 原模板的第 4 块（2026-10-28 拆分）。
 * 纯展示：只收 props、不发事件。按节点筛选（historyNode）和点行展开（openHistoryId）是纯展示状态，留在本组件内。
 * history 为 null 表示加载失败，与「暂无记录」分开显示。
 */
import { computed, ref } from 'vue';
import Card from './Card.vue';
import Tag from './Tag.vue';
import { H3, SELECT, TD, TH } from './deployView.styles.ts';
import { filterHistoryByNode } from './ship.logic.ts';
import type { HistoryRow, ShipNode } from './ship.logic.ts';

const props = defineProps<{
  /** null 表示加载失败 */
  history: HistoryRow[] | null;
  /** 只用来生成「按节点筛选」下拉 */
  nodes: ShipNode[];
}>();

const historyNode = ref(0);
const openHistoryId = ref(0);

const shown = computed(() => filterHistoryByNode(props.history ?? [], historyNode.value));

function toggle(id: number): void {
  openHistoryId.value = openHistoryId.value === id ? 0 : id;
}
</script>

<template>
  <Card
    padding="none"
    data-spec="ship-history"
  >
    <div class="px-[18px] pb-2 pt-4">
      <h3 :class="H3">
        部署历史
        <span class="flex-1" />
        <select
          v-model.number="historyNode"
          :class="SELECT"
          data-spec="ship-history-node"
        >
          <option :value="0">
            全部节点
          </option>
          <option
            v-for="n in nodes"
            :key="n.id"
            :value="n.nodeId"
          >
            {{ n.name }}
          </option>
        </select>
      </h3>
      <div class="text-[11.5px] leading-[1.6] text-[var(--el-text-color-secondary)]">
        这是下发记录：「成功」只表示命令下发时没有报错，不代表节点已经装好。按节点筛选只作用于已取回的最近 {{ history?.length ?? 0 }} 条；流水线写的记录没有节点，筛选时不会出现。
      </div>
    </div>
    <table class="w-full border-collapse">
      <thead>
        <tr>
          <th :class="TH">
            时间
          </th>
          <th :class="TH">
            节点
          </th>
          <th :class="TH">
            动作
          </th>
          <th :class="TH">
            下发
          </th>
          <th :class="TH">
            备注
          </th>
        </tr>
      </thead>
      <tbody>
        <template
          v-for="h in shown"
          :key="h.id"
        >
          <tr
            data-spec="ship-history-row"
            class="group cursor-pointer transition-colors duration-[120ms] hover:bg-[var(--el-bg-color-overlay)]"
            @click="toggle(h.id)"
          >
            <td :class="[TD, 'font-mono tabular-nums text-[var(--el-text-color-secondary)]']">
              {{ h.time }}
            </td>
            <td :class="TD">
              {{ h.node }}
            </td>
            <td :class="TD">
              {{ h.actionLabel }}
            </td>
            <td :class="TD">
              <Tag :tone="h.success ? 'ok' : 'err'">
                {{ h.success ? '已下发' : '下发失败' }}
              </Tag>
            </td>
            <td :class="[TD, 'max-w-[320px] truncate text-[var(--el-text-color-secondary)]']">
              {{ h.remark || '—' }}
            </td>
          </tr>
          <tr
            v-if="openHistoryId === h.id"
            data-spec="ship-history-detail"
          >
            <td
              colspan="5"
              class="border-b border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[14px] py-3 text-[12px] leading-[1.7]"
            >
              <div class="whitespace-pre-wrap break-words text-[var(--el-text-color-primary)]">
                {{ h.remark || '（无备注）' }}
              </div>
              <div class="mt-1 font-mono text-[11px] text-[var(--el-text-color-secondary)]">
                动作 {{ h.action || '—' }} · TraceId {{ h.traceId || '—' }} · 来源 {{ h.ip || '—' }}
              </div>
            </td>
          </tr>
        </template>
        <tr v-if="history === null">
          <td
            colspan="5"
            class="p-5 text-[12.5px] text-[var(--el-color-danger)]"
            data-spec="ship-history-failed"
          >
            部署历史加载失败，请点上方「刷新」重试
          </td>
        </tr>
        <tr v-else-if="!shown.length">
          <td
            colspan="5"
            class="p-5 text-[12.5px] text-[var(--el-text-color-secondary)]"
            data-spec="ship-history-empty"
          >
            {{ historyNode ? '最近这一页里没有该节点的记录' : '暂无部署历史' }}
          </td>
        </tr>
      </tbody>
    </table>
  </Card>
</template>
