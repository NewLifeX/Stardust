<script setup lang="ts">
/**
 * ShipPanel：部署内容容器，/ship 页（不传 section，纵向显示全部）与应用工作台的版本、节点、部署历史、流水线四个页签（传 section，只渲染一块）共用。
 * 概览页签（传 overview）：只渲染版本条与「需关注事项」（buildAttention 算出，点按钮发 go(tab)），不渲染四个操作块。
 * 2026-10-28 起退化为容器：取数、刷新、操作反馈、应用版本、节点操作、选中运行都在 useDeployData（有单测），
 * 展示拆成五个只收 props 的组件（各有 story 与 CT）：
 *  1. DeployVersionBar    当前生效版本与刷新按钮（F06：编辑发布集 / 同步应用入口）
 *  2. DeployVersionList   版本列表，可「应用此版本」（UseVersion）；F05 上传新版本
 *  3. DeployNodes         部署节点，单个/批量远程操作（F07），新增/编辑/删除
 *  4. DeployHistory       部署历史（AppDeployHistory，只是下发记录，不是节点执行结果）
 *  5. DeployPipelineRuns  流水线运行与按节点的部署结果（F09：5s 轮询在 useDeployData，一键回滚入口）
 * 容器只做：把状态算成 props 传下去、点击后用 openGuard 做二次确认、管理 F06/F07/F08 的抽屉表单与取数回填。
 */
import { computed, ref, watch } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import { pipelineSteps, isRunActive } from '../Stardust/appsModel';
import type { PipelineRunRow } from '../Stardust/appsModel';
import Btn from '@/layouts/StardustLayout2/Btn.vue';
import Card from '@/layouts/StardustLayout2/Card.vue';
import BuildNodes from '@/layouts/StardustLayout2/BuildNodes.vue';
import DeployEditDrawer from '@/layouts/StardustLayout2/DeployEditDrawer.vue';
import DeployFeedbackStrip from '@/layouts/StardustLayout2/DeployFeedbackStrip.vue';
import DeployHistory from '@/layouts/StardustLayout2/DeployHistory.vue';
import DeployNodes from '@/layouts/StardustLayout2/DeployNodes.vue';
import DeployPipelineRuns from '@/layouts/StardustLayout2/DeployPipelineRuns.vue';
import DeployVersionBar from '@/layouts/StardustLayout2/DeployVersionBar.vue';
import DeployVersionList from '@/layouts/StardustLayout2/DeployVersionList.vue';
import NodeFormDrawer from '@/layouts/StardustLayout2/NodeFormDrawer.vue';
import DeployLogDrawer from '@/layouts/StardustLayout2/DeployLogDrawer.vue';
import PipelineConfigCard from '@/layouts/StardustLayout2/PipelineConfigCard.vue';
import PipelineConfigDrawer from '@/layouts/StardustLayout2/PipelineConfigDrawer.vue';
import PipelineRunDetailDrawer from '@/layouts/StardustLayout2/PipelineRunDetailDrawer.vue';
import PipelineStepDrawer from '@/layouts/StardustLayout2/PipelineStepDrawer.vue';
import PipelineWebhookCard from '@/layouts/StardustLayout2/PipelineWebhookCard.vue';
import {
  copyTextToClipboard,
  fetchDeployNodeOptions,
  fetchPipelineProjectId,
  searchBuildNodes,
  toPipelineForm,
} from '@/layouts/StardustLayout2/pipelineConfig.logic';
import type { BuildNodeSearchItem, PipelineForm, PipelineNodeOption, PipelineRow } from '@/layouts/StardustLayout2/pipelineConfig.logic';
import { fetchWebhookFields } from '@/layouts/StardustLayout2/pipelineWebhook.logic';
import type { WebhookFieldDoc } from '@/layouts/StardustLayout2/pipelineWebhook.logic';
import AttentionList from '@/layouts/StardustLayout2/AttentionList.vue';
import AppLov from '@/layouts/StardustLayout2/AppLov.vue';
import { openGuard } from '@/layouts/StardustLayout2/ui.store';
import type { DeploySection } from '@/layouts/StardustLayout2/workbench.logic';
import type { AppTab } from '@/layouts/StardustLayout2/palette.items';
import { buildAttention } from '@/layouts/StardustLayout2/overview.logic';
import { toast } from '@/layouts/StardustLayout2/useToast';
import { useDeployData } from '@/layouts/StardustLayout2/useDeployData';
import { nodeStats as calcNodeStats, nodeVersionTag as calcNodeVersionTag } from '@/layouts/StardustLayout2/deployView.logic';
import type { VersionTag } from '@/layouts/StardustLayout2/deployView.logic';
import type { PipelineView, StageItem } from '@/layouts/StardustLayout2/deployPipeline.styles';
import {
  buildNodeResults,
  buildRunRows,
  buildVersionCells,
  operateNotice,
  useVersionNotice,
} from '@/layouts/StardustLayout2/ship.logic';
import type { NodeActionInfo, Notice, RunFilter, ShipNode, ShipRunRow, VersionOption } from '@/layouts/StardustLayout2/ship.logic';
import { fetchBuildNodes, runBuildUpload, runPackageUpload } from '@/layouts/StardustLayout2/buildNodes.logic';
import type { BuildNodeAct, BuildNodeRow } from '@/layouts/StardustLayout2/buildNodes.logic';
import {
  fetchBuildNodeDetail,
  fetchDeployDetail,
  fetchDeployNodeDetail,
  fetchNodeOptions,
  resolveEffectiveLogDir,
  rollbackCandidates,
  rollbackNotice,
} from '@/layouts/StardustLayout2/deploy.logic';
import type { BuildNodeForm, DeployEditModel, DeployNodeForm, NodeOption } from '@/layouts/StardustLayout2/deploy.logic';

