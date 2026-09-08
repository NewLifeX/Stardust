<script setup lang="ts">
/**
 * Registry/AppMeter/Index — 应用性能（自定义迁移页 + ECharts 曲线）
 *
 * MVC 源：隐藏页 [Menu(0,false)]，原 cshtml 用 ECharts 渲染 CPU/内存/线程/句柄等性能时序曲线，后端迁移时已删除绘图。
 * 前端自建视图兜底（框架 DefaultEntity 已支持无菜单时按视图目录兜底），#/Registry/AppMeter 可访问。
 *
 * 数据：GET /Registry/AppMeter（GetPage，后端 Search 按 Id 降序，appId 指定时 PageSize 调整到近24小时），
 *       行数据即图表数据源。字段见 Stardust.Data.AppMeter（应用性能.cs）：
 *       AppId / ClientId(实例) / NodeId / Source / Memory(内存M) / ProcessorTime / CpuUsage(CPU负载%) /
 *       Threads(线程数) / WorkerThreads / IOThreads / AvailableThreads / PendingItems / CompletedItems /
 *       Handles(句柄数) / Connections(连接数) / GCCount(GC次数) / HeapSize(堆内存M) / Time(采集时间)
 *
 * 图表：X=Time(采集时间)，Y=多性能指标曲线（内存/CPU负载/线程数/句柄数/连接数/堆内存/GC次数）。
 *       CPU负载(%)使用右侧 Y 轴（百分比），其余使用左侧值轴。
 *       种子无数据时显示空状态提示。
 *
 * 前端经 usePageApi + request，自定义 Action 写相对无前缀路径（request 自动拼 baseURL）。
 */
import { ref, computed, onMounted } from 'vue';
import EChart from '@/components/EChart.vue';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';
import type { FieldMeta } from '@newlifex/cube-vue/core/types/field';

const api = usePageApi('Registry', 'AppMeter');

interface Row {
  [k: string]: unknown;
}

interface BackendField {
  name: string;
  displayName: string;
  typeName: string;
  itemType?: string;
  primaryKey?: boolean;
  [k: string]: unknown;
}
interface PageMeta {
  list: BackendField[];
  [k: string]: unknown;
}

/** 后端下发的列表字段（WebApi RemoveField("Id")，其余列顺序与 MVC 一致，动态取以保证对齐） */
const listFields = ref<FieldMeta[]>([]);

function toFieldMeta(f: BackendField): FieldMeta {
  return {
    name: f.name,
    displayName: f.displayName,
    typeName: f.typeName,
    itemType: f.itemType,
    primaryKey: f.primaryKey,
  };
}

async function fetchMeta() {
  try {
    const res: any = await api.getPage();
    const meta = (res?.data ?? res) as PageMeta;
    if (meta?.list?.length) {
      listFields.value = meta.list.map(toFieldMeta);
    }
  } catch (e) {
    console.error('[AppMeter] GetPage failed', e);
  }
}

/** 搜索：MVC _List_Search 仅日期范围(dtStart/dtEnd)；appId 经链接带参进入（hash 预填，不展示输入框） */
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

function formatDateTime(v: unknown): string {
  if (v == null) return '';
  return String(v).replace('T', ' ').slice(0, 19);
}

function cellText(row: Row, key: string): string {
  const v = row[key];
  if (v == null) return '';
  if (key === 'Time' || key === 'CreateTime') return formatDateTime(v);
  return String(v);
}

/** ECharts option：X=Time，Y=内存/CPU负载/线程/句柄/连接/堆/GC 多指标曲线 */
const chartOption = computed<Record<string, unknown>>(() => {
  // 按采集时间升序
  const sorted = [...rows.value].sort(
    (a, b) => String(a.Time ?? '').localeCompare(String(b.Time ?? '')),
  );
  const times = sorted.map((r) => formatDateTime(r.Time));

  // 值指标（左侧轴） & 百分比指标（右侧轴：CPU负载）
  // 对齐 MVC AppMeterController.Search 的 chart.Add 系列顺序：
  // Memory / CpuUsage(右轴) / Threads / AvailableThreads / PendingItems / CompletedItems / Handles / Connections / HeapSize / GCCount
  const valueMetrics: Array<{ key: keyof Row; label: string }> = [
    { key: 'Memory', label: '内存(M)' },
    { key: 'Threads', label: '线程数' },
    { key: 'AvailableThreads', label: '活跃线程' },
    { key: 'PendingItems', label: '挂起任务' },
    { key: 'CompletedItems', label: '完成任务' },
    { key: 'Handles', label: '句柄数' },
    { key: 'Connections', label: '连接数' },
    { key: 'HeapSize', label: '堆内存(M)' },
    { key: 'GCCount', label: 'GC次数' },
  ];
  const pctMetrics: Array<{ key: keyof Row; label: string }> = [
    { key: 'CpuUsage', label: 'CPU负载(%)' },
  ];

  const legendData: string[] = [];
  const series: Record<string, unknown>[] = [];

  for (const m of valueMetrics) {
    legendData.push(m.label);
    series.push({
      name: m.label,
      type: 'line',
      smooth: true,
      showSymbol: false,
      yAxisIndex: 0,
      data: sorted.map((r) => Number(r[m.key] ?? 0)),
    });
  }
  for (const m of pctMetrics) {
    legendData.push(m.label);
    series.push({
      name: m.label,
      type: 'line',
      smooth: true,
      showSymbol: false,
      yAxisIndex: 1,
      data: sorted.map((r) => Number(r[m.key] ?? 0)),
    });
  }

  return {
    tooltip: { trigger: 'axis' },
    legend: { data: legendData, bottom: 0, type: 'scroll' },
    grid: { left: 48, right: 48, top: 24, bottom: 48, containLabel: true },
    xAxis: {
      type: 'category',
      boundaryGap: false,
      data: times,
      axisLabel: { color: '#606266' },
    },
    yAxis: [
      {
        type: 'value',
        name: '指标',
        axisLabel: { color: '#606266' },
        splitLine: { lineStyle: { color: '#EBEDF0' } },
      },
      {
        type: 'value',
        name: '百分比(%)',
        axisLabel: { color: '#606266', formatter: '{value}%' },
        splitLine: { show: false },
      },
    ],
    series,
  };
});

