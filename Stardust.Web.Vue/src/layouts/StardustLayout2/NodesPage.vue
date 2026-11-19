<script setup lang="ts">
/**
 * NodesPage：控制台「节点」页（/nodes）容器。版面：页头 → 暗底星图 canvas → 过滤条 → 批量操作卡 → 节点表格。
 * 取数与归一全部在 nodesPage.logic（request 注入）；本组件只管 canvas 绘制、过滤、勾选、抽屉与上下文联动。
 * projectId 取自 stardustContext（非空时随请求 params 透传，全局拦截器也会注入），随项目切换重取。
 *
 * 现实约束（不造数字）：
 *  - 批量运维五动作已接 NodeOnlineController（CARD-NODE-11，2026-11-09）：CheckUpgrade/SyncTime/
 *    Restart/Reboot/Execute 均 GET+keys=在线会话 Id 逗号串（勾选 Node.Id 经在线快照映射，nodeOnline.logic）；
 *    重启系统/执行命令走本地 GuardModal 二次确认；结果呈契约汇总条+逐节点展开行（不走 toast，保留逐节点回复）；
 *    前端无权限位数据源（旅程 NodeOnline.md §2「未在源码确认」）→ 不做前端权限禁用，权限不足由后端拒绝经失败条呈现；
 *  - 框架批量管理已接（CARD-NODE-08，2026-11-10）：批量卡「框架安装」区 + 抽屉「安装 .NET」内联面板
 *    共用 nodeFramework.logic（GET InstallFramework/UninstallFramework，keys=Node 主键逗号串）；
 *    ver「自动（最新）」=传空（NodeFrameworkController.cs:80 留空/auto 自动匹配最新启用安装包，ResolveForPush
 *    唯一解析真源）；卸载 ver 必填无 kind（:134/:137）；高危远程指令 → 本地 GuardModal 二次确认，仅本地栈节点实走；
 *  - 单节点抽屉「升级 Agent」仍无后端端点（NodeController 无对应动作），保持 disabled
 *    并在 title 里说明原因；「接入节点向导」由外壳 A 键接入 Drawer 处理；
 *  - 终端按钮不再 toast：经 ui.store 的 requestTermNode 通道把节点 id 发给外壳，
 *    外壳（index.vue）watch 到后自动展开底部控制台的终端页签；
 *  - NodeStat 是每日全局聚合，没有单节点 CPU 时序 → 抽屉「概览」CPU 走势默认空态「CPU 走势待接入」。
 */
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import PageHead from './PageHead.vue';
import Btn from './Btn.vue';
import Card from './Card.vue';
import Tag from './Tag.vue';
import Dot from './Dot.vue';
import KIcon from './KIcon.vue';
import Chip from './Chip.vue';
import Drawer from './Drawer/Drawer.vue';
import GuardModal from './GuardModal/GuardModal.vue';
import NodeLov from './NodeLov.vue';
import EChart from '../../components/EChart.vue';
import { requestTermNode, openWizard } from './ui.store.ts';
import { useContextReload } from './useContextReload.ts';
import { stardustContext } from '@/stores/stardustContext';
import { H3, TD, TH } from './deployView.styles.ts';
import {
  barLevel,
  buildNodeDataChartOption,
  drawerKv,
  fetchCpuHistory,
  fetchNodeApps,
  fetchNodeData,
  fetchNodes,
  filterConsole,
  layoutNodes,
  relTime,
  toNodeDataChart,
  toneCounts,
  toggleSelect,
  type CpuPoint,
  type NodeAppRow,
  type NodeDataChart,
  type NodeRowView,
  type SkyNode,
  type ToneFilter,
} from './nodesPage.logic.ts';
import { cmdListState, fetchNodeCommands, type CmdListState, type NodeCmdRow } from './nodeCmd.logic.ts';
import {
  BATCH_ACTIONS,
  batchAction,
  buildKeys,
  fetchOnlineIndex,
  guardTexts,
  parseBatchSummary,
  runBatchAction,
  validateBatchInput,
  type BatchActionKey,
  type BatchSummary,
} from './nodeOnline.logic.ts';
import {
  fetchFrameworkKinds,
  fetchFrameworkVersions,
  runFrameworkInstall,
  runFrameworkUninstall,
} from './nodeFramework.logic.ts';
import { RouterLink } from 'vue-router';
import {
  fetchNodeHistory,
  historyListState,
  type HistoryListState,
  type NodeHistoryRow,
  type SuccessFilter,
} from './nodeHistory.logic.ts';

const props = defineProps<{
  /** 测试缝：注入 request；生产不传（gallery 也靠它触发首轮取数，见下方 immediate load） */
  deps?: { request?: Req };
}>();

function req(): Req {
  return props.deps?.request ?? (request as Req);
}

// ---------- 列表状态 ----------
const rows = ref<NodeRowView[]>([]);
const loading = ref(true);
const error = ref('');
const filter = ref<ToneFilter>('all');
const keyword = ref('');
const selected = ref<Set<number>>(new Set());

const counts = computed(() => toneCounts(rows.value));
const filtered = computed(() => filterConsole(rows.value, filter.value, keyword.value));
const skyNodes = computed<SkyNode[]>(() => layoutNodes(rows.value));
const sub = computed(
  () => `${counts.value.running} 台正常 · ${counts.value.high} 台高负载 · ${counts.value.offline} 台离线`,
);

const FILTERS: ReadonlyArray<{ key: ToneFilter; label: string }> = [
  { key: 'all', label: '全部' },
  { key: 'running', label: '正常' },
  { key: 'high', label: '高负载' },
  { key: 'offline', label: '离线' },
];
function filterCount(k: ToneFilter): number {
  if (k === 'all') return counts.value.total;
  if (k === 'running') return counts.value.running;
  if (k === 'high') return counts.value.high;
  return counts.value.offline;
}

async function loadNodes(): Promise<void> {
  loading.value = true;
  error.value = '';
  const r = await fetchNodes(req(), stardustContext.projectId);
  rows.value = r.rows;
  error.value = r.error;
  loading.value = false;
}

// 生产：上下文就绪/切换后取数。gallery 里 stardustContext.ready 默认 false，useContextReload 不会触发；
// 因此当显式注入 deps.request（story/CT）时立即首轮取数。
useContextReload(() => { void loadNodes(); }, stardustContext);
if (props.deps?.request) void loadNodes();

// ---------- 勾选 ----------
function onToggleSelect(id: number): void {
  selected.value = toggleSelect(selected.value, id);
}
function onClearSelection(): void {
  selected.value = new Set();
  batchResult.value = null;
  batchError.value = '';
}

// ---------- 批量运维（CARD-NODE-11，2026-11-09：NodeOnlineController 五动作） ----------
// keys=在线会话 Id（后端 NodeOnline.FindById 主键），勾选 Node.Id 经 /Nodes/NodeOnline 快照映射；
// 重启系统/执行命令先走本地 GuardModal（AppInstances 同款：CT gallery 不挂全局宿主）；
// 前端无权限位数据源 → 不做前端权限禁用，权限不足由后端拒绝经失败条呈现（旅程 S3 如实）。

const batchBusy = ref<Set<string>>(new Set());
const batchError = ref('');
const batchCommand = ref('');
const batchArgument = ref('');
const batchResult = ref<{ summary: BatchSummary; raw: string; missing: number[] } | null>(null);
const batchExpanded = ref(false);
const batchGuard = ref<{ key: BatchActionKey; label: string; detail: string } | null>(null);
const onlineIndex = ref<Map<number, number>>(new Map());

async function ensureOnlineIndex(): Promise<void> {
  if (onlineIndex.value.size > 0) return; // 懒加载一次；doBatch 是唯一调用点（busy 已挡 UI 重入），不判 batchBusy
  const r = await fetchOnlineIndex(req(), stardustContext.projectId);
  onlineIndex.value = r.index;
}

/** 动作入口：先校验（未勾选/未输命令警告入失败条，§11 视线内），guard 动作弹确认 */
function askBatch(key: BatchActionKey): void {
  const check = validateBatchInput(selected.value.size, key === 'Execute' ? batchCommand.value : 'ok');
  if (check) {
    batchError.value = check;
    batchResult.value = null;
    return;
  }
  batchError.value = '';
  const texts = guardTexts(key, selected.value.size, batchCommand.value, batchArgument.value);
  if (texts.label) {
    batchGuard.value = { key, label: texts.label, detail: texts.detail };
    return;
  }
  void doBatch(key);
}

function onBatchGuardCancel(): void {
  batchGuard.value = null;
}
function onBatchGuardConfirm(): void {
  const g = batchGuard.value;
  batchGuard.value = null;
  if (g) void doBatch(g.key);
}

