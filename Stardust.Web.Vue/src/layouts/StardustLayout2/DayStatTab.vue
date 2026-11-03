<script setup lang="ts">
/**
 * DayStatTab：应用工作台「监控大屏」页签。
 * 日统计双图（调用/错误折线 + 耗时）+ 最近 24h 分钟曲线切换 + RetryStat 批量重算。
 * 数据真实接口：GET /api/Monitors/AppDayStat?appId=&dtStart=&dtEnd=、
 *   GET /api/Monitors/AppMinuteStat?appId=&dtStart=&dtEnd=&pageSize=288（只读控制器，最近24h）、
 *   POST /api/Monitors/AppDayStat/RetryStat?keys=（SelectKeys 读 query 逗号串）。
 * 字段真源：Stardust.Data/Monitors/应用每日统计.cs、应用分钟统计.cs；行为真源见 daystat.logic.ts 头注释。
 * 图表复用 src/components/EChart.vue（白屏教训：禁止直接 import * as echarts）。
 * 颜色：EChart 是 canvas，令牌经 cssVar() 求值后注入 option（cssVar.ts）。
 * 交互反馈按 AGENTS.md §11：刷新/切换/重算按钮一律 loading + 禁用 + 结果 toast。
 */
import { computed, ref, watch } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import EChart from '@/components/EChart.vue';
import { cssVar } from '@/utils/cssVar';
import Btn from './Btn.vue';
import Card from './Card.vue';
import KIcon from './KIcon.vue';
import Tag from './Tag.vue';
import { openGuard } from './ui.store';
import { toast } from './useToast.ts';
import { useManualRefresh } from './useManualRefresh.ts';
import { H3, TD, TH } from './deployView.styles.ts';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import {
  dayCallsChartOption,
  dayCostChartOption,
  dayStatSummary,
  fetchDayStats,
  fetchMinuteStats,
  minuteChartOption,
  retryDayStat,
  type ChartPalette,
  type DayStatRow,
  type MinuteStatRow,
} from './daystat.logic.ts';

const props = defineProps<{
  appId: number;
  appName?: string;
  deps?: { request?: Req };
}>();

function req(): Req {
  return props.deps?.request ?? (request as Req);
}

/** 天数选择：7 / 30 */
const days = ref<7 | 30>(7);
/** 分钟曲线开关：开 = 追加最近 24h 分钟级曲线 */
const minuteOn = ref(false);

const rows = ref<DayStatRow[]>([]);
const minuteRows = ref<MinuteStatRow[]>([]);
const loading = ref(true);
const loadFailed = ref(false);
const failText = ref('');
let token = 0;

const summary = computed(() => dayStatSummary(rows.value));

async function load(): Promise<void> {
  const mine = ++token;
  loading.value = true;
  loadFailed.value = false;
  try {
    const list = await fetchDayStats(req(), props.appId, days.value);
    if (mine !== token) return;
    rows.value = list;
  } catch (e) {
    if (mine !== token) return;
    loadFailed.value = true;
    failText.value = e instanceof Error ? e.message : String(e);
    rows.value = [];
  } finally {
    if (mine === token) loading.value = false;
  }
}

async function loadMinute(): Promise<void> {
  if (!minuteOn.value) return;
  const mine = ++token;
  try {
    const list = await fetchMinuteStats(req(), props.appId, 24);
    if (mine !== token) return;
    minuteRows.value = list;
  } catch (e) {
    if (mine !== token) return;
    minuteRows.value = [];
    toast(`最近 24h 分钟曲线加载失败：${e instanceof Error ? e.message : String(e)}`, 'err');
  }
}

const { refreshing, refresh } = useManualRefresh({
  load: async () => {
    await load();
    await loadMinute();
  },
  outcome: () => ({
    loadError: loadFailed.value ? `监控大屏加载失败：${failText.value}` : '',
    versionsFailed: false,
    nodesFailed: false,
    historyFailed: false,
  }),
  notify: (ok, text) => toast(text, ok ? 'ok' : 'err'),
});

watch(
  () => props.appId,
  () => {
    rows.value = [];
    minuteRows.value = [];
    void load();
    void loadMinute();
  },
  { immediate: true },
);

watch(days, () => {
  void load();
});

function toggleMinute(): void {
  minuteOn.value = !minuteOn.value;
  if (minuteOn.value && minuteRows.value.length === 0) void loadMinute();
}

// ---------- RetryStat 批量重算 ----------
const retrying = ref(false);

