// 发布数据组合函数：取数、刷新、操作反馈、应用版本、节点操作、选中运行记录。
// 从 ShipPanel.vue 移出，行为与原组件一致；各展示组件（DeployVersionBar、DeployNodes 等）共用同一份状态。
// 取数函数、request、通知、时钟都由调用方注入，便于在 Vitest 里不 mock 模块直接单测。
// 二次确认弹窗（openGuard）是 UI，留在容器里：用户确认后才调用这里的 applyVersion / operate。
import { computed, onScopeDispose, reactive, ref, watch } from 'vue';
import { fetchPipelines, fetchRunHistory, isRunActive } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import type { Req, RunHistoryRow } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import {
  actionInfo,
  fetchDeploy,
  fetchDeployHistory,
  fetchRunDetail,
  fetchRunHistoryFiltered,
  fetchRunSteps,
  fetchShipNodes,
  fetchVersions,
  isRunFilterActive,
  operateNode,
  reprocessRun,
  useVersion,
  useVersionNotice,
} from './ship.logic.ts';
import type { RunDetail, RunFilter } from './ship.logic.ts';
import {
  copyWebhookUrl,
  createPipeline,
  deletePipeline,
  fetchPipelineList,
  parseTriggerRunNo,
  triggerPipeline,
  updatePipeline,
} from './pipelineConfig.logic.ts';
import type { PipelineForm, PipelineRow } from './pipelineConfig.logic.ts';
import type { DeployInfo, DeployOthers, HistoryRow, NodeAction, ShipNode, StepRow, VersionOption } from './ship.logic.ts';
import {
  batchOperateNodes,
  createBuildNode,
  createDeployNode,
  deleteBuildNode,
  deleteDeployNode,
  saveDeploy,
  syncAppDeploy,
  updateBuildNode,
  updateDeployNode,
  uploadVersion,
} from './deploy.logic.ts';
import type { BuildNodeForm, DeployEditModel, DeployNodeForm } from './deploy.logic.ts';
import { useManualRefresh } from './useManualRefresh.ts';

/** 可注入的取数与操作函数，类型直接取自真实实现 */
export interface DeployDeps {
  fetchDeploy: typeof fetchDeploy;
  fetchVersions: typeof fetchVersions;
  fetchShipNodes: typeof fetchShipNodes;
  fetchDeployHistory: typeof fetchDeployHistory;
  fetchPipelines: typeof fetchPipelines;
  fetchRunHistory: typeof fetchRunHistory;
  fetchRunSteps: typeof fetchRunSteps;
  useVersion: typeof useVersion;
  operateNode: typeof operateNode;
  saveDeploy: typeof saveDeploy;
  syncAppDeploy: typeof syncAppDeploy;
  batchOperateNodes: typeof batchOperateNodes;
  uploadVersion: typeof uploadVersion;
  createDeployNode: typeof createDeployNode;
  updateDeployNode: typeof updateDeployNode;
  deleteDeployNode: typeof deleteDeployNode;
  createBuildNode: typeof createBuildNode;
  updateBuildNode: typeof updateBuildNode;
  deleteBuildNode: typeof deleteBuildNode;
  // —— CARD-DEP-01/02（2026-11-07 接线点登记）：流水线配置 CRUD/复制/启动 + 运行筛选/详情/重跑 ——
  fetchPipelineList: typeof fetchPipelineList;
  fetchRunHistoryFiltered: typeof fetchRunHistoryFiltered;
  fetchRunDetail: typeof fetchRunDetail;
  reprocessRun: typeof reprocessRun;
  createPipeline: typeof createPipeline;
  updatePipeline: typeof updatePipeline;
  deletePipeline: typeof deletePipeline;
  copyWebhookUrl: typeof copyWebhookUrl;
  triggerPipeline: typeof triggerPipeline;
}