// section：只渲染哪一块（versions / nodes / history / pipeline）；不传表示纵向显示全部（/ship 整页用，停点 20 随该页一起去掉）
// overview：概览页签，只渲染版本条与需关注事项；instances 是在线实例数，由页面传入（未知不传）
const props = defineProps<{ appId: number; appName: string; section?: DeploySection; overview?: boolean; instances?: number }>();
const emit = defineEmits<{ go: [tab: AppTab]; openApp: [appId: number] }>();

// 该块是否要渲染
function show(name: DeploySection): boolean {
  if (props.overview) return false;
  return props.section === undefined || props.section === name;
}

const {
  loading,
  loadError,
  deploy,
  others,
  versions,
  versionsFailed,
  nodes,
  nodesFailed,
  history,
  runs,
  selectedRunId,
  steps,
  busyKey,
  busyText,
  feedback,
  enabledNodes,
  refreshing,
  refresh: onRefresh,
  load,
  applyVersion,
  operate,
  selectRun,
  saveDeployEdit,
  syncApp,
  batchOperate,
  uploadNewVersion,
  saveNode,
  removeNode,
  saveBuildNode,
  removeBuildNode,
  // CARD-DEP-01/02（2026-11-07 接线点登记）：流水线配置 + 运行筛选/详情/重跑
  pipelines,
  pipelinesFailed,
  runFilter,
  setRunFilter,
  resetRunFilter,
  runDetailOpen,
  runDetail,
  runDetailState,
  openRunDetail,
  closeRunDetail,
  retryRunDetail,
  rerunRun,
  savePipeline,
  removePipeline,
  copyWebhookFor,
  triggerRun,
} = useDeployData({
  appId: () => props.appId,
  appName: () => props.appName,
  request,
  notify: (ok, text) => toast(text, ok ? 'ok' : 'err'),
});

// ---------- 当前生效版本与节点版本标签 ----------

// 计算逻辑在 deployView.logic.ts（有单测），这里只负责把当前状态传进去
const nodeStats = computed(() => calcNodeStats(deploy.value?.version ?? '', nodes.value));

// 按节点行 id 索引，停用节点或没有部署集时为 null
const versionTags = computed(() => {
  const tags: Record<number, VersionTag | null> = {};
  for (const n of nodes.value) tags[n.id] = calcNodeVersionTag(deploy.value, n);
  return tags;
});

// ---------- 版本列表：应用此版本前二次确认 ----------

const versionNotice = computed<Notice | null>(() => useVersionNotice(deploy.value, enabledNodes.value.length));

function askApply(v: VersionOption): void {
  if (busyKey.value || v.disabled) return;
  const notice = versionNotice.value;
  openGuard(`${props.appName} 切换到 ${v.version}。${notice?.text ?? ''}`, () => applyVersion(v));
}

// ---------- 部署节点：操作前二次确认 ----------

function askOperate(n: ShipNode, a: NodeActionInfo): void {
  if (busyKey.value) return;
  const notice = operateNotice(a, n);
  openGuard(`${n.name} · ${a.label}。${notice.text}`, () => operate(n, a.act));
}

// ---------- F07 批量远程操作：二次确认写明对象与后果 ----------

