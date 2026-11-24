// 发布数据组合函数单测（Vitest）：取数函数、request、时钟全部注入，不 mock 模块。
import { describe, it, expect, vi } from 'vitest';
import { effectScope, nextTick, ref } from 'vue';
import { useDeployData } from './useDeployData.ts';
import type { DeployDeps } from './useDeployData.ts';
import type { DeployInfo, HistoryRow, RunDetail, ShipNode, StepRow, VersionOption } from './ship.logic.ts';
import type { Req, RunHistoryRow } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

const request: Req = async () => ({});

function dep(appId: number, version = `v${appId}`): DeployInfo {
  return { id: appId * 10, appId, name: '', version, enable: true, autoPublish: false, multiVersion: false };
}
function ver(id: number, version: string): VersionOption {
  return { id, version, disabled: false, reason: '', current: false, label: version, url: '' };
}
function node(id: number, name: string): ShipNode {
  return { id, nodeId: id, name, ip: '10.0.0.1', enable: true, version: 'v1', appVersion: '', mode: 10, modeLabel: 'Standard', delay: 0, processId: 0, startTime: '—', compile: '—', lastActive: '—' };
}
const HIST: HistoryRow = {
  id: 1, deployId: 10, nodeId: 1, node: 'n1', action: 'install', actionLabel: '安装',
  success: true, remark: '', traceId: '', at: '', time: '', ip: '',
};
const RUNS: RunHistoryRow[] = [{ id: 11 }, { id: 12 }];

function baseDeps(): DeployDeps {
  return {
    fetchDeploy: async (_r, appId) => ({ status: 'ok', deploy: dep(appId), others: { count: 0, nodes: 0 } }),
    fetchVersions: async () => [ver(2, 'v2'), ver(1, 'v1')],
    fetchShipNodes: async () => [node(1, 'n1')],
    fetchDeployHistory: async () => [HIST],
    fetchPipelines: async () => [{ id: 5 }],
    fetchRunHistory: async () => RUNS,
    fetchRunSteps: async () => [] as StepRow[],
    useVersion: async () => ({ ok: true, message: '已下发部署命令' }),
    operateNode: async (_r, _act, n) => ({ ok: true, message: `${n.name}：已下发` }),
    saveDeploy: async () => ({ ok: true, message: '部署集已保存' }),
    syncAppDeploy: async () => ({ ok: true, message: '成功同步[0]个应用！' }),
    batchOperateNodes: async (_r, act, ids) => ({ ok: true, message: `批量执行[${act}]操作（${ids.length}）` }),
    uploadVersion: async () => ({ ok: true, message: '上传成功', version: '20261103-120001' }),
    createDeployNode: async () => ({ ok: true, message: '部署节点已添加' }),
    updateDeployNode: async () => ({ ok: true, message: '部署节点已保存' }),
    deleteDeployNode: async () => ({ ok: true, message: '部署节点已删除' }),
    createBuildNode: async () => ({ ok: true, message: '编译节点已添加' }),
    updateBuildNode: async () => ({ ok: true, message: '编译节点已保存' }),
    deleteBuildNode: async () => ({ ok: true, message: '编译节点已删除' }),
    // CARD-DEP-01/02：流水线配置与运行详情/重跑
    fetchPipelineList: async () => [],
    fetchRunHistoryFiltered: async () => [],
    fetchRunDetail: async () => 'missing' as const,
    reprocessRun: async () => ({ ok: true, message: '流水线已重新触发，运行编号[42]' }),
    cancelRun: async () => ({ ok: true, message: '流水线运行[42]已取消' }),
    createPipeline: async () => ({ ok: true, message: '流水线已创建' }),
    updatePipeline: async () => ({ ok: true, message: '流水线已保存' }),
    deletePipeline: async () => ({ ok: true, message: '流水线已删除' }),
    copyWebhookUrl: async () => ({ ok: true, message: 'ok', url: 'http://s/Deployment/Pipeline/Webhook?token=t' }),
    triggerPipeline: async () => ({ ok: true, message: '流水线已触发，运行编号[42]' }),
  };
}
function deferred<T>() {
  let resolve!: (v: T) => void;
  const promise = new Promise<T>((r) => {
    resolve = r;
  });
  return { promise, resolve };
}

/** 让已排队的微任务与 watch 回调都跑完 */
async function settle(): Promise<void> {
  await nextTick();
  for (let i = 0; i < 20; i++) await Promise.resolve();
}

/** 每个 it 自己调用，拿到独立的 scope、时钟与记录 */
function setup(over: Partial<DeployDeps> = {}, appIdInit = 1) {
  const appId = ref(appIdInit);
  const toasts: Array<{ ok: boolean; text: string }> = [];
  const slept: number[] = [];
  const deps: DeployDeps = { ...baseDeps(), ...over };
  const scope = effectScope();
  const d = scope.run(() =>
    useDeployData({
      appId: () => appId.value,
      appName: () => 'S',
      request,
      notify: (ok, text) => toasts.push({ ok, text }),
      deps,
      now: () => 1000,
      sleep: async (ms) => {
        slept.push(ms);
      },
    }),
  )!;
  return { d, appId, toasts, slept, scope };
}

