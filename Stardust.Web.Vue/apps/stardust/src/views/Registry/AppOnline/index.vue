<script setup lang="ts">
/**
 * Registry/AppOnline/Index — 应用在线实例列表（自定义迁移页）
 *
 * 后端 AppOnlineController 继承自 EntityController<AppOnline>，[Menu(95)] 可见页，
 * 路由由 MenuTree 注册，直接访问 #/Registry/AppOnline。本页在其之上扩展自定义逻辑
 * （对应 MVC 源 AppOnlineController.cs / _List_Search.cshtml / _List_Toolbar_Batch.cshtml）：
 *  - 后端自定义 url 跳转字段（AutoPage 不渲染 Url 链接列，需自建）：
 *      AppName   DisplayName="{AppName}"  Url="/Registry/App/Detail?id={AppId}"        → 应用详情
 *      NodeName  Header="节点"            Url="/Nodes/Node?Id={NodeId}"                → 节点
 *      Meter     DisplayName="性能"        Url="/Registry/AppMeter?appId={AppId}&clientId={Client}" → 性能
 *      History   DisplayName="历史"        Url="/Registry/AppHistory?appId={AppId}&client={Client}" → 历史
 *      TraceId   TraceUrl() 跟踪列
 *  - 自定义 Action（选中行后显示，data-fields=keys / keys,command,argument）：
 *      FreeMemory GET /Registry/AppOnline/FreeMemory?keys=...        （释放内存）
 *      Execute   GET /Registry/AppOnline/Execute?command=&argument=&keys=... （执行命令）
 *  - 自定义搜索栏（_List_Search）：类别下拉(category, App.FindAllCategory()) + 日期范围(dtStart/dtEnd)
 *
 * 标准列表列来自后端 GetPage 元数据的 list 字段（过滤掉上述 Url 跳转字段），
 * 通过 usePageApi 对接，分页 pageIndex: page-1，总条数 res.page.totalCount。
 * 自定义 Action 写相对无前缀路径 /Registry/AppOnline/<Action>（request 自动拼 baseURL，勿写 /api/）。
 */
import { ref, reactive, computed, onMounted } from 'vue';
import { ElMessage } from 'element-plus';
import request from '@newlifex/cube-vue/core/utils/request';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';
import type { FieldMeta } from '@newlifex/cube-vue/core/types/field';

const api = usePageApi('Registry', 'AppOnline');

/** 后端下发的自定义 url 跳转字段（需自建链接列，保持其在 GetPage list 中的原位，不移除） */
const LINK_FIELDS = new Set(['AppName', 'NodeName', 'Meter', 'History', 'TraceId']);

