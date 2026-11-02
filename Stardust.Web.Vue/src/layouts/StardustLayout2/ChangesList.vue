<script setup lang="ts">
/**
 * ChangesList：跨应用「变更」时间线（停点 20）。纯 UI：只收 changes.logic 归一化好的 items，
 * 点行只发 select(item)，跳转由页面决定。
 * 三类状态分开显示：loading（spinner）、error（可见错误，不静默空态）、空态「最近没有变更记录」。
 * D=发布/部署（品牌色方块）、C=配置（灰方块）；成功/失败用 Tag 的 ok/err 语气，进行中不显示状态。
 * 样式只用 --el-* + Tailwind，无硬编码色值。
 */
import Card from './Card.vue';
import Tag from './Tag.vue';
import type { ChangeItem } from './changes.logic.ts';

withDefaults(
  defineProps<{
    items: ChangeItem[];
    loading?: boolean;
    error?: string;
  }>(),
  { loading: false, error: '' },
);

const emit = defineEmits<{ select: [item: ChangeItem] }>();
</script>

<template>
  <Card
    padding="none"
    data-spec="changes-list"
  >
    <div
      class="flex items-center gap-[8px] border-b border-[var(--el-border-color-light)] px-[14px] py-[10px] text-[13px] font-semibold text-[var(--el-text-color-primary)]"
    >
      跨应用变更
      <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">{{ items.length }} 条</span>
      <span class="flex-1" />
      <span class="font-mono text-[10.5px] font-normal uppercase tracking-[0.06em] text-[var(--el-text-color-secondary)]">
        D 发布 · C 配置
      </span>
    </div>

    <!-- 加载中 -->
    <div
      v-if="loading"
      data-spec="changes-loading"
      class="flex items-center gap-[8px] px-[14px] py-[18px] text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      <span class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
      变更加载中…
    </div>

    <!-- 失败：可见错误，不当作空态 -->
    <div
      v-else-if="error"
      data-spec="changes-failed"
      class="px-[14px] py-[14px] text-[12.5px] text-[var(--el-color-danger)]"
    >
      变更加载失败：{{ error }}
    </div>

    <!-- 空态 -->
    <div
      v-else-if="items.length === 0"
      data-spec="changes-empty"
      class="px-[14px] py-[18px] text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      最近没有变更记录
    </div>

    <!-- 时间线 -->
    <ul
      v-else
      class="m-0 list-none p-0"
    >
      <li
        v-for="it in items"
        :key="it.id"
        data-spec="changes-row"
        :data-kind="it.kind"
        :data-path="it.path"
        class="flex cursor-pointer items-center gap-[12px] border-b border-[var(--el-border-color-light)] px-[14px] py-[10px] transition-colors last:border-b-0 hover:bg-[var(--el-bg-color-overlay)]"
        @click="emit('select', it)"
      >
        <span class="w-[128px] shrink-0 font-mono text-[11.5px] tabular-nums text-[var(--el-text-color-secondary)]">
          {{ it.time }}
        </span>
        <span
          class="inline-flex h-[20px] w-[20px] shrink-0 items-center justify-center rounded-[5px] border font-mono text-[11px] font-semibold"
          :class="it.kind === 'D'
            ? 'border-[color-mix(in_oklab,var(--el-color-primary)_32%,transparent)] bg-[color-mix(in_oklab,var(--el-color-primary)_12%,transparent)] text-[var(--el-color-primary-light-3)]'
            : 'border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] text-[var(--el-text-color-secondary)]'"
        >
          {{ it.kind }}
        </span>
        <span class="w-[150px] shrink-0 truncate text-[12.5px] font-medium text-[var(--el-text-color-primary)]">
          {{ it.appName }}
        </span>
        <span
          class="min-w-0 flex-1 truncate text-[12.5px] text-[var(--el-text-color-secondary)]"
          :title="it.summary"
        >
          {{ it.summary }}
        </span>
        <span class="w-[80px] shrink-0 truncate text-right font-mono text-[11px] text-[var(--el-text-color-secondary)]">
          {{ it.operator || '—' }}
        </span>
        <Tag
          v-if="it.ok === true"
          tone="ok"
        >
          成功
        </Tag>
        <Tag
          v-else-if="it.ok === false"
          tone="err"
        >
          失败
        </Tag>
      </li>
    </ul>
  </Card>
</template>
