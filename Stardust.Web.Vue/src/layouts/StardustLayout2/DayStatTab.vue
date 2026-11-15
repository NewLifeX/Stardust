<script setup lang="ts">
/**
 * DayStatTab：应用工作台「监控大屏」页签。
 * 日统计双图（调用/错误折线 + 耗时）+ 最近 24h 分钟曲线切换 + RetryStat 批量重算
 * + 埋点统计区块（TraceStatCard，CARD-MON-07；表格接口/Http/数据库/MQ/Redis 列行内链接 C17 列链钻取）。
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
import TraceStatCard from './TraceStatCard.vue';
import { openGuard } from './ui.store';
import { toast } from './useToast.ts';
import { useManualRefresh } from './useManualRefresh.ts';
import { H3, TD, TH } from './deployView.styles.ts';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import {
  dayCallsChartOption,
  dayCostChartOption,
  dayStatSummary,
  dayStatTotalRow,
  fetchDayStats,
  fetchDayStatsRange,
  fetchMinuteStats,
  fmtErrorRate,
  minuteChartOption,
  retryDayStat,
  createLoadGuard,
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

/** 天数选择：7 / 30（选自定义区间时下拉置「自定义」并禁用） */
const days = ref<7 | 30>(7);
/** 自定义日期区间（CARD-MON-04）：两端都选中后按区间重拉，清除恢复 7/30 预设 */
const rangeStart = ref('');
const rangeEnd = ref('');
/** 分钟曲线开关：开 = 追加最近 24h 分钟级曲线 */
const minuteOn = ref(false);

const rangeActive = computed(() => rangeStart.value !== '' && rangeEnd.value !== '');
const rangeLabel = computed(() => (rangeActive.value ? `自定义区间 ${rangeStart.value} ~ ${rangeEnd.value}` : `最近 ${days.value} 天`));
/** 天数下拉显示代理：区间激活时显示「自定义」（value 0），选 7/30 即清除区间恢复预设 */
const daysModel = computed<number>({
  get: () => (rangeActive.value ? 0 : days.value),
  set: (v) => {
    if (v === 7 || v === 30) days.value = v;
  },
});

const rows = ref<DayStatRow[]>([]);
const minuteRows = ref<MinuteStatRow[]>([]);
const loading = ref(true);
const loadFailed = ref(false);
const failText = ref('');
// CARD-MON-DEF-01：load 与 loadMinute 各持独立竞态守卫——此前共用同一 token，
// 分钟域 begin 会让在飞日统计域的 finally 复位判失效，loading 永卡 true（空数据应用主区永挂加载态）。
const loadGuard = createLoadGuard();
const minuteGuard = createLoadGuard();

/** C17 列链：表格种类列行内链接 → 埋点统计区块单日+种类钻取（seq 递增驱动 watch） */
const drillState = ref<{ date: string; type: string; seq: number } | null>(null);
let drillSeq = 0;
function drillTo(date: string, type: string): void {
  drillState.value = { date, type, seq: ++drillSeq };
}

const summary = computed(() => dayStatSummary(rows.value));
/** 表格总计行（CARD-MON-04）：前端聚合已加载行 */
const totalRow = computed(() => dayStatTotalRow(rows.value));

async function load(): Promise<void> {
  const mine = loadGuard.begin();
  loading.value = true;
  loadFailed.value = false;
  try {
    const list = rangeActive.value
      ? await fetchDayStatsRange(req(), props.appId, rangeStart.value, rangeEnd.value)
      : await fetchDayStats(req(), props.appId, days.value);
    if (loadGuard.isStale(mine)) return;
    rows.value = list;
  } catch (e) {
    if (loadGuard.isStale(mine)) return;
    loadFailed.value = true;
    failText.value = e instanceof Error ? e.message : String(e);
    rows.value = [];
  } finally {
    if (!loadGuard.isStale(mine)) loading.value = false;
  }
}

