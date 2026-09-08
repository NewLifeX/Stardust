<script setup lang="ts">
/**
 * Nodes/NodeStat/Index — 节点统计（自定义迁移页 + ECharts 曲线）
 *
 * MVC 源：可见页 [Menu(60)]，路由自动注册，#/Nodes/NodeStat 可访问。
 * 原 cshtml 用 ECharts 渲染节点数量/在线率等趋势，后端迁移时已删除绘图。
 * 前端自建视图兜底（框架 DefaultEntity 按视图目录兜底），复用通用 EChart 组件。
 *
 * 数据：GET /Nodes/NodeStat（GetPage，后端 Search 支持 category/key/dtStart/dtEnd，
 *       默认 category=操作系统，带日期时按 StatDate 升序 PageSize 100），
 *       行数据即图表数据源。字段见 Stardust.Data.Nodes.NodeStat：
 *       Category(类别) / StatDate(统计日期) / Key(统计项) /
 *       Total(总数) / Actives(活跃数) / ActivesT7 / ActivesT30 /
 *       News(新增数) / NewsT7 / NewsT30 / CreateTime / UpdateTime
 *
 * 图表：X=StatDate，Y=Total(总数)/Actives(活跃数)/News(新增数) 三条曲线（line）。
 *       种子无数据时显示空状态提示。
 *
 * 前端经 usePageApi + request，自定义 Action 写相对无前缀路径（request 自动拼 baseURL）。
 */
import { ref, computed, onMounted } from 'vue';
import EChart from '@/components/EChart.vue';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';

const api = usePageApi('Nodes', 'NodeStat');

interface Row {
  ID?: number;
  Category?: string;
  StatDate?: string;
  Key?: string;
  LinkItem?: string;
  Total?: number;
  Actives?: number;
  ActivesT7?: number;
  ActivesT30?: number;
  News?: number;
  NewsT7?: number;
  NewsT30?: number;
  CreateTime?: string;
  UpdateTime?: string;
  [k: string]: unknown;
}

/** 标准列表列（ID/LinkItem/CreateTime/UpdateTime/Remark 由后端 ListFields 隐藏） */
type Col = { name: string; label: string; minWidth?: number; align?: 'left' | 'center' | 'right' };
const ALL_COLUMNS: Col[] = [
  { name: 'StatDate', label: '统计日期', minWidth: 120 },
  { name: 'Category', label: '类别', minWidth: 100 },
  { name: 'Key', label: '统计项', minWidth: 120 },
  { name: 'Total', label: '总数', minWidth: 90, align: 'right' },
  { name: 'Actives', label: '活跃数', minWidth: 90, align: 'right' },
  { name: 'ActivesT7', label: '7天活跃', minWidth: 90, align: 'right' },
  { name: 'ActivesT30', label: '30天活跃', minWidth: 100, align: 'right' },
  { name: 'News', label: '新增数', minWidth: 90, align: 'right' },
  { name: 'NewsT7', label: '7天新增', minWidth: 90, align: 'right' },
  { name: 'NewsT30', label: '30天新增', minWidth: 100, align: 'right' },
  { name: 'nodes', label: '明细', minWidth: 90, align: 'center' },
];

/** 搜索：category 下拉（加分项）+ Key（节点/城市/版本维度）+ 日期范围 dtStart/dtEnd */
const CATEGORIES = [
  '操作系统',
  '版本',
  '城市',
  '运行时',
  '核心数',
  '内存',
  '架构',
  '.NET',
];
const categoryFilter = ref<string>('操作系统');
const keyFilter = ref<string>('');
const timeRange = ref<[string, string] | null>(null);

const loading = ref(false);
const rows = ref<Row[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);

const hasActiveFilter = computed(
  () =>
    (categoryFilter.value != null && categoryFilter.value.length > 0) ||
    (keyFilter.value != null && keyFilter.value.length > 0) ||
    timeRange.value != null,
);

const hasData = computed(() => rows.value.length > 0);

function formatDate(v: unknown): string {
  if (v == null) return '';
  // StatDate 形如 2026-08-09T00:00:00，只取日期部分
  return String(v).slice(0, 10);
}

function formatTime(v: unknown): string {
  if (v == null) return '';
  return String(v).replace('T', ' ').slice(0, 19);
}

function cellText(row: Row, key: string): string {
  const v = row[key];
  if (v == null) return '';
  if (key === 'StatDate') return formatDate(v);
  if (key === 'CreateTime' || key === 'UpdateTime') return formatTime(v);
  return String(v);
}

/** ECharts option：X=StatDate，Y=Total/Actives/News 三条曲线 */
const chartOption = computed<Record<string, unknown>>(() => {
  const dates = rows.value.map((r) => formatDate(r.StatDate));
  const totals = rows.value.map((r) => Number(r.Total ?? 0));
  const actives = rows.value.map((r) => Number(r.Actives ?? 0));
  const news = rows.value.map((r) => Number(r.News ?? 0));

  return {
    tooltip: { trigger: 'axis' },
    legend: { data: ['总数', '活跃数', '新增数'], bottom: 0 },
    grid: { left: 48, right: 24, top: 24, bottom: 48, containLabel: true },
    xAxis: {
      type: 'category',
      boundaryGap: false,
      data: dates,
      axisLabel: { color: '#606266' },
    },
    yAxis: {
      type: 'value',
      axisLabel: { color: '#606266' },
      splitLine: { lineStyle: { color: '#EBEDF0' } },
    },
    series: [
      { name: '总数', type: 'line', smooth: true, showSymbol: false, data: totals },
      { name: '活跃数', type: 'line', smooth: true, showSymbol: false, data: actives },
      { name: '新增数', type: 'line', smooth: true, showSymbol: false, data: news },
    ],
  };
});