describe('useDeployData · 取数', () => {
  it('初始 loading，取完后各区块写入并结束 loading', async () => {
    const s = setup();
    expect(s.d.loading.value).toBe(true);
    await settle();
    expect(s.d.loading.value).toBe(false);
    expect(s.d.loadError.value).toBe('');
    expect(s.d.deploy.value?.version).toBe('v1');
    expect(s.d.versions.value.map((v) => v.version)).toEqual(['v2', 'v1']);
    expect(s.d.nodes.value.map((n) => n.name)).toEqual(['n1']);
    expect(s.d.history.value).toEqual([HIST]);
    expect(s.d.runs.value).toEqual(RUNS);
    s.scope.stop();
  });

  it('没有部署集：各区块清空，不再取版本与节点', async () => {
    const fetchVersions = vi.fn<DeployDeps['fetchVersions']>(async () => []);
    const s = setup({ fetchDeploy: async () => ({ status: 'none' }), fetchVersions });
    await settle();
    expect(s.d.loading.value).toBe(false);
    expect(s.d.deploy.value).toBeNull();
    expect(s.d.versions.value).toEqual([]);
    expect(s.d.nodes.value).toEqual([]);
    expect(fetchVersions).not.toHaveBeenCalled();
    s.scope.stop();
  });

  it('部署集加载失败：给出 loadError 并结束 loading', async () => {
    const s = setup({ fetchDeploy: async () => ({ status: 'failed' }) });
    await settle();
    expect(s.d.loading.value).toBe(false);
    expect(s.d.loadError.value).toBe('部署集加载失败');
    s.scope.stop();
  });

  it('换 appId 整体重取', async () => {
    const s = setup();
    await settle();
    expect(s.d.deploy.value?.appId).toBe(1);
    s.appId.value = 2;
    await settle();
    expect(s.d.deploy.value?.appId).toBe(2);
    expect(s.d.deploy.value?.version).toBe('v2');
    s.scope.stop();
  });

  it('慢的旧响应不覆盖新应用的数据', async () => {
    const slow = deferred<Awaited<ReturnType<DeployDeps['fetchDeploy']>>>();
    const s = setup({
      fetchDeploy: (_r, appId) => (appId === 1 ? slow.promise : Promise.resolve({ status: 'ok', deploy: dep(appId), others: { count: 0, nodes: 0 } })),
    });
    s.appId.value = 2;
    await settle();
    expect(s.d.deploy.value?.version).toBe('v2');
    slow.resolve({ status: 'ok', deploy: dep(1, 'old'), others: { count: 0, nodes: 0 } });
    await settle();
    expect(s.d.deploy.value?.version).toBe('v2');
    s.scope.stop();
  });

  it('静默刷新：不置 loading，不清选中的运行与操作反馈', async () => {
    const s = setup();
    await settle();
    await s.d.selectRun(11);
    expect(s.d.selectedRunId.value).toBe(11);
    const p = s.d.load(true);
    expect(s.d.loading.value).toBe(false);
    await p;
    expect(s.d.selectedRunId.value).toBe(11);
    s.scope.stop();
  });

  it('非静默重取：清掉选中的运行与操作反馈', async () => {
    vi.useFakeTimers();
    try {
      const s = setup();
      await settle();
      await s.d.operate(node(1, 'n1'), 'restart');
      await s.d.selectRun(11);
      expect(s.d.feedback.value).toHaveLength(1);
      await s.d.load();
      expect(s.d.selectedRunId.value).toBe(0);
      expect(s.d.feedback.value).toEqual([]);
      s.scope.stop();
    } finally {
      vi.useRealTimers();
    }
  });
});

describe('useDeployData · 手动刷新（AGENTS.md §11）', () => {
  it('刷新进行中 refreshing 为 true，请求很快也补足旋转时间', async () => {
    const s = setup();
    await settle();
    const p = s.d.refresh();
    expect(s.d.refreshing.value).toBe(true);
    await p;
    expect(s.d.refreshing.value).toBe(false);
    expect(s.slept).toEqual([300]);
    s.scope.stop();
  });

  it('刷新时版本列表加载失败：给出可见的失败提示', async () => {
    const s = setup({ fetchVersions: async () => null });
    await settle();
    await s.d.refresh();
    expect(s.toasts.some((t) => !t.ok)).toBe(true);
    expect(s.d.feedback.value[0]?.ok).toBe(false);
    s.scope.stop();
  });
});

