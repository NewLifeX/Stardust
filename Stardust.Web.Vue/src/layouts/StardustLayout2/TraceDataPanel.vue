<script setup lang="ts">
/**
 * TraceDataPanel：工作台「监控中心」页签 ·「追踪数据」区块（CARD-MON-11，2026-11-09 定稿）。
 * 数据真实接口（TraceDataController.cs 现行，零后端改动）：
 *   GET  /Monitors/TraceData（appId 必传；dtStart/dtEnd 默认今天；pageSize 100）
 *   POST /Monitors/TraceData/Trace?id=   → /trace/{traceId} 深链（雪花 string）
 *   POST /Monitors/TraceData/Exclude?id= → 应用采样排除名单（GuardModal 两段式确认）
 * 双图显示条件对齐后端 MVC 旧条件：itemId 查询值非空 && 本页行数 > 1；
 * 左「调用与错误」按 Name Top10 聚合双柱状，右「平均耗时」按 StatHour 升序加权折线。
 * ECharts 仅容器态上 CT（§9.1：canvas 内图形不验），纯逻辑在 traceData.logic.ts。
 */
import { computed, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { request } from '@newlifex/cube-vue/core/utils/request';
import EChart from '@/components/EChart.vue';
import { cssVar } from '@/utils/cssVar';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import Btn from './Btn.vue';
import Card from './Card.vue';
import { H3, TD, TH } from './deployView.styles.ts';
import { localDateStr } from './daystat.logic.ts';
import { useManualRefresh } from './useManualRefresh.ts';
import { openGuard } from './ui.store.ts';
import { toast } from './useToast.ts';
import {
  excludeTraceData,
  fetchTraceData,
  fmtUnixMs,
  resolveTraceDataTrace,
  showTraceDataCharts,
  traceDataCostLine,
  traceDataNameBar,
  traceDataTotalRow,
  type TraceDataRow,
} from './traceData.logic.ts';

const props = defineProps<{
  appId: number;
  appName?: string;
  deps?: { request?: Req };
}>();

function req(): Req {
  return props.deps?.request ?? (request as Req);
}

// story/CT 直挂组件时无 router 上下文，router 为 undefined，按钮点击为 no-op（AppTracerTab 先例）。
const router = useRouter();

const subtitle = computed(() =>
  props.appName
    ? `${props.appName}·每日聚合。搜索标签按逗号取首段，日期默认今天`
    : '每日聚合。搜索标签按逗号取首段，日期默认今天',
);

// ---------- 查询条件（日期默认今天） ----------
const dtStart = ref(localDateStr(new Date()));
const dtEnd = ref(localDateStr(new Date()));
const itemIdKw = ref(0);
const nameKw = ref('');
const searchTagKw = ref(false);
const qKw = ref('');

// ---------- 状态 ----------
const rows = ref<TraceDataRow[]>([]);
const loading = ref(false);
const loadFailed = ref(false);
const failText = ref('');
let token = 0;

const REFRESH_MIN_SPIN_MS = 300;
const sleep = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms));

/** 请求再快也至少转 300ms（AGENTS §11），所有加载路径统一保底 */
async function minSpin(started: number): Promise<void> {
  const used = Date.now() - started;
  const rest = Math.max(0, REFRESH_MIN_SPIN_MS - (Number.isFinite(used) && used > 0 ? used : 0));
  if (rest > 0) await sleep(rest);
}

async function load(force = false): Promise<void> {
  const mine = ++token;
  const started = Date.now();
  if (!force) loading.value = true;
  loadFailed.value = false;
  const r = await fetchTraceData(req(), {
    appId: props.appId,
    itemId: itemIdKw.value > 0 ? itemIdKw.value : undefined,
    name: nameKw.value || undefined,
    searchTag: searchTagKw.value || undefined,
    dtStart: dtStart.value || undefined,
    dtEnd: dtEnd.value || undefined,
    q: qKw.value || undefined,
  });
  if (mine !== token) return;
  if (r.error) {
    loadFailed.value = true;
    failText.value = r.error;
    rows.value = [];
  } else {
    rows.value = r.rows;
  }
  if (mine !== token) return;
  await minSpin(started);
  if (mine !== token) return;
  loading.value = false;
}

function query(): void {
  void load();
}

const { refreshing, refresh } = useManualRefresh({
  load: async () => {
    await load(true);
  },
  outcome: () => ({
    loadError: loadFailed.value ? `追踪数据加载失败：${failText.value}` : '',
    versionsFailed: false,
    nodesFailed: false,
    historyFailed: false,
  }),
  notify: (ok, text) => toast(text, ok ? 'ok' : 'err'),
});

