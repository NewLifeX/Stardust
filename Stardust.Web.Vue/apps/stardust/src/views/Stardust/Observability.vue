<script setup lang="ts">
/**
 * 可观测中心 Observability（第四阶段 · 方案 §13/§14/§15/§35）
 *
 * 一级结构：指标 / Trace / 日志 / 告警 四 Tab（§13.1）。
 *  - 指标：应用选择器 + 时间范围 + KPI 行（QPS/P99/错误率/在线率）+ 时间序列双图（§13.2-13.3）
 *  - Trace：搜索条 + 列表 + 详情 Drawer（时间轴/调用树/关系图 三视图，§13.4-13.5）
 *  - 日志：查询条（应用/节点/级别/时间/关键字/TraceId），无统一查询 API → 空状态「待接入」（§14.1）
 *  - 告警：统计行 + 筛选 + 列表 + Drawer 详情，无事件列表 API → 空状态「待接入」（§15.1-15.3）
 *
 * 上下文联动（§35）：watch stardustContext.revision → 重载当前 Tab；cubeAxios 拦截器自动注入 projectId/environment。
 * 样式纪律：颜色一律 --el-* 令牌 + Tailwind 布局，禁止自定义 token/硬编码色值；节点禁止裸缩写。
 */
import { ref, computed, onMounted, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { ElMessage } from 'element-plus';
import { request } from '@newlifex/cube-vue/core/utils/request';
import { stardustContext } from '@/stores/stardustContext';
import StatusPill from '@/layouts/StardustLayout/components/StatusPill.vue';
import KpiStat from '@/layouts/StardustLayout/components/KpiStat.vue';
import EmptyState from '@/layouts/StardustLayout/components/EmptyState.vue';
import EChart from '@/components/EChart.vue';
import * as m from './observability.model';
import ObservabilityMetricsGrid from '@/layouts/StardustLayout/components/ObservabilityMetricsGrid.vue';
import ObservabilityTraceSplit from '@/layouts/StardustLayout/components/ObservabilityTraceSplit.vue';
import ObservabilityLogStream from '@/layouts/StardustLayout/components/ObservabilityLogStream.vue';
import ObservabilityAlertBoard from '@/layouts/StardustLayout/components/ObservabilityAlertBoard.vue';

const route = useRoute();
const router = useRouter();

// ===== Tab 切换（与应用 Drawer 页签风格一致）=====
type TabKey = 'metrics' | 'trace' | 'logs' | 'alerts';
const tabs: { key: TabKey; label: string }[] = [
  { key: 'metrics', label: '指标' },
  { key: 'trace', label: 'Trace' },
  { key: 'logs', label: '日志' },
  { key: 'alerts', label: '告警' },
];
const tab = ref<TabKey>(((route.query.tab as TabKey) || 'metrics') as TabKey);

function switchTab(k: TabKey) {
  tab.value = k;
  // 同步 query（SideBar children 带 ?tab=xxx）
  void router.replace({ query: { ...route.query, tab: k } });
}

// 已在本页时点击侧栏子项 → query 变化但组件不重挂载，需同步 tab
watch(
  () => route.query.tab as TabKey | undefined,
  (k) => {
    if (k && k !== tab.value) tab.value = k;
  },
);

// ===== 应用选择器（指标 / Trace / 日志 共用）=====
const apps = ref<m.AppOption[]>([]);
const appMap = computed(() => new Map(apps.value.map((a) => [a.id, a])));
const selectedAppId = ref<number | null>(null);

// ===== 指标 Tab =====
const metricRows = ref<m.MinuteStatRow[]>([]);
const metricLoading = ref(false);
const timePresets = [
  { key: '1h', label: '近1小时' },
  { key: '6h', label: '近6小时' },
  { key: '24h', label: '近24小时' },
  { key: '7d', label: '近7天' },
];
const metricPreset = ref('1h');
const dimensionChips = [
  { key: 'app', label: '应用' },
  { key: 'service', label: '服务' },
  { key: 'instance', label: '实例' },
  { key: 'node', label: '节点' },
  { key: 'api', label: '接口' },
];
const activeDimension = ref('app');

const metricKpi = computed(() => m.computeMetricKpi(metricRows.value));
const requestChart = computed(() => m.buildRequestChart(metricRows.value));
const costChart = computed(() => m.buildCostChart(metricRows.value));
const metricEmpty = computed(() => !metricLoading.value && metricRows.value.length === 0);

async function loadMetrics() {
  metricLoading.value = true;
  try {
    metricRows.value = await m.fetchMinuteStats(request, selectedAppId.value ?? undefined);
  } catch (e: unknown) {
    ElMessage.error(e instanceof Error ? e.message : '加载指标失败');
    metricRows.value = [];
  } finally {
    metricLoading.value = false;
  }
}

// ===== Trace Tab =====
const traceRows = ref<m.TraceListRow[]>([]);
const traceLoading = ref(false);
const traceSearch = ref('');
const traceOnlyError = ref(false);

const filteredTraceRows = computed(() => {
  const kw = traceSearch.value.trim().toLowerCase();
  return traceRows.value.filter((r) => {
        if (selectedAppId.value != null && selectedAppId.value > 0 && r.appId !== selectedAppId.value) return false;
    if (traceOnlyError.value && r.ok) return false;
    if (kw && !r.traceId.toLowerCase().includes(kw) && !r.endpoint.toLowerCase().includes(kw) && !r.appName.toLowerCase().includes(kw)) return false;
    return true;
  });
});

async function loadTraces() {
  traceLoading.value = true;
  try {
    const items = await m.fetchTraceItems(request);
    traceRows.value = m.toTraceListRows(items, appMap.value);
  } catch (e: unknown) {
    ElMessage.error(e instanceof Error ? e.message : '加载 Trace 列表失败');
    traceRows.value = [];
  } finally {
    traceLoading.value = false;
  }
}

// Trace 详情 Drawer
const traceDrawerVisible = ref(false);
const traceSpans = ref<m.SpanRow[]>([]);
const traceSpansLoading = ref(false);
const currentTrace = ref<m.TraceListRow | null>(null);
const spanView = ref<'timeline' | 'tree' | 'graph'>('timeline');
const spanTab = ref<'logs' | 'tags' | 'errors'>('logs');

const waterfall = computed(() => m.buildWaterfall(traceSpans.value));
const callTree = computed(() => m.buildCallTree(traceSpans.value));
const spanSummary = computed(() => m.summarizeSpans(traceSpans.value));
const errorSpans = computed(() => traceSpans.value.filter((s) => s.success === false || (s.error && s.error.length > 0)));
const tagSpans = computed(() => traceSpans.value.filter((s) => s.tag && String(s.tag).length > 0));

async function openTrace(row: m.TraceListRow) {
  currentTrace.value = row;
  traceDrawerVisible.value = true;
  spanView.value = 'timeline';
  spanTab.value = 'logs';
  traceSpans.value = [];
  if (!row.traceId) return;
  traceSpansLoading.value = true;
  try {
    traceSpans.value = await m.fetchTraceSpans(request, row.traceId);
  } catch (e: unknown) {
    ElMessage.error(e instanceof Error ? e.message : '加载 Trace 详情失败');
  } finally {
    traceSpansLoading.value = false;
  }
}

// ===== 日志 Tab（§14.1：查询条 UI 完整，列表待接入）=====
const logApp = ref<number | null>(null);
const logLevel = ref<string[]>(['INFO', 'WARN', 'ERROR']);
const logKeyword = ref('');
const logTraceId = ref('');
const levelOptions = ['DEBUG', 'INFO', 'WARN', 'ERROR'];
const levelColor: Record<string, string> = {
  DEBUG: 'var(--el-color-info)',
  INFO: 'var(--el-color-primary)',
  WARN: 'var(--el-color-warning)',
  ERROR: 'var(--el-color-danger)',
};

// ===== 告警 Tab（§15：统计 0，列表待接入）=====
const alertRows = ref<m.AlertRow[]>([]);
const alertSummary = computed(() => m.summarizeAlerts(alertRows.value));
const alertFilter = ref<'all' | m.AlertStatus>('all');
const alertLevelFilter = ref<'all' | m.AlertLevel>('all');
const filteredAlerts = computed(() =>
  alertRows.value.filter((r) => (alertFilter.value === 'all' || r.status === alertFilter.value) && (alertLevelFilter.value === 'all' || r.level === alertLevelFilter.value)),
);
const alertDrawerVisible = ref(false);
const currentAlert = ref<m.AlertRow | null>(null);

function openAlert(row: m.AlertRow) {
  currentAlert.value = row;
  alertDrawerVisible.value = true;
}

// ===== 上下文联动（§5.3 / §35）：项目/环境切换后重载当前 Tab =====
function reloadCurrent() {
  if (tab.value === 'metrics') void loadMetrics();
  else if (tab.value === 'trace') void loadTraces();
  // 日志/告警 无 API，无需重载
}

onMounted(async () => {
  try {
    apps.value = await m.fetchAppOptions(request);
  } catch (e: unknown) {
    ElMessage.error(e instanceof Error ? e.message : '加载应用列表失败');
  }
  await reloadCurrent();
});

watch(
  () => stardustContext.revision,
  () => reloadCurrent(),
);
// 切 Tab 时若对应数据未加载则加载
watch(tab, (k) => {
  if (k === 'metrics' && metricRows.value.length === 0) void loadMetrics();
  if (k === 'trace' && traceRows.value.length === 0) void loadTraces();
});
// 切换应用过滤 → 重载指标
watch(selectedAppId, () => {
  if (tab.value === 'metrics') void loadMetrics();
});

/** A6：空态图标（Lucide 线性 path，stroke 1.5） */
const ICON_EMPTY = 'M3 7h18M3 12h18M3 17h10';

</script>

<template>
  <div class="flex h-full flex-col gap-5">
    <!-- 页头（§13.1） -->
    <div class="flex flex-col gap-1">
      <h1 class="text-xl font-semibold text-[var(--el-text-color-primary)]">
        可观测中心
      </h1>
      <p class="text-xs text-[var(--el-text-color-regular)]">
        指标 · Trace · 日志 · 告警 — 从异常到定位的排障主路径
      </p>
    </div>

    <!-- Tab 栏（与应用 Drawer 页签风格一致） -->
    <div class="sd-tabs">
      <button
        v-for="t in tabs"
        :key="t.key"
        class="sd-tab"
        :class="{ 'sd-tab-active': tab === t.key }"
        @click="switchTab(t.key)"
      >
        {{ t.label }}
      </button>
    </div>

    <div
      class="flex flex-wrap items-center gap-3"
      data-spec="obs-app-bar"
    >
      <el-select
        v-model="selectedAppId"
        clearable
        placeholder="应用: 全部"
        size="small"
        style="width: 200px"
      >
        <el-option
          v-for="a in apps"
          :key="a.id"
          :label="a.displayName || a.name"
          :value="a.id"
        />
      </el-select>
      <span class="text-xs text-[var(--el-text-color-secondary)]">Metrics · Traces · Logs · Alerts · 全链路排查</span>
    </div>

    <!-- ===== 指标 Tab（§13.2-13.3）===== -->
    <div
      v-if="tab === 'metrics'"
      class="flex flex-col gap-4"
    >
      <ObservabilityMetricsGrid :app-id="selectedAppId" />
    </div>

    <!-- ===== Trace Tab（§13.4-13.5）===== -->
    <div
      v-else-if="tab === 'trace'"
      class="flex flex-col gap-4"
    >
      <ObservabilityTraceSplit :app-id="selectedAppId" />
    </div>

    <!-- ===== 日志 Tab（§14.1：查询条完整，列表待接入）===== -->
    <div
      v-else-if="tab === 'logs'"
      class="flex flex-col gap-4"
    >
      <ObservabilityLogStream :app-id="selectedAppId" />
    </div>

    <!-- ===== 告警 Tab（§15：统计 0，列表待接入）===== -->
    <div
      v-else
      class="flex flex-col gap-4"
    >
      <ObservabilityAlertBoard />
    </div>

    <!-- ===== Trace 详情 Drawer（§13.5）===== -->
    <el-drawer
      v-model="traceDrawerVisible"
      :title="currentTrace?.endpoint || 'Trace 详情'"
      size="720px"
      destroy-on-close
      class="stardust-drawer"
    >
      <template #header>
        <div class="flex min-w-0 flex-1 items-center justify-between gap-3">
          <div class="flex min-w-0 flex-col">
            <span class="truncate text-lg font-bold text-[var(--el-text-color-primary)]">
              {{ currentTrace?.endpoint || 'Trace 详情' }}
            </span>
            <span class="truncate font-mono text-xs text-[var(--el-text-color-secondary)]">
              {{ currentTrace?.traceId || '—' }}
            </span>
          </div>
          <button
            class="sd-close"
            aria-label="关闭"
            @click="traceDrawerVisible = false"
          >
            ×
          </button>
        </div>
      </template>

      <div
        v-loading="traceSpansLoading"
        class="flex flex-col gap-4"
      >
        <!-- 头部摘要：总耗时 / Span 数 / 错误数 -->
        <div class="grid grid-cols-3 gap-3">
          <div class="sd-stat-box">
            <div class="sd-stat-label">
              总耗时
            </div>
            <div class="sd-stat-value">
              {{ spanSummary.totalCost }}ms
            </div>
          </div>
          <div class="sd-stat-box">
            <div class="sd-stat-label">
              Span 数
            </div>
            <div class="sd-stat-value">
              {{ spanSummary.spanCount }}
            </div>
          </div>
          <div class="sd-stat-box">
            <div class="sd-stat-label">
              错误数
            </div>
            <div
              class="sd-stat-value"
              :style="{ color: spanSummary.errorCount > 0 ? 'var(--el-color-danger)' : 'var(--el-color-success)' }"
            >
              {{ spanSummary.errorCount }}
            </div>
          </div>
        </div>

        <!-- 视图切换：时间轴 / 调用树 / 关系图 -->
        <div class="sd-seg">
          <button
            class="sd-seg-btn"
            :class="{ 'sd-seg-active': spanView === 'timeline' }"
            @click="spanView = 'timeline'"
          >
            时间轴
          </button>
          <button
            class="sd-seg-btn"
            :class="{ 'sd-seg-active': spanView === 'tree' }"
            @click="spanView = 'tree'"
          >
            调用树
          </button>
          <button
            class="sd-seg-btn"
            :class="{ 'sd-seg-active': spanView === 'graph' }"
            @click="spanView = 'graph'"
          >
            关系图
          </button>
        </div>

        <!-- 时间轴：服务耗时分解瀑布条（节点带中文说明+耗时，禁止裸缩写） -->
        <div
          v-if="spanView === 'timeline'"
          class="sd-waterfall"
        >
          <div
            v-for="n in waterfall"
            :key="n.key"
            class="sd-waterfall-row"
            :style="{ paddingLeft: `${8 + n.depth * 16}px` }"
          >
            <div class="sd-waterfall-label">
              {{ n.name }}
            </div>
            <div class="sd-waterfall-track">
              <div
                class="sd-waterfall-bar"
                :style="{
                  left: `${n.startOffset}%`,
                  width: `${n.widthPct}%`,
                  backgroundColor: n.ok ? 'var(--el-color-primary)' : 'var(--el-color-danger)',
                }"
              />
            </div>
          </div>
          <EmptyState
            :icon="ICON_EMPTY"
            title="暂无 Span 数据"
          />
        </div>

        <!-- 调用树 -->
        <div
          v-else-if="spanView === 'tree'"
          class="sd-calltree"
        >
          <el-tree
            v-if="callTree.length"
            :data="callTree"
            default-expand-all
            node-key="key"
            :expand-on-click-node="false"
          >
            <template #default="{ data }">
              <span class="inline-flex items-center gap-2 text-[13px]">
                <span :style="{ color: data.ok ? 'var(--el-text-color-primary)' : 'var(--el-color-danger)' }">{{ data.name }}</span>
                <span class="text-xs text-[var(--el-text-color-secondary)]">{{ data.appName }}</span>
              </span>
            </template>
          </el-tree>
          <EmptyState
            :icon="ICON_EMPTY"
            title="暂无调用树数据"
          />
        </div>

        <!-- 关系图：外链现有 graph 页 -->
        <div
          v-else
          class="sd-empty-card"
        >
          <EmptyState
            :icon="ICON_EMPTY"
            title="`关系图待接入 — 可跳转 /Trace/Graph?traceId=${currentTrace.traceId}`"
          />
          <EmptyState
            :icon="ICON_EMPTY"
            title="关系图待接入"
          />
        </div>

        <!-- 日志 / 标签 / 异常 子页签 -->
        <div class="sd-subtabs">
          <button
            class="sd-subtab"
            :class="{ 'sd-subtab-active': spanTab === 'logs' }"
            @click="spanTab = 'logs'"
          >
            日志
          </button>
          <button
            class="sd-subtab"
            :class="{ 'sd-subtab-active': spanTab === 'tags' }"
            @click="spanTab = 'tags'"
          >
            标签
          </button>
          <button
            class="sd-subtab"
            :class="{ 'sd-subtab-active': spanTab === 'errors' }"
            @click="spanTab = 'errors'"
          >
            异常
          </button>
        </div>

        <div v-if="spanTab === 'logs'">
          <EmptyState
            :icon="ICON_EMPTY"
            title="关联日志待接入"
          />
        </div>
        <div
          v-else-if="spanTab === 'tags'"
          class="flex flex-col gap-2"
        >
          <div
            v-for="(s, i) in tagSpans"
            :key="i"
            class="sd-tag-row"
          >
            <span class="text-[var(--el-text-color-primary)]">{{ s.name || s.appName }}</span>
            <span class="font-mono text-[var(--el-color-secondary)]">{{ s.tag }}</span>
          </div>
          <EmptyState
            :icon="ICON_EMPTY"
            title="暂无标签"
          />
        </div>
        <div
          v-else
          class="flex flex-col gap-2"
        >
          <div
            v-for="(s, i) in errorSpans"
            :key="i"
            class="sd-error-row"
          >
            <span class="text-[var(--el-color-danger)]">{{ s.name || s.appName }}</span>
            <span class="text-[var(--el-text-color-regular)]">{{ s.error }}</span>
          </div>
          <EmptyState
            :icon="ICON_EMPTY"
            title="暂无异常"
          />
        </div>
      </div>
    </el-drawer>

    <!-- ===== 告警详情 Drawer（§15.3：结构保留，无数据时为空）===== -->
    <el-drawer
      v-model="alertDrawerVisible"
      title="告警详情"
      size="560px"
      destroy-on-close
      class="stardust-drawer"
    >
      <template #header>
        <div class="flex min-w-0 flex-1 items-center justify-between gap-3">
          <span class="truncate text-lg font-bold text-[var(--el-text-color-primary)]">
            {{ currentAlert?.title || '告警详情' }}
          </span>
          <button
            class="sd-close"
            aria-label="关闭"
            @click="alertDrawerVisible = false"
          >
            ×
          </button>
        </div>
      </template>
      <div
        v-if="currentAlert"
        class="flex flex-col gap-4"
      >
        <div class="sd-overview-grid">
          <div class="sd-overview-item">
            <span class="sd-overview-label">对象</span>
            <span class="sd-overview-value">{{ currentAlert.target }}</span>
          </div>
          <div class="sd-overview-item">
            <span class="sd-overview-label">当前值</span>
            <span class="sd-overview-value">{{ currentAlert.currentValue }}</span>
          </div>
          <div class="sd-overview-item">
            <span class="sd-overview-label">阈值</span>
            <span class="sd-overview-value">{{ currentAlert.threshold }}</span>
          </div>
          <div class="sd-overview-item">
            <span class="sd-overview-label">持续时长</span>
            <span class="sd-overview-value">{{ currentAlert.duration }}</span>
          </div>
          <div class="sd-overview-item">
            <span class="sd-overview-label">影响实例数</span>
            <span class="sd-overview-value">{{ currentAlert.instanceCount }}</span>
          </div>
          <div class="sd-overview-item">
            <span class="sd-overview-label">关联 Trace 数</span>
            <span class="sd-overview-value">{{ currentAlert.traceCount }}</span>
          </div>
        </div>
        <div class="flex items-center gap-2">
          <el-button
            type="primary"
            size="small"
          >
            确认
          </el-button>
          <el-button
            size="small"
          >
            静默
          </el-button>
          <el-button
            size="small"
          >
            查看应用
          </el-button>
          <el-button
            size="small"
          >
            查看 Trace
          </el-button>
        </div>
      </div>
      <EmptyState
        :icon="ICON_EMPTY"
        title="告警中心待接入"
      />
    </el-drawer>
  </div>