export const DEFAULT_DEPLOY_DEPS: DeployDeps = {
  fetchDeploy,
  fetchVersions,
  fetchShipNodes,
  fetchDeployHistory,
  fetchPipelines,
  fetchRunHistory,
  fetchRunSteps,
  useVersion,
  operateNode,
  saveDeploy,
  syncAppDeploy,
  batchOperateNodes,
  uploadVersion,
  createDeployNode,
  updateDeployNode,
  deleteDeployNode,
  createBuildNode,
  updateBuildNode,
  deleteBuildNode,
  fetchPipelineList,
  fetchRunHistoryFiltered,
  fetchRunDetail,
  reprocessRun,
  createPipeline,
  updatePipeline,
  deletePipeline,
  copyWebhookUrl,
  triggerPipeline,
};

/** 反馈条目的直达动作（task-88 2026-11-12）：现阶段只有「打开该次运行的详情抽屉」 */
export interface DeployFeedbackAction {
  kind: 'openRun';
  label: string;
  /** AppPipelineRun.Id（触发/重跑 message「运行编号[N]」解析） */
  runId: number;
}

export interface DeployFeedback {
  key: number;
  ok: boolean;
  text: string;
  at: string;
  /** 成功触发/重跑流水线时携带「查看运行」直达动作；其余操作无 */
  action?: DeployFeedbackAction;
}

export interface DeployDataOptions {
  /** 当前应用 Id；变化时整体重取 */
  appId: () => number;
  appName: () => string;
  request: Req;
  /** 反馈同时弹 toast：页面传 toast，测试传记录函数 */
  notify: (ok: boolean, text: string) => void;
  deps?: DeployDeps;
  now?: () => number;
  sleep?: (ms: number) => Promise<void>;
  /** F09 轮询间隔毫秒数；测试传小值。默认 5000 */
  pollMs?: number;
}

