<script setup lang="ts">
/**
 * Deployment/AppDeployNode/Index — 部署节点列表（自定义迁移页）
 *
 * MVC 源 _List_Data / _List_Search / _List_Toolbar_Batch 还原：
 *  - 列表列：编号/发布名/[应用]/[节点]/IP/延迟/启用/进程/进程时间/版本/编译时间/最后活跃/远程操作/操作
 *    （URL 带 deployId 时不显示「应用」「节点」列，仅显示其余）
 *  - 「远程操作」列每行 5 个链接：发布/启动/终止/重启/卸载
 *    -> GET /Deployment/AppDeployNode/Operate?act=<动作>&id=<行Id>（后端带 confirm）
 *  - 搜索栏：_Enable(启用下拉) + _DateRange(时间范围 dtStart/dtEnd)
 *  - 批量工具栏：选中行时启用 5 个批量按钮（发布/启动/终止/重启/卸载）
 *    -> POST /Deployment/AppDeployNode/BatchOperate?act=<动作>（keys=选中id 数组）
 *  - 后端 API：GET /api/Deployment/AppDeployNode（GetPage/GetList）、Operate/BatchOperate 已实现
 *
 * 前端经 usePageApi + request，自定义 Action 写相对无前缀路径 /Deployment/AppDeployNode/Operate
 * （request 会自动拼 baseURL，不要写 /api/... 否则双写 404）。
 */
import { ref, reactive, computed, onMounted } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';
import request from '@newlifex/cube-vue/core/utils/request';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';

const api = usePageApi('Deployment', 'AppDeployNode');

interface Row {
  Id?: number;
  DeployName?: string;
  DeployId?: number;
  NodeId?: number;
  NodeName?: string;
  IP?: string;
  Delay?: number;
  Enable?: boolean;
  ProcessId?: number;
  StartTime?: string;
  Version?: string;
  Compile?: string;
  LastActive?: string;
  [k: string]: unknown;
}

/** 标准列（带 deployId 时移除「应用」「节点」列）
 * 注意：「应用」列在 MVC 中显示 entity.DeployName ?? entity.Deploy?.Name，
 * 这里用特殊键 __App__ 由 cellText 单独渲染。 */
type Col = { name: string; label: string; width?: number; hideWithDeployId?: boolean };
const ALL_COLUMNS: Col[] = [
  { name: 'Id', label: '编号', width: 80 },
  { name: 'DeployName', label: '发布名' },
  { name: '__App__', label: '应用', width: 140, hideWithDeployId: true },
  { name: 'NodeName', label: '节点', width: 140, hideWithDeployId: true },
  { name: 'IP', label: 'IP', width: 130 },
  { name: 'Delay', label: '延迟', width: 80 },
  { name: 'Enable', label: '启用', width: 80 },
  { name: 'ProcessId', label: '进程', width: 90 },
  { name: 'StartTime', label: '进程时间', width: 170 },
  { name: 'Version', label: '版本', width: 120 },
  { name: 'Compile', label: '编译时间', width: 170 },
  { name: 'LastActive', label: '最后活跃', width: 170 },
];

const REMOTE_ACTIONS = [
  { act: 'install', label: '发布' },
  { act: 'start', label: '启动' },
  { act: 'stop', label: '终止' },
  { act: 'restart', label: '重启' },
  { act: 'uninstall', label: '卸载' },
] as const;

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

/** 是否从 AppDeploy 带 deployId 进入（隐藏应用/节点列） */
const deployId = ref<number>(0);
/** 搜索：启用下拉（''=全部 / true / false） */
const enableFilter = ref<string>('');
/** 搜索：时间范围 dtStart/dtEnd */
const timeRange = ref<[string, string] | null>(null);

const selectedRows = ref<Row[]>([]);
const batchActing = ref(false);

const columns = computed(() =>
  deployId.value > 0 ? ALL_COLUMNS.filter((c) => !c.hideWithDeployId) : ALL_COLUMNS,
);

