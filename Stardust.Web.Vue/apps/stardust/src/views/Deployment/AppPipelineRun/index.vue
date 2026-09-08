<script setup lang="ts">
/**
 * Deployment/AppPipelineRun/Index — 流水线运行记录列表（自定义迁移页）
 *
 * MVC 源控制器 ListFields + 自定义 Action 还原：
 *  - 列表列：运行编号(隐藏)/流水线 PipelineId(PipelineName)/状态 Status/分支 Branch/
 *           提交 CommitId/开始时间/结束时间/耗时 Cost/创建时间 CreateTime 等
 *           （Id/TraceId/TriggerSource 已隐藏）
 *  - 「步骤」链接列：每行 -> #/Deployment/AppPipelineStep?runId=<行Id>
 *    （Url 字段，后端 df.Url = "/Deployment/AppPipelineStep?runId={Id}"，AutoPage 不渲染，自建）
 *  - 「重跑」操作列：confirm -> GET /Deployment/AppPipelineRun/Reprocess?id=<行Id>
 *    -> ElMessage(成功/失败) -> 刷新列表
 *  - TraceUrl（TraceId 调用链）：若后端 GetPage 行数据返回 TraceUrl 字段则渲染为调用链链接（加分项）
 *  - 搜索：pipelineId / status / 时间范围 dtStart/dtEnd（加分项）
 *
 * 前端经 usePageApi + request，自定义 Action 写相对无前缀路径 /Deployment/AppPipelineRun/Reprocess
 * （request 会自动拼 baseURL，不要写 /api/）。
 */
import { ref, computed, onMounted, defineAsyncComponent } from 'vue';
import { useRoute } from 'vue-router';
import { ElMessage, ElMessageBox } from 'element-plus';
import request from '@newlifex/cube-vue/core/utils/request';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';

const route = useRoute();
const api = usePageApi('Deployment', 'AppPipelineRun');

/** 隐藏页 Detail 兜底：三级路由 #/Deployment/AppPipelineRun/<id> 命中 index.vue 时渲染详情组件 */
const AppPipelineRunDetail = defineAsyncComponent(() => import('./detail.vue'));

/** 当前是否处于详情模式（三级路由 id） */
const isDetail = computed<boolean>(() => {
  const id = Number((route.params as any).id ?? 0);
  return id > 0;
});

interface Row {
  Id?: number;
  PipelineId?: number;
  PipelineName?: string;
  Status?: number | string;
  Branch?: string;
  CommitId?: string;
  BuildStartedTime?: string;
  BuildFinishedTime?: string;
  DeployStartedTime?: string;
  DeployFinishedTime?: string;
  StartTime?: string;
  EndTime?: string;
  Cost?: number;
  TraceId?: string;
  TraceUrl?: string;
  CreateTime?: string;
  [k: string]: unknown;
}

/**
 * 标准列——对齐 MVC ListFields / WebApi GetPage data.list（权威列，按列顺序）：
 *   PipelineName(流水线) / Status(状态) / CommitId(提交SHA) / CommitMessage(提交信息) /
 *   CommitAuthor(提交人) / CommitTime(提交时间) / Branch(分支) / BuildNodeId(编译节点) /
 *   AppVersionId(产物版本) / BuildStartedTime(编译开始) / BuildFinishedTime(编译完成) /
 *   DeployStartedTime(部署开始) / DeployFinishedTime(部署完成) / Steps(步骤) /
 *   UpdateUserId(更新者) / UpdateTime(更新时间) / UpdateIP(更新地址)
 * 已隐藏：Id / TraceId(TraceUrl) / TriggerSource / CreateUserId / CreateTime / CreateIP / Remark
 * 说明：Reprocess(重跑) 以右侧独立操作列呈现；Steps 为本表内链接列；追踪列为加分项（无 TraceUrl 降级显示 -）。
 */
type Col = { name: string; label: string; minWidth?: number };
const ALL_COLUMNS: Col[] = [
  { name: 'PipelineName', label: '流水线', minWidth: 110 },
  { name: 'Status', label: '状态', minWidth: 80 },
  { name: 'CommitId', label: '提交SHA', minWidth: 120 },
  { name: 'CommitMessage', label: '提交信息', minWidth: 160 },
  { name: 'CommitAuthor', label: '提交人', minWidth: 100 },
  { name: 'CommitTime', label: '提交时间', minWidth: 150 },
  { name: 'Branch', label: '分支', minWidth: 100 },
  { name: 'BuildNodeId', label: '编译节点', minWidth: 100 },
  { name: 'AppVersionId', label: '产物版本', minWidth: 100 },
  { name: 'BuildStartedTime', label: '编译开始', minWidth: 150 },
  { name: 'BuildFinishedTime', label: '编译完成', minWidth: 150 },
  { name: 'DeployStartedTime', label: '部署开始', minWidth: 150 },
  { name: 'DeployFinishedTime', label: '部署完成', minWidth: 150 },
  { name: 'Steps', label: '步骤', minWidth: 100 },
  { name: 'UpdateUserId', label: '更新者', minWidth: 100 },
  { name: 'UpdateTime', label: '更新时间', minWidth: 150 },
  { name: 'UpdateIP', label: '更新地址', minWidth: 120 },
];

