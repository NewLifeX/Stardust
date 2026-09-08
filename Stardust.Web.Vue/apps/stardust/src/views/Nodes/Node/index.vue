<script setup lang="ts">
/**
 * Nodes/Node/Index — 节点列表（自定义迁移页）
 *
 * MVC 源 NodeController.cs + _List_Toolbar_Batch.cshtml 还原：
 *  - 列表列：ID/ProjectName/Name/Code/Category/ProductCode/CityName/Enable/Version/
 *           OSKind/Runtime/Framework/IP/OS/MachineName/Cpu/Memory/TotalSize/Logins/
 *           LastActive/OnlineTime/UpdateTime/UpdateIP
 *    - Name → 链接 #/Nodes/Node/Detail?id=<ID>（Url 字段，自建）
 *    - ProjectName → 链接 #/Platform/GalaxyProject?projectId=<ProjectId>（Url 字段，自建）
 *    - Log → 链接 /Admin/Log?category=节点&linkId=<ID>（Url 字段，自建）
 *  - 批量工具栏：选中行后启用「启用告警/重置告警/修复」按钮
 *    -> GET /Nodes/Node/<Action>?keys=<选中id 逗号串>（后端读 SelectKeys）
 *  - 「追踪」操作：每行按钮 → confirm → GET /Nodes/Node/Trace?id=<行Id> → ElMessage「已发送命令」→ 刷新
 *  - 搜索：启用 + 关键字 Q + 时间范围（加分项）
 *
 * 前端经 usePageApi + request，自定义 Action 写相对无前缀路径 /Nodes/Node/<Action>
 * （request 会自动拼 baseURL，不要写 /api/）。
 */
import { ref, computed, onMounted, watch } from 'vue';
import { useRoute } from 'vue-router';
import { ElMessage, ElMessageBox } from 'element-plus';
import request from '@newlifex/cube-vue/core/utils/request';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';

const api = usePageApi('Nodes', 'Node');
const route = useRoute();
const projectId = ref<number | undefined>(undefined);

interface Row {
  Id?: number;
  ProjectId?: number;
  ProjectName?: string;
  Name?: string;
  Code?: string;
  Category?: string;
  ProductCode?: string;
  CityName?: string;
  Enable?: boolean;
  Version?: string;
  OSKind?: number | string;
  Runtime?: string;
  Framework?: string;
  IP?: string;
  OS?: string;
  MachineName?: string;
  Cpu?: string;
  Memory?: number;
  TotalSize?: number;
  Logins?: number;
  LastActive?: string;
  OnlineTime?: number;
  UpdateTime?: string;
  UpdateIP?: string;
  [k: string]: unknown;
}

/** 标准列（含 Name/ProjectName 链接列在模板自建渲染） */
type Col = { name: string; label: string; width?: number; minWidth?: number };
const ALL_COLUMNS: Col[] = [
  { name: 'Id', label: '编号', width: 80 },
  { name: 'ProjectName', label: '项目', minWidth: 130 },
  { name: 'Name', label: '名称', minWidth: 130 },
  { name: 'Code', label: '编码', minWidth: 120 },
  { name: 'Category', label: '类别', width: 90 },
  { name: 'ProductCode', label: '产品', minWidth: 110 },
  { name: 'CityName', label: '城市', width: 90 },
  { name: 'Enable', label: '启用', width: 80 },
  { name: 'Version', label: '版本', width: 110 },
  { name: 'OSKind', label: '系统', width: 90 },
  { name: 'Runtime', label: '运行时', width: 110 },
  { name: 'Framework', label: '框架', width: 110 },
  { name: 'IP', label: 'IP', width: 130 },
  { name: 'OS', label: '操作系统', minWidth: 140 },
  { name: 'MachineName', label: '机器名', minWidth: 120 },
  { name: 'Cpu', label: 'CPU', minWidth: 110 },
  { name: 'Memory', label: '内存', width: 90 },
  { name: 'TotalSize', label: '磁盘', width: 90 },
  { name: 'Logins', label: '登录数', width: 80 },
  { name: 'LastActive', label: '最后活跃', minWidth: 170 },
  { name: 'OnlineTime', label: '在线时长', width: 110 },
  { name: 'Log', label: '日志', minWidth: 140 },
  { name: 'UpdateTime', label: '更新时间', minWidth: 170 },
  { name: 'UpdateIP', label: '更新IP', width: 120 },
];

