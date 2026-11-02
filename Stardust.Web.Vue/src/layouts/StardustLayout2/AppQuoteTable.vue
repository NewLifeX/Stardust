<script setup lang="ts">
/**
 * AppQuoteTable：/configs「应用依赖」区块表格（F15）。
 * 纯展示三态（loading/failed/empty）+ 行操作 emit（create/edit/remove），
 * 行内应用名由逻辑层按 appId→应用名映射注入；AppQuote.Kind 实体无枚举，原样展示数字。
 */
import Btn from './Btn.vue';
import Card from './Card.vue';
import KIcon from './KIcon.vue';
import Tag from './Tag.vue';
import { H3, TD, TH } from './deployView.styles.ts';
import type { AppQuoteRow } from './configCenter.logic.ts';

defineProps<{
  rows: AppQuoteRow[];
  loading: boolean;
  /** 非空=加载失败文案 */
  error: string;
  removingId: number | null;
  busy: boolean;
}>();

const emit = defineEmits<{
  retry: [];
  create: [];
  edit: [row: AppQuoteRow];
  remove: [row: AppQuoteRow];
}>();
</script>

<template>
  <Card
    padding="none"
    class="mt-4"
    data-spec="app-quote-panel"
  >
    <div class="flex items-center gap-[10px] border-b border-[var(--el-border-color-light)] px-[18px] py-3">
      <h3 :class="H3">
        应用依赖
        <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">
          应用引用其它应用（AppQuote），编辑此处即改全局依赖表
        </span>
      </h3>
      <span class="flex-1" />
      <Btn
        size="sm"
        variant="primary"
        :disabled="busy"
        :aria-busy="busy"
        data-spec="app-quote-create"
        @click="emit('create')"
      >
        新增依赖
      </Btn>
    </div>

    <div
      v-if="loading"
      class="py-10 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
      data-spec="app-quote-loading"
    >
      依赖加载中…
    </div>
    <div
      v-else-if="error"
      class="py-10 text-center text-[12.5px] text-[var(--el-color-danger)]"
      data-spec="app-quote-failed"
    >
      {{ error }}
      <Btn
        size="sm"
        variant="ghost"
        data-spec="app-quote-retry"
        @click="emit('retry')"
      >
        <template #icon>
          <span class="inline-flex">
            <KIcon
              name="Refresh"
              :size="12"
            />
          </span>
        </template>
        重试
      </Btn>
    </div>

    <table
      v-else
      class="w-full border-collapse"
    >
      <thead>
        <tr>
          <th :class="TH">
            依赖方
          </th>
          <th :class="TH">
            被依赖
          </th>
          <th :class="[TH, 'w-[72px]']">
            类型
          </th>
          <th :class="[TH, 'w-[72px]']">
            启用
          </th>
          <th :class="[TH, 'w-[120px]']">
            操作
          </th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="r in rows"
          :key="r.id"
          data-spec="app-quote-row"
          :data-quote-id="r.id"
        >
          <td :class="[TD, 'font-mono text-[12px] text-[var(--el-text-color-primary)]']">
            {{ r.appName }}
          </td>
          <td :class="[TD, 'font-mono text-[12px] text-[var(--el-text-color-primary)]']">
            {{ r.targetName }}
          </td>
          <td :class="[TD, 'font-mono text-[12px]']">
            {{ r.kind }}
          </td>
          <td :class="TD">
            <Tag :tone="r.enable ? 'ok' : 'default'">
              {{ r.enable ? '启用' : '停用' }}
            </Tag>
          </td>
          <td :class="TD">
            <div class="flex items-center gap-1">
              <Btn
                size="sm"
                variant="ghost"
                data-spec="app-quote-edit"
                @click="emit('edit', r)"
              >
                编辑
              </Btn>
              <Btn
                size="sm"
                variant="ghost"
                :disabled="removingId === r.id"
                data-spec="app-quote-remove"
                @click="emit('remove', r)"
              >
                {{ removingId === r.id ? '删除中…' : '删除' }}
              </Btn>
            </div>
          </td>
        </tr>
        <tr v-if="!rows.length">
          <td
            colspan="5"
            class="p-6 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
            data-spec="app-quote-empty"
          >
            还没有应用依赖记录，点「新增依赖」声明应用之间的引用关系
          </td>
        </tr>
      </tbody>
    </table>
  </Card>
</template>
