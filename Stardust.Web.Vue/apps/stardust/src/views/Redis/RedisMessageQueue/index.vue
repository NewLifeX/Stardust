<script setup lang="ts">
/**
 * Redis/RedisMessageQueue/Index — Redis 消息队列（自定义迁移页）
 *
 * MVC 源 RedisMessageQueueController.cs + _List_Search.cshtml 还原：
 *  - [Menu(30,false)] 隐藏页，displayName "Redis消息队列"，路由由 MenuTree 注册，可访问 #/Redis/RedisMessageQueue
 *    （RedisNode 监控列的 队列 跳转即指向 #/Redis/RedisMessageQueue?redisId={Id}）
 *  - 标准列表列（来自后端 GetPage list 元数据，顺序即对 MVC ListFields）：
 *      编号 / Redis节点(RedisName) / 名称 / 分类 / 库 / 主题 / 类型 / 消费组 /
 *      消费者 / 总消费 / 消息数 / 最大积压 / 启用 / 最早消费 / 最后活跃 /
 *      追踪(TraceId) / 审计日志(Log) / 更新时间
 *    （WebHook、ConsumerInfo、创建系列、更新系列除 UpdateTime 已在后端 ListFields 移除）
 *    RedisName 为后端 mapField=RedisId 的扩展列（Redis节点名称）。
 *  - 独立链接列（自建，不进入自动表格列）：
 *      TraceId → 追踪：有值时显示，跳转星尘链路（降级 #/Monitors/Trace?id={TraceId}，target=_blank）
 *      Log     → 审计日志：/Admin/Log?category=Redis消息队列&linkId={Id}  (target=_frame)
 *  - 搜索（_List_Search）：节点编号(redisId) + 类别下拉(category, RedisMessageQueue.GetCategoryList()) + 日期范围(dtStart/dtEnd)
 *    后端 Search 支持 redisId/category/dtStart/dtEnd；类别下拉前端从列表聚合（同 RedisNode 做法）。
 *
 * 前端经 usePageApi + request，自定义搜索/链接均写相对无前缀路径（request 自动拼 baseURL，不要写 /api）。
 */
import { ref, reactive, computed, onMounted } from 'vue';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';
import type { FieldMeta } from '@newlifex/cube-vue/core/types/field';

const api = usePageApi('Redis', 'RedisMessageQueue');

/** 后端下发的自定义 url 跳转字段（作为独立链接列，不进入自动表格列） */
const SKIP_FIELDS = new Set(['TraceId', 'Log']);

interface BackendField {
  name: string;
  displayName: string;
  typeName: string;
  itemType?: string;
  primaryKey?: boolean;
  mapField?: string;
  lovCode?: string;
  multiple?: boolean;
}
interface PageMeta {
  list: BackendField[];
  [k: string]: unknown;
}
interface Row {
  Id?: number;
  RedisName?: string;
  Name?: string;
  Category?: string;
  TraceId?: string;
  [k: string]: unknown;
}

const loading = ref(false);
const rows = ref<Row[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);
const listFields = ref<FieldMeta[]>([]);

// 搜索栏：节点编号 + 类别 + 日期范围
const redisIdFilter = ref<number | null>(null);
const categoryOptions = ref<{ value: string; label: string }[]>([]);
const filters = reactive({
  category: '',
  timeRange: null as [string, string] | null,
});

function toFieldMeta(f: BackendField): FieldMeta {
  return {
    name: f.name,
    displayName: f.displayName,
    typeName: f.typeName,
    itemType: f.itemType,
    primaryKey: f.primaryKey,
    lovCode: f.lovCode,
    multiple: f.multiple,
  };
}

async function fetchMeta() {
  try {
    const res: any = await api.getPage();
    const meta = (res?.data ?? res) as PageMeta;
    if (meta?.list?.length) {
      listFields.value = meta.list
        .filter((f) => !SKIP_FIELDS.has(f.name))
        .map(toFieldMeta);
    }
  } catch (e) {
    console.error('[Redis/RedisMessageQueue] GetPage failed', e);
  }
}

async function fetchCategories() {
  try {
    const res: any = await api.getList<Row>({ pageIndex: 0, pageSize: 1000, fields: 'Category' });
    const data = res?.data ?? [];
    const set = new Set<string>();
    data.forEach((r: Row) => {
      const c = r.Category;
      if (c) set.add(c);
    });
    const list = Array.from(set)
      .sort((a, b) => a.localeCompare(b))
      .map((c) => ({ value: c, label: c }));
    categoryOptions.value = [{ value: '', label: '全部' }, ...list];
  } catch (e) {
    console.error('[Redis/RedisMessageQueue] fetchCategories failed', e);
    categoryOptions.value = [{ value: '', label: '全部' }];
  }
}

async function fetchList() {
  loading.value = true;
  try {
    const params: Record<string, unknown> = {
      pageIndex: page.value - 1,
      pageSize: pageSize.value,
    };
    if (redisIdFilter.value != null && redisIdFilter.value > 0) params.redisId = redisIdFilter.value;
    if (filters.category) params.category = filters.category;
    if (filters.timeRange?.[0]) params.dtStart = filters.timeRange[0];
    if (filters.timeRange?.[1]) params.dtEnd = filters.timeRange[1];

    const res: any = await api.getList<Row>(params);
    rows.value = res?.data ?? [];
    total.value = res?.page?.totalCount ?? res?.page?.total ?? rows.value.length;
  } catch (e) {
    console.error('[Redis/RedisMessageQueue] GetList failed', e);
    rows.value = [];
    total.value = 0;
  } finally {
    loading.value = false;
  }
}