function askBatch(a: NodeActionInfo, ids: number[]): void {
  if (busyKey.value || ids.length === 0) return;
  const names = nodes.value.filter((n) => ids.includes(n.id)).map((n) => n.name);
  const who = names.length > 3 ? `${names.slice(0, 3).join('、')} 等 ${names.length} 个节点` : names.join('、');
  const notice = a.danger
    ? `「${a.label}」会让这些节点上的应用${a.act === 'uninstall' ? '卸载' : '停止运行'}，且会停用节点；请确认没有正在服务的使用者。`
    : `将对 ${who} 批量下发「${a.label}」，命令发出后不等节点回包，是否生效请看节点版本与部署历史。`;
  openGuard(`批量 ${a.label} · ${who}。${notice}`, () => batchOperate(a.act, ids, names));
}

// ---------- F06 部署集编辑 Drawer + 同步应用 ----------

const deployDrawer = ref<{ open: boolean; loading: boolean; model: DeployEditModel | null }>({ open: false, loading: false, model: null });
const deploySaving = computed(() => busyKey.value === 'deploy-save');
const syncing = computed(() => busyKey.value === 'deploy-sync');

async function askEditDeploy(): Promise<void> {
  const id = deploy.value?.id ?? 0;
  if (!id || busyKey.value) return;
  deployDrawer.value = { open: true, loading: true, model: null };
  const m = await fetchDeployDetail(request, id);
  if (!deployDrawer.value.open) return;
  if (!m) {
    deployDrawer.value = { open: false, loading: false, model: null };
    toast('部署集详情加载失败，请稍后重试', 'err');
    return;
  }
  deployDrawer.value.model = m;
  deployDrawer.value.loading = false;
}

function onDeploySubmit(m: DeployEditModel): void {
  void saveDeployEdit(m).then((ok) => {
    if (ok) deployDrawer.value.open = false;
  });
}

function onSync(): void {
  if (busyKey.value) return;
  // SyncApp 是从应用注册表补齐部署集（幂等、无删除），不需要危险确认；进行中状态在版本条按钮上
  void syncApp();
}

// CARD-DEP-08 D5：联想框选中应用 → 交给宿主跳该应用的部署页签（App.vue 推 /app/versions/<id>）
function onLovSelect(item: { value: string; id: number }): void {
  if (item?.id > 0) emit('openApp', item.id);
}

// ---------- F07/F08 节点表单 Drawer（部署 / 编译共用 NodeFormDrawer） ----------

type NodeKind = 'deploy' | 'build';
const nodeOptions = ref<NodeOption[]>([]);
const nodeOptionsLoading = ref(false);
const nodeDrawer = ref<{
  open: boolean;
  kind: NodeKind;
  id: number;
  title: string;
  sub: string;
  initial: DeployNodeForm | BuildNodeForm | null;
  loading: boolean;
}>({ open: false, kind: 'deploy', id: 0, title: '', sub: '', initial: null, loading: false });
const nodeSaving = computed(() => busyKey.value.startsWith('node') || busyKey.value.startsWith('bnode'));

const logDrawer = ref<{ open: boolean; deployNodeId: number; nodeName: string }>({
  open: false,
  deployNodeId: 0,
  nodeName: '',
});

// task-90：生效日志目录 = 节点级 logDirectory 优先，空=跟随部署集；双空=未配置（抽屉里前置拦截+配置引导）。
// 抽屉保存后 refreshSoon 刷新 nodes/deploy → 此 computed 重算 → DeployLogDrawer watch(logDir) 自动加载。
const logDrawerNode = computed(() => nodes.value.find((n) => n.id === logDrawer.value.deployNodeId) ?? null);
const effectiveLogDir = computed(() => resolveEffectiveLogDir(logDrawerNode.value, deploy.value));

function openDeployLogs(n: ShipNode): void {
  if (busyKey.value) return;
  logDrawer.value = { open: true, deployNodeId: n.id, nodeName: n.name };
}

// task-90：配置引导空态的「配置日志目录」按钮 → 复用既有节点编辑抽屉通道（askEditNode 拉详情+节点选项），
// 日志抽屉保持打开（编辑抽屉 z-index 41 叠在其上），保存成功 onNodeSubmit 关编辑抽屉 → 自动加载闭环。
function onConfigureLog(): void {
  const n = logDrawerNode.value;
  if (!n || busyKey.value) return;
  void askEditNode(n);
}

async function loadNodeOptions(): Promise<void> {
  if (nodeOptions.value.length || nodeOptionsLoading.value) return;
  nodeOptionsLoading.value = true;
  const list = await fetchNodeOptions(request);
  nodeOptionsLoading.value = false;
  if (list === null) {
    toast('物理节点列表加载失败，无法选择节点', 'err');
    return;
  }
  nodeOptions.value = list;
}