function buildParams(): Record<string, unknown> {
  const p: Record<string, unknown> = {
    pageIndex: page.value - 1,
    pageSize: pageSize.value,
  };
  if (categoryFilter.value) p.category = categoryFilter.value;
  if (keyFilter.value) p.key = keyFilter.value;
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
    console.error('[NodeStat] GetList failed', e);
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
  categoryFilter.value = '操作系统';
  keyFilter.value = '';
  timeRange.value = null;
  page.value = 1;
  fetchList();
}

function onPageChange(p: number) {
  page.value = p;
  fetchList();
}

onMounted(async () => {
  // 从 hash 读取 category（来自其他页跳转）
  const m = location.hash.match(/[?&]category=([^&]+)/);
  if (m) categoryFilter.value = decodeURIComponent(m[1]);
  await fetchList();
});
</script>

<template>
  <div class="list-page node-stat-page">
    <div class="lp-header">
      <h2 class="lph-title">
        节点统计
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

    <!-- 搜索栏：category + Key + 日期范围 -->
    <div class="lp-card-search">
      <div class="ns-search-grid">
        <div class="ns-search-field">
          <label class="ns-search-label">类别</label>
          <el-select
            v-model="categoryFilter"
            placeholder="类别"
            class="ns-search-input"
            clearable
          >
            <el-option
              v-for="c in CATEGORIES"
              :key="c"
              :label="c"
              :value="c"
            />
          </el-select>
        </div>

        <div class="ns-search-field">
          <label class="ns-search-label">统计项</label>
          <el-input
            v-model="keyFilter"
            placeholder="Key（节点/城市/版本）"
            class="ns-search-input"
            clearable
          />
        </div>

        <div class="ns-search-field ns-search-field--wide">
          <label class="ns-search-label">统计日期</label>
          <el-date-picker
            v-model="timeRange"
            type="daterange"
            range-separator="~"
            start-placeholder="开始日期"
            end-placeholder="结束日期"
            value-format="YYYY-MM-DD"
            class="ns-search-input ns-search-datepicker"
          />
        </div>

        <div class="ns-search-actions">
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
        <span class="lp-card-title">节点统计曲线</span>
        <span class="lp-card-sub">总数 / 活跃数 / 新增数</span>
      </div>
      <div class="lp-card-body ns-chart-body">
        <EChart
          v-if="hasData"
          :option="chartOption"
          height="360px"
        />
        <el-empty
          v-else
          description="暂无统计数据"
        />
      </div>
    </div>

    <!-- 列表卡片 -->
    <div class="lp-card">
      <div class="lp-card-body">
        <el-table
          v-loading="loading"
          :data="rows"
          class="ns-table"
          stripe
          border
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
                v-if="c.name === 'nodes'"
                type="primary"
                :underline="false"
                :href="`#/Nodes/NodeStat?category=${encodeURIComponent(row.Category ?? '')}&dtStart=${formatDate(row.StatDate)}&dtEnd=${formatDate(row.StatDate)}`"
                target="_blank"
              >
                明细
              </el-link>
              <span v-else>{{ cellText(row, c.name) }}</span>
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
.node-stat-page {
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

  .ns-chart-body {
    min-height: 360px;
    display: flex;
    align-items: stretch;
  }

  .lp-card-footer {
    padding: 12px 4px 0;
    border-top: 1px solid var(--el-border-color-light);
  }
}

.ns-search-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 12px 16px;
  align-items: end;
}

.ns-search-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;

  &--wide {
    grid-column: span 2;
  }
}

.ns-search-label {
  font-size: 12px;
  font-weight: 500;
  color: var(--el-text-color-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.ns-search-input {
  width: 100%;

  :deep(.el-input__wrapper),
  :deep(.el-range-editor),
  :deep(.el-select__wrapper) {
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
  :deep(.el-range-editor.is-active),
  :deep(.el-select__wrapper.is-focused) {
    background: var(--el-fill-color-blank);
    border-color: var(--el-color-primary);
    box-shadow:
      0 0 0 1px var(--el-color-primary) inset,
      0 0 0 3px var(--el-color-primary-light-8);
  }

  :deep(.el-input__inner),
  :deep(.el-select__placeholder) {
    font-size: 13px;
    color: var(--el-text-color-primary);
  }
}

.ns-search-datepicker {
  :deep(.el-range-editor) {
    width: 100%;
  }
}

.ns-search-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  padding-bottom: 0;
}

.ns-table {
  width: 100%;
}

@media (max-width: 900px) {
  .ns-search-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .ns-search-grid {
    grid-template-columns: 1fr;
  }

  .ns-search-field--wide {
    grid-column: span 1;
  }
}
</style>
