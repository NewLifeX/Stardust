<script setup lang="ts">
// 星尘控制台 应用管理：表格列表 + 抽屉七页签（概览/实例/服务/配置/发布/可观测/审计）；fetch 编排与聚合在 appsModel，脚本仅 UI 状态；样式仅 --el-* 令牌
import { ref, computed, onMounted, onBeforeUnmount, watch } from 'vue';
import { useRouter } from 'vue-router';
import { ElMessage, ElMessageBox } from 'element-plus';
import { request } from '@newlifex/cube-vue/core/utils/request';
import { stardustContext } from '@/stores/stardustContext';
import StatusPill from '@/layouts/StardustLayout/components/StatusPill.vue';
import KpiStat from '@/layouts/StardustLayout/components/KpiStat.vue';
import FilterChips from '@/layouts/StardustLayout/components/FilterChips.vue';
import EmptyState from '@/layouts/StardustLayout/components/EmptyState.vue';
import RefreshDot from '@/layouts/StardustLayout/components/RefreshDot.vue';
import * as m from './appsModel';
const router = useRouter();
const list = ref<m.AppAggRow[]>([]), filter = ref<'all' | m.AppStatus>('all'), keyword = ref('');
const current = ref<m.AppAggRow | null>(null), drawerVisible = ref(false), tab = ref('overview'), configMode = ref<'table' | 'text'>('table');
const onlineRows = ref<m.OnlineRow[]>([]), detail = ref<m.AppDetail & { history: m.HistoryRow[] }>({ config: [], runs: [], stats: [], services: [], history: [], instances: 0 });
/** P0-1: 实例 Inspector 展开索引（null = 全部收起） */
const expandedInstance = ref<number | null>(null);
let timer = 0;
const stats = computed(() => m.aggregateKpis(list.value)), filtered = computed(() => m.filterByKeyword(m.filterByStatus(list.value, filter.value), keyword.value));
const instances = computed(() => m.instancesRows(onlineRows.value, current.value?.name ?? '')), services = computed(() => m.servicesGroups(detail.value.services));
const configKvs = computed(() => m.configKv(detail.value.config)), configTxt = computed(() => m.configText(detail.value.config));
const histories = computed(() => m.historyRows(detail.value.history));
const steps = computed(() => m.pipelineSteps(detail.value.runs[0] ?? {})), monitor = computed(() => m.monitorSummary(detail.value.stats));
/** P1-2: 概览摘要（实例健康/服务依赖/最近发布/最近告警） */
const overviewSummary = computed(() => m.overviewSummary(detail.value.runs, detail.value.services, instances.value.length));
/** P1-2: 最近事件时间线（部署/配置变更） */
const recentEvents = computed(() => m.recentEvents(detail.value.runs, detail.value.history));
async function showDetail(row: m.AppAggRow) {
  current.value = row; drawerVisible.value = true; tab.value = 'overview'; expandedInstance.value = null;
  try {
    const d = await m.fetchAppDetail(request, row.name, row.id);
    detail.value = { ...d, history: await m.fetchConfigHistory(request, d.config[0]?.id) };
  } catch (e: unknown) { ElMessage.error(e instanceof Error ? e.message : '加载详情失败'); }
}
async function doDiff(h: m.HistoryRow) {
  if (!h.id) return;
  try { const k = (await m.configDiff(request, h.id)).join('、'); await ElMessageBox.alert(k || '无差异', '配置差异', { confirmButtonText: '知道了' }); }
  catch (e: unknown) { ElMessage.error(e instanceof Error ? e.message : '对比失败'); }
}
async function doRollback(h: m.HistoryRow) {
  if (!h.id) return;
  try { await ElMessageBox.confirm(`确认回滚到「${h.action || '发布'} ${h.time || '-'}」版本？`, '确认回滚', { type: 'warning' }); } catch { return; }
  try { ElMessage.success((await m.configRollback(request, h.id)) || '已回滚到此版本'); if (current.value) showDetail(current.value); }
  catch (e: unknown) { ElMessage.error(e instanceof Error ? e.message : '回滚失败'); }
}
async function load() {
  try {
    const r = await m.fetchAppList(request);
    onlineRows.value = r.online; list.value = r.rows;
  } catch (e: unknown) { ElMessage.error(e instanceof Error ? e.message : '加载应用失败'); }
}
onMounted(() => {
  load();
  timer = window.setInterval(() => {
    if (drawerVisible.value && current.value) m.fetchMinuteStats(request, current.value.name, current.value.id).then((s) => (detail.value = { ...detail.value, stats: s })).catch(() => undefined);
  }, 15000);
});
onBeforeUnmount(() => { window.clearInterval(timer); stopRunPolling(); });

/** 方案 §5.3：项目 / 环境切换后自动刷新当前页（清理失效的局部筛选：关闭详情抽屉、重置过滤条件） */
watch(
  () => stardustContext.revision,
  () => {
    drawerVisible.value = false;
    current.value = null;
    filter.value = 'all';
    keyword.value = '';
    void load();
  },
);