async function askCreateNode(kind: NodeKind): Promise<void> {
  if (busyKey.value) return;
  nodeDrawer.value = {
    open: true, kind, id: 0,
    title: kind === 'deploy' ? '新增部署节点' : '新增编译节点',
    sub: `部署集 ${props.appName}`,
    initial: null, loading: false,
  };
  void loadNodeOptions();
}

async function askEditNode(n: ShipNode): Promise<void> {
  if (busyKey.value) return;
  nodeDrawer.value = { open: true, kind: 'deploy', id: n.id, title: `编辑部署节点 · ${n.name}`, sub: `IP ${n.ip || '—'} · 编号 ${n.id}`, initial: null, loading: true };
  void loadNodeOptions();
  const m = await fetchDeployNodeDetail(request, n.id);
  if (!nodeDrawer.value.open || nodeDrawer.value.id !== n.id) return;
  if (!m) {
    nodeDrawer.value.open = false;
    toast(`部署节点「${n.name}」详情加载失败，请稍后重试`, 'err');
    return;
  }
  nodeDrawer.value.initial = m;
  nodeDrawer.value.loading = false;
}

async function askEditBuildNode(r: BuildNodeRow): Promise<void> {
  if (busyKey.value) return;
  nodeDrawer.value = { open: true, kind: 'build', id: r.id, title: `编辑编译节点 · ${r.nodeName}`, sub: `IP ${r.nodeIp || '—'} · 编号 ${r.id}`, initial: null, loading: true };
  void loadNodeOptions();
  const m = await fetchBuildNodeDetail(request, r.id);
  if (!nodeDrawer.value.open || nodeDrawer.value.id !== r.id) return;
  if (!m) {
    nodeDrawer.value.open = false;
    toast(`编译节点「${r.nodeName}」详情加载失败，请稍后重试`, 'err');
    return;
  }
  nodeDrawer.value.initial = m;
  nodeDrawer.value.loading = false;
}

function onNodeSubmit(form: DeployNodeForm | BuildNodeForm): void {
  const id = nodeDrawer.value.id || undefined;
  const call = nodeDrawer.value.kind === 'deploy'
    ? saveNode(form as DeployNodeForm, id)
    : saveBuildNode(form as BuildNodeForm, id);
  void call.then((ok) => {
    if (ok) nodeDrawer.value.open = false;
  });
}

function askRemoveNode(n: ShipNode): void {
  if (busyKey.value) return;
  openGuard(`删除部署节点 ${n.name}（${n.ip || '—'}）。只把节点移出本部署集，不影响节点上的其它部署；正在运行的应用不会被停止。确认？`, () => removeNode(n.id, n.name));
}

function askRemoveBuildNode(r: BuildNodeRow): void {
  if (busyKey.value) return;
  openGuard(`删除编译节点 ${r.nodeName}（${r.nodeIp || '—'}）。删除后该节点不再参与本部署集的编译打包；确认？`, () => removeBuildNode(r.id, r.nodeName));
}

// ---------- F05 上传新版本 ----------

function onUpload(file: File): void {
  if (busyKey.value) return;
  void uploadNewVersion(file);
}

// ---------- F09 一键回滚：候选版本 + Guard 说明 ----------

const rollbackList = computed(() => rollbackCandidates(versions.value, deploy.value?.version ?? ''));
const rollbackBusy = computed(() => busyKey.value.startsWith('ver:'));
const rollbackDisabled = computed(() => rollbackBusy.value || rollbackList.value.length === 0);
const rollbackTitle = computed(() => {
  if (rollbackList.value.length === 0) return '没有可回滚的版本（需要除当前生效版本之外的可用版本）';
  return `回滚到上一个版本 ${rollbackList.value[0].version}`;
});

function askRollback(): void {
  if (rollbackDisabled.value) return;
  const target = rollbackList.value[0];
  const notice = rollbackNotice(target, !!deploy.value?.autoPublish, enabledNodes.value.length);
  openGuard(`${props.appName} 一键回滚。${notice}`, () => applyVersion(target as VersionOption));
}

// ---------- 编译节点（子 tab） ----------

type NodeTab = 'deploy' | 'build';
const nodeTab = ref<NodeTab>('deploy');
const buildRows = ref<BuildNodeRow[]>([]);
const buildFailed = ref(false);
const buildBusyKey = ref('');