function askRetry(): void {
  if (retrying.value) return;
  const ids = rows.value.map((r) => r.id);
  if (ids.length === 0) {
    toast('没有可重算的日统计行', 'err');
    return;
  }
  openGuard(`对「${props.appName || `应用 #${props.appId}`}」最近 ${days.value} 天的 ${ids.length} 条日统计重新计算（RetryStat）。确认？`, async () => {
    retrying.value = true;
    try {
      const msg = await retryDayStat(req(), ids);
      toast(`重算完成：${msg || '成功'}`, 'ok');
      await load();
      await loadMinute();
    } catch (e) {
      toast(`重算失败（${ids.length} 条日统计）：${e instanceof Error ? e.message : String(e)}`, 'err');
    } finally {
      retrying.value = false;
    }
  });
}

// ---------- 图表 ----------
const palette = computed<ChartPalette>(() => ({
  primary: cssVar('--el-color-primary', '#5B8DB8'),
  danger: cssVar('--el-color-danger', '#B08C6A'),
  cost: cssVar('--el-color-success', '#5F8D7E'),
  costRange: cssVar('--el-text-color-secondary', '#8E8E8E'),
  axis: cssVar('--el-text-color-secondary', '#8E8E8E'),
  splitLine: cssVar('--el-border-color-light', '#e4e7ed'),
}));

const callsOption = computed(() => dayCallsChartOption(rows.value, palette.value));
const costOption = computed(() => dayCostChartOption(rows.value, palette.value));
const minuteOption = computed(() => minuteChartOption(minuteRows.value, palette.value));

const card = 'rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] p-[12px]';
const sumLabel = 'text-[11px] text-[var(--el-text-color-secondary)]';
const sumValue = 'mt-[2px] text-[20px] font-semibold tabular-nums text-[var(--el-text-color-primary)]';
</script>

