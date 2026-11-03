// 实例页签纯逻辑单测（Vitest）。2026-10-29：子代理 B 配置/实例两页签，先红后绿。
// 2026-11-03：+F04 Snapshot 快照（cpu/memory 真实值）+F03 Execute/FreeMemory 批量命令。
import { describe, it, expect } from 'vitest';
import {
  cpuText,
  executeCommand,
  fetchInstances,
  fetchSnapshots,
  freeMemory,
  isDangerousCommand,
  mergeSnapshots,
  toInstances,
  toSnapshot,
} from './appInstances.logic';
import type { InstanceRow } from './appInstances.logic';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

function stubReq(queue: Array<{ res?: unknown; err?: unknown }>): { req: Req; calls: unknown[] } {
  const calls: unknown[] = [];
  const req: Req = async (opt) => {
    calls.push(opt);
    const next = queue.shift();
    if (!next) throw new Error('stub: 多余的请求');
    if (next.err) throw next.err instanceof Error ? next.err : new Error(String(next.err));
    return (next.res ?? { data: [] }) as Awaited<ReturnType<Req>>;
  };
  return { req, calls };
}

describe('toInstances · 行映射', () => {
  it('大小写字段都认，取节点名/IP/版本/长连接', () => {
    const rows = toInstances([
      { Id: 1, NodeId: 7, NodeName: 'node-a', IP: '10.0.0.1', Client: '10.0.0.1@1234', Version: '1.2.0', LongLink: true },
      { id: 2, nodeId: 8, nodeName: 'node-b', ip: '10.0.0.2', client: '10.0.0.2@5678', version: '1.1.0', longLink: false },
    ]);
    expect(rows).toEqual([
      { id: 1, nodeId: 7, node: 'node-a', ip: '10.0.0.1', client: '10.0.0.1@1234', version: '1.2.0', longLink: true },
      { id: 2, nodeId: 8, node: 'node-b', ip: '10.0.0.2', client: '10.0.0.2@5678', version: '1.1.0', longLink: false },
    ]);
  });

  it('空行兜底', () => {
    expect(toInstances([])).toEqual([]);
  });
});

describe('fetchInstances · 取在线实例', () => {
  it('按 appId 请求并映射行', async () => {
    const { req, calls } = stubReq([{ res: { data: [{ Id: 1, NodeName: 'node-a', IP: '10.0.0.1', Version: '1.2.0' }] } }]);
    const rows = await fetchInstances(req, 32);
    expect(rows).toHaveLength(1);
    expect(rows?.[0]).toMatchObject({ node: 'node-a', ip: '10.0.0.1', version: '1.2.0' });
    expect(calls[0]).toMatchObject({ url: '/Registry/AppOnline', method: 'GET' });
  });

  it('appId 非法返回空数组，不发请求', async () => {
    const { req, calls } = stubReq([]);
    expect(await fetchInstances(req, 0)).toEqual([]);
    expect(calls).toHaveLength(0);
  });

  it('请求失败返回 null（页面显示加载失败，不造空表）', async () => {
    const { req } = stubReq([{ err: new Error('500') }]);
    expect(await fetchInstances(req, 32)).toBeNull();
  });
});

// ==================== F04 实例快照（Snapshot） ====================

describe('F04 toSnapshot · camelCase 归一', () => {
  it('完整字段；无性能数据的 cpu/memory 为 null（不造 0）', () => {
    const s = toSnapshot({
      id: 11, clientId: '10.0.0.1@1234', nodeId: 7, name: 'Star.Web', ip: '10.0.0.1', online: true,
      cpu: 12.34, memory: 104857600, availableMemory: 8589934592, threads: 32, connections: 5, updateTime: '2026-11-03T10:00:00',
    });
    expect(s).toEqual({
      id: 11, clientId: '10.0.0.1@1234', nodeId: 7, name: 'Star.Web', ip: '10.0.0.1', online: true,
      cpu: 12.34, memory: 104857600, availableMemory: 8589934592, threads: 32, connections: 5, updateTime: '2026-11-03T10:00:00',
    });
    const empty = toSnapshot({ id: 12, clientId: 'x', nodeId: 0, name: '', ip: '', online: false });
    expect(empty.cpu).toBeNull();
    expect(empty.memory).toBeNull();
    expect(empty.availableMemory).toBeNull();
    expect(empty.threads).toBeNull();
    expect(empty.connections).toBeNull();
    expect(empty.updateTime).toBeNull();
    expect(empty.online).toBe(false);
  });
});