async function loadBuildNodes(): Promise<void> {
  const did = deploy.value?.id ?? 0;
  if (!did || did <= 0) {
    buildRows.value = [];
    buildFailed.value = false;
    return;
  }
  const rows = await fetchBuildNodes(request, did);
  if (rows === null) {
    buildFailed.value = true;
  } else {
    buildFailed.value = false;
    buildRows.value = rows;
  }
}

// deploy 出现/变化时拉一次编译节点；刷新按钮已在 useDeployData 里，这里不动它
watch(deploy, () => { void loadBuildNodes(); });
watch(() => props.section, (s) => {
  if (!props.overview && (s === undefined || s === 'pipeline')) void loadNodeOptions();
}, { immediate: true });

function askBuildOperate(row: BuildNodeRow, act: BuildNodeAct): void {
  if (buildBusyKey.value) return;
  const label = act === 'Build-Upload' ? '编译上传' : '打包上传';
  const desc = act === 'Build-Upload'
    ? '将在节点上拉取源代码、编译项目、打包并上传应用包'
    : '不拉取源代码、不编译，直接打包输出目录并上传应用包';
  openGuard(`${row.nodeName} · ${label}。${desc}。确认？`, async () => {
    buildBusyKey.value = `${row.id}:${act}`;
    try {
      const fn = act === 'Build-Upload' ? runBuildUpload : runPackageUpload;
      const r = await fn(request, row.id);
      toast(r.message, r.ok ? 'ok' : 'err');
      await loadBuildNodes();
      // 编译是长任务，4 秒后再刷新一次，让用户看到状态变化
      setTimeout(() => { void loadBuildNodes(); }, 4000);
    } finally {
      buildBusyKey.value = '';
    }
  });
}

// ---------- 流水线运行与按节点结果 ----------

const runRows = computed(() => buildRunRows(runs.value, versions.value));
const selectedRaw = computed(() => runs.value.find((r) => r.id === selectedRunId.value));

// 流水线四段（构建、上传、分发、部署）；只用运行状态推算，不读步骤表
const stages = computed<StageItem[]>(() => {
  const r = selectedRaw.value;
  return r ? pipelineSteps(r as unknown as PipelineRunRow) : [];
});

const view = computed<PipelineView | null>(() => {
  const d = deploy.value;
  if (!d) return null;
  const run = selectedRaw.value;
  if (run && steps.value) {
    const r = buildNodeResults(nodes.value, steps.value, run.status);
    if (r.deploySteps > 0) return { items: r.items, allSkipped: r.allSkipped, note: `运行 #${run.id} 按节点的部署步骤` };
    // task-88（2026-11-12）：触发后自动选中的新运行常在此刻还没有步骤上报，别误报「没有按节点的部署步骤」
    if (isRunActive(run.status)) {
      return {
        items: buildVersionCells(nodes.value, d.version),
        allSkipped: false,
        note: `运行 #${run.id} 进行中，按节点的部署步骤要等节点执行后显示；点「查看运行」或行内「详情」看最新状态`,
      };
    }
    return {
      items: buildVersionCells(nodes.value, d.version),
      allSkipped: false,
      note: `运行 #${run.id} 没有按节点的部署步骤（只有流水线发起的部署才有），下面是节点当前版本`,
    };
  }
  if (run && steps.value === null) {
    return { items: buildVersionCells(nodes.value, d.version), allSkipped: false, note: '部署步骤加载失败，下面是节点当前版本' };
  }
  return {
    items: buildVersionCells(nodes.value, d.version),
    allSkipped: false,
    note: `点一条运行记录查看按节点的部署步骤；未选择时是节点当前版本与部署集版本 ${d.version || '—'} 的比对`,
  };
});

// ---------- 概览：需关注事项 ----------

// useDeployData 加载失败时不清旧的 deploy，所以加载失败一律当没有部署集，不拿旧数据判断
const attention = computed(() =>
  buildAttention({
    deploy: loadError.value ? null : deploy.value,
    deployFailed: !!loadError.value,
    nodes: nodes.value,
    nodesFailed: nodesFailed.value,
    runs: runs.value,
    history: history.value,
    instances: props.instances,
    now: Date.now(),
  }),
);

// ---------- CARD-DEP-01 流水线配置：抽屉 + 级联 + 行操作 ----------

const pipeDrawer = ref<{ open: boolean; id: number; initial: PipelineForm | null; title: string; sub: string }>({
  open: false, id: 0, initial: null, title: '', sub: '',
});
const pipeNodeOptions = ref<PipelineNodeOption[]>([]);
const pipeNodesLoading = ref(false);
const pipeProjectId = ref(0);
/** 复制中的流水线 Id（从 busyKey 提取，供 Webhook 卡与配置卡按钮态） */
const pipeCopyId = computed(() => (busyKey.value.startsWith('copy:') ? Number(busyKey.value.slice(5)) || 0 : 0));
/** 重跑中的运行（rerun:{id}，供运行表行操作） */
const rerunKey = computed(() => (busyKey.value.startsWith('rerun:') ? busyKey.value : ''));

