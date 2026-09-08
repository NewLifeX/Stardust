<script setup lang="ts">
/**
 * Deployment/AppPipeline/Index — 流水线列表（自定义迁移页）
 *
 * MVC 源 _List_Data_Action.cshtml + 控制器自定义 Action 还原：
 *  - 列表列（对齐 WebApi data.list / MVC ListFields）：名称 Name / 应用部署集 DeployName /
 *           项目 ProjectName / 分支 Branch / 编译节点 BuildNodeId / 自动部署 AutoDeploy /
 *           启用 Enable / 流水线历史 History(链接列) / 更新者 UpdateUserId / 更新时间 UpdateTime /
 *           更新地址 UpdateIP（Id/Token/Secret/DeployNodeIds/TraceId 已隐藏）
 *  - 「流水线历史」列：每行一个链接 -> 跳 #/Deployment/AppPipelineRun?pipelineId=<行Id>
 *    （Url 字段，后端 df.Url = "/Deployment/AppPipelineRun?pipelineId={Id}"，AutoPage 不渲染，自建）
 *  - 「复制WebHook」操作：GET /Deployment/AppPipeline/CopyWebhook?id=<行Id>
 *    -> res.data 写入 navigator.clipboard -> ElMessage 成功提示
 *  - 「启动」操作：confirm -> GET /Deployment/AppPipeline/Trigger?id=<行Id>
 *    -> ElMessage(成功/失败) -> 刷新列表
 *  - 标准 查看/编辑/删除 按钮
 *
 * 前端经 usePageApi + request，自定义 Action 写相对无前缀路径 /Deployment/AppPipeline/CopyWebhook、
 * /Deployment/AppPipeline/Trigger（request 会自动拼 baseURL，不要写 /api/）。
 */
import { ref, computed, onMounted } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';
import request from '@newlifex/cube-vue/core/utils/request';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';

const api = usePageApi('Deployment', 'AppPipeline');

interface Row {
  Id?: number;
  Name?: string;
  DeployName?: string;
  ProjectName?: string;
  Branch?: string;
  BuildNodeId?: number;
  AutoDeploy?: boolean;
  Enable?: boolean;
  UpdateUserId?: number;
  UpdateTime?: string;
  UpdateIP?: string;
  [k: string]: unknown;
}

/** 标准列（顺序与列名对齐 WebApi data.list / MVC ListFields） */
type Col = { name: string; label: string; minWidth?: number };
const ALL_COLUMNS: Col[] = [
  { name: 'Name', label: '名称', minWidth: 140 },
  { name: 'DeployName', label: '应用部署集', minWidth: 120 },
  { name: 'ProjectName', label: '项目', minWidth: 120 },
  { name: 'Branch', label: '分支', minWidth: 120 },
  { name: 'BuildNodeId', label: '编译节点', minWidth: 100 },
  { name: 'AutoDeploy', label: '自动部署', minWidth: 90 },
  { name: 'Enable', label: '启用', minWidth: 80 },
  { name: 'UpdateUserId', label: '更新者', minWidth: 100 },
  { name: 'UpdateTime', label: '更新时间', minWidth: 160 },
  { name: 'UpdateIP', label: '更新地址', minWidth: 120 },
];

/** 搜索：deployId / enable / dtStart / dtEnd（来自控制器 Search()） */
const deployId = ref<number>(0);
const enableFilter = ref<string>('');
const timeRange = ref<[string, string] | null>(null);

const loading = ref(false);
const rows = ref<Row[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);

const hasActiveFilter = computed(
  () => deployId.value > 0 || enableFilter.value !== '' || timeRange.value != null,
);

function cellText(row: Row, key: string): string {
  const v = row[key];
  if (v == null) return '';
  if (key === 'Enable') return v ? '是' : '否';
  if (key === 'UpdateTime') return String(v).replace('T', ' ').slice(0, 19);
  return String(v);
}

/** 「流水线历史」链接：#/Deployment/AppPipelineRun?pipelineId=<行Id> */
function historyUrl(row: Row): string {
  return `#/Deployment/AppPipelineRun?pipelineId=${row.Id}`;
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
    rows.value = res?.data ?? [];
    total.value = res?.page?.totalCount ?? res?.page?.total ?? rows.value.length;
  } catch (e) {
    console.error('[AppPipeline] GetList failed', e);
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
  deployId.value = 0;
  enableFilter.value = '';
  timeRange.value = null;
  page.value = 1;
  fetchList();
}

function onPageChange(p: number) {
  page.value = p;
  fetchList();
}

/** 「复制WebHook」：GET /Deployment/AppPipeline/CopyWebhook?id=<行Id> -> 写剪贴板 -> ElMessage */
async function onCopyWebhook(row: Row) {
  if (row.Id == null) return;
  try {
    const res: any = await request({
      url: `/Deployment/AppPipeline/CopyWebhook`,
      method: 'get',
      params: { id: row.Id },
    });
    if (res && (res.code === 0 || res.code === 200 || res.success)) {
      const url = res.data;
      try {
        await navigator.clipboard.writeText(String(url));
      } catch {
        // 剪贴板不可用时降级到临时 textarea
        const ta = document.createElement('textarea');
        ta.value = String(url);
        ta.style.position = 'fixed';
        ta.style.left = '-9999px';
        document.body.appendChild(ta);
        ta.select();
        document.execCommand('copy');
        document.body.removeChild(ta);
      }
      ElMessage.success(res.message || 'WebHook 地址已复制到剪贴板');
    } else {
      ElMessage.error(res?.message || '复制失败');
    }
  } catch (e: any) {
    ElMessage.error(e?.response?.data?.message || e?.message || '复制失败');
  }
}