</template>

<style scoped>
/* 页签（与应用 Drawer 一致） */
.sd-tabs {
  position: relative;
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  border-bottom: 1px solid var(--el-border-color-light);
}
.sd-tab {
  padding: 8px 14px;
  font-size: 13px;
  line-height: 1.4;
  color: var(--el-text-color-regular);
  transition: color 0.2s;
}
.sd-tab:hover {
  color: var(--el-text-color-primary);
}
.sd-tab-active {
  color: var(--el-color-primary);
  font-weight: 600;
}
.sd-tab-active::after {
  content: '';
  position: absolute;
  right: 14px;
  bottom: -1px;
  left: 14px;
  height: 2px;
  border-radius: 2px 2px 0 0;
  background: var(--el-color-primary);
}

/* 表格玻璃态 */
.sd-table {
  --el-table-border-color: var(--el-border-color-lighter);
  --el-table-header-bg-color: color-mix(in srgb, var(--el-bg-color-overlay) 60%, transparent);
  --el-table-tr-bg-color: color-mix(in srgb, var(--el-bg-color) 55%, transparent);
  --el-table-row-hover-bg-color: color-mix(in srgb, var(--el-color-primary) 10%, transparent);
  --el-table-bg-color: transparent;
  border-radius: 12px;
  overflow: hidden;
}
.sd-table :deep(.el-table__inner-wrapper::before) {
  display: none;
}
.sd-table :deep(th.el-table__cell) {
  background: color-mix(in srgb, var(--el-bg-color-overlay) 60%, transparent);
  color: var(--el-text-color-secondary);
  font-weight: 600;
}
.sd-table :deep(td.el-table__cell) {
  border-bottom-color: var(--el-border-color-lighter);
  cursor: pointer;
}