describe('useDeployData · 应用此版本', () => {
  it('成功：调 useVersion，进行中有 busyKey，结束后写入成功反馈', async () => {
    vi.useFakeTimers();
    try {
      const gate = deferred<{ ok: boolean; message: string }>();
      const useVersion = vi.fn<DeployDeps['useVersion']>(() => gate.promise);
      const s = setup({ useVersion });
      await settle();
      const p = s.d.applyVersion(ver(2, 'v2'));
      expect(s.d.busyKey.value).toBe('ver:2');
      expect(s.d.busyText.value).toContain('v2');
      gate.resolve({ ok: true, message: '' });
      await p;
      expect(useVersion).toHaveBeenCalledWith(request, 2);
      expect(s.d.busyKey.value).toBe('');
      expect(s.d.feedback.value[0]?.ok).toBe(true);
      expect(s.d.feedback.value[0]?.text).toContain('已切换到 v2');
      s.scope.stop();
    } finally {
      vi.useRealTimers();
    }
  });

  it('失败：反馈写明对象、版本与原因', async () => {
    vi.useFakeTimers();
    try {
      const s = setup({ useVersion: async () => ({ ok: false, message: 'boom' }) });
      await settle();
      await s.d.applyVersion(ver(2, 'v2'));
      const fb = s.d.feedback.value[0];
      expect(fb?.ok).toBe(false);
      expect(fb?.text).toContain('S 切换到 v2 失败：boom');
      s.scope.stop();
    } finally {
      vi.useRealTimers();
    }
  });

  it('已置灰的版本不能应用', async () => {
    const useVersion = vi.fn<DeployDeps['useVersion']>(async () => ({ ok: true, message: '' }));
    const s = setup({ useVersion });
    await settle();
    await s.d.applyVersion({ ...ver(3, 'v3'), disabled: true, reason: '版本未启用' });
    expect(useVersion).not.toHaveBeenCalled();
    s.scope.stop();
  });
});

describe('useDeployData · 节点操作', () => {
  it('进行中有 busyKey 与提示文案，结束后反馈用接口返回的文案', async () => {
    vi.useFakeTimers();
    try {
      const gate = deferred<{ ok: boolean; message: string }>();
      const operateNode = vi.fn<DeployDeps['operateNode']>(() => gate.promise);
      const s = setup({ operateNode });
      await settle();
      const n = node(7, 'n7');
      const p = s.d.operate(n, 'restart');
      expect(s.d.busyKey.value).toBe('7:restart');
      expect(s.d.busyText.value).toContain('n7');
      gate.resolve({ ok: true, message: 'n7：已下发' });
      await p;
      expect(operateNode).toHaveBeenCalledWith(request, 'restart', n);
      expect(s.d.busyKey.value).toBe('');
      expect(s.d.feedback.value[0]).toMatchObject({ ok: true, text: 'n7：已下发' });
      expect(s.toasts).toContainEqual({ ok: true, text: 'n7：已下发' });
      s.scope.stop();
    } finally {
      vi.useRealTimers();
    }
  });

  it('有操作在进行时，再次触发被忽略', async () => {
    vi.useFakeTimers();
    try {
      const gate = deferred<{ ok: boolean; message: string }>();
      const operateNode = vi.fn<DeployDeps['operateNode']>(() => gate.promise);
      const s = setup({ operateNode });
      await settle();
      const p1 = s.d.operate(node(1, 'n1'), 'start');
      await s.d.operate(node(2, 'n2'), 'stop');
      expect(operateNode).toHaveBeenCalledTimes(1);
      gate.resolve({ ok: true, message: 'ok' });
      await p1;
      s.scope.stop();
    } finally {
      vi.useRealTimers();
    }
  });

  it('操作后 400ms 首刷一次，4 秒兜底再刷一次（走查缺陷1：避开写事务提交时差）', async () => {
    vi.useFakeTimers();
    try {
      const fetchDeploy = vi.fn<DeployDeps['fetchDeploy']>(async (_r, appId) => ({ status: 'ok', deploy: dep(appId), others: { count: 0, nodes: 0 } }));
      const s = setup({ fetchDeploy });
      await settle();
      expect(fetchDeploy).toHaveBeenCalledTimes(1);
      await s.d.operate(node(1, 'n1'), 'install');
      await settle();
      // 不再立即刷：立即 load 可能读到写事务提交前的旧数据
      expect(fetchDeploy).toHaveBeenCalledTimes(1);
      await vi.advanceTimersByTimeAsync(400);
      await settle();
      expect(fetchDeploy).toHaveBeenCalledTimes(2);
      await vi.advanceTimersByTimeAsync(3600);
      await settle();
      expect(fetchDeploy).toHaveBeenCalledTimes(3);
      s.scope.stop();
    } finally {
      vi.useRealTimers();
    }
  });

  it('操作反馈只保留最近 6 条，新的在前', async () => {
    vi.useFakeTimers();
    try {
      let seq = 0;
      const s = setup({ operateNode: async () => ({ ok: true, message: `第${++seq}次` }) });
      await settle();
      for (let i = 0; i < 7; i++) await s.d.operate(node(1, 'n1'), 'start');
      expect(s.d.feedback.value).toHaveLength(6);
      expect(s.d.feedback.value[0]?.text).toBe('第7次');
      expect(s.d.feedback.value[5]?.text).toBe('第2次');
      s.scope.stop();
    } finally {
      vi.useRealTimers();
    }
  });
});

