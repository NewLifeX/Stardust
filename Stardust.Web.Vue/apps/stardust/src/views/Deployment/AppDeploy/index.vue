<script setup lang="ts">
/**
 * Deployment/AppDeploy/Index — 应用部署集列表（自定义迁移页）
 *
 * 后端 AppDeployController 继承自 EntityController，标准 CRUD 由框架自动驱动；
 * 本页在其之上扩展自定义逻辑（对应 MVC 源 _List_Search / _List_Toolbar_Custom 与控制器 ListFields）：
 *  - 自定义跳转列（独立 el-table-column，不合并进操作列）：
 *      部署节点 NodeManage / 编译节点 BuildNode / 版本 Version / 部署历史 History
 *      （对应后端 AddListField 配置的 Url 跳转字段，AutoPage 只当纯文本渲染，必须自建）
 *  - 工具栏「同步应用」按钮：POST /Deployment/AppDeploy/SyncApp（自动建发布集逻辑由后端 Search() 处理）
 *  - 搜索栏（对应 MVC _List_Search）：类别 category（AppDeploy.GetCategoryList）/ 启用 enable / 日期范围 dtStart/dtEnd
 *
 * 列表列完整来自后端 GetPage 元数据的 list 字段（保留原始顺序，不剔除任何字段）：
 *  - 后端自定义 Url 跳转字段中本页需还原的 4 个（部署节点/编译节点/版本/部署历史）渲染为独立 el-table-column；
 *  - 其余字段（含 Name/ProjectName/AppName/AddVersion/Log 等带 Url 的）降级为纯文本列，但仍保留字段与位置；
 * 通过 usePageApi 对接，不手写字段/路由。
 */
import { ref, reactive, computed, onMounted } from 'vue';
import { ElMessage } from 'element-plus';
import request from '@newlifex/cube-vue/core/utils/request';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';
import type { FieldMeta } from '@newlifex/cube-vue/core/types/field';

const api = usePageApi('Deployment', 'AppDeploy');

/**
 * 后端下发的「独立链接列」（AddListField 配置 Url 的字段）：
 *   NodeManage=部署节点 / BuildNode=编译节点 / Version=版本 / History=部署历史
 * 这些字段在 AutoPage 只当纯文本渲染，必须自建为独立 el-table-column，
 * 并保留其在 WebApi list 中的原始顺序（对齐 MVC 渲染顺序）。
 * 其余带 Url 的字段（Name/ProjectName/AppName/AddVersion/Log）按现有设计降级为纯文本列，
 * 但仍须保留字段与位置，不可从列表中剔除。
 */
const LINK_FIELD_NAMES = new Set(['NodeManage', 'BuildNode', 'Version', 'History']);
const LINK_URL: Record<string, (row: Row) => string> = {
  NodeManage: (row) => `#/Deployment/AppDeployNode?deployId=${row.Id}`,
  BuildNode: (row) => `#/Deployment/AppBuildNode?deployId=${row.Id}`,
  Version: (row) => `#/Deployment/AppDeployVersion?deployId=${row.Id}`,
  History: (row) => `#/Deployment/AppDeployHistory?deployId=${row.Id}`,
};

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
  Category?: string;
  Enable?: boolean;
  [k: string]: unknown;
}

const loading = ref(false);
const rows = ref<Row[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);
const listFields = ref<FieldMeta[]>([]);
const syncing = ref(false);

/** 后端返回 camelCase 字段，转 PascalCase 以匹配 Row 接口与模板（修复单元格全空问题） */
function toPascalRow(r: Record<string, unknown>): Row {
  const out: Record<string, unknown> = {};
  for (const k of Object.keys(r)) {
    out[k.charAt(0).toUpperCase() + k.slice(1)] = r[k];
  }
  return out as Row;
}

// 搜索栏：类别 / 启用 / 日期范围（对齐 MVC _List_Search）
const categoryOptions = ref<{ value: string; label: string }[]>([]);
const filters = reactive({
  category: '',
  enable: '' as '' | 'true' | 'false',
  timeRange: null as [string, string] | null,
});

const hasActiveFilter = computed(
  () => !!filters.category || filters.enable !== '' || filters.timeRange != null,
);

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
      // 保留后端下发的完整字段与原始顺序（含独立链接列），由模板按字段类型区分渲染
      listFields.value = meta.list.map(toFieldMeta);
    }
  } catch (e) {
    console.error('[AppDeploy] GetPage failed', e);
  }
}

/** 类别下拉：从列表数据去重得到（对齐 MVC AppDeploy.GetCategoryList）。
 *  运行态 GetPage 为空时仅保留「全部」，控件仍渲染满足呈现级一致。 */
async function fetchCategories() {
  try {
    const res: any = await api.getList<Row>({ pageIndex: 0, pageSize: 1000, fields: 'Category' });
    const data = (res?.data ?? []).map(toPascalRow);
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
    console.error('[AppDeploy] fetchCategories failed', e);
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
    if (filters.enable !== '') params.enable = filters.enable === 'true';
    if (filters.timeRange?.[0]) params.dtStart = filters.timeRange[0];
    if (filters.timeRange?.[1]) params.dtEnd = filters.timeRange[1];

    const res: any = await api.getList<Row>(params);
    rows.value = (res?.data ?? []).map(toPascalRow);
    total.value = res?.page?.totalCount ?? res?.page?.total ?? rows.value.length;
  } catch (e) {
    console.error('[AppDeploy] GetList failed', e);
    rows.value = [];
    total.value = 0;
  } finally {
    loading.value = false;
  }
}