/** 编译节点 suggest：NodeSearch 按 GetDeployProject 级联出的项目 Id 过滤（旧壳 _Form_SelectBuildNode 同参） */
function searchNodesFor(key: string): Promise<BuildNodeSearchItem[] | null> {
  return searchBuildNodes(request, pipeProjectId.value, key);
}

async function loadPipeCascade(): Promise<void> {
  const did = deploy.value?.id ?? 0;
  if (!did) {
    pipeNodeOptions.value = [];
    pipeProjectId.value = 0;
    return;
  }
  pipeNodesLoading.value = true;
  const [opts, pid] = await Promise.all([fetchDeployNodeOptions(request, did), fetchPipelineProjectId(request, did)]);
  pipeNodesLoading.value = false;
  pipeNodeOptions.value = opts ?? [];
  pipeProjectId.value = pid ?? 0;
}

function askCreatePipeline(): void {
  if (!deploy.value || busyKey.value) return;
  pipeDrawer.value = { open: true, id: 0, initial: null, title: '新增流水线', sub: `部署集 ${props.appName}` };
  void loadPipeCascade();
}

function askEditPipeline(row: PipelineRow): void {
  if (busyKey.value) return;
  pipeDrawer.value = { open: true, id: row.id, initial: toPipelineForm(row), title: `编辑流水线 · ${row.name}`, sub: `部署集 ${props.appName}` };
  void loadPipeCascade();
}

function onPipelineSubmit(form: PipelineForm): void {
  const id = pipeDrawer.value.id || undefined;
  void savePipeline(form, id).then((ok) => {
    if (ok) pipeDrawer.value.open = false;
  });
}

function askRemovePipeline(row: PipelineRow): void {
  if (busyKey.value) return;
  openGuard(`删除流水线 ${row.name}（分支 ${row.branch || '—'}）。删除后 Webhook 地址立即失效，历史运行记录保留。确认？`, () => removePipeline(row));
}

async function onCopyWebhook(row: PipelineRow): Promise<void> {
  if (busyKey.value) return;
  const r = await copyWebhookFor(row);
  if (!r.ok) return; // 失败反馈已在 useDeployData.pushFeedback
  const copied = await copyTextToClipboard(r.url, navigator, document);
  toast(copied ? 'WebHook 地址已复制到剪贴板' : `复制失败，地址：${r.url}`, copied ? 'ok' : 'err');
}

function askTriggerPipeline(row: PipelineRow): void {
  if (busyKey.value) return;
  openGuard('启动流水线。确认手动触发该流水线运行（使用当前分支最新提交）？', () => triggerRun(row));
}

// ---------- CARD-DEP-02 运行筛选/重跑/详情 + CARD-DEP-03/04 ----------

function askRerun(row: ShipRunRow): void {
  if (busyKey.value) return;
  openGuard('重跑流水线。确认基于本次运行重新触发流水线？', () => rerunRun(row.id));
}

function onFilterChange(patch: Partial<RunFilter>): void {
  setRunFilter(patch);
}

// CARD-DEP-03：步骤明细抽屉（数据源 = 详情响应 steps[]，零新增请求）
const stepsDrawerOpen = ref(false);

// CARD-DEP-04：Webhook 字段说明（GetFields 一次加载；失败降级静态说明）
const webhookFields = ref<WebhookFieldDoc[] | null>(null);
const webhookFieldsLoading = ref(false);

async function loadWebhookFields(): Promise<void> {
  if (webhookFields.value || webhookFieldsLoading.value) return;
  webhookFieldsLoading.value = true;
  // GetFields 无 /api 前缀（走查缺陷2）：fetchWebhookFields 内部直连 VITE_APP_BASE_API，失败静默降级
  webhookFields.value = await fetchWebhookFields();
  webhookFieldsLoading.value = false;
}

// pipeline 页签可见（或整页 /ship 不分区块）时加载一次
watch(
  () => (props.section ?? 'pipeline') === 'pipeline',
  (v) => {
    if (v) void loadWebhookFields();
  },
  { immediate: true },
);
</script>

