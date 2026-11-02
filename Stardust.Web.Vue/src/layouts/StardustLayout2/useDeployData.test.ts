// 发布数据组合函数单测（Vitest）：取数函数、request、时钟全部注入，不 mock 模块。
import { describe, it, expect, vi } from 'vitest';
import { effectScope, nextTick, ref } from 'vue';
import { useDeployData } from './useDeployData.ts';
import type { DeployDeps } from './useDeployData.ts';
import type { DeployInfo, HistoryRow, ShipNode, StepRow, VersionOption } from './ship.logic.ts';
import type { Req, RunHistoryRow } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

const request: Req = async () => ({});

function dep(appId: number, version = `v${appId}`): DeployInfo {
  return { id: appId * 10, appId, version, enable: true, autoPublish: false, multiVersion: false };
}
function ver(id: number, version: string): VersionOption {
  return { id, version, disabled: false, reason: '', current: false, label: version };
}
function node(id: number, name: string): ShipNode {
  return { id, nodeId: id, name, ip: '10.0.0.1', enable: true, version: 'v1', mode: 10, modeLabel: 'Standard' };
}
const HIST: HistoryRow = {
  id: 1, deployId: 10, nodeId: 1, node: 'n1', action: 'install', actionLabel: '安装',
  success: true, remark: '', traceId: '', at: '', time: '', ip: '',
};
const RUNS: RunHistoryRow[] = [{ id: 11 }, { id: 12 }];

function baseDeps(): DeployDeps {
  return {
    fetchDeploy: async (_r, appId) => ({ status: 'ok', deploy: dep(appId) }),
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
      fetchDeploy: (_r, appId) => (appId === 1 ? slow.promise : Promise.resolve({ status: 'ok', deploy: dep(appId) })),
    });
    s.appId.value = 2;
    await settle();
    expect(s.d.deploy.value?.version).toBe('v2');
    slow.resolve({ status: 'ok', deploy: dep(1, 'old') });
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

  it('操作后立即刷新一次，4 秒后再刷新一次', async () => {
    vi.useFakeTimers();
    try {
      const fetchDeploy = vi.fn<DeployDeps['fetchDeploy']>(async (_r, appId) => ({ status: 'ok', deploy: dep(appId) }));
      const s = setup({ fetchDeploy });
      await settle();
      expect(fetchDeploy).toHaveBeenCalledTimes(1);
      await s.d.operate(node(1, 'n1'), 'install');
      await settle();
      expect(fetchDeploy).toHaveBeenCalledTimes(2);
      await vi.advanceTimersByTimeAsync(4000);
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
    slow.resolve({ status: 'ok', deploy: dep(1) });
    await settle();
    expect(s.d.deploy.value).toBeNull();
  });

  it('scope 停止后，等待中的 4 秒刷新被取消', async () => {
    vi.useFakeTimers();
    try {
      const fetchDeploy = vi.fn<DeployDeps['fetchDeploy']>(async (_r, appId) => ({ status: 'ok', deploy: dep(appId) }));
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
      const fetchDeploy = vi.fn<DeployDeps['fetchDeploy']>(async (_r, appId) => ({ status: 'ok', deploy: dep(appId) }));
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
      const fetchDeploy = vi.fn<DeployDeps['fetchDeploy']>(async (_r, appId) => ({ status: 'ok', deploy: dep(appId) }));
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
      const fetchDeploy = vi.fn<DeployDeps['fetchDeploy']>(async (_r, appId) => ({ status: 'ok', deploy: dep(appId) }));
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
    const ok = await s.d.saveDeployEdit({ name: 'x', category: '', enable: true, packageName: '', autoPublish: false, multiVersion: false, repository: '', repoUserName: '', repoPassword: '', branch: '', projectPath: '', buildArgs: '', packageFilters: '', remark: '' });
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
    const m = { nodeId: 2, enable: true, port: 0, fileName: '', arguments: '', workingDirectory: '', userName: '', environments: '', maxMemory: 0, delay: 0, healthCheck: '', remark: '' };
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