const BATCH_ACTIONS = [
  { act: 'SetAlarm', label: '启用告警', params: { enable: 'true' } },
  { act: 'SetAlarm', label: '禁用告警', params: { enable: 'false' } },
  { act: 'ResetAlarm', label: '重置告警', params: {} },
  { act: 'Fix', label: '修复', params: {} },
] as const;

/** 搜索：类别/产品/版本/系统 下拉 + 启用 + 关键字 + 时间范围 */
const enableFilter = ref<string>('');
const keyword = ref<string>('');
const timeRange = ref<[string, string] | null>(null);
const categoryOptions = ref<{ value: string; label: string }[]>([]);
const productOptions = ref<{ value: string; label: string }[]>([]);
const versionOptions = ref<{ value: string; label: string }[]>([]);
const osKindOptions = ref<{ value: string; label: string }[]>([]);

const loading = ref(false);

/** 后端返回 camelCase 字段，转 PascalCase 以匹配 Row 接口与模板（修复单元格全空问题） */
function toPascalRow(r: Record<string, unknown>): Row {
  const out: Record<string, unknown> = {};
  for (const k of Object.keys(r)) {
    out[k.charAt(0).toUpperCase() + k.slice(1)] = r[k];
  }
  return out as Row;
}

const rows = ref<Row[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);

const selectedRows = ref<Row[]>([]);
const batchActing = ref(false);

const hasActiveFilter = computed(
  () =>
    enableFilter.value !== '' ||
    keyword.value.trim() !== '' ||
    timeRange.value != null ||
    categoryFilter.value !== '' ||
    productFilter.value !== '' ||
    versionFilter.value !== '' ||
    osKindFilter.value !== '',
);

/** 类别/产品/版本/系统 下拉（MVC Node.FindAllCategory/FindAllProduct/FindAllVersion/FindAllOSKind） */
const categoryFilter = ref<string>('');
const productFilter = ref<string>('');
const versionFilter = ref<string>('');
const osKindFilter = ref<string>('');

const selectedIds = computed(() =>
  selectedRows.value.map((r) => r.Id).filter((id): id is number => id != null),
);

/** 内存展示（MB → GB 友好） */
function memoryText(row: Row): string {
  const v = row.Memory;
  if (v == null) return '';
  if (v >= 1024) return `${(v / 1024).toFixed(1)} GB`;
  return `${v} MB`;
}

/** 在线时长（秒 → 友好） */
function onlineText(row: Row): string {
  const v = row.OnlineTime;
  if (v == null) return '';
  const s = Number(v);
  if (s < 60) return `${s} 秒`;
  if (s < 3600) return `${Math.floor(s / 60)} 分`;
  if (s < 86400) return `${(s / 3600).toFixed(1)} 时`;
  return `${(s / 86400).toFixed(1)} 天`;
}

function cellText(row: Row, key: string): string {
  const v = row[key];
  if (v == null) return '';
  if (key === 'Enable') return v ? '是' : '否';
  if (key === 'Id' || key === 'Logins') return String(v);
  if (key === 'Memory') return memoryText(row);
  if (key === 'OnlineTime') return onlineText(row);
  if (key === 'LastActive' || key === 'UpdateTime')
    return String(v).replace('T', ' ').slice(0, 19);
  return String(v);
}

/** 链接目标 */
function detailUrl(row: Row): string {
  return `#/Nodes/Node/Detail?id=${row.Id}`;
}
function projectUrl(row: Row): string {
  return `#/Platform/GalaxyProject?projectId=${row.ProjectId}`;
}
function logUrl(row: Row): string {
  return `#/Admin/Log?category=节点&linkId=${row.Id}`;
}

