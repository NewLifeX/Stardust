<script setup lang="ts">
/**
 * Redis/RedisData/Index — Redis 性能时序数据（自定义迁移页 + ECharts 曲线）
 *
 * MVC 源：隐藏页 [Menu(40,false)]，原 cshtml 用 ECharts 渲染 Redis 性能时序曲线，后端迁移时已删除绘图。
 * 前端自建视图兜底（框架 DefaultEntity 已支持无菜单时按视图目录兜底），#/Redis/RedisData 可访问。
 *
 * 数据：GET /Redis/RedisData（GetPage，后端 Search 支持 redisId/dtStart/dtEnd），
 *       行数据即图表数据源。字段见 Stardust.Data.Nodes.RedisData：
 *       RedisId / Name / Speed(每秒操作数) / InputKbps / OutputKbps / Uptime /
 *       ConnectedClients(连接数) / UsedMemory(已用内存MB) / FragmentationRatio /
 *       Keys / ExpiredKeys / EvictedKeys / KeySpaceHits / KeySpaceMisses /
 *       Commands(命令数) / Reads / Writes / AvgTtl / CreateTime
 *       （Id、TopCommand 已在后端 ListFields 移除）
 *
 * 图表：X=CreateTime(时间轴)，Y=Speed(每秒操作数)/InputKbps(入流量)/
 *       OutputKbps(出流量)/ConnectedClients(连接数)/UsedMemory(已用内存MB)/Keys(Keys数)
 *       曲线（line），与 MVC RedisDataController ECharts 系列一致（不含 Commands）。
 *       种子无数据时显示空状态提示。
 *
 * 前端经 usePageApi + request，自定义 Action 写相对无前缀路径（request 自动拼 baseURL）。
 */
import { ref, computed, onMounted, watch } from 'vue';
import EChart from '@/components/EChart.vue';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';

const api = usePageApi('Redis', 'RedisData');

interface Row {
  ID?: number;
  RedisId?: number;
  Name?: string;
  Speed?: number;
  InputKbps?: number;
  OutputKbps?: number;
  Uptime?: number;
  ConnectedClients?: number;
  UsedMemory?: number;
  FragmentationRatio?: number;
  Keys?: number;
  ExpiredKeys?: number;
  EvictedKeys?: number;
  KeySpaceHits?: number;
  KeySpaceMisses?: number;
  Commands?: number;
  Reads?: number;
  Writes?: number;
  AvgTtl?: number;
  CreateTime?: string;
  TraceId?: string;
  Remark?: string;
  [k: string]: unknown;
}

/** 标准列表列（Id、TopCommand 已隐藏，顺序对齐 MVC RedisDataController 静态 ListFields；列名以实体 DisplayName 为权威） */
type Col = { name: string; label: string; minWidth?: number; align?: 'left' | 'center' | 'right' };
const ALL_COLUMNS: Col[] = [
  { name: 'RedisId', label: 'Redis节点', minWidth: 90, align: 'right' },
  { name: 'Name', label: '名称', minWidth: 120 },
  { name: 'Speed', label: '速度', minWidth: 90, align: 'right' },
  { name: 'InputKbps', label: '入流量', minWidth: 100, align: 'right' },
  { name: 'OutputKbps', label: '出流量', minWidth: 100, align: 'right' },
  { name: 'Uptime', label: '启动时间', minWidth: 100, align: 'right' },
  { name: 'ConnectedClients', label: '连接数', minWidth: 80, align: 'right' },
  { name: 'UsedMemory', label: '已用内存', minWidth: 100, align: 'right' },
  { name: 'FragmentationRatio', label: '碎片率', minWidth: 80, align: 'right' },
  { name: 'Keys', label: 'Keys数', minWidth: 80, align: 'right' },
  { name: 'ExpiredKeys', label: '过期Keys', minWidth: 90, align: 'right' },
  { name: 'EvictedKeys', label: '驱逐Keys', minWidth: 90, align: 'right' },
  { name: 'KeySpaceHits', label: '命中数', minWidth: 90, align: 'right' },
  { name: 'KeySpaceMisses', label: 'Miss数', minWidth: 90, align: 'right' },
  { name: 'Commands', label: '命令数', minWidth: 80, align: 'right' },
  { name: 'Reads', label: '读取数', minWidth: 80, align: 'right' },
  { name: 'Writes', label: '写入数', minWidth: 80, align: 'right' },
  { name: 'AvgTtl', label: '平均过期', minWidth: 100, align: 'right' },
  { name: 'CreateTime', label: '创建时间', minWidth: 150 },
  { name: 'TraceId', label: '追踪', minWidth: 120 },
  { name: 'Remark', label: '备注', minWidth: 150 },
];

/** 搜索：redisId 节点（加分项）+ 日期范围 dtStart/dtEnd */
const redisIdFilter = ref<number | null>(null);
const timeRange = ref<[string, string] | null>(null);