/** 搜索：pipelineId / status / 时间范围 dtStart/dtEnd */
const pipelineId = ref<number | null>(null);
const statusFilter = ref<string>('');
const timeRange = ref<[string, string] | null>(null);

const loading = ref(false);
const rows = ref<Row[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);

const hasActiveFilter = computed(
  () => (pipelineId.value != null && pipelineId.value > 0) || statusFilter.value !== '' || timeRange.value != null,
);

/** 状态枚举中文映射（后端 PipelineStatus：Pending/Building/UploadSucceeded/Deploying/Success/Failed/Cancelled） */
const STATUS_LABELS: Record<number, string> = {
  0: '等待',
  1: '编译中',
  2: '上传成功',
  3: '部署中',
  4: '成功',
  5: '失败',
  6: '已取消',
};

function formatTime(v: unknown): string {
  if (v == null) return '';
  return String(v).replace('T', ' ').slice(0, 19);
}

function calcCost(row: Row): string {
  // 优先使用后端返回的 Cost 字段；否则用 BuildFinishedTime - BuildStartedTime 秒数
  if (row.Cost != null) return `${String(row.Cost)}`;
  const s = row.BuildStartedTime ? new Date(row.BuildStartedTime).getTime() : 0;
  const e = row.BuildFinishedTime ? new Date(row.BuildFinishedTime).getTime() : 0;
  if (s > 0 && e >= s) return String(Math.round((e - s) / 1000));
  return '';
}

function cellText(row: Row, key: string): string {
  const v = row[key];
  if (v == null) return '';
  if (key === 'Status') {
    const n = typeof v === 'number' ? v : Number(v);
    return STATUS_LABELS[n] ?? String(v);
  }
  if (key === 'BuildStartedTime' || key === 'StartTime') return formatTime(row.BuildStartedTime ?? row.StartTime);
  if (key === 'BuildFinishedTime' || key === 'EndTime') return formatTime(row.BuildFinishedTime ?? row.EndTime);
  if (key === 'Cost') return calcCost(row);
  if (key === 'CreateTime' || key === 'CommitTime' || key === 'DeployStartedTime' || key === 'DeployFinishedTime')
    return formatTime(v);
  return String(v);
}

/** 「步骤」链接：#/Deployment/AppPipelineStep?runId=<行Id> */
function stepsUrl(row: Row): string {
  return `#/Deployment/AppPipelineStep?runId=${row.Id}`;
}

function buildParams(): Record<string, unknown> {
  const p: Record<string, unknown> = {
    pageIndex: page.value - 1,
    pageSize: pageSize.value,
  };
  if (pipelineId.value != null && pipelineId.value > 0) p.pipelineId = pipelineId.value;
  if (statusFilter.value !== '') p.status = Number(statusFilter.value);
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
    console.error('[AppPipelineRun] GetList failed', e);
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
  pipelineId.value = null;
  statusFilter.value = '';
  timeRange.value = null;
  page.value = 1;
  fetchList();
}

function onPageChange(p: number) {
  page.value = p;
  fetchList();
}

/** 「重跑」：confirm -> GET /Deployment/AppPipelineRun/Reprocess?id=<行Id> -> ElMessage -> 刷新 */
async function onReprocess(row: Row) {
  if (row.Id == null) return;
  try {
    await ElMessageBox.confirm('确认基于本次运行重新触发流水线？', '重跑流水线', {
      type: 'warning',
      confirmButtonText: '确认',
      cancelButtonText: '取消',
    });
  } catch {
    return; // 用户取消
  }
  try {
    const res: any = await request({
      url: `/Deployment/AppPipelineRun/Reprocess`,
      method: 'get',
      params: { id: row.Id },
    });
    if (res && (res.code === 0 || res.code === 1 || res.code === 200 || res.success)) {
      ElMessage.success(res.message || '已重新触发流水线');
    } else {
      ElMessage.error(res?.message || '重跑失败');
    }
  } catch (e: any) {
    ElMessage.error(e?.response?.data?.message || e?.message || '重跑失败');
  } finally {
    await fetchList();
  }
}