/** 更新时间格式化（YYYY-MM-DD HH:mm，空值显示 —） */
function fmtTime(iso: string | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`;
}

/** 打开完整详情页（/Registry/App/:id 既有 AutoPage 详情） */
/** A8：跳转独立详情页，并携带行上下文（详情页首屏标题/版本/状态，避免二次请求列表） */
function goDetail(id: number | undefined) {
  if (id == null) return;
  const row = list.value.find((r) => r.id === id);
  if (row) {
    try {
      sessionStorage.setItem('stardust-app-ctx', JSON.stringify({ name: row.name, version: row.version, status: row.status }));
    } catch {
      /* sessionStorage 不可用时忽略 */
    }
  }
  void router.push('/stardust/apps/' + id);
}

/** 页签切换：流水线页签首次切入时加载数据（懒加载，其余页签数据走 showDetail 四路并行） */
function switchTab(key: string) {
  tab.value = key;
  if (key === 'pipeline' && pipelines.value.length === 0 && !pipelinesLoading.value) void loadPipelineTab();
}

/** P0-1: 切换实例 Inspector 展开/收起（点击其他实例自动收起当前） */
function toggleInstance(i: number) {
  expandedInstance.value = expandedInstance.value === i ? null : i;
}

/** P0-1: 快捷入口跳转（带实例筛选参数） */
function goInstanceLink(path: string, row: m.OnlineRow) {
  drawerVisible.value = false;
  const params = new URLSearchParams();
  if (current.value?.name) params.set('AppName', current.value.name);
  if (row.client) params.set('Client', row.client);
  if (row.nodeName) params.set('NodeName', row.nodeName);
  void router.push(`${path}?${params.toString()}`);
}

// ===== 流水线页签（§4.7 修订 2026-10-13）：状态 + 发布/触发/编辑/历史 =====
const pipelines = ref<m.PipelineRow[]>([]);
const pipelinesLoading = ref(false);
const runHistory = ref<m.RunHistoryRow[]>([]);
const runHistoryLoading = ref(false);
const editVisible = ref(false);
const editForm = ref<m.PipelineEditForm>({ branch: '', buildNodeId: undefined, autoDeploy: false, enable: true });
const editTarget = ref<m.PipelineRow | null>(null);
const editSaving = ref(false);
const triggering = ref<number | null>(null);

/** 加载流水线页签数据：应用发布集 -> 流水线列表 ->（合并）运行历史 */
async function loadPipelineTab() {
  const app = current.value;
  if (!app?.id) return;
  pipelinesLoading.value = true;
  try {
    const deployId = await m.resolveDeployId(request, app.id);
    pipelines.value = deployId > 0 ? await m.fetchPipelines(request, deployId) : [];
  } catch (e: unknown) {
    ElMessage.error(e instanceof Error ? e.message : '加载流水线失败');
    pipelines.value = [];
  } finally {
    pipelinesLoading.value = false;
  }
  await reloadRunHistory();
}

/** 重新拉取下方运行历史（触发/保存后复用） */
async function reloadRunHistory() {
  runHistoryLoading.value = true;
  try {
    runHistory.value = await m.fetchRunHistory(request, pipelines.value);
  } finally {
    runHistoryLoading.value = false;
  }
}

/** 轮询定时器：有进行中 run（排队/编译/部署）时自动刷新历史，直到全部到终态或超时 */
let runPollTimer: number | undefined;

/** 启动进行中轮询：立即刷一次，之后最多 6 次、每 3s 一次；期间只要有进行中 run 就继续 */
function startRunPolling() {
  stopRunPolling();
  let ticks = 0;
  const poll = async () => {
    ticks++;
    await reloadRunHistory();
    const hasActive = runHistory.value.some((r) => m.isRunActive(r.status));
    if (hasActive && ticks < 6) runPollTimer = window.setTimeout(poll, 3000);
  };
  runPollTimer = window.setTimeout(poll, 1500);
}
function stopRunPolling() {
  if (runPollTimer) {
    window.clearTimeout(runPollTimer);
    runPollTimer = undefined;
  }
}

/** 是否有进行中的 run（历史头部提示「编译/部署进行中，列表自动刷新」） */
const hasActiveRun = computed(() => runHistory.value.some((r) => m.isRunActive(r.status)));

/** 「发布」主按钮：取第一条启用流水线直接触发；无流水线给出引导文案 */
async function onPublish() {
  const p = pipelines.value.find((x) => x.enable && x.id);
  if (!p?.id) {
    ElMessage.warning('该应用尚无启用中的流水线，请在下方流水线行配置后重试');
    return;
  }
  await doTrigger(p, '发布');
}

/** 行内「手动触发」（el-popconfirm confirm 回调） */
async function onManualTrigger(row: m.PipelineRow) {
  await doTrigger(row, '手动触发');
}

/** 触发流水线共用：confirm 文案区分；成功后延时轮询历史（run 为异步创建/推进，立即刷可能还未入库或还在排队） */
async function doTrigger(row: m.PipelineRow, action: string) {
  if (!row.id) return;
  triggering.value = row.id;
  try {
    const r = await m.triggerPipeline(request, row.id);
    if (r.ok) {
      ElMessage.success(`${action}成功：${r.message}`);
      // 立即刷一次（run 已 Insert），随后进入进行中轮询（状态推进可视化）
      await reloadRunHistory();
      startRunPolling();
    } else {
      ElMessage.error(r.message);
    }
  } catch (e: unknown) {
    ElMessage.error(e instanceof Error ? e.message : '触发失败');
  } finally {
    triggering.value = null;
  }
}

/** 打开编辑 dialog（拷贝当前行到表单） */
function openEdit(row: m.PipelineRow) {
  editTarget.value = row;
  editForm.value = { branch: row.branch || '', buildNodeId: row.buildNodeId, autoDeploy: !!row.autoDeploy, enable: !!row.enable };
  editVisible.value = true;
}

/** 编辑保存：POST Update -> 成功后关窗刷新 */
async function onEditSave() {
  if (!editTarget.value) return;
  editSaving.value = true;
  try {
    const r = await m.updatePipeline(request, editTarget.value, editForm.value);
    if (r.ok) {
      ElMessage.success(r.message);
      editVisible.value = false;
      await loadPipelineTab();
    } else {
      ElMessage.error(r.message);
    }
  } catch (e: unknown) {
    ElMessage.error(e instanceof Error ? e.message : '保存失败');
  } finally {
    editSaving.value = false;
  }
}

/** 运行历史 -> 详情页（三级路由兜底已注册 /Deployment/AppPipelineRun/:id） */
function goRunDetail(row: m.RunHistoryRow) {
  if (!row.id) return;
  drawerVisible.value = false;
  void router.push(`/Deployment/AppPipelineRun/${row.id}`);
}

/** A6：空态图标（Lucide 线性 path，stroke 1.5，内联 SVG） */
const ICON_PACKAGE = 'M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z';


/** A6：空态图标（Lucide 线性 path，stroke 1.5） */
const ICON_EMPTY = 'M3 7h18M3 12h18M3 17h10';

</script>

<template>
  <div class="flex h-full flex-col gap-5">
    <!-- 页头 -->
    <div class="flex items-center justify-between">
      <div class="flex flex-col gap-1">
        <h1 class="text-xl font-semibold text-[var(--el-text-color-primary)]">
          应用
        </h1>
        <p class="text-xs text-[var(--el-text-color-regular)]">
          以应用为核心资源，收敛实例 / 服务 / 配置 / 发布 / 可观测能力 — 点击行快速查看
        </p>
      </div>
      <div class="flex items-center gap-3">
        <RefreshDot label="列表自动刷新" />
        <el-button
          type="primary"
          size="small"
        >
          新建应用
        </el-button>
      </div>
    </div>

    <!-- KPI（盒装卡片，对照效果图） -->
    <div class="grid grid-cols-4 gap-4">
      <KpiStat
        label="应用总数"
        :value="stats.total"
        accent="primary"
      />
      <KpiStat
        label="运行"
        :value="stats.running"
        accent="success"
      />
      <KpiStat
        label="异常"
        :value="stats.error"
        accent="warning"
      />
      <KpiStat
        label="离线"
        :value="stats.offline"
        accent="danger"
      />
    </div>

    <!-- 过滤 + 搜索 -->
    <div class="flex items-center justify-between gap-4">
      <div class="flex items-center gap-2">
        <FilterChips
          v-model="filter"
          :options="m.statusChips.map((c, i) => ({ value: i === 0 ? 'all' : c.key, label: c.label, count: m.chipCount(stats, c.key) }))"
        />
        />
      </div>
      <el-input
        v-model="keyword"
        placeholder="搜索应用名称"
        clearable
        size="small"
        class="w-56"
      />
    </div>

    <!-- 列表行：表格优先（方案 §7.2 / §22.3），点击行打开应用 Drawer -->
    <div class="flex flex-col gap-3">
      <el-table
        :data="filtered"
        data-spec="atable"
        class="sd-app-table"
        @row-click="(row: m.AppAggRow) => showDetail(row)"
      >
        <el-table-column
          label="应用"
          min-width="240"
        >
          <template #default="{ row }">
            <div class="flex items-center gap-2.5">
              <div
                class="flex h-[34px] w-[34px] shrink-0 items-center justify-center rounded-lg bg-[var(--el-color-primary)]/15 text-sm font-semibold text-[var(--el-color-primary)]"
              >
                {{ String(row.name || 'A').charAt(0) }}
              </div>
              <div class="min-w-0">
                <div class="truncate text-[14px] font-semibold text-[var(--el-text-color-primary)]">
                  {{ row.name || '-' }}
                </div>
                <div class="truncate text-xs text-[var(--el-text-color-placeholder)]">
                  {{ row.displayName || row.category || '暂无描述' }}
                </div>
              </div>
            </div>
          </template>
        </el-table-column>
        <el-table-column
          label="状态"
          width="96"
        >
          <template #default="{ row }">
            <StatusPill :status="row.status" />
          </template>
        </el-table-column>
        <el-table-column
          label="实例"
          width="76"
          align="right"
        >
          <template #default="{ row }">
            <span class="tabular-nums text-[var(--el-text-color-primary)]">{{ row.instances }}</span>
          </template>
        </el-table-column>
        <el-table-column
          label="服务"
          width="76"
          align="right"
        >
          <template #default="{ row }">
            <span class="tabular-nums text-[var(--el-text-color-primary)]">{{ row.services }}</span>
          </template>
        </el-table-column>
        <el-table-column
          label="QPS"
          width="90"
          align="right"
        >
          <template #default="{ row }">
            <span class="tabular-nums text-[var(--el-text-color-regular)]">{{ row.metric ? row.metric.qps.toFixed(1) : '—' }}</span>
          </template>
        </el-table-column>
        <el-table-column
          label="错误率"
          width="90"
          align="right"
        >
          <template #default="{ row }">
            <span
              class="tabular-nums"
              :style="{ color: row.metric && row.metric.errorRate >= 0.01 ? 'var(--el-color-danger)' : 'var(--el-text-color-regular)' }"
            >{{ row.metric ? (row.metric.errorRate * 100).toFixed(2) + '%' : '—' }}</span>
          </template>
        </el-table-column>
        <el-table-column
          label="P99"
          width="90"
          align="right"
        >
          <template #default="{ row }">
            <span class="tabular-nums text-[var(--el-text-color-regular)]">{{ row.metric ? row.metric.p99.toFixed(0) + 'ms' : '—' }}</span>
          </template>
        </el-table-column>
        <el-table-column
          label="版本"
          width="100"
        >
          <template #default="{ row }">
            <span class="rounded bg-[var(--el-fill-color-light)] px-1.5 py-0.5 text-xs text-[var(--el-text-color-regular)]">
              {{ row.version || '—' }}
            </span>
          </template>
        </el-table-column>
        <el-table-column
          label="更新时间"
          width="150"
        >
          <template #default="{ row }">
            <span class="text-xs tabular-nums text-[var(--el-text-color-placeholder)]">{{ fmtTime(row.updateTime) }}</span>
          </template>
        </el-table-column>
        <template #empty>
          <EmptyState
            :icon="ICON_PACKAGE"
            title="当前项目暂无应用"
            hint="可切换项目，或新建应用后接入注册中心"
          />
        </template>
      </el-table>
    </div>

    <!-- 应用详情抽屉：概览 / 实例 / 服务 / 配置 / 发布 / 可观测 / 审计 -->
    <el-drawer
      v-model="drawerVisible"
      :title="current?.name || '应用详情'"
      size="560px"
      data-spec="drawer"
      class="stardust-drawer"
      :show-close="false"
      destroy-on-close
    >
      <template #header>
        <div class="flex min-w-0 flex-1 items-center justify-between gap-3">
          <div class="flex min-w-0 flex-col">
            <span class="truncate text-lg font-bold text-[var(--el-text-color-primary)]">
              {{ current?.name || '应用详情' }}
            </span>
            <span
              v-if="current?.displayName"
              class="truncate text-xs text-[var(--el-text-color-secondary)]"
            >
              {{ current.displayName }}
            </span>
          </div>
          <div class="flex shrink-0 items-center gap-2">
            <button
              class="rounded-lg border border-[var(--el-border-color)] px-2.5 py-1.5 text-xs text-[var(--el-text-color-regular)] transition-colors hover:border-[var(--el-color-primary)] hover:text-[var(--el-color-primary)]"
              @click="goDetail(current?.id)"
            >
              查看完整详情 →
            </button>
            <button
              class="sd-close"
              aria-label="关闭"
              @click="drawerVisible = false"
            >
              ×
            </button>
          </div>
        </div>
      </template>

      <div class="flex flex-col">
        <!-- 抽屉头部七态 banner -->
        <div
          v-if="current"
          class="sd-banner"
          :style="m.bannerFor(current.status)"
        >
          <div class="flex items-center gap-2.5">
            <span class="sd-banner-dot" />
            <span class="sd-banner-text">{{ m.bannerFor(current.status).text }}</span>
          </div>
          <span class="sd-banner-version">{{ current.version || '-' }}</span>
        </div>

        <!-- 自定义页签 -->
        <div class="sd-tabs">
          <button
            v-for="t in m.drawerTabs"
            :key="t.key"
            class="sd-tab"
            :class="{ 'sd-tab-active': tab === t.key }"
            @click="switchTab(t.key)"
          >
            {{ t.label }}
          </button>
        </div>

        <!-- 概览（§7.6：KPI + 最近事件 + 摘要区块） -->
        <div
          v-if="tab === 'overview'"
          class="flex flex-col gap-4"
        >
          <!-- KPI 小卡：实例数 / QPS / 错误率 / P99 -->
          <div class="grid grid-cols-4 gap-2">
            <KpiStat
              label="实例数"
              :value="current?.instances ?? 0"
              accent="primary"
            />
            <KpiStat
              label="QPS"
              :value="current?.metric ? Math.round(current.metric.qps * 10) / 10 : 0"
              accent="success"
            />
            <KpiStat
              label="错误率 %"
              :value="current?.metric ? Math.round(current.metric.errorRate * 10000) / 100 : 0"
              accent="warning"
            />
            <KpiStat
              label="P99 (ms)"
              :value="current?.metric ? Math.round(current.metric.p99) : 0"
              accent="warning"
            />
          </div>

          <!-- 最近事件时间线 -->
          <div class="sd-overview-card">
            <div class="sd-overview-title">
              最近事件
            </div>
            <div
              v-if="recentEvents.length"
              class="flex flex-col gap-2.5"
            >
              <div
                v-for="(ev, i) in recentEvents"
                :key="i"
                class="flex items-center gap-2.5 text-xs"
              >
                <span
                  class="h-2 w-2 shrink-0 rounded-full"
                  :class="ev.type === 'deploy' ? 'bg-[var(--el-color-primary)]' : 'bg-[var(--el-color-success)]'"
                />
                <span class="flex-1 truncate text-[var(--el-text-color-primary)]">{{ ev.text }}</span>
                <span class="shrink-0 tabular-nums text-[var(--el-text-color-placeholder)]">{{ ev.time }}</span>
              </div>
            </div>
            <div
              v-else
              class="text-xs text-[var(--el-text-color-placeholder)]"
            >
              暂无最近事件
            </div>
          </div>

          <!-- 应用注册信息 -->
          <div class="sd-overview-card">
            <div class="sd-overview-title">
              应用注册信息
            </div>
            <div class="sd-overview-grid">
              <div class="sd-overview-item">
                <span class="sd-overview-label">应用ID</span>
                <span class="sd-overview-value">{{ current?.id ?? '-' }}</span>
              </div>
              <div class="sd-overview-item">
                <span class="sd-overview-label">当前版本</span>
                <span class="sd-overview-value font-mono">{{ current?.version || '-' }}</span>
              </div>
              <div class="sd-overview-item">
                <span class="sd-overview-label">实例数</span>
                <span class="sd-overview-value">{{ current?.instances ?? 0 }}</span>
              </div>
              <div class="sd-overview-item">
                <span class="sd-overview-label">语言框架</span>
                <span class="sd-overview-value">{{ [String(current?.language ?? ''), String(current?.framework ?? '')].filter(Boolean).join(' ') || '-' }}</span>
              </div>
              <div class="sd-overview-item">
                <span class="sd-overview-label">负责人</span>
                <span class="sd-overview-value">{{ String(current?.createUser ?? '-') }}</span>
              </div>
              <div class="sd-overview-item">
                <span class="sd-overview-label">注册服务</span>
                <div class="flex items-center gap-2">
                  <span class="sd-mini-pill sd-mini-pill-success">{{ services.provide.length }} 提供</span>
                  <span class="sd-mini-pill sd-mini-pill-primary">{{ services.consume.length }} 消费</span>
                </div>
              </div>
            </div>
          </div>

          <!-- 摘要区块：实例健康 / 服务依赖 / 最近发布 / 最近告警 -->
          <div class="grid grid-cols-2 gap-2">
            <div class="sd-overview-card !p-3">
              <div class="text-[11px] text-[var(--el-text-color-secondary)]">
                实例健康
              </div>
              <div class="mt-1 text-sm font-semibold text-[var(--el-text-color-primary)]">
                {{ overviewSummary.onlineInstances }} / {{ overviewSummary.totalInstances }} 在线
              </div>
            </div>
            <div class="sd-overview-card !p-3">
              <div class="text-[11px] text-[var(--el-text-color-secondary)]">
                服务依赖
              </div>
              <div class="mt-1 text-sm font-semibold text-[var(--el-text-color-primary)]">
                {{ overviewSummary.provideServices }} 提供 · {{ overviewSummary.consumeServices }} 消费
              </div>
            </div>
            <div class="sd-overview-card !p-3">
              <div class="text-[11px] text-[var(--el-text-color-secondary)]">
                最近发布
              </div>
              <div class="mt-1 text-sm font-semibold text-[var(--el-text-color-primary)]">
                {{ overviewSummary.latestVersion }} <span class="text-xs font-normal text-[var(--el-text-color-placeholder)]">{{ overviewSummary.latestReleaseTime }}</span>
              </div>
            </div>
            <div class="sd-overview-card !p-3">
              <div class="text-[11px] text-[var(--el-text-color-secondary)]">
                最近告警
              </div>
              <div class="mt-1 text-sm font-semibold text-[var(--el-text-color-primary)]">
                {{ overviewSummary.latestAlertLevel }} <span class="text-xs font-normal text-[var(--el-text-color-placeholder)]">{{ overviewSummary.latestAlertTime }}</span>
              </div>
            </div>
          </div>
        </div>

        <!-- 实例（§8.1/§8.2：列表 + 点击展开 Inspector） -->
        <div
          v-if="tab === 'instances'"
          class="flex flex-col gap-2"
        >
          <!-- P2-1: 实例级 CPU/内存指标待接入提示 -->
          <div class="text-[11px] text-[var(--el-text-color-placeholder)]">
            实例级 CPU/内存指标待接入 · 点击实例行展开详情
          </div>
          <div
            v-for="(r, i) in instances"
            :key="i"
            class="sd-svc cursor-pointer transition-colors"
            :class="expandedInstance === i ? '!border-[var(--el-color-primary)] !bg-[color-mix(in_srgb,var(--el-color-primary)_8%,transparent)]' : 'hover:!bg-[color-mix(in_srgb,var(--el-color-primary)_4%,transparent)]'"
            @click="toggleInstance(i)"
          >
            <div class="flex items-center justify-between gap-2">
              <div class="flex min-w-0 items-center gap-2">
                <span class="truncate text-[13px] font-medium text-[var(--el-text-color-primary)]">{{ r.client || r.processName || '-' }}</span>
                <span
                  class="sd-role"
                  :style="m.pillStyleFor(r.enable === false ? '离线' : '运行')"
                >{{ r.enable === false ? '离线' : '运行' }}</span>
              </div>
              <span class="shrink-0 text-xs text-[var(--el-text-color-placeholder)]">{{ r.ip || '-' }}</span>
            </div>
            <div class="mt-1 flex items-center justify-between gap-2 text-[11px] text-[var(--el-text-color-placeholder)]">
              <span class="truncate">{{ r.nodeName || r.machineName || '-' }}</span>
              <span class="shrink-0">{{ r.version || '-' }}</span>
            </div>

            <!-- P0-1: 实例 Inspector 展开面板 -->
            <div
              v-if="expandedInstance === i"
              class="mt-3 flex flex-col gap-3 border-t border-[var(--el-border-color-light)] pt-3"
            >
              <!-- 身份信息 -->
              <div>
                <div class="mb-1.5 text-[11px] font-semibold text-[var(--el-text-color-secondary)]">
                  身份信息
                </div>
                <div class="grid grid-cols-2 gap-x-3 gap-y-1.5 text-xs">
                  <div class="flex justify-between">
                    <span class="text-[var(--el-text-color-placeholder)]">进程</span><span class="font-mono text-[var(--el-text-color-primary)]">{{ r.processName || '—' }}</span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-[var(--el-text-color-placeholder)]">节点</span><span class="text-[var(--el-text-color-primary)]">{{ r.nodeName || r.machineName || '—' }}</span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-[var(--el-text-color-placeholder)]">IP</span><span class="font-mono text-[var(--el-text-color-primary)]">{{ r.ip || '—' }}</span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-[var(--el-text-color-placeholder)]">运行时</span><span class="text-[var(--el-text-color-primary)]">{{ m.detectRuntime(r) }}</span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-[var(--el-text-color-placeholder)]">启动时间</span><span class="tabular-nums text-[var(--el-text-color-primary)]">{{ r.startTime ? String(r.startTime).slice(0, 16) : '—' }}</span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-[var(--el-text-color-placeholder)]">版本</span><span class="font-mono text-[var(--el-text-color-primary)]">{{ r.version || '—' }}</span>
                  </div>
                </div>
              </div>
              <!-- 资源指标 -->
              <div>
                <div class="mb-1.5 text-[11px] font-semibold text-[var(--el-text-color-secondary)]">
                  资源指标
                </div>
                <div class="grid grid-cols-2 gap-x-3 gap-y-1.5 text-xs">
                  <div class="flex justify-between">
                    <span class="text-[var(--el-text-color-placeholder)]">CPU</span><span class="text-[var(--el-text-color-placeholder)]">— 待接入</span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-[var(--el-text-color-placeholder)]">内存</span><span class="text-[var(--el-text-color-placeholder)]">— 待接入</span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-[var(--el-text-color-placeholder)]">网络</span><span class="text-[var(--el-text-color-placeholder)]">— 待接入</span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-[var(--el-text-color-placeholder)]">句柄</span><span class="text-[var(--el-text-color-placeholder)]">— 待接入</span>
                  </div>
                </div>
              </div>
              <!-- 快捷入口 -->
              <div class="flex items-center gap-2">
                <button
                  class="rounded-md border border-[var(--el-border-color)] px-2 py-1 text-[11px] text-[var(--el-text-color-regular)] transition-colors hover:border-[var(--el-color-primary)] hover:text-[var(--el-color-primary)]"
                  @click.stop="goInstanceLink('/Monitors/AppLog', r)"
                >
                  查看日志
                </button>
                <button
                  class="rounded-md border border-[var(--el-border-color)] px-2 py-1 text-[11px] text-[var(--el-text-color-regular)] transition-colors hover:border-[var(--el-color-primary)] hover:text-[var(--el-color-primary)]"
                  @click.stop="goInstanceLink('/Monitors/TraceData', r)"
                >
                  查看 Trace
                </button>
                <button
                  class="rounded-md border border-[var(--el-border-color)] px-2 py-1 text-[11px] text-[var(--el-text-color-regular)] transition-colors hover:border-[var(--el-color-primary)] hover:text-[var(--el-color-primary)]"
                  @click.stop="goInstanceLink('/Nodes/NodeOnline', r)"
                >
                  执行命令
                </button>
              </div>
            </div>
          </div>
          <EmptyState
            :icon="ICON_EMPTY"
            title="暂无在线实例"
          />
        </div>

        <!-- 配置：表格 / 文本双模式 + 版本历史卡 -->
        <div
          v-if="tab === 'config'"
          class="flex flex-col"
        >
          <div class="flex items-center justify-between">
            <div class="sd-seg">
              <button
                class="sd-seg-btn"
                :class="{ 'sd-seg-active': configMode === 'table' }"
                @click="configMode = 'table'"
              >
                表格
              </button>
              <button
                class="sd-seg-btn"
                :class="{ 'sd-seg-active': configMode === 'text' }"
                @click="configMode = 'text'"
              >
                文本
              </button>
            </div>
            <span class="text-xs text-[var(--el-text-color-placeholder)]">{{ configKvs.length }} 项</span>
          </div>
          <div
            v-if="configMode === 'table'"
            class="mt-2 flex flex-col"
          >
            <div
              v-for="kv in configKvs"
              :key="kv.key"
              class="flex justify-between border-b border-[var(--el-border-color-lighter)] py-[9px] text-[13px]"
            >
              <span class="text-[var(--el-text-color-regular)]">{{ kv.key }}</span>
              <span class="sd-kv-value">{{ kv.value }}</span>
            </div>
            <div
              v-if="!configKvs.length"
              class="py-2 text-xs text-[var(--el-text-color-placeholder)]"
            >
              暂无配置
            </div>
          </div>
          <pre
            v-else
            class="sd-config-text"
          >{{ configTxt || '暂无配置' }}</pre>
          <div class="sd-hist-card">
            <div class="text-xs font-semibold text-[var(--el-text-color-regular)]">
              版本历史
            </div>
            <div class="mt-[10px] flex flex-col gap-2">
              <template v-if="histories.length">
                <div
                  v-for="h in histories"
                  :key="h.id"
                  class="flex items-center justify-between gap-2 text-xs"
                >
                  <div class="flex min-w-0 flex-col gap-0.5">
                    <span class="truncate text-[var(--el-text-color-primary)]">{{ h.action || '发布' }}</span>
                    <span class="text-[var(--el-text-color-placeholder)]">{{ h.time || '-' }}</span>
                  </div>
                  <span class="flex shrink-0 items-center gap-3 text-[var(--el-color-primary)]">
                    <button
                      class="cursor-pointer"
                      @click="doDiff(h)"
                    >对比上一版 ⟲</button>
                    <button
                      class="cursor-pointer"
                      @click="doRollback(h)"
                    >回滚到此版本 ⤺</button>
                  </span>
                </div>
              </template>
              <div
                v-else
                class="text-xs text-[var(--el-text-color-placeholder)]"
              >
                暂无发布历史
              </div>
            </div>
          </div>
        </div>

        <!-- 发布：流水线 pstep + 箭头 + 图例 -->
        <div
          v-if="tab === 'publish'"
          class="flex flex-col"
        >
          <div
            class="flex flex-wrap items-start gap-2"
            data-spec="psteps"
          >
            <template
              v-for="(s, i) in steps"
              :key="s.key"
            >
              <div class="flex flex-col items-center gap-1.5">
                <div
                  data-spec="pstep"
                  class="sd-step"
                  :class="s.state === 'done' ? 'sd-step-done' : s.state === 'run' ? 'sd-step-run' : 'sd-step-wait'"
                >
                  {{ s.state === 'done' ? '✓' : s.state === 'run' ? '●' : '○' }}
                </div>
                <span class="text-[11px] text-[var(--el-text-color-secondary)]">{{ s.label }}</span>
              </div>
              <span
                v-if="i < steps.length - 1"
                class="sd-step-arrow"
              >→</span>
            </template>
          </div>
          <div class="mt-3 text-xs text-[var(--el-text-color-secondary)]">
            ✓ 完成 ● 进行中 ○ 等待
          </div>
          <div class="mt-4 flex flex-col gap-2">
            <div
              v-for="(r, i) in detail.runs.slice(0, 5)"
              :key="i"
              class="sd-svc flex items-center gap-2"
            >
              <span
                class="sd-role"
                :style="m.pillStyleFor(r.status === 'Success' ? '运行' : '离线')"
              >{{ r.status || '-' }}</span>
              <span class="min-w-0 flex-1 truncate text-[13px] font-semibold text-[var(--el-text-color-primary)]">
                {{ r.pipelineName || '流水线' }} v{{ r.appVersionId ?? '-' }}
              </span>
              <span class="shrink-0 text-[11px] text-[var(--el-text-color-placeholder)]">
                {{ String(r.deployFinishedTime || r.buildFinishedTime || '').slice(0, 16) }}
              </span>
            </div>
            <div
              v-if="!detail.runs.length"
              class="text-xs text-[var(--el-text-color-placeholder)]"
            >
              暂无发布记录
            </div>
          </div>
        </div>

        <!-- 流水线（§4.7 修订 2026-10-13）：发布 CTA / 流水线行（手动触发 + 编辑）/ 运行历史 -->
        <div
          v-if="tab === 'pipeline'"
          class="flex flex-col gap-4"
          data-spec="pipeline-tab"
        >
          <!-- 主 CTA：发布（单焦点，主按钮唯一） -->
          <div
            class="sd-pipe-hero"
            data-spec="pipe-publish"
          >
            <div class="flex flex-col gap-0.5">
              <span class="text-[13px] font-semibold text-[var(--el-text-color-primary)]">发布新版本</span>
              <span class="text-[11px] text-[var(--el-text-color-secondary)]">
                触发该应用的启用流水线：编译 → 上传 → 部署
              </span>
            </div>
            <el-button
              type="primary"
              size="small"
              :loading="triggering != null"
              data-spec="pipe-publish-btn"
              @click="onPublish"
            >
              发布
            </el-button>
          </div>

          <!-- 流水线行列表（本应用全部流水线） -->
          <div class="flex flex-col gap-2">
            <div class="text-xs font-semibold text-[var(--el-text-color-secondary)]">
              流水线（{{ pipelines.length }}）
            </div>
            <div
              v-for="p in pipelines"
              :key="p.id"
              class="sd-pipe-row"
              data-spec="pipe-row"
            >
              <div class="flex min-w-0 flex-1 flex-col gap-0.5">
                <div class="flex min-w-0 items-center gap-2">
                  <span class="truncate text-[13px] font-semibold text-[var(--el-text-color-primary)]">{{ p.name || `流水线 #${p.id}` }}</span>
                  <span
                    class="sd-pipe-tag"
                    :class="{ 'sd-pipe-tag-off': !p.enable }"
                  >{{ p.enable ? '启用' : '停用' }}</span>
                  <span
                    v-if="p.autoDeploy"
                    class="sd-pipe-tag"
                  >自动部署</span>
                </div>
                <span class="truncate text-[11px] text-[var(--el-text-color-placeholder)]">
                  分支 {{ p.branch || '—' }} · 编译节点 {{ p.buildNodeId ?? '—' }}
                </span>
              </div>
              <div class="flex shrink-0 items-center gap-2">
                <el-popconfirm
                  title="确认手动触发该流水线？"
                  confirm-button-text="触发"
                  cancel-button-text="取消"
                  @confirm="onManualTrigger(p)"
                >
                  <template #reference>
                    <el-button
                      size="small"
                      type="success"
                      plain
                      :disabled="!p.enable"
                      :loading="triggering === p.id"
                      data-spec="pipe-trigger"
                    >
                      手动触发
                    </el-button>
                  </template>
                </el-popconfirm>
                <el-button
                  size="small"
                  plain
                  data-spec="pipe-edit"
                  @click="openEdit(p)"
                >
                  编辑
                </el-button>
              </div>
            </div>
            <div
              v-if="!pipelines.length && !pipelinesLoading"
              class="text-xs text-[var(--el-text-color-placeholder)]"
            >
              该应用尚未创建流水线 — 可在「发布 → 流水线」页配置（关联部署集与编译节点）
            </div>
            <div
              v-if="pipelinesLoading"
              v-loading="pipelinesLoading"
              class="h-10"
            />
          </div>

          <!-- 流水线历史（下方只读列表，最新在前） -->
          <div class="flex flex-col gap-2 border-t border-[var(--el-border-color-lighter)] pt-3">
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2">
                <span class="text-xs font-semibold text-[var(--el-text-color-secondary)]">流水线历史</span>
                <span
                  v-if="hasActiveRun"
                  class="flex items-center gap-1 text-[11px] text-[var(--el-color-primary)]"
                >
                  <span class="h-1.5 w-1.5 animate-pulse rounded-full bg-[var(--el-color-primary)]" />
                  进行中 · 列表自动刷新
                </span>
              </div>
              <el-button
                link
                type="primary"
                size="small"
                @click="reloadRunHistory"
              >
                刷新
              </el-button>
            </div>
            <div
              v-for="r in runHistory.slice(0, 10)"
              :key="r.id"
              class="sd-pipe-run"
              data-spec="pipe-run"
            >
              <span
                class="sd-pipe-status"
                :style="{ color: m.runStatusStyle(r.status).color, background: m.runStatusStyle(r.status).background }"
              >{{ m.runStatusStyle(r.status).label }}</span>
              <div class="flex min-w-0 flex-1 flex-col">
                <span class="truncate text-[12px] font-medium text-[var(--el-text-color-primary)]">
                  {{ r.pipelineName || '流水线' }} <span v-if="r.appVersionId">v{{ r.appVersionId }}</span>
                </span>
                <span class="truncate text-[11px] text-[var(--el-text-color-placeholder)]">
                  {{ m.triggerLabel(r.triggerSource || '') }} · {{ r.commitAuthor || '—' }} · {{ (r.finishedTime || r.buildStartedTime || '').slice(0, 16) }}
                </span>
              </div>
              <el-button
                link
                type="primary"
                size="small"
                @click="goRunDetail(r)"
              >
                详情
              </el-button>
            </div>
            <div
              v-if="!runHistory.length && !runHistoryLoading"
              class="text-xs text-[var(--el-text-color-placeholder)]"
            >
              暂无运行记录 — 触发后这里会显示每次构建/部署的进展
            </div>
            <div
              v-if="runHistoryLoading"
              v-loading="runHistoryLoading"
              class="h-10"
            />
          </div>
        </div>

        <!-- 编辑流水线 dialog（重量确认 §ui-spec） -->
        <el-dialog
          v-model="editVisible"
          :title="`编辑流水线 · ${editTarget?.name || ''}`"
          width="440px"
          append-to-body
          destroy-on-close
        >
          <el-form
            label-width="90px"
            label-position="left"
          >
            <el-form-item label="分支">
              <el-input
                v-model="editForm.branch"
                placeholder="main（支持 release/* 通配）"
              />
            </el-form-item>
            <el-form-item label="编译节点">
              <el-input-number
                v-model="editForm.buildNodeId"
                :min="0"
                controls-position="right"
                class="w-full"
              />
            </el-form-item>
            <el-form-item label="自动部署">
              <el-switch v-model="editForm.autoDeploy" />
            </el-form-item>
            <el-form-item label="启用">
              <el-switch v-model="editForm.enable" />
            </el-form-item>
          </el-form>
          <template #footer>
            <el-button @click="editVisible = false">
              取消
            </el-button>
            <el-button
              type="primary"
              :loading="editSaving"
              @click="onEditSave"
            >
              保存
            </el-button>
          </template>
        </el-dialog>

        <!-- 可观测：时间行 + 4 KPI + 双折线 svg（请求/延迟） -->
        <div
          v-if="tab === 'monitor'"
          class="flex flex-col"
        >
          <div class="flex items-center justify-between text-xs">
            <span class="text-[var(--el-text-color-regular)]">近 1 小时</span>
            <span class="flex items-center gap-1 text-[var(--el-text-color-placeholder)]">
              <i class="sd-dot bg-[var(--el-color-success)] animate-pulse" />自动刷新 15s
            </span>
          </div>
          <div class="mt-2 grid grid-cols-4 gap-2">
            <KpiStat
              label="QPS"
              :value="Math.round(monitor.qps * 10) / 10"
              accent="success"
            />
            <KpiStat
              label="P99 (ms)"
              :value="monitor.p99"
              accent="warning"
            />
            <KpiStat
              label="错误率 %"
              :value="Math.round(monitor.errorRate * 10000) / 100"
              accent="warning"
            />
            <KpiStat
              label="在线率 %"
              :value="Math.round(monitor.onlineRate * 10000) / 100"
              accent="success"
            />
          </div>
          <svg
            viewBox="0 0 320 96"
            preserveAspectRatio="none"
            class="mt-2 h-24 w-full rounded-lg bg-[var(--el-fill-color-light)]/50"
          >
            <polyline
              fill="none"
              stroke="var(--el-color-success)"
              stroke-width="2"
              stroke-linejoin="round"
              :points="monitor.series.map((s, i) => `${monitor.series.length > 1 ? (i * 300) / (monitor.series.length - 1) : 150},${90 - (s.qps / Math.max(...monitor.series.map((x) => x.qps), 1)) * 80}`).join(' ')"
            />
            <polyline
              fill="none"
              stroke="var(--el-color-primary)"
              stroke-width="2"
              stroke-linejoin="round"
              :points="monitor.series.map((s, i) => `${monitor.series.length > 1 ? (i * 300) / (monitor.series.length - 1) : 150},${90 - (s.latency / Math.max(...monitor.series.map((x) => x.latency), 1)) * 80}`).join(' ')"
            />
          </svg>
          <div class="mt-2 flex items-center gap-4 text-xs text-[var(--el-text-color-regular)]">
            <span class="flex items-center gap-1"><i class="sd-line bg-[var(--el-color-success)]" />请求</span>
            <span class="flex items-center gap-1"><i class="sd-line bg-[var(--el-color-primary)]" />延迟</span>
          </div>
        </div>

        <!-- 服务：提供 / 消费 -->
        <div
          v-if="tab === 'service'"
          class="flex flex-col gap-4"
        >
          <div>
            <div class="mb-2 text-xs font-semibold text-[var(--el-text-color-regular)]">
              提供（{{ services.provide.length }}）
            </div>
            <div class="flex flex-col gap-2">
              <div
                v-for="(r, i) in services.provide"
                :key="i"
                class="sd-svc"
              >
                <div class="flex items-center justify-between gap-2">
                  <div class="flex min-w-0 items-center gap-2">
                    <span class="truncate text-[13px] font-medium text-[var(--el-text-color-primary)]">{{ r.serviceName || '-' }}</span>
                    <span
                      class="sd-role"
                      :style="m.pillStyleFor('提供')"
                    >提供</span>
                  </div>
                  <span
                    class="shrink-0 text-xs"
                    :class="r.healthy ? 'text-[var(--el-color-success)]' : 'text-[var(--el-color-danger)]'"
                  >{{ r.healthy ? '正常' : '异常' }}</span>
                </div>
                <div class="mt-1 flex items-center justify-between gap-2 text-[11px] text-[var(--el-text-color-placeholder)]">
                  <span class="truncate">{{ r.address || '-' }}</span>
                  <span class="shrink-0">v{{ r.version || '-' }} · 权重 {{ r.weight ?? '-' }}</span>
                </div>
              </div>
              <EmptyState
                :icon="ICON_EMPTY"
                title="暂无提供服务"
              />
            </div>
          </div>
          <div>
            <div class="mb-2 text-xs font-semibold text-[var(--el-text-color-regular)]">
              消费（{{ services.consume.length }}）
            </div>
            <div class="flex flex-col gap-2">
              <div
                v-for="(r, i) in services.consume"
                :key="i"
                class="sd-svc"
              >
                <div class="flex items-center justify-between gap-2">
                  <div class="flex min-w-0 items-center gap-2">
                    <span class="truncate text-[13px] font-medium text-[var(--el-text-color-primary)]">{{ r.serviceName || '-' }}</span>
                    <span
                      class="sd-role"
                      :style="m.pillStyleFor('消费')"
                    >消费</span>
                  </div>
                  <span class="shrink-0 text-xs text-[var(--el-text-color-placeholder)]">{{ r.client || '-' }}</span>
                </div>
                <div class="mt-1 flex items-center justify-between gap-2 text-[11px] text-[var(--el-text-color-placeholder)]">
                  <span class="truncate">{{ r.address || '-' }}</span>
                  <span class="shrink-0">Ping {{ r.pingCount ?? 0 }}</span>
                </div>
              </div>
              <EmptyState
                :icon="ICON_EMPTY"
                title="暂无消费服务"
              />
            </div>
          </div>
        </div>

        <!-- 审计（§7.5：操作日志列表，无 API 时空状态） -->
        <div
          v-if="tab === 'audit'"
          class="flex flex-col gap-2"
        >
          <div class="text-xs text-[var(--el-text-color-secondary)]">
            操作日志记录应用的配置变更、发布操作、权限修改等审计事件。
          </div>
          <!-- 从流水线运行记录推导审计事件（无独立审计 API 时的兜底数据） -->
          <template v-if="detail.runs.length">
            <div
              v-for="(r, i) in detail.runs.slice(0, 10)"
              :key="'run-' + i"
              class="flex items-center gap-2.5 rounded-lg border border-[var(--el-border-color-light)] bg-[color-mix(in_srgb,var(--el-text-color-primary)_4%,transparent)] px-3 py-2.5"
            >
              <span
                class="rounded-lg px-[7px] py-0.5 text-[10px] font-medium"
                :style="r.status === 'Success'
                  ? { color: 'var(--el-color-success)', backgroundColor: 'color-mix(in srgb, var(--el-color-success) 15%, transparent)' }
                  : { color: 'var(--el-color-danger)', backgroundColor: 'color-mix(in srgb, var(--el-color-danger) 15%, transparent)' }"
              >{{ r.status || '未知' }}</span>
              <div class="flex min-w-0 flex-1 flex-col">
                <span class="truncate text-[13px] font-medium text-[var(--el-text-color-primary)]">
                  {{ r.pipelineName || '流水线' }} 发布 v{{ r.appVersionId ?? '-' }}
                </span>
                <span class="text-[11px] text-[var(--el-text-color-placeholder)]">
                  操作人：{{ r.commitAuthor || '系统' }} · 分支：{{ r.branch || '-' }}
                </span>
              </div>
              <span class="shrink-0 text-[11px] tabular-nums text-[var(--el-text-color-placeholder)]">
                {{ String(r.deployFinishedTime || r.buildFinishedTime || '').slice(0, 16) }}
              </span>
            </div>
          </template>
          <EmptyState
            :icon="ICON_EMPTY"
            title="审计日志待接入"
          />
        </div>
      </div>
    </el-drawer>
  </div>
</template>

<style scoped>
/* 应用表格：玻璃态表面 + 行 hover 抬升，全部 --el-* 派生 */
.sd-app-table {
  --el-table-border-color: var(--el-border-color-lighter);
  --el-table-header-bg-color: color-mix(in srgb, var(--el-bg-color-overlay) 60%, transparent);
  --el-table-tr-bg-color: color-mix(in srgb, var(--el-bg-color) 55%, transparent);
  --el-table-row-hover-bg-color: color-mix(in srgb, var(--el-color-primary) 10%, transparent);
  --el-table-bg-color: transparent;
  border-radius: 12px;
  overflow: hidden;
}
.sd-app-table :deep(.el-table__inner-wrapper::before) {
  display: none;
}
.sd-app-table :deep(th.el-table__cell) {
  background: color-mix(in srgb, var(--el-bg-color-overlay) 60%, transparent);
  color: var(--el-text-color-secondary);
  font-weight: 600;
}
.sd-app-table :deep(td.el-table__cell) {
  border-bottom-color: var(--el-border-color-lighter);
  cursor: pointer;
}
.sd-config-text {
  margin: 0;
  max-height: 220px;
  overflow: auto;
  border-radius: 8px;
  background: var(--el-fill-color-light);
  color: var(--el-color-info);
  padding: 10px 12px;
  font-family: ui-monospace, SFMono-Regular, Consolas, 'Courier New', monospace;
  font-size: 12px;
  line-height: 1.6;
  white-space: pre-wrap;
  word-break: break-all;
}
.sd-hist-card {
  display: flex;
  flex-direction: column;
  margin-top: 16px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 8px;
  padding: 10px 12px;
}
.sd-seg {
  display: inline-flex;
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
  overflow: hidden;
}
.sd-seg-btn {
  padding: 6px 14px;
  font-size: 12px;
  line-height: 1.4;
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
.sd-kv-value {
  font-family: ui-monospace, SFMono-Regular, Consolas, 'Courier New', monospace;
  color: var(--el-color-info);
}
/* 流水线页签（§4.7）：hero CTA / 行卡 / 徽标 / 历史行 —— 全 --el-* 派生 */
.sd-pipe-hero {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 10px;
  padding: 12px 14px;
  background: color-mix(in srgb, var(--el-color-primary) 6%, transparent);
}
.sd-pipe-row {
  display: flex;
  align-items: center;
  gap: 10px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 8px;
  padding: 10px 12px;
  background: color-mix(in srgb, var(--el-bg-color) 60%, transparent);
}
.sd-pipe-tag {
  flex-shrink: 0;
  border-radius: 6px;
  padding: 1px 7px;
  font-size: 10px;
  color: var(--el-color-success);
  background: color-mix(in srgb, var(--el-color-success) 12%, transparent);
}
.sd-pipe-tag-off {
  color: var(--el-text-color-secondary);
  background: color-mix(in srgb, var(--el-text-color-secondary) 12%, transparent);
}
.sd-pipe-run {
  display: flex;
  align-items: center;
  gap: 10px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  padding: 8px 2px;
}
.sd-pipe-status {
  flex-shrink: 0;
  border-radius: 6px;
  padding: 1px 7px;
  font-size: 10px;
  font-weight: 600;
}
.sd-step {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 54px;
  height: 54px;
  border: 2px solid;
  border-radius: 9999px;
  font-size: 20px;
  line-height: 1;
}
.sd-step-done {
  border-color: var(--el-color-success);
  background: color-mix(in srgb, var(--el-color-success) 15%, transparent);
  color: var(--el-color-success);
}
.sd-step-run {
  border-color: var(--el-color-primary);
  background: color-mix(in srgb, var(--el-color-primary) 15%, transparent);
  color: var(--el-color-primary);
  box-shadow: 0 0 12px color-mix(in srgb, var(--el-color-primary) 35%, transparent);
}
.sd-step-wait {
  border-color: var(--el-border-color);
  color: var(--el-text-color-secondary);
  opacity: 0.5;
}
.sd-step-arrow {
  margin-top: 18px;
  font-size: 18px;
  line-height: 1;
  color: var(--el-text-color-secondary);
}
.sd-dot {
  display: inline-block;
  width: 8px;
  height: 8px;
  border-radius: 9999px;
}
.sd-line {
  display: inline-block;
  width: 18px;
  height: 3px;
  border-radius: 2px;
}
.stardust-drawer :deep(.el-drawer__header) {
  margin-bottom: 0;
  padding: 16px 20px;
  border-bottom: 1px solid var(--el-border-color-light);
}
.stardust-drawer :deep(.el-drawer__body) {
  padding: 20px;
  background: color-mix(in srgb, var(--el-bg-color) 92%, transparent);
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
.sd-banner {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  border: 1px solid transparent;
  border-radius: 10px;
  padding: 12px 14px;
}
.sd-banner-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: currentColor;
  box-shadow: 0 0 10px currentColor;
}
.sd-banner-text {
  font-size: 13px;
  font-weight: 600;
}
.sd-banner-version {
  border-radius: 6px;
  padding: 3px 8px;
  background: color-mix(in srgb, var(--el-bg-color) 60%, transparent);
  font-family: ui-monospace, SFMono-Regular, Consolas, 'Courier New', monospace;
  font-size: 12px;
  color: var(--el-text-color-regular);
}
.sd-tabs {
  position: relative;
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin-top: 18px;
  margin-bottom: 16px;
  border-bottom: 1px solid var(--el-border-color-light);
}
.sd-tab {
  position: relative;
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
.sd-overview-card {
  border: 1px solid var(--el-border-color-light);
  border-radius: 12px;
  padding: 16px;
  background: color-mix(in srgb, var(--el-bg-color) 70%, transparent);
  backdrop-filter: blur(8px);
}
.sd-overview-title {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 14px;
  font-size: 12px;
  font-weight: 600;
  color: var(--el-text-color-regular);
}
.sd-overview-title::before {
  content: '';
  width: 3px;
  height: 12px;
  border-radius: 2px;
  background: var(--el-color-primary);
}
.sd-overview-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 16px 12px;
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
.sd-mini-pill {
  border-radius: 6px;
  padding: 2px 7px;
  font-size: 11px;
  font-weight: 600;
}
.sd-mini-pill-success {
  color: var(--el-color-success);
  background: color-mix(in srgb, var(--el-color-success) 12%, transparent);
}
.sd-mini-pill-primary {
  color: var(--el-color-primary);
  background: color-mix(in srgb, var(--el-color-primary) 12%, transparent);
}
.sd-tip {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  border: 1px solid color-mix(in srgb, var(--el-color-info) 25%, transparent);
  border-radius: 10px;
  padding: 12px 14px;
  background: color-mix(in srgb, var(--el-color-info) 8%, transparent);
  color: var(--el-text-color-regular);
  font-size: 12px;
  line-height: 1.5;
}
.sd-tip-icon {
  display: inline-flex;
  flex-shrink: 0;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 16px;
  border-radius: 50%;
  background: color-mix(in srgb, var(--el-color-info) 18%, transparent);
  color: var(--el-color-info);
  font-size: 10px;
  font-weight: 700;
}
.sd-svc {
  border: 1px solid var(--el-border-color-light);
  border-radius: 8px;
  padding: 10px 12px;
  background: color-mix(in srgb, var(--el-text-color-primary) 4%, transparent);
}
.sd-role {
  border-radius: 8px;
  padding: 2px 7px;
  font-size: 10px;
  line-height: 1.4;
  font-weight: 500;
  white-space: nowrap;
}
</style>
