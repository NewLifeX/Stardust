<script setup lang="ts">
/**
 * Deployment/AppPipelineRun/Detail — 流水线运行详情（隐藏页自定义视图）
 *
 * 控制器 [Menu(40,false)] 不在 MenuTree，框架 DefaultEntity 兜底：
 * 当 URL 形如 #/Deployment/AppPipelineRun/<id> 且无菜单匹配时，
 * 尝试渲染本视图。
 *
 * 自定义逻辑还原：
 *  - 调 usePageApi('Deployment','AppPipelineRun').getDetail(id) 读取单条运行记录
 *  - el-descriptions 展示：编号/流水线/状态/开始结束时间/耗时/触发源/错误信息/创建时间等
 *  - 状态轮询：当 Status 为「运行中」0/1/2/3 时，每 4s getDetail 刷新直到结束
 *  - 返回列表链接 -> #/Deployment/AppPipelineRun
 */
import { ref, computed, onMounted, onUnmounted, watchEffect } from 'vue';
import { useRoute } from 'vue-router';
import { ElMessage } from 'element-plus';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';

const route = useRoute();
const api = usePageApi('Deployment', 'AppPipelineRun');

interface RunDetail {
  Id?: number;
  PipelineId?: number;
  PipelineName?: string;
  Status?: number;
  TriggerSource?: string;
  CommitId?: string;
  CommitMessage?: string;
  CommitAuthor?: string;
  CommitTime?: string;
  Branch?: string;
  BuildNodeId?: number;
  AppVersionId?: number;
  BuildStartedTime?: string;
  BuildFinishedTime?: string;
  DeployStartedTime?: string;
  DeployFinishedTime?: string;
  StartTime?: string;
  EndTime?: string;
  Cost?: number;
  TraceId?: string;
  Remark?: string;
  CreateTime?: string;
  UpdateTime?: string;
  [k: string]: unknown;
}

const runId = computed<number>(() => {
  // 优先取三级路由 params.id；fallback 时取 query.id
  const p = Number((route.params as any).id ?? 0);
  if (p > 0) return p;
  const q = Number((route.query as any).id ?? 0);
  return q > 0 ? q : 0;
});

const loading = ref(false);
const detail = ref<RunDetail | null>(null);
const notFound = ref(false);
const errorText = ref('');

let pollTimer: ReturnType<typeof setInterval> | null = null;

/** PipelineStatus：Pending/Building/UploadSucceeded/Deploying = 运行中 */
const RUNNING_STATUS = new Set([0, 1, 2, 3]);

const statusLabel = computed(() => {
  const n = Number(detail.value?.Status ?? -1);
  if (Number.isNaN(n) || n < 0) return '-';
  const map: Record<number, string> = {
    0: '等待',
    1: '编译中',
    2: '上传成功',
    3: '部署中',
    4: '成功',
    5: '失败',
    6: '已取消',
  };
  return map[n] ?? String(n);
});

const isRunning = computed(() => RUNNING_STATUS.has(Number(detail.value?.Status ?? -1)));

function formatTime(v: unknown): string {
  if (v == null) return '';
  return String(v).replace('T', ' ').slice(0, 19);
}

function calcCost(): string {
  const row = detail.value;
  if (!row) return '';
  if (row.Cost != null) return `${String(row.Cost)}`;
  const s = row.StartTime ?? row.BuildStartedTime;
  const e = row.EndTime ?? row.BuildFinishedTime;
  if (s && e) {
    const ms = new Date(e).getTime() - new Date(s).getTime();
    if (ms > 0) return String(Math.round(ms / 1000));
  }
  return '';
}

async function fetchDetail(silent = false) {
  const id = runId.value;
  if (id <= 0) {
    notFound.value = true;
    return;
  }
  if (!silent) loading.value = true;
  try {
    const res: any = await api.getDetail<RunDetail>(id);
    detail.value = res?.data ?? res ?? null;
    notFound.value = !detail.value;
    errorText.value = '';
  } catch (e: any) {
    console.error('[AppPipelineRunDetail] getDetail failed', e);
    detail.value = null;
    notFound.value = true;
    errorText.value = e?.response?.data?.message || e?.message || '加载详情失败';
    if (!silent) ElMessage.error(errorText.value);
  } finally {
    if (!silent) loading.value = false;
  }
}

function stopPolling() {
  if (pollTimer) {
    clearInterval(pollTimer);
    pollTimer = null;
  }
}

function startPolling() {
  stopPolling();
  pollTimer = setInterval(() => {
    if (runId.value > 0) fetchDetail(true);
  }, 4000);
}

onMounted(() => {
  fetchDetail();
});

onUnmounted(() => {
  stopPolling();
});

// 轮询生命周期：仅在运行中状态启用；数据加载后/状态变化时重新判断
watchEffect(() => {
  if (isRunning.value && runId.value > 0) {
    startPolling();
  } else {
    stopPolling();
  }
});
</script>