/* 图表卡片 */
.sd-chart-card {
  border: 1px solid var(--el-border-color-light);
  border-radius: 12px;
  padding: 14px 16px;
  background: color-mix(in srgb, var(--el-bg-color) 70%, transparent);
}
.sd-chart-title {
  margin-bottom: 8px;
  font-size: 13px;
  font-weight: 600;
  color: var(--el-text-color-regular);
}

/* 空状态卡片 */
.sd-empty-card {
  border: 1px dashed var(--el-border-color);
  border-radius: 12px;
  padding: 16px;
  background: color-mix(in srgb, var(--el-bg-color) 50%, transparent);
}

/* Drawer */
.stardust-drawer :deep(.el-drawer__header) {
  margin-bottom: 0;
  padding: 16px 20px;
  border-bottom: 1px solid var(--el-border-color-light);
}
.stardust-drawer :deep(.el-drawer__body) {
  padding: 20px;
  background: color-mix(in srgb, var(--el-bg-color) 92%, transparent);
}
.stardust-drawer :deep(.el-drawer__close-btn) {
  display: none;
}
.sd-close {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border-radius: 50%;
  font-size: 18px;
  line-height: 1;
  color: var(--el-text-color-regular);
  transition:
    background-color 0.2s,
    color 0.2s;
}
.sd-close:hover {
  background: var(--el-fill-color-light);
  color: var(--el-text-color-primary);
}

