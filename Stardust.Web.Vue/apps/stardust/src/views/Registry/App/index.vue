<script setup lang="ts">
/**
 * Registry/App/Index — 应用列表（自定义迁移页）
 *
 * 后端 AppController 继承自 EntityController<App>，[Menu(99)] 可见页，路由由 MenuTree 注册，
 * 直接访问 #/Registry/App。本页在其之上扩展自定义逻辑（对应 MVC 源 AppController.cs /
 * _List_Search.cshtml / _List_Toolbar_Batch.cshtml）：
 *  - 自定义 url 跳转列：Name→详情、ProjectName→项目、AppLog→应用日志、Log→审计日志（后端 url 字段，自建）
 *  - 自定义搜索栏：类别下拉(category，来自 App.FindAllCategory()) + 日期范围(dtStart/dtEnd)
 *  - 自定义 Action：
 *      AppSearch  GET /api/Registry/App/AppSearch          （应用搜索建议，el-autocomplete 远程联想）
 *      SetAlarm   GET /api/Registry/App/SetAlarm?keys=...&enable=  （批量启用/禁用下线告警）
 *
 * 标准列表列来自后端 GetPage 元数据的 list 字段（过滤掉后端自定义 url 跳转字段），
 * 通过 usePageApi 对接，分页 pageIndex: page-1，总条数 res.page.totalCount。
 */
import { ref, reactive, computed, onMounted, watch } from 'vue';
import { useRoute } from 'vue-router';
import { ElMessage } from 'element-plus';
import request from '@newlifex/cube-vue/core/utils/request';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';
import type { FieldMeta } from '@newlifex/cube-vue/core/types/field';

const api = usePageApi('Registry', 'App');
const route = useRoute();
const projectId = ref<number | undefined>(undefined);

/** 后端下发的自定义 url 跳转字段（独立链接列，按后端顺序内联渲染，不合并） */

interface BackendField {
  name: string;
  displayName: string;
  typeName: string;
  itemType?: string;
  primaryKey?: boolean;
  lovCode?: string;
  multiple?: boolean;
  url?: string | null;
  target?: string | null;
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
  DisplayName?: string;
  Category?: string;
  [k: string]: unknown;
}

const loading = ref(false);
const rows = ref<Row[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);
const listFields = ref<FieldMeta[]>([]);

const selectedIds = ref<number[]>([]);

// 搜索栏
const categoryOptions = ref<{ value: string; label: string }[]>([]);
const filters = reactive({
  category: '',
  timeRange: null as [string, string] | null,
});

// 应用搜索联想（AppSearch）
const searchKey = ref('');
const searchSuggestions = ref<{ value: string; id: number }[]>([]);

function toFieldMeta(f: BackendField): FieldMeta {
  return {
    name: f.name,
    displayName: f.displayName,
    typeName: f.typeName,
    itemType: f.itemType,
    primaryKey: f.primaryKey,
    lovCode: f.lovCode,
    multiple: f.multiple,
    url: f.url ?? undefined,
    target: f.target ?? undefined,
  };
}

async function fetchMeta() {
  try {
    const res: any = await api.getPage();
    const meta = (res?.data ?? res) as PageMeta;
    if (meta?.list?.length) {
      // 保留后端完整顺序（含独立链接列），由模板按字段逐列渲染对齐 MVC 呈现
      listFields.value = meta.list.map(toFieldMeta);
    }
  } catch (e) {
    console.error('[Registry/App] GetPage failed', e);
  }
}