function buildParams(): Record<string, unknown> {
  const p: Record<string, unknown> = {
    pageIndex: page.value - 1,
    pageSize: pageSize.value,
  };
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
    console.error('[AppMeter] GetList failed', e);
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
  // 从 hash 读取 appId（来自其他页跳转）
  const m = location.hash.match(/[?&]appId=(\d+)/);
  if (m) appIdFilter.value = Number(m[1]);
  await fetchMeta();
  await fetchList();
});
</script>

<template>
  <div class="list-page app-meter-page">
    <div class="lp-header">
      <h2 class="lph-title">
        应用性能
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

    <!-- 搜索栏：采集时间（MVC _List_Search 仅日期范围；appId 由链接带参进入） -->
    <div class="lp-card-search">
      <div class="am-search-grid">
        <div class="am-search-field am-search-field--wide">
          <label class="am-search-label">采集时间</label>
          <el-date-picker
            v-model="timeRange"
            type="daterange"
            range-separator="~"
            start-placeholder="开始日期"
            end-placeholder="结束日期"
            value-format="YYYY-MM-DD"
            class="am-search-input am-search-datepicker"
          />
        </div>

        <div class="am-search-actions">
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

    <!-- 图表卡片 -->
    <div class="lp-card">
      <div class="lp-card-header">
        <span class="lp-card-title">应用性能曲线</span>
        <span class="lp-card-sub">内存 / CPU负载 / 线程 / 句柄 / 连接 / 堆 / GC</span>
      </div>
      <div class="lp-card-body am-chart-body">
        <EChart
          v-if="hasData"
          :option="chartOption"
          height="360px"
        />
        <el-empty
          v-else
          description="暂无性能数据"
        />
      </div>
    </div>

    <!-- 列表卡片 -->
    <div class="lp-card">
      <div class="lp-card-body">
        <el-table
          v-loading="loading"
          :data="rows"
          class="am-table"
          stripe
          border
          empty-text="暂无数据"
        >
          <el-table-column
            v-for="f in listFields"
            :key="f.name"
            :prop="f.name"
            :label="f.displayName || f.name"
            show-overflow-tooltip
          >
            <template #default="{ row }">
              <span>{{ cellText(row, f.name) }}</span>
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
          @size-change="
            (s: number) => {
              pageSize = s;
              page = 1;
              fetchList();
            }
          "
        />
      </div>
    </div>
  </div>
</template>

<style lang="scss" scoped>
.app-meter-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: auto;

  .lp-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: var(--el-main-padding, 24px) var(--el-main-padding, 24px) 0;

    .lph-title {
      margin: 0;
      font-size: 18px;
      font-weight: 600;
      color: var(--el-text-color-primary);
    }

    .lp-toolbar {
      display: flex;
      gap: 8px;
    }
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

    .lp-card-title {
      font-size: 15px;
      font-weight: 600;
      color: var(--el-text-color-primary);
    }

    .lp-card-sub {
      font-size: 12px;
      color: var(--el-text-color-secondary);
    }
  }

  .lp-card-body {
    padding-top: 16px;
    min-height: 200px;
  }

  .am-chart-body {
    min-height: 360px;
    display: flex;
    align-items: stretch;
  }

  .lp-card-footer {
    padding: 12px 4px 0;
    border-top: 1px solid var(--el-border-color-light);
  }
}

.am-search-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 12px 16px;
  align-items: end;
}

.am-search-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;

  &--wide {
    grid-column: span 2;
  }
}

.am-search-label {
  font-size: 12px;
  font-weight: 500;
  color: var(--el-text-color-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.am-search-input {
  width: 100%;

  :deep(.el-input__wrapper),
  :deep(.el-range-editor) {
    min-height: 36px;
    border-radius: var(--el-border-radius-base);
    background: var(--el-bg-color);
    border: 1px solid var(--el-border-color-light);
    box-shadow: none;
    transition:
      border-color 0.15s ease,
      background 0.15s ease,
      box-shadow 0.15s ease;
  }

  :deep(.el-input__wrapper.is-focus),
  :deep(.el-range-editor.is-active) {
    background: var(--el-fill-color-blank);
    border-color: var(--el-color-primary);
    box-shadow:
      0 0 0 1px var(--el-color-primary) inset,
      0 0 0 3px var(--el-color-primary-light-8);
  }

  :deep(.el-input__inner) {
    font-size: 13px;
    color: var(--el-text-color-primary);
  }
}

.am-search-datepicker {
  :deep(.el-range-editor) {
    width: 100%;
  }
}

.am-search-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  padding-bottom: 0;
}

.am-table {
  width: 100%;
}

@media (max-width: 900px) {
  .am-search-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .am-search-grid {
    grid-template-columns: 1fr;
  }

  .am-search-field--wide {
    grid-column: span 1;
  }
}
</style>
