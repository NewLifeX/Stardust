<script setup lang="ts">
/**
 * AppRow：应用列表行，对照原型 kit-ui.jsx 的 AppRow 与 kit.css 的 .x-arow（高 52、悬停浮出快捷操作条）。
 * 纯展示：点击整行发出 open('overview')，快捷按钮发出 open('versions' | 'config' | 'logs')。
 * 构建状态小标：构建中（primary）/失败（danger），成功不显示；数据由父行注入（buildStatus）。
 * 趋势小图：60×20 SVG sparkline，数据来自 row.trend（AppDayStat 最近 14 天），不足 2 天不渲染。
 * 悬停或键盘聚焦时，右侧指标与小图隐藏、快捷按钮浮出。
 */
import { computed } from 'vue';
import type { AppAggRow } from '../Stardust/appsModel';
import { statusLabel } from '../Stardust/appsModel';
import Btn from '@/layouts/StardustLayout2/Btn.vue';
import Dot from '@/layouts/StardustLayout2/Dot.vue';
import { avatarInitial } from '@/layouts/StardustLayout2/avatar.logic';
import {
  avatarTone,
  dotToneOf,
  formatErrorRate,
  isErrorRateHigh,
  rowSubtitle,
  sparklinePath,
} from '@/layouts/StardustLayout2/apps.logic';
import type { AppTab } from '@/layouts/StardustLayout2/palette.items';

const props = withDefaults(
  defineProps<{
    row: AppAggRow;
    /** F12 批量操作：显示行首勾选框 */
    selectable?: boolean;
    selected?: boolean;
  }>(),
  { selectable: false, selected: false },
);
const emit = defineEmits<{ open: [tab: AppTab]; toggle: [id: number]; edit: [row: AppAggRow] }>();

const name = computed(() => props.row.name ?? '');
const title = computed(() => props.row.displayName || name.value);
const high = computed(() => isErrorRateHigh(props.row.metric));

/** 构建状态小标文案与语气 */
const buildTag = computed(() => {
  const s = props.row.buildStatus;
  if (s === 'building') return { text: '构建中', tone: 'primary' as const };
  if (s === 'failed') return { text: '失败', tone: 'danger' as const };
  return null;
});

/** 趋势小图 path：取 row.trend 的 total 序列，少于 2 个点返回空串 */
const trendPath = computed(() => sparklinePath((props.row.trend ?? []).map((p) => p.total), 60, 20));
</script>

<template>
  <div
    role="button"
    tabindex="0"
    data-spec="app-row"
    :data-app="name"
    class="group relative flex h-[52px] cursor-pointer items-center gap-[12px] border-b border-[var(--el-border-color-light)] px-[14px] outline-none transition-colors last:border-b-0 hover:bg-[var(--el-fill-color-light)] focus-visible:bg-[var(--el-fill-color-light)]"
    @click="emit('open', 'overview')"
    @keydown.enter.self="emit('open', 'overview')"
  >
    <input
      v-if="props.selectable"
      type="checkbox"
      :checked="props.selected"
      data-spec="app-row-check"
      :aria-label="`选择应用 ${name}`"
      class="h-[14px] w-[14px] flex-none accent-[var(--el-color-primary)]"
      @click.stop
      @change="props.row.id !== undefined && emit('toggle', props.row.id)"
    >
    <span
      class="grid h-[26px] w-[26px] flex-none place-items-center rounded-[6px] text-[12px] font-semibold text-[var(--el-color-white)]"
      :style="{ background: avatarTone(name) }"
    >
      {{ avatarInitial(name, '?') }}
    </span>

    <div class="min-w-0 flex-1">
      <div class="flex items-center gap-[6px]">
        <b class="truncate text-[13px] font-medium text-[var(--el-text-color-primary)]">{{ title }}</b>
        <span
          v-if="buildTag"
          data-spec="app-row-build-tag"
          :class="[
            'flex-none rounded-[4px] px-[4px] py-[1px] text-[10px] font-medium leading-[14px]',
            buildTag.tone === 'primary'
              ? 'text-[var(--el-color-primary)] bg-[color-mix(in_srgb,var(--el-color-primary)_12%,transparent)]'
              : 'text-[var(--el-color-danger)] bg-[color-mix(in_srgb,var(--el-color-danger)_12%,transparent)]',
          ]"
        >
          {{ buildTag.text }}
        </span>
      </div>
      <small class="block truncate text-[11px] text-[var(--el-text-color-secondary)]">{{ rowSubtitle(row) }}</small>
    </div>

    <span class="flex shrink-0 items-center gap-[10px] group-hover:opacity-0 group-focus-within:opacity-0">
      <svg
        v-if="trendPath"
        data-spec="app-row-trend"
        width="60"
        height="20"
        class="flex-none"
        aria-hidden="true"
      >
        <path
          :d="trendPath"
          fill="none"
          stroke="var(--el-color-primary)"
          stroke-width="1.2"
          stroke-linejoin="round"
          stroke-linecap="round"
        />
      </svg>
      <span
        data-spec="app-row-err"
        :class="[
          'font-mono text-[12px]',
          high ? 'text-[var(--el-color-danger)]' : 'text-[var(--el-text-color-secondary)]',
        ]"
        :title="row.metric ? '最近一分钟错误率' : '暂无监控统计'"
      >
        {{ formatErrorRate(row.metric) }}
      </span>
      <Dot
        :tone="dotToneOf(row.status)"
        :title="statusLabel[row.status]"
      />
    </span>

    <span
      class="absolute right-[10px] top-1/2 hidden -translate-y-1/2 gap-[4px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] p-[3px] group-hover:flex group-focus-within:flex"
      @click.stop
    >
      <Btn
        size="sm"
        data-spec="app-row-versions"
        @click="emit('open', 'versions')"
      >
        发布
      </Btn>
      <Btn
        size="sm"
        data-spec="app-row-config"
        @click="emit('open', 'config')"
      >
        配置
      </Btn>
      <Btn
        size="sm"
        data-spec="app-row-logs"
        @click="emit('open', 'logs')"
      >
        日志
      </Btn>
      <!-- F12-1：编辑应用入口（AppFormDrawer 编辑态，Name 只读、Secret 留空=不修改） -->
      <Btn
        size="sm"
        variant="ghost"
        data-spec="app-row-edit"
        @click="emit('edit', props.row)"
      >
        编辑
      </Btn>
    </span>
  </div>
</template>
