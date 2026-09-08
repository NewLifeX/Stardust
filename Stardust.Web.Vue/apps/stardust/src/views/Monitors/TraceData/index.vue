<script setup lang="ts">
/**
 * Monitors/TraceData/Index — 调用链采样监控列表（自定义迁移页）
 *
 * MVC 源 _List_Data（自定义渲染）+ _List_Search 还原：
 *  - 隐藏页 [Menu(0,false)]，靠自定义视图兜底（框架 DefaultEntity 已支持无菜单时按视图目录兜底）
 *  - 列表列（GetPage 字段名，ID 已 RemoveField 隐藏）：
 *      应用 AppName(链接?appId=) / 实例 ClientId(链接) / 应用节点 NodeName(链接/Nodes/Node?Id=) /
 *      操作名 Name(链接) / 追踪(按钮 Trace?id=) / 总数 Total / 错误 Errors / 平均 Cost / 最大 MaxCost /
 *      最小 MinCost / 数值 TotalValue / 正常 Samples / 异常 ErrorSamples / 开始时间 Start /
 *      创建时间 CreateTime / 创建地址 CreateIP
 *      （与 MVC _List_Data 一致，不渲染 End/StatDate/StatHour/StatMinute/TotalCost/LinkId 等后台统计列）
 *  - 总计行：聚合 Total/Errors/Cost/MaxCost/MinCost/TotalValue/Samples/ErrorSamples（Cost 取平均整数）
 *  - 搜索：searchTag 复选框 + 日期范围 dtStart/dtEnd（加分项 appId/itemId 下拉）
 *  - 自定义 Action：Trace(GET /Monitors/TraceData/Trace?id=<行Id>)
 *      成功 el-notification 提示 traceId；失败（后端抛「找不到采样数据」）提示「无采样数据」
 *  - 后端 API：GET /Monitors/TraceData（GetPage）、Trace 已实现
 *
 * 前端经 usePageApi + request，自定义 Action 写相对无前缀路径 /Monitors/TraceData/Trace
 * （request 自动拼 baseURL，勿写 /api/）。
 */
import { ref, computed, onMounted } from 'vue';
import { ElMessage, ElNotification } from 'element-plus';
import request from '@newlifex/cube-vue/core/utils/request';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';

const api = usePageApi('Monitors', 'TraceData');

interface Row {
  Id?: number;
  AppId?: number;
  AppName?: string;
  ClientId?: string;
  ItemId?: number;
  NodeId?: number;
  NodeName?: string;
  Name?: string;
  Total?: number;
  Errors?: number;
  Cost?: number;
  MaxCost?: number;
  MinCost?: number;
  TotalValue?: number;
  Samples?: number;
  ErrorSamples?: number;
  LinkId?: number;
  Start?: string;
  CreateTime?: string;
  CreateIP?: string;
  [k: string]: unknown;
}

/** 标准列（ID 已隐藏） */
type Col = { name: string; label: string; minWidth?: number; align?: 'left' | 'center' | 'right' };
const ALL_COLUMNS: Col[] = [
  { name: 'AppName', label: '应用', minWidth: 120 },
  { name: 'ClientId', label: '实例', minWidth: 130 },
  { name: 'NodeName', label: '应用节点', minWidth: 120 },
  { name: 'Name', label: '操作名', minWidth: 120 },
  // 追踪列：与 MVC 一致，位于「操作名」之后、「总数」之前（特殊列，模板内渲染按钮）
  { name: '__trace__', label: '追踪', minWidth: 70, align: 'center' },
  { name: 'Total', label: '总数', minWidth: 70, align: 'right' },
  { name: 'Errors', label: '错误', minWidth: 70, align: 'right' },
  { name: 'Cost', label: '平均', minWidth: 70, align: 'right' },
  { name: 'MaxCost', label: '最大', minWidth: 80, align: 'right' },
  { name: 'MinCost', label: '最小', minWidth: 80, align: 'right' },
  { name: 'TotalValue', label: '数值', minWidth: 80, align: 'right' },
  { name: 'Samples', label: '正常', minWidth: 70, align: 'right' },
  { name: 'ErrorSamples', label: '异常', minWidth: 70, align: 'right' },
  { name: 'Start', label: '开始时间', minWidth: 150 },
  { name: 'CreateTime', label: '创建时间', minWidth: 150 },
  { name: 'CreateIP', label: '创建地址', minWidth: 110 },
];