async function doBatch(key: BatchActionKey): Promise<void> {
  const def = batchAction(key);
  batchBusy.value = new Set(BATCH_ACTIONS.map((a) => a.key));
  batchError.value = '';
  batchResult.value = null;
  batchExpanded.value = false;
  try {
    await ensureOnlineIndex();
    const { keys, missing } = buildKeys([...selected.value], onlineIndex.value);
    if (!keys) {
      batchError.value = '勾选节点均无在线会话（在线快照无对应记录），未下发';
      return;
    }
    const extra =
      key === 'Execute'
        ? { command: batchCommand.value.trim(), argument: batchArgument.value.trim() }
        : undefined;
    const r = await runBatchAction(req(), def.path, keys, extra);
    if (r.ok) {
      const summary = parseBatchSummary(r.message);
      batchResult.value = {
        summary: summary ?? { sent: 0, replied: 0, timeout: 0, skipped: null, nodeReplies: [] },
        raw: r.message,
        missing,
      };
    } else {
      batchError.value = `批量操作失败：${r.error}`;
    }
  } finally {
    batchBusy.value = new Set();
  }
}

// ---------- 框架批量管理（CARD-NODE-08，2026-11-10：NodeFrameworkController） ----------
// keys=Node 主键逗号串（SelectKeys :64/:70，与 NODE-11 在线会话 Id 不同，无映射直连）；
// ver「自动（最新）」=传空（:80/:334 ResolveForPush 自动匹配）；卸载 ver 必填无 kind（:134/:137）。
// 高危红线：安装/卸载为远程指令，先弹本地 GuardModal，仅本地栈节点实走（生产/远程仅展示）。
const fwBusy = ref<'install' | 'uninstall' | null>(null);
const fwError = ref('');
const fwResult = ref<{ raw: string; summary: BatchSummary } | null>(null);
const fwExpanded = ref(false);
const fwGuard = ref<null | { op: 'install' | 'uninstall'; ids: number[] }>(null);
const fwVer = ref(''); // 安装版本：''=「自动（最新）」→ ver 传空
const fwKind = ref('auto'); // 安装类型：GetKindList 首项 auto（空/auto 后端等效，Biz :337）
const fwUnVer = ref(''); // 卸载版本：必填（无自动项）
const fwVersions = ref<{ value: string; label: string }[]>([]);
const fwKinds = ref<{ value: string; label: string }[]>([]);
const fwOptionsLoaded = ref(false);
const drawerDotnetOpen = ref(false); // 抽屉单节点框架面板（与批量卡共用 fw* 状态）

/** 版本/类型下拉懒加载：首次展开批量框架区或抽屉面板时拉取（失败给空列表，可重开重试） */
async function ensureFwOptions(): Promise<void> {
  if (fwOptionsLoaded.value) return;
  fwOptionsLoaded.value = true;
  const [v, k] = await Promise.all([fetchFrameworkVersions(req()), fetchFrameworkKinds(req())]);
  fwVersions.value = v;
  fwKinds.value = k;
}

/** 框架操作入口（批量卡/抽屉共用）：卸载先校验版本（C07「请选择框架版本」逐字），再弹本地 GuardModal */
function askFw(op: 'install' | 'uninstall', ids: number[]): void {
  fwError.value = '';
  fwResult.value = null;
  if (op === 'uninstall' && !fwUnVer.value.trim()) {
    fwError.value = '请选择框架版本';
    return;
  }
  void ensureFwOptions();
  fwGuard.value = { op, ids };
}
function askFwBatch(op: 'install' | 'uninstall'): void {
  askFw(op, [...selected.value]);
}
function onFwGuardCancel(): void {
  fwGuard.value = null;
}
function onFwGuardConfirm(): void {
  const g = fwGuard.value;
  fwGuard.value = null;
  if (!g || g.ids.length === 0) return;
  void doFw(g.op, g.ids);
}
async function doFw(op: 'install' | 'uninstall', ids: number[]): Promise<void> {
  fwBusy.value = op;
  fwError.value = '';
  fwResult.value = null;
  fwExpanded.value = false;
  try {
    const r =
      op === 'install'
        ? await runFrameworkInstall(req(), ids, fwVer.value, fwKind.value)
        : await runFrameworkUninstall(req(), ids, fwUnVer.value);
    if (r.error) {
      fwError.value = `框架操作失败：${r.error}`;
      return;
    }
    const summary = parseBatchSummary(r.message);
    fwResult.value = {
      raw: r.message,
      summary: summary ?? { sent: 0, replied: 0, timeout: 0, skipped: null, nodeReplies: [] },
    };
  } finally {
    fwBusy.value = null;
  }
}
function toggleDrawerDotnet(): void {
  drawerDotnetOpen.value = !drawerDotnetOpen.value;
  if (drawerDotnetOpen.value && drawerRow.value) void ensureFwOptions();
}
function askFwDrawer(op: 'install' | 'uninstall'): void {
  if (!drawerRow.value) return;
  askFw(op, [drawerRow.value.id]);
}
/** GuardModal 文案：目标节点名（最多 6 个，超出记「等」）+ 指令语义 + 高危提示（仅本地栈节点） */
const fwGuardDetail = computed(() => {
  if (!fwGuard.value) return '';
  const names = fwGuard.value.ids
    .map((id) => rows.value.find((r) => r.id === id)?.name ?? `#${id}`)
    .slice(0, 6)
    .join('、');
  const more = fwGuard.value.ids.length > 6 ? ' 等' : '';
  return fwGuard.value.op === 'install'
    ? `将向 ${fwGuard.value.ids.length} 台节点（${names}${more}）下发 framework/install 安装指令（版本：${fwVer.value || '自动（最新）'}，类型：${fwKind.value || 'auto'}）。该指令为高危远程操作，请确认目标为本地栈节点。`
    : `将向 ${fwGuard.value.ids.length} 台节点（${names}${more}）下发 framework/uninstall 卸载指令（版本：${fwUnVer.value}）。该指令为高危远程操作，请确认目标为本地栈节点。`;
});
const allFilteredSelected = computed(
  () => filtered.value.length > 0 && filtered.value.every((r) => selected.value.has(r.id)),
);
function onToggleAll(): void {
  const next = new Set(selected.value);
  if (allFilteredSelected.value) {
    for (const r of filtered.value) next.delete(r.id);
  } else {
    for (const r of filtered.value) next.add(r.id);
  }
  selected.value = next;
}

// ---------- 抽屉 ----------
const drawerId = ref<number | null>(null);
const drawerTab = ref<'overview' | 'apps' | 'command' | 'perf' | 'history'>('overview');
const appRows = ref<NodeAppRow[]>([]);
const appsLoading = ref(false);
const appsError = ref('');
const cpuPts = ref<CpuPoint[]>([]);
const cpuLoading = ref(false);
// 「命令」页签（NODE-06 B1）：懒加载——首次切到该页签才请求，打开抽屉不发额外请求
const cmdRows = ref<NodeCmdRow[]>([]);
const cmdLoading = ref(false);
const cmdError = ref('');
const cmdLoaded = ref(false);
const cmdState = computed<CmdListState>(() => cmdListState(cmdLoading.value, cmdError.value, cmdRows.value));
// 「性能」页签（CARD-NODE-07）：懒加载同命令页签；24h 采样换算取全量点（fetchNodeData）→
// LocalTime 升序 + 13 序列按值显隐（toNodeDataChart）→ 单 Y 轴 EChart（buildNodeDataChartOption）；
// 页签内节点搜索 Lov（NodeSearch）切换目标节点并重取，不切抽屉。
const perfChart = ref<NodeDataChart | null>(null);
const perfLoading = ref(false);
const perfError = ref('');
const perfLoaded = ref(false);
const perfTarget = ref<{ id: number; value: string } | null>(null);
const perfChartOption = computed(() => (perfChart.value ? buildNodeDataChartOption(perfChart.value) : {}));
// 「历史」页签（CARD-NODE-09）：懒加载同命令页签；success 过滤传 'true'/'false' 字符串
// （后端 ToBoolean 直读，NodeHistoryController.cs:47）；排序不传参——后端 Id 降序兜底（:52）。
const histRows = ref<NodeHistoryRow[]>([]);
const histLoading = ref(false);
const histError = ref('');
const histLoaded = ref(false);
const histSuccess = ref<SuccessFilter>('');
const histState = computed<HistoryListState>(() => historyListState(histLoading.value, histError.value, histRows.value));

const drawerRow = computed(() => rows.value.find((r) => r.id === drawerId.value) ?? null);
const drawerKvPairs = computed(() => (drawerRow.value ? drawerKv(drawerRow.value) : []));

