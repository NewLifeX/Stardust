<script setup lang="ts">
/**
 * Redis/RedisNode/Index — Redis 节点列表（自定义迁移页）
 *
 * MVC 源 RedisNodeController.cs + _List_Search.cshtml 还原：
 *  - [Menu(50)] 可见页，路由由 MenuTree 注册，直接访问 #/Redis/RedisNode
 *  - 标准列：地址、端口、数据库、最大内存、已用内存、客户端、命令、连接数、状态等
 *    （WebHook、Alarm 系列、UpdateUser 系列、Remark 已在后端 ListFields 移除），由后端 GetPage 元数据提供
 *  - 自定义 url 跳转列（自建，AutoPage 不渲染）：
 *      ProjectName → /Platform/GalaxyProject?projectId={ProjectId}  (target=_frame)
 *      Monitor     → /Redis/RedisData?redisId={Id}
 *      Queue       → /Redis/RedisMessageQueue?redisId={Id}
 *      Log         → /Admin/Log?category=Redis节点&linkId={Id}       (target=_frame)
 *      Refresh     → /Redis/RedisNode/Refresh?Id={Id}                (DataAction="action")
 *  - 自定义 Action Refresh：GET /Redis/RedisNode/Refresh?id= → JsonRefresh → ElMessage + 刷新列表
 *      （清监控/刷新节点状态；[EntityAuthorize(Update)]，逐行或顶部批量刷新）
 *  - 搜索（_List_Search）：类别下拉(category, RedisNode.GetCategoryList()) + 日期范围(dtStart/dtEnd)
 *
 * 前端经 usePageApi + request，自定义 Action 写相对无前缀路径 /Redis/RedisNode/Refresh
 * （request 会自动拼 baseURL，不要写 /api/）。
 */
import { ref, reactive, computed, onMounted } from 'vue';
import { ElMessage } from 'element-plus';
import request from '@newlifex/cube-vue/core/utils/request';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';
import type { FieldMeta } from '@newlifex/cube-vue/core/types/field';

const api = usePageApi('Redis', 'RedisNode');

/** 后端下发的自定义 url 跳转字段（自建链接列，不进入自动表格列） */
const SKIP_FIELDS = new Set(['ProjectName', 'Monitor', 'Queue', 'Log', 'Refresh']);

interface BackendField {
  name: string;
  displayName: string;
  typeName: string;
  itemType?: string;
  primaryKey?: boolean;
  lovCode?: string;
  multiple?: boolean;
}
interface PageMeta {
  list: BackendField[];
  [k: string]: unknown;
}
interface Row {
  Id?: number;
  Name?: string;
  ProjectName?: string;
  ProjectId?: number;
  Category?: string;
  [k: string]: unknown;
}

const loading = ref(false);
const rows = ref<Row[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);
const listFields = ref<FieldMeta[]>([]);

// 搜索栏
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
    console.error('[Redis/RedisNode] GetPage failed', e);
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
    console.error('[Redis/RedisNode] fetchCategories failed', e);
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
    if (filters.category) params.category = filters.category;
    if (filters.timeRange?.[0]) params.dtStart = filters.timeRange[0];
    if (filters.timeRange?.[1]) params.dtEnd = filters.timeRange[1];

    const res: any = await api.getList<Row>(params);
    rows.value = res?.data ?? [];
    total.value = res?.page?.totalCount ?? res?.page?.total ?? rows.value.length;
  } catch (e) {
    console.error('[Redis/RedisNode] GetList failed', e);
    rows.value = [];
    total.value = 0;
  } finally {
    loading.value = false;
  }
}

function cellText(row: Row, key: string): string {
  const v = row[key];
  return v == null ? '' : String(v);
}

// 自建链接列
function projectUrl(row: Row): string {
  return `#/Platform/GalaxyProject?projectId=${row.ProjectId}`;
}
function monitorUrl(row: Row): string {
  return `#/Redis/RedisData?redisId=${row.Id}`;
}
function queueUrl(row: Row): string {
  return `#/Redis/RedisMessageQueue?redisId=${row.Id}`;
}
function auditLogUrl(row: Row): string {
  return `#/Admin/Log?category=${encodeURIComponent('Redis节点')}&linkId=${row.Id}`;
}

// 自定义 Action Refresh：GET /Redis/RedisNode/Refresh?id=<行Id>
const refreshing = ref(false);
async function onRefresh(row?: Row) {
  const ids: number[] = row && row.Id != null ? [row.Id] : rows.value.map((r) => r.Id as number).filter((id) => id != null);
  if (ids.length === 0) {
    ElMessage.info('没有可刷新的节点');
    return;
  }
  refreshing.value = true;
  let ok = 0;
  try {
    for (const id of ids) {
      try {
        const res: any = await request({
          url: `/Redis/RedisNode/Refresh`,
          method: 'get',
          params: { id },
        });
        if (res && (res.code === 0 || res.code === 200 || res.success)) {
          ok++;
        } else if (res?.message) {
          ElMessage.warning(res.message);
        }
      } catch (e: any) {
        ElMessage.error(e?.response?.data?.message || e?.message || `刷新节点[${id}]失败`);
      }
    }
    if (ok > 0) {
      ElMessage.success(`已刷新 ${ok} 个节点`);
      await fetchList();
    }
  } finally {
    refreshing.value = false;
  }
}