<template>
  <div
    data-spec="ship-panel"
    class="flex flex-col gap-3"
  >
    <Card
      v-if="loading"
      class="text-[13px] text-[var(--el-text-color-secondary)]"
      data-spec="ship-loading"
    >
      加载中…
    </Card>

    <Card
      v-else-if="loadError"
      data-spec="ship-error"
    >
      <div class="flex items-center gap-3 text-[13px] text-[var(--el-color-danger)]">
        {{ loadError }}
        <Btn
          size="sm"
          @click="load()"
        >
          重试
        </Btn>
      </div>
    </Card>

    <Card
      v-else-if="!deploy && !overview"
      class="text-[13px] text-[var(--el-text-color-secondary)]"
      data-spec="ship-nodeploy"
    >
      {{ appName }} 还没有部署集，无法在这里发布。
    </Card>

    <template v-else-if="deploy">
      <!-- 操作进行中与最近反馈：始终在用户视线内；「查看运行」直达该次运行详情抽屉（task-88 2026-11-12） -->
      <DeployFeedbackStrip
        :busy-text="busyText"
        :items="feedback"
        @open-run="openRunDetail"
      />

      <DeployVersionBar
        :version="deploy.version"
        :enable="deploy.enable"
        :auto-publish="deploy.autoPublish"
        :multi-version="deploy.multiVersion"
        :stats="nodeStats"
        :nodes-failed="nodesFailed"
        :refreshing="refreshing"
        :busy="!!busyKey"
        :syncing="syncing"
        @refresh="onRefresh"
        @edit="askEditDeploy"
        @sync="onSync"
      />

      <div
        v-if="!overview"
        class="flex flex-col gap-3"
        data-spec="ship-body"
        :data-section="section ?? 'all'"
      >
        <div v-if="show('versions')">
          <!-- CARD-DEP-08 D5（J-REG-01 §8 行 9 移交项）：工作台部署页签顶部应用联想框，选中跳该应用部署页签（仅工作台 versions 页签，/ship 页自有下拉不重复） -->
          <div
            v-if="section === 'versions'"
            class="mb-3"
          >
            <AppLov @select="onLovSelect" />
          </div>
          <DeployVersionList
            :versions="versions"
            :versions-failed="versionsFailed"
            :notice="versionNotice"
            :busy-key="busyKey"
            :uploading="busyKey === 'upload'"
            @apply="askApply"
            @upload="onUpload"
          />
        </div>

        <div v-if="show('nodes')">
          <!-- 子 tab：部署节点 / 编译节点 -->
          <div
            class="mb-[10px] inline-flex gap-[2px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color)] p-[3px]"
            data-spec="node-subtabs"
          >
            <button
              type="button"
              data-spec="node-subtab-deploy"
              :aria-current="nodeTab === 'deploy' ? 'page' : undefined"
              class="h-[26px] whitespace-nowrap rounded-[6px] px-[13px] text-[12.5px] font-medium transition-colors"
              :class="nodeTab === 'deploy'
                ? 'bg-[var(--el-fill-color-light)] text-[var(--el-text-color-primary)]'
                : 'text-[var(--el-text-color-secondary)] hover:bg-[var(--el-bg-color-overlay)] hover:text-[var(--el-text-color-primary)]'"
              @click="nodeTab = 'deploy'"
            >
              部署节点
            </button>
            <button
              type="button"
              data-spec="node-subtab-build"
              :aria-current="nodeTab === 'build' ? 'page' : undefined"
              class="h-[26px] whitespace-nowrap rounded-[6px] px-[13px] text-[12.5px] font-medium transition-colors"
              :class="nodeTab === 'build'
                ? 'bg-[var(--el-fill-color-light)] text-[var(--el-text-color-primary)]'
                : 'text-[var(--el-text-color-secondary)] hover:bg-[var(--el-bg-color-overlay)] hover:text-[var(--el-text-color-primary)]'"
              @click="nodeTab = 'build'"
            >
              编译节点
            </button>
          </div>

          <DeployNodes
            v-if="nodeTab === 'deploy'"
            :nodes="nodes"
            :nodes-failed="nodesFailed"
            :busy-key="busyKey"
            :version-tags="versionTags"
            :deploy-name="deploy?.name ?? ''"
            :others="others"
            @operate="askOperate"
            @batch="askBatch"
            @create="askCreateNode('deploy')"
            @edit="askEditNode"
            @remove="askRemoveNode"
            @view-logs="openDeployLogs"
          />
          <BuildNodes
            v-else
            :rows="buildRows"
            :failed="buildFailed"
            :busy-key="buildBusyKey"
            @operate="askBuildOperate"
            @create="askCreateNode('build')"
            @edit="askEditBuildNode"
            @remove="askRemoveBuildNode"
          />
        </div>

        <div v-if="show('history')">
          <DeployHistory
            :history="history"
            :nodes="nodes"
          />
        </div>

        <div v-if="show('pipeline')">
          <div class="flex flex-col gap-3">
            <!-- CARD-DEP-01：流水线配置（CRUD + 复制地址 + 启动） -->
            <PipelineConfigCard
              :rows="pipelines"
              :failed="pipelinesFailed"
              :busy-key="busyKey"
              @create="askCreatePipeline"
              @copy="onCopyWebhook"
              @trigger="askTriggerPipeline"
              @edit="askEditPipeline"
              @remove="askRemovePipeline"
              @retry="load()"
            />

            <!-- CARD-DEP-02：流水线运行（筛选 + 行内重跑/详情 + 5s 轮询在 useDeployData） -->
            <DeployPipelineRuns
              :run-rows="runRows"
              :selected-run-id="selectedRunId"
              :stages="stages"
              :view="view"
              :rollback-disabled="rollbackDisabled"
              :rollback-title="rollbackTitle"
              :rolling-back="rollbackBusy"
              :run-filter="runFilter"
              :rerun-key="rerunKey"
              @select="selectRun"
              @rollback="askRollback"
              @rerun="askRerun"
              @detail="openRunDetail($event.id)"
              @filter-change="onFilterChange"
              @filter-reset="resetRunFilter"
            />

            <!-- CARD-DEP-04：Webhook 接入说明（复制 + GetFields 指引 + 结果语义速查） -->
            <PipelineWebhookCard
              :rows="pipelines"
              :fields="webhookFields"
              :fields-loading="webhookFieldsLoading"
              :copying-id="pipeCopyId"
              @copy="onCopyWebhook"
            />
          </div>
        </div>
      </div>
    </template>

    <!-- F06 部署集编辑抽屉 -->
    <DeployEditDrawer
      :open="deployDrawer.open"
      :initial="deployDrawer.model"
      :busy="deploySaving || deployDrawer.loading"
      @close="deployDrawer.open = false"
      @submit="onDeploySubmit"
    />

    <!-- F07/F08 部署节点 / 编译节点表单抽屉 -->
    <NodeFormDrawer
      :open="nodeDrawer.open"
      :kind="nodeDrawer.kind"
      :initial="nodeDrawer.initial"
      :title="nodeDrawer.title"
      :sub="nodeDrawer.sub"
      :node-options="nodeOptions"
      :nodes-loading="nodeOptionsLoading"
      :busy="nodeSaving || nodeDrawer.loading"
      @close="nodeDrawer.open = false"
      @submit="onNodeSubmit"
    />

    <DeployLogDrawer
      :open="logDrawer.open"
      :deploy-node-id="logDrawer.deployNodeId"
      :node-name="logDrawer.nodeName"
      :log-dir="effectiveLogDir"
      @close="logDrawer.open = false"
      @configure="onConfigureLog"
    />

    <!-- CARD-DEP-01 流水线配置抽屉（新增/编辑 + 三级联） -->
    <PipelineConfigDrawer
      :open="pipeDrawer.open"
      :initial="pipeDrawer.initial"
      :title="pipeDrawer.title"
      :sub="pipeDrawer.sub"
      :project-id="pipeProjectId"
      :node-options="pipeNodeOptions"
      :nodes-loading="pipeNodesLoading"
      :search-nodes="searchNodesFor"
      :busy="busyKey === 'save'"
      @close="pipeDrawer.open = false"
      @submit="onPipelineSubmit"
    />

    <!-- CARD-DEP-02 运行详情抽屉（Detail JSON，5s 轮询由 useDeployData） -->
    <PipelineRunDetailDrawer
      :open="runDetailOpen"
      :state="runDetailState === 'idle' ? 'loading' : runDetailState"
      :detail="runDetail"
      @close="closeRunDetail"
      @open-steps="stepsDrawerOpen = true"
      @retry="retryRunDetail"
    />

    <!-- CARD-DEP-03 步骤明细抽屉（数据源 = 详情响应 steps[]，零新增请求） -->
    <PipelineStepDrawer
      :open="stepsDrawerOpen"
      :detail="runDetail"
      :nodes="nodes"
      @close="stepsDrawerOpen = false"
    />

    <!-- 概览：版本条之后是需关注事项；加载中不显示，加载失败时照常显示（部署相关项为待接入，实例数不依赖部署集） -->
    <AttentionList
      v-if="overview && !loading"
      :items="attention"
      @go="emit('go', $event)"
    />
  </div>
</template>