describe('useDeployData · 选中运行记录', () => {
  it('点一条拉取步骤；再点同一条取消选中', async () => {
    const fetchRunSteps = vi.fn<DeployDeps['fetchRunSteps']>(async () => [] as StepRow[]);
    const s = setup({ fetchRunSteps });
    await settle();
    expect(s.d.steps.value).toBeUndefined();
    await s.d.selectRun(11);
    expect(fetchRunSteps).toHaveBeenCalledWith(request, 11);
    expect(s.d.selectedRunId.value).toBe(11);
    expect(s.d.steps.value).toEqual([]);
    await s.d.selectRun(11);
    expect(s.d.selectedRunId.value).toBe(0);
    expect(s.d.steps.value).toBeUndefined();
    s.scope.stop();
  });

  it('切换到另一条后，上一条迟到的步骤不会覆盖', async () => {
    const slow = deferred<StepRow[] | null>();
    const s = setup({ fetchRunSteps: (_r, runId) => (runId === 11 ? slow.promise : Promise.resolve([] as StepRow[])) });
    await settle();
    const p1 = s.d.selectRun(11);
    await s.d.selectRun(12);
    expect(s.d.selectedRunId.value).toBe(12);
    slow.resolve(null);
    await p1;
    expect(s.d.steps.value).toEqual([]);
    s.scope.stop();
  });
});

describe('useDeployData · 销毁', () => {
  it('scope 停止后，在途请求的结果不再写入', async () => {
    const slow = deferred<Awaited<ReturnType<DeployDeps['fetchDeploy']>>>();
    const s = setup({ fetchDeploy: () => slow.promise });
    s.scope.stop();
    slow.resolve({ status: 'ok', deploy: dep(1), others: { count: 0, nodes: 0 } });
    await settle();
    expect(s.d.deploy.value).toBeNull();
  });

  it('scope 停止后，等待中的 4 秒刷新被取消', async () => {
    vi.useFakeTimers();
    try {
      const fetchDeploy = vi.fn<DeployDeps['fetchDeploy']>(async (_r, appId) => ({ status: 'ok', deploy: dep(appId), others: { count: 0, nodes: 0 } }));
      const s = setup({ fetchDeploy });
      await settle();
      await s.d.operate(node(1, 'n1'), 'install');
      await settle();
      const before = fetchDeploy.mock.calls.length;
      s.scope.stop();
      await vi.advanceTimersByTimeAsync(5000);
      expect(fetchDeploy.mock.calls.length).toBe(before);
    } finally {
      vi.useRealTimers();
    }
  });
});

describe('useDeployData · F09 运行轮询', () => {
  function runWith(status: number): RunHistoryRow {
    return { id: 99, status: String(status) };
  }

  it('有进行中的运行：pollMs 后静默重取，轮询链持续到无活跃运行', async () => {
    vi.useFakeTimers();
    try {
      const fetchDeploy = vi.fn<DeployDeps['fetchDeploy']>(async (_r, appId) => ({ status: 'ok', deploy: dep(appId), others: { count: 0, nodes: 0 } }));
      // 第一轮运行是 Deploying(3)，之后两轮切到 Success(4)
      let round = 0;
      const fetchRunHistory = vi.fn<DeployDeps['fetchRunHistory']>(async () => {
        round++;
        return [runWith(round <= 2 ? 3 : 4)];
      });
      setup({ fetchDeploy, fetchRunHistory });
      await settle();
      const first = fetchDeploy.mock.calls.length;
      await vi.advanceTimersByTimeAsync(5000);
      expect(fetchDeploy.mock.calls.length).toBeGreaterThan(first);
      const second = fetchDeploy.mock.calls.length;
      await vi.advanceTimersByTimeAsync(5000);
      expect(fetchDeploy.mock.calls.length).toBeGreaterThan(second);
      // 第三轮已是终态：再等也不再取
      const third = fetchDeploy.mock.calls.length;
      await vi.advanceTimersByTimeAsync(12000);
      expect(fetchDeploy.mock.calls.length).toBe(third);
    } finally {
      vi.useRealTimers();
    }
  });

  it('没有活跃运行：不轮询', async () => {
    vi.useFakeTimers();
    try {
      const fetchDeploy = vi.fn<DeployDeps['fetchDeploy']>(async (_r, appId) => ({ status: 'ok', deploy: dep(appId), others: { count: 0, nodes: 0 } }));
      setup({ fetchDeploy, fetchRunHistory: async () => [runWith(4)] });
      await settle();
      const n = fetchDeploy.mock.calls.length;
      await vi.advanceTimersByTimeAsync(12000);
      expect(fetchDeploy.mock.calls.length).toBe(n);
    } finally {
      vi.useRealTimers();
    }
  });

  it('scope 停止后轮询取消', async () => {
    vi.useFakeTimers();
    try {
      const fetchDeploy = vi.fn<DeployDeps['fetchDeploy']>(async (_r, appId) => ({ status: 'ok', deploy: dep(appId), others: { count: 0, nodes: 0 } }));
      const s = setup({ fetchDeploy, fetchRunHistory: async () => [runWith(3)] });
      await settle();
      s.scope.stop();
      const n = fetchDeploy.mock.calls.length;
      await vi.advanceTimersByTimeAsync(12000);
      expect(fetchDeploy.mock.calls.length).toBe(n);
    } finally {
      vi.useRealTimers();
    }
  });
});

