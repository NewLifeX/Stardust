// 命令面板数据来源单测（Vitest）：request 用桩函数注入，不发真实请求。
import { describe, it, expect } from 'vitest';
import { buildPaletteApps, buildPaletteNodes, fetchPaletteData, resolveVerb, type PaletteData } from './paletteData.logic';

type Req = Parameters<typeof fetchPaletteData>[0];

const NOW = Date.parse('2026-01-01T00:00:00Z');
const secAgo = (s: number) => new Date(NOW - s * 1000).toISOString();

/** 按 url 返回预设数据；值为 Error 时模拟请求失败；calls 记录请求过的 url */
function stub(responses: Record<string, unknown>, calls: string[] = []): Req {
  return ({ url }) => {
    calls.push(url);
    const r = responses[url];
    return r instanceof Error ? Promise.reject(r) : Promise.resolve({ data: r });
  };
}

const appRows = () => [
  { id: 1, name: 'OrderService', category: '业务', version: '1.2.0', enable: true },
  { id: 2, name: 'Idle', enable: true },
  { id: 3, name: 'Disabled', enable: false },
  { name: 'NoId', enable: true },
  { id: 5, enable: true },
];

const onlineRows = () => [
  { appName: 'OrderService', nodeName: 'n1' },
  { appName: 'OrderService', nodeName: 'n2' },
  { appName: 'Disabled', nodeName: 'n1' },
];

const nodeRows = () => [
  { id: 1, name: 'n1', iP: '10.0.0.1' },
  { id: 2, name: 'n2', address: '10.0.0.2' },
  { id: 3, name: 'cold', iP: '10.0.0.3', lastActive: secAgo(3600) },
  { id: 4, name: 'alarm', iP: '10.0.0.4', lastActive: secAgo(30), cpu: 90, alarmCpuRate: 80 },
  { id: 5 },
  { id: 6, name: 'noip' },
];

describe('buildPaletteApps · 应用行映射', () => {
  it('实例数按 appName 计数，有实例且已启用算在线', () => {
    const r = buildPaletteApps(appRows(), onlineRows()).find((a) => a.name === 'OrderService');
    expect(r).toEqual({ id: 1, name: 'OrderService', category: '业务', version: '1.2.0', instances: 2, online: true });
  });

  it('已启用但没有在线实例算离线', () => {
    const r = buildPaletteApps(appRows(), onlineRows()).find((a) => a.name === 'Idle');
    expect(r?.online).toBe(false);
    expect(r?.instances).toBe(0);
  });

  it('未启用算离线，即使还有在线实例', () => {
    const r = buildPaletteApps(appRows(), onlineRows()).find((a) => a.name === 'Disabled');
    expect(r?.online).toBe(false);
    expect(r?.instances).toBe(1);
  });

  it('没有名字的行被丢弃', () => {
    expect(buildPaletteApps(appRows(), onlineRows())).toHaveLength(3);
  });

  it('没有 Id 的行被丢弃：没有 Id 就拼不出工作台路由', () => {
    const names = buildPaletteApps(appRows(), onlineRows()).map((a) => a.name);
    expect(names).not.toContain('NoId');
  });

  it('空输入返回空数组', () => {
    expect(buildPaletteApps([], [])).toEqual([]);
  });
});

describe('buildPaletteNodes · 节点行映射', () => {
  it('在线实例的 nodeName 命中算在线', () => {
    const r = buildPaletteNodes(nodeRows(), onlineRows(), NOW).find((n) => n.name === 'n1');
    expect(r?.online).toBe(true);
  });

  it('iP 缺失时用 address 作为 ip', () => {
    const r = buildPaletteNodes(nodeRows(), onlineRows(), NOW).find((n) => n.name === 'n2');
    expect(r?.ip).toBe('10.0.0.2');
  });

  it('120 秒内活跃算在线', () => {
    const rows = [{ id: 9, name: 'recent', iP: '10.0.0.9', lastActive: secAgo(30) }];
    expect(buildPaletteNodes(rows, [], NOW)[0].online).toBe(true);
  });

  it('CPU 超告警阈值的异常节点仍算在线', () => {
    const r = buildPaletteNodes(nodeRows(), onlineRows(), NOW).find((n) => n.name === 'alarm');
    expect(r?.online).toBe(true);
  });

  it('长时间不活跃且无在线实例算离线', () => {
    const r = buildPaletteNodes(nodeRows(), onlineRows(), NOW).find((n) => n.name === 'cold');
    expect(r?.online).toBe(false);
  });

  it('没有名字和编码的节点被丢弃', () => {
    const names = buildPaletteNodes(nodeRows(), onlineRows(), NOW).map((n) => n.name);
    expect(names).toEqual(['n1', 'n2', 'cold', 'alarm', 'noip']);
  });

  it('没有 ip 时为 undefined，不显示 "-"', () => {
    const r = buildPaletteNodes(nodeRows(), onlineRows(), NOW).find((n) => n.name === 'noip');
    expect(r?.ip).toBeUndefined();
  });

  it('id 原样保留', () => {
    const r = buildPaletteNodes(nodeRows(), onlineRows(), NOW).find((n) => n.name === 'alarm');
    expect(r?.id).toBe(4);
  });
});