async function loadDrawerApps(nodeId: number): Promise<void> {
  appsLoading.value = true;
  appsError.value = '';
  const r = await fetchNodeApps(req(), nodeId, stardustContext.projectId);
  appRows.value = r.rows;
  appsError.value = r.error;
  appsLoading.value = false;
}
async function loadCpu(nodeId: number): Promise<void> {
  cpuLoading.value = true;
  const r = await fetchCpuHistory(req(), nodeId, stardustContext.projectId);
  cpuPts.value = r.points;
  cpuLoading.value = false;
}
async function loadDrawerCmds(nodeId: number): Promise<void> {
  cmdLoading.value = true;
  cmdError.value = '';
  const r = await fetchNodeCommands(req(), nodeId, drawerRow.value?.name ?? '', stardustContext.projectId);
  cmdRows.value = r.rows;
  cmdError.value = r.error;
  cmdLoading.value = false;
}
/** 性能数据：period 取节点实体 Period（raw 行兜底；不在抽屉清单里时后端兜底 1440） */
async function loadNodeData(nodeId: number): Promise<void> {
  perfLoading.value = true;
  perfError.value = '';
  const rawPeriod = (drawerRow.value?.raw as Record<string, unknown> | undefined)?.period;
  const period = rawPeriod === undefined || rawPeriod === null || rawPeriod === '' ? null : Number(rawPeriod);
  const r = await fetchNodeData(req(), nodeId, period, stardustContext.projectId);
  perfError.value = r.error;
  perfChart.value = r.error ? null : toNodeDataChart(r.rows);
  perfLoading.value = false;
}
async function loadNodeHistory(nodeId: number): Promise<void> {
  histLoading.value = true;
  histError.value = '';
  const r = await fetchNodeHistory(req(), nodeId, drawerRow.value?.name ?? '', histSuccess.value, stardustContext.projectId);
  histRows.value = r.rows;
  histError.value = r.error;
  histLoading.value = false;
}
/** 页签切换：命令页签懒加载（仅首次；失败重试复用同一函数） */
function setTab(tab: 'overview' | 'apps' | 'command' | 'perf' | 'history'): void {
  drawerTab.value = tab;
  if (tab === 'command' && !cmdLoaded.value && drawerId.value !== null) {
    cmdLoaded.value = true;
    void loadDrawerCmds(drawerId.value);
  }
  if (tab === 'perf' && !perfLoaded.value && drawerId.value !== null) {
    perfLoaded.value = true;
    void loadNodeData(drawerId.value);
  }
  if (tab === 'history' && !histLoaded.value && drawerId.value !== null) {
    histLoaded.value = true;
    void loadNodeHistory(drawerId.value);
  }
}
/** 命令列表失败重试（ASYNC 反馈规范：重试期间 loading 自动接手） */
function retryCmds(): void {
  if (drawerId.value !== null) void loadDrawerCmds(drawerId.value);
}
/** 性能曲线失败重试 */
function retryNodeData(): void {
  if (drawerId.value !== null) void loadNodeData(drawerId.value);
}
/** 页签内节点搜索 Lov 选中：切换目标节点并重取性能数据（抽屉不切换） */
function onLovSelect(item: { id: number; value: string }): void {
  perfTarget.value = item;
  void loadNodeData(item.id);
}
/** 历史列表失败重试 + 状态过滤重取（ASYNC 反馈规范：重试期间 loading 自动接手） */
function retryHistory(): void {
  if (drawerId.value !== null) void loadNodeHistory(drawerId.value);
}
function onHistSuccessChange(): void {
  retryHistory();
}
function openNode(id: number): void {
  drawerId.value = id;
  drawerTab.value = 'overview';
  appRows.value = [];
  appsError.value = '';
  cpuPts.value = [];
  cmdRows.value = [];
  cmdError.value = '';
  cmdLoaded.value = false;
  perfChart.value = null;
  perfError.value = '';
  perfLoaded.value = false;
  perfTarget.value = null;
  histRows.value = [];
  histError.value = '';
  histLoaded.value = false;
  histSuccess.value = '';
  void loadDrawerApps(id);
  void loadCpu(id);
}
function closeDrawer(): void {
  drawerId.value = null;
  drawerDotnetOpen.value = false;
}

/** 终端：经模块级通道把节点 id 发给外壳，外壳自动展开终端页签（见 ui.store.ts TermNodeRequest 注释） */
function openTerm(id: number): void {
  requestTermNode(id);
}

// ---------- 表格小工具 ----------
function barColor(pct: number | null): string {
  const lvl = barLevel(pct);
  if (!lvl) return 'transparent';
  if (lvl === 'ok') return cssVar('--el-color-success');
  if (lvl === 'warn') return cssVar('--el-color-warning');
  return cssVar('--el-color-danger');
}
function pctText(pct: number | null): string {
  return pct === null ? '—' : `${pct.toFixed(0)}%`;
}

// ---------- 星图 canvas ----------
const canvasRef = ref<HTMLCanvasElement | null>(null);
const mousePt = ref<{ x: number; y: number } | null>(null);
const hovNode = ref<SkyNode | null>(null);

function cssVar(name: string): string {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
}

function draw(): void {
  const cv = canvasRef.value;
  if (!cv) return;
  const ctx = cv.getContext('2d');
  if (!ctx) return;
  const dpr = window.devicePixelRatio || 1;
  const W = cv.clientWidth;
  const H = cv.clientHeight;
  if (W <= 0 || H <= 0) return;
  cv.width = Math.floor(W * dpr);
  cv.height = Math.floor(H * dpr);
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  ctx.clearRect(0, 0, W, H);

  const ok = cssVar('--el-color-success');
  const warn = cssVar('--el-color-warning');
  const mut = cssVar('--el-text-color-secondary');
  const line = cssVar('--el-border-color-light');
  const ink = cssVar('--el-text-color-primary');

  // 背景星尘（确定性派生，不随机，CT 截图稳定）
  ctx.fillStyle = line;
  for (let i = 0; i < 60; i += 1) {
    const x = (((i * 137) % 100) / 100) * W;
    const y = (((i * 89) % 100) / 100) * H;
    ctx.globalAlpha = 0.3;
    ctx.beginPath();
    ctx.arc(x, y, 0.6 + ((i * 7) % 10) * 0.12, 0, 6.283);
    ctx.fill();
  }
  ctx.globalAlpha = 1;

  // 按集群分组连线 + 集群标签
  const byCluster = new Map<string, SkyNode[]>();
  for (const s of skyNodes.value) {
    const row = rows.value.find((r) => r.id === s.id);
    const c = row?.cluster || '默认集群';
    const arr = byCluster.get(c);
    if (arr) arr.push(s);
    else byCluster.set(c, [s]);
  }
  ctx.strokeStyle = line;
  ctx.lineWidth = 1;
  for (const [cluster, nodes] of byCluster) {
    if (nodes.length > 1) {
      ctx.beginPath();
      nodes.forEach((s, i) => {
        const x = s.x * W;
        const y = s.y * H;
        if (i === 0) ctx.moveTo(x, y);
        else ctx.lineTo(x, y);
      });
      ctx.stroke();
    }
    ctx.fillStyle = mut;
    ctx.font = '500 10px "JetBrains Mono", monospace';
    ctx.fillText(cluster.slice(0, 6).toUpperCase(), Math.max(4, nodes[0].x * W - 14), H - 8);
  }

  // 命中检测
  let hovered: SkyNode | null = null;
  const m = mousePt.value;
  if (m) {
    for (const s of skyNodes.value) {
      const x = s.x * W;
      const y = s.y * H;
      if (Math.hypot(m.x - x, m.y - y) < 15) {
        hovered = s;
        break;
      }
    }
  }

  for (const s of skyNodes.value) {
    const x = s.x * W;
    const y = s.y * H;
    const col = s.tone === 'offline' ? mut : s.tone === 'high' ? warn : ok;
    const R = 3 + (s.cpu ?? 30) / 26;

    ctx.beginPath();
    ctx.arc(x, y, R, 0, 6.283);
    if (s.tone === 'offline') {
      ctx.strokeStyle = mut;
      ctx.lineWidth = 1.2;
      ctx.setLineDash([3, 3]);
      ctx.stroke();
      ctx.setLineDash([]);
    } else {
      ctx.fillStyle = col;
      ctx.shadowColor = col;
      ctx.shadowBlur = s.tone === 'high' ? 12 : 6;
      ctx.fill();
      ctx.shadowBlur = 0;
    }

    if (hovered?.id === s.id) {
      ctx.strokeStyle = ink;
      ctx.lineWidth = 1.2;
      ctx.beginPath();
      ctx.arc(x, y, R + 6, 0, 6.283);
      ctx.stroke();
    }
  }

  hovNode.value = hovered;
  cv.style.cursor = hovered ? 'pointer' : 'default';
}