function cellText(row: Row, key: string): string {
  const v = row[key];
  if (v == null) return '';
  if (key === 'Enable') return v ? '是' : '否';
  return String(v);
}

async function onSyncApp() {
  syncing.value = true;
  try {
    const res: any = await request({
      url: `/Deployment/AppDeploy/SyncApp`,
      method: 'post',
    });
    if (res && res.code === 0) {
      ElMessage.success(res.message || '同步成功');
      await fetchList();
    } else {
      ElMessage.error(res?.message || '同步失败');
    }
  } catch (e: any) {
    ElMessage.error(e?.response?.data?.message || e?.message || '同步失败');
  } finally {
    syncing.value = false;
  }
}

function handleSearch() {
  page.value = 1;
  fetchList();
}
function handleReset() {
  filters.category = '';
  filters.enable = '';
  filters.timeRange = null;
  page.value = 1;
  fetchList();
}

function onPageChange(p: number) {
  page.value = p;
  fetchList();
}

onMounted(async () => {
  await fetchMeta();
  await fetchCategories();
  await fetchList();
});
</script>

<template>
  <div class="list-page app-deploy-page">
    <div class="lp-header">
      <h2 class="lph-title">
        应用部署集
      </h2>
      <div class="lp-toolbar">
        <el-button
          type="primary"
          :loading="syncing"
          @click="onSyncApp"
        >
          同步应用
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
      <!-- 搜索栏：类别 / 启用 / 日期范围（对齐 MVC _List_Search） -->
      <div class="lp-card-header ad-search">
        <div class="ad-search-grid">
          <div class="ad-search-field">
            <label class="ad-search-label">类别</label>
            <el-select
              v-model="filters.category"
              clearable
              placeholder="全部"
              class="ad-search-input"
            >
              <el-option
                v-for="opt in categoryOptions"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
              />
            </el-select>
          </div>

          <div class="ad-search-field">
            <label class="ad-search-label">启用</label>
            <el-select
              v-model="filters.enable"
              clearable
              placeholder="全部"
              class="ad-search-input"
            >
              <el-option
                label="全部"
                value=""
              />
              <el-option
                label="启用"
                value="true"
              />
              <el-option
                label="禁用"
                value="false"
              />
            </el-select>
          </div>

          <div class="ad-search-field ad-search-field--wide">
            <label class="ad-search-label">时间</label>
            <el-date-picker
              v-model="filters.timeRange"
              type="daterange"
              range-separator="~"
              start-placeholder="开始日期"
              end-placeholder="结束日期"
              value-format="YYYY-MM-DD"
              class="ad-search-input ad-search-datepicker"
            />
          </div>

          <div class="ad-search-actions">
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
          <!-- 按 WebApi list 原始顺序渲染：独立链接列（部署节点/编译节点/版本/部署历史）在 MVC 对应位置还原为独立 el-table-column，其余为纯文本列 -->
          <template
            v-for="f in listFields"
            :key="f.name"
          >
            <el-table-column
              v-if="LINK_FIELD_NAMES.has(f.name)"
              :label="f.displayName || f.name"
              width="100"
              align="center"
            >
              <template #default="{ row }">
                <el-link
                  v-if="row.Id != null"
                  type="primary"
                  :underline="false"
                  :href="LINK_URL[f.name](row)"
                  target="_self"
                >
                  {{ f.displayName || f.name }}
                </el-link>
                <span v-else>-</span>
              </template>
            </el-table-column>
            <el-table-column
              v-else
              :prop="f.name"
              :label="f.displayName || f.name"
              min-width="90"
              show-overflow-tooltip
            >
              <template #default="{ row }">
                <span>{{ cellText(row, f.name) }}</span>
              </template>
            </el-table-column>
          </template>
        </el-table>
      </div>

      <div class="lp-pager">
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
.app-deploy-page {
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

  .act-table {
    width: 100%;
  }

  .lp-pager {
    margin-top: 16px;
    padding: 12px 20px;
    border-top: 1px solid var(--el-border-color-light);
    display: flex;
    justify-content: flex-end;
  }
}

.ad-search {
  .ad-search-grid {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 12px 16px;
    align-items: end;
  }

  .ad-search-field {
    display: flex;
    flex-direction: column;
    gap: 6px;
    min-width: 0;

    &--wide {
      grid-column: span 1;
    }
  }

  .ad-search-label {
    font-size: 12px;
    font-weight: 500;
    color: var(--el-text-color-secondary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .ad-search-input {
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

  .ad-search-datepicker {
    :deep(.el-range-editor) {
      width: 100%;
    }
  }

  .ad-search-actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    padding-bottom: 0;
  }
}

@media (max-width: 900px) {
  .ad-search .ad-search-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .ad-search .ad-search-grid {
    grid-template-columns: 1fr;
  }
}
</style>
