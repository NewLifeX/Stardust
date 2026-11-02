<script setup lang="ts">
/**
 * AttentionList：应用概览的「需关注事项」，2026-10-29 停点 19 第 7 步。
 * 纯 UI：只收 buildAttention 算好的 items，点按钮只发 go(tab)，跳转由页面决定。
 * 语气映射：ok 绿点、warn 黄点（详情用警告色）、info 灰点、pending 空心圈加「待接入」标签。
 * 取不到数据时显示「待接入」，不当作正常，也不写成没有记录。
 */
import Btn from './Btn.vue';
import Card from './Card.vue';
import Dot from './Dot.vue';
import Tag from './Tag.vue';
import type { AttentionItem, AttentionTone } from './overview.logic.ts';
import type { AppTab } from './palette.items.ts';

defineProps<{ items: AttentionItem[] }>();
const emit = defineEmits<{ go: [tab: AppTab] }>();

type DotTone = 'default' | 'ok' | 'err' | 'warn' | 'off';

const DOT: Record<AttentionTone, DotTone> = {
  ok: 'ok',
  warn: 'warn',
  info: 'default',
  pending: 'off',
};

// 详情文字色：只有 warn 用警告色，其余用次要文字色
const DETAIL: Record<AttentionTone, string> = {
  ok: 'text-[var(--el-text-color-secondary)]',
  warn: 'text-[var(--el-color-warning)]',
  info: 'text-[var(--el-text-color-secondary)]',
  pending: 'text-[var(--el-text-color-secondary)]',
};
</script>

<template>
  <Card
    padding="none"
    data-spec="attention-list"
  >
    <div
      class="border-b border-[var(--el-border-color-light)] px-[14px] py-[10px] text-[13px] font-semibold text-[var(--el-text-color-primary)]"
    >
      需关注事项
    </div>
    <ul class="m-0 list-none p-0">
      <li
        v-for="it in items"
        :key="it.key"
        data-spec="attention-item"
        :data-key="it.key"
        :data-tone="it.tone"
        class="flex items-center gap-3 border-b border-[var(--el-border-color-light)] px-[14px] py-[10px] last:border-b-0"
      >
        <Dot :tone="DOT[it.tone]" />
        <span class="w-[72px] shrink-0 text-[12.5px] font-medium text-[var(--el-text-color-primary)]">{{ it.title }}</span>
        <span
          data-spec="attention-detail"
          class="min-w-0 flex-1 break-words text-[12.5px]"
          :class="DETAIL[it.tone]"
        >
          {{ it.detail }}
        </span>
        <Tag
          v-if="it.tone === 'pending'"
          data-spec="attention-pending"
        >
          待接入
        </Tag>
        <Btn
          size="sm"
          data-spec="attention-go"
          @click="emit('go', it.tab)"
        >
          {{ it.actionLabel }}
        </Btn>
      </li>
    </ul>
  </Card>
</template>