describe('useDeployData · F06/F07/F08 新操作', () => {
  it('saveDeployEdit：busy + 成功反馈，返回 true', async () => {
    const saveDeploy = vi.fn<DeployDeps['saveDeploy']>(async () => ({ ok: true, message: '部署集已保存' }));
    const s = setup({ saveDeploy });
    await settle();
    const ok = await s.d.saveDeployEdit({ name: 'x', category: '', enable: true, packageName: '', autoPublish: false, multiVersion: false, repository: '', repoUserName: '', repoPassword: '', branch: '', projectPath: '', buildArgs: '', packageFilters: '', remark: '', logDirectory: '', logFilePattern: '' });
    expect(ok).toBe(true);
    expect(s.d.busyKey.value).toBe('');
    expect(saveDeploy.mock.calls[0][1]).toBe(10); // dep() 里 id = appId*10
    expect(s.toasts.at(-1)?.ok).toBe(true);
  });

  it('syncApp：调 syncAppDeploy 并反馈后端文案', async () => {
    const syncAppDeploy = vi.fn<DeployDeps['syncAppDeploy']>(async () => ({ ok: true, message: '成功同步[3]个应用！' }));
    const s = setup({ syncAppDeploy });
    await settle();
    await s.d.syncApp();
    expect(syncAppDeploy).toHaveBeenCalledTimes(1);
    expect(s.toasts.at(-1)?.text).toContain('成功同步[3]个应用');
  });

  it('batchOperate：keys 走勾选 ids，反馈带节点名', async () => {
    const batchOperateNodes = vi.fn<DeployDeps['batchOperateNodes']>(async (_r, _act, ids) => ({ ok: true, message: `批量执行 ${ids.length}` }));
    const s = setup({ batchOperateNodes });
    await settle();
    await s.d.batchOperate('restart', [3, 5], ['n1', 'n2']);
    expect(batchOperateNodes.mock.calls[0][2]).toEqual([3, 5]);
    expect(s.toasts.at(-1)?.text).toContain('n1、n2');
  });

  it('batchOperate：空勾选不发请求', async () => {
    const batchOperateNodes = vi.fn<DeployDeps['batchOperateNodes']>(async () => ({ ok: true, message: '' }));
    const s = setup({ batchOperateNodes });
    await settle();
    await s.d.batchOperate('stop', [], []);
    expect(batchOperateNodes).not.toHaveBeenCalled();
  });

  it('uploadNewVersion：busy 上传中，成功反馈带版本号', async () => {
    const uploadVersion = vi.fn<DeployDeps['uploadVersion']>(async () => ({ ok: true, message: '上传成功', version: '20261103-120001' }));
    const s = setup({ uploadVersion });
    await settle();
    const file = new File([new Uint8Array([1])], 'star.zip');
    const ok = await s.d.uploadNewVersion(file);
    expect(ok).toBe(true);
    expect(uploadVersion.mock.calls[0][2]).toBe(file);
    expect(s.toasts.at(-1)?.text).toContain('20261103-120001');
  });

  it('saveNode：id 空走新增，带 id 走更新；removeNode 走删除', async () => {
    const createDeployNode = vi.fn<DeployDeps['createDeployNode']>(async () => ({ ok: true, message: '部署节点已添加' }));
    const updateDeployNode = vi.fn<DeployDeps['updateDeployNode']>(async () => ({ ok: true, message: '部署节点已保存' }));
    const deleteDeployNode = vi.fn<DeployDeps['deleteDeployNode']>(async () => ({ ok: true, message: '部署节点已删除' }));
    const s = setup({ createDeployNode, updateDeployNode, deleteDeployNode });
    await settle();
    const m = { nodeId: 2, enable: true, port: 0, fileName: '', arguments: '', workingDirectory: '', userName: '', environments: '', maxMemory: 0, delay: 0, healthCheck: '', remark: '', logDirectory: '', logFilePattern: '' };
    expect(await s.d.saveNode(m)).toBe(true);
    expect(createDeployNode).toHaveBeenCalledTimes(1);
    expect(await s.d.saveNode(m, 12)).toBe(true);
    expect(updateDeployNode.mock.calls[0][1]).toBe(12);
    await s.d.removeNode(12, 'n1');
    expect(deleteDeployNode.mock.calls[0][1]).toBe(12);
  });

  it('saveBuildNode / removeBuildNode 走编译节点 CRUD', async () => {
    const createBuildNode = vi.fn<DeployDeps['createBuildNode']>(async () => ({ ok: true, message: '编译节点已添加' }));
    const deleteBuildNode = vi.fn<DeployDeps['deleteBuildNode']>(async () => ({ ok: true, message: '编译节点已删除' }));
    const s = setup({ createBuildNode, deleteBuildNode });
    await settle();
    const m = { nodeId: 2, enable: true, sourcePath: 'D:/src', pullCode: true, buildProject: true, packageOutput: true, uploadPackage: true, outputPath: '', remark: '' };
    expect(await s.d.saveBuildNode(m)).toBe(true);
    expect(createBuildNode).toHaveBeenCalledTimes(1);
    await s.d.removeBuildNode(4, 'bn');
    expect(deleteBuildNode.mock.calls[0][1]).toBe(4);
  });
});