async function loadMinute(): Promise<void> {
  if (!minuteOn.value) return;
  const mine = minuteGuard.begin();
  try {
    const list = await fetchMinuteStats(req(), props.appId, 24);
    if (minuteGuard.isStale(mine)) return;
    minuteRows.value = list;
  } catch (e) {
    if (minuteGuard.isStale(mine)) return;
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

// 区间两端都选中后按区间重拉；分钟开关开着时同轮联动重拉分钟曲线
watch(rangeActive, (on) => {
  if (!on) return;
  void load();
  if (minuteOn.value) void loadMinute();
});

function clearRange(): void {
  rangeStart.value = '';
  rangeEnd.value = '';
  void load(); // 清除后恢复 7/30 预设（days 保持原值）
}

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
  openGuard(`对「${props.appName || `应用 #${props.appId}`}」${rangeLabel.value}的 ${ids.length} 条日统计重新计算（RetryStat）。确认？`, async () => {
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
      <div
        data-spec="daystat-range"
        class="flex items-center gap-1"
      >
        <input
          v-model="rangeStart"
          type="date"
          data-spec="daystat-range-start"
          aria-label="开始日期"
          class="h-[28px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[6px] text-[12px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
        >
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">~</span>
        <input
          v-model="rangeEnd"
          type="date"
          data-spec="daystat-range-end"
          aria-label="结束日期"
          class="h-[28px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[6px] text-[12px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
        >
        <Btn
          v-if="rangeActive"
          variant="default"
          size="sm"
          data-spec="daystat-range-clear"
          title="清除自定义区间，恢复最近 7 天"
          @click="clearRange"
        >
          清除
        </Btn>
      </div>
      <select
        v-model.number="daysModel"
        data-spec="daystat-days"
        :disabled="rangeActive"
        title="已选自定义日期区间，点「清除」恢复预设"
        class="h-[28px] cursor-pointer rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px] text-[12px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)] disabled:cursor-not-allowed disabled:opacity-60"
        aria-label="统计天数"
      >
        <option :value="7">
          最近 7 天
        </option>
        <option :value="30">
          最近 30 天
        </option>
        <option
          :value="0"
          disabled
        >
          自定义
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
        :title="rows.length === 0 ? '没有可重算的日统计行' : `对${rangeLabel}重新统计（RetryStat）`"
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
            调用 / 错误（{{ rangeLabel }}）
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

      <TraceStatCard
        :app-id="appId"
        :app-name="appName"
        :deps="deps"
        :drill="drillState"
      />

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
                {{ fmtErrorRate(r.errorRate) }}
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
                <button
                  type="button"
                  data-spec="daystat-drill-api"
                  class="text-[var(--el-color-primary)] hover:underline"
                  :title="`查看 ${r.statDate} 的接口类追踪统计`"
                  @click="drillTo(r.statDate, 'Api')"
                >
                  {{ r.apis }}
                </button>
              </td>
              <td
                :class="TD"
                class="tabular-nums"
              >
                <button
                  type="button"
                  data-spec="daystat-drill-http"
                  class="text-[var(--el-color-primary)] hover:underline"
                  :title="`查看 ${r.statDate} 的Http类追踪统计`"
                  @click="drillTo(r.statDate, 'Http')"
                >
                  {{ r.https }}
                </button>
              </td>
              <td
                :class="TD"
                class="tabular-nums"
              >
                <button
                  type="button"
                  data-spec="daystat-drill-db"
                  class="text-[var(--el-color-primary)] hover:underline"
                  :title="`查看 ${r.statDate} 的数据库类追踪统计`"
                  @click="drillTo(r.statDate, 'Db')"
                >
                  {{ r.dbs }}
                </button>
              </td>
              <td
                :class="TD"
                class="tabular-nums"
              >
                <button
                  type="button"
                  data-spec="daystat-drill-mq"
                  class="text-[var(--el-color-primary)] hover:underline"
                  :title="`查看 ${r.statDate} 的MQ类追踪统计`"
                  @click="drillTo(r.statDate, 'Mq')"
                >
                  {{ r.mqs }}
                </button>
              </td>
              <td
                :class="TD"
                class="tabular-nums"
              >
                <button
                  type="button"
                  data-spec="daystat-drill-redis"
                  class="text-[var(--el-color-primary)] hover:underline"
                  :title="`查看 ${r.statDate} 的Redis类追踪统计`"
                  @click="drillTo(r.statDate, 'Redis')"
                >
                  {{ r.redis }}
                </button>
              </td>
            </tr>
            <tr
              data-spec="daystat-totalrow"
              class="bg-[var(--el-fill-color-light)] font-medium"
            >
              <td :class="TD">
                总计（已加载 {{ totalRow.days }} 天）
              </td>
              <td
                :class="TD"
                class="tabular-nums"
              >
                {{ totalRow.total }}
              </td>
              <td
                :class="TD"
                class="tabular-nums"
              >
                {{ totalRow.errors }}
              </td>
              <td
                :class="TD"
                class="tabular-nums"
              >
                {{ totalRow.errorRate }}%
              </td>
              <td
                :class="TD"
                class="tabular-nums"
              >
                {{ totalRow.avgCost }}
              </td>
              <td
                :class="TD"
                class="tabular-nums"
              >
                {{ totalRow.maxCost }}
              </td>
              <td
                :class="TD"
                class="tabular-nums"
              >
                {{ totalRow.names }}
              </td>
              <td
                :class="TD"
                class="tabular-nums"
              >
                {{ totalRow.apis }}
              </td>
              <td
                :class="TD"
                class="tabular-nums"
              >
                {{ totalRow.https }}
              </td>
              <td
                :class="TD"
                class="tabular-nums"
              >
                {{ totalRow.dbs }}
              </td>
              <td
                :class="TD"
                class="tabular-nums"
              >
                {{ totalRow.mqs }}
              </td>
              <td
                :class="TD"
                class="tabular-nums"
              >
                {{ totalRow.redis }}
              </td>
            </tr>
          </tbody>
        </table>
        <div
          data-spec="daystat-totalnote"
          class="mt-[6px] text-[11px] text-[var(--el-text-color-secondary)]"
        >
          口径=已加载行合计，非服务端全量
        </div>
      </div>
    </template>

    <!-- CARD-MON-DEF-01 形态 A（j-config 裁定）：分钟卡独立于有数据分支渲染——
         DayStat 与 MinuteStat 两表独立，空日统计应用开分钟开关后主区空态与分钟卡共存（可见）。 -->
    <div
      v-if="minuteOn && !loadFailed"
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
  </Card>
</template>
