<script setup lang="ts">
/**
 * Monitors/AppMinuteStat/Index — 应用分钟级统计（自定义迁移页 + ECharts 双图表）
 *
 * MVC 源（_List_Data.cshtml + 控制器 Search）：
 *  - 隐藏页 [Menu(0,false)]，靠自定义视图兜底。
 *  - 列表列（与 MVC _List_Data 逐列一致）：
 *      编号 ID / 统计分钟 StatTime / 应用 AppName(始终显示) / 总次数 Total(链接) /
 *      错误数 Errors / 错误率 ErrorRate / 平均耗时 Cost / 最大耗时 MaxCost / 最小耗时 MinCost /
 *      创建时间 CreateTime / 更新时间 UpdateTime
 *  - 应用列始终显示（MVC 无 appId<=0 条件）；总次数链接到 traceData。
 *  - 行着色 ErrorRate 阈值（MVC danger/warning）；总计行聚合（ErrorRate/Cost/MaxCost/MinCost 均值，Total/Errors 求和）。
 *  - 图表（控制器 Search 权威）：双图
 *      图1（调用次数轴）：总次数 / 错误数(次轴,红)
 *      图2（耗时轴）：平均耗时 / 最小耗时 / 最大耗时(次轴)
 *  - 搜索（_List_Search）：appId 下拉 + 时间范围 dtStart/dtEnd(datetimerange)。
 */
import { ref, computed, onMounted, watch } from 'vue';
import EChart from '@/components/EChart.vue';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';

const api = usePageApi('Monitors', 'AppMinuteStat');

interface Row {
  ID?: number;
  StatTime?: string;
  AppId?: number;
  AppName?: string;
  Total?: number;
  Errors?: number;
  ErrorRate?: number;
  TotalCost?: number;
  Cost?: number;
  MaxCost?: number;
  MinCost?: number;
  CreateTime?: string;
  UpdateTime?: string;
  [k: string]: unknown;
}

/** 标准列表列（与 MVC _List_Data 一致） */
type Col = { name: string; label: string; minWidth?: number; align?: 'left' | 'center' | 'right' };
const ALL_COLUMNS: Col[] = [
  { name: 'ID', label: '编号', minWidth: 70, align: 'right' },
  { name: 'StatTime', label: '统计分钟', minWidth: 150 },
  { name: 'AppName', label: '应用', minWidth: 140 },
  { name: 'Total', label: '总次数', minWidth: 90, align: 'right' },
  { name: 'Errors', label: '错误数', minWidth: 80, align: 'right' },
  { name: 'ErrorRate', label: '错误率', minWidth: 80, align: 'right' },
  { name: 'Cost', label: '平均耗时', minWidth: 90, align: 'right' },
  { name: 'MaxCost', label: '最大耗时', minWidth: 90, align: 'right' },
  { name: 'MinCost', label: '最小耗时', minWidth: 90, align: 'right' },
  { name: 'CreateTime', label: '创建时间', minWidth: 150 },
  { name: 'UpdateTime', label: '更新时间', minWidth: 150 },
];

const SUM_NUM_COLUMNS = ['Total', 'Errors'];

/** 搜索：appId + 时间范围 dtStart/dtEnd(datetimerange) */
const appIdFilter = ref<number | null>(null);
const timeRange = ref<[string, string] | null>(null);

const loading = ref(false);
const rows = ref<Row[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);

const hasActiveFilter = computed(
  () => (appIdFilter.value != null && appIdFilter.value > 0) || timeRange.value != null,
);
const hasData = computed(() => rows.value.length > 0);

function formatTime(v: unknown): string {
  if (v == null) return '';
  return String(v).replace('T', ' ').slice(0, 19);
}
function formatMinute(v: unknown): string {
  if (v == null) return '';
  return String(v).replace('T', ' ').slice(0, 16);
}
function formatPct(v: unknown): string {
  if (v == null) return '';
  return `${(Number(v) * 100).toFixed(2)}%`;
}
function cellText(row: Row, key: string): string {
  const v = row[key];
  if (v == null) return '';
  if (key === 'StatTime') return formatMinute(v);
  if (key === 'CreateTime' || key === 'UpdateTime') return formatTime(v);
  if (key === 'AppName') return String(v);
  if (key === 'ErrorRate') return formatPct(v);
  if (key === 'Total' || key === 'Errors' || key === 'Cost' || key === 'MaxCost' || key === 'MinCost' || key === 'ID') {
    return Number(v).toLocaleString('en-US');
  }
  return String(v);
}

