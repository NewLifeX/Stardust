// 发布页纯逻辑单测（Vitest）：request 用桩函数注入，不发真实请求。
import { describe, it, expect } from 'vitest';
import type { Req, ReqOptions, RunHistoryRow } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import {
  buildNodeResults,
  buildRunRows,
  buildVersionCells,
  fetchDeploy,
  fetchRunSteps,
  fetchShipNodes,
  fetchVersions,
  fmtTime,
  isRunTerminal,
  modeLabel,
  nodeCell,
  normalizeStepStatus,
  releaseBlock,
  runTone,
  toShipNodes,
  toStepRows,
  toVersionOptions,
  useVersion,
  type DeployInfo,
  type ReleaseGate,
  type ShipNode,
  type StepRow,
  actionInfo,
  actionLabel,
  fetchDeployHistory,
  filterHistoryByNode,
  NODE_ACTIONS,
  operateNode,
  operateNotice,
  toHistoryRows,
  useVersionNotice,
  type NodeAction,
  type NodeActionInfo,
  REFRESH_MIN_SPIN_MS,
  refreshFeedback,
  refreshRemainMs,
  type RefreshOutcome,
} from './ship.logic';

// 桩：返回什么就是什么；函数内抛错模拟请求失败
const mk = (fn: (o: ReqOptions) => unknown): Req => ((o: ReqOptions) => Promise.resolve(fn(o))) as Req;

const deploy = (over: Partial<DeployInfo> = {}): DeployInfo => ({
  id: 7,
  appId: 3,
  version: '1.0',
  enable: true,
  autoPublish: true,
  multiVersion: false,
  ...over,
});

const step = (over: Partial<StepRow> = {}): StepRow => ({
  id: 1,
  runId: 9,
  stepType: 'Deploy',
  stepIndex: 1,
  nodeId: 11,
  commandId: 0,
  status: 'Success',
  message: '',
  ...over,
});

const node = (over: Partial<ShipNode> = {}): ShipNode => ({
  id: 1,
  nodeId: 11,
  name: 'n1',
  ip: '10.0.0.1',
  enable: true,
  version: '1.0',
  mode: 10,
  modeLabel: 'Standard',
  ...over,
});

describe('fetchDeploy · 部署集', () => {
  it('按 appId 命中，字段大小写都认', async () => {
    const r = await fetchDeploy(mk(() => ({ data: [{ Id: 7, AppId: 3, Version: '1.0', Enable: true, AutoPublish: true }] })), 3);
    expect(r).toEqual({ status: 'ok', deploy: deploy() });
    const r2 = await fetchDeploy(mk(() => ({ data: [{ id: 7, appId: 3, version: '1.0', enable: 1, autoPublish: 'true' }] })), 3);
    expect(r2).toEqual({ status: 'ok', deploy: deploy() });
  });

  it('列表里没有该 appId 时是 none，不取第一行', async () => {
    const r = await fetchDeploy(mk(() => ({ data: [{ Id: 8, AppId: 4 }] })), 3);
    expect(r).toEqual({ status: 'none' });
  });

  it('appId 无效不发请求', async () => {
    let called = 0;
    const r = await fetchDeploy(mk(() => { called++; return {}; }), 0);
    expect(r).toEqual({ status: 'none' });
    expect(called).toBe(0);
  });

  it('请求失败是 failed', async () => {
    const r = await fetchDeploy(mk(() => { throw new Error('x'); }), 3);
    expect(r).toEqual({ status: 'failed' });
  });
});