const loading = ref(false);
const rows = ref<Row[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(50);

const hasActiveFilter = computed(
  () => (redisIdFilter.value != null && redisIdFilter.value > 0) || timeRange.value != null,
);

const hasData = computed(() => rows.value.length > 0);

function formatTime(v: unknown): string {
  if (v == null) return '';
  return String(v).replace('T', ' ').slice(0, 19);
}

function cellText(row: Row, key: string): string {
  const v = row[key];
  if (v == null) return '';
  if (key === 'CreateTime') return formatTime(v);
  return String(v);
}

/** ECharts option：X=CreateTime，Y=多指标曲线 */
const chartOption = computed<Record<string, unknown>>(() => {
  const times = rows.value.map((r) => formatTime(r.CreateTime));
  const speed = rows.value.map((r) => Number(r.Speed ?? 0));
  const clients = rows.value.map((r) => Number(r.ConnectedClients ?? 0));
  const memory = rows.value.map((r) => Number(r.UsedMemory ?? 0));
  const keys = rows.value.map((r) => Number(r.Keys ?? 0));
  const input = rows.value.map((r) => Number(r.InputKbps ?? 0));
  const output = rows.value.map((r) => Number(r.OutputKbps ?? 0));

  return {
    tooltip: { trigger: 'axis' },
    legend: {
      data: ['每秒操作', '入流量', '出流量', '连接数', '已用内存(MB)', 'Keys数'],
      bottom: 0,
    },
    grid: { left: 56, right: 24, top: 24, bottom: 56, containLabel: true },
    xAxis: {
      type: 'category',
      boundaryGap: false,
      data: times,
      axisLabel: { color: '#606266' },
    },
    yAxis: {
      type: 'value',
      axisLabel: { color: '#606266' },
      splitLine: { lineStyle: { color: '#EBEDF0' } },
    },
    series: [
      { name: '每秒操作', type: 'line', smooth: true, showSymbol: false, data: speed },
      { name: '入流量', type: 'line', smooth: true, showSymbol: false, data: input },
      { name: '出流量', type: 'line', smooth: true, showSymbol: false, data: output },
      { name: '连接数', type: 'line', smooth: true, showSymbol: false, data: clients },
      { name: '已用内存(MB)', type: 'line', smooth: true, showSymbol: false, data: memory },
      { name: 'Keys数', type: 'line', smooth: true, showSymbol: false, data: keys },
    ],
  };
});

function buildParams(): Record<string, unknown> {
  const p: Record<string, unknown> = {
    pageIndex: page.value - 1,
    pageSize: pageSize.value,
  };
  if (redisIdFilter.value != null && redisIdFilter.value > 0) p.redisId = redisIdFilter.value;
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
    console.error('[Redis/RedisData] GetList failed', e);
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
  redisIdFilter.value = null;
  timeRange.value = null;
  page.value = 1;
  fetchList();
}

function onPageChange(p: number) {
  page.value = p;
  fetchList();
}

onMounted(async () => {
  // 从 hash 读取 redisId（来自 RedisNode 监控列跳转：#/Redis/RedisData?redisId={Id}）
  const m = location.hash.match(/[?&]redisId=(\d+)/);
  if (m) redisIdFilter.value = Number(m[1]);
  await fetchList();
});

// 数据变化无需额外处理：EChart 组件内部 watch option 自动重绘
watch(rows, () => {}, { deep: true });
</script>

<template>
  <div class="list-page redis-data-page">
    <div class="lp-header">
      <h2 class="lph-title">
        Redis 性能时序数据
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

    <!-- 搜索栏：redisId 节点 + 日期范围 -->
    <div class="lp-card-search">
      <div class="rdd-search-grid">
        <div class="rdd-search-field">
          <label class="rdd-search-label">节点编号</label>
          <el-input
            v-model.number="redisIdFilter"
            type="number"
            placeholder="RedisId"
            class="rdd-search-input"
            clearable
          />
        </div>

        <div class="rdd-search-field rdd-search-field--wide">
          <label class="rdd-search-label">统计时间</label>
          <el-date-picker
            v-model="timeRange"
            type="daterange"
            range-separator="~"
            start-placeholder="开始日期"
            end-placeholder="结束日期"
            value-format="YYYY-MM-DD"
            class="rdd-search-input rdd-search-datepicker"
          />
        </div>

        <div class="rdd-search-actions">
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
        <span class="lp-card-title">Redis 性能时序</span>
        <span class="lp-card-sub">每秒操作 / 入出流量 / 连接数 / 已用内存 / Keys数</span>
      </div>
      <div class="lp-card-body rdd-chart-body">
        <EChart
          v-if="hasData"
          :option="chartOption"
          height="360px"
        />
        <el-empty
          v-else
          description="暂无性能时序数据"
        />
      </div>
    </div>

    <!-- 列表卡片 -->
    <div class="lp-card">
      <div class="lp-card-body">
        <el-table
          v-loading="loading"
          :data="rows"
          class="rdd-table"
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
              <span>{{ cellText(row, c.name) }}</span>
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
.redis-data-page {
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

  .rdd-chart-body {
    min-height: 360px;
    display: flex;
    align-items: stretch;
  }

  .lp-card-footer {
    padding: 12px 4px 0;
    border-top: 1px solid var(--el-border-color-light);
  }
}

.rdd-search-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 12px 16px;
  align-items: end;
}

.rdd-search-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;

  &--wide {
    grid-column: span 2;
  }
}

.rdd-search-label {
  font-size: 12px;
  font-weight: 500;
  color: var(--el-text-color-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.rdd-search-input {
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

.rdd-search-datepicker {
  :deep(.el-range-editor) {
    width: 100%;
  }
}

.rdd-search-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  padding-bottom: 0;
}

.rdd-table {
  width: 100%;
}

@media (max-width: 900px) {
  .rdd-search-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .rdd-search-grid {
    grid-template-columns: 1fr;
  }

  .rdd-search-field--wide {
    grid-column: span 1;
  }
}
</style>