/** 总次数链接到 traceData（与 MVC 一致） */
function totalUrl(row: Row): string {
  return `#/Monitors/traceData?appId=${row.AppId}&kind=minute&time=${formatTime(row.StatTime)}`;
}

/** 行着色（MVC: ErrorRate>=0.01 danger / >=0.001 warning） */
function rowClassName({ row }: { row: Row }): string {
  const er = Number(row.ErrorRate ?? 0);
  if (er >= 0.01) return 'ams-row-danger';
  if (er >= 0.001) return 'ams-row-warning';
  return '';
}

/** 总计行（summary-method）：ErrorRate/Cost/MaxCost/MinCost 取均值，Total/Errors 求和 */
function getSummaries(param: { columns: any[]; data: Row[] }): string[] {
  const { columns, data } = param;
  const sums: string[] = [];
  columns.forEach((col, idx) => {
    if (idx === 0) {
      sums[idx] = '总计';
      return;
    }
    const prop = col.property as string;
    if (prop === 'ErrorRate' || prop === 'Cost' || prop === 'MaxCost' || prop === 'MinCost') {
      const vals = data.map((r) => Number((r as any)[prop] ?? 0)).filter((n) => !Number.isNaN(n));
      sums[idx] = vals.length ? (prop === 'ErrorRate' ? formatPct(vals.reduce((a, b) => a + b, 0) / vals.length) : String(Math.round(vals.reduce((a, b) => a + b, 0) / vals.length))) : '';
      return;
    }
    if (SUM_NUM_COLUMNS.includes(prop)) {
      sums[idx] = String(data.reduce((acc, r) => acc + Number((r as any)[prop] ?? 0), 0));
      return;
    }
    sums[idx] = '';
  });
  return sums;
}

/** ECharts 图1：调用次数轴（Total / Errors 次轴） */
const chartOption1 = computed<Record<string, unknown>>(() => {
  const times = rows.value.map((r) => formatMinute(r.StatTime));
  const totals = rows.value.map((r) => Number(r.Total ?? 0));
  const errors = rows.value.map((r) => Number(r.Errors ?? 0));
  return {
    tooltip: { trigger: 'axis' },
    legend: { data: ['调用次数', '错误数'], bottom: 0 },
    grid: { left: 48, right: 48, top: 24, bottom: 48, containLabel: true },
    xAxis: { type: 'category', boundaryGap: false, data: times, axisLabel: { color: '#606266' } },
    yAxis: [
      { type: 'value', name: '调用次数', axisLabel: { color: '#606266' }, splitLine: { lineStyle: { color: '#EBEDF0' } } },
      { type: 'value', name: '错误数', axisLabel: { color: '#606266' }, splitLine: { show: false } },
    ],
    series: [
      { name: '调用次数', type: 'line', smooth: true, showSymbol: false, data: totals },
      { name: '错误数', type: 'line', smooth: true, showSymbol: false, yAxisIndex: 1, data: errors, itemStyle: { color: 'rgba(255,0,0,0.5)' } },
    ],
  };
});

/** ECharts 图2：耗时轴（Cost / MinCost / MaxCost 次轴） */
const chartOption2 = computed<Record<string, unknown>>(() => {
  const times = rows.value.map((r) => formatMinute(r.StatTime));
  const costs = rows.value.map((r) => Number(r.Cost ?? 0));
  const minCosts = rows.value.map((r) => Number(r.MinCost ?? 0));
  const maxCosts = rows.value.map((r) => Number(r.MaxCost ?? 0));
  return {
    tooltip: { trigger: 'axis' },
    legend: { data: ['平均耗时(ms)', '最小耗时(ms)', '最大耗时(ms)'], bottom: 0 },
    grid: { left: 48, right: 48, top: 24, bottom: 48, containLabel: true },
    xAxis: { type: 'category', boundaryGap: false, data: times, axisLabel: { color: '#606266' } },
    yAxis: [
      { type: 'value', name: '耗时（ms）', axisLabel: { color: '#606266' }, splitLine: { lineStyle: { color: '#EBEDF0' } } },
      { type: 'value', name: '最大耗时（ms）', axisLabel: { color: '#606266' }, splitLine: { show: false } },
    ],
    series: [
      { name: '平均耗时(ms)', type: 'line', smooth: true, showSymbol: false, data: costs },
      { name: '最小耗时(ms)', type: 'line', smooth: true, showSymbol: false, data: minCosts },
      { name: '最大耗时(ms)', type: 'line', smooth: true, showSymbol: false, yAxisIndex: 1, data: maxCosts },
    ],
  };
});