/** 链接列中文表头（与 MVC 实体 DisplayName / Header 对齐：应用/节点/性能/历史/追踪） */
const LINK_LABELS: Record<string, string> = {
  AppName: '应用',
  NodeName: '节点',
  Meter: '性能',
  History: '历史',
  TraceId: '追踪',
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
  AppId?: number;
  AppName?: string;
  NodeId?: number;
  NodeName?: string;
  Client?: string;
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
const selection = ref<Row[]>([]);

// 搜索栏
const categoryOptions = ref<{ value: string; label: string }[]>([]);
const filters = reactive({
  category: '',
  timeRange: null as [string, string] | null,
});

// 执行命令参数（_List_Toolbar_Batch）
const cmd = reactive({
  command: '',
  argument: '',
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
      // 保留完整 list 顺序（含链接字段原位），渲染时按字段类型区分链接/文本
      listFields.value = meta.list.map(toFieldMeta);
    }
  } catch (e) {
    console.error('[Registry/AppOnline] GetPage failed', e);
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
    console.error('[Registry/AppOnline] fetchCategories failed', e);
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
    console.error('[Registry/AppOnline] GetList failed', e);
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

function linkLabel(name: string): string {
  return LINK_LABELS[name] ?? name;
}

// 自建链接列
function appDetailUrl(row: Row): string {
  return `#/Registry/App/Detail?id=${row.AppId ?? ''}`;
}
function nodeUrl(row: Row): string {
  return `#/Nodes/Node?Id=${row.NodeId ?? ''}`;
}
function meterUrl(row: Row): string {
  return `#/Registry/AppMeter?appId=${row.AppId ?? ''}&clientId=${row.Client ?? ''}`;
}
function historyUrl(row: Row): string {
  return `#/Registry/AppHistory?appId=${row.AppId ?? ''}&client=${row.Client ?? ''}`;
}

// 跟踪链接列（MVC ListFields.TraceUrl() 生成 TraceId 跟踪跳转；WebApi 端 TraceUrl 仅取字段无 Url，需自建）
function traceUrl(row: Row): string {
  return `#/Monitors/Trace/Detail?Id=${row.TraceId ?? ''}`;
}

// 批量选择
function onSelectionChange(sel: Row[]) {
  selection.value = sel;
  selectedIds.value = sel.map((r) => r.Id as number).filter((id) => id != null);
}

// 释放内存（FreeMemory）
const freeing = ref(false);
async function onFreeMemory() {
  if (selectedIds.value.length === 0) {
    ElMessage.warning('请先勾选在线应用');
    return;
  }
  freeing.value = true;
  try {
    const res: any = await request({
      url: `/Registry/AppOnline/FreeMemory`,
      method: 'get',
      params: { keys: selectedIds.value.join(',') },
    });
    if (res && (res.code === 0 || res.code === 200 || res.success)) {
      ElMessage.success(res.message || '已下发释放内存命令');
      await fetchList();
    } else {
      ElMessage.error(res?.message || '操作失败');
    }
  } catch (e: any) {
    ElMessage.error(e?.response?.data?.message || e?.message || '操作失败');
  } finally {
    freeing.value = false;
  }
}

// 执行命令（Execute）
const executing = ref(false);
async function onExecute() {
  if (selectedIds.value.length === 0) {
    ElMessage.warning('请先勾选在线应用');
    return;
  }
  if (!cmd.command) {
    ElMessage.warning('请输入命令');
    return;
  }
  executing.value = true;
  try {
    const res: any = await request({
      url: `/Registry/AppOnline/Execute`,
      method: 'get',
      params: {
        command: cmd.command,
        argument: cmd.argument,
        keys: selectedIds.value.join(','),
      },
    });
    if (res && (res.code === 0 || res.code === 200 || res.success)) {
      ElMessage.success(res.message || '命令已下发');
      await fetchList();
    } else {
      ElMessage.error(res?.message || '操作失败');
    }
  } catch (e: any) {
    ElMessage.error(e?.response?.data?.message || e?.message || '操作失败');
  } finally {
    executing.value = false;
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
  console.log('[Registry/AppOnline] custom view mounted');
  await fetchMeta();
  await fetchCategories();
  await fetchList();
});
</script>

<template>
  <div class="list-page app-online-page">
    <div class="lp-header">
      <h2 class="lph-title">
        应用在线实例
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
      <!-- 搜索栏：类别 + 时间范围 -->
      <div class="lp-card-header ao-search">
        <div class="ao-search-grid">
          <div class="ao-search-field">
            <label class="ao-search-label">类别</label>
            <el-select
              v-model="filters.category"
              clearable
              placeholder="全部"
              class="ao-search-input"
            >
              <el-option
                v-for="opt in categoryOptions"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
              />
            </el-select>
          </div>

          <div class="ao-search-field ao-search-field--wide">
            <label class="ao-search-label">时间</label>
            <el-date-picker
              v-model="filters.timeRange"
              type="daterange"
              range-separator="~"
              start-placeholder="开始日期"
              end-placeholder="结束日期"
              value-format="YYYY-MM-DD"
              class="ao-search-input ao-search-datepicker"
            />
          </div>

          <div class="ao-search-actions">
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
            :label="LINK_FIELDS.has(f.name) ? linkLabel(f.name) : (f.displayName || f.name)"
            show-overflow-tooltip
          >
            <template #default="{ row }">
              <!-- 自建链接列：应用名 → 应用详情 -->
              <template v-if="f.name === 'AppName'">
                <el-link
                  v-if="row.AppName"
                  type="primary"
                  :href="appDetailUrl(row)"
                  target="_blank"
                >
                  {{ row.AppName }}
                </el-link>
                <span v-else>-</span>
              </template>
              <!-- 自建链接列：节点 → 节点页 -->
              <template v-else-if="f.name === 'NodeName'">
                <el-link
                  v-if="row.NodeName"
                  type="primary"
                  :href="nodeUrl(row)"
                  target="_blank"
                >
                  {{ row.NodeName }}
                </el-link>
                <span v-else>-</span>
              </template>
              <!-- 自建链接列：性能 → AppMeter -->
              <template v-else-if="f.name === 'Meter'">
                <el-link
                  v-if="row.AppId != null && row.Client != null"
                  type="primary"
                  :underline="false"
                  :href="meterUrl(row)"
                  target="_blank"
                >
                  性能
                </el-link>
                <span v-else>-</span>
              </template>
              <!-- 自建链接列：历史 → AppHistory -->
              <template v-else-if="f.name === 'History'">
                <el-link
                  v-if="row.AppId != null && row.Client != null"
                  type="info"
                  :underline="false"
                  :href="historyUrl(row)"
                  target="_blank"
                >
                  历史
                </el-link>
                <span v-else>-</span>
              </template>
              <!-- 自建链接列：跟踪 → 追踪详情 -->
              <template v-else-if="f.name === 'TraceId'">
                <el-link
                  v-if="row.TraceId"
                  type="info"
                  :underline="false"
                  :href="traceUrl(row)"
                  target="_blank"
                >
                  {{ row.TraceId }}
                </el-link>
                <span v-else>-</span>
              </template>
              <!-- 标准字段文本列 -->
              <template v-else>
                <span>{{ cellText(row, f.name) }}</span>
              </template>
            </template>
          </el-table-column>
        </el-table>
      </div>

      <!-- 批量工具栏：选中行才显示（MVC _List_Toolbar_Batch 行为） -->
      <div
        v-if="selection.length > 0"
        class="lp-card-batchbar"
      >
        <span class="ao-batch-info">已选 {{ selection.length }} 项</span>
        <el-input
          v-model="cmd.command"
          placeholder="命令"
          class="ao-batch-cmd"
          clearable
        />
        <el-input
          v-model="cmd.argument"
          placeholder="参数"
          class="ao-batch-arg"
          clearable
        />
        <el-button
          type="success"
          :loading="freeing"
          @click="onFreeMemory"
        >
          释放内存
        </el-button>
        <el-button
          type="primary"
          :loading="executing"
          @click="onExecute"
        >
          执行命令
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
.app-online-page {
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

    .ao-batch-info {
      font-size: 13px;
      color: var(--el-text-color-secondary);
    }

    .ao-batch-cmd,
    .ao-batch-arg {
      width: 160px;
    }
  }

  .lp-card-footer {
    padding: 12px 20px;
    border-top: 1px solid var(--el-border-color-light);
  }
}

.ao-search {
  .ao-search-grid {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 12px 16px;
    align-items: end;
  }

  .ao-search-field {
    display: flex;
    flex-direction: column;
    gap: 6px;
    min-width: 0;

    &--wide {
      grid-column: span 2;
    }
  }

  .ao-search-label {
    font-size: 12px;
    font-weight: 500;
    color: var(--el-text-color-secondary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .ao-search-input {
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

  .ao-search-datepicker {
    :deep(.el-range-editor) {
      width: 100%;
    }
  }

  .ao-search-actions {
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
  .ao-search .ao-search-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .ao-search .ao-search-grid {
    grid-template-columns: 1fr;
  }

  .ao-search .ao-search-field--wide {
    grid-column: span 1;
  }
}
</style>