/* 摘要统计盒 */
.sd-stat-box {
  border: 1px solid var(--el-border-color-light);
  border-radius: 10px;
  padding: 12px 14px;
  background: color-mix(in srgb, var(--el-bg-color) 70%, transparent);
}
.sd-stat-label {
  font-size: 11px;
  color: var(--el-text-color-secondary);
}
.sd-stat-value {
  margin-top: 4px;
  font-size: 20px;
  font-weight: 700;
  color: var(--el-text-color-primary);
}

/* 视图切换分段 */
.sd-seg {
  display: inline-flex;
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
  overflow: hidden;
}
.sd-seg-btn {
  padding: 6px 14px;
  font-size: 12px;
  color: var(--el-text-color-regular);
  background: transparent;
  transition:
    background-color 0.2s,
    color 0.2s;
}
.sd-seg-btn + .sd-seg-btn {
  border-left: 1px solid var(--el-border-color);
}
.sd-seg-active {
  background: color-mix(in srgb, var(--el-color-primary) 20%, transparent);
  color: var(--el-color-white);
}

/* 瀑布时间轴 */
.sd-waterfall {
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.sd-waterfall-row {
  display: flex;
  align-items: center;
  gap: 10px;
}
.sd-waterfall-label {
  width: 220px;
  flex-shrink: 0;
  font-size: 12px;
  color: var(--el-text-color-regular);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.sd-waterfall-track {
  position: relative;
  flex: 1;
  height: 14px;
  border-radius: 7px;
  background: var(--el-fill-color-light);
}
.sd-waterfall-bar {
  position: absolute;
  top: 0;
  height: 14px;
  border-radius: 7px;
  min-width: 2px;
}

/* 调用树 */
.sd-calltree {
  border: 1px solid var(--el-border-color-light);
  border-radius: 10px;
  padding: 10px 12px;
  background: color-mix(in srgb, var(--el-bg-color) 60%, transparent);
}

/* 子页签 */
.sd-subtabs {
  display: flex;
  gap: 4px;
  border-bottom: 1px solid var(--el-border-color-light);
}
.sd-subtab {
  padding: 6px 12px;
  font-size: 12px;
  color: var(--el-text-color-regular);
}
.sd-subtab-active {
  color: var(--el-color-primary);
  font-weight: 600;
  border-bottom: 2px solid var(--el-color-primary);
}

/* 标签/异常行 */
.sd-tag-row,
.sd-error-row {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  padding: 8px 10px;
  border-radius: 8px;
  background: color-mix(in srgb, var(--el-text-color-primary) 4%, transparent);
  font-size: 12px;
}
.sd-error-row {
  background: color-mix(in srgb, var(--el-color-danger) 8%, transparent);
}

/* 概览网格（告警详情） */
.sd-overview-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 14px 12px;
}
.sd-overview-item {
  display: flex;
  flex-direction: column;
  gap: 5px;
}
.sd-overview-label {
  font-size: 11px;
  color: var(--el-text-color-secondary);
}
.sd-overview-value {
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}
</style>