function formatTime(v: unknown): string {
  if (v == null) return '';
  return String(v).replace('T', ' ').slice(0, 19);
}

function cellText(row: Row, key: string): string {
  const v = row[key];
  if (v == null) return '';
  if (key === 'UpdateTime' || key === 'FirstConsumer' || key === 'LastActive' || key === 'CreateTime') {
    return formatTime(v);
  }
  if (typeof v === 'boolean') return v ? '是' : '否';
  return String(v);
}

// 独立链接列：追踪（降级到监控中心链路查询页）
function traceUrl(row: Row): string {
  return `#/Monitors/Trace?id=${row.TraceId}`;
}
// 独立链接列：审计日志（_frame 内嵌）
function auditLogUrl(row: Row): string {
  return `#/Admin/Log?category=${encodeURIComponent('Redis消息队列')}&linkId=${row.Id}`;
}

function handleSearch() {
  page.value = 1;
  fetchList();
}
function handleReset() {
  redisIdFilter.value = null;
  filters.category = '';
  filters.timeRange = null;
  page.value = 1;
  fetchList();
}

function onPageChange(p: number) {
  page.value = p;
  fetchList();
}

const hasActiveFilter = computed(
  () => (redisIdFilter.value != null && redisIdFilter.value > 0) || !!filters.category || filters.timeRange != null,
);

onMounted(async () => {
  console.log('[Redis/RedisMessageQueue] custom view mounted');
  // 从 hash 读取 redisId（来自 RedisNode 监控列跳转：#/Redis/RedisMessageQueue?redisId={Id}）
  const m = location.hash.match(/[?&]redisId=(\d+)/);
  if (m) redisIdFilter.value = Number(m[1]);
  await fetchMeta();
  await fetchCategories();
  await fetchList();
});
</script>

<template>
  <div class="list-page rmq-page">
    <div class="lp-header">
      <h2 class="lph-title">
        Redis 消息队列
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
      <div class="lp-card-header rmq-search">
        <div class="rmq-search-grid">
          <div class="rmq-search-field">
            <label class="rmq-search-label">节点编号</label>
            <el-input
              v-model.number="redisIdFilter"
              type="number"
              placeholder="RedisId"
              class="rmq-search-input"
              clearable
            />
          </div>

          <div class="rmq-search-field">
            <label class="rmq-search-label">类别</label>
            <el-select
              v-model="filters.category"
              clearable
              placeholder="全部"
              class="rmq-search-input"
            >
              <el-option
                v-for="opt in categoryOptions"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
              />
            </el-select>
          </div>

          <div class="rmq-search-field rmq-search-field--wide">
            <label class="rmq-search-label">时间</label>
            <el-date-picker
              v-model="filters.timeRange"
              type="daterange"
              range-separator="~"
              start-placeholder="开始日期"
              end-placeholder="结束日期"
              value-format="YYYY-MM-DD"
              class="rmq-search-input rmq-search-datepicker"
            />
          </div>

          <div class="rmq-search-actions">
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
          class="act-table"
          stripe
          border
          empty-text="暂无数据"
        >
          <el-table-column
            v-for="f in listFields"
            :key="f.name"
            :prop="f.name"
            :label="f.displayName || f.name"
            min-width="90"
            show-overflow-tooltip
          >
            <template #default="{ row }">
              <span>{{ cellText(row, f.name) }}</span>
            </template>
          </el-table-column>

          <!-- 独立链接列：追踪（有值时显示，跳转星尘链路） -->
          <el-table-column
            label="追踪"
            width="120"
            align="left"
          >
            <template #default="{ row }">
              <el-link
                v-if="row.TraceId"
                type="primary"
                :underline="false"
                :href="traceUrl(row)"
                target="_blank"
              >
                {{ row.TraceId }}
              </el-link>
              <span v-else>-</span>
            </template>
          </el-table-column>

          <!-- 独立链接列：审计日志（_frame 内嵌） -->
          <el-table-column
            label="审计日志"
            width="90"
            align="center"
          >
            <template #default="{ row }">
              <el-link
                v-if="row.Id != null"
                type="info"
                :underline="false"
                :href="auditLogUrl(row)"
                target="_self"
              >
                审计日志
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
.rmq-page {
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
      align-items: center;
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

.rmq-search {
  .rmq-search-grid {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    gap: 12px 16px;
    align-items: end;
  }

  .rmq-search-field {
    display: flex;
    flex-direction: column;
    gap: 6px;
    min-width: 0;

    &--wide {
      grid-column: span 2;
    }
  }

  .rmq-search-label {
    font-size: 12px;
    font-weight: 500;
    color: var(--el-text-color-secondary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .rmq-search-input {
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

    :deep(.el-input__inner),
    :deep(.el-select__selected-item) {
      font-size: 13px;
      color: var(--el-text-color-primary);
    }
  }

  .rmq-search-datepicker {
    :deep(.el-range-editor) {
      width: 100%;
    }
  }

  .rmq-search-actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    padding-bottom: 0;
  }
}

.act-table {
  width: 100%;
}

@media (max-width: 900px) {
  .rmq-search .rmq-search-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .rmq-search .rmq-search-grid {
    grid-template-columns: 1fr;
  }

  .rmq-search .rmq-search-field--wide {
    grid-column: span 1;
  }
}
</style>