watch(
  () => props.appId,
  () => {
    itemIdKw.value = 0;
    void load();
  },
  { immediate: true },
);

// ---------- 双图（itemId 非空 && 本页 >1 行） ----------
const chartsOn = computed(() => showTraceDataCharts(itemIdKw.value, rows.value));

const chartPalette = computed(() => ({
  primary: cssVar('--el-color-primary', '#409eff'),
  danger: cssVar('--el-color-danger', '#f56c6c'),
  axis: cssVar('--el-text-color-secondary', '#909399'),
  splitLine: cssVar('--el-border-color-light', '#e4e7ed'),
}));

const nameBarOption = computed(() => traceDataNameBar(rows.value, chartPalette.value));
const costLineOption = computed(() => traceDataCostLine(rows.value, chartPalette.value));

// ---------- 总计行 ----------
const totalRow = computed(() => traceDataTotalRow(rows.value));

// ---------- 行操作：追踪 ----------
const tracing = ref('');

async function onTrace(r: TraceDataRow): Promise<void> {
  if (tracing.value) return;
  tracing.value = r.id;
  try {
    const res = await resolveTraceDataTrace(req(), r.id);
    if (res.ok) {
      toast(`已解析追踪 ${res.traceId}，正在打开…`, 'ok');
      if (router) void router.push(`/trace/${res.traceId}`);
    } else {
      toast(`追踪失败：${res.message}`, 'err');
    }
  } finally {
    tracing.value = '';
  }
}

// ---------- 行操作：屏蔽（GuardModal 两段式：短标题 + 详情） ----------
const excluding = ref('');

function onExclude(r: TraceDataRow): void {
  if (excluding.value) return;
  openGuard(
    '屏蔽该操作名',
    async () => {
      excluding.value = r.id;
      try {
        const res = await excludeTraceData(req(), r.id);
        if (res.ok) toast(res.message || '成功！', 'ok');
        else toast(`屏蔽失败：${res.message}`, 'err');
      } finally {
        excluding.value = '';
      }
    },
    `将把 ${r.name} 加入应用 ${props.appId} 的采样排除名单，后续上报不再采样`,
  );
}

const inputCls =
  'h-[28px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px] text-[12px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]';

function costText(n: number): string {
  return `${n} ms`;
}
</script>