describe('toVersionOptions · 版本下拉', () => {
  it('按 Id 倒序，当前版本带「当前」', () => {
    const o = toVersionOptions([{ Id: 1, Version: '1.0', Enable: true, Url: '/a' }, { Id: 2, Version: '1.1', Enable: true, Url: '/b' }], '1.0');
    expect(o.map((x) => x.id)).toEqual([2, 1]);
    expect(o[1].current).toBe(true);
    expect(o[1].label).toBe('1.0 · 当前');
    expect(o[0].disabled).toBe(false);
  });

  it('未启用、没版本号、没文件地址都置灰并写明原因', () => {
    const o = toVersionOptions(
      [
        { Id: 1, Version: '1.0', Enable: false, Url: '/a' },
        { Id: 2, Version: '', Enable: true, Url: '/a' },
        { Id: 3, Version: '1.2', Enable: true, Url: ' ' },
      ],
      '',
    );
    const by = new Map(o.map((x) => [x.id, x]));
    expect(by.get(1)).toMatchObject({ disabled: true, reason: '版本未启用' });
    expect(by.get(2)).toMatchObject({ disabled: true, reason: '没有版本号' });
    expect(by.get(3)).toMatchObject({ disabled: true, reason: '没有文件地址' });
  });

  it('接口没返回 Url 字段时不置灰', () => {
    const o = toVersionOptions([{ Id: 1, Version: '1.0', Enable: true }], '');
    expect(o[0].disabled).toBe(false);
  });
});

describe('fetchVersions / fetchShipNodes / fetchRunSteps · 取数失败返回 null', () => {
  it('deployId 无效返回空数组', async () => {
    expect(await fetchVersions(mk(() => ({})), 0, '')).toEqual([]);
    expect(await fetchShipNodes(mk(() => ({})), 0)).toEqual([]);
    expect(await fetchRunSteps(mk(() => ({})), 0)).toEqual([]);
  });

  it('请求失败返回 null，页面据此显示「加载失败」', async () => {
    const boom = mk(() => { throw new Error('x'); });
    expect(await fetchVersions(boom, 7, '')).toBeNull();
    expect(await fetchShipNodes(boom, 7)).toBeNull();
    expect(await fetchRunSteps(boom, 9)).toBeNull();
  });

  it('请求参数：runId 必带', async () => {
    let seen: ReqOptions | undefined;
    await fetchRunSteps(mk((o) => { seen = o; return { data: [] }; }), 9);
    expect(seen?.url).toBe('/Deployment/AppPipelineStep');
    expect(seen?.params).toMatchObject({ runId: 9 });
  });
});

describe('节点与运行模式', () => {
  it('modeLabel：10/11/12/13 对应 Standard/Shadow/Hosted/Task', () => {
    expect([10, 11, 12, 13].map(modeLabel)).toEqual(['Standard', 'Shadow', 'Hosted', 'Task']);
    expect(modeLabel(0)).toBe('—');
    expect(modeLabel(99)).toBe('模式 99');
  });

  it('toShipNodes：缺节点名时用「节点 id」兜底', () => {
    const n = toShipNodes([{ Id: 5, NodeId: 11, IP: '10.0.0.1', Enable: true, Version: '1.0', Mode: 11 }]);
    expect(n[0]).toMatchObject({ id: 5, nodeId: 11, name: '节点 11', ip: '10.0.0.1', enable: true, version: '1.0', modeLabel: 'Shadow' });
  });
});

describe('步骤状态', () => {
  it('normalizeStepStatus 不区分大小写，未知为空串', () => {
    expect(normalizeStepStatus('running')).toBe('Running');
    expect(normalizeStepStatus(' Skipped ')).toBe('Skipped');
    expect(normalizeStepStatus('xx')).toBe('');
  });

  it('toStepRows 字段映射', () => {
    const r = toStepRows([{ Id: 1, RunId: 9, StepType: 'Deploy', StepIndex: 2, NodeId: 11, CommandId: 5, Status: 'Failed', Message: 'bad' }]);
    expect(r[0]).toEqual({ id: 1, runId: 9, stepType: 'Deploy', stepIndex: 2, nodeId: 11, commandId: 5, status: 'Failed', message: 'bad' });
  });

  it('isRunTerminal：成功、失败、已取消为终态，部署中不是', () => {
    expect([4, 5, 6].map(isRunTerminal)).toEqual([true, true, true]);
    expect([0, 1, 2, 3].map(isRunTerminal)).toEqual([false, false, false, false]);
  });
});