// ================= CARD-DEP-01/02（2026-11-07）：流水线配置 + 运行筛选/详情/重跑 =================

describe('useDeployData · 流水线配置（CARD-DEP-01）', () => {
  it('load 并行取流水线配置：成功写入 pipelines，失败置 pipelinesFailed 且清空', async () => {
    const s = setup({ fetchPipelineList: async () => [{ id: 7, name: 'build-main', deployId: 10, branch: 'main', buildNodeId: 3, projectId: 88, deployNodeIds: '1,2', autoDeploy: true, enable: true, updateTime: '' }] });
    await settle();
    expect(s.d.pipelines.value.map((p) => p.id)).toEqual([7]);
    expect(s.d.pipelinesFailed.value).toBe(false);
    s.scope.stop();

    const f = setup({ fetchPipelineList: async () => null });
    await settle();
    expect(f.d.pipelinesFailed.value).toBe(true);
    expect(f.d.pipelines.value).toEqual([]);
    f.scope.stop();
  });

  it('savePipeline：无 id 走 create（带 deployId），有 id 走 update；busy 与反馈', async () => {
    const createPipeline = vi.fn<DeployDeps['createPipeline']>(async () => ({ ok: true, message: '流水线已创建' }));
    const updatePipeline = vi.fn<DeployDeps['updatePipeline']>(async () => ({ ok: true, message: '流水线已保存' }));
    const s = setup({ createPipeline, updatePipeline });
    await settle();
    const m = { name: 'a', branch: 'main', buildNodeId: 3, deployNodeIds: [1, 2], autoDeploy: true, enable: true };
    expect(await s.d.savePipeline(m)).toBe(true);
    expect(createPipeline.mock.calls[0]).toEqual([expect.anything(), 10, m]);
    expect(await s.d.savePipeline(m, 7)).toBe(true);
    expect(updatePipeline.mock.calls[0][1]).toBe(7);
    expect(s.toasts.at(-1)?.text).toBe('流水线已保存');
    expect(s.d.busyKey.value).toBe('');
    s.scope.stop();
  });

  it('removePipeline：busyKey=del:{id}，成功反馈「流水线已删除」', async () => {
    const deletePipeline = vi.fn<DeployDeps['deletePipeline']>(async () => ({ ok: true, message: '流水线已删除' }));
    const s = setup({ deletePipeline });
    await settle();
    await s.d.removePipeline({ id: 7, name: 'a', deployId: 10, branch: 'main', buildNodeId: 0, projectId: 0, deployNodeIds: '', autoDeploy: false, enable: true, updateTime: '' });
    expect(deletePipeline.mock.calls[0][1]).toBe(7);
    expect(s.toasts.at(-1)).toEqual({ ok: true, text: '流水线已删除' });
    s.scope.stop();
  });

  it('copyWebhookFor：busyKey=copy:{id}，返回完整 URL；失败时反馈不带成功 toast', async () => {
    const s = setup();
    await settle();
    const row = { id: 7, name: 'a', deployId: 10, branch: 'main', buildNodeId: 0, projectId: 0, deployNodeIds: '', autoDeploy: false, enable: true, updateTime: '' };
    const r = await s.d.copyWebhookFor(row);
    expect(r.ok).toBe(true);
    expect(r.url).toContain('?token=');
    expect(s.d.busyKey.value).toBe('');
    s.scope.stop();
  });

  it('triggerRun：busyKey=trigger:{id}，message 透传后端「流水线已触发，运行编号[N]」', async () => {
    const triggerPipeline = vi.fn<DeployDeps['triggerPipeline']>(async () => ({ ok: true, message: '流水线已触发，运行编号[42]' }));
    const s = setup({ triggerPipeline });
    await settle();
    await s.d.triggerRun({ id: 7, name: 'a', deployId: 10, branch: 'main', buildNodeId: 0, projectId: 0, deployNodeIds: '', autoDeploy: false, enable: true, updateTime: '' });
    expect(triggerPipeline.mock.calls[0][1]).toBe(7);
    expect(s.toasts.at(-1)).toEqual({ ok: true, text: '流水线已触发，运行编号[42]' });
    s.scope.stop();
  });

  // task-88（2026-11-12）：触发成功反馈带「查看运行」直达，并自动选中该次运行（行高亮 + 状态徽标）
  it('triggerRun：成功解析「运行编号[N]」→ 反馈带 openRun 动作 + 选中该运行；失败与无编号不带动作', async () => {
    const triggerPipeline = vi.fn<DeployDeps['triggerPipeline']>(async () => ({ ok: true, message: '流水线已触发，运行编号[42]' }));
    const fetchRunSteps = vi.fn<DeployDeps['fetchRunSteps']>(async () => [] as StepRow[]);
    const s = setup({ triggerPipeline, fetchRunSteps });
    await settle();
    await s.d.triggerRun({ id: 7, name: 'a', deployId: 10, branch: 'main', buildNodeId: 0, projectId: 0, deployNodeIds: '', autoDeploy: false, enable: true, updateTime: '' });
    const fb = s.d.feedback.value[0];
    expect(fb?.ok).toBe(true);
    expect(fb?.action).toEqual({ kind: 'openRun', label: '查看运行', runId: 42 });
    await settle();
    expect(s.d.selectedRunId.value).toBe(42);
    expect(fetchRunSteps).toHaveBeenCalledWith(expect.anything(), 42);
    s.scope.stop();

    // 失败：反馈不带动作，也不选中
    const bad = setup({ triggerPipeline: async () => ({ ok: false, message: '流水线未启用' }) });
    await settle();
    await bad.d.triggerRun({ id: 7, name: 'a', deployId: 10, branch: 'main', buildNodeId: 0, projectId: 0, deployNodeIds: '', autoDeploy: false, enable: true, updateTime: '' });
    expect(bad.d.feedback.value[0]?.ok).toBe(false);
    expect(bad.d.feedback.value[0]?.action).toBeUndefined();
    expect(bad.d.selectedRunId.value).toBe(0);
    bad.scope.stop();

    // 成功但 message 无编号：反馈不带动作（降级为普通提示）
    const noNo = setup({ triggerPipeline: async () => ({ ok: true, message: '触发成功' }) });
    await settle();
    await noNo.d.triggerRun({ id: 7, name: 'a', deployId: 10, branch: 'main', buildNodeId: 0, projectId: 0, deployNodeIds: '', autoDeploy: false, enable: true, updateTime: '' });
    expect(noNo.d.feedback.value[0]?.action).toBeUndefined();
    noNo.scope.stop();
  });

  // task-88：重跑与手动触发同格式（「流水线已重新触发，运行编号[N]」），同样带直达动作 + 选中
  it('rerunRun：成功反馈带 openRun 动作（运行编号[43]）并选中该运行', async () => {
    const reprocessRun = vi.fn<DeployDeps['reprocessRun']>(async () => ({ ok: true, message: '流水线已重新触发，运行编号[43]' }));
    const s = setup({ reprocessRun });
    await settle();
    await s.d.rerunRun(11);
    const fb = s.d.feedback.value[0];
    expect(fb?.text).toBe('流水线已重新触发，运行编号[43]');
    expect(fb?.action).toEqual({ kind: 'openRun', label: '查看运行', runId: 43 });
    await settle();
    expect(s.d.selectedRunId.value).toBe(43);
    s.scope.stop();
  });

  // 走查缺陷1 锁行为（lead 修复单）：写成功后不点刷新，配置卡数据也要自动上屏
  it('创建/删除流水线成功后自动刷新：无需手动点刷新，pipelines 上屏新数据', async () => {
    vi.useFakeTimers();
    try {
      let list = [{ id: 7, name: 'demo-main', deployId: 10, branch: 'main', buildNodeId: 3, projectId: 88, deployNodeIds: '1,2', autoDeploy: true, enable: true, updateTime: '' }];
      const fetchPipelineList = vi.fn<DeployDeps['fetchPipelineList']>(async () => list);
      const createPipeline = vi.fn<DeployDeps['createPipeline']>(async (_r, _did, m) => {
        list = [...list, { id: 8, name: m.name, deployId: 10, branch: m.branch, buildNodeId: m.buildNodeId, projectId: 88, deployNodeIds: '1,2', autoDeploy: m.autoDeploy, enable: m.enable, updateTime: '' }];
        return { ok: true, message: '流水线已创建' };
      });
      const deletePipeline = vi.fn<DeployDeps['deletePipeline']>(async () => {
        list = list.filter((p) => p.id !== 8);
        return { ok: true, message: '流水线已删除' };
      });
      const s = setup({ fetchPipelineList, createPipeline, deletePipeline });
      await settle();
      expect(s.d.pipelines.value.map((p) => p.id)).toEqual([7]);

      // 创建 walk-fix：写返回后 400ms 首刷即上屏
      await s.d.savePipeline({ name: 'walk-fix', branch: 'main', buildNodeId: 0, deployNodeIds: [1], autoDeploy: false, enable: true });
      expect(fetchPipelineList).toHaveBeenCalledTimes(1);
      await vi.advanceTimersByTimeAsync(400);
      await settle();
      expect(fetchPipelineList).toHaveBeenCalledTimes(2);
      expect(s.d.pipelines.value.map((p) => p.id)).toEqual([7, 8]);
      expect(s.toasts.at(-1)?.text).toBe('流水线已创建');

      // 删除 walk-fix：兜底刷后回到单条（即使首刷读旧，4s 兜底也会追上）
      await s.d.removePipeline(list.find((p) => p.id === 8)!);
      await vi.advanceTimersByTimeAsync(400);
      await settle();
      expect(s.d.pipelines.value.map((p) => p.id)).toEqual([7]);
      // 兜底定时器已重排，不再残留旧值
      await vi.advanceTimersByTimeAsync(3600);
      await settle();
      expect(s.d.pipelines.value.map((p) => p.id)).toEqual([7]);
      s.scope.stop();
    } finally {
      vi.useRealTimers();
    }
  });
});