/** 参与总计聚合的数值列（Cost 在 footer 中取平均，不参与求和） */
const SUM_COLUMNS = ['Total', 'Errors', 'MaxCost', 'MinCost', 'TotalValue', 'Samples', 'ErrorSamples'];

/** 搜索：searchTag 复选框 + 时间范围 dtStart/dtEnd（加分项 appId/itemId） */
const searchTag = ref<boolean>(false);
const timeRange = ref<[string, string] | null>(null);
const appIdFilter = ref<number | null>(null);
const itemIdFilter = ref<number | null>(null);

const loading = ref(false);
const rows = ref<Row[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);

const hasActiveFilter = computed(
  () => searchTag.value || timeRange.value != null || (appIdFilter.value != null && appIdFilter.value > 0) || (itemIdFilter.value != null && itemIdFilter.value > 0),
);

function formatTime(v: unknown): string {
  if (v == null) return '';
  return String(v).replace('T', ' ').slice(0, 19);
}

function cellText(row: Row, key: string): string {
  const v = row[key];
  if (v == null) return '';
  if (key === 'Start' || key === 'End' || key === 'CreateTime') return formatTime(v);
  if (key === 'NodeName') return String(v);
  return String(v);
}

/** 链接列 URL */
function appUrl(row: Row): string {
  return `#/Monitors/TraceData?appId=${row.AppId}`;
}
function clientUrl(row: Row): string {
  return `#/Monitors/TraceData?appId=${row.AppId}&clientId=${row.ClientId}&itemId=${row.ItemId}`;
}
function nameUrl(row: Row): string {
  return `#/Monitors/TraceData?appId=${row.AppId}&itemId=${row.ItemId}`;
}
function nodeUrl(row: Row): string {
  return `/Nodes/Node?Id=${row.NodeId}`;
}
/** 采样数据明细链接（MVC 总数/正常/异常列均跳转 SampleData，dataid 取 LinkId 优先） */
function sampleUrl(row: Row, success?: 0 | 1): string {
  const dataId = row.LinkId != null && row.LinkId > 0 ? row.LinkId : row.Id;
  let url = `#/Monitors/SampleData?dataid=${dataId}`;
  if (success != null) url += `&success=${success}`;
  return url;
}

/** 总计行（el-table show-summary 的 summary-method） */
function getSummaries(param: { columns: any[]; data: Row[] }): string[] {
  const { columns, data } = param;
  const sums: string[] = [];
  columns.forEach((col, idx) => {
    if (idx === 0) {
      sums[idx] = '总计';
      return;
    }
    const prop = col.property as string;
    if (prop === 'Cost') {
      // 平均耗时取整
      const vals = data.map((r) => Number(r.Cost ?? 0)).filter((n) => !Number.isNaN(n));
      sums[idx] = vals.length ? String(Math.round(vals.reduce((a, b) => a + b, 0) / vals.length)) : '';
      return;
    }
    if (SUM_COLUMNS.includes(prop)) {
      const totalVal = data.reduce((acc, r) => acc + Number((r as any)[prop] ?? 0), 0);
      sums[idx] = String(totalVal);
      return;
    }
    sums[idx] = '';
  });
  return sums;
}

function buildParams(): Record<string, unknown> {
  const p: Record<string, unknown> = {
    pageIndex: page.value - 1,
    pageSize: pageSize.value,
  };
  if (searchTag.value) p.searchTag = true;
  if (timeRange.value?.[0]) p.dtStart = timeRange.value[0];
  if (timeRange.value?.[1]) p.dtEnd = timeRange.value[1];
  if (appIdFilter.value != null && appIdFilter.value > 0) p.appId = appIdFilter.value;
  if (itemIdFilter.value != null && itemIdFilter.value > 0) p.itemId = itemIdFilter.value;
  return p;
}

