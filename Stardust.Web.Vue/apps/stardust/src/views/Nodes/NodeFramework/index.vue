<script setup lang="ts">
/**
 * Nodes/NodeFramework/Index — 节点框架管理（命令面板，自定义迁移页）
 *
 * 后端 NodeFrameworkController 继承自 NodesEntityController<Node>，[Menu(70)] 可见页，
 * 路由由 MenuTree 注册，直接访问 #/Nodes/NodeFramework。本页在其之上扩展自定义逻辑
 * （对应 MVC 源 NodeFrameworkController.cs / _List_Search.cshtml / _List_Toolbar_Batch.cshtml）：
 *  - 列表列：后端 GetPage 元数据（Node 实体字段），自建 el-table 还原
 *      （NodeFramework 控制器无自定义 Url 跳转字段，无需自建链接列，直接渲染元数据列）
 *  - 自定义 Action（命令面板，对应 _List_Toolbar_Batch 的 data-fields=keys,ver,kind）：
 *      InstallFramework   GET /Nodes/NodeFramework/InstallFramework?ver=&kind=&keys=   （安装框架）
 *      UninstallFramework GET /Nodes/NodeFramework/UninstallFramework?ver=&keys=       （卸载框架）
 *  - 自定义搜索栏（_List_Search）：类别(category) / 产品(product) / 版本(version) / 系统(osKind) 下拉 + 日期范围(dtStart/dtEnd)
 *
 * 标准列表列来自后端 GetPage 元数据的 list 字段，通过 usePageApi 对接，
 * 分页 pageIndex: page-1，总条数 res.page.totalCount。
 * 自定义 Action 写相对无前缀路径 /Nodes/NodeFramework/<Action>（request 自动拼 baseURL，勿写 /api/）。
 */
import { ref, reactive, computed, onMounted } from 'vue';
import { ElMessage } from 'element-plus';
import request from '@newlifex/cube-vue/core/utils/request';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';
import type { FieldMeta } from '@newlifex/cube-vue/core/types/field';

const api = usePageApi('Nodes', 'NodeFramework');

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
  [k: string]: unknown;
}

const loading = ref(false);
const rows = ref<Row[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);
const listFields = ref<FieldMeta[]>([]);

const selection = ref<Row[]>([]);
const selectedIds = ref<number[]>([]);

// 搜索栏（_List_Search）
const categoryOptions = ref<{ value: string; label: string }[]>([]);
const productOptions = ref<{ value: string; label: string }[]>([]);
const versionOptions = ref<{ value: string; label: string }[]>([]);
const osKindOptions = ref<{ value: string; label: string }[]>([]);
const filters = reactive({
  category: '',
  product: '',
  version: '',
  osKind: '',
  timeRange: null as [string, string] | null,
});

// 批量工具栏（_List_Toolbar_Batch）：版本 / 类型下拉
const pkgVersionOptions = ref<{ value: string; label: string }[]>([]);
const pkgKindOptions = ref<{ value: string; label: string }[]>([]);
const batch = reactive({
  ver: '',
  kind: '',
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
      listFields.value = meta.list.map(toFieldMeta);
    }
  } catch (e) {
    console.error('[Nodes/NodeFramework] GetPage failed', e);
  }
}

// 从已有记录去重提取下拉选项（与 MVC Node.FindAllCategory/Product/Version/OSKind 语义一致）
async function fetchSelectOptions() {
  try {
    const res: any = await api.getList<Row>({
      pageIndex: 0,
      pageSize: 1000,
      fields: 'Category,Product,Version,OSKind',
    });
    const data = res?.data ?? [];
    const pick = (key: string) => {
      const set = new Set<string>();
      data.forEach((r: Row) => {
        const v = r[key];
        if (v != null && v !== '') set.add(String(v));
      });
      return Array.from(set)
        .sort((a, b) => a.localeCompare(b))
        .map((c) => ({ value: c, label: c }));
    };
    categoryOptions.value = [{ value: '', label: '全部' }, ...pick('Category')];
    productOptions.value = [{ value: '', label: '全部' }, ...pick('Product')];
    versionOptions.value = [{ value: '', label: '全部' }, ...pick('Version')];
    osKindOptions.value = [{ value: '', label: '全部' }, ...pick('OSKind')];
  } catch (e) {
    console.error('[Nodes/NodeFramework] fetchSelectOptions failed', e);
    categoryOptions.value = [{ value: '', label: '全部' }];
    productOptions.value = [{ value: '', label: '全部' }];
    versionOptions.value = [{ value: '', label: '全部' }];
    osKindOptions.value = [{ value: '', label: '全部' }];
  }
}

