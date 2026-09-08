<script setup lang="ts">
/**
 * Monitors/AppDayStat/Index — 应用日统计（自定义迁移页 + ECharts 双图表）
 *
 * MVC 源（_List_Data.cshtml + 控制器 Search）：
 *  - [Menu(80)] 可见页；列表列（与 MVC _List_Data 逐列一致）：
 *      编号 ID / 统计日期 StatDate / [应用 AppName] / 当日监控(链接列) /
 *      埋点数 Names / 总次数 Total / 环比 RingRate / 错误数 Errors / 错误率 ErrorRate /
 *      平均耗时 Cost / 最大耗时 MaxCost / 最小耗时 MinCost /
 *      接口数 Apis / Http请求 Https / 数据库 Dbs / 消息队列 Mqs / Redis缓存 Redis / 其它 Others /
 *      创建时间 CreateTime / 更新时间 UpdateTime
 *  - 「当日监控」独立链接列：#/Monitors/TraceDayStat?appId=&date=（MVC 独立列，不依赖 TraceUrl）。
 *  - 分项列 Apis/Https/Dbs/Mqs/Redis/Others 在 MVC 中均链接到当日监控并带 type 参数，已还原为链接列。
 *  - 行着色 ErrorRate 阈值；总计行聚合（RingRate/ErrorRate/Cost/MaxCost/MinCost 均值，其余求和）。
 *  - 图表（控制器 Search 权威）：双图
 *      图1（调用次数轴）：总次数 / 接口数 / Http请求 / 数据库 / 消息队列 / Redis / 其它 / 错误数(次轴,红)
 *      图2（耗时轴）：平均耗时 / 最小耗时 / 最大耗时(次轴)
 *  - 搜索（_List_Search）：monitorId(appId) 下拉 + 日期范围 dtStart/dtEnd。
 *  - 批量统计：_List_Toolbar_Batch 的 RetryStat Action（POST /Monitors/AppDayStat/RetryStat，选中行 keys）。
 */
import { ref, computed, onMounted } from 'vue';
import { ElMessage } from 'element-plus';
import EChart from '@/components/EChart.vue';
import request from '@newlifex/cube-vue/core/utils/request';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';

const api = usePageApi('Monitors', 'AppDayStat');

interface Row {
  ID?: number;
  StatDate?: string;
  AppId?: number;
  AppName?: string;
  Names?: number;
  Total?: number;
  RingRate?: number;
  Errors?: number;
  ErrorRate?: number;
  TotalCost?: number;
  Cost?: number;
  MaxCost?: number;
  MinCost?: number;
  Apis?: number;
  Https?: number;
  Dbs?: number;
  Mqs?: number;
  Redis?: number;
  Others?: number;
  CreateTime?: string;
  UpdateTime?: string;
  [k: string]: unknown;
}

type Col = { name: string; label: string; minWidth?: number; align?: 'left' | 'center' | 'right' };
const ALL_COLUMNS: Col[] = [
  { name: 'ID', label: '编号', minWidth: 80 },
  { name: 'StatDate', label: '统计日期', minWidth: 120 },
  { name: 'AppName', label: '应用', minWidth: 130 },
  { name: 'Names', label: '埋点数', minWidth: 80, align: 'right' },
  { name: 'Total', label: '总次数', minWidth: 90, align: 'right' },
  { name: 'RingRate', label: '环比', minWidth: 80, align: 'right' },
  { name: 'Errors', label: '错误数', minWidth: 80, align: 'right' },
  { name: 'ErrorRate', label: '错误率', minWidth: 80, align: 'right' },
  { name: 'Cost', label: '平均耗时', minWidth: 90, align: 'right' },
  { name: 'MaxCost', label: '最大耗时', minWidth: 90, align: 'right' },
  { name: 'MinCost', label: '最小耗时', minWidth: 90, align: 'right' },
  { name: 'Apis', label: '接口数', minWidth: 80, align: 'right' },
  { name: 'Https', label: 'Http请求', minWidth: 90, align: 'right' },
  { name: 'Dbs', label: '数据库', minWidth: 80, align: 'right' },
  { name: 'Mqs', label: '消息队列', minWidth: 90, align: 'right' },
  { name: 'Redis', label: 'Redis缓存', minWidth: 90, align: 'right' },
  { name: 'Others', label: '其它', minWidth: 80, align: 'right' },
  { name: 'CreateTime', label: '创建时间', minWidth: 150 },
  { name: 'UpdateTime', label: '更新时间', minWidth: 150 },
];