<template>
  <Card
    data-spec="trace-data-panel"
    class="mt-3"
  >
    <div :class="H3">
      <h3 class="m-0">
        追踪数据
      </h3>
      <span class="flex-1" />
      <Btn
        variant="default"
        size="sm"
        data-spec="trace-data-refresh"
        :disabled="refreshing || loading"
        @click="refresh"
      >
        <template #icon>
          <span
            v-if="refreshing"
            class="inline-flex animate-spin"
          >
            <span class="h-[12px] w-[12px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
          </span>
        </template>
        刷新
      </Btn>
    </div>
    <p
      data-spec="trace-data-subtitle"
      class="mb-[10px] mt-0 text-[11.5px] text-[var(--el-text-color-secondary)]"
    >
      {{ subtitle }}
    </p>

    <div class="mb-[10px] flex flex-wrap items-center gap-2 text-[12px] text-[var(--el-text-color-secondary)]">
      <label>日期起</label>
      <input
        v-model="dtStart"
        data-spec="trace-data-dt-start"
        type="date"
        :class="inputCls"
      >
      <label>止</label>
      <input
        v-model="dtEnd"
        data-spec="trace-data-dt-end"
        type="date"
        :class="inputCls"
      >
      <label>跟踪项</label>
      <input
        v-model.number="itemIdKw"
        data-spec="trace-data-itemid"
        type="number"
        min="0"
        placeholder="全项"
        :class="inputCls"
        class="w-[88px]"
      >
      <label>操作名</label>
      <input
        v-model="nameKw"
        data-spec="trace-data-name"
        type="text"
        placeholder="精确操作名"
        :class="inputCls"
      >
      <label class="flex items-center gap-1">
        <input
          v-model="searchTagKw"
          data-spec="trace-data-searchtag"
          type="checkbox"
        >
        搜索标签
      </label>
      <label>关键字</label>
      <input
        v-model="qKw"
        data-spec="trace-data-q"
        type="text"
        placeholder="Q"
        :class="inputCls"
      >
      <Btn
        variant="primary"
        size="sm"
        data-spec="trace-data-query"
        :disabled="loading"
        @click="query"
      >
        <template #icon>
          <span
            v-if="loading"
            class="inline-flex animate-spin"
          >
            <span class="h-[12px] w-[12px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-white)]" />
          </span>
        </template>
        查询
      </Btn>
    </div>

    <!-- 双图：仅 itemId>0 && 本页 >1 行（对齐后端 MVC 旧条件） -->
    <div
      v-if="chartsOn"
      data-spec="trace-data-charts"
      class="mb-[10px] grid grid-cols-1 gap-3 xl:grid-cols-2"
    >
      <div
        data-spec="trace-data-chart-namebar"
        class="rounded-[10px] border border-[var(--el-border-color-light)] p-2"
      >
        <EChart
          :option="nameBarOption"
          height="260px"
        />
      </div>
      <div
        data-spec="trace-data-chart-costline"
        class="rounded-[10px] border border-[var(--el-border-color-light)] p-2"
      >
        <EChart
          :option="costLineOption"
          height="260px"
        />
      </div>
    </div>

    <div
      v-if="loadFailed"
      data-spec="trace-data-failed"
      class="py-6 text-[12.5px] text-[var(--el-color-danger)]"
    >
      追踪数据加载失败：{{ failText }}
      <Btn
        variant="default"
        size="sm"
        class="ml-2"
        data-spec="trace-data-retry"
        @click="load(true)"
      >
        重试
      </Btn>
    </div>

    <div
      v-else-if="rows.length === 0"
      data-spec="trace-data-empty"
      class="py-6 text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      <span
        v-if="loading"
        data-spec="trace-data-loading"
        class="mr-2 inline-flex animate-spin align-[-2px]"
      >
        <span class="h-[12px] w-[12px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
      </span>
      {{ loading ? '追踪数据加载中…' : '暂无追踪数据记录' }}
    </div>

    <div
      v-else
      class="overflow-x-auto"
    >
      <table class="w-full border-collapse">
        <thead>
          <tr>
            <th :class="TH">
              操作名
            </th>
            <th :class="TH">
              实例
            </th>
            <th :class="TH">
              统计分钟
            </th>
            <th :class="TH">
              总次数
            </th>
            <th :class="TH">
              错误数
            </th>
            <th :class="TH">
              平均耗时
            </th>
            <th :class="TH">
              最大耗时
            </th>
            <th :class="TH">
              开始时间
            </th>
            <th :class="TH">
              操作
            </th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="r in rows"
            :key="r.id"
            class="group"
            data-spec="trace-data-row"
          >
            <td
              :class="TD"
              class="font-mono"
            >
              {{ r.name }}
            </td>
            <td :class="TD">
              {{ r.clientId || '—' }}
            </td>
            <td
              :class="TD"
              class="whitespace-nowrap"
            >
              {{ r.statMinute || '—' }}
            </td>
            <td :class="TD">
              {{ r.total }}
            </td>
            <td :class="TD">
              <span :class="r.errors > 0 ? 'text-[var(--el-color-danger)]' : ''">{{ r.errors }}</span>
            </td>
            <td :class="TD">
              {{ costText(r.cost) }}
            </td>
            <td :class="TD">
              {{ costText(r.maxCost) }}
            </td>
            <td
              :class="TD"
              class="whitespace-nowrap"
            >
              {{ fmtUnixMs(r.startTime) }}
            </td>
            <td :class="TD">
              <div class="flex gap-1">
                <Btn
                  variant="default"
                  size="sm"
                  data-spec="trace-data-trace"
                  :disabled="tracing !== ''"
                  :title="`按 ${r.name} 本次采样打开调用链详情`"
                  @click="onTrace(r)"
                >
                  <template #icon>
                    <span
                      v-if="tracing === r.id"
                      class="inline-flex animate-spin"
                    >
                      <span class="h-[12px] w-[12px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
                    </span>
                  </template>
                  {{ tracing === r.id ? '追踪中…' : '追踪' }}
                </Btn>
                <Btn
                  variant="default"
                  size="sm"
                  data-spec="trace-data-exclude"
                  :disabled="excluding !== ''"
                  :title="`把 ${r.name} 加入应用 ${appId} 的采样排除名单`"
                  @click="onExclude(r)"
                >
                  {{ excluding === r.id ? '屏蔽中…' : '屏蔽' }}
                </Btn>
              </div>
            </td>
          </tr>
          <tr
            v-if="totalRow"
            class="group"
            data-spec="trace-data-total"
          >
            <td
              :class="TD"
              class="font-medium text-[var(--el-text-color-primary)]"
              colspan="3"
            >
              {{ totalRow.label }} · Σ总耗时 {{ costText(totalRow.totalCost) }}
            </td>
            <td :class="TD">
              {{ totalRow.total }}
            </td>
            <td :class="TD">
              {{ totalRow.errors }}
            </td>
            <td :class="TD">
              {{ costText(totalRow.avgCost) }}
            </td>
            <td :class="TD" />
            <td :class="TD" />
            <td :class="TD" />
          </tr>
        </tbody>
      </table>
    </div>
  </Card>
</template>