describe('nodeCell · 单个节点的格子状态', () => {
  it('没有步骤是 none', () => {
    expect(nodeCell(undefined, false)).toBe('none');
  });

  it('Running：运行中显示 ing；run 已终态说明没回包，显示 noreply', () => {
    expect(nodeCell(step({ status: 'Running' }), false)).toBe('ing');
    expect(nodeCell(step({ status: 'Running' }), true)).toBe('noreply');
  });

  it('Pending：运行中是 wait；run 已终态是 unrun', () => {
    expect(nodeCell(step({ status: 'Pending' }), false)).toBe('wait');
    expect(nodeCell(step({ status: 'Pending' }), true)).toBe('unrun');
  });

  it('终态步骤原样映射', () => {
    expect(nodeCell(step({ status: 'Success' }), true)).toBe('ok');
    expect(nodeCell(step({ status: 'Failed' }), true)).toBe('fail');
    expect(nodeCell(step({ status: 'Skipped' }), true)).toBe('skip');
    expect(nodeCell(step({ status: 'Cancelled' }), true)).toBe('cancel');
  });
});

describe('buildNodeResults · 按节点的部署结果', () => {
  it('同一节点取 stepIndex 最大的 Deploy 步骤，忽略 Build/Upload', () => {
    const r = buildNodeResults(
      [node()],
      [step({ id: 1, stepIndex: 1, status: 'Failed' }), step({ id: 2, stepIndex: 2, status: 'Success' }), step({ id: 3, stepType: 'Build', stepIndex: 9, status: 'Failed' })],
      4,
    );
    expect(r.items[0].cell).toBe('ok');
    expect(r.deploySteps).toBe(2);
  });

  it('全部 Deploy 步骤都是 Skipped：allSkipped 为 true（后端此时可能把 run 标成功）', () => {
    const r = buildNodeResults([node()], [step({ status: 'Skipped' })], 4);
    expect(r.allSkipped).toBe(true);
    expect(r.items[0].cell).toBe('skip');
  });

  it('没有任何 Deploy 步骤：allSkipped 为 false，格子是 none', () => {
    const r = buildNodeResults([node()], [], 4);
    expect(r.allSkipped).toBe(false);
    expect(r.items[0].cell).toBe('none');
  });

  it('run 已失败而步骤停在 Running：显示未回包', () => {
    const r = buildNodeResults([node(), node({ nodeId: 12, name: 'n2' })], [step({ nodeId: 11, status: 'Failed' }), step({ id: 2, nodeId: 12, status: 'Running' })], 5);
    expect(r.items.map((x) => x.cell)).toEqual(['fail', 'noreply']);
  });

  it('步骤里有、节点列表里没有的节点也要列出', () => {
    const r = buildNodeResults([node()], [step({ nodeId: 99, status: 'Success' })], 4);
    expect(r.items.map((x) => x.name)).toEqual(['n1', '节点 99']);
  });
});

describe('buildVersionCells · 没有运行记录时只比版本', () => {
  it('停用是 off，版本一致是 cur，其余是 old', () => {
    const cells = buildVersionCells(
      [node({ nodeId: 1 }), node({ nodeId: 2, version: '0.9' }), node({ nodeId: 3, enable: false }), node({ nodeId: 4, version: '' })],
      '1.0',
    );
    expect(cells.map((c) => c.cell)).toEqual(['cur', 'old', 'off', 'old']);
  });
});

describe('releaseBlock · 发起部署的门槛与原因', () => {
  const version = toVersionOptions([{ Id: 1, Version: '1.1', Enable: true, Url: '/a' }], '1.0')[0];
  const ok: ReleaseGate = { deploy: deploy(), version, enabledNodes: 2, busy: false };

  it('条件齐全时返回空串', () => {
    expect(releaseBlock(ok)).toBe('');
  });

  it('逐项拦截，原因各不相同', () => {
    expect(releaseBlock({ ...ok, deploy: null })).toBe('该应用还没有部署集');
    expect(releaseBlock({ ...ok, deploy: deploy({ enable: false }) })).toBe('部署集未启用');
    // 未开自动发布不再拦截：后端只改版本号，后果由 useVersionNotice 在点击前说明
    expect(releaseBlock({ ...ok, deploy: deploy({ autoPublish: false }) })).toBe('');
    expect(releaseBlock({ ...ok, enabledNodes: 0 })).toBe('没有启用的目标节点');
    expect(releaseBlock({ ...ok, version: undefined })).toBe('请选择版本');
    expect(releaseBlock({ ...ok, version: { ...version, disabled: true, reason: '版本未启用' } })).toBe('该版本不可用：版本未启用');
    expect(releaseBlock({ ...ok, busy: true })).toBe('正在发起，请稍候');
  });
});

