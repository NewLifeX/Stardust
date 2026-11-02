// 发布数据组合函数：取数、刷新、操作反馈、应用版本、节点操作、选中运行记录。
// 从 ShipPanel.vue 移出，行为与原组件一致；各展示组件（DeployVersionBar、DeployNodes 等）共用同一份状态。
// 取数函数、request、通知、时钟都由调用方注入，便于在 Vitest 里不 mock 模块直接单测。
// 二次确认弹窗（openGuard）是 UI，留在容器里：用户确认后才调用这里的 applyVersion / operate。
import { computed, onScopeDispose, ref, watch } from 'vue';
import { fetchPipelines, fetchRunHistory } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
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

  const enabledNodes = computed(() => nodes.value.filter((n) => n.enable));

  function pushFeedback(ok: boolean, text: string): void {
    const at = new Date().toTimeString().slice(0, 8);
    feedback.value = [{ key: ++feedbackSeq, ok, text, at }, ...feedback.value].slice(0, 6);
    opt.notify(ok, text);
  }

  /** silent 为 true 时不置 loading、不清选中的运行与操作反馈（操作后刷新、手动刷新用） */
  async function load(silent = false): Promise<void> {
    const token = ++loadToken;
    if (!silent) {
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
  }

  watch(() => opt.appId(), () => void load(), { immediate: true });
  onScopeDispose(() => {
    loadToken++;
    if (reloadTimer) clearTimeout(reloadTimer);
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
  };
}