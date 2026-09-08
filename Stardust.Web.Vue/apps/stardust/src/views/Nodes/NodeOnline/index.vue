<script setup lang="ts">
/**
 * Nodes/NodeOnline/Index — 在线节点列表（命令面板，自定义迁移页）
 *
 * 后端 NodeOnlineController 继承自 EntityController<NodeOnline>，[Menu(80)] 可见页，
 * 路由由 MenuTree 注册，直接访问 #/Nodes/NodeOnline。本页在其之上扩展自定义逻辑
 * （对应 MVC 源 NodeOnlineController.cs / _List_Search.cshtml / _List_Toolbar_Batch.cshtml）：
 *  - 后端自定义 url 跳转字段（AutoPage 不渲染 Url 链接列，需自建）：
 *      Name     DisplayName="{Name}"  Url="/Nodes/Node/Detail?Id={NodeID}"  → 节点详情
 *      TraceId  TraceUrl() 跟踪列                                    → 追踪详情
 *  - 自定义 Action（选中行后显示，对应 _List_Toolbar_Batch 的 data-fields=keys）：
 *      CheckUpgrade GET /Nodes/NodeOnline/CheckUpgrade?keys=...        （检查更新）
 *      SyncTime    GET /Nodes/NodeOnline/SyncTime?keys=...             （同步时间）
 *      Restart     GET /Nodes/NodeOnline/Restart?keys=...              （重启服务）
 *      Reboot      GET /Nodes/NodeOnline/Reboot?keys=...               （重启系统）
 *      Execute     GET /Nodes/NodeOnline/Execute?command=&argument=&keys=... （执行命令）
 *  - 自定义搜索栏（_List_Search）：类别下拉(category, Node.FindAllCategory())
 *
 * 标准列表列来自后端 GetPage 元数据的 list 字段（过滤掉 Name / TraceId 两个 Url 跳转字段），
 * 通过 usePageApi 对接，分页 pageIndex: page-1，总条数 res.page.totalCount。
 * 自定义 Action 写相对无前缀路径 /Nodes/NodeOnline/<Action>（request 自动拼 baseURL，勿写 /api/）。
 */
import { ref, reactive, computed, onMounted } from 'vue';
import { ElMessage } from 'element-plus';
import request from '@newlifex/cube-vue/core/utils/request';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';
import type { FieldMeta } from '@newlifex/cube-vue/core/types/field';

const api = usePageApi('Nodes', 'NodeOnline');

/** 后端下发的自定义 url 跳转字段（自建链接列，不进入自动表格列） */
const SKIP_FIELDS = new Set(['Name', 'TraceId']);

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
  NodeID?: number;
  Name?: string;
  TraceId?: string;
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
});

// 执行命令参数（_List_Toolbar_Batch）
const cmd = reactive({
  command: '',
  argument: '',
});

function toFieldMeta(f: BackendField): FieldMeta {
  return {
    name: f.name,
    // displayName 形如 "{Name}" 时去掉花括号，还原成可读列名
    displayName: (f.displayName || f.name).replace(/^\{(.+)\}$/, '$1'),
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
    console.error('[Nodes/NodeOnline] GetPage failed', e);
  }
}

