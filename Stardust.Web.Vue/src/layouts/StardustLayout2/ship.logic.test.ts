// 发布页纯逻辑单测（Vitest）：request 用桩函数注入，不发真实请求。
import { describe, it, expect } from 'vitest';
import type { Req, ReqOptions, RunHistoryRow } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import {
  buildNodeResults,
  buildRunRows,
  buildVersionCells,
  fetchDeploy,
  fetchAppOptions,
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
  toLovItem,
  toShipNodes,
  toStepRows,
  toVersionOptions,
  versionDownloadHref,
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
  // CARD-DEP-02：运行筛选 / 详情 / 重跑
  EMPTY_RUN_FILTER,
  fetchRunDetail,
  fetchRunHistoryFiltered,
  isRunFilterActive,
  nodeNameOf,
  reprocessRun,
  runElapsedSeconds,
  RUN_STATUS_FILTERS,
  runFilterParams,
  toRunDetail,
} from './ship.logic';

// 桩：返回什么就是什么；函数内抛错模拟请求失败
const mk = (fn: (o: ReqOptions) => unknown): Req => ((o: ReqOptions) => Promise.resolve(fn(o))) as Req;

const deploy = (over: Partial<DeployInfo> = {}): DeployInfo => ({
  id: 7,
  appId: 3,
  name: '',
  version: '1.0',
  enable: true,
  autoPublish: true,
  multiVersion: false,
  logDirectory: '',
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
  appVersion: '',
  mode: 10,
  modeLabel: 'Standard',
  delay: 0,
  processId: 0,
  startTime: '—',
  compile: '—',
  lastActive: '—',
  ...over,
});