function handleSearch() {
  page.value = 1;
  fetchList();
}
function handleReset() {
  filters.category = '';
  filters.timeRange = null;
  page.value = 1;
  fetchList();
}

function onPageChange(p: number) {
  page.value = p;
  fetchList();
}

const hasActiveFilter = computed(() => !!filters.category || filters.timeRange != null);

onMounted(async () => {
  console.log('[Redis/RedisNode] custom view mounted');
  await fetchMeta();
  await fetchCategories();
  await fetchList();
});
</script>

<template>
  <div class="list-page redis-node-page">
    <div class="lp-header">
      <h2 class="lph-title">
        Redis 节点
      </h2>
      <div class="lp-toolbar">
        <el-button
          :loading="refreshing"
          @click="onRefresh()"
        >
          <el-icon><Refresh /></el-icon>刷新
        </el-button>
        <el-button
          :loading="loading"
          @click="fetchList"
        >
          重载
        </el-button>
      </div>
    </div>

    <div class="lp-card">
      <div class="lp-card-header rn-search">
        <div class="rn-search-grid">
          <div class="rn-search-field">
            <label class="rn-search-label">类别</label>
            <el-select
              v-model="filters.category"
              clearable
              placeholder="全部"
              class="rn-search-input"
            >
              <el-option
                v-for="opt in categoryOptions"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
              />
            </el-select>
          </div>

          <div class="rn-search-field rn-search-field--wide">
            <label class="rn-search-label">时间</label>
            <el-date-picker
              v-model="filters.timeRange"
              type="daterange"
              range-separator="~"
              start-placeholder="开始日期"
              end-placeholder="结束日期"
              value-format="YYYY-MM-DD"
              class="rn-search-input rn-search-datepicker"
            />
          </div>

          <div class="rn-search-actions">
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

          <!-- 自定义链接列：项目 -->
          <el-table-column
            label="项目"
            width="140"
          >
            <template #default="{ row }">
              <el-link
                v-if="row.ProjectName"
                type="primary"
                :href="projectUrl(row)"
                target="_self"
              >
                {{ row.ProjectName }}
              </el-link>
              <span v-else>-</span>
            </template>
          </el-table-column>

          <!-- 自定义链接列：监控 -->
          <el-table-column
            label="监控"
            width="80"
            align="center"
          >
            <template #default="{ row }">
              <el-link
                v-if="row.Id != null"
                type="primary"
                :underline="false"
                :href="monitorUrl(row)"
                target="_self"
              >
                监控
              </el-link>
              <span v-else>-</span>
            </template>
          </el-table-column>

          <!-- 自定义链接列：队列 -->
          <el-table-column
            label="队列"
            width="80"
            align="center"
          >
            <template #default="{ row }">
              <el-link
                v-if="row.Id != null"
                type="primary"
                :underline="false"
                :href="queueUrl(row)"
                target="_self"
              >
                队列
              </el-link>
              <span v-else>-</span>
            </template>
          </el-table-column>

          <!-- 自定义 Action 列：刷新（GET /Redis/RedisNode/Refresh?id=） -->
          <el-table-column
            label="刷新"
            width="80"
            align="center"
          >
            <template #default="{ row }">
              <el-link
                v-if="row.Id != null"
                type="success"
                :underline="false"
                @click="onRefresh(row)"
              >
                刷新
              </el-link>
              <span v-else>-</span>
            </template>
          </el-table-column>

          <!-- 自定义链接列：审计日志 -->
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
.redis-node-page {
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

.rn-search {
  .rn-search-grid {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    gap: 12px 16px;
    align-items: end;
  }

  .rn-search-field {
    display: flex;
    flex-direction: column;
    gap: 6px;
    min-width: 0;

    &--wide {
      grid-column: span 2;
    }
  }

  .rn-search-label {
    font-size: 12px;
    font-weight: 500;
    color: var(--el-text-color-secondary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    text-transform: uppercase;
    letter-spacing: 0.05em;
  }

  .rn-search-input {
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

  .rn-search-datepicker {
    :deep(.el-range-editor) {
      width: 100%;
    }
  }

  .rn-search-actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    padding-bottom: 0;
  }
}

.act-table {
  width: 100%;
}

.rn-ops {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;

  .rn-op {
    font-size: 13px;
  }
}

@media (max-width: 900px) {
  .rn-search .rn-search-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .rn-search .rn-search-grid {
    grid-template-columns: 1fr;
  }

  .rn-search .rn-search-field--wide {
    grid-column: span 1;
  }
}
</style>
