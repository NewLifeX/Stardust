// 实例页签纯逻辑单测（Vitest）。2026-10-29：子代理 B 配置/实例两页签，先红后绿。
import { describe, it, expect } from 'vitest';
import { fetchInstances, toInstances } from './appInstances.logic';
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