// 安装包版本/类型下拉（DotNetPackage.GetVersionList / GetKindList）
async function fetchPackageOptions() {
  try {
    const [verRes, kindRes]: any = await Promise.all([
      request({ url: '/Nodes/NodeFramework/GetVersionList', method: 'get' }),
      request({ url: '/Nodes/NodeFramework/GetKindList', method: 'get' }),
    ]);
    const verData = verRes?.data ?? verRes;
    const kindData = kindRes?.data ?? kindRes;
    const verDict: Record<string, string> =
      verData && typeof verData === 'object' && !Array.isArray(verData) ? verData : {};
    const kindDict: Record<string, string> =
      kindData && typeof kindData === 'object' && !Array.isArray(kindData) ? kindData : {};

    pkgVersionOptions.value = [
      { value: '', label: '请选择' },
      ...Object.keys(verDict).map((v: string) => ({ value: v, label: verDict[v] || v })),
    ];
    pkgKindOptions.value = [
      { value: '', label: '请选择' },
      ...Object.keys(kindDict).map((v: string) => ({ value: v, label: kindDict[v] || v })),
    ];
  } catch (e) {
    console.error('[Nodes/NodeFramework] fetchPackageOptions failed', e);
    pkgVersionOptions.value = [{ value: '', label: '请选择' }];
    pkgKindOptions.value = [{ value: '', label: '请选择' }];
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
    if (filters.product) params.product = filters.product;
    if (filters.version) params.version = filters.version;
    if (filters.osKind) params.osKind = filters.osKind;
    if (filters.timeRange?.[0]) params.dtStart = filters.timeRange[0];
    if (filters.timeRange?.[1]) params.dtEnd = filters.timeRange[1];

    const res: any = await api.getList<Row>(params);
    rows.value = res?.data ?? [];
    total.value = res?.page?.totalCount ?? res?.page?.total ?? rows.value.length;
  } catch (e) {
    console.error('[Nodes/NodeFramework] GetList failed', e);
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

// 批量选择
function onSelectionChange(sel: Row[]) {
  selection.value = sel;
  selectedIds.value = sel.map((r) => r.Id as number).filter((id) => id != null);
}

// 批量命令（相对无前缀路径，request 自动拼 baseURL）
async function runAction(url: string, extra: Record<string, unknown> = {}, successMsg = '操作已下发') {
  if (selectedIds.value.length === 0) {
    ElMessage.warning('请先勾选节点');
    return;
  }
  try {
    const res: any = await request({
      url,
      method: 'get',
      params: { keys: selectedIds.value.join(','), ...extra },
    });
    if (res && (res.code === 0 || res.code === 200 || res.success)) {
      ElMessage.success(res.message || successMsg);
      await fetchList();
    } else {
      ElMessage.error(res?.message || '操作失败');
    }
  } catch (e: any) {
    ElMessage.error(e?.response?.data?.message || e?.message || '操作失败');
  }
}

const installing = ref(false);
const uninstalling = ref(false);

async function onInstallFramework() {
  if (!batch.ver) {
    ElMessage.warning('请选择框架版本');
    return;
  }
  installing.value = true;
  try {
    await runAction(
      '/Nodes/NodeFramework/InstallFramework',
      { ver: batch.ver, kind: batch.kind },
      '已下发安装框架',
    );
  } finally {
    installing.value = false;
  }
}
async function onUninstallFramework() {
  if (!batch.ver) {
    ElMessage.warning('请选择框架版本');
    return;
  }
  uninstalling.value = true;
  try {
    await runAction(
      '/Nodes/NodeFramework/UninstallFramework',
      { ver: batch.ver },
      '已下发卸载框架',
    );
  } finally {
    uninstalling.value = false;
  }
}

function handleSearch() {
  page.value = 1;
  fetchList();
}
function handleReset() {
  filters.category = '';
  filters.product = '';
  filters.version = '';
  filters.osKind = '';
  filters.timeRange = null;
  page.value = 1;
  fetchList();
}

function onPageChange(p: number) {
  page.value = p;
  fetchList();
}

const hasActiveFilter = computed(
  () =>
    !!filters.category ||
    !!filters.product ||
    !!filters.version ||
    !!filters.osKind ||
    filters.timeRange != null,
);

onMounted(async () => {
  console.log('[Nodes/NodeFramework] custom view mounted');
  await fetchMeta();
  await fetchSelectOptions();
  await fetchPackageOptions();
  await fetchList();
});
</script>

<template>
  <div class="list-page node-framework-page">
    <div class="lp-header">
      <h2 class="lph-title">
        节点框架
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
      <!-- 搜索栏：类别 / 产品 / 版本 / 系统 + 日期范围 -->
      <div class="lp-card-header nf-search">
        <div class="nf-search-grid">
          <div class="nf-search-field">
            <label class="nf-search-label">类别</label>
            <el-select
              v-model="filters.category"
              clearable
              placeholder="全部"
              class="nf-search-input"
            >
              <el-option
                v-for="opt in categoryOptions"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
              />
            </el-select>
          </div>

          <div class="nf-search-field">
            <label class="nf-search-label">产品</label>
            <el-select
              v-model="filters.product"
              clearable
              placeholder="全部"
              class="nf-search-input"
            >
              <el-option
                v-for="opt in productOptions"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
              />
            </el-select>
          </div>

          <div class="nf-search-field">
            <label class="nf-search-label">版本</label>
            <el-select
              v-model="filters.version"
              clearable
              placeholder="全部"
              class="nf-search-input"
            >
              <el-option
                v-for="opt in versionOptions"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
              />
            </el-select>
          </div>

          <div class="nf-search-field">
            <label class="nf-search-label">系统</label>
            <el-select
              v-model="filters.osKind"
              clearable
              placeholder="全部"
              class="nf-search-input"
            >
              <el-option
                v-for="opt in osKindOptions"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
              />
            </el-select>
          </div>

          <div class="nf-search-field nf-search-field--wide">
            <label class="nf-search-label">时间</label>
            <el-date-picker
              v-model="filters.timeRange"
              type="daterange"
              range-separator="~"
              start-placeholder="开始日期"
              end-placeholder="结束日期"
              value-format="YYYY-MM-DD"
              class="nf-search-input nf-search-datepicker"
            />
          </div>

          <div class="nf-search-actions">
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
            min-width="120"
            show-overflow-tooltip
          >
            <template #default="{ row }">
              <span>{{ cellText(row, f.name) }}</span>
            </template>
          </el-table-column>
        </el-table>
      </div>

      <!-- 批量工具栏：选中行才显示（MVC _List_Toolbar_Batch 行为） -->
      <div
        v-if="selection.length > 0"
        class="lp-card-batchbar"
      >
        <span class="nf-batch-info">已选 {{ selection.length }} 项</span>
        <el-select
          v-model="batch.ver"
          clearable
          placeholder="版本"
          class="nf-batch-ver"
        >
          <el-option
            v-for="opt in pkgVersionOptions"
            :key="opt.value"
            :label="opt.label"
            :value="opt.value"
          />
        </el-select>
        <el-select
          v-model="batch.kind"
          clearable
          placeholder="类型"
          class="nf-batch-kind"
        >
          <el-option
            v-for="opt in pkgKindOptions"
            :key="opt.value"
            :label="opt.label"
            :value="opt.value"
          />
        </el-select>
        <el-button
          type="primary"
          :loading="installing"
          @click="onInstallFramework"
        >
          安装框架
        </el-button>
        <el-button
          type="warning"
          :loading="uninstalling"
          @click="onUninstallFramework"
        >
          卸载框架
        </el-button>
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
.node-framework-page {
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

  .lp-card-batchbar {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 12px 20px;
    border-top: 1px solid var(--el-border-color-light);
    background: var(--el-fill-color-light);
    flex-wrap: wrap;

    .nf-batch-info {
      font-size: 13px;
      color: var(--el-text-color-secondary);
    }

    .nf-batch-ver,
    .nf-batch-kind {
      width: 140px;
    }
  }

  .lp-card-footer {
    padding: 12px 20px;
    border-top: 1px solid var(--el-border-color-light);
  }
}

.nf-search {
  .nf-search-grid {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    gap: 12px 16px;
    align-items: end;
  }

  .nf-search-field {
    display: flex;
    flex-direction: column;
    gap: 6px;
    min-width: 0;

    &--wide {
      grid-column: span 2;
    }
  }

  .nf-search-label {
    font-size: 12px;
    font-weight: 500;
    color: var(--el-text-color-secondary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    text-transform: uppercase;
    letter-spacing: 0.05em;
  }

  .nf-search-input {
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

  .nf-search-datepicker {
    :deep(.el-range-editor) {
      width: 100%;
    }
  }

  .nf-search-actions {
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
  .nf-search .nf-search-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .nf-search .nf-search-grid {
    grid-template-columns: 1fr;
  }

  .nf-search .nf-search-field--wide {
    grid-column: span 1;
  }
}
</style>
