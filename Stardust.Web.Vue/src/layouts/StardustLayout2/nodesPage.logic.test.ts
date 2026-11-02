import { describe, expect, it } from 'vitest';
import {
  mergeNodes,
  toneCounts,
  filterConsole,
  toggleSelect,
  barLevel,
  relTime,
  memPct,
  layoutNodes,
  toNodeAppRows,
  cpuPoints,
  fetchNodes,
  fetchNodeApps,
  type NodeRowView,
} from './nodesPage.logic.ts';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

// 固定“现在”，让心跳/相对时间可断言（2026-10-30 10:00:00 UTC）
const NOW = Date.parse('2026-10-30T10:00:00Z');
const ago = (sec: number) => new Date(NOW - sec * 1000).toISOString();

// 节点清单（camelCase JSON，与后端序列化一致）
const NODES = [
  {
    id: 1, name: 'web-1', code: 'N001', iP: '10.0.0.1', projectName: 'tokyo',
    oS: 'Windows', oSVersion: 'Server 2022', version: 'v3.9.2',
    framework: '.NET 8', runtime: '8.0.11', machineName: 'WEB1',
    lastActive: ago(30), alarmCpuRate: 80,
  },
  {
    id: 2, name: 'db-1', code: 'N002', iP: '10.0.0.2', projectName: 'tokyo',
    oS: 'Linux', oSVersion: 'Ubuntu 22', version: 'v3.9.2',
    lastActive: ago(20), alarmCpuRate: 80,
  },
  {
    id: 3, name: 'cache-1', code: 'N003', iP: '10.0.0.3', projectName: 'osaka',
    oS: 'Linux', lastActive: ago(99999),
  },
];

// 在线快照（NodeOnline：cpuRate 百分比、memory/memoryUsed 单位 M）
const ONLINE = [
  { nodeId: 1, name: 'web-1', cpuRate: 42.5, memory: 16384, memoryUsed: 8192, pingCount: 1234 },
  { nodeId: 2, name: 'db-1', cpuRate: 92, memory: 32768, memoryUsed: 24576, pingCount: 88 },
];

// 节点上在线应用（AppOnline：nodeId 关联节点）
const APPS = [
  { id: 11, name: 'OrderService', nodeId: 1, version: '1.2.0', processName: 'OrderService.exe', processId: 1001 },
  { id: 12, name: 'IotWeb', nodeId: 1, version: '2.0.1', processName: 'IotWeb', processId: 1002 },
  { id: 13, name: 'mysql', nodeId: 2, version: '8.0.36', processName: 'mysqld', processId: 2001 },
];

describe('mergeNodes：mapNodes 衔接 + 实时指标合并', () => {
  it('正常/高负载/离线三态，CPU%/内存%/服务数正确', () => {
    const rows = mergeNodes(NODES, ONLINE, APPS, NOW);
    expect(rows).toHaveLength(3);

    const web = rows[0];
    expect(web.id).toBe(1);
    expect(web.tone).toBe('running'); // cpu 42.5 < 80
    expect(web.cpu).toBe(42.5);
    expect(web.mem).toBe(50); // 8192/16384
    expect(web.services).toBe(2); // OrderService + IotWeb
    expect(web.cluster).toBe('tokyo');
    expect(web.os).toBe('Windows Server 2022');

    const db = rows[1];
    expect(db.tone).toBe('high'); // cpu 92 >= 80
    expect(db.cpu).toBe(92);
    expect(db.mem).toBe(75); // 24576/32768
    expect(db.services).toBe(1);

    const cache = rows[2];
    expect(cache.tone).toBe('offline'); // 无在线快照 + 心跳陈旧
    expect(cache.cpu).toBeNull();
    expect(cache.mem).toBeNull();
    expect(cache.services).toBe(0);
  });

  it('空数组 → 空行，不抛错', () => {
    expect(mergeNodes([], [], [], NOW)).toEqual([]);
    expect(mergeNodes(undefined as never, undefined as never, undefined as never, NOW)).toEqual([]);
  });

  it('缺字段/脏值防御：null、空串、NaN、缺 online 行都不炸', () => {
    const dirty = [
      { id: 7, name: '', iP: '', lastActive: ago(5) }, // 无 projectName/code/os
      { id: 8, name: 'bad', iP: 'x', cpu: 'abc', lastActive: ago(5) },
      { id: 9 }, // 全缺
    ];
    const rows = mergeNodes(dirty, [], [], NOW);
    expect(rows).toHaveLength(3);
    // 无在线快照且心跳新 → mapNodes 视为在线；cpu 无快照 → null
    expect(rows[0].cpu).toBeNull();
    expect(rows[0].mem).toBeNull();
    expect(rows[0].cluster).toBe('默认集群');
    expect(rows[2].name).toBe('-'); // mapNodes 空名兜底
  });

  it('memPct：used/total 计算与越界兜底', () => {
    expect(memPct({ memory: 100, memoryUsed: 25 })).toBe(25);
    expect(memPct({})).toBeNull();
    expect(memPct({ memory: 0, memoryUsed: 5 })).toBeNull();
    expect(memPct({ memory: 100, memoryUsed: 150 })).toBe(100);
  });
});