<template>
  <div
    v-loading="loading"
    class="detail-page app-pipeline-run-detail"
  >
    <div class="detail-header">
      <div class="detail-title-wrap">
        <h2 class="detail-title">
          流水线运行详情
        </h2>
        <span
          v-if="detail?.Id != null"
          class="detail-subtitle"
        >运行编号 #{{ detail.Id }}</span>
      </div>
      <div class="detail-toolbar">
        <a
          class="el-link el-link--primary"
          href="#/Deployment/AppPipelineRun"
        >
          <el-icon><ArrowLeft /></el-icon>
          <span>返回列表</span>
        </a>
      </div>
    </div>

    <div class="detail-card">
      <el-empty
        v-if="notFound"
        description="运行记录不存在或已被删除"
      />

      <template v-else-if="detail">
        <el-descriptions
          :column="2"
          border
        >
          <el-descriptions-item label="运行编号">
            {{ detail.Id ?? '-' }}
          </el-descriptions-item>
          <el-descriptions-item label="所属流水线">
            <span v-if="detail.PipelineName">{{ detail.PipelineName }}（{{ detail.PipelineId }}）</span>
            <span v-else>{{ detail.PipelineId ?? '-' }}</span>
          </el-descriptions-item>

          <el-descriptions-item label="状态">
            <el-tag
              :type="isRunning ? 'warning' : detail.Status === 4 ? 'success' : detail.Status === 5 ? 'danger' : 'info'"
              size="small"
            >
              {{ statusLabel }}
              <span
                v-if="isRunning"
                class="poll-hint"
              >（轮询中）</span>
            </el-tag>
          </el-descriptions-item>

          <el-descriptions-item label="触发源">
            {{ detail.TriggerSource ?? '-' }}
          </el-descriptions-item>

          <el-descriptions-item label="分支">
            {{ detail.Branch ?? '-' }}
          </el-descriptions-item>

          <el-descriptions-item label="提交SHA">
            {{ detail.CommitId ?? '-' }}
          </el-descriptions-item>

          <el-descriptions-item label="提交信息">
            {{ detail.CommitMessage ?? '-' }}
          </el-descriptions-item>

          <el-descriptions-item label="提交人">
            {{ detail.CommitAuthor ?? '-' }}
          </el-descriptions-item>

          <el-descriptions-item label="编译开始">
            {{ formatTime(detail.BuildStartedTime) }}
          </el-descriptions-item>
          <el-descriptions-item label="编译完成">
            {{ formatTime(detail.BuildFinishedTime) }}
          </el-descriptions-item>

          <el-descriptions-item label="部署开始">
            {{ formatTime(detail.DeployStartedTime) }}
          </el-descriptions-item>
          <el-descriptions-item label="部署完成">
            {{ formatTime(detail.DeployFinishedTime) }}
          </el-descriptions-item>

          <el-descriptions-item label="耗时（秒）">
            {{ calcCost() || '-' }}
          </el-descriptions-item>

          <el-descriptions-item label="产物版本">
            {{ detail.AppVersionId ?? '-' }}
          </el-descriptions-item>

          <el-descriptions-item label="编译节点">
            {{ detail.BuildNodeId ?? '-' }}
          </el-descriptions-item>

          <el-descriptions-item label="追踪TraceId">
            {{ detail.TraceId ?? '-' }}
          </el-descriptions-item>

          <el-descriptions-item
            label="备注/错误信息"
            :span="2"
          >
            {{ detail.Remark ?? '-' }}
          </el-descriptions-item>

          <el-descriptions-item label="创建时间">
            {{ formatTime(detail.CreateTime) }}
          </el-descriptions-item>
          <el-descriptions-item label="更新时间">
            {{ formatTime(detail.UpdateTime) }}
          </el-descriptions-item>
        </el-descriptions>
      </template>
    </div>
  </div>
</template>

<style lang="scss" scoped>
.app-pipeline-run-detail {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  padding: var(--el-main-padding, 24px);

  .detail-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 16px;

    .detail-title-wrap {
      display: flex;
      align-items: baseline;
      gap: 12px;
    }

    .detail-title {
      margin: 0;
      font-size: 18px;
      font-weight: 600;
      color: var(--el-text-color-primary);
    }

    .detail-subtitle {
      font-size: 13px;
      color: var(--el-text-color-secondary);
    }

    .detail-toolbar {
      display: flex;
      align-items: center;
      gap: 8px;

      .el-link {
        display: inline-flex;
        align-items: center;
        gap: 4px;
      }
    }
  }

  .detail-card {
    flex: 1;
    padding: 20px;
    background: var(--el-bg-color-overlay);
    border: 1px solid var(--el-border-color-light);
    border-radius: var(--el-border-radius-base);
    box-shadow: var(--el-box-shadow-light);
    overflow: auto;
  }

  .poll-hint {
    margin-left: 4px;
    font-size: 12px;
    opacity: 0.8;
  }
}
</style>