function onCanvasMove(e: MouseEvent): void {
  const cv = canvasRef.value;
  if (!cv) return;
  const rect = cv.getBoundingClientRect();
  mousePt.value = { x: e.clientX - rect.left, y: e.clientY - rect.top };
  draw();
}
function onCanvasLeave(): void {
  mousePt.value = null;
  hovNode.value = null;
  draw();
}
function onCanvasClick(): void {
  if (hovNode.value) openNode(hovNode.value.id);
}
function onResize(): void {
  draw();
}

onMounted(() => {
  draw();
  window.addEventListener('resize', onResize);
});
onBeforeUnmount(() => {
  window.removeEventListener('resize', onResize);
});
watch(skyNodes, () => draw());
// 批量卡一出现就预拉框架版本/类型下拉（CARD-NODE-08：点击安装/卸载时才拉会晚于用户操作下拉）
watch(() => selected.value.size, (n) => { if (n > 0) void ensureFwOptions(); });
</script>

<template>
  <div data-spec="nodes-page">
    <PageHead
      eyebrow="节点 · 每台装着 StarAgent 的机器是一颗星，应用是它身边的尘"
      title="星图"
      :sub="sub"
    >
      <template #actions>
        <RouterLink
          to="/nodes/agent-deploy"
          data-spec="nodes-agent-deploy-entry"
          class="mr-[8px] inline-flex h-[28px] items-center rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[12px] text-[12px] text-[var(--el-text-color-regular)] hover:border-[var(--el-color-primary)] hover:text-[var(--el-color-primary)]"
        >
          远程部署
        </RouterLink>
        <Btn
          variant="primary"
          size="sm"
          data-spec="nodes-connect"
          @click="openWizard"
        >
          <template #icon>
            <KIcon
              name="Plus"
              :size="13"
            />
          </template>
          接入节点
        </Btn>
      </template>
    </PageHead>

    <!-- 星图 canvas -->
    <Card
      padding="none"
      data-spec="nodes-sky"
      class="mb-4"
    >
      <div
        class="relative"
        data-spec="nodes-sky-wrap"
      >
        <canvas
          ref="canvasRef"
          class="block h-[280px] w-full"
          data-spec="nodes-sky-canvas"
          @mousemove="onCanvasMove"
          @mouseleave="onCanvasLeave"
          @click="onCanvasClick"
        />
        <!-- hover tooltip -->
        <div
          v-if="hovNode"
          data-spec="nodes-sky-tip"
          class="pointer-events-none absolute z-10 min-w-[140px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] py-[8px] text-[11.5px] text-[var(--el-text-color-primary)] shadow-[var(--el-box-shadow)]"
          :style="{ left: `${Math.min(92, hovNode.x * 100)}%`, top: `${Math.max(2, hovNode.y * 100)}%`, transform: 'translate(14px, -40px)' }"
        >
          <div class="font-medium">
            {{ hovNode.name }}
            <span class="text-[var(--el-text-color-secondary)]">· CPU {{ hovNode.cpu === null ? '—' : `${hovNode.cpu}%` }} · {{ hovNode.services }} 个应用</span>
          </div>
          <div class="mt-[2px] font-mono text-[10.5px] text-[var(--el-text-color-secondary)]">
            {{ hovNode.ip }}
          </div>
        </div>
        <!-- 空态 -->
        <div
          v-if="!loading && !error && rows.length === 0"
          data-spec="nodes-sky-empty"
          class="absolute inset-0 grid place-items-center text-[12.5px] text-[var(--el-text-color-secondary)]"
        >
          还没有节点
        </div>
      </div>
    </Card>

    <!-- 过滤条 -->
    <div
      class="mb-4 flex flex-wrap items-center gap-[6px]"
      data-spec="nodes-filter"
    >
      <Chip
        v-for="f in FILTERS"
        :key="f.key"
        :on="filter === f.key"
        data-spec="nodes-filter-chip"
        :data-key="f.key"
        @click="filter = f.key"
      >
        {{ f.label }} {{ filterCount(f.key) }}
      </Chip>
      <span class="flex-1" />
      <div class="relative">
        <span class="pointer-events-none absolute left-[9px] top-1/2 -translate-y-1/2 text-[var(--el-text-color-placeholder)]">
          <KIcon
            name="Search"
            :size="13"
          />
        </span>
        <input
          v-model="keyword"
          type="text"
          placeholder="按名称 / IP 过滤"
          data-spec="nodes-search"
          class="h-[28px] w-[220px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] pl-[28px] pr-[10px] text-[12px] text-[var(--el-text-color-primary)] outline-none placeholder:text-[var(--el-text-color-placeholder)] focus:border-[var(--el-color-primary)]"
        >
      </div>
    </div>

    <!-- 失败 -->
    <div
      v-if="error"
      data-spec="nodes-error"
      class="mb-4 rounded-[8px] border border-[color-mix(in_oklab,var(--el-color-danger)_35%,transparent)] bg-[color-mix(in_oklab,var(--el-color-danger)_8%,transparent)] px-[14px] py-[10px] text-[12.5px] text-[var(--el-color-danger)]"
    >
      节点加载失败：{{ error }}
      <button
        type="button"
        data-spec="nodes-retry"
        class="ml-[8px] cursor-pointer underline"
        @click="loadNodes"
      >
        重试
      </button>
    </div>

    <!-- 批量操作卡（CARD-NODE-11 五动作：GET+keys=在线会话 Id，nodeOnline.logic） -->
    <Card
      v-if="selected.size > 0"
      padding="pad"
      data-spec="nodes-batch"
      class="mb-4"
    >
      <div class="flex flex-wrap items-center gap-[8px]">
        <b class="text-[12.5px] text-[var(--el-text-color-primary)]">已选 {{ selected.size }} 台</b>
        <Btn
          v-for="a in BATCH_ACTIONS"
          :key="a.key"
          size="sm"
          :disabled="batchBusy.size > 0"
          :title="batchBusy.size > 0 ? '下发中…' : undefined"
          :data-spec="`node-online-batch-${a.key.toLowerCase()}`"
          @click="askBatch(a.key)"
        >
          <span
            v-if="batchBusy.has(a.key)"
            class="mr-[4px] inline-block h-[10px] w-[10px] animate-spin rounded-full border border-current border-t-transparent align-[-1px]"
          />
          {{ a.label }}
        </Btn>
        <input
          v-model="batchCommand"
          data-spec="node-online-batch-command"
          :disabled="batchBusy.size > 0"
          placeholder="命令（如 deploy/start）"
          class="h-[26px] w-[190px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-fill-color-blank)] px-[8px] text-[12px] text-[var(--el-text-color-primary)] placeholder:text-[var(--el-text-color-placeholder)]"
        >
        <input
          v-model="batchArgument"
          data-spec="node-online-batch-argument"
          :disabled="batchBusy.size > 0"
          placeholder="参数（JSON，可选）"
          class="h-[26px] w-[190px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-fill-color-blank)] px-[8px] text-[12px] text-[var(--el-text-color-primary)] placeholder:text-[var(--el-text-color-placeholder)]"
        >
        <span class="flex-1" />
        <Btn
          size="sm"
          variant="ghost"
          data-spec="nodes-batch-clear"
          @click="onClearSelection"
        >
          取消
        </Btn>
      </div>

      <!-- 框架安装区（CARD-NODE-08：NodeFrameworkController，keys=Node 主键；ver「自动（最新）」=传空，:80/:334） -->
      <div
        class="mt-[10px] flex flex-wrap items-center gap-[8px] border-t border-[var(--el-border-color-light)] pt-[10px]"
        data-spec="node-framework-batch"
      >
        <b class="text-[12.5px] text-[var(--el-text-color-primary)]">框架安装</b>
        <select
          v-model="fwVer"
          data-spec="node-framework-batch-ver"
          :disabled="fwBusy !== null"
          title="「自动（最新）」= 后端自动匹配最新启用安装包"
          class="h-[26px] w-[140px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-fill-color-blank)] px-[8px] text-[12px] text-[var(--el-text-color-primary)]"
        >
          <option value="">
            自动（最新）
          </option>
          <option
            v-for="o in fwVersions"
            :key="o.value"
            :value="o.value"
          >
            {{ o.label }}
          </option>
        </select>
        <select
          v-model="fwKind"
          data-spec="node-framework-batch-kind"
          :disabled="fwBusy !== null"
          title="「自动」= 按节点自动判断安装类型"
          class="h-[26px] w-[110px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-fill-color-blank)] px-[8px] text-[12px] text-[var(--el-text-color-primary)]"
        >
          <option
            v-for="o in fwKinds"
            :key="o.value"
            :value="o.value"
          >
            {{ o.label }}
          </option>
        </select>
        <Btn
          size="sm"
          :disabled="fwBusy !== null"
          :title="fwBusy ? '下发中…' : undefined"
          data-spec="node-framework-batch-install"
          @click="askFwBatch('install')"
        >
          <span
            v-if="fwBusy === 'install'"
            class="mr-[4px] inline-block h-[10px] w-[10px] animate-spin rounded-full border border-current border-t-transparent align-[-1px]"
          />
          安装 .NET
        </Btn>
        <select
          v-model="fwUnVer"
          data-spec="node-framework-batch-unver"
          :disabled="fwBusy !== null"
          title="卸载必须指定版本（后端 ver 必填，无自动项）"
          class="h-[26px] w-[140px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-fill-color-blank)] px-[8px] text-[12px] text-[var(--el-text-color-primary)]"
        >
          <option value="">
            请选择版本
          </option>
          <option
            v-for="o in fwVersions"
            :key="o.value"
            :value="o.value"
          >
            {{ o.label }}
          </option>
        </select>
        <Btn
          size="sm"
          :disabled="fwBusy !== null"
          :title="fwBusy ? '下发中…' : undefined"
          data-spec="node-framework-batch-uninstall"
          @click="askFwBatch('uninstall')"
        >
          <span
            v-if="fwBusy === 'uninstall'"
            class="mr-[4px] inline-block h-[10px] w-[10px] animate-spin rounded-full border border-current border-t-transparent align-[-1px]"
          />
          卸载框架
        </Btn>
      </div>

      <!-- 框架失败条（C07 警告逐字 / §11 失败可见，不走 toast） -->
      <div
        v-if="fwError"
        data-spec="node-framework-error"
        class="mt-[10px] rounded-[8px] border border-[color-mix(in_oklab,var(--el-color-danger)_35%,transparent)] bg-[color-mix(in_oklab,var(--el-color-danger)_8%,transparent)] px-[12px] py-[8px] text-[12.5px] text-[var(--el-color-danger)]"
      >
        {{ fwError }}
      </div>

      <!-- 框架结果汇总条：契约首行逐字 + 可展开逐节点回复（与 NODE-11 同形） -->
      <div
        v-if="fwResult"
        data-spec="node-framework-result-bar"
        class="mt-[10px] rounded-[8px] border border-[color-mix(in_oklab,var(--el-color-success)_35%,transparent)] bg-[color-mix(in_oklab,var(--el-color-success)_8%,transparent)] px-[12px] py-[8px] text-[12.5px] text-[var(--el-text-color-primary)]"
      >
        <span data-spec="node-framework-result-summary">{{ fwResult.raw.split('\n')[0] }}</span>
        <button
          v-if="fwResult.summary.nodeReplies.length"
          type="button"
          data-spec="node-framework-result-toggle"
          class="ml-[10px] cursor-pointer underline"
          @click="fwExpanded = !fwExpanded"
        >
          {{ fwExpanded ? '收起回复' : `展开 ${fwResult.summary.nodeReplies.length} 条节点回复` }}
        </button>
        <div
          v-if="fwExpanded"
          class="mt-[6px] flex flex-col gap-[3px] border-t border-[var(--el-border-color)] pt-[6px]"
        >
          <div
            v-for="(r, i) in fwResult.summary.nodeReplies"
            :key="i"
            data-spec="node-framework-result-row"
            class="flex gap-[8px]"
          >
            <b class="shrink-0">{{ r.name }}</b>
            <span class="break-all text-[var(--el-text-color-regular)]">{{ r.text }}</span>
          </div>
        </div>
      </div>

      <!-- 拦截/失败条（§11：失败可见，不走 toast） -->
      <div
        v-if="batchError"
        data-spec="node-online-batch-error"
        class="mt-[10px] rounded-[8px] border border-[color-mix(in_oklab,var(--el-color-danger)_35%,transparent)] bg-[color-mix(in_oklab,var(--el-color-danger)_8%,transparent)] px-[12px] py-[8px] text-[12.5px] text-[var(--el-color-danger)]"
      >
        {{ batchError }}
      </div>

      <!-- 结果汇总条：后端契约首行逐字 + 可展开逐节点回复（契约展开行，不走 ElMessage 简版） -->
      <div
        v-if="batchResult"
        data-spec="node-online-result-bar"
        class="mt-[10px] rounded-[8px] border border-[color-mix(in_oklab,var(--el-color-success)_35%,transparent)] bg-[color-mix(in_oklab,var(--el-color-success)_8%,transparent)] px-[12px] py-[8px] text-[12.5px] text-[var(--el-text-color-primary)]"
      >
        <span data-spec="node-online-result-summary">{{ batchResult.raw.split('\n')[0] }}</span>
        <span
          v-if="batchResult.missing.length"
          data-spec="node-online-result-missing"
          class="ml-[6px] text-[var(--el-text-color-secondary)]"
        >（{{ batchResult.missing.length }} 台勾选节点无在线会话，未下发）</span>
        <button
          v-if="batchResult.summary.nodeReplies.length"
          type="button"
          data-spec="node-online-result-toggle"
          class="ml-[10px] cursor-pointer underline"
          @click="batchExpanded = !batchExpanded"
        >
          {{ batchExpanded ? '收起回复' : `展开 ${batchResult.summary.nodeReplies.length} 条节点回复` }}
        </button>
        <div
          v-if="batchExpanded"
          class="mt-[6px] flex flex-col gap-[3px] border-t border-[var(--el-border-color)] pt-[6px]"
        >
          <div
            v-for="(r, i) in batchResult.summary.nodeReplies"
            :key="i"
            data-spec="node-online-result-row"
            class="flex gap-[8px]"
          >
            <b class="shrink-0">{{ r.name }}</b>
            <span class="break-all text-[var(--el-text-color-regular)]">{{ r.text }}</span>
          </div>
        </div>
      </div>
    </Card>

    <!-- 批量二次确认（本地 GuardModal：CT gallery 不挂全局宿主，AppInstances 同款） -->
    <GuardModal
      v-if="batchGuard"
      :label="batchGuard.label"
      :detail="batchGuard.detail"
      :data-spec="`node-online-confirm-${batchGuard.key.toLowerCase()}`"
      @cancel="onBatchGuardCancel"
      @confirm="onBatchGuardConfirm"
    />

    <!-- 框架高危二次确认（安装/卸载远程指令，仅本地栈节点实走；与 NODE-11 同形 GuardModal） -->
    <GuardModal
      v-if="fwGuard"
      :label="fwGuard.op === 'install' ? '批量安装 .NET 框架' : '批量卸载 .NET 框架'"
      :detail="fwGuardDetail"
      :data-spec="`node-framework-confirm-${fwGuard.op}`"
      @cancel="onFwGuardCancel"
      @confirm="onFwGuardConfirm"
    />

    <!-- 节点表格 -->
    <Card
      padding="none"
      data-spec="nodes-table-card"
    >
      <div
        v-if="loading"
        data-spec="nodes-loading"
        class="py-12 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
      >
        <span class="mr-[6px] inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)] align-[-3px]" />
        节点加载中…
      </div>
      <table
        v-else
        class="w-full border-collapse"
        data-spec="nodes-table"
      >
        <thead>
          <tr>
            <th :class="[TH, 'w-[34px]']">
              <input
                type="checkbox"
                data-spec="nodes-check-all"
                :checked="allFilteredSelected"
                @change="onToggleAll"
              >
            </th>
            <th :class="TH">
              节点
            </th>
            <th :class="TH">
              系统
            </th>
            <th :class="[TH, 'text-right']">
              CPU
            </th>
            <th :class="[TH, 'text-right']">
              内存
            </th>
            <th :class="[TH, 'text-right']">
              应用
            </th>
            <th :class="TH">
              心跳
            </th>
            <th :class="[TH, 'w-[90px]']">
              终端
            </th>
          </tr>
        </thead>
        <tbody class="group">
          <tr
            v-for="r in filtered"
            :key="r.id"
            data-spec="nodes-row"
            :data-id="r.id"
            :data-tone="r.tone"
            class="cursor-pointer hover:bg-[var(--el-bg-color-overlay)]"
            @click="openNode(r.id)"
          >
            <td
              :class="TD"
              @click.stop
            >
              <input
                type="checkbox"
                data-spec="nodes-row-check"
                :checked="selected.has(r.id)"
                @change="onToggleSelect(r.id)"
              >
            </td>
            <td :class="TD">
              <div class="flex items-center gap-[8px]">
                <Dot :tone="r.tone === 'high' ? 'warn' : r.tone === 'offline' ? 'off' : 'ok'" />
                <div class="min-w-0">
                  <div class="text-[12.5px] font-medium text-[var(--el-text-color-primary)]">
                    {{ r.name }}
                  </div>
                  <div class="font-mono text-[11px] text-[var(--el-text-color-secondary)]">
                    {{ r.ip }}
                  </div>
                </div>
              </div>
            </td>
            <td :class="TD">
              <div class="text-[11.5px] text-[var(--el-text-color-secondary)]">
                {{ r.os || '—' }}
              </div>
              <div class="mt-[3px] flex items-center gap-[4px]">
                <Tag tone="default">
                  {{ r.agentVer || '—' }}
                </Tag>
              </div>
            </td>
            <td :class="[TD, 'text-right']">
              <span
                v-if="r.tone !== 'offline' && r.cpu !== null"
                class="inline-flex items-center justify-end gap-[6px]"
              >
                <span class="h-[6px] w-[60px] overflow-hidden rounded-full bg-[var(--el-fill-color)]">
                  <span
                    class="block h-full rounded-full"
                    :style="{ width: `${Math.min(100, r.cpu)}%`, backgroundColor: barColor(r.cpu) }"
                  />
                </span>
                <span class="font-mono text-[11.5px] tabular-nums text-[var(--el-text-color-primary)]">{{ pctText(r.cpu) }}</span>
              </span>
              <span
                v-else
                class="font-mono text-[11.5px] text-[var(--el-text-color-secondary)]"
              >
                —
              </span>
            </td>
            <td :class="[TD, 'text-right']">
              <span
                v-if="r.tone !== 'offline' && r.mem !== null"
                class="inline-flex items-center justify-end gap-[6px]"
              >
                <span class="h-[6px] w-[60px] overflow-hidden rounded-full bg-[var(--el-fill-color)]">
                  <span
                    class="block h-full rounded-full"
                    :style="{ width: `${Math.min(100, r.mem)}%`, backgroundColor: barColor(r.mem) }"
                  />
                </span>
                <span class="font-mono text-[11.5px] tabular-nums text-[var(--el-text-color-primary)]">{{ pctText(r.mem) }}</span>
              </span>
              <span
                v-else
                class="font-mono text-[11.5px] text-[var(--el-text-color-secondary)]"
              >
                —
              </span>
            </td>
            <td :class="[TD, 'text-right']">
              <span class="font-mono text-[11.5px] tabular-nums text-[var(--el-text-color-secondary)]">{{ r.services }}</span>
            </td>
            <td :class="TD">
              <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">{{ relTime(r.lastActive, Date.now()) }}</span>
            </td>
            <td :class="TD">
              <Btn
                size="sm"
                variant="ghost"
                data-spec="nodes-row-terminal"
                @click.stop="openTerm(r.id)"
              >
                终端
              </Btn>
            </td>
          </tr>
          <tr v-if="!filtered.length">
            <td
              colspan="8"
              class="px-[14px] py-[24px] text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
              data-spec="nodes-empty"
            >
              {{ loading ? '节点加载中…' : '没有匹配的节点' }}
            </td>
          </tr>
        </tbody>
      </table>
    </Card>

    <!-- 节点抽屉 -->
    <Drawer
      :open="drawerRow !== null"
      :title="drawerRow?.name ?? ''"
      :sub="drawerRow ? `${drawerRow.ip} · ${drawerRow.cluster}` : ''"
      @close="closeDrawer"
    >
      <template #actions>
        <!-- CARD-NODE-09：迁移页深链兜底入口（新窗口 /Nodes/Node?Id=，与迁移页节点列下钻同目标，C04 断言零改写两全） -->
        <a
          v-if="drawerRow"
          :href="`/Nodes/Node?Id=${drawerRow.id}`"
          target="_blank"
          rel="noopener"
          data-spec="nodes-drawer-open-migration"
          class="mr-[4px] self-center text-[12px] text-[var(--el-color-primary)] hover:underline"
        >在迁移页打开</a>
        <Btn
          variant="primary"
          size="sm"
          data-spec="nodes-drawer-terminal"
          @click="drawerRow && openTerm(drawerRow.id)"
        >
          <template #icon>
            <KIcon
              name="Terminal"
              :size="12"
            />
          </template>
          打开终端
        </Btn>
        <Btn
          size="sm"
          disabled
          title="后端暂未提供批量操作接口"
          data-spec="nodes-drawer-upgrade"
        >
          升级 Agent
        </Btn>
        <Btn
          size="sm"
          :data-spec="'nodes-drawer-dotnet'"
          @click="toggleDrawerDotnet"
        >
          安装 .NET
        </Btn>
      </template>

      <!-- 单节点框架面板（CARD-NODE-08：与批量卡共用 fw* 状态；keys=该节点主键，无在线映射） -->
      <div
        v-if="drawerDotnetOpen && drawerRow"
        data-spec="nodes-drawer-dotnet-panel"
        class="mb-3 rounded-[8px] border border-[var(--el-border-color)] p-[10px]"
      >
        <div class="mb-[8px] flex items-center justify-between">
          <b class="text-[12.5px] text-[var(--el-text-color-primary)]">框架管理（{{ drawerRow.name }}）</b>
          <button
            type="button"
            class="cursor-pointer text-[12px] text-[var(--el-text-color-secondary)] hover:text-[var(--el-text-color-primary)]"
            @click="drawerDotnetOpen = false"
          >
            收起
          </button>
        </div>
        <div class="flex flex-wrap items-center gap-[8px]">
          <select
            v-model="fwVer"
            data-spec="node-framework-batch-ver"
            :disabled="fwBusy !== null"
            title="「自动（最新）」= 后端自动匹配最新启用安装包"
            class="h-[26px] w-[140px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-fill-color-blank)] px-[8px] text-[12px] text-[var(--el-text-color-primary)]"
          >
            <option value="">
              自动（最新）
            </option>
            <option
              v-for="o in fwVersions"
              :key="o.value"
              :value="o.value"
            >
              {{ o.label }}
            </option>
          </select>
          <select
            v-model="fwKind"
            data-spec="node-framework-batch-kind"
            :disabled="fwBusy !== null"
            title="「自动」= 按节点自动判断安装类型"
            class="h-[26px] w-[110px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-fill-color-blank)] px-[8px] text-[12px] text-[var(--el-text-color-primary)]"
          >
            <option
              v-for="o in fwKinds"
              :key="o.value"
              :value="o.value"
            >
              {{ o.label }}
            </option>
          </select>
          <Btn
            size="sm"
            :disabled="fwBusy !== null"
            :title="fwBusy ? '下发中…' : undefined"
            data-spec="node-framework-drawer-install"
            @click="askFwDrawer('install')"
          >
            <span
              v-if="fwBusy === 'install'"
              class="mr-[4px] inline-block h-[10px] w-[10px] animate-spin rounded-full border border-current border-t-transparent align-[-1px]"
            />
            安装 .NET
          </Btn>
          <select
            v-model="fwUnVer"
            data-spec="node-framework-batch-unver"
            :disabled="fwBusy !== null"
            title="卸载必须指定版本（后端 ver 必填，无自动项）"
            class="h-[26px] w-[140px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-fill-color-blank)] px-[8px] text-[12px] text-[var(--el-text-color-primary)]"
          >
            <option value="">
              请选择版本
            </option>
            <option
              v-for="o in fwVersions"
              :key="o.value"
              :value="o.value"
            >
              {{ o.label }}
            </option>
          </select>
          <Btn
            size="sm"
            :disabled="fwBusy !== null"
            :title="fwBusy ? '下发中…' : undefined"
            data-spec="node-framework-drawer-uninstall"
            @click="askFwDrawer('uninstall')"
          >
            <span
              v-if="fwBusy === 'uninstall'"
              class="mr-[4px] inline-block h-[10px] w-[10px] animate-spin rounded-full border border-current border-t-transparent align-[-1px]"
            />
            卸载框架
          </Btn>
        </div>
        <!-- 面板内失败/结果条（与批量卡共用 fwError/fwResult，两处渲染） -->
        <div
          v-if="fwError"
          data-spec="node-framework-error"
          class="mt-[8px] rounded-[8px] border border-[color-mix(in_oklab,var(--el-color-danger)_35%,transparent)] bg-[color-mix(in_oklab,var(--el-color-danger)_8%,transparent)] px-[12px] py-[8px] text-[12.5px] text-[var(--el-color-danger)]"
        >
          {{ fwError }}
        </div>
        <div
          v-if="fwResult"
          data-spec="node-framework-result-bar"
          class="mt-[8px] rounded-[8px] border border-[color-mix(in_oklab,var(--el-color-success)_35%,transparent)] bg-[color-mix(in_oklab,var(--el-color-success)_8%,transparent)] px-[12px] py-[8px] text-[12.5px] text-[var(--el-text-color-primary)]"
        >
          <span data-spec="node-framework-result-summary">{{ fwResult.raw.split('\n')[0] }}</span>
        </div>
      </div>

      <!-- tab 切换 -->
      <div
        class="mb-3 flex gap-[2px] border-b border-[var(--el-border-color-light)]"
        data-spec="nodes-drawer-tabs"
      >
        <button
          type="button"
          data-spec="nodes-drawer-tab"
          data-key="overview"
          :class="[
            '-mb-px h-[34px] border-b-2 border-solid px-[13px] text-[13px] font-medium transition-colors',
            drawerTab === 'overview'
              ? 'border-b-[var(--el-color-primary)] text-[var(--el-color-primary-light-3)]'
              : 'border-b-transparent text-[var(--el-text-color-secondary)] hover:text-[var(--el-text-color-primary)]',
          ]"
          @click="drawerTab = 'overview'"
        >
          概览
        </button>
        <button
          type="button"
          data-spec="nodes-drawer-tab"
          data-key="apps"
          :class="[
            '-mb-px h-[34px] border-b-2 border-solid px-[13px] text-[13px] font-medium transition-colors',
            drawerTab === 'apps'
              ? 'border-b-[var(--el-color-primary)] text-[var(--el-color-primary-light-3)]'
              : 'border-b-transparent text-[var(--el-text-color-secondary)] hover:text-[var(--el-text-color-primary)]',
          ]"
          @click="drawerTab = 'apps'"
        >
          应用进程 {{ appRows.length }}
        </button>
        <button
          type="button"
          data-spec="nodes-drawer-tab"
          data-key="command"
          :class="[
            '-mb-px h-[34px] border-b-2 border-solid px-[13px] text-[13px] font-medium transition-colors',
            drawerTab === 'command'
              ? 'border-b-[var(--el-color-primary)] text-[var(--el-color-primary-light-3)]'
              : 'border-b-transparent text-[var(--el-text-color-secondary)] hover:text-[var(--el-text-color-primary)]',
          ]"
          @click="setTab('command')"
        >
          命令
        </button>
        <button
          type="button"
          data-spec="nodes-drawer-tab"
          data-key="perf"
          :class="[
            '-mb-px h-[34px] border-b-2 border-solid px-[13px] text-[13px] font-medium transition-colors',
            drawerTab === 'perf'
              ? 'border-b-[var(--el-color-primary)] text-[var(--el-color-primary-light-3)]'
              : 'border-b-transparent text-[var(--el-text-color-secondary)] hover:text-[var(--el-text-color-primary)]',
          ]"
          @click="setTab('perf')"
        >
          性能
        </button>
        <button
          type="button"
          data-spec="nodes-drawer-tab"
          data-key="history"
          :class="[
            '-mb-px h-[34px] border-b-2 border-solid px-[13px] text-[13px] font-medium transition-colors',
            drawerTab === 'history'
              ? 'border-b-[var(--el-color-primary)] text-[var(--el-color-primary-light-3)]'
              : 'border-b-transparent text-[var(--el-text-color-secondary)] hover:text-[var(--el-text-color-primary)]',
          ]"
          @click="setTab('history')"
        >
          历史
        </button>
      </div>

      <!-- 概览 tab -->
      <div v-if="drawerTab === 'overview'">
        <Card
          padding="pad"
          class="mb-3"
          data-spec="nodes-drawer-cpu"
        >
          <h3 :class="H3">
            CPU %
          </h3>
          <div
            v-if="cpuLoading"
            class="py-6 text-center text-[12px] text-[var(--el-text-color-secondary)]"
          >
            <span class="mr-[6px] inline-block h-[13px] w-[13px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)] align-[-3px]" />
            走势加载中…
          </div>
          <div
            v-else-if="cpuPts.length > 0"
            class="py-2 text-[12px] text-[var(--el-text-color-secondary)]"
          >
            {{ cpuPts.length }} 个采样点（示意）
          </div>
          <div
            v-else
            data-spec="nodes-drawer-cpu-empty"
            class="py-6 text-center text-[12px] text-[var(--el-text-color-secondary)]"
          >
            CPU 走势待接入
          </div>
        </Card>

        <table class="w-full border-collapse">
          <tbody>
            <tr
              v-for="kv in drawerKvPairs"
              :key="kv.k"
              class="border-b border-[var(--el-border-color-light)] last:border-b-0"
            >
              <td class="w-[130px] px-[2px] py-[8px] text-[12px] text-[var(--el-text-color-secondary)]">
                {{ kv.k }}
              </td>
              <td class="px-[2px] py-[8px] font-mono text-[12px] text-[var(--el-text-color-primary)]">
                {{ kv.v }}
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- 应用进程 tab -->
      <div v-else-if="drawerTab === 'apps'">
        <div
          v-if="appsLoading"
          data-spec="nodes-drawer-apps-loading"
          class="py-8 text-center text-[12px] text-[var(--el-text-color-secondary)]"
        >
          <span class="mr-[6px] inline-block h-[13px] w-[13px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)] align-[-3px]" />
          应用加载中…
        </div>
        <div
          v-else-if="appsError"
          data-spec="nodes-drawer-apps-error"
          class="py-4 text-[12px] text-[var(--el-color-danger)]"
        >
          应用列表加载失败：{{ appsError }}
        </div>
        <div
          v-else-if="appRows.length === 0"
          data-spec="nodes-drawer-apps-empty"
          class="py-8 text-center text-[12px] text-[var(--el-text-color-secondary)]"
        >
          该节点没有应用
        </div>
        <ul
          v-else
          class="m-0 list-none p-0"
        >
          <li
            v-for="a in appRows"
            :key="a.id"
            data-spec="nodes-drawer-app"
            class="flex items-center gap-[10px] border-b border-[var(--el-border-color-light)] px-[2px] py-[10px] last:border-b-0"
          >
            <span class="grid h-[28px] w-[28px] shrink-0 place-items-center rounded-[6px] bg-[color-mix(in_oklab,var(--el-color-primary)_12%,transparent)] font-mono text-[12px] text-[var(--el-color-primary-light-3)]">
              {{ a.name.slice(0, 1).toUpperCase() }}
            </span>
            <div class="min-w-0 flex-1">
              <div class="text-[12.5px] font-medium text-[var(--el-text-color-primary)]">
                {{ a.name }}
              </div>
              <div class="font-mono text-[10.5px] text-[var(--el-text-color-secondary)]">
                {{ a.process || '—' }}{{ a.pid !== null ? ` · PID ${a.pid}` : '' }}{{ a.ip ? ` · ${a.ip}` : '' }}
              </div>
            </div>
            <span class="font-mono text-[10.5px] text-[var(--el-text-color-secondary)]">{{ a.version || '—' }}</span>
          </li>
        </ul>
      </div>

      <!-- 命令 tab（NODE-06 B1，只读；下发入口不在列表页） -->
      <div v-else-if="drawerTab === 'command'">
        <div
          v-if="cmdState === 'loading'"
          data-spec="nodes-drawer-cmd-loading"
          class="py-8 text-center text-[12px] text-[var(--el-text-color-secondary)]"
        >
          <span class="mr-[6px] inline-block h-[13px] w-[13px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)] align-[-3px]" />
          命令记录加载中…
        </div>
        <div
          v-else-if="cmdState === 'error'"
          data-spec="nodes-drawer-cmd-error"
          class="py-4 text-[12px] text-[var(--el-color-danger)]"
        >
          命令记录加载失败：{{ cmdError }}
          <Btn
            type="button"
            data-spec="nodes-drawer-cmd-retry"
            class="ml-[8px]"
            @click="retryCmds"
          >
            重试
          </Btn>
        </div>
        <div
          v-else-if="cmdState === 'empty'"
          data-spec="nodes-drawer-cmd-empty"
          class="py-8 text-center text-[12px] text-[var(--el-text-color-secondary)]"
        >
          暂无命令记录
        </div>
        <div
          v-else
          data-spec="nodes-drawer-cmd-table"
          class="max-h-[420px] overflow-auto"
        >
          <table class="w-full border-collapse text-[12px]">
            <thead>
              <tr class="text-[var(--el-text-color-secondary)]">
                <th class="border-b border-[var(--el-border-color-light)] px-[8px] py-[6px] text-left font-normal">
                  编号
                </th>
                <th class="border-b border-[var(--el-border-color-light)] px-[8px] py-[6px] text-left font-normal">
                  节点
                </th>
                <th class="border-b border-[var(--el-border-color-light)] px-[8px] py-[6px] text-left font-normal">
                  命令
                </th>
                <th class="border-b border-[var(--el-border-color-light)] px-[8px] py-[6px] text-left font-normal">
                  参数
                </th>
                <th class="border-b border-[var(--el-border-color-light)] px-[8px] py-[6px] text-left font-normal">
                  结果
                </th>
                <th class="border-b border-[var(--el-border-color-light)] px-[8px] py-[6px] text-left font-normal">
                  追踪
                </th>
                <th class="border-b border-[var(--el-border-color-light)] px-[8px] py-[6px] text-left font-normal">
                  创建者
                </th>
                <th class="border-b border-[var(--el-border-color-light)] px-[8px] py-[6px] text-left font-normal">
                  创建时间
                </th>
                <th class="border-b border-[var(--el-border-color-light)] px-[8px] py-[6px] text-left font-normal">
                  更新时间
                </th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="c in cmdRows"
                :key="c.id ?? `${c.command}-${c.createTime}`"
                data-spec="nodes-drawer-cmd-row"
                class="border-b border-[var(--el-border-color-light)]"
              >
                <td class="px-[8px] py-[6px] font-mono">
                  {{ c.id ?? '—' }}
                </td>
                <td class="px-[8px] py-[6px]">
                  {{ c.nodeName || '—' }}
                </td>
                <td class="px-[8px] py-[6px] font-mono text-[var(--el-color-primary-light-3)]">
                  {{ c.command || '—' }}
                </td>
                <td
                  class="max-w-[180px] truncate px-[8px] py-[6px] font-mono"
                  :title="c.argument"
                >
                  {{ c.argument || '—' }}
                </td>
                <td class="px-[8px] py-[6px]">
                  {{ c.result || '—' }}
                </td>
                <td class="px-[8px] py-[6px]">
                  <RouterLink
                    v-if="c.traceId"
                    :to="`/trace/${c.traceId}`"
                    data-spec="nodes-drawer-cmd-trace"
                    class="text-[var(--el-color-primary-light-3)] hover:underline"
                  >
                    {{ c.traceId }}
                  </RouterLink>
                  <span v-else>—</span>
                </td>
                <td class="px-[8px] py-[6px]">
                  {{ c.createUser || '—' }}
                </td>
                <td class="px-[8px] py-[6px] font-mono">
                  {{ c.createTime }}
                </td>
                <td class="px-[8px] py-[6px] font-mono">
                  {{ c.updateTime }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- 性能 tab（CARD-NODE-07）：页签内节点搜索 Lov + 24h 采样单 Y 轴曲线；懒加载（首次切入才请求） -->
      <div v-else-if="drawerTab === 'perf'">
        <div class="mb-3 flex items-center gap-[10px]">
          <NodeLov
            class="flex-1"
            @select="onLovSelect"
          />
          <span
            data-spec="nodes-drawer-perf-selected"
            class="shrink-0 text-[12px] text-[var(--el-text-color-secondary)]"
          >
            {{ perfTarget ? `目标节点：${perfTarget.value}` : '当前节点' }}
          </span>
        </div>
        <div
          v-if="perfLoading"
          data-spec="nodes-drawer-perf-loading"
          class="py-8 text-center text-[12px] text-[var(--el-text-color-secondary)]"
        >
          <span class="mr-[6px] inline-block h-[13px] w-[13px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)] align-[-3px]" />
          性能数据加载中…
        </div>
        <div
          v-else-if="perfError"
          data-spec="nodes-drawer-perf-error"
          class="py-4 text-[12px] text-[var(--el-color-danger)]"
        >
          性能数据加载失败：{{ perfError }}
          <Btn
            type="button"
            data-spec="nodes-drawer-perf-retry"
            class="ml-[8px]"
            @click="retryNodeData"
          >
            重试
          </Btn>
        </div>
        <div
          v-else-if="!perfChart || perfChart.times.length === 0"
          data-spec="nodes-drawer-perf-empty"
          class="py-8 text-center text-[12px] text-[var(--el-text-color-secondary)]"
        >
          暂无性能数据
        </div>
        <Card
          v-else
          padding="pad"
          data-spec="nodes-drawer-perf-chart"
        >
          <h3 :class="H3">
            近 24 小时性能
          </h3>
          <EChart
            :option="perfChartOption"
            height="360px"
          />
        </Card>
      </div>

      <!-- 历史 tab（CARD-NODE-09）：懒加载；7 列与迁移页 COLUMNS 同集；success 过滤 'true'/'false' 字符串 -->
      <div v-else>
        <div class="mb-[8px] flex items-center gap-[6px] text-[12px]">
          <span class="text-[var(--el-text-color-secondary)]">状态</span>
          <select
            v-model="histSuccess"
            data-spec="nodes-drawer-hist-success"
            class="rounded-[6px] border border-[var(--el-border-color)] bg-transparent px-[8px] py-[4px] text-[12px]"
            @change="onHistSuccessChange"
          >
            <option value="">
              全部
            </option>
            <option value="true">
              成功
            </option>
            <option value="false">
              失败
            </option>
          </select>
        </div>
        <div
          v-if="histState === 'loading'"
          data-spec="nodes-drawer-hist-loading"
          class="py-8 text-center text-[12px] text-[var(--el-text-color-secondary)]"
        >
          <span class="mr-[6px] inline-block h-[13px] w-[13px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)] align-[-3px]" />
          历史记录加载中…
        </div>
        <div
          v-else-if="histState === 'error'"
          data-spec="nodes-drawer-hist-error"
          class="py-4 text-[12px] text-[var(--el-color-danger)]"
        >
          历史记录加载失败：{{ histError }}
          <Btn
            type="button"
            data-spec="nodes-drawer-hist-retry"
            class="ml-[8px]"
            @click="retryHistory"
          >
            重试
          </Btn>
        </div>
        <div
          v-else-if="histState === 'empty'"
          data-spec="nodes-drawer-hist-empty"
          class="py-8 text-center text-[12px] text-[var(--el-text-color-secondary)]"
        >
          暂无历史
        </div>
        <div
          v-else
          data-spec="nodes-drawer-hist-table"
          class="max-h-[420px] overflow-auto"
        >
          <table class="w-full border-collapse text-[12px]">
            <thead>
              <tr class="text-[var(--el-text-color-secondary)]">
                <th class="border-b border-[var(--el-border-color-light)] px-[8px] py-[6px] text-left font-normal">
                  节点
                </th>
                <th class="border-b border-[var(--el-border-color-light)] px-[8px] py-[6px] text-left font-normal">
                  操作
                </th>
                <th class="border-b border-[var(--el-border-color-light)] px-[8px] py-[6px] text-left font-normal">
                  成功
                </th>
                <th class="border-b border-[var(--el-border-color-light)] px-[8px] py-[6px] text-left font-normal">
                  内容
                </th>
                <th class="border-b border-[var(--el-border-color-light)] px-[8px] py-[6px] text-left font-normal">
                  追踪
                </th>
                <th class="border-b border-[var(--el-border-color-light)] px-[8px] py-[6px] text-left font-normal">
                  创建时间
                </th>
                <th class="border-b border-[var(--el-border-color-light)] px-[8px] py-[6px] text-left font-normal">
                  创建地址
                </th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="h in histRows"
                :key="h.id ?? h.createTime"
                data-spec="nodes-drawer-hist-row"
              >
                <td class="border-b border-[var(--el-border-color-light)] px-[8px] py-[6px]">
                  {{ h.nodeName }}
                </td>
                <td class="border-b border-[var(--el-border-color-light)] px-[8px] py-[6px]">
                  {{ h.action }}
                </td>
                <td class="border-b border-[var(--el-border-color-light)] px-[8px] py-[6px]">
                  <span
                    :class="h.successText === '是' ? 'text-[var(--el-color-success)]' : 'text-[var(--el-color-danger)]'"
                  >{{ h.successText }}</span>
                </td>
                <td class="border-b border-[var(--el-border-color-light)] px-[8px] py-[6px]">
                  {{ h.remark }}
                </td>
                <td class="border-b border-[var(--el-border-color-light)] px-[8px] py-[6px] text-[var(--el-text-color-secondary)]">
                  {{ h.traceId || '—' }}
                </td>
                <td class="border-b border-[var(--el-border-color-light)] px-[8px] py-[6px] text-[var(--el-text-color-secondary)]">
                  {{ h.createTime }}
                </td>
                <td class="border-b border-[var(--el-border-color-light)] px-[8px] py-[6px] text-[var(--el-text-color-secondary)]">
                  {{ h.createIP || '—' }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </Drawer>
  </div>
</template>
