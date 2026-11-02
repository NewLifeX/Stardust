<script setup lang="ts">
/**
 * ConfigCenterList：跨应用配置中心列表（/configs）。纯 UI：收归一好的 rows，
 * 内部做搜索 + 应用过滤（filterRows 纯函数），点行发 open(configId)，打开详情 Drawer。
 * 三态分开：loading（spinner）、error（可见错误 + 重试）、空态「还没有应用配置」。
 * 状态 Tag：有待发布草稿=橙色「待发布」，否则绿色「已发布」。样式只用 --el-* + Tailwind。
 */
import { computed, ref } from 'vue';
import Card from './Card.vue';
import Tag from './Tag.vue';
import Btn from './Btn.vue';
import KIcon from './KIcon.vue';
import { SELECT, TD, TH } from './deployView.styles.ts';
import { filterRows } from './configCenter.logic.ts';
import type { ConfigCenterRow } from './configCenter.logic.ts';

const props = withDefaults(
  defineProps<{
    rows: ConfigCenterRow[];
    loading?: boolean;
    error?: string;
  }>(),
  { loading: false, error: '' },
);

const emit = defineEmits<{
  open: [configId: number];
  retry: [];
}>();

const keyword = ref('');
const appFilter = ref(0); // 0 = 全部应用

/** 过滤后的行（搜索 + 应用下拉，AND） */
const filtered = computed(() =>
  filterRows(props.rows, { keyword: keyword.value, appId: appFilter.value }),
);

/** 应用下拉选项：从已有配置的行里去重（appId, appName） */
const appOptions = computed(() => {
  const seen = new Map<number, string>();
  for (const r of props.rows) {
    if (!seen.has(r.appId)) seen.set(r.appId, r.appName);
  }
  return [...seen.entries()].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name));
});
</script>

<template>
  <Card
    padding="none"
    data-spec="config-center"
  >
    <!-- 工具条：标题 + 计数 + 搜索 + 应用过滤 -->
    <div class="flex flex-wrap items-center gap-[10px] border-b border-[var(--el-border-color-light)] px-[14px] py-[10px]">
      <span class="text-[13px] font-semibold text-[var(--el-text-color-primary)]">
        跨应用配置
      </span>
      <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">{{ rows.length }} 条</span>
      <span class="flex-1" />

      <span class="relative">
        <KIcon
          name="Search"
          :size="13"
          class="pointer-events-none absolute left-[9px] top-1/2 -translate-y-1/2 text-[var(--el-text-color-placeholder)]"
        />
        <input
          v-model="keyword"
          type="text"
          placeholder="搜索应用 / 配置名"
          data-spec="config-search"
          class="h-[28px] w-[200px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] pl-[28px] pr-[10px] text-[12px] text-[var(--el-text-color-primary)] outline-none placeholder:text-[var(--el-text-color-placeholder)] focus:border-[var(--el-color-primary)]"
        >
      </span>

      <select
        v-model.number="appFilter"
        :class="SELECT"
        data-spec="config-app-filter"
      >
        <option :value="0">
          全部应用
        </option>
        <option
          v-for="a in appOptions"
          :key="a.id"
          :value="a.id"
        >
          {{ a.name }}
        </option>
      </select>
    </div>

    <!-- 加载中 -->
    <div
      v-if="loading"
      data-spec="config-loading"
      class="flex items-center gap-[8px] px-[14px] py-[18px] text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      <span class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
      配置加载中…
    </div>

    <!-- 失败：可见错误 + 重试，不当作空态 -->
    <div
      v-else-if="error"
      data-spec="config-failed"
      class="flex items-center gap-[10px] px-[14px] py-[14px]"
    >
      <span class="text-[12.5px] text-[var(--el-color-danger)]">配置加载失败：{{ error }}</span>
      <Btn
        size="sm"
        data-spec="config-retry"
        @click="emit('retry')"
      >
        重试
      </Btn>
    </div>

    <!-- 空态 -->
    <div
      v-else-if="rows.length === 0"
      data-spec="config-empty"
      class="px-[14px] py-[18px] text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      还没有应用配置
    </div>

    <!-- 过滤后空态（有数据但被筛没了） -->
    <div
      v-else-if="filtered.length === 0"
      data-spec="config-no-match"
      class="px-[14px] py-[18px] text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      没有匹配「{{ keyword }}」的应用配置
    </div>

    <!-- 表格 -->
    <table
      v-else
      class="w-full border-collapse"
    >
      <thead>
        <tr>
          <th :class="TH">
            应用
          </th>
          <th :class="TH">
            配置名
          </th>
          <th :class="[TH, 'w-[80px]']">
            Key 数
          </th>
          <th :class="[TH, 'w-[110px]']">
            状态
          </th>
          <th :class="[TH, 'w-[140px]']">
            最近更新
          </th>
          <th :class="[TH, 'w-[40px]']" />
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="row in filtered"
          :key="row.configId"
          data-spec="config-row"
          :data-config-id="row.configId"
          class="group cursor-pointer transition-colors hover:bg-[var(--el-bg-color-overlay)]"
          @click="emit('open', row.configId)"
        >
          <td :class="TD">
            <span class="font-medium text-[var(--el-text-color-primary)]">{{ row.appName }}</span>
          </td>
          <td :class="[TD, 'text-[var(--el-text-color-secondary)]']">
            {{ row.configName }}
          </td>
          <td :class="[TD, 'font-mono text-[12px] tabular-nums text-[var(--el-text-color-secondary)]']">
            {{ row.keyCount }}
          </td>
          <td :class="TD">
            <Tag
              v-if="row.pending"
              tone="warn"
            >
              待发布
            </Tag>
            <Tag
              v-else
              tone="ok"
            >
              已发布
            </Tag>
          </td>
          <td :class="[TD, 'font-mono text-[11.5px] tabular-nums text-[var(--el-text-color-secondary)]']">
            {{ row.lastPublishText }}
          </td>
          <td :class="[TD, 'text-right']">
            <KIcon
              name="ChevronRight"
              :size="14"
              class="text-[var(--el-text-color-placeholder)]"
            />
          </td>
        </tr>
      </tbody>
    </table>
  </Card>
</template>