describe('过滤 / 勾选 / 计数', () => {
  const rows = mergeNodes(NODES, ONLINE, APPS, NOW);

  it('toneCounts：正常1 高负载1 离线1', () => {
    expect(toneCounts(rows)).toEqual({ total: 3, running: 1, high: 1, offline: 1 });
  });

  it('filterConsole：状态过滤 + 名称/IP 搜索', () => {
    expect(filterConsole(rows, 'high', '')).toHaveLength(1);
    expect(filterConsole(rows, 'high', '')[0].id).toBe(2);
    expect(filterConsole(rows, 'offline', '')).toHaveLength(1);
    expect(filterConsole(rows, 'all', 'web')).toHaveLength(1);
    expect(filterConsole(rows, 'all', '10.0.0.3')).toHaveLength(1); // 按 IP 命中
    expect(filterConsole(rows, 'all', 'NOPE')).toHaveLength(0);
    expect(filterConsole(rows, 'all', '')).toHaveLength(3);
  });

  it('toggleSelect：不可变切换，批量计数随集合', () => {
    let sel = toggleSelect(new Set<number>(), 1);
    sel = toggleSelect(sel, 2);
    expect([...sel].sort()).toEqual([1, 2]);
    const again = toggleSelect(sel, 1);
    expect([...again]).toEqual([2]);
    expect(sel.has(1)).toBe(true); // 原集合不变
  });
});

describe('展示小工具', () => {
  it('barLevel：阈值分档', () => {
    expect(barLevel(10)).toBe('ok');
    expect(barLevel(70)).toBe('warn');
    expect(barLevel(95)).toBe('danger');
    expect(barLevel(null)).toBeNull();
  });
  it('relTime：中文相对时间', () => {
    expect(relTime(ago(10), NOW)).toBe('刚刚');
    expect(relTime(ago(1900), NOW)).toBe('31 分钟前');
    expect(relTime(ago(7200), NOW)).toBe('2 小时前');
    expect(relTime(ago(90000), NOW)).toBe('1 天前');
    expect(relTime(undefined, NOW)).toBe('—');
    expect(relTime('not-a-date', NOW)).toBe('—');
  });
  it('layoutNodes：确定性坐标，同一输入两次结果一致', () => {
    const rows = mergeNodes(NODES, ONLINE, APPS, NOW);
    const a = layoutNodes(rows);
    const b = layoutNodes(rows);
    expect(a).toEqual(b);
    expect(a.map((n) => n.id)).toEqual([1, 2, 3]);
    for (const n of a) {
      expect(n.x).toBeGreaterThanOrEqual(0);
      expect(n.x).toBeLessThanOrEqual(1);
      expect(n.y).toBeGreaterThanOrEqual(0);
      expect(n.y).toBeLessThanOrEqual(1);
    }
  });
});