<template>
  <Card data-spec="daystat-tab">
    <div class="mb-[10px] flex flex-wrap items-center gap-2">
      <h3 :class="H3">
        监控大屏 · 日统计
      </h3>
      <span class="flex-1" />
      <select
        v-model.number="days"
        data-spec="daystat-days"
        class="h-[28px] cursor-pointer rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px] text-[12px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
        aria-label="统计天数"
      >
        <option :value="7">
          最近 7 天
        </option>
        <option :value="30">
          最近 30 天
        </option>
      </select>
      <Btn
        variant="default"
        size="sm"
        data-spec="daystat-minute-toggle"
        :aria-pressed="minuteOn"
        @click="toggleMinute"
      >
        <template #icon>
          <KIcon
            :name="minuteOn ? 'Check' : 'Activity'"
            :size="12"
          />
        </template>
        最近24h分钟
      </Btn>
      <Btn
        variant="default"
        size="sm"
        data-spec="daystat-retry"
        :disabled="retrying || refreshing"
        :title="rows.length === 0 ? '没有可重算的日统计行' : `对最近 ${days} 天重新统计（RetryStat）`"
        @click="askRetry"
      >
        <template #icon>
          <span
            v-if="retrying"
            class="inline-flex animate-spin"
          >
            <span class="h-[12px] w-[12px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-white)]" />
          </span>
        </template>
        批量重算
      </Btn>
      <Btn
        variant="primary"
        size="sm"
        data-spec="daystat-refresh"
        :disabled="refreshing"
        @click="refresh"
      >
        <template #icon>
          <span :class="{ 'animate-spin': refreshing }">
            <KIcon
              name="Refresh"
              :size="12"
            />
          </span>
        </template>
        刷新
      </Btn>
    </div>

    <div
      v-if="loadFailed"
      data-spec="daystat-failed"
      class="py-6 text-[12.5px] text-[var(--el-color-danger)]"
    >
      监控大屏加载失败：{{ failText }}
      <Btn
        variant="default"
        size="sm"
        class="ml-2"
        data-spec="daystat-retryload"
        @click="refresh"
      >
        重试
      </Btn>
    </div>

    <div
      v-else-if="loading && rows.length === 0"
      data-spec="daystat-loading"
      class="flex items-center gap-2 py-6 text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      <span class="inline-flex animate-spin">
        <span class="h-[12px] w-[12px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
      </span>
      正在加载日统计…
    </div>

    <div
      v-else-if="rows.length === 0"
      data-spec="daystat-empty"
      class="py-6 text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      「{{ props.appName || `应用 #${props.appId}` }}」在所选时间范围还没有日统计数据。应用上报监控后，统计每 5 分钟刷新一次。
    </div>

    <template v-else>
      <div
        class="mb-[12px] grid grid-cols-2 gap-[10px] md:grid-cols-4"
        data-spec="daystat-summary"
      >
        <div :class="card">
          <div :class="sumLabel">
            调用总数
          </div>
          <div
            :class="sumValue"
            data-spec="daystat-total"
          >
            {{ summary.total }}
          </div>
        </div>
        <div :class="card">
          <div :class="sumLabel">
            错误数
          </div>
          <div
            :class="sumValue"
            data-spec="daystat-errors"
          >
            {{ summary.errors }}
          </div>
        </div>
        <div :class="card">
          <div :class="sumLabel">
            平均耗时 (ms)
          </div>
          <div
            :class="sumValue"
            data-spec="daystat-avgcost"
          >
            {{ summary.avgCost }}
          </div>
        </div>
        <div :class="card">
          <div :class="sumLabel">
            错误率
          </div>
          <div
            :class="sumValue"
            data-spec="daystat-errorrate"
          >
            {{ summary.errorRate }}%
          </div>
        </div>
      </div>

      <div class="grid grid-cols-1 gap-[12px] xl:grid-cols-2">
        <div :class="card">
          <div class="mb-[6px] text-[12.5px] font-medium text-[var(--el-text-color-primary)]">
            调用 / 错误（最近 {{ days }} 天）
          </div>
          <EChart
            :option="callsOption"
            height="260px"
          />
        </div>
        <div :class="card">
          <div class="mb-[6px] text-[12.5px] font-medium text-[var(--el-text-color-primary)]">
            耗时（平均 / 最大 / 最小，ms）
          </div>
          <EChart
            :option="costOption"
            height="260px"
          />
        </div>
      </div>

      <div
        v-if="minuteOn"
        :class="card"
        class="mt-[12px]"
      >
        <div class="mb-[6px] flex items-center gap-2">
          <span class="text-[12.5px] font-medium text-[var(--el-text-color-primary)]">最近 24h 分钟级（5 分钟粒度）</span>
          <span
            v-if="minuteRows.length === 0"
            class="text-[12px] text-[var(--el-text-color-secondary)]"
          >
            暂无分钟数据
          </span>
          <span class="flex-1" />
          <Tag tone="default">
            {{ minuteRows.length }} 个点
          </Tag>
        </div>
        <EChart
          v-if="minuteRows.length > 0"
          :option="minuteOption"
          height="220px"
        />
        <div
          v-else
          data-spec="daystat-minute-empty"
          class="py-4 text-center text-[12px] text-[var(--el-text-color-secondary)]"
        >
          该应用最近 24h 没有分钟级统计。应用上报监控后，这里会画出调用与错误的分钟曲线。
        </div>
      </div>

      <div class="mt-[12px] overflow-x-auto">
        <table class="w-full border-collapse">
          <thead>
            <tr>
              <th :class="TH">
                统计日期
              </th>
              <th :class="TH">
                总次数
              </th>
              <th :class="TH">
                错误数
              </th>
              <th :class="TH">
                错误率
              </th>
              <th :class="TH">
                平均耗时(ms)
              </th>
              <th :class="TH">
                最大耗时(ms)
              </th>
              <th :class="TH">
                埋点数
              </th>
              <th :class="TH">
                接口
              </th>
              <th :class="TH">
                Http
              </th>
              <th :class="TH">
                数据库
              </th>
              <th :class="TH">
                MQ
              </th>
              <th :class="TH">
                Redis
              </th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="r in rows"
              :key="r.id"
              class="group"
              data-spec="daystat-row"
              :data-stat-date="r.statDate"
            >
              <td :class="TD">
                {{ r.statDate }}
              </td>
              <td
                :class="TD"
                class="tabular-nums"
              >
                {{ r.total }}
              </td>
              <td
                :class="TD"
                class="tabular-nums"
              >
                {{ r.errors }}
              </td>
              <td
                :class="TD"
                class="tabular-nums"
              >
                {{ r.errorRate }}%
              </td>
              <td
                :class="TD"
                class="tabular-nums"
              >
                {{ r.cost }}
              </td>
              <td
                :class="TD"
                class="tabular-nums"
              >
                {{ r.maxCost }}
              </td>
              <td
                :class="TD"
                class="tabular-nums"
              >
                {{ r.names }}
              </td>
              <td
                :class="TD"
                class="tabular-nums"
              >
                {{ r.apis }}
              </td>
              <td
                :class="TD"
                class="tabular-nums"
              >
                {{ r.https }}
              </td>
              <td
                :class="TD"
                class="tabular-nums"
              >
                {{ r.dbs }}
              </td>
              <td
                :class="TD"
                class="tabular-nums"
              >
                {{ r.mqs }}
              </td>
              <td
                :class="TD"
                class="tabular-nums"
              >
                {{ r.redis }}
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>
  </Card>
</template>