async function fetchList() {
  loading.value = true;
  try {
    const res: any = await api.getList<Row>(buildParams());
    rows.value = res?.data ?? [];
    total.value = res?.page?.totalCount ?? res?.page?.total ?? rows.value.length;
  } catch (e) {
    console.error('[TraceData] GetList failed', e);
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
  searchTag.value = false;
  timeRange.value = null;
  appIdFilter.value = null;
  itemIdFilter.value = null;
  page.value = 1;
  fetchList();
}

function onPageChange(p: number) {
  page.value = p;
  fetchList();
}

/** 追踪：GET /Monitors/TraceData/Trace?id=<行Id> -> 成功提示 traceId；失败提示「无采样数据」 */
async function onTrace(row: Row) {
  if (row.Id == null) return;
  try {
    const res: any = await request({
      url: `/Monitors/TraceData/Trace`,
      method: 'get',
      params: { id: row.Id },
    });
    const traceId = res?.data?.traceId ?? res?.traceId;
    if (traceId != null) {
      ElNotification.success({ title: '追踪', message: `traceId: ${traceId}` });
    } else if (res && (res.code === 0 || res.code === 1 || res.success)) {
      ElNotification.success({ title: '追踪', message: res.message || '成功' });
    } else {
      ElNotification.warning({ title: '追踪', message: res?.message || '无采样数据' });
    }
  } catch (e: any) {
    const msg = e?.response?.data?.message || e?.message || '';
    if (msg.includes('采样') || msg.includes('找不到')) {
      ElNotification.info({ title: '追踪', message: '无采样数据' });
    } else {
      ElNotification.error({ title: '追踪失败', message: msg || '请求出错' });
    }
  }
}

onMounted(async () => {
  // 从 hash 读取 appId / clientId / itemId（来自列表链接跳转）
  const m = location.hash.match(/[?&]appId=(\d+)/);
  if (m) appIdFilter.value = Number(m[1]);
  const mi = location.hash.match(/[?&]itemId=(\d+)/);
  if (mi) itemIdFilter.value = Number(mi[1]);
  await fetchList();
});
</script>

<template>
  <div class="list-page trace-data-page">
    <div class="lp-header">
      <h2 class="lph-title">
        调用链监控
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

    <div class="lp-card">
      <!-- 搜索栏：searchTag 复选框 + 时间范围 + appId/itemId（加分项） -->
      <div class="lp-card-header td-search">
        <div class="td-search-grid">
          <div class="td-search-field">
            <label class="td-search-label">应用编号</label>
            <el-input
              v-model.number="appIdFilter"
              type="number"
              placeholder="AppId"
              class="td-search-input"
              clearable
            />
          </div>

          <div class="td-search-field">
            <label class="td-search-label">埋点编号</label>
            <el-input
              v-model.number="itemIdFilter"
              type="number"
              placeholder="ItemId"
              class="td-search-input"
              clearable
            />
          </div>

          <div class="td-search-field td-search-field--wide">
            <label class="td-search-label">时间</label>
            <el-date-picker
              v-model="timeRange"
              type="daterange"
              range-separator="~"
              start-placeholder="开始日期"
              end-placeholder="结束日期"
              value-format="YYYY-MM-DD"
              class="td-search-input td-search-datepicker"
            />
          </div>

          <div class="td-search-field td-search-field--check">
            <el-checkbox
              v-model="searchTag"
              label="仅错误(仅看错误标签)"
            />
          </div>

          <div class="td-search-actions">
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

      <div class="lp-card-body">
        <el-table
          v-loading="loading"
          :data="rows"
          class="td-table"
          stripe
          border
          show-summary
          :summary-method="getSummaries"
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
              <!-- 应用：链接 ?appId= -->
              <el-link
                v-if="c.name === 'AppName' && row.AppId != null"
                type="primary"
                :underline="false"
                :href="appUrl(row)"
                target="_self"
              >
                {{ cellText(row, c.name) }}
              </el-link>
              <!-- 实例：链接 appId+clientId+itemId -->
              <el-link
                v-else-if="c.name === 'ClientId' && row.AppId != null && row.ClientId != null"
                type="primary"
                :underline="false"
                :href="clientUrl(row)"
                target="_self"
              >
                {{ cellText(row, c.name) }}
              </el-link>
              <!-- 应用节点：链接 /Nodes/Node?Id= -->
              <el-link
                v-else-if="c.name === 'NodeName' && row.NodeId != null"
                type="primary"
                :underline="false"
                :href="nodeUrl(row)"
                target="_self"
              >
                {{ cellText(row, c.name) }}
              </el-link>
              <!-- 操作名：链接 appId+itemId -->
              <el-link
                v-else-if="c.name === 'Name' && row.AppId != null && row.ItemId != null"
                type="primary"
                :underline="false"
                :href="nameUrl(row)"
                target="_self"
              >
                {{ cellText(row, c.name) }}
              </el-link>
              <!-- 追踪：与 MVC 一致位于操作名之后、总数之前 -->
              <el-link
                v-else-if="c.name === '__trace__' && (row.Id ?? (row as any).id) != null"
                type="primary"
                :underline="false"
                @click="onTrace(row)"
              >
                追踪
              </el-link>
              <!-- 总数：链接 SampleData（与 MVC 一致） -->
              <el-link
                v-else-if="c.name === 'Total' && (row.Id ?? (row as any).id) != null"
                type="primary"
                :underline="false"
                :href="sampleUrl(row)"
                target="_self"
              >
                {{ cellText(row, c.name) }}
              </el-link>
              <!-- 正常：链接 SampleData&success=1 -->
              <el-link
                v-else-if="c.name === 'Samples' && (row.Id ?? (row as any).id) != null"
                type="primary"
                :underline="false"
                :href="sampleUrl(row, 1)"
                target="_self"
              >
                {{ cellText(row, c.name) }}
              </el-link>
              <!-- 异常：链接 SampleData&success=0 -->
              <el-link
                v-else-if="c.name === 'ErrorSamples' && (row.Id ?? (row as any).id) != null"
                type="primary"
                :underline="false"
                :href="sampleUrl(row, 0)"
                target="_self"
              >
                {{ cellText(row, c.name) }}
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
.trace-data-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;

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

  .lp-card {
    flex: 1;
    margin: var(--el-main-padding, 24px);
    padding: 16px;
    background: var(--el-bg-color-overlay);
    border: 1px solid var(--el-border-color-light);
    border-radius: var(--el-border-radius-base);
    box-shadow: var(--el-box-shadow-light);
    overflow: hidden;
    display: flex;
    flex-direction: column;
  }

  .lp-card-header {
    padding: 16px 20px;
    border-bottom: 1px solid var(--el-border-color-light);
  }

  .lp-card-body {
    flex: 1;
    min-height: 200px;
    overflow: auto;
  }

  .lp-card-footer {
    padding: 12px 20px;
    border-top: 1px solid var(--el-border-color-light);
  }
}

.td-search {
  .td-search-grid {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 12px 16px;
    align-items: end;
  }

  .td-search-field {
    display: flex;
    flex-direction: column;
    gap: 6px;
    min-width: 0;

    &--wide {
      grid-column: span 2;
    }

    &--check {
      justify-content: flex-end;
      padding-bottom: 6px;
    }
  }

  .td-search-label {
    font-size: 12px;
    font-weight: 500;
    color: var(--el-text-color-secondary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .td-search-input {
    width: 100%;

    :deep(.el-input__wrapper),
    :deep(.el-select__wrapper),
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
    :deep(.el-select__wrapper.is-focused),
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

  .td-search-datepicker {
    :deep(.el-range-editor) {
      width: 100%;
    }
  }

  .td-search-actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    padding-bottom: 0;
  }
}

.td-table {
  width: 100%;
}

@media (max-width: 900px) {
  .td-search .td-search-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .td-search .td-search-grid {
    grid-template-columns: 1fr;
  }

  .td-search .td-search-field--wide {
    grid-column: span 1;
  }
}
</style>