/** 分项类型映射（MVC 当日监控链接 type 参数） */
const TYPE_MAP: Record<string, string> = {
  Apis: 'api',
  Https: 'http',
  Dbs: 'db',
  Mqs: 'mq',
  Redis: 'redis',
  Others: 'other',
};

const showAppCol = computed(() => !(appIdFilter.value != null && appIdFilter.value > 0));
const SUM_NUM_COLUMNS = ['Names', 'Total', 'Errors', 'Apis', 'Https', 'Dbs', 'Mqs', 'Redis', 'Others'];

const appIdFilter = ref<number | null>(null);
const timeRange = ref<[string, string] | null>(null);
const loading = ref(false);
const rows = ref<Row[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);
const selectedKeys = ref<number[]>([]);

const hasActiveFilter = computed(
  () => (appIdFilter.value != null && appIdFilter.value > 0) || timeRange.value != null,
);
const hasData = computed(() => rows.value.length > 0);

function formatTime(v: unknown): string {
  if (v == null) return '';
  return String(v).replace('T', ' ').slice(0, 19);
}
function formatDate(v: unknown): string {
  if (v == null) return '';
  return String(v).slice(0, 10);
}
function formatPct(v: unknown): string {
  if (v == null) return '';
  return `${(Number(v) * 100).toFixed(2)}%`;
}
function cellText(row: Row, key: string): string {
  const v = row[key];
  if (v == null) return '';
  if (key === 'StatDate') return formatDate(v);
  if (key === 'CreateTime' || key === 'UpdateTime') return formatTime(v);
  if (key === 'RingRate' || key === 'ErrorRate') return formatPct(v);
  return String(v);
}

/** 当日监控链接列 URL */
function dayMonitorUrl(row: Row): string {
  return `#/Monitors/TraceDayStat?appId=${row.AppId}&date=${formatDate(row.StatDate)}`;
}
/** 分项列链接（带 type 参数） */
function typeDetailUrl(row: Row, key: string): string {
  return `${dayMonitorUrl(row)}&type=${TYPE_MAP[key]}`;
}

function rowClassName({ row }: { row: Row }): string {
  const er = Number(row.ErrorRate ?? 0);
  if (er >= 0.01) return 'ads-row-danger';
  if (er >= 0.001) return 'ads-row-warning';
  return '';
}