onMounted(async () => {
  // 从 hash 读取 pipelineId（来自 AppPipeline 的「流水线历史」跳转）
  const m = location.hash.match(/[?&]pipelineId=(\d+)/);
  if (m) {
    const v = Number(m[1]);
    pipelineId.value = v > 0 ? v : null;
  }
  await fetchList();
});
</script>

<template>
  <AppPipelineRunDetail v-if="isDetail" />
  <div
    v-else
    class="list-page app-pipeline-run-page"
  >
    <div class="lp-header">
      <h2 class="lph-title">
        流水线运行
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
      <!-- 搜索栏：流水线编号 / 状态 / 时间范围 -->
      <div class="lp-card-header apr-search">
        <div class="apr-search-grid">
          <div class="apr-search-field">
            <label class="apr-search-label">流水线编号</label>
            <el-input
              v-model.number="pipelineId"
              type="number"
              placeholder="PipelineId"
              class="apr-search-input"
              clearable
            />
          </div>

          <div class="apr-search-field">
            <label class="apr-search-label">状态</label>
            <el-select
              v-model="statusFilter"
              clearable
              placeholder="全部"
              class="apr-search-input"
            >
              <el-option
                label="全部"
                value=""
              />
              <el-option
                label="等待"
                value="0"
              />
              <el-option
                label="编译中"
                value="1"
              />
              <el-option
                label="上传成功"
                value="2"
              />
              <el-option
                label="部署中"
                value="3"
              />
              <el-option
                label="成功"
                value="4"
              />
              <el-option
                label="失败"
                value="5"
              />
              <el-option
                label="已取消"
                value="6"
              />
            </el-select>
          </div>

          <div class="apr-search-field apr-search-field--wide">
            <label class="apr-search-label">时间</label>
            <el-date-picker
              v-model="timeRange"
              type="daterange"
              range-separator="~"
              start-placeholder="开始日期"
              end-placeholder="结束日期"
              value-format="YYYY-MM-DD"
              class="apr-search-input apr-search-datepicker"
            />
          </div>

          <div class="apr-search-actions">
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
              <!-- 步骤列：跳转到该运行的步骤记录列表 -->
              <el-link
                v-if="c.name === 'Steps' && row.Id != null"
                type="primary"
                :underline="false"
                :href="stepsUrl(row)"
                target="_self"
              >
                步骤
              </el-link>
              <span v-else-if="c.name === 'Steps'">-</span>
              <span v-else>{{ cellText(row, c.name) }}</span>
            </template>
          </el-table-column>

          <!-- TraceId 调用链链接（加分项：后端返回 TraceUrl 则渲染） -->
          <el-table-column
            label="追踪"
            min-width="70"
            align="center"
          >
            <template #default="{ row }">
              <el-link
                v-if="row.TraceUrl"
                type="primary"
                :underline="false"
                :href="row.TraceUrl"
                target="_blank"
              >
                追踪
              </el-link>
              <span v-else>-</span>
            </template>
          </el-table-column>

          <!-- 操作列：重跑（对应 MVC Reprocess action 列） / 详情 / 编辑 / 删除 -->
          <el-table-column
            label="重跑"
            min-width="180"
            fixed="right"
            align="center"
          >
            <template #default="{ row }">
              <div class="apr-ops">
                <el-button
                  size="small"
                  type="warning"
                  plain
                  @click="onReprocess(row)"
                >
                  重跑
                </el-button>
                <el-link
                  type="primary"
                  :underline="false"
                  class="apr-op"
                  @click="fetchList"
                >
                  详情
                </el-link>
                <el-link
                  type="primary"
                  :underline="false"
                  class="apr-op"
                  @click="fetchList"
                >
                  编辑
                </el-link>
                <el-link
                  type="danger"
                  :underline="false"
                  class="apr-op"
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
.app-pipeline-run-page {
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

.apr-search {
  .apr-search-grid {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 12px 16px;
    align-items: end;
  }

  .apr-search-field {
    display: flex;
    flex-direction: column;
    gap: 6px;
    min-width: 0;

    &--wide {
      grid-column: span 2;
    }
  }

  .apr-search-label {
    font-size: 12px;
    font-weight: 500;
    color: var(--el-text-color-secondary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .apr-search-input {
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

  .apr-search-datepicker {
    :deep(.el-range-editor) {
      width: 100%;
    }
  }

  .apr-search-actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    padding-bottom: 0;
  }
}

.act-table {
  width: 100%;
}

.apr-ops {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-wrap: wrap;
  gap: 8px;

  .apr-op {
    font-size: 13px;
  }
}

@media (max-width: 900px) {
  .apr-search .apr-search-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .apr-search .apr-search-grid {
    grid-template-columns: 1fr;
  }

  .apr-search .apr-search-field--wide {
    grid-column: span 1;
  }
}
</style>