describe('useVersion · 发起部署', () => {
  it('code 为 0 视为成功，带回后端文案', async () => {
    const r = await useVersion(mk(() => ({ code: 0, message: '已发布' })), 5);
    expect(r).toEqual({ ok: true, message: '已发布' });
  });

  it('成功但没有文案时用默认文案', async () => {
    const r = await useVersion(mk(() => ({ code: 0 })), 5);
    expect(r).toEqual({ ok: true, message: '已下发部署命令' });
  });

  it('判不出成功就按失败提示，并让用户去节点页核对', async () => {
    const r = await useVersion(mk(() => ({})), 5);
    expect(r.ok).toBe(false);
    expect(r.message).toContain('节点页');
  });

  it('请求抛错时带回错误信息', async () => {
    const r = await useVersion(mk(() => { throw new Error('超时'); }), 5);
    expect(r).toEqual({ ok: false, message: '超时' });
  });

  it('请求参数：版本 Id', async () => {
    let seen: ReqOptions | undefined;
    await useVersion(mk((o) => { seen = o; return { code: 0 }; }), 5);
    expect(seen?.url).toBe('/Deployment/AppDeployVersion/UseVersion');
    expect(seen?.params).toEqual({ id: 5 });
  });
});

describe('运行列表', () => {
  it('runTone：状态到语气', () => {
    expect(runTone(4)).toEqual({ tone: 'ok', label: '成功' });
    expect(runTone('Failed')).toEqual({ tone: 'err', label: '失败' });
    expect(runTone(3)).toEqual({ tone: 'brand', label: '部署中' });
    expect(runTone(0)).toEqual({ tone: 'warn', label: '排队中' });
    expect(runTone('???')).toEqual({ tone: 'default', label: '未知' });
  });

  it('fmtTime：去掉 T 与秒，空值是破折号', () => {
    expect(fmtTime('2026-10-28T10:20:30')).toBe('2026-10-28 10:20');
    expect(fmtTime('')).toBe('—');
    expect(fmtTime(undefined)).toBe('—');
  });

  it('buildRunRows：版本号按 appVersionId 映射，查不到是破折号', () => {
    const versions = toVersionOptions([{ Id: 2, Version: '1.1', Enable: true }], '');
    const runs: RunHistoryRow[] = [
      { id: 10, appVersionId: 2, pipelineName: 'p1', status: 'Success', triggerSource: 'Manual', commitAuthor: 'a', commitMessage: 'm', finishedTime: '2026-10-28T10:20:30' },
      { id: 11, appVersionId: 99, status: 'Deploying' },
    ];
    const rows = buildRunRows(runs, versions);
    expect(rows[0]).toMatchObject({ id: 10, version: '1.1', pipeline: 'p1', trigger: '手动', author: 'a', tone: 'ok', time: '2026-10-28 10:20', active: false });
    expect(rows[1]).toMatchObject({ id: 11, version: '—', pipeline: '—', author: '—', tone: 'brand', active: true });
  });
});

describe('useVersionNotice · 应用此版本前的后果说明', () => {
  it('没有部署集时不提示', () => {
    expect(useVersionNotice(null, 2)).toBeNull();
  });

  it('部署集未启用、未开自动发布、没有启用节点：只切换版本号，语气为 info', () => {
    expect(useVersionNotice(deploy({ enable: false }), 2)).toMatchObject({ tone: 'info' });
    const off = useVersionNotice(deploy({ autoPublish: false }), 2);
    expect(off?.tone).toBe('info');
    expect(off?.text).toContain('不会下发');
    expect(useVersionNotice(deploy(), 0)?.tone).toBe('info');
  });

  it('开了自动发布且有启用节点：warn，写明节点数', () => {
    const n = useVersionNotice(deploy(), 3);
    expect(n?.tone).toBe('warn');
    expect(n?.text).toContain('3 个启用节点');
  });
});