describe('useDeployData · 运行筛选/详情/重跑（CARD-DEP-02）', () => {
  it('筛选生效时改走 fetchRunHistoryFiltered；重置恢复 fetchRunHistory', async () => {
    const fetchRunHistory = vi.fn<DeployDeps['fetchRunHistory']>(async () => RUNS);
    const fetchRunHistoryFiltered = vi.fn<DeployDeps['fetchRunHistoryFiltered']>(async () => [{ id: 31 }]);
    const s = setup({ fetchRunHistory, fetchRunHistoryFiltered });
    await settle();
    expect(fetchRunHistory).toHaveBeenCalledTimes(1);
    expect(fetchRunHistoryFiltered).not.toHaveBeenCalled();

    s.d.setRunFilter({ status: 5 });
    await settle();
    expect(fetchRunHistoryFiltered).toHaveBeenCalledTimes(1);
    expect(s.d.runs.value).toEqual([{ id: 31 }]);
    // 参数带上 status；dtStart/dtEnd 不带
    expect(s.d.runFilter.status).toBe(5);

    s.d.resetRunFilter();
    await settle();
    expect(s.d.runFilter.status).toBe(-1);
    expect(fetchRunHistory).toHaveBeenCalledTimes(2);
    s.scope.stop();
  });

  it('openRunDetail：就绪态回填流水线名与产物版本；missing/failed 状态机', async () => {
    const full = { id: 42, pipelineId: 7, status: 'Success', appVersionId: 2 } as unknown as RunDetail;
    const s = setup({ fetchRunDetail: async () => full });
    await settle();
    await s.d.openRunDetail(42);
    expect(s.d.runDetailOpen.value).toBe(true);
    expect(s.d.runDetailState.value).toBe('ready');
    expect(s.d.runDetail.value?.pipelineName).toBe('#7'); // pipelines 为空 → #Id 兜底
    expect(s.d.runDetail.value?.versionLabel).toBe('v2');
    s.scope.stop();

    const m = setup({ fetchRunDetail: async () => 'missing' as const });
    await settle();
    await m.d.openRunDetail(42);
    expect(m.d.runDetailState.value).toBe('missing');
    m.scope.stop();

    const f = setup({ fetchRunDetail: async () => null });
    await settle();
    await f.d.openRunDetail(42);
    expect(f.d.runDetailState.value).toBe('failed');
    f.d.retryRunDetail();
    await settle();
    expect(f.d.runDetailState.value).toBe('failed');
    f.scope.stop();
  });

  it('closeRunDetail 清空状态；rerunRun busyKey=rerun:{id} 且透传 message', async () => {
    const reprocessRun = vi.fn<DeployDeps['reprocessRun']>(async () => ({ ok: true, message: '流水线已重新触发，运行编号[43]' }));
    const s = setup({ reprocessRun, fetchRunDetail: async () => ({ id: 42, status: 'Success' } as unknown as RunDetail) });
    await settle();
    await s.d.openRunDetail(42);
    expect(s.d.runDetailOpen.value).toBe(true);
    s.d.closeRunDetail();
    expect(s.d.runDetailOpen.value).toBe(false);
    expect(s.d.runDetail.value).toBeNull();
    expect(s.d.runDetailState.value).toBe('idle');

    await s.d.rerunRun(42);
    expect(reprocessRun.mock.calls[0][1]).toBe(42);
    expect(s.d.busyKey.value).toBe('');
    expect(s.toasts.at(-1)).toEqual({ ok: true, text: '流水线已重新触发，运行编号[43]' });
    s.scope.stop();
  });
});