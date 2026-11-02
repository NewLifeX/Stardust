// paletteRuntime 缓存运行时单测（Vitest）：request 用桩函数注入。
import { describe, it, expect } from 'vitest';
import { createPaletteRuntime } from './paletteRuntime.logic';

type Req = Parameters<typeof createPaletteRuntime>[0];

/** 按 url 返回预设数据；值为 Error 时模拟请求失败；calls 记录请求过的 url */
function stub(responses: Record<string, unknown>, calls: string[] = []): Req {
  return ({ url }) => {
    calls.push(url);
    const r = responses[url];
    return r instanceof Error ? Promise.reject(r) : Promise.resolve({ data: r });
  };
}

describe('createPaletteRuntime · 缓存命中', () => {
  it('首次调用发请求，第二次命中缓存不再发', async () => {
    const calls: string[] = [];
    const runtime = createPaletteRuntime(stub({ '/Registry/App': [], '/Registry/AppOnline': [], '/Nodes/Node': [] }, calls));

    await runtime.ensure();
    expect(calls.length).toBe(3); // 首次发三个请求

    await runtime.ensure();
    expect(calls.length).toBe(3); // 第二次不发新请求
  });

  it('peek 返回当前缓存', async () => {
    const runtime = createPaletteRuntime(stub({ '/Registry/App': [], '/Registry/AppOnline': [], '/Nodes/Node': [] }));
    expect(runtime.peek()).toBeNull();
    await runtime.ensure();
    expect(runtime.peek()).not.toBeNull();
  });
});

describe('createPaletteRuntime · 并发去重', () => {
  it('同时调用两次 ensure 只发一次请求', async () => {
    const calls: string[] = [];
    const runtime = createPaletteRuntime(stub({ '/Registry/App': [], '/Registry/AppOnline': [], '/Nodes/Node': [] }, calls));

    // 同时发起两个 ensure，不等第一个完成
    const p1 = runtime.ensure();
    const p2 = runtime.ensure();

    await Promise.all([p1, p2]);
    // 每个接口只请求一次
    const appCalls = calls.filter((c) => c === '/Registry/App').length;
    expect(appCalls).toBe(1);
  });
});

describe('createPaletteRuntime · 部分失败不缓存', () => {
  it('有 failed 时不视为缓存命中，下次重新请求', async () => {
    // 让 /Registry/AppOnline 失败 → apps 和 nodes 都标记 failed
    const calls: string[] = [];
    const runtime = createPaletteRuntime(
      stub(
        {
          '/Registry/App': [{ id: 1, name: 'A', enable: true }],
          '/Registry/AppOnline': new Error('network'),
          '/Nodes/Node': [{ id: 1, name: 'n1' }],
        },
        calls,
      ),
    );

    await runtime.ensure();
    expect(calls.length).toBe(3); // 首次请求

    await runtime.ensure();
    expect(calls.length).toBe(6); // 部分失败 → 第二次重新请求
  });
});

describe('createPaletteRuntime · 上下文失效', () => {
  it('invalidate 后缓存清空，下次 ensure 重新请求', async () => {
    const calls: string[] = [];
    const runtime = createPaletteRuntime(stub({ '/Registry/App': [], '/Registry/AppOnline': [], '/Nodes/Node': [] }, calls));

    await runtime.ensure();
    expect(calls.length).toBe(3);

    runtime.invalidate();
    expect(runtime.peek()).toBeNull();

    await runtime.ensure();
    expect(calls.length).toBe(6); // 失效后重新请求
  });

  it('invalidate 后在途旧请求结果不写入缓存', async () => {
    // fetchPaletteData 内部并行发三个请求；这里用收集器收集所有 resolve
    const resolvers: Array<() => void> = [];
    const slowReq: Req = () =>
      new Promise((resolve) => {
        resolvers.push(() => resolve({ data: [] }));
      });

    const runtime = createPaletteRuntime(slowReq);

    // 发起请求（不等待）
    const p = runtime.ensure();
    // 请求进行中时失效
    runtime.invalidate();
    // 旧请求完成（resolve 所有在途调用）
    resolvers.forEach((r) => r());
    await p;

    // 缓存仍为空（旧结果被丢弃）
    expect(runtime.peek()).toBeNull();
  });

  it('连续 invalidate 多次不报错', () => {
    const runtime = createPaletteRuntime(stub({}));
    runtime.invalidate();
    runtime.invalidate();
    runtime.invalidate();
    expect(runtime.peek()).toBeNull();
  });
});

describe('createPaletteRuntime · fetchPaletteData 集成', () => {
  it('正常返回数据结构正确', async () => {
    const runtime = createPaletteRuntime(
      stub({
        '/Registry/App': [{ id: 1, name: 'OrderService', enable: true }],
        '/Registry/AppOnline': [{ appName: 'OrderService', nodeName: 'n1' }],
        '/Nodes/Node': [{ id: 1, name: 'n1', iP: '10.0.0.1' }],
      }),
    );

    const data = await runtime.ensure();
    expect(data.failed).toEqual([]);
    expect(data.apps).toHaveLength(1);
    expect(data.apps[0].name).toBe('OrderService');
    expect(data.nodes).toHaveLength(1);
  });
});