describe('fetchDeploy · 部署集', () => {
  it('按 appId 命中，字段大小写都认', async () => {
    const r = await fetchDeploy(mk(() => ({ data: [{ Id: 7, AppId: 3, Version: '1.0', Enable: true, AutoPublish: true }] })), 3);
    expect(r).toEqual({ status: 'ok', deploy: deploy(), others: { count: 0, nodes: 0 } });
    const r2 = await fetchDeploy(mk(() => ({ data: [{ id: 7, appId: 3, version: '1.0', enable: 1, autoPublish: 'true' }] })), 3);
    expect(r2).toEqual({ status: 'ok', deploy: deploy(), others: { count: 0, nodes: 0 } });
  });

  it('task-90 logDirectory：部署集行大小写都认，缺键为空串', async () => {
    const r = await fetchDeploy(mk(() => ({ data: [{ Id: 7, AppId: 3, Version: '1.0', Enable: true, LogDirectory: 'D:/deploy/logs' }] })), 3);
    expect(r.status).toBe('ok');
    if (r.status === 'ok') expect(r.deploy.logDirectory).toBe('D:/deploy/logs');
    const r2 = await fetchDeploy(mk(() => ({ data: [{ id: 7, appId: 3, logDirectory: '/deploy/logs' }] })), 3);
    expect(r2.status).toBe('ok');
    if (r2.status === 'ok') expect(r2.deploy.logDirectory).toBe('/deploy/logs');
    // 旧构建响应缺键（本地 checkout 后端滞后 212）→ 空串
    const r3 = await fetchDeploy(mk(() => ({ data: [{ Id: 7, AppId: 3 }] })), 3);
    expect(r3.status).toBe('ok');
    if (r3.status === 'ok') expect(r3.deploy.logDirectory).toBe('');
  });

  it('CARD-DEP-08 D2：多集应用返回其余集数与全部集启用节点数之和，主集取第一行', async () => {
    const r = await fetchDeploy(mk(() => ({
      data: [
        { Id: 9, AppId: 3, Name: '主集', Version: '2.0', Enable: true, Nodes: 2 },
        { Id: 5, AppId: 3, Name: '旧集A', Version: '1.0', Enable: true, Nodes: 5 },
        { Id: 6, AppId: 3, Name: '旧集B', Version: '0.9', Nodes: '0' },
      ],
    })), 3);
    expect(r.status).toBe('ok');
    if (r.status === 'ok') {
      expect(r.deploy.id).toBe(9);
      expect(r.deploy.name).toBe('主集');
      expect(r.others).toEqual({ count: 2, nodes: 7 });
    }
  });

  it('其余应用的行不计入 others；Nodes 缺失按 0', async () => {
    const r = await fetchDeploy(mk(() => ({
      data: [
        { Id: 7, AppId: 3, Version: '1.0', Enable: true },
        { Id: 8, AppId: 4, Version: '9.9', Nodes: 99 },
      ],
    })), 3);
    expect(r.status).toBe('ok');
    if (r.status === 'ok') expect(r.others).toEqual({ count: 0, nodes: 0 });
  });

  it('列表里没有该 appId 时是 none，不取第一行', async () => {
    const r = await fetchDeploy(mk(() => ({ data: [{ Id: 8, AppId: 4 }] })), 3);
    expect(r).toEqual({ status: 'none' });
  });

  it('列表为空时是 none', async () => {
    const r = await fetchDeploy(mk(() => ({ data: [] })), 30);
    expect(r).toEqual({ status: 'none' });
  });

  it('task-93 行自描述（回退裁定 22:5x）：掩码契约下行键可被投影，缺 AppId/appId 的行信任后端过滤采纳首行', async () => {
    // 212 新掩码契约（AppDeployController ListFields.RemoveField("AppId", …)）下行内无 appId 可比对：
    // 直接采纳后端过滤结果首行（Id 倒序最新集）；行内带 appId 时仍严格按行过滤，其余应用行不进结果、不进 others。
    const r = await fetchDeploy(mk(() => ({
      data: [
        { id: 25, name: 'MindHub.Api', version: 'v20261111-145554', enable: true, nodes: 3, appName: 'MindHub.Api', port: 9280 },
      ],
    })), 30);
    expect(r.status).toBe('ok');
    if (r.status === 'ok') {
      expect(r.deploy.id).toBe(25);
      expect(r.others).toEqual({ count: 0, nodes: 3 });
    }
    // 字段在=按行过滤：appId=42 行采纳（证据 mineFirstRow appId=42），缺字段行与其余应用行不进结果、不进 others
    const r2 = await fetchDeploy(mk(() => ({
      data: [
        { id: 16, appId: 42, name: 'unit', enable: true, nodes: 1 },
        { id: 17, appName: 'no-field', nodes: 9 },
        { id: 18, appId: 43, name: 'other', nodes: 2 },
      ],
    })), 42);
    expect(r2.status).toBe('ok');
    if (r2.status === 'ok') {
      expect(r2.deploy.id).toBe(16);
      expect(r2.others).toEqual({ count: 0, nodes: 1 });
    }
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

  it('C18 行携带 url 原文（trim 后），缺 Url 字段为空串', () => {
    const o = toVersionOptions([{ Id: 1, Version: '1.0', Enable: true, Url: ' /cube/file?id=7523198473409880064.zip ' }, { Id: 2, Version: '1.1', Enable: true }], '');
    expect(o[1].url).toBe('/cube/file?id=7523198473409880064.zip');
    expect(o[0].url).toBe('');
  });
});

describe('versionDownloadHref · C18 下载包链接', () => {
  const BASE = 'http://localhost:6681';

  it('相对路径拼 API 基址（base 注入）', () => {
    expect(versionDownloadHref('/cube/file?id=7523198473409880064.zip', BASE)).toBe('http://localhost:6681/cube/file?id=7523198473409880064.zip');
  });

  it('绝对地址直通不拼基址', () => {
    expect(versionDownloadHref('http://127.0.0.1/pkg/1.0.0.zip', BASE)).toBe('http://127.0.0.1/pkg/1.0.0.zip');
  });

  it('无 Url/空白/缺字段返回 null（不渲染链接）', () => {
    expect(versionDownloadHref('', BASE)).toBeNull();
    expect(versionDownloadHref('  ', BASE)).toBeNull();
    expect(versionDownloadHref(undefined, BASE)).toBeNull();
    expect(versionDownloadHref(null, BASE)).toBeNull();
  });

  it('基址尾斜杠去重、相对路径不带斜杠时补齐', () => {
    expect(versionDownloadHref('cube/file?id=1', 'http://localhost:6681/')).toBe('http://localhost:6681/cube/file?id=1');
  });

  it('无 base（单测环境无 VITE_APP_BASE_API 兜底）时相对路径降级原样返回', () => {
    expect(versionDownloadHref('/cube/file?id=1', '')).toBe('/cube/file?id=1');
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

  it('task-90 toShipNodes：logDirectory 大小写都认，缺键为空串', () => {
    const n = toShipNodes([
      { Id: 1, NodeId: 11, LogDirectory: 'D:/node/logs' },
      { Id: 2, NodeId: 12, logDirectory: '/node/logs' },
      { Id: 3, NodeId: 13 },
    ]);
    expect(n.map((x) => x.logDirectory)).toEqual(['D:/node/logs', '/node/logs', '']);
  });

  it('task-92 toShipNodes：延迟/进程/进程时间/编译时间/最后活跃 双认大小写（212 Pascal 形）', () => {
    const n = toShipNodes([
      { Id: 1, NodeId: 11, Delay: 30, ProcessId: 61000, StartTime: '2026-11-01T19:09:36', Compile: '2026-10-30 11:20:00', LastActive: '2026-11-01T19:09:42' },
      { Id: 2, NodeId: 12, delay: 0, processId: 0, startTime: '0001-01-01 00:00:00', compile: '', lastActive: '2026-11-02 08:05:00' },
    ]);
    expect(n[0]).toMatchObject({ delay: 30, processId: 61000, startTime: '2026-11-01 19:09', compile: '2026-10-30 11:20', lastActive: '2026-11-01 19:09' });
    expect(n[1]).toMatchObject({ delay: 0, processId: 0, startTime: '—', compile: '—', lastActive: '2026-11-02 08:05' });
  });

  it('task-92 toShipNodes：真实响应体回放（本地 6681 AppDeployNode deployId=3 抓包，Doc/用户旅程/evidence/deploy-nodes-cols/response.json）', () => {
    // 逐键忠实拷贝自抓包（camelCase、null 保留、"0001-01-01 00:00:00" 占位、时间带秒）——防线一：fixture 必须回放真实形状
    const row5 = { nodeName: 'DGCWIAX10', id: 5, deployName: null, deployId: 3, nodeId: 3, ip: null, enable: false, version: null, port: 0, fileName: null, arguments: null, workingDirectory: null, userName: null, environments: null, maxMemory: 0, priority: 0, mode: 0, delay: 0, healthCheck: null, processId: 61000, processName: 'exec', processUser: '12504', startTime: '2026-11-01 19:09:36', compile: '0001-01-01 00:00:00', listens: null, lastActive: '2026-11-01 19:09:42', lastUpload: '0001-01-01 00:00:00', traceId: '0abc528d1793531382630000003eee48', createUserId: 0, createTime: '2026-11-01 19:08:02', createIP: '127.0.0.1', updateUserId: 0, updateTime: '2026-11-01 19:09:42', updateIP: '127.0.0.1', remark: null };
    const row10 = { nodeName: 'mcpapi-node', id: 10, deployName: null, deployId: 3, nodeId: 1, ip: '127.0.0.1', enable: true, version: '1.0.0', port: 0, fileName: null, arguments: null, workingDirectory: null, userName: null, environments: null, maxMemory: 0, priority: 0, mode: 0, delay: 0, healthCheck: null, processId: 0, processName: null, processUser: null, startTime: '0001-01-01 00:00:00', compile: '0001-01-01 00:00:00', listens: null, lastActive: '0001-01-01 00:00:00', lastUpload: '0001-01-01 00:00:00', traceId: null, createUserId: 0, createTime: '2026-11-02 11:29:34', createIP: '10.188.82.141', updateUserId: 0, updateTime: '2026-11-02 11:29:34', updateIP: '10.188.82.141', remark: null };
    const [a, b] = toShipNodes([row5, row10]);
    expect(a).toMatchObject({ id: 5, name: 'DGCWIAX10', ip: '', delay: 0, processId: 61000, startTime: '2026-11-01 19:09', compile: '—', lastActive: '2026-11-01 19:09' });
    expect(b).toMatchObject({ id: 10, name: 'mcpapi-node', delay: 0, processId: 0, startTime: '—', compile: '—', lastActive: '—' });
  });

  it('AppDeployNode 版本双列：appVersion（应用在线上报的程序集文件版本）大小写都认，缺键为空串；version（部署版本，install 下发回填）不受影响', () => {
    const n = toShipNodes([
      { Id: 1, NodeId: 11, Version: 'v20261112-100000', AppVersion: '1.2.3.0' },
      { Id: 2, NodeId: 12, version: 'v20261112-100000', appVersion: '2.0.0.0' },
      { Id: 3, NodeId: 13 },
    ]);
    expect(n[0]).toMatchObject({ version: 'v20261112-100000', appVersion: '1.2.3.0' });
    expect(n[1]).toMatchObject({ version: 'v20261112-100000', appVersion: '2.0.0.0' });
    expect(n[2]).toMatchObject({ version: '', appVersion: '' });
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

  it('fmtTime：DateTime.MinValue（0001-01-01，未到达/空值语义）占位破折号（走查缺陷3）', () => {
    expect(fmtTime('0001-01-01T00:00:00')).toBe('—');
    expect(fmtTime('0001-01-01 00:00:00')).toBe('—');
    // 不误伤正常年份
    expect(fmtTime('2026-01-01T08:00:00')).toBe('2026-01-01 08:00');
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

// ================= CARD-DEP-02（2026-11-07）：运行筛选 / 详情 / 重跑 / 步骤明细行 =================

describe('ship · RUN_STATUS_FILTERS 与筛选参数', () => {
  it('状态下拉七项逐字（全部/等待/编译中/上传成功/部署中/成功/失败/已取消），值 -1..6', () => {
    expect(RUN_STATUS_FILTERS.map((o) => o.label)).toEqual(['全部', '等待', '编译中', '上传成功', '部署中', '成功', '失败', '已取消']);
    expect(RUN_STATUS_FILTERS.map((o) => o.value)).toEqual([-1, 0, 1, 2, 3, 4, 5, 6]);
  });

  it('isRunFilterActive：status>=0 或任一日期非空才生效', () => {
    expect(isRunFilterActive({ status: -1, dtStart: '', dtEnd: '' })).toBe(false);
    expect(isRunFilterActive({ status: 4, dtStart: '', dtEnd: '' })).toBe(true);
    expect(isRunFilterActive({ status: -1, dtStart: '2026-11-01', dtEnd: '' })).toBe(true);
  });

  it('runFilterParams：只带生效项（status=-1 不带、空日期不带）', () => {
    expect(runFilterParams({ status: -1, dtStart: '', dtEnd: '' })).toEqual({});
    expect(runFilterParams({ status: 5, dtStart: '2026-11-01', dtEnd: '2026-11-07' })).toEqual({ status: 5, dtStart: '2026-11-01', dtEnd: '2026-11-07' });
    expect(runFilterParams({ status: 0, dtStart: '', dtEnd: '2026-11-07' })).toEqual({ status: 0, dtEnd: '2026-11-07' });
  });

  it('fetchRunHistoryFiltered：逐流水线带筛选参数，经 mergeRunHistory 合并去重', async () => {
    const seen: ReqOptions[] = [];
    const req = mk((o) => {
      seen.push(o);
      return { code: 0, data: [{ Id: 11, PipelineId: (o.params as Record<string, unknown>).pipelineId, TriggerSource: 'webhook' }] };
    });
    const rows = await fetchRunHistoryFiltered(req, [{ id: 7, name: 'a' }, { id: 8, name: 'b' }], { status: 4, dtStart: '2026-11-01', dtEnd: '' });
    expect(rows.map((r) => r.id)).toEqual([11]);
    expect(seen.map((o) => o.url)).toEqual(['/Deployment/AppPipelineRun', '/Deployment/AppPipelineRun']);
    const p0 = seen[0].params as Record<string, unknown>;
    expect(p0.pipelineId).toBe(7);
    expect(p0.status).toBe(4);
    expect(p0.dtStart).toBe('2026-11-01');
    expect(p0.PageSize).toBe(20);
    // status=-1 不带参数
    seen.length = 0;
    await fetchRunHistoryFiltered(req, [{ id: 7 }], EMPTY_RUN_FILTER);
    expect(seen[0].params).toEqual({ pipelineId: 7, PageSize: 20 });
  });
});

describe('ship · 运行详情（Api Detail JSON）', () => {
  it('toRunDetail：字段映射、状态 Tone、active 标记、steps 九字段含起止时间', () => {
    const d = toRunDetail({
      Id: 42, PipelineId: 7, Status: 'Deploying', TriggerSource: 'webhook', Branch: 'release/*',
      CommitId: 'a1b2c3d', CommitMessage: 'fix', CommitAuthor: 'zhang', CommitTime: '2026-11-07T09:12:00',
      BuildNodeId: 3, AppVersionId: 12,
      BuildStartedTime: '2026-11-07T09:13:00', BuildFinishedTime: '2026-11-07T09:19:00',
      DeployStartedTime: '2026-11-07T09:20:00', DeployFinishedTime: null,
      TraceId: 'abcd1234', Remark: '',
      steps: [
        { Id: 901, RunId: 42, StepType: 'Build', StepIndex: 0, NodeId: 0, CommandId: 5501, Status: 'Success', Message: 'ok', StartedTime: '2026-11-07T09:13:00', FinishedTime: '2026-11-07T09:19:00' },
        { Id: 902, RunId: 42, StepType: 'Deploy', StepIndex: 1, NodeId: 1, CommandId: 5502, Status: 'Failed', Message: 'x', StartedTime: '2026-11-07T09:20:00', FinishedTime: '2026-11-07T09:20:40' },
      ],
    });
    expect(d.statusLabel).toBe('部署中');
    expect(d.tone).toBe('brand');
    expect(d.active).toBe(true);
    expect(d.commitTime).toBe('2026-11-07 09:12');
    expect(d.steps.map((s) => s.id)).toEqual([901, 902]);
    expect(d.steps[1]).toMatchObject({ stepType: 'Deploy', stepIndex: 1, commandId: 5502, status: 'Failed', startText: '2026-11-07 09:20', endText: '2026-11-07 09:20' });
  });

  it('runElapsedSeconds：终点取部署完成缺省编译完成；任一缺失为 null；终态成功手算 480 秒', () => {
    const base = toRunDetail({ BuildStartedTime: '2026-11-07T09:13:00', BuildFinishedTime: '2026-11-07T09:19:00', DeployFinishedTime: '2026-11-07T09:21:00' });
    expect(runElapsedSeconds(base)).toBe(480);
    expect(runElapsedSeconds(toRunDetail({ BuildStartedTime: '2026-11-07T09:13:00', BuildFinishedTime: '2026-11-07T09:19:00' }))).toBe(360);
    expect(runElapsedSeconds(toRunDetail({}))).toBeNull();
  });

  it('fetchRunDetail：GET /Deployment/AppPipelineRun/Detail?id=；data 缺失→missing；HTTP 500→missing；网络异常→null', async () => {
    const okReq = mk(() => ({ code: 0, data: { Id: 42, Status: 'Success', steps: [] } }));
    const d = await fetchRunDetail(okReq, 42);
    expect(d && d !== 'missing' ? d.id : -1).toBe(42);

    const emptyReq = mk(() => ({ code: 0, data: null }));
    expect(await fetchRunDetail(emptyReq, 42)).toBe('missing');
    expect(await fetchRunDetail(emptyReq, 0)).toBe('missing');

    const err500 = Object.assign(new Error('500'), { status: 500 });
    expect(await fetchRunDetail((async () => { throw err500; }) as Req, 42)).toBe('missing');

    const errNet = new Error('timeout');
    expect(await fetchRunDetail((async () => { throw errNet; }) as Req, 42)).toBeNull();
  });

  it('reprocessRun：GET /Deployment/AppPipelineRun/Reprocess?id=，message 透传', async () => {
    const req = mk(() => ({ code: 0, message: '流水线已重新触发，运行编号[43]' }));
    expect(await reprocessRun(req, 42)).toEqual({ ok: true, message: '流水线已重新触发，运行编号[43]' });
  });

  it('nodeNameOf：命中返回节点名，未命中兜底「节点 {Id}」，0 为 —', () => {
    const nodes: ShipNode[] = [{ id: 1, nodeId: 1, name: 'node-a', ip: '', enable: true, version: '', appVersion: '', mode: 10, modeLabel: 'Standard', delay: 0, processId: 0, startTime: '—', compile: '—', lastActive: '—' }];
    expect(nodeNameOf(nodes, 1)).toBe('node-a');
    expect(nodeNameOf(nodes, 9)).toBe('节点 9');
    expect(nodeNameOf(nodes, 0)).toBe('—');
  });
});

// CARD-DEP-08 D5（J-REG-01 §8 行 9 移交项）：应用联想纯函数
describe('fetchAppOptions / toLovItem · D5 应用联想', () => {
  it('fetchAppOptions：GET /Registry/App/AppSearch params {key}，字段大小写都认，无效行过滤', async () => {
    const seen: ReqOptions[] = [];
    const req = mk((o) => {
      seen.push(o);
      return {
        code: 0,
        data: [
          { Id: 3, Name: 'StarServer', DisplayName: '服务端' },
          { id: 5, name: 'StarWeb', displayName: 'Web 端' },
          { Id: 0, Name: 'bad' }, // 无效 id 行过滤
          { Name: 'noId' },
        ],
      };
    });
    const list = await fetchAppOptions(req, 'star');
    expect(seen).toHaveLength(1);
    expect(seen[0].url).toBe('/Registry/App/AppSearch');
    expect(seen[0].method).toBe('GET');
    expect((seen[0].params as Record<string, unknown>).key).toBe('star');
    expect(list).toEqual([
      { id: 3, name: 'StarServer', displayName: '服务端' },
      { id: 5, name: 'StarWeb', displayName: 'Web 端' },
    ]);
  });

  it('fetchAppOptions：空/空白关键字不发请求返回 []；请求失败返回 []', async () => {
    const seen: ReqOptions[] = [];
    const req = mk((o) => {
      seen.push(o);
      return { code: 0, data: [] };
    });
    expect(await fetchAppOptions(req, '')).toEqual([]);
    expect(await fetchAppOptions(req, '   ')).toEqual([]);
    expect(seen).toHaveLength(0);

    const boom = (async () => { throw new Error('net'); }) as Req;
    expect(await fetchAppOptions(boom, 'star')).toEqual([]);
  });

  it('toLovItem：有显示名拼「name（displayName）」，无显示名只留 name', () => {
    expect(toLovItem({ id: 3, name: 'StarServer', displayName: '服务端' })).toEqual({ value: 'StarServer（服务端）', id: 3 });
    expect(toLovItem({ id: 5, name: 'StarWeb', displayName: '' })).toEqual({ value: 'StarWeb', id: 5 });
  });
});
