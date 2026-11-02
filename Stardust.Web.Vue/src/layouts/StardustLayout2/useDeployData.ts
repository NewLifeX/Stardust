// 发布数据组合函数：取数、刷新、操作反馈、应用版本、节点操作、选中运行记录。
// 从 ShipPanel.vue 移出，行为与原组件一致；各展示组件（DeployVersionBar、DeployNodes 等）共用同一份状态。
// 取数函数、request、通知、时钟都由调用方注入，便于在 Vitest 里不 mock 模块直接单测。
// 二次确认弹窗（openGuard）是 UI，留在容器里：用户确认后才调用这里的 applyVersion / operate。
import { computed, onScopeDispose, ref, watch } from 'vue';
import { fetchPipelines, fetchRunHistory, isRunActive } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import type { Req, RunHistoryRow } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import {
  actionInfo,
  fetchDeploy,
  fetchDeployHistory,
  fetchRunSteps,
  fetchShipNodes,
  fetchVersions,
  operateNode,
  useVersion,
  useVersionNotice,
} from './ship.logic.ts';
import type { DeployInfo, HistoryRow, NodeAction, ShipNode, StepRow, VersionOption } from './ship.logic.ts';
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
};

export interface DeployFeedback {
  key: number;
  ok: boolean;
  text: string;
  at: string;
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

  // 操作状态：busyKey 为空表示空闲；同一时刻只允许一个操作，避免命令交叉
  const busyKey = ref('');
  const busyText = ref('');
  const feedback = ref<DeployFeedback[]>([]);
  let feedbackSeq = 0;

  let loadToken = 0;
  let reloadTimer: ReturnType<typeof setTimeout> | undefined;
  // F09：运行未到终态时 5s 静默轮询；到终态/销毁/离开页签（scope 释放）即停
  let pollTimer: ReturnType<typeof setTimeout> | undefined;

  const enabledNodes = computed(() => nodes.value.filter((n) => n.enable));

  function pushFeedback(ok: boolean, text: string): void {
    const at = new Date().toTimeString().slice(0, 8);
    feedback.value = [{ key: ++feedbackSeq, ok, text, at }, ...feedback.value].slice(0, 6);
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
      versions.value = [];
      nodes.value = [];
      history.value = [];
      runs.value = [];
      loading.value = false;
      schedulePoll();
      return;
    }
    const dep = d.deploy;
    const [vs, ns, pipelines, hs] = await Promise.all([
      deps.fetchVersions(opt.request, dep.id, dep.version),
      deps.fetchShipNodes(opt.request, dep.id),
      deps.fetchPipelines(opt.request, dep.id),
      deps.fetchDeployHistory(opt.request, dep.id),
    ]);
    const rs = await deps.fetchRunHistory(opt.request, pipelines);
    if (token !== loadToken) return;
    deploy.value = dep;
    versionsFailed.value = vs === null;
    nodesFailed.value = ns === null;
    versions.value = vs ?? [];
    nodes.value = ns ?? [];
    history.value = hs;
    runs.value = rs;
    loading.value = false;
    schedulePoll();
  }

  watch(() => opt.appId(), () => void load(), { immediate: true });
  onScopeDispose(() => {
    loadToken++;
    if (reloadTimer) clearTimeout(reloadTimer);
    if (pollTimer) clearTimeout(pollTimer);
  });

  /** 命令是 fire-and-forget，节点装完需要几秒：立刻刷新一次，4 秒后再刷新一次 */
  function refreshSoon(): void {
    if (reloadTimer) clearTimeout(reloadTimer);
    void load(true);
    reloadTimer = setTimeout(() => void load(true), 4000);
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

  return {
    loading,
    loadError,
    deploy,
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
  };
}