async function fetchCategories() {
  try {
    // 从已有记录去重提取类别，与 MVC Node.FindAllCategory() 语义一致
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
    console.error('[Nodes/NodeOnline] fetchCategories failed', e);
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

    const res: any = await api.getList<Row>(params);
    rows.value = res?.data ?? [];
    total.value = res?.page?.totalCount ?? res?.page?.total ?? rows.value.length;
  } catch (e) {
    console.error('[Nodes/NodeOnline] GetList failed', e);
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
function nodeDetailUrl(row: Row): string {
  return `#/Nodes/Node/Detail?Id=${row.NodeID ?? ''}`;
}
function traceUrl(row: Row): string {
  return `#/Monitors/Trace/Detail?Id=${row.TraceId ?? ''}`;
}

// 批量选择
function onSelectionChange(sel: Row[]) {
  selection.value = sel;
  selectedIds.value = sel.map((r) => r.Id as number).filter((id) => id != null);
}

// 批量命令（相对无前缀路径，request 自动拼 baseURL）
async function runAction(url: string, extra: Record<string, unknown> = {}, successMsg = '操作已下发') {
  if (selectedIds.value.length === 0) {
    ElMessage.warning('请先勾选在线节点');
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

const acting = reactive({
  check: false,
  sync: false,
  restart: false,
  reboot: false,
  execute: false,
});

async function onCheckUpgrade() {
  acting.check = true;
  try {
    await runAction('/Nodes/NodeOnline/CheckUpgrade', {}, '已下发检查更新');
  } finally {
    acting.check = false;
  }
}
async function onSyncTime() {
  acting.sync = true;
  try {
    await runAction('/Nodes/NodeOnline/SyncTime', {}, '已下发同步时间');
  } finally {
    acting.sync = false;
  }
}
async function onRestart() {
  acting.restart = true;
  try {
    await runAction('/Nodes/NodeOnline/Restart', {}, '已下发重启服务');
  } finally {
    acting.restart = false;
  }
}
async function onReboot() {
  acting.reboot = true;
  try {
    await runAction('/Nodes/NodeOnline/Reboot', {}, '已下发重启系统');
  } finally {
    acting.reboot = false;
  }
}
async function onExecute() {
  if (selectedIds.value.length === 0) {
    ElMessage.warning('请先勾选在线节点');
    return;
  }
  if (!cmd.command) {
    ElMessage.warning('请输入命令');
    return;
  }
  acting.execute = true;
  try {
    await runAction(
      '/Nodes/NodeOnline/Execute',
      { command: cmd.command, argument: cmd.argument },
      '命令已下发',
    );
  } finally {
    acting.execute = false;
  }
}

function handleSearch() {
  page.value = 1;
  fetchList();
}
function handleReset() {
  filters.category = '';
  page.value = 1;
  fetchList();
}

function onPageChange(p: number) {
  page.value = p;
  fetchList();
}

const hasActiveFilter = computed(() => !!filters.category);

onMounted(async () => {
  console.log('[Nodes/NodeOnline] custom view mounted');
  await fetchMeta();
  await fetchCategories();
  await fetchList();
});
</script>

<template>
  <div class="list-page node-online-page">
    <div class="lp-header">
      <h2 class="lph-title">
        在线节点
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
      <!-- 搜索栏：类别 -->
      <div class="lp-card-header no-search">
        <div class="no-search-grid">
          <div class="no-search-field">
            <label class="no-search-label">类别</label>
            <el-select
              v-model="filters.category"
              clearable
              placeholder="全部"
              class="no-search-input"
            >
              <el-option
                v-for="opt in categoryOptions"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
              />
            </el-select>
          </div>

          <div class="no-search-actions">
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

          <template
            v-for="f in listFields"
            :key="f.name"
          >
            <el-table-column
              :prop="f.name"
              :label="f.displayName || f.name"
              show-overflow-tooltip
            >
              <template #default="{ row }">
                <span>{{ cellText(row, f.name) }}</span>
              </template>
            </el-table-column>

            <!-- 自定义链接列：名称 → 节点详情（对齐 MVC 列序，紧跟 ProjectName 之后） -->
            <el-table-column
              v-if="f.name === 'ProjectName'"
              label="名称"
              width="200"
              fixed="left"
            >
              <template #default="{ row }">
                <el-link
                  v-if="row.Name"
                  type="primary"
                  :href="nodeDetailUrl(row)"
                  target="_blank"
                >
                  {{ row.Name }}
                </el-link>
                <span v-else>-</span>
              </template>
            </el-table-column>

            <!-- 自定义链接列：追踪 → 追踪详情（对齐 MVC 列序，紧跟 InternetScore 之后） -->
            <el-table-column
              v-if="f.name === 'InternetScore'"
              label="追踪"
              width="120"
              align="center"
            >
              <template #default="{ row }">
                <el-link
                  v-if="row.TraceId"
                  type="info"
                  :underline="false"
                  :href="traceUrl(row)"
                  target="_blank"
                >
                  追踪
                </el-link>
                <span v-else>-</span>
              </template>
            </el-table-column>
          </template>
        </el-table>
      </div>

      <!-- 批量工具栏：选中行才显示（MVC _List_Toolbar_Batch 行为） -->
      <div
        v-if="selection.length > 0"
        class="lp-card-batchbar"
      >
        <span class="no-batch-info">已选 {{ selection.length }} 项</span>
        <el-button
          type="success"
          :loading="acting.check"
          @click="onCheckUpgrade"
        >
          检查更新
        </el-button>
        <el-button
          type="success"
          :loading="acting.sync"
          @click="onSyncTime"
        >
          同步时间
        </el-button>
        <el-button
          type="warning"
          :loading="acting.restart"
          @click="onRestart"
        >
          重启服务
        </el-button>
        <el-button
          type="danger"
          :loading="acting.reboot"
          @click="onReboot"
        >
          重启系统
        </el-button>
        <el-input
          v-model="cmd.command"
          placeholder="命令"
          class="no-batch-cmd"
          clearable
        />
        <el-input
          v-model="cmd.argument"
          placeholder="参数"
          class="no-batch-arg"
          clearable
        />
        <el-button
          type="primary"
          :loading="acting.execute"
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
.node-online-page {
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

    .no-batch-info {
      font-size: 13px;
      color: var(--el-text-color-secondary);
    }

    .no-batch-cmd,
    .no-batch-arg {
      width: 160px;
    }
  }

  .lp-card-footer {
    padding: 12px 20px;
    border-top: 1px solid var(--el-border-color-light);
  }
}

.no-search {
  .no-search-grid {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    gap: 12px 16px;
    align-items: end;
  }

  .no-search-field {
    display: flex;
    flex-direction: column;
    gap: 6px;
    min-width: 0;
  }

  .no-search-label {
    font-size: 12px;
    font-weight: 500;
    color: var(--el-text-color-secondary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .no-search-input {
    width: 100%;

    :deep(.el-input__wrapper),
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
    :deep(.el-select__wrapper.is-focused) {
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

  .no-search-actions {
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
  .no-search .no-search-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .no-search .no-search-grid {
    grid-template-columns: 1fr;
  }
}
</style>