function buildParams(): Record<string, unknown> {
  const p: Record<string, unknown> = { pageIndex: page.value - 1, pageSize: pageSize.value };
  if (appIdFilter.value != null && appIdFilter.value > 0) p.appId = appIdFilter.value;
  if (timeRange.value?.[0]) p.dtStart = timeRange.value[0];
  if (timeRange.value?.[1]) p.dtEnd = timeRange.value[1];
  return p;
}

async function fetchList() {
  loading.value = true;
  try {
    const res: any = await api.getList<Row>(buildParams());
    rows.value = res?.data ?? [];
    total.value = res?.page?.totalCount ?? res?.page?.total ?? rows.value.length;
  } catch (e) {
    console.error('[AppMinuteStat] GetList failed', e);
    rows.value = [];
    total.value = 0;
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  page.value = 1;
  fetchList();
}
function handleReset() {
  appIdFilter.value = null;
  timeRange.value = null;
  page.value = 1;
  fetchList();
}
function onPageChange(p: number) {
  page.value = p;
  fetchList();
}

onMounted(async () => {
  const m = location.hash.match(/[?&]appId=(\d+)/);
  if (m) appIdFilter.value = Number(m[1]);
  await fetchList();
});

watch(rows, () => {}, { deep: true });
</script>

<template>
  <div class="list-page app-minute-stat-page">
    <div class="lp-header">
      <h2 class="lph-title">
        应用分钟统计
      </h2>
      <div class="lp-toolbar">
        <el-button
          :loading="loading"
          @click="fetchList"
        >
          刷新
        </el-button>
      </div>
    </div>

    <div class="lp-card-search">
      <div class="ams-search-grid">
        <div class="ams-search-field">
          <label class="ams-search-label">应用编号</label>
          <el-input
            v-model.number="appIdFilter"
            type="number"
            placeholder="AppId"
            class="ams-search-input"
            clearable
          />
        </div>
        <div class="ams-search-field ams-search-field--wide">
          <label class="ams-search-label">统计分钟</label>
          <el-date-picker
            v-model="timeRange"
            type="datetimerange"
            range-separator="~"
            start-placeholder="开始时间"
            end-placeholder="结束时间"
            value-format="YYYY-MM-DD HH:mm:ss"
            format="YYYY-MM-DD HH:mm:ss"
            class="ams-search-input ams-search-datepicker"
          />
        </div>
        <div class="ams-search-actions">
          <el-button
            :class="{ 'is-active': hasActiveFilter }"
            @click="handleReset"
          >
            重置
          </el-button>
          <el-button
            type="primary"
            @click="handleSearch"
          >
            <el-icon><Search /></el-icon>搜索
          </el-button>
        </div>
      </div>
    </div>

    <div class="lp-card">
      <div class="lp-card-header">
        <span class="lp-card-title">按分钟统计曲线（调用次数 / 错误数）</span>
      </div>
      <div class="lp-card-body ams-chart-body">
        <EChart
          v-if="hasData"
          :option="chartOption1"
          height="360px"
        />
        <el-empty
          v-else
          description="暂无统计数据"
        />
      </div>
    </div>

    <div class="lp-card">
      <div class="lp-card-header">
        <span class="lp-card-title">按分钟耗时曲线（平均 / 最小 / 最大 耗时）</span>
      </div>
      <div class="lp-card-body ams-chart-body">
        <EChart
          v-if="hasData"
          :option="chartOption2"
          height="360px"
        />
        <el-empty
          v-else
          description="暂无统计数据"
        />
      </div>
    </div>

    <div class="lp-card">
      <div class="lp-card-body">
        <el-table
          v-loading="loading"
          :data="rows"
          class="ams-table"
          stripe
          border
          show-summary
          :summary-method="getSummaries"
          :row-class-name="rowClassName"
          empty-text="暂无数据"
        >
          <el-table-column
            v-for="c in ALL_COLUMNS"
            :key="c.name"
            :prop="c.name"
            :label="c.label"
            :min-width="c.minWidth"
            :align="c.align || 'left'"
            show-overflow-tooltip
          >
            <template #default="{ row }">
              <el-link
                v-if="c.name === 'AppName' && row.AppId != null"
                type="primary"
                :underline="false"
                :href="`#/Monitors/AppDayStat?monitorId=${row.AppId}`"
                target="_self"
              >
                {{ cellText(row, c.name) }}
              </el-link>
              <el-link
                v-else-if="c.name === 'Total' && row.AppId != null"
                type="primary"
                :underline="false"
                :href="totalUrl(row)"
                target="_self"
              >
                {{ cellText(row, c.name) }}
              </el-link>
              <span
                v-else
                :class="c.name === 'ErrorRate' ? 'ams-errorrate' : ''"
              >{{ cellText(row, c.name) }}</span>
            </template>
          </el-table-column>
        </el-table>
      </div>

      <div class="lp-card-footer">
        <el-pagination
          :current-page="page"
          :page-size="pageSize"
          :total="total"
          layout="total, prev, pager, next, sizes"
          :page-sizes="[10, 20, 50, 100]"
          @current-change="onPageChange"
          @size-change="(s: number) => { pageSize = s; page = 1; fetchList(); }"
        />
      </div>
    </div>
  </div>
</template>

<style lang="scss" scoped>
.app-minute-stat-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: auto;

  .lp-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: var(--el-main-padding, 24px) var(--el-main-padding, 24px) 0;
    .lph-title { margin: 0; font-size: 18px; font-weight: 600; color: var(--el-text-color-primary); }
    .lp-toolbar { display: flex; gap: 8px; }
  }

  .lp-card-search {
    margin: 16px var(--el-main-padding, 24px) 0;
    padding: 16px 20px;
    background: var(--el-bg-color-overlay);
    border: 1px solid var(--el-border-color-light);
    border-radius: var(--el-border-radius-base);
    box-shadow: var(--el-box-shadow-light);
  }

  .lp-card {
    margin: 16px var(--el-main-padding, 24px);
    padding: 16px;
    background: var(--el-bg-color-overlay);
    border: 1px solid var(--el-border-color-light);
    border-radius: var(--el-border-radius-base);
    box-shadow: var(--el-box-shadow-light);
    overflow: hidden;
  }

  .lp-card-header {
    display: flex;
    align-items: baseline;
    gap: 12px;
    padding: 0 4px 12px;
    border-bottom: 1px solid var(--el-border-color-light);
    .lp-card-title { font-size: 15px; font-weight: 600; color: var(--el-text-color-primary); }
  }

  .lp-card-body { padding-top: 16px; min-height: 200px; }
  .ams-chart-body { min-height: 360px; display: flex; align-items: stretch; }
  .lp-card-footer { padding: 12px 4px 0; border-top: 1px solid var(--el-border-color-light); }
}