function buildParams(): Record<string, unknown> {
  const p: Record<string, unknown> = {
    pageIndex: page.value - 1,
    pageSize: pageSize.value,
  };
  // 项目维度隔离：从 GalaxyProject 项目页进入时携带 projectId，列表按项目过滤
  if (projectId.value != null) p.projectId = projectId.value;
  if (enableFilter.value !== '') p.enable = enableFilter.value === 'true';
  if (keyword.value.trim() !== '') p.Q = keyword.value.trim();
  if (categoryFilter.value) p.category = categoryFilter.value;
  if (productFilter.value) p.product = productFilter.value;
  if (versionFilter.value) p.version = versionFilter.value;
  if (osKindFilter.value) p.osKind = osKindFilter.value;
  if (timeRange.value?.[0]) p.dtStart = timeRange.value[0];
  if (timeRange.value?.[1]) p.dtEnd = timeRange.value[1];
  return p;
}

/** 从已有记录去重提取类别/产品/版本/系统下拉选项，对齐 MVC FindAll* */
async function fetchSelectOptions() {
  try {
    const res: any = await api.getList<Row>({
      pageIndex: 0,
      pageSize: 1000,
      fields: 'Category,Product,Version,OSKind',
    });
    const data = (res?.data ?? []).map(toPascalRow);
    const pick = (key: string) => {
      const set = new Set<string>();
      data.forEach((r: Row) => {
        const v = r[key as keyof Row];
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
    console.error('[Node] fetchSelectOptions failed', e);
    categoryOptions.value = [{ value: '', label: '全部' }];
    productOptions.value = [{ value: '', label: '全部' }];
    versionOptions.value = [{ value: '', label: '全部' }];
    osKindOptions.value = [{ value: '', label: '全部' }];
  }
}

async function fetchList() {
  loading.value = true;
  try {
    const res: any = await api.getList<Row>(buildParams());
    rows.value = (res?.data ?? []).map(toPascalRow);
    total.value = res?.page?.totalCount ?? res?.page?.total ?? rows.value.length;
  } catch (e) {
    console.error('[Node] GetList failed', e);
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
  enableFilter.value = '';
  keyword.value = '';
  categoryFilter.value = '';
  productFilter.value = '';
  versionFilter.value = '';
  osKindFilter.value = '';
  timeRange.value = null;
  page.value = 1;
  fetchList();
}

function onSelectionChange(val: Row[]) {
  selectedRows.value = val;
}

function onPageChange(p: number) {
  page.value = p;
  fetchList();
}

/** 阈值输入（MVC _List_Toolbar_Batch 的 alarmRate，默认 80） */
const alarmRate = ref<number>(80);

/** 批量操作：GET /Nodes/Node/<Action>?keys=id1,id2&enable/alarmRate */
async function onBatchAction(act: string, params: Record<string, unknown> = {}) {
  if (selectedIds.value.length === 0) {
    ElMessage.warning('请先选择要操作的节点');
    return;
  }
  try {
    await ElMessageBox.confirm(
      `确认对选中的 ${selectedIds.value.length} 个节点执行「${act}」？`,
      '批量操作',
      { type: 'warning', confirmButtonText: '确认', cancelButtonText: '取消' },
    );
  } catch {
    return;
  }
  batchActing.value = true;
  try {
    const reqParams: Record<string, unknown> = { keys: selectedIds.value.join(','), ...params };
    if (act === 'ResetAlarm') reqParams.alarmRate = alarmRate.value;
    const res: any = await request({
      url: `/Nodes/Node/${act}`,
      method: 'get',
      params: reqParams,
    });
    if (res && (res.code === 0 || res.code === 200 || res.success)) {
      ElMessage.success(res.message || '操作成功');
      await fetchList();
    } else {
      ElMessage.error(res?.message || '操作失败');
    }
  } catch (e: any) {
    ElMessage.error(e?.response?.data?.message || e?.message || '操作失败');
  } finally {
    batchActing.value = false;
  }
}

/** 「追踪」：confirm → GET /Nodes/Node/Trace?id= → ElMessage「已发送命令」→ 刷新 */
async function onTrace(row: Row) {
  if (row.Id == null) return;
  try {
    await ElMessageBox.confirm('确认向该节点发送「截屏/抓日志」命令？', '追踪节点', {
      type: 'warning',
      confirmButtonText: '确认',
      cancelButtonText: '取消',
    });
  } catch {
    return; // 用户取消
  }
  try {
    const res: any = await request({
      url: `/Nodes/Node/Trace`,
      method: 'get',
      params: { id: row.Id },
    });
    if (res && (res.code === 0 || res.code === 200 || res.success)) {
      ElMessage.success(res.message || '已发送命令');
    } else {
      ElMessage.error(res?.message || '发送失败');
    }
  } catch (e: any) {
    ElMessage.error(e?.response?.data?.message || e?.message || '发送失败');
  } finally {
    await fetchList();
  }
}

onMounted(async () => {
  projectId.value = route.query.projectId ? Number(route.query.projectId) : undefined;
  await fetchSelectOptions();
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
  <div class="list-page node-page">
    <div class="lp-header">
      <h2 class="lph-title">
        节点
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
      <!-- 搜索栏：类别/产品/版本/系统/启用/关键字 + 时间范围（对齐 MVC _List_Search） -->
      <div class="lp-card-header node-search">
        <div class="node-search-grid">
          <div class="node-search-field">
            <label class="node-search-label">类别</label>
            <el-select
              v-model="categoryFilter"
              clearable
              placeholder="全部"
              class="node-search-input"
            >
              <el-option
                v-for="opt in categoryOptions"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
              />
            </el-select>
          </div>

          <div class="node-search-field">
            <label class="node-search-label">产品</label>
            <el-select
              v-model="productFilter"
              clearable
              placeholder="全部"
              class="node-search-input"
            >
              <el-option
                v-for="opt in productOptions"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
              />
            </el-select>
          </div>

          <div class="node-search-field">
            <label class="node-search-label">版本</label>
            <el-select
              v-model="versionFilter"
              clearable
              placeholder="全部"
              class="node-search-input"
            >
              <el-option
                v-for="opt in versionOptions"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
              />
            </el-select>
          </div>

          <div class="node-search-field">
            <label class="node-search-label">系统</label>
            <el-select
              v-model="osKindFilter"
              clearable
              placeholder="全部"
              class="node-search-input"
            >
              <el-option
                v-for="opt in osKindOptions"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
              />
            </el-select>
          </div>

          <div class="node-search-field">
            <label class="node-search-label">启用</label>
            <el-select
              v-model="enableFilter"
              clearable
              placeholder="全部"
              class="node-search-input"
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

          <div class="node-search-field">
            <label class="node-search-label">关键字</label>
            <el-input
              v-model="keyword"
              placeholder="名称/编码/IP"
              class="node-search-input"
              clearable
            />
          </div>

          <div class="node-search-field node-search-field--wide">
            <label class="node-search-label">时间</label>
            <el-date-picker
              v-model="timeRange"
              type="daterange"
              range-separator="~"
              start-placeholder="开始日期"
              end-placeholder="结束日期"
              value-format="YYYY-MM-DD"
              class="node-search-input node-search-datepicker"
            />
          </div>

          <div class="node-search-actions">
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

      <!-- 批量工具栏（对齐 MVC _List_Toolbar_Batch：启用告警/禁用告警/重置阈值+alarmRate/修正） -->
      <div class="lp-batchbar">
        <span class="lp-batchbar-tip">已选 {{ selectedIds.length }} 项</span>
        <el-button
          v-for="a in BATCH_ACTIONS"
          :key="'batch-' + a.act + a.label"
          size="small"
          :disabled="selectedIds.length === 0 || batchActing"
          :loading="batchActing"
          @click="onBatchAction(a.act, a.params)"
        >
          {{ a.label }}
        </el-button>
        <span class="lp-batchbar-rate">
          阈值
          <el-input-number
            v-model="alarmRate"
            :min="0"
            :max="100"
            size="small"
            controls-position="right"
          />
        </span>
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
            width="46"
          />

          <el-table-column
            v-for="c in ALL_COLUMNS"
            :key="c.name"
            :prop="c.name"
            :label="c.label"
            :width="c.width"
            :min-width="c.minWidth"
            show-overflow-tooltip
          >
            <template #default="{ row }">
              <!-- Name 链接列 -->
              <el-link
                v-if="c.name === 'Name' && row.Id != null"
                type="primary"
                :underline="false"
                :href="detailUrl(row)"
                target="_blank"
              >
                {{ cellText(row, c.name) }}
              </el-link>
              <!-- ProjectName 链接列 -->
              <el-link
                v-else-if="c.name === 'ProjectName' && row.ProjectId != null"
                type="primary"
                :underline="false"
                :href="projectUrl(row)"
              >
                {{ cellText(row, c.name) }}
              </el-link>
              <!-- 其余字段纯文本 -->
              <span v-else>{{ cellText(row, c.name) }}</span>
            </template>
          </el-table-column>

          <!-- 日志链接列 -->
          <el-table-column
            label="日志"
            min-width="80"
            align="center"
          >
            <template #default="{ row }">
              <el-link
                v-if="row.Id != null"
                type="primary"
                :underline="false"
                :href="logUrl(row)"
                target="_blank"
              >
                日志
              </el-link>
              <span v-else>-</span>
            </template>
          </el-table-column>

          <!-- 操作列：追踪 / 详情 / 编辑 / 删除 -->
          <el-table-column
            label="操作"
            min-width="280"
            fixed="right"
            align="center"
          >
            <template #default="{ row }">
              <div class="node-ops">
                <el-button
                  size="small"
                  type="primary"
                  plain
                  @click="onTrace(row)"
                >
                  追踪
                </el-button>
                <el-link
                  type="primary"
                  :underline="false"
                  class="node-op"
                  @click="fetchList"
                >
                  详情
                </el-link>
                <el-link
                  type="primary"
                  :underline="false"
                  class="node-op"
                  @click="fetchList"
                >
                  编辑
                </el-link>
                <el-link
                  type="danger"
                  :underline="false"
                  class="node-op"
                  @click="fetchList"
                >
                  删除
                </el-link>
              </div>
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
.node-page {
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

  .lp-batchbar {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 12px 20px;
    border-bottom: 1px solid var(--el-border-color-light);
    background: var(--el-fill-color-light);

    .lp-batchbar-tip {
      font-size: 12px;
      color: var(--el-text-color-secondary);
      margin-right: 4px;
    }

    .lp-batchbar-rate {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      font-size: 12px;
      color: var(--el-text-color-secondary);

      :deep(.el-input-number) {
        width: 92px;
      }
    }
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

.node-search {
  .node-search-grid {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    gap: 12px 16px;
    align-items: end;
  }

  .node-search-field {
    display: flex;
    flex-direction: column;
    gap: 6px;
    min-width: 0;

    &--wide {
      grid-column: span 2;
    }
  }

  .node-search-label {
    font-size: 12px;
    font-weight: 500;
    color: var(--el-text-color-secondary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .node-search-input {
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

  .node-search-datepicker {
    :deep(.el-range-editor) {
      width: 100%;
    }
  }

  .node-search-actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    padding-bottom: 0;
  }
}

.act-table {
  width: 100%;
}

.node-ops {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-wrap: wrap;
  gap: 8px;

  .node-op {
    font-size: 13px;
  }
}

@media (max-width: 900px) {
  .node-search .node-search-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .node-search .node-search-grid {
    grid-template-columns: 1fr;
  }

  .node-search .node-search-field--wide {
    grid-column: span 1;
  }
}
</style>