export function useDeployData(opt: DeployDataOptions) {
  const deps = opt.deps ?? DEFAULT_DEPLOY_DEPS;

  const loading = ref(true);
  const loadError = ref('');
  const deploy = ref<DeployInfo | null>(null);
  // CARD-DEP-08 D2：同应用其余部署集摘要（单集应用 count=0，显示层判 0 不渲染副行）；加载失败/无集时为 null
  const others = ref<DeployOthers | null>(null);
  const versions = ref<VersionOption[]>([]);
  const versionsFailed = ref(false);
  const nodes = ref<ShipNode[]>([]);
  const nodesFailed = ref(false);
  // null：历史加载失败
  const history = ref<HistoryRow[] | null>([]);
  const runs = ref<RunHistoryRow[]>([]);
  const selectedRunId = ref(0);
  // undefined：未加载；null：加载失败
  const steps = ref<StepRow[] | null | undefined>(undefined);

  // —— CARD-DEP-01：流水线配置列表（pipelineConfig.logic 的行形状，含 DeployNodeIds/UpdateTime） ——
  const pipelines = ref<PipelineRow[]>([]);
  const pipelinesFailed = ref(false);

  // —— CARD-DEP-02：运行筛选（状态/时间区间）+ 详情抽屉 ——
  const runFilter = reactive<RunFilter>({ status: -1, dtStart: '', dtEnd: '' });
  const runDetailOpen = ref(false);
  const runDetail = ref<RunDetail | null>(null);
  const runDetailState = ref<'idle' | 'loading' | 'ready' | 'missing' | 'failed'>('idle');
  let detailTimer: ReturnType<typeof setTimeout> | undefined;

  // 操作状态：busyKey 为空表示空闲；同一时刻只允许一个操作，避免命令交叉
  const busyKey = ref('');
  const busyText = ref('');
  const feedback = ref<DeployFeedback[]>([]);
  let feedbackSeq = 0;

  let loadToken = 0;
  let reloadTimer: ReturnType<typeof setTimeout> | undefined;
  let reloadBackupTimer: ReturnType<typeof setTimeout> | undefined;
  // F09：运行未到终态时 5s 静默轮询；到终态/销毁/离开页签（scope 释放）即停
  let pollTimer: ReturnType<typeof setTimeout> | undefined;

  const enabledNodes = computed(() => nodes.value.filter((n) => n.enable));

  function pushFeedback(ok: boolean, text: string, action?: DeployFeedbackAction): void {
    const at = new Date().toTimeString().slice(0, 8);
    feedback.value = [{ key: ++feedbackSeq, ok, text, at, action }, ...feedback.value].slice(0, 6);
    opt.notify(ok, text);
  }

  /** 有进行中的运行就排下一次静默轮询（仅在 load 成功路径上调用，避免竞态叠加） */
  function schedulePoll(): void {
    if (pollTimer) clearTimeout(pollTimer);
    pollTimer = undefined;
    const active = runs.value.some((r) => isRunActive(r.status));
    if (!active) return;
    const ms = opt.pollMs ?? 5000;
    pollTimer = setTimeout(() => {
      pollTimer = undefined;
      void load(true).then(schedulePoll);
    }, ms);
  }

  /** silent 为 true 时不置 loading、不清选中的运行与操作反馈（操作后刷新、手动刷新用） */
  async function load(silent = false): Promise<void> {
    const token = ++loadToken;
    if (!silent) {
      // 非静默重取（换应用/手动整取）会重置全部状态：取消未到点的轮询，成功后由 schedulePoll 重新排
      if (pollTimer) clearTimeout(pollTimer);
      pollTimer = undefined;
      loading.value = true;
      selectedRunId.value = 0;
      steps.value = undefined;
      feedback.value = [];
    }
    loadError.value = '';
    const d = await deps.fetchDeploy(opt.request, opt.appId());
    if (token !== loadToken) return;
    if (d.status === 'failed') {
      loadError.value = '部署集加载失败';
      loading.value = false;
      return;
    }
    if (d.status === 'none') {
      deploy.value = null;
      others.value = null;
      versions.value = [];
      nodes.value = [];
      history.value = [];
      runs.value = [];
      pipelines.value = [];
      loading.value = false;
      schedulePoll();
      return;
    }
    const dep = d.deploy;
    const [vs, ns, pipelines0, hs, cfg] = await Promise.all([
      deps.fetchVersions(opt.request, dep.id, dep.version),
      deps.fetchShipNodes(opt.request, dep.id),
      deps.fetchPipelines(opt.request, dep.id),
      deps.fetchDeployHistory(opt.request, dep.id),
      deps.fetchPipelineList(opt.request, dep.id),
    ]);
    const rs = isRunFilterActive(runFilter)
      ? await deps.fetchRunHistoryFiltered(opt.request, pipelines0, runFilter)
      : await deps.fetchRunHistory(opt.request, pipelines0);
    if (token !== loadToken) return;
    deploy.value = dep;
    others.value = d.others ?? null;
    versionsFailed.value = vs === null;
    nodesFailed.value = ns === null;
    versions.value = vs ?? [];
    nodes.value = ns ?? [];
    history.value = hs;
    runs.value = rs;
    pipelinesFailed.value = cfg === null;
    pipelines.value = cfg ?? [];
    loading.value = false;
    schedulePoll();
  }

  watch(() => opt.appId(), () => void load(), { immediate: true });
  onScopeDispose(() => {
    loadToken++;
    if (reloadTimer) clearTimeout(reloadTimer);
    if (reloadBackupTimer) clearTimeout(reloadBackupTimer);
    if (pollTimer) clearTimeout(pollTimer);
    if (detailTimer) clearTimeout(detailTimer);
  });

  /** 写操作后自动重取（走查缺陷1，2026-11-07 实测定位）：400ms 首刷（避开写事务提交时差），
   *  首刷完成后再接 3.6s 兜底（节点命令 fire-and-forget 装完需要几秒，保留第二次）。
   *  原实现「400ms/4s 两个独立定时器」在后端慢（实测单次 load 全程 3s+）时必然交叠：
   *  后启动的 load ++loadToken 作废先启动的（load() 尾部 discard），上屏被推迟到最后一个 load 跑完，
   *  表现为「toast 成功但卡长时间不刷新」。串行链保证同一时刻最多一个重取在跑，永不互相作废；
   *  重复写操作重排定时器，旧链若已在飞，由更新链收敛（最后启动的 load 拿最新数据上屏）。 */
  function refreshSoon(): void {
    if (reloadTimer) clearTimeout(reloadTimer);
    if (reloadBackupTimer) clearTimeout(reloadBackupTimer);
    reloadTimer = setTimeout(() => {
      reloadTimer = undefined;
      void load(true).then(() => {
        if (reloadBackupTimer) clearTimeout(reloadBackupTimer);
        reloadBackupTimer = setTimeout(() => {
          reloadBackupTimer = undefined;
          void load(true);
        }, 3600);
      });
    }, 400);
  }

  /** 手动刷新：状态机在 useManualRefresh（防重入、至少旋转 300ms、失败给可见提示，AGENTS.md §11） */
  const { refreshing, refresh } = useManualRefresh({
    load: () => load(true),
    outcome: () => ({
      loadError: loadError.value,
      versionsFailed: versionsFailed.value,
      nodesFailed: nodesFailed.value,
      historyFailed: history.value === null,
    }),
    notify: pushFeedback,
    ...(opt.now ? { now: opt.now } : {}),
    ...(opt.sleep ? { sleep: opt.sleep } : {}),
  });

  /** 应用此版本（UseVersion）。调用前由容器完成二次确认 */
  async function applyVersion(v: VersionOption): Promise<void> {
    if (busyKey.value || v.disabled) return;
    const name = opt.appName();
    const pushes = useVersionNotice(deploy.value, enabledNodes.value.length)?.tone === 'warn';
    const count = enabledNodes.value.length;
    busyKey.value = `ver:${v.id}`;
    busyText.value = `正在把 ${name} 切换到 ${v.version}…`;
    const r = await deps.useVersion(opt.request, v.id);
    busyKey.value = '';
    busyText.value = '';
    if (r.ok) {
      pushFeedback(
        true,
        `${name} 已切换到 ${v.version}。` +
          (pushes
            ? `已向 ${count} 个启用节点下发安装，命令只表示已发出，是否装好请看「部署节点」的版本。`
            : '没有向节点下发，要装到节点请在「部署节点」里操作。'),
      );
    } else {
      pushFeedback(false, `${name} 切换到 ${v.version} 失败：${r.message}`);
    }
    refreshSoon();
  }

  /** 对单个部署节点下发操作。调用前由容器完成二次确认 */
  async function operate(n: ShipNode, act: NodeAction): Promise<void> {
    if (busyKey.value) return;
    const label = actionInfo(act)?.label ?? act;
    busyKey.value = `${n.id}:${act}`;
    busyText.value = `正在向 ${n.name} 下发「${label}」…`;
    const r = await deps.operateNode(opt.request, act, n);
    busyKey.value = '';
    busyText.value = '';
    pushFeedback(r.ok, r.message);
    refreshSoon();
  }

  /** F06：保存部署集编辑（容器已完成表单校验与 Drawer 提交） */
  async function saveDeployEdit(m: DeployEditModel): Promise<boolean> {
    const id = deploy.value?.id ?? 0;
    if (!id || busyKey.value) return false;
    busyKey.value = 'deploy-save';
    busyText.value = '正在保存部署集…';
    const r = await deps.saveDeploy(opt.request, id, m);
    busyKey.value = '';
    busyText.value = '';
    pushFeedback(r.ok, r.ok ? `${opt.appName()}：${r.message}` : `${opt.appName()} 部署集保存失败：${r.message}`);
    refreshSoon();
    return r.ok;
  }

  /** F06：同步应用（SyncApp 同步全部应用，后端返回「成功同步[N]个应用！」） */
  async function syncApp(): Promise<void> {
    if (busyKey.value) return;
    busyKey.value = 'deploy-sync';
    busyText.value = '正在同步应用部署集…';
    const r = await deps.syncAppDeploy(opt.request);
    busyKey.value = '';
    busyText.value = '';
    pushFeedback(r.ok, r.message);
    refreshSoon();
  }

  /** F07：批量远程操作（keys 为勾选的 AppDeployNode.Id）。调用前由容器完成二次确认 */
  async function batchOperate(act: NodeAction, ids: number[], names: string[]): Promise<void> {
    if (busyKey.value || ids.length === 0) return;
    const label = actionInfo(act)?.label ?? act;
    const who = names.length > 3 ? `${names.slice(0, 3).join('、')} 等 ${names.length} 个节点` : names.join('、');
    busyKey.value = `batch:${act}`;
    busyText.value = `正在向 ${who} 批量下发「${label}」…`;
    const r = await deps.batchOperateNodes(opt.request, act, ids);
    busyKey.value = '';
    busyText.value = '';
    pushFeedback(r.ok, r.ok ? `${who}：${r.message}` : `${who} 批量「${label}」失败：${r.message}`);
    refreshSoon();
  }

  /** F05：上传新版本。成功文案带上后端生成的版本号；开自动发布时上传即发布，提示里说明 */
  async function uploadNewVersion(file: File, remark?: string): Promise<boolean> {
    const id = deploy.value?.id ?? 0;
    if (!id || busyKey.value) return false;
    busyKey.value = 'upload';
    busyText.value = `正在上传 ${file.name}（${Math.max(1, Math.round(file.size / 1024))} KB）…`;
    const r = await deps.uploadVersion(opt.request, id, file, remark);
    busyKey.value = '';
    busyText.value = '';
    if (r.ok) {
      const ver = r.version ? `，新版本 ${r.version}` : '';
      const auto = deploy.value?.autoPublish ? '；部署集已开自动发布，上传即向启用节点发布' : '';
      pushFeedback(true, `${opt.appName()} 上传成功${ver}${auto}。`);
    } else {
      pushFeedback(false, `${opt.appName()} 上传失败：${r.message}`);
    }
    refreshSoon();
    return r.ok;
  }

  /** F07：新增/编辑部署节点（id 为空走新增）。成功返回 true 供容器关 Drawer */
  async function saveNode(m: DeployNodeForm, id?: number): Promise<boolean> {
    const deployId = deploy.value?.id ?? 0;
    if (!deployId || busyKey.value) return false;
    busyKey.value = id ? `node:${id}` : 'node:new';
    busyText.value = id ? '正在保存部署节点…' : '正在添加部署节点…';
    const r = id ? await deps.updateDeployNode(opt.request, id, m) : await deps.createDeployNode(opt.request, deployId, m);
    busyKey.value = '';
    busyText.value = '';
    pushFeedback(r.ok, r.ok ? r.message : `${opt.appName()} 部署节点保存失败：${r.message}`);
    refreshSoon();
    return r.ok;
  }

  /** F07：删除部署节点。调用前由容器完成二次确认 */
  async function removeNode(id: number, name: string): Promise<void> {
    if (busyKey.value) return;
    busyKey.value = `node-del:${id}`;
    busyText.value = `正在删除部署节点 ${name}…`;
    const r = await deps.deleteDeployNode(opt.request, id);
    busyKey.value = '';
    busyText.value = '';
    pushFeedback(r.ok, r.ok ? r.message : `删除部署节点 ${name} 失败：${r.message}`);
    refreshSoon();
  }

  /** F08：新增/编辑编译节点 */
  async function saveBuildNode(m: BuildNodeForm, id?: number): Promise<boolean> {
    const deployId = deploy.value?.id ?? 0;
    if (!deployId || busyKey.value) return false;
    busyKey.value = id ? `bnode:${id}` : 'bnode:new';
    busyText.value = id ? '正在保存编译节点…' : '正在添加编译节点…';
    const r = id ? await deps.updateBuildNode(opt.request, id, m) : await deps.createBuildNode(opt.request, deployId, m);
    busyKey.value = '';
    busyText.value = '';
    pushFeedback(r.ok, r.ok ? r.message : `${opt.appName()} 编译节点保存失败：${r.message}`);
    refreshSoon();
    return r.ok;
  }

  /** F08：删除编译节点。调用前由容器完成二次确认 */
  async function removeBuildNode(id: number, name: string): Promise<void> {
    if (busyKey.value) return;
    busyKey.value = `bnode-del:${id}`;
    busyText.value = `正在删除编译节点 ${name}…`;
    const r = await deps.deleteBuildNode(opt.request, id);
    busyKey.value = '';
    busyText.value = '';
    pushFeedback(r.ok, r.ok ? r.message : `删除编译节点 ${name} 失败：${r.message}`);
    refreshSoon();
  }

  /** 选中一条运行记录并拉取步骤；再点同一条取消选中 */
  async function selectRun(id: number): Promise<void> {
    if (!id) return;
    if (selectedRunId.value === id) {
      selectedRunId.value = 0;
      steps.value = undefined;
      return;
    }
    selectedRunId.value = id;
    steps.value = undefined;
    const s = await deps.fetchRunSteps(opt.request, id);
    if (selectedRunId.value === id) steps.value = s;
  }

  // ---------- CARD-DEP-01：流水线配置操作（Guard 确认在容器） ----------

  async function savePipeline(m: PipelineForm, id?: number): Promise<boolean> {
    const deployId = deploy.value?.id ?? 0;
    if (busyKey.value) return false;
    busyKey.value = 'save';
    busyText.value = id ? '正在保存流水线…' : '正在创建流水线…';
    const r = id ? await deps.updatePipeline(opt.request, id, m) : await deps.createPipeline(opt.request, deployId, m);
    busyKey.value = '';
    busyText.value = '';
    pushFeedback(r.ok, r.ok ? r.message : `流水线保存失败：${r.message}`);
    refreshSoon();
    return r.ok;
  }

  async function removePipeline(row: PipelineRow): Promise<void> {
    if (busyKey.value) return;
    busyKey.value = `del:${row.id}`;
    busyText.value = `正在删除流水线 ${row.name}…`;
    const r = await deps.deletePipeline(opt.request, row.id);
    busyKey.value = '';
    busyText.value = '';
    pushFeedback(r.ok, r.ok ? r.message : `删除流水线 ${row.name} 失败：${r.message}`);
    refreshSoon();
  }

  /** 复制 WebHook：返回完整 URL，剪贴板与成功 toast 在容器（文案「WebHook 地址已复制到剪贴板」） */
  async function copyWebhookFor(row: PipelineRow): Promise<{ ok: boolean; url: string; message: string }> {
    if (busyKey.value) return { ok: false, url: '', message: '已有操作进行中' };
    busyKey.value = `copy:${row.id}`;
    busyText.value = `正在获取 ${row.name} 的 WebHook 地址…`;
    const r = await deps.copyWebhookUrl(opt.request, row.id);
    busyKey.value = '';
    busyText.value = '';
    if (!r.ok) pushFeedback(false, `复制 WebHook 地址失败：${r.message}`);
    return r;
  }

  async function triggerRun(row: PipelineRow): Promise<void> {
    if (busyKey.value) return;
    busyKey.value = `trigger:${row.id}`;
    busyText.value = `正在触发流水线 ${row.name}…`;
    const r = await deps.triggerPipeline(opt.request, row.id);
    busyKey.value = '';
    busyText.value = '';
    // task-88（2026-11-12）：成功反馈带「查看运行」直达（运行编号[N] = AppPipelineRun.Id），并选中该次运行——
    // 行高亮 + 头部状态徽标 + 四段进度立即可见；新运行行由 refreshSoon/5s 轮询上屏（不新增轮询）。
    if (r.ok) {
      const runId = parseTriggerRunNo(r.message);
      pushFeedback(true, r.message, runId > 0 ? { kind: 'openRun', label: '查看运行', runId } : undefined);
      if (runId > 0 && selectedRunId.value !== runId) void selectRun(runId);
    } else {
      pushFeedback(false, `触发流水线 ${row.name} 失败：${r.message}`);
    }
    refreshSoon();
  }

  // ---------- CARD-DEP-02：运行筛选 / 详情抽屉 / 重跑 ----------

  /** 应用筛选并重取（轮询按同筛选静默重取，语义不变） */
  function setRunFilter(f: Partial<RunFilter>): void {
    Object.assign(runFilter, f);
    void load(true);
  }

  function resetRunFilter(): void {
    Object.assign(runFilter, { status: -1, dtStart: '', dtEnd: '' });
    void load(true);
  }

  function stopDetailPoll(): void {
    if (detailTimer) clearTimeout(detailTimer);
    detailTimer = undefined;
  }

  /** 详情 5s 轮询（统一口径，裁定 #4）：状态 0-3 时静默重取，到终态/关闭/离开页面停止 */
  function scheduleDetailPoll(): void {
    stopDetailPoll();
    if (!runDetailOpen.value || !runDetail.value || !isRunActive(runDetail.value.status)) return;
    const ms = opt.pollMs ?? 5000;
    detailTimer = setTimeout(() => {
      detailTimer = undefined;
      void pollDetail().then(scheduleDetailPoll);
    }, ms);
  }

  async function pollDetail(): Promise<void> {
    if (!runDetailOpen.value || !runDetail.value) return;
    const id = runDetail.value.id;
    const d = await deps.fetchRunDetail(opt.request, id);
    if (!runDetailOpen.value || runDetail.value?.id !== id) return;
    if (d === 'missing') {
      runDetail.value = null;
      runDetailState.value = 'missing';
      stopDetailPoll();
      return;
    }
    if (d) {
      runDetail.value = fillRunDetail(d);
      runDetailState.value = 'ready';
    }
  }

  /** 容器回填：流水线名与产物版本号（详情接口只回 Id） */
  function fillRunDetail(d: RunDetail): RunDetail {
    d.pipelineName = pipelines.value.find((p) => p.id === d.pipelineId)?.name ?? (d.pipelineId > 0 ? `#${d.pipelineId}` : '—');
    d.versionLabel = versions.value.find((v) => v.id === d.appVersionId)?.version ?? (d.appVersionId > 0 ? `#${d.appVersionId}` : '—');
    return d;
  }

  async function openRunDetail(id: number): Promise<void> {
    if (!id) return;
    runDetailOpen.value = true;
    runDetailState.value = 'loading';
    runDetail.value = null;
    const d = await deps.fetchRunDetail(opt.request, id);
    if (!runDetailOpen.value) return;
    if (d === 'missing') {
      runDetail.value = null;
      runDetailState.value = 'missing';
      return;
    }
    if (!d) {
      runDetailState.value = 'failed';
      return;
    }
    runDetail.value = fillRunDetail(d);
    runDetailState.value = 'ready';
    scheduleDetailPoll();
  }

  function closeRunDetail(): void {
    runDetailOpen.value = false;
    stopDetailPoll();
    runDetail.value = null;
    runDetailState.value = 'idle';
  }

  function retryRunDetail(): void {
    const id = runDetail.value?.id ?? 0;
    void openRunDetail(id);
  }

  async function rerunRun(id: number): Promise<void> {
    if (busyKey.value) return;
    busyKey.value = `rerun:${id}`;
    busyText.value = '正在重跑流水线…';
    const r = await deps.reprocessRun(opt.request, id);
    busyKey.value = '';
    busyText.value = '';
    // task-88：重跑与手动触发同格式（「流水线已重新触发，运行编号[N]」），同样给「查看运行」直达 + 选中高亮
    if (r.ok) {
      const runId = parseTriggerRunNo(r.message);
      pushFeedback(true, r.message, runId > 0 ? { kind: 'openRun', label: '查看运行', runId } : undefined);
      if (runId > 0 && selectedRunId.value !== runId) void selectRun(runId);
    } else {
      pushFeedback(false, `重跑失败：${r.message}`);
    }
    refreshSoon();
  }

  return {
    loading,
    loadError,
    deploy,
    versions,
    versionsFailed,
    nodes,
    nodesFailed,
    others,
    history,
    runs,
    selectedRunId,
    steps,
    busyKey,
    busyText,
    feedback,
    enabledNodes,
    refreshing,
    refresh,
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
    // —— CARD-DEP-01/02（2026-11-07）：流水线配置 + 运行筛选/详情/重跑 ——
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
  };
}