const hasActiveFilter = computed(
  () => enableFilter.value !== '' || timeRange.value != null,
);

const selectedIds = computed(() =>
  selectedRows.value.map((r) => r.Id).filter((id): id is number => id != null),
);

function appDisplay(row: Row): string {
  if (row.DeployName) return row.DeployName;
  const deploy: any = row.Deploy;
  if (deploy?.Name) return String(deploy.Name);
  return String(row.DeployId ?? '');
}

function cellText(row: Row, key: string): string {
  if (key === '__App__') return appDisplay(row);
  const v = row[key];
  if (v == null) return '';
  if (key === 'Enable') return v ? '是' : '否';
  if (key === 'StartTime' || key === 'Compile' || key === 'LastActive' || key === 'LastUpload')
    return String(v).replace('T', ' ').slice(0, 19);
  return String(v);
}

function buildParams(): Record<string, unknown> {
  const p: Record<string, unknown> = {
    pageIndex: page.value - 1,
    pageSize: pageSize.value,
  };
  if (deployId.value > 0) p.deployId = deployId.value;
  if (enableFilter.value !== '') p.enable = enableFilter.value === 'true';
  if (timeRange.value?.[0]) p.dtStart = timeRange.value[0];
  if (timeRange.value?.[1]) p.dtEnd = timeRange.value[1];
  return p;
}