function getSummaries(param: { columns: any[]; data: Row[] }): string[] {
  const { columns, data } = param;
  const sums: string[] = [];
  // 编号 ID 列（ALL_COLUMNS 首项，对应 MVC 总计行的「编号」列）
  const idColIdx = columns.findIndex((c) => (c.property as string) === 'ID');
  columns.forEach((col, idx) => {
    if (idx === idColIdx && idColIdx >= 0) {
      sums[idx] = '总计';
      return;
    }
    const prop = col.property as string;
    if (prop === 'AppName') {
      sums[idx] = '';
      return;
    }
    if (prop === 'RingRate' || prop === 'ErrorRate' || prop === 'Cost' || prop === 'MaxCost' || prop === 'MinCost') {
      const vals = data.map((r) => Number((r as any)[prop] ?? 0)).filter((n) => !Number.isNaN(n));
      sums[idx] = vals.length ? String(Math.round(vals.reduce((a, b) => a + b, 0) / vals.length)) : '';
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

const chartOption1 = computed<Record<string, unknown>>(() => {
  const dates = rows.value.map((r) => formatDate(r.StatDate));
  const series: Record<string, unknown>[] = [
    { name: '总次数', type: 'line', smooth: true, showSymbol: false, data: rows.value.map((r) => Number(r.Total ?? 0)) },
    { name: '接口数', type: 'line', smooth: true, showSymbol: false, data: rows.value.map((r) => Number(r.Apis ?? 0)) },
    { name: 'Http请求', type: 'line', smooth: true, showSymbol: false, data: rows.value.map((r) => Number(r.Https ?? 0)) },
    { name: '数据库', type: 'line', smooth: true, showSymbol: false, data: rows.value.map((r) => Number(r.Dbs ?? 0)) },
    { name: '消息队列', type: 'line', smooth: true, showSymbol: false, data: rows.value.map((r) => Number(r.Mqs ?? 0)) },
    { name: 'Redis', type: 'line', smooth: true, showSymbol: false, data: rows.value.map((r) => Number(r.Redis ?? 0)) },
    { name: '其它', type: 'line', smooth: true, showSymbol: false, data: rows.value.map((r) => Number(r.Others ?? 0)) },
    { name: '错误数', type: 'line', smooth: true, showSymbol: false, yAxisIndex: 1, data: rows.value.map((r) => Number(r.Errors ?? 0)), itemStyle: { color: 'rgba(255,0,0,0.5)' } },
  ];
  return {
    tooltip: { trigger: 'axis' },
    legend: { data: ['总次数', '接口数', 'Http请求', '数据库', '消息队列', 'Redis', '其它', '错误数'], bottom: 0, type: 'scroll' },
    grid: { left: 48, right: 48, top: 24, bottom: 48, containLabel: true },
    xAxis: { type: 'category', boundaryGap: false, data: dates, axisLabel: { color: '#606266' } },
    yAxis: [
      { type: 'value', name: '调用次数', axisLabel: { color: '#606266' }, splitLine: { lineStyle: { color: '#EBEDF0' } } },
      { type: 'value', name: '错误数', axisLabel: { color: '#606266' }, splitLine: { show: false } },
    ],
    series,
  };
});

const chartOption2 = computed<Record<string, unknown>>(() => {
  const dates = rows.value.map((r) => formatDate(r.StatDate));
  const series: Record<string, unknown>[] = [
    { name: '平均耗时(ms)', type: 'line', smooth: true, showSymbol: false, data: rows.value.map((r) => Number(r.Cost ?? 0)) },
    { name: '最小耗时(ms)', type: 'line', smooth: true, showSymbol: false, data: rows.value.map((r) => Number(r.MinCost ?? 0)) },
    { name: '最大耗时(ms)', type: 'line', smooth: true, showSymbol: false, yAxisIndex: 1, data: rows.value.map((r) => Number(r.MaxCost ?? 0)) },
  ];
  return {
    tooltip: { trigger: 'axis' },
    legend: { data: ['平均耗时(ms)', '最小耗时(ms)', '最大耗时(ms)'], bottom: 0 },
    grid: { left: 48, right: 48, top: 24, bottom: 48, containLabel: true },
    xAxis: { type: 'category', boundaryGap: false, data: dates, axisLabel: { color: '#606266' } },
    yAxis: [
      { type: 'value', name: '耗时（ms）', axisLabel: { color: '#606266' }, splitLine: { lineStyle: { color: '#EBEDF0' } } },
      { type: 'value', name: '最大耗时（ms）', axisLabel: { color: '#606266' }, splitLine: { show: false } },
    ],
    series,
  };
});

function buildParams(): Record<string, unknown> {
  const p: Record<string, unknown> = { pageIndex: page.value - 1, pageSize: pageSize.value };
  if (appIdFilter.value != null && appIdFilter.value > 0) p.monitorId = appIdFilter.value;
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
    console.error('[AppDayStat] GetList failed', e);
    rows.value = [];
    total.value = 0;
  } finally {
    loading.value = false;
  }
}

/** 批量统计：POST /Monitors/AppDayStat/RetryStat（选中行 keys） */
async function onRetryStat() {
  if (selectedKeys.value.length === 0) {
    ElMessage.warning('请先选择要重新统计的行');
    return;
  }
  try {
    const res: any = await request({
      url: `/Monitors/AppDayStat/RetryStat`,
      method: 'post',
      data: { keys: selectedKeys.value },
    });
    ElMessage.success(res?.message || '批量统计成功');
    await fetchList();
  } catch (e: any) {
    const msg = e?.response?.data?.message || e?.message || '';
    ElMessage.error(`批量统计失败：${msg || '请求出错'}`);
  }
}

function handleSelectionChange(rows: Row[]) {
  selectedKeys.value = rows.map((r) => r.ID ?? -1).filter((id) => id > 0);
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
</script>

<template>
  <div class="list-page app-day-stat-page">
    <div class="lp-header">
      <h2 class="lph-title">
        应用日统计
      </h2>
      <div class="lp-toolbar">
        <el-button
          :loading="loading"
          @click="fetchList"
        >
          刷新
        </el-button>
        <el-button
          type="primary"
          :disabled="selectedKeys.length === 0"
          @click="onRetryStat"
        >
          批量统计
        </el-button>
      </div>
    </div>

    <div class="lp-card-search">
      <div class="ads-search-grid">
        <div class="ads-search-field">
          <label class="ads-search-label">应用编号</label>
          <el-input
            v-model.number="appIdFilter"
            type="number"
            placeholder="AppId"
            class="ads-search-input"
            clearable
          />
        </div>
        <div class="ads-search-field ads-search-field--wide">
          <label class="ads-search-label">统计日期</label>
          <el-date-picker
            v-model="timeRange"
            type="daterange"
            range-separator="~"
            start-placeholder="开始日期"
            end-placeholder="结束日期"
            value-format="YYYY-MM-DD"
            class="ads-search-input ads-search-datepicker"
          />
        </div>
        <div class="ads-search-actions">
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
        <span class="lp-card-title">每日统计曲线（总次数 / 分项 / 错误数）</span>
      </div>
      <div class="lp-card-body ads-chart-body">
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
        <span class="lp-card-title">每日耗时曲线（平均 / 最小 / 最大 耗时）</span>
      </div>
      <div class="lp-card-body ads-chart-body">
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
          class="ads-table"
          stripe
          border
          show-summary
          :summary-method="getSummaries"
          :row-class-name="rowClassName"
          empty-text="暂无数据"
          @selection-change="handleSelectionChange"
        >
          <el-table-column
            type="selection"
            width="45"
          />
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
                v-if="c.name === 'AppName' && showAppCol && row.AppId != null"
                type="primary"
                :underline="false"
                :href="`#/Monitors/AppDayStat?appId=${row.AppId}`"
                target="_self"
              >
                {{ cellText(row, c.name) }}
              </el-link>
              <!-- 埋点数/总次数/错误数/耗时：链接当日监控 -->
              <el-link
                v-else-if="(c.name === 'Names' || c.name === 'Total' || c.name === 'Errors') && row.AppId != null"
                type="primary"
                :underline="false"
                :href="dayMonitorUrl(row)"
                target="_self"
              >
                {{ cellText(row, c.name) }}
              </el-link>
              <!-- 分项列：链接当日监控并带 type 参数 -->
              <el-link
                v-else-if="TYPE_MAP[c.name] && row.AppId != null"
                type="primary"
                :underline="false"
                :href="typeDetailUrl(row, c.name)"
                target="_self"
              >
                {{ cellText(row, c.name) }}
              </el-link>
              <span
                v-else
                :class="c.name === 'ErrorRate' ? 'ads-errorrate' : ''"
              >{{ cellText(row, c.name) }}</span>
            </template>
          </el-table-column>

          <!-- 当日监控独立链接列 -->
          <el-table-column
            label="当日监控"
            min-width="90"
            align="center"
          >
            <template #default="{ row }">
              <el-link
                v-if="row.AppId != null"
                type="primary"
                :underline="false"
                :href="dayMonitorUrl(row)"
                target="_self"
              >
                当日监控
              </el-link>
              <span v-else>-</span>
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
.app-day-stat-page {
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
  .ads-chart-body { min-height: 360px; display: flex; align-items: stretch; }
  .lp-card-footer { padding: 12px 4px 0; border-top: 1px solid var(--el-border-color-light); }
}

.ads-search-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 12px 16px;
  align-items: end;
}
.ads-search-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;
  &--wide { grid-column: span 2; }
}
.ads-search-label {
  font-size: 12px;
  font-weight: 500;
  color: var(--el-text-color-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.ads-search-input {
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
.ads-search-datepicker { :deep(.el-range-editor) { width: 100%; } }
.ads-search-actions { display: flex; justify-content: flex-end; gap: 8px; padding-bottom: 0; }
.ads-table { width: 100%; }
.ads-errorrate { font-weight: 600; }

.ads-row-danger { --el-table-tr-bg-color: var(--el-color-danger-light-9); td { color: var(--el-color-danger); } }
.ads-row-warning { --el-table-tr-bg-color: var(--el-color-warning-light-9); td { color: var(--el-color-warning); } }

@media (max-width: 900px) { .ads-search-grid { grid-template-columns: repeat(2, 1fr); } }
@media (max-width: 560px) {
  .ads-search-grid { grid-template-columns: 1fr; }
  .ads-search-field--wide { grid-column: span 1; }
}
</style>