async function fetchCategories() {
  try {
    // 从已有记录去重提取类别，与 MVC App.FindAllCategory() 语义一致
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
    console.error('[Registry/App] fetchCategories failed', e);
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
    // 项目维度隔离：从 GalaxyProject 项目页进入时携带 projectId，列表按项目过滤
    if (projectId.value != null) params.projectId = projectId.value;

    const res: any = await api.getList<Row>(params);
    rows.value = res?.data ?? [];
    total.value = res?.page?.totalCount ?? res?.page?.total ?? rows.value.length;
  } catch (e) {
    console.error('[Registry/App] GetList failed', e);
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
function detailUrl(row: Row): string {
  return `#/Registry/App/Detail?id=${row.Id}`;
}
function projectUrl(row: Row): string {
  return `#/Platform/GalaxyProject?projectId=${row.ProjectId}`;
}
function appLogUrl(row: Row): string {
  return `#/Registry/AppLog?appId=${row.Id}`;
}
function auditLogUrl(row: Row): string {
  return `#/Admin/Log?category=${encodeURIComponent('应用系统')}&linkId=${row.Id}`;
}

// 批量选择
function onSelectionChange(selection: Row[]) {
  selectedIds.value = selection.map((r) => r.Id as number).filter((id) => id != null);
}

// 批量告警（SetAlarm）
const alarming = ref(false);
async function onSetAlarm(enable: boolean) {
  if (selectedIds.value.length === 0) {
    ElMessage.warning('请先勾选应用');
    return;
  }
  alarming.value = true;
  try {
    const res: any = await request({
      url: `/api/Registry/App/SetAlarm?enable=${enable ? 'true' : 'false'}`,
      method: 'get',
      params: { keys: selectedIds.value.join(',') },
    });
    if (res && res.code === 0) {
      ElMessage.success(res.message || (enable ? '已启用下线告警' : '已禁用下线告警'));
      await fetchList();
    } else {
      ElMessage.error(res?.message || '操作失败');
    }
  } catch (e: any) {
    ElMessage.error(e?.response?.data?.message || e?.message || '操作失败');
  } finally {
    alarming.value = false;
  }
}

// 应用搜索联想（AppSearch）
async function querySearchAsync(query: string, cb: (items: { value: string; id: number }[]) => void) {
  if (!query) {
    cb([]);
    return;
  }
  try {
    const res: any = await request({
      url: '/api/Registry/App/AppSearch',
      method: 'get',
      params: { key: query },
    });
    const data = res?.data ?? [];
    const items = data.map((e: any) => ({
      value: e.DisplayName ? `${e.Name}（${e.DisplayName}）` : e.Name,
      id: e.Id,
    }));
    searchSuggestions.value = items;
    cb(items);
  } catch (e) {
    console.error('[Registry/App] AppSearch failed', e);
    cb([]);
  }
}

function onSearchSelect(item: { value: string; id: number }) {
  if (item?.id != null) {
    window.open(`#/Registry/App/Detail?id=${item.id}`, '_blank');
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

const hasActiveFilter = computed(
  () => !!filters.category || filters.timeRange != null || !!searchKey.value,
);

onMounted(async () => {
  console.log('[Registry/App] custom view mounted');
  projectId.value = route.query.projectId ? Number(route.query.projectId) : undefined;
  await fetchMeta();
  await fetchCategories();
  await fetchList();
});
// 路由 projectId 变化时（在项目页点击不同项目跳转）重新过滤
watch(
  () => route.query.projectId,
  (val) => {
    projectId.value = val ? Number(val) : undefined;
    fetchList();
  },
);
</script>

<template>
  <div class="list-page registry-app-page">
    <div class="lp-header">
      <h2 class="lph-title">
        应用管理
      </h2>
      <div class="lp-toolbar">
        <el-autocomplete
          v-model="searchKey"
          :fetch-suggestions="querySearchAsync"
          placeholder="搜索应用名称"
          clearable
          class="app-search-box"
          @select="onSearchSelect"
        />
        <el-button
          type="success"
          :loading="alarming"
          @click="onSetAlarm(true)"
        >
          启用告警
        </el-button>
        <el-button
          type="danger"
          :loading="alarming"
          @click="onSetAlarm(false)"
        >
          禁用告警
        </el-button>
        <el-button
          :loading="loading"
          @click="fetchList"
        >
          刷新
        </el-button>
      </div>
    </div>

    <div class="lp-card">
      <div class="lp-card-header registry-app-search">
        <div class="ra-search-grid">
          <div class="ra-search-field">
            <label class="ra-search-label">类别</label>
            <el-select
              v-model="filters.category"
              clearable
              placeholder="全部"
              class="ra-search-input"
            >
              <el-option
                v-for="opt in categoryOptions"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
              />
            </el-select>
          </div>

          <div class="ra-search-field ra-search-field--wide">
            <label class="ra-search-label">时间</label>
            <el-date-picker
              v-model="filters.timeRange"
              type="daterange"
              range-separator="~"
              start-placeholder="开始日期"
              end-placeholder="结束日期"
              value-format="YYYY-MM-DD"
              class="ra-search-input ra-search-datepicker"
            />
          </div>

          <div class="ra-search-actions">
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
          @selection-change="onSelectionChange"
        >
          <el-table-column
            type="selection"
            width="48"
          />

          <el-table-column
            v-for="f in listFields"
            :key="f.name"
            :prop="f.name"
            :label="f.displayName || f.name"
            :fixed="f.name === 'Name' ? 'left' : false"
            show-overflow-tooltip
          >
            <template #default="{ row }">
              <!-- 独立链接列：按后端字段名渲染，不合并 -->
              <template v-if="f.name === 'Name'">
                <el-link
                  type="primary"
                  :href="detailUrl(row)"
                  target="_blank"
                >
                  {{ row.Name }}
                </el-link>
              </template>
              <template v-else-if="f.name === 'ProjectName'">
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
              <template v-else-if="f.name === 'AppLog'">
                <el-link
                  type="primary"
                  :href="appLogUrl(row)"
                  target="_self"
                >
                  应用日志
                </el-link>
              </template>
              <template v-else-if="f.name === 'Log'">
                <el-link
                  type="info"
                  :href="auditLogUrl(row)"
                  target="_self"
                >
                  审计日志
                </el-link>
              </template>
              <template v-else>
                <span>{{ cellText(row, f.name) }}</span>
              </template>
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
.registry-app-page {
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

    .app-search-box {
      width: 220px;
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

.registry-app-search {
  .ra-search-grid {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    gap: 12px 16px;
    align-items: end;
  }

  .ra-search-field {
    display: flex;
    flex-direction: column;
    gap: 6px;
    min-width: 0;

    &--wide {
      grid-column: span 2;
    }
  }

  .ra-search-label {
    font-size: 12px;
    font-weight: 500;
    color: var(--el-text-color-secondary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    text-transform: uppercase;
    letter-spacing: 0.05em;
  }

  .ra-search-input {
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

  .ra-search-datepicker {
    :deep(.el-range-editor) {
      width: 100%;
    }
  }

  .ra-search-actions {
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
  .registry-app-search .ra-search-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .registry-app-search .ra-search-grid {
    grid-template-columns: 1fr;
  }

  .registry-app-search .ra-search-field--wide {
    grid-column: span 1;
  }
}
</style>