describe('operateNotice · 节点操作前的后果说明', () => {
  const info = (a: NodeAction): NodeActionInfo => {
    const x = actionInfo(a);
    if (!x) throw new Error(a);
    return x;
  };

  it('已停用节点执行安装、启动、重启：提示会被重新启用', () => {
    for (const a of ['install', 'start', 'restart'] as const) {
      const n = operateNotice(info(a), node({ enable: false }));
      expect(n.tone).toBe('warn');
      expect(n.text).toContain('重新启用');
    }
  });

  it('启用节点执行停止、卸载：提示会被标为停用', () => {
    for (const a of ['stop', 'uninstall'] as const) {
      const n = operateNotice(info(a), node({ enable: true }));
      expect(n.tone).toBe('warn');
      expect(n.text).toContain('标为停用');
    }
  });

  it('其余情况是普通说明，并写明命令不等回包', () => {
    const n = operateNotice(info('install'), node({ enable: true }));
    expect(n.tone).toBe('info');
    expect(n.text).toContain('不等节点回包');
    expect(n.text).toContain('10.0.0.1');
  });

  it('NODE_ACTIONS：五个动作齐全，停止与卸载为危险操作', () => {
    expect(NODE_ACTIONS.map((a) => a.act)).toEqual(['install', 'start', 'restart', 'stop', 'uninstall']);
    expect(NODE_ACTIONS.filter((a) => a.danger).map((a) => a.act)).toEqual(['stop', 'uninstall']);
  });
});

describe('operateNode · 节点操作', () => {
  it('请求参数：act 与部署节点 Id', async () => {
    let seen: ReqOptions | undefined;
    await operateNode(mk((o) => { seen = o; return { code: 0 }; }), 'restart', { id: 5, name: 'n1' });
    expect(seen?.url).toBe('/Deployment/AppDeployNode/Operate');
    expect(seen?.params).toEqual({ act: 'restart', id: 5 });
  });

  it('code 为 0：已下发，文案带节点名与动作，并说明只表示命令已发出', async () => {
    const r = await operateNode(mk(() => ({ code: 0 })), 'install', { id: 5, name: 'n1' });
    expect(r.ok).toBe(true);
    expect(r.message).toContain('n1');
    expect(r.message).toContain('已下发「安装」命令');
    expect(r.message).toContain('只表示命令已发出');
  });

  it('后端返回 500：失败，带回节点名与后端文案', async () => {
    const r = await operateNode(mk(() => ({ code: 500, message: '[5]不存在' })), 'stop', { id: 5, name: 'n1' });
    expect(r.ok).toBe(false);
    expect(r.message).toContain('n1');
    expect(r.message).toContain('[5]不存在');
  });

  it('判不出结果：按失败提示，并让用户去部署历史核对', async () => {
    const r = await operateNode(mk(() => ({})), 'start', { id: 5, name: 'n1' });
    expect(r.ok).toBe(false);
    expect(r.message).toContain('部署历史');
  });

  it('请求抛错：带回错误信息', async () => {
    const r = await operateNode(mk(() => { throw new Error('超时'); }), 'uninstall', { id: 5, name: 'n1' });
    expect(r.ok).toBe(false);
    expect(r.message).toContain('超时');
    expect(r.message).toContain('卸载');
  });

  it('缺少部署节点编号、不支持的操作都不发请求', async () => {
    let called = 0;
    const req = mk(() => { called++; return { code: 0 }; });
    expect((await operateNode(req, 'install', { id: 0, name: 'n1' })).ok).toBe(false);
    expect((await operateNode(req, 'reboot' as NodeAction, { id: 5, name: 'n1' })).ok).toBe(false);
    expect(called).toBe(0);
  });
});