describe('抽屉数据归一', () => {
  it('toNodeAppRows：映射字段、过滤无名行', () => {
    const rows = toNodeAppRows(APPS);
    expect(rows).toHaveLength(3);
    expect(rows[0]).toMatchObject({ name: 'OrderService', version: '1.2.0', pid: 1001 });
    expect(toNodeAppRows([{ foo: 1 }])).toHaveLength(0); // 无 name → 过滤
  });
  it('cpuPoints：NodeStat 是每日全局聚合，无单节点 CPU 字段 → 空点（走空态）', () => {
    // 真实 NodeStat 行：Category/StatDate/Key/Total/Actives…
    const statRows = [
      { Category: 'OS', StatDate: '2026-10-29', Key: 'Windows', Total: 12, Actives: 9 },
      { Category: 'OS', StatDate: '2026-10-28', Key: 'Linux', Total: 8, Actives: 7 },
    ];
    expect(cpuPoints(statRows)).toEqual([]); // 无 cpu 字段 → 不产生走势点
    // 若某天真带了 cpu 数值，则能抽出
    expect(cpuPoints([{ statDate: '2026-10-30', cpuRate: 33 }])).toEqual([{ t: '2026-10-30', cpu: 33 }]);
  });
});

describe('fetchNodes：request 注入 / projectId 透传 / 失败 error', () => {
  interface Call { url: string; method?: string; params?: Record<string, unknown> }
  function stub(handler: (c: Call) => unknown): { req: Req; calls: Call[] } {
    const calls: Call[] = [];
    const req: Req = async (c) => {
      calls.push(c);
      return handler(c) as { data?: unknown };
    };
    return { req, calls };
  }

  it('三路并行取数并合并，projectId 非空时随每个请求透传', async () => {
    const { req, calls } = stub((c) => {
      if (c.url === '/Nodes/Node') return { data: NODES };
      if (c.url === '/Nodes/NodeOnline') return { data: ONLINE };
      return { data: APPS }; // /Registry/AppOnline
    });
    const { rows, error } = await fetchNodes(req, 42);
    expect(error).toBe('');
    expect(rows).toHaveLength(3);
    // 三个请求都带了 projectId=42
    expect(calls).toHaveLength(3);
    for (const c of calls) expect(c.params?.projectId).toBe(42);
    expect(calls[0].url).toBe('/Nodes/Node');
  });

  it('projectId 为 null/缺省时不发该参数', async () => {
    const { req, calls } = stub(() => ({ data: [] }));
    await fetchNodes(req, null);
    for (const c of calls) expect(c.params?.projectId).toBeUndefined();
  });

  it('请求失败 → 返回 error 与空行，不静默', async () => {
    const bad: Req = async () => { throw new Error('Request failed with status 500'); };
    const { rows, error } = await fetchNodes(bad);
    expect(rows).toEqual([]);
    expect(error).toMatch(/500/);
  });

  it('返回非数组（被包裹）→ 按空数组处理不崩', async () => {
    const { req } = stub(() => ({ data: { weird: 1 } }));
    const { rows, error } = await fetchNodes(req, 1);
    expect(error).toBe('');
    expect(rows).toEqual([]);
  });

  it('fetchNodeApps：按 nodeId 拉应用列表', async () => {
    const { req, calls } = stub((c) => ({ data: c.params?.nodeId === 1 ? APPS.slice(0, 2) : [] }));
    const { rows, error } = await fetchNodeApps(req, 1, 7);
    expect(error).toBe('');
    expect(rows).toHaveLength(2);
    expect(calls[0].params?.nodeId).toBe(1);
    expect(calls[0].params?.projectId).toBe(7);
  });

  it('类型自检：NodeRowView 形状（防止字段漂移）', () => {
    const rows = mergeNodes(NODES, ONLINE, APPS, NOW);
    const w = rows.find((r) => r.id === 1) as NodeRowView;
    expect(Object.keys(w).sort()).toEqual([
      'agentVer', 'cluster', 'cpu', 'framework', 'id', 'ip', 'lastActive', 'machineName',
      'mem', 'name', 'os', 'ping', 'raw', 'runtime', 'services', 'tone', 'code',
    ].sort());
  });
});