describe('F04 fetchSnapshots / mergeSnapshots / cpuText', () => {
  it('按 appId 拉 Snapshot 并以 id 建映射', async () => {
    const { req, calls } = stubReq([{ res: { code: 0, data: [{ id: 11, clientId: 'c1', online: true, cpu: 3.5, memory: 2048, threads: 4, connections: 2, updateTime: '2026-11-03T10:00:00' }] } }]);
    const m = await fetchSnapshots(req, 32);
    expect(m?.get(11)?.cpu).toBe(3.5);
    expect(calls[0]).toMatchObject({ url: '/Registry/AppOnline/Snapshot', method: 'GET', params: { appId: 32 } });
  });

  it('appId 非法返回空映射不发请求；失败返回 null', async () => {
    const { req, calls } = stubReq([]);
    expect(await fetchSnapshots(req, 0)).toEqual(new Map());
    expect(calls).toHaveLength(0);
    const bad = stubReq([{ err: new Error('x') }]);
    expect(await fetchSnapshots(bad.req, 32)).toBeNull();
  });

  it('mergeSnapshots：快照覆盖 online/性能字段，快照失败回退列表行（性能 null）', () => {
    const rows: InstanceRow[] = [{ id: 11, nodeId: 7, node: 'node-a', ip: '10.0.0.1', client: 'c1', version: '1.0', longLink: true }];
    const snaps = new Map([[11, toSnapshot({ id: 11, online: false, cpu: 55.55, memory: 4096, availableMemory: 8192, threads: 9, connections: 3, updateTime: '2026-11-03T10:00:00' })]]);
    const merged = mergeSnapshots(rows, snaps);
    // Snapshot 的 memory/availableMemory 单位为 MB，merge 转成字节（4096MB→4GiB）
    expect(merged[0]).toMatchObject({ online: false, cpu: 55.55, memory: 4096 * 1024 * 1024, updateTimeText: '2026-11-03 10:00' });
    const merged2 = mergeSnapshots(rows, null);
    expect(merged2[0]).toMatchObject({ online: true, cpu: null, memory: null, updateTimeText: '' });
  });

  it('内存单位：MB 转字节后 formatBytes 显示 189 MB / 5.0 GB', async () => {
    const { formatBytes } = await import('./appObserve.logic');
    const rows: InstanceRow[] = [{ id: 21, nodeId: 7, node: 'n', ip: '10.0.0.9', client: 'c', version: '', longLink: false }];
    // memory=189（MB）、availableMemory=5119（MB≈5GB）；若误当字节会显示成「189 B」
    const snaps = new Map([[21, toSnapshot({ id: 21, online: true, cpu: 3, memory: 189, availableMemory: 5119, threads: 8, connections: 1, updateTime: '2026-11-03T10:00:00' })]]);
    const merged = mergeSnapshots(rows, snaps)[0];
    expect(formatBytes(merged.memory as number)).toBe('189 MB');
    expect(formatBytes(merged.availableMemory as number)).toBe('5.0 GB');
  });

  it('cpuText：null 空串，数字一位小数', () => {
    expect(cpuText(null)).toBe('');
    expect(cpuText(12.34)).toBe('12.3%');
  });
});

// ==================== F03 批量命令（Execute / FreeMemory） ====================

describe('F03 isDangerousCommand', () => {
  it('清单命中（不分大小写、含子串），普通命令与空串不命中', () => {
    expect(isDangerousCommand('app/restart')).toBe(true);
    expect(isDangerousCommand('Shutdown')).toBe(true);
    expect(isDangerousCommand('rm -rf')).toBe(true);
    expect(isDangerousCommand('app/info')).toBe(false);
    expect(isDangerousCommand('')).toBe(false);
    expect(isDangerousCommand('  ')).toBe(false);
  });
});

describe('F03 executeCommand / freeMemory', () => {
  it('executeCommand：GET 带 command/argument/keys 逗号串，透传 JsonRefresh 汇总', async () => {
    const { req, calls } = stubReq([{ res: { code: 0, message: '操作成功！下发2个，响应1个，超时1个\nnode-a: pong' } }]);
    const r = await executeCommand(req, [11, 12], 'app/ping', '-v');
    expect(r).toMatchObject({ ok: true, message: '操作成功！下发2个，响应1个，超时1个\nnode-a: pong' });
    expect(calls[0]).toMatchObject({
      url: '/Registry/AppOnline/Execute',
      method: 'GET',
      params: { command: 'app/ping', argument: '-v', keys: '11,12' },
    });
  });

  it('executeCommand：响应0个（全超时）仍 ok=true，文案如实', async () => {
    const { req } = stubReq([{ res: { code: 0, message: '操作成功！下发1个，响应0个，超时1个' } }]);
    const r = await executeCommand(req, [11], 'app/ping', '');
    expect(r.ok).toBe(true);
    expect(r.message).toContain('超时1个');
  });

  it('executeCommand：空 keys / 空命令不发请求', async () => {
    const { req, calls } = stubReq([]);
    expect((await executeCommand(req, [], 'app/ping', '')).ok).toBe(false);
    expect((await executeCommand(req, [11], '  ', '')).ok).toBe(false);
    expect(calls).toHaveLength(0);
  });

  it('freeMemory：GET /Registry/AppOnline/FreeMemory 只带 keys', async () => {
    const { req, calls } = stubReq([{ res: { code: 0, message: '操作成功！下发1个，响应1个，超时0个\nnode-a: freed 10MB' } }]);
    const r = await freeMemory(req, [11]);
    expect(r.ok).toBe(true);
    expect(calls[0]).toMatchObject({ url: '/Registry/AppOnline/FreeMemory', method: 'GET', params: { keys: '11' } });
  });

  it('freeMemory：空 keys 不发请求；失败透传', async () => {
    const { req, calls } = stubReq([{ res: { code: 500, message: '无权限' } }]);
    expect((await freeMemory(req, [])).ok).toBe(false);
    expect((await freeMemory(req, [11])).message).toBe('无权限');
    expect(calls).toHaveLength(1);
  });
});
