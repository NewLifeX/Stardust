<script setup lang="ts">
/**
 * AppRuleTable：/configs 页「应用规则」区块（F11）。纯 UI + 内部勾选无；收 rows 做表格，
 * 操作全部 emit 给容器（Configs.vue）发请求：新增/编辑/删除/启停切换。
 * 三态：loading / error+重试 / 空态「还没有应用规则」。空态引导新增。
 */
import Btn from './Btn.vue';
import KIcon from './KIcon.vue';
import Tag from './Tag.vue';
import { H3, TD, TH } from './deployView.styles.ts';
import type { AppRuleRow } from './configCenter.logic.ts';

defineProps<{
  rows: AppRuleRow[];
  loading: boolean;
  error: string;
  /** 正在删除的行 id（删除中该行按钮写「删除中…」） */
  removingId: number;
  busy: boolean;
}>();

const emit = defineEmits<{ retry: []; create: []; edit: [row: AppRuleRow]; remove: [row: AppRuleRow] }>();
</script>

<template>
  <div
    class="mt-5"
    data-spec="app-rule-block"
  >
    <div class="mb-[10px] flex flex-wrap items-center gap-[10px]">
      <h3 :class="H3">
        应用规则
        <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">
          按 IP 等条件给应用划分作用域（全局规则，命中即生效）
        </span>
      </h3>
      <span class="flex-1" />
      <Btn
        size="sm"
        :disabled="busy"
        data-spec="app-rule-create"
        @click="emit('create')"
      >
        <template #icon>
          <KIcon
            name="Plus"
            :size="12"
          />
        </template>
        新增规则
      </Btn>
    </div>

    <div
      v-if="loading"
      data-spec="app-rule-loading"
      class="flex items-center gap-[8px] rounded-[10px] border border-[var(--el-border-color-light)] px-[14px] py-[16px] text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      <span class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
      应用规则加载中…
    </div>

    <div
      v-else-if="error"
      data-spec="app-rule-failed"
      class="flex items-center gap-[10px] rounded-[10px] border border-[var(--el-border-color-light)] px-[14px] py-[14px]"
    >
      <span class="text-[12.5px] text-[var(--el-color-danger)]">应用规则加载失败：{{ error }}</span>
      <Btn
        size="sm"
        data-spec="app-rule-retry"
        @click="emit('retry')"
      >
        重试
      </Btn>
    </div>

    <div
      v-else-if="rows.length === 0"
      data-spec="app-rule-empty"
      class="rounded-[10px] border border-[var(--el-border-color-light)] px-[14px] py-[16px] text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      还没有应用规则。需要按 IP 段划分生产/测试作用域时，点「新增规则」创建。
    </div>

    <table
      v-else
      class="w-full border-collapse overflow-hidden rounded-[10px] border border-[var(--el-border-color-light)]"
    >
      <thead>
        <tr>
          <th :class="[TH, 'w-[38%]']">
            规则表达式
          </th>
          <th :class="TH">
            命中结果
          </th>
          <th :class="[TH, 'w-[90px] text-right']">
            优先级
          </th>
          <th :class="[TH, 'w-[90px]']">
            状态
          </th>
          <th :class="[TH, 'w-[150px]']">
            更新时间
          </th>
          <th :class="[TH, 'w-[130px] text-right']">
            操作
          </th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="r in rows"
          :key="r.id"
          data-spec="app-rule-row"
          :data-rule-id="r.id"
          class="hover:bg-[var(--el-bg-color-overlay)]"
        >
          <td :class="[TD, 'font-mono text-[12px] text-[var(--el-text-color-primary)]']">
            {{ r.rule }}
          </td>
          <td :class="[TD, 'font-mono text-[12px] text-[var(--el-text-color-secondary)]']">
            {{ r.result }}
          </td>
          <td :class="[TD, 'text-right font-mono tabular-nums text-[12px]']">
            {{ r.priority }}
          </td>
          <td :class="TD">
            <Tag :tone="r.enable ? 'ok' : 'default'">
              {{ r.enable ? '启用' : '停用' }}
            </Tag>
          </td>
          <td :class="[TD, 'font-mono text-[11.5px] tabular-nums text-[var(--el-text-color-secondary)]']">
            {{ r.updateTimeText }}
          </td>
          <td :class="[TD, 'text-right']">
            <Btn
              size="sm"
              variant="ghost"
              :disabled="busy"
              data-spec="app-rule-edit"
              @click="emit('edit', r)"
            >
              编辑
            </Btn>
            <Btn
              size="sm"
              variant="ghost"
              :disabled="busy"
              :aria-busy="removingId === r.id"
              data-spec="app-rule-remove"
              class="!text-[var(--el-color-danger)]"
              @click="emit('remove', r)"
            >
              {{ removingId === r.id ? '删除中…' : '删除' }}
            </Btn>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>