describe('fetchPaletteData · 取数与失败处理', () => {
  it('三个接口都成功：有数据，failed 为空', async () => {
    const request = stub({ '/Registry/App': appRows(), '/Registry/AppOnline': onlineRows(), '/Nodes/Node': nodeRows() });
    const d = await fetchPaletteData(request, NOW);
    expect(d.failed).toEqual([]);
    expect(d.apps).toHaveLength(3);
    expect(d.nodes).toHaveLength(5);
  });

  it('请求的是应用、在线实例、节点三个接口', async () => {
    const calls: string[] = [];
    const request = stub({ '/Registry/App': [], '/Registry/AppOnline': [], '/Nodes/Node': [] }, calls);
    await fetchPaletteData(request, NOW);
    expect([...calls].sort()).toEqual(['/Nodes/Node', '/Registry/App', '/Registry/AppOnline']);
  });

  it('在线实例请求失败：应用与节点都算失败，数组为空', async () => {
    const request = stub({
      '/Registry/App': appRows(),
      '/Registry/AppOnline': new Error('boom'),
      '/Nodes/Node': nodeRows(),
    });
    const d = await fetchPaletteData(request, NOW);
    expect(d.failed).toEqual(['apps', 'nodes']);
    expect(d.apps).toEqual([]);
    expect(d.nodes).toEqual([]);
  });

  it('只有节点请求失败：应用照常返回', async () => {
    const request = stub({
      '/Registry/App': appRows(),
      '/Registry/AppOnline': onlineRows(),
      '/Nodes/Node': new Error('boom'),
    });
    const d = await fetchPaletteData(request, NOW);
    expect(d.failed).toEqual(['nodes']);
    expect(d.apps).toHaveLength(3);
    expect(d.nodes).toEqual([]);
  });

  it('只有应用请求失败：节点照常返回', async () => {
    const request = stub({
      '/Registry/App': new Error('boom'),
      '/Registry/AppOnline': onlineRows(),
      '/Nodes/Node': nodeRows(),
    });
    const d = await fetchPaletteData(request, NOW);
    expect(d.failed).toEqual(['apps']);
    expect(d.apps).toEqual([]);
    expect(d.nodes).toHaveLength(5);
  });

  it('data 不是数组时按没有数据处理，不算失败', async () => {
    const request = stub({ '/Registry/App': {}, '/Registry/AppOnline': null, '/Nodes/Node': 'x' });
    const d = await fetchPaletteData(request, NOW);
    expect(d.failed).toEqual([]);
    expect(d.apps).toEqual([]);
    expect(d.nodes).toEqual([]);
  });
});

const okData = (over: Partial<PaletteData> = {}): PaletteData => ({
  apps: [
    { id: 1, name: 'OrderService', version: '1.2.0', instances: 2, online: true },
    { id: 2, name: 'Idle', online: false },
  ],
  nodes: [
    { id: 1, name: 'n1', ip: '10.0.0.1', online: true },
    { id: 2, name: 'cold', ip: '10.0.0.3', online: false },
  ],
  failed: [],
  ...over,
});