.ams-search-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 12px 16px;
  align-items: end;
}
.ams-search-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;
  &--wide { grid-column: span 2; }
}
.ams-search-label {
  font-size: 12px;
  font-weight: 500;
  color: var(--el-text-color-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.ams-search-input {
  width: 100%;
  :deep(.el-input__wrapper), :deep(.el-range-editor) {
    min-height: 36px;
    border-radius: var(--el-border-radius-base);
    background: var(--el-bg-color);
    border: 1px solid var(--el-border-color-light);
    box-shadow: none;
  }
  :deep(.el-input__wrapper.is-focus), :deep(.el-range-editor.is-active) {
    background: var(--el-fill-color-blank);
    border-color: var(--el-color-primary);
    box-shadow: 0 0 0 1px var(--el-color-primary) inset, 0 0 0 3px var(--el-color-primary-light-8);
  }
  :deep(.el-input__inner) { font-size: 13px; color: var(--el-text-color-primary); }
}
.ams-search-datepicker { :deep(.el-range-editor) { width: 100%; } }
.ams-search-actions { display: flex; justify-content: flex-end; gap: 8px; padding-bottom: 0; }
.ams-table { width: 100%; }
.ams-errorrate { font-weight: 600; }

.ams-row-danger { --el-table-tr-bg-color: var(--el-color-danger-light-9); td { color: var(--el-color-danger); } }
.ams-row-warning { --el-table-tr-bg-color: var(--el-color-warning-light-9); td { color: var(--el-color-warning); } }

@media (max-width: 900px) { .ams-search-grid { grid-template-columns: repeat(2, 1fr); } }
@media (max-width: 560px) {
  .ams-search-grid { grid-template-columns: 1fr; }
  .ams-search-field--wide { grid-column: span 1; }
}
</style>