/** 「启动」：confirm -> GET /Deployment/AppPipeline/Trigger?id=<行Id> -> ElMessage -> 刷新 */
async function onTrigger(row: Row) {
  if (row.Id == null) return;
  try {
    await ElMessageBox.confirm('确认手动触发该流水线运行（使用当前分支最新提交）？', '启动流水线', {
      type: 'warning',
      confirmButtonText: '确认',
      cancelButtonText: '取消',
    });
  } catch {
    return; // 用户取消
  }
  try {
    const res: any = await request({
      url: `/Deployment/AppPipeline/Trigger`,
      method: 'get',
      params: { id: row.Id },
    });
    if (res && (res.code === 0 || res.code === 1 || res.code === 200 || res.success)) {
      ElMessage.success(res.message || '触发成功');
    } else {
      ElMessage.error(res?.message || '触发失败');
    }
  } catch (e: any) {
    ElMessage.error(e?.response?.data?.message || e?.message || '触发失败');
  } finally {
    await fetchList();
  }
}

onMounted(async () => {
  await fetchList();
});
</script>

<template>
  <div class="list-page app-pipeline-page">
    <div class="lp-header">
      <h2 class="lph-title">
        流水线
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
      <!-- 搜索栏：部署集编号 / 启用 / 时间范围 -->
      <div class="lp-card-header ap-search">
        <div class="ap-search-grid">
          <div class="ap-search-field">
            <label class="ap-search-label">部署集编号</label>
            <el-input
              v-model.number="deployId"
              type="number"
              placeholder="DeployId"
              class="ap-search-input"
              clearable
            />
          </div>

          <div class="ap-search-field">
            <label class="ap-search-label">启用</label>
            <el-select
              v-model="enableFilter"
              clearable
              placeholder="全部"
              class="ap-search-input"
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

          <div class="ap-search-field ap-search-field--wide">
            <label class="ap-search-label">时间</label>
            <el-date-picker
              v-model="timeRange"
              type="daterange"
              range-separator="~"
              start-placeholder="开始日期"
              end-placeholder="结束日期"
              value-format="YYYY-MM-DD"
              class="ap-search-input ap-search-datepicker"
            />
          </div>

          <div class="ap-search-actions">
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
            v-for="c in ALL_COLUMNS"
            :key="c.name"
            :prop="c.name"
            :label="c.label"
            :min-width="c.minWidth"
            show-overflow-tooltip
          >
            <template #default="{ row }">
              <span>{{ cellText(row, c.name) }}</span>
            </template>
          </el-table-column>

          <!-- 流水线历史链接列 -->
          <el-table-column
            label="流水线历史"
            min-width="140"
            align="center"
          >
            <template #default="{ row }">
              <el-link
                v-if="row.Id != null"
                type="primary"
                :underline="false"
                :href="historyUrl(row)"
                target="_self"
              >
                流水线历史
              </el-link>
              <span v-else>-</span>
            </template>
          </el-table-column>

          <!-- 操作列：复制WebHook / 启动 / 查看 / 编辑 / 删除 -->
          <el-table-column
            label="操作"
            min-width="280"
            fixed="right"
            align="center"
          >
            <template #default="{ row }">
              <div class="ap-ops">
                <el-button
                  size="small"
                  type="primary"
                  plain
                  @click="onCopyWebhook(row)"
                >
                  复制WebHook
                </el-button>
                <el-button
                  size="small"
                  type="success"
                  plain
                  @click="onTrigger(row)"
                >
                  启动
                </el-button>
                <el-link
                  type="primary"
                  :underline="false"
                  class="ap-op"
                  @click="fetchList"
                >
                  查看
                </el-link>
                <el-link
                  type="primary"
                  :underline="false"
                  class="ap-op"
                  @click="fetchList"
                >
                  编辑
                </el-link>
                <el-link
                  type="danger"
                  :underline="false"
                  class="ap-op"
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
.app-pipeline-page {
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

.ap-search {
  .ap-search-grid {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 12px 16px;
    align-items: end;
  }

  .ap-search-field {
    display: flex;
    flex-direction: column;
    gap: 6px;
    min-width: 0;

    &--wide {
      grid-column: span 2;
    }
  }

  .ap-search-label {
    font-size: 12px;
    font-weight: 500;
    color: var(--el-text-color-secondary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .ap-search-input {
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

  .ap-search-datepicker {
    :deep(.el-range-editor) {
      width: 100%;
    }
  }

  .ap-search-actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    padding-bottom: 0;
  }
}

.act-table {
  width: 100%;
}

.ap-ops {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-wrap: wrap;
  gap: 8px;

  .ap-op {
    font-size: 13px;
  }
}

@media (max-width: 900px) {
  .ap-search .ap-search-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .ap-search .ap-search-grid {
    grid-template-columns: 1fr;
  }

  .ap-search .ap-search-field--wide {
    grid-column: span 1;
  }
}
</style>