describe('resolveVerb · 动词键的结果', () => {
  it('A 键：传了 startWizard 就启动向导', () => {
    expect(resolveVerb('A', okData(), { go: () => {}, startWizard: () => {} })).toEqual({ type: 'wizard' });
  });

  it('A 键：没传 startWizard 提示「接入向导待接入」', () => {
    expect(resolveVerb('A', okData(), { go: () => {} })).toEqual({
      type: 'toast',
      message: '接入向导待接入',
      tone: 'warn',
    });
  });

  it('A 键不受取数失败影响', () => {
    const data = okData({ failed: ['apps', 'nodes'] });
    expect(resolveVerb('A', data, { go: () => {}, startWizard: () => {} })).toEqual({ type: 'wizard' });
  });

  it.each(['P', 'C', 'L'] as const)('%s 键：应用取数失败提示「应用列表加载失败」', (verb) => {
    const data = okData({ failed: ['apps'] });
    expect(resolveVerb(verb, data, { go: () => {} })).toEqual({
      type: 'toast',
      message: '应用列表加载失败',
      tone: 'err',
    });
  });

  it('P 键：有在线应用时打开面板，只列在线项', () => {
    const r = resolveVerb('P', okData(), { go: () => {} });
    expect(r.type).toBe('palette');
    if (r.type !== 'palette') return;
    expect(r.config.groups).toHaveLength(1);
    expect(r.config.groups[0].list.map((i) => i.text)).toEqual(['OrderService']);
  });

  it('P 键：提示语是「发布哪个应用？」', () => {
    const r = resolveVerb('P', okData(), { go: () => {} });
    expect(r.type === 'palette' && r.config.placeholder).toBe('发布哪个应用？');
  });

  it('P 键：选中应用后跳到该应用的发布页签', () => {
    const calls: string[] = [];
    const r = resolveVerb('P', okData(), { go: (p) => calls.push(p) });
    if (r.type !== 'palette') throw new Error('应当打开面板');
    r.config.groups[0].list[0].run();
    expect(calls).toEqual(['/app/versions/1']);
  });

  it('P 键：全是离线应用时提示「暂无在线应用」', () => {
    const data = okData({ apps: [{ id: 2, name: 'Idle', online: false }] });
    expect(resolveVerb('P', data, { go: () => {} })).toEqual({
      type: 'toast',
      message: '暂无在线应用',
      tone: 'warn',
    });
  });

  it('P 键：应用列表为空时提示「暂无在线应用」', () => {
    expect(resolveVerb('P', okData({ apps: [] }), { go: () => {} })).toEqual({
      type: 'toast',
      message: '暂无在线应用',
      tone: 'warn',
    });
  });

  it('P 键：只有节点取数失败时不受影响', () => {
    const r = resolveVerb('P', okData({ failed: ['nodes'] }), { go: () => {} });
    expect(r.type).toBe('palette');
  });

  it('N 键：节点取数失败提示「节点列表加载失败」', () => {
    const data = okData({ failed: ['nodes'] });
    expect(resolveVerb('N', data, { go: () => {}, openTerm: () => {} })).toEqual({
      type: 'toast',
      message: '节点列表加载失败',
      tone: 'err',
    });
  });

  it('N 键：取数失败优先于「终端节点选择待接入」', () => {
    const data = okData({ failed: ['nodes'] });
    expect(resolveVerb('N', data, { go: () => {} })).toMatchObject({ message: '节点列表加载失败' });
  });

  it('N 键：没传 openTerm 提示「终端节点选择待接入」', () => {
    expect(resolveVerb('N', okData(), { go: () => {} })).toEqual({
      type: 'toast',
      message: '终端节点选择待接入',
      tone: 'warn',
    });
  });

  it('N 键：有在线节点时打开面板，只列在线项', () => {
    const r = resolveVerb('N', okData(), { go: () => {}, openTerm: () => {} });
    expect(r.type).toBe('palette');
    if (r.type !== 'palette') return;
    expect(r.config.placeholder).toBe('连接哪台节点？');
    expect(r.config.groups[0].list.map((i) => i.text)).toEqual(['n1']);
  });

  it('N 键：选中节点后用节点 id 调 openTerm', () => {
    const ids: Array<string | number> = [];
    const r = resolveVerb('N', okData(), { go: () => {}, openTerm: (id) => ids.push(id) });
    if (r.type !== 'palette') throw new Error('应当打开面板');
    r.config.groups[0].list[0].run();
    expect(ids).toEqual([1]);
  });

  it('N 键：全是离线节点时提示「暂无在线节点」', () => {
    const data = okData({ nodes: [{ id: 2, name: 'cold', online: false }] });
    expect(resolveVerb('N', data, { go: () => {}, openTerm: () => {} })).toEqual({
      type: 'toast',
      message: '暂无在线节点',
      tone: 'warn',
    });
  });
});