describe('部署历史', () => {
  const rows = [
    { Id: 1, DeployId: 7, NodeId: 11, NodeName: 'n1', Action: 'install', Success: true, Remark: 'ok', TraceId: 't1', CreateTime: '2026-10-28T10:00:00', CreateIP: '1.1.1.1' },
    { Id: 2, DeployId: 7, NodeId: 0, Action: 'pipeline/autoDeploy', Success: false, Remark: 'bad', CreateTime: '2026-10-28T11:00:00' },
  ];

  it('toHistoryRows：字段映射，按时间倒序，NodeId 为 0 显示破折号', () => {
    const r = toHistoryRows(rows);
    expect(r.map((x) => x.id)).toEqual([2, 1]);
    expect(r[0]).toMatchObject({ node: '—', success: false, remark: 'bad', time: '2026-10-28 11:00:00', actionLabel: 'pipeline/autoDeploy' });
    expect(r[1]).toMatchObject({ node: 'n1', actionLabel: '安装', success: true, traceId: 't1', ip: '1.1.1.1' });
  });

  it('actionLabel：识别动作名，兼容流水线写的路径，其余原样', () => {
    expect(actionLabel('install')).toBe('安装');
    expect(actionLabel('deploy/install/Deploy')).toBe('安装');
    expect(actionLabel('deploy/compile/Build-Upload')).toBe('deploy/compile/Build-Upload');
    expect(actionLabel('')).toBe('—');
  });

  it('filterHistoryByNode：按 nodeId 过滤，nodeId 无效返回全部', () => {
    const r = toHistoryRows(rows);
    expect(filterHistoryByNode(r, 11).map((x) => x.id)).toEqual([1]);
    expect(filterHistoryByNode(r, 0)).toHaveLength(2);
  });

  it('fetchDeployHistory：请求参数、deployId 无效不发请求、失败返回 null', async () => {
    let seen: ReqOptions | undefined;
    const ok = await fetchDeployHistory(mk((o) => { seen = o; return { data: rows }; }), 7);
    expect(seen?.url).toBe('/Deployment/AppDeployHistory');
    expect(seen?.params).toMatchObject({ deployId: 7 });
    expect(ok).toHaveLength(2);

    let called = 0;
    expect(await fetchDeployHistory(mk(() => { called++; return {}; }), 0)).toEqual([]);
    expect(called).toBe(0);
    expect(await fetchDeployHistory(mk(() => { throw new Error('x'); }), 7)).toBeNull();
  });
});

describe('手动刷新反馈', () => {
  const OK: RefreshOutcome = { loadError: '', versionsFailed: false, nodesFailed: false, historyFailed: false };

  it('REFRESH_MIN_SPIN_MS 为 300', () => {
    expect(REFRESH_MIN_SPIN_MS).toBe(300);
  });

  it('refreshRemainMs：没用时间补满 300，用了 120 补 180', () => {
    expect(refreshRemainMs(0)).toBe(300);
    expect(refreshRemainMs(120)).toBe(180);
  });

  it('refreshRemainMs：刚好 300 或超过 300 返回 0', () => {
    expect(refreshRemainMs(300)).toBe(0);
    expect(refreshRemainMs(450)).toBe(0);
  });

  it('refreshRemainMs：时钟异常（负数、NaN）按没用时间算', () => {
    expect(refreshRemainMs(-50)).toBe(300);
    expect(refreshRemainMs(Number.NaN)).toBe(300);
  });

  it('refreshRemainMs：可自定义最短时间', () => {
    expect(refreshRemainMs(40, 100)).toBe(60);
    expect(refreshRemainMs(40, 30)).toBe(0);
  });

  it('refreshFeedback：全部成功返回 null', () => {
    expect(refreshFeedback(OK)).toBeNull();
  });

  it('refreshFeedback：整体失败带上原因', () => {
    expect(refreshFeedback({ ...OK, loadError: '部署集加载失败' })).toEqual({ ok: false, text: '刷新失败：部署集加载失败' });
  });

  it('refreshFeedback：版本、节点、历史任一失败都提示未完全成功', () => {
    for (const k of ['versionsFailed', 'nodesFailed', 'historyFailed'] as const) {
      const fb = refreshFeedback({ ...OK, [k]: true });
      expect(fb?.ok).toBe(false);
      expect(fb?.text).toContain('未完全成功');
    }
  });

  it('refreshFeedback：整体失败优先于区块失败', () => {
    const fb = refreshFeedback({ loadError: '部署集加载失败', versionsFailed: true, nodesFailed: true, historyFailed: true });
    expect(fb?.text).toBe('刷新失败：部署集加载失败');
  });
});