async function fetchList() {
  loading.value = true;
  try {
    const res: any = await api.getList<Row>(buildParams());
    rows.value = (res?.data ?? []).map(toPascalRow);
    total.value = res?.page?.totalCount ?? res?.page?.total ?? rows.value.length;
  } catch (e) {
    console.error('[AppDeployNode] GetList failed', e);
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

/** 单行远程操作：GET /Deployment/AppDeployNode/Operate?act=&id= */
async function onRemoteOperate(act: string, row: Row) {
  if (row.Id == null) return;
  try {
    await ElMessageBox.confirm('危险操作！确认执行？', `${act} 操作`, {
      type: 'warning',
      confirmButtonText: '确认',
      cancelButtonText: '取消',
    });
  } catch {
    return; // 用户取消
  }
  try {
    const res: any = await request({
      url: `/Deployment/AppDeployNode/Operate`,
      method: 'get',
      params: { act, id: row.Id },
    });
    if (res && (res.code === 0 || res.code === 200)) {
      ElMessage.success(res.message || '操作成功');
    } else {
      ElMessage.error(res?.message || '操作失败');
    }
  } catch (e: any) {
    ElMessage.error(e?.response?.data?.message || e?.message || '操作失败');
  }
}

/** 批量操作：POST /Deployment/AppDeployNode/BatchOperate?act= 带 keys 数组 */
async function onBatchOperate(act: string) {
  if (selectedIds.value.length === 0) {
    ElMessage.warning('请先选择要操作的行');
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
    const res: any = await request({
      url: `/Deployment/AppDeployNode/BatchOperate`,
      method: 'post',
      params: { act },
      data: new URLSearchParams({ keys: selectedIds.value.join(',') }),
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    });
    if (res && (res.code === 0 || res.code === 200)) {
      ElMessage.success(res.message || '批量操作成功');
      await fetchList();
    } else {
      ElMessage.error(res?.message || '批量操作失败');
    }
  } catch (e: any) {
    ElMessage.error(e?.response?.data?.message || e?.message || '批量操作失败');
  } finally {
    batchActing.value = false;
  }
}

onMounted(async () => {
  // 从 hash 读取 deployId（来自 AppDeploy 的「部署节点」跳转）
  const m = location.hash.match(/[?&]deployId=(\d+)/);
  if (m) deployId.value = Number(m[1]);
  await fetchList();
});
</script>

<template>
  <div class="list-page app-deploy-node-page">
    <div class="lp-header">
      <h2 class="lph-title">
        部署节点
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
      <!-- 搜索栏：启用 + 时间范围 -->
      <div class="lp-card-header adn-search">
        <div class="adn-search-grid">
          <div class="adn-search-field">
            <label class="adn-search-label">启用</label>
            <el-select
              v-model="enableFilter"
              clearable
              placeholder="全部"
              class="adn-search-input"
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

          <div class="adn-search-field adn-search-field--wide">
            <label class="adn-search-label">时间</label>
            <el-date-picker
              v-model="timeRange"
              type="daterange"
              range-separator="~"
              start-placeholder="开始日期"
              end-placeholder="结束日期"
              value-format="YYYY-MM-DD"
              class="adn-search-input adn-search-datepicker"
            />
          </div>

          <div class="adn-search-actions">
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

      <!-- 批量工具栏 -->
      <div class="lp-batchbar">
        <span class="lp-batchbar-tip">已选 {{ selectedIds.length }} 项</span>
        <el-button
          v-for="a in REMOTE_ACTIONS"
          :key="'batch-' + a.act"
          size="small"
          :disabled="selectedIds.length === 0 || batchActing"
          :loading="batchActing"
          @click="onBatchOperate(a.act)"
        >
          {{ a.label }}
        </el-button>
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
            v-for="c in columns"
            :key="c.name"
            :prop="c.name"
            :label="c.label"
            :width="c.width"
            show-overflow-tooltip
          >
            <template #default="{ row }">
              <a
                v-if="c.name === 'DeployName' && row.DeployId"
                class="adn-link"
                :href="`/Deployment/AppDeploy?deployId=${row.DeployId}`"
                target="_blank"
              >{{ cellText(row, c.name) }}</a>
              <span v-else>{{ cellText(row, c.name) }}</span>
            </template>
          </el-table-column>

          <!-- 远程操作列：5 个动作 -->
          <el-table-column
            label="远程操作"
            width="340"
            fixed="right"
            align="center"
          >
            <template #default="{ row }">
              <div class="adn-ops">
                <el-link
                  v-for="a in REMOTE_ACTIONS"
                  :key="a.act"
                  type="primary"
                  :underline="false"
                  class="adn-op"
                  @click="onRemoteOperate(a.act, row)"
                >
                  {{ a.label }}
                </el-link>
              </div>
            </template>
          </el-table-column>

          <!-- 操作列（详情/编辑/删除，框架标准） -->
          <el-table-column
            label="操作"
            width="160"
            fixed="right"
            align="center"
          >
            <template #default>
              <div class="adn-ops">
                <el-link
                  type="primary"
                  :underline="false"
                  class="adn-op"
                  @click="fetchList"
                >
                  详情
                </el-link>
                <el-link
                  type="primary"
                  :underline="false"
                  class="adn-op"
                  @click="fetchList"
                >
                  编辑
                </el-link>
                <el-link
                  type="danger"
                  :underline="false"
                  class="adn-op"
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
.app-deploy-node-page {
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

.adn-search {
  .adn-search-grid {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 12px 16px;
    align-items: end;
  }

  .adn-search-field {
    display: flex;
    flex-direction: column;
    gap: 6px;
    min-width: 0;

    &--wide {
      grid-column: span 2;
    }
  }

  .adn-search-label {
    font-size: 12px;
    font-weight: 500;
    color: var(--el-text-color-secondary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .adn-search-input {
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

  .adn-search-datepicker {
    :deep(.el-range-editor) {
      width: 100%;
    }
  }

  .adn-search-actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    padding-bottom: 0;
  }
}

.act-table {
  width: 100%;
}

.adn-link {
  color: var(--el-color-primary);
  text-decoration: none;
  font-size: 13px;

  &:hover {
    text-decoration: underline;
  }
}

.adn-ops {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-wrap: wrap;
  gap: 10px;

  .adn-op {
    font-size: 13px;
  }
}

@media (max-width: 900px) {
  .adn-search .adn-search-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .adn-search .adn-search-grid {
    grid-template-columns: 1fr;
  }

  .adn-search .adn-search-field--wide {
    grid-column: span 1;
  }
}
</style>
