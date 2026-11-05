// traceGraph.logic 单元测试（Vitest；request 由桩注入，手算数据，不造演示数据进生产）
import { describe, expect, it, vi } from 'vitest';
import {
  buildGraphOption,
  fetchGraphData,
  fetchSampleRows,
  parseSampleRowsToGraph,
  resolveGraphData,
  toGraphData,
} from './traceGraph.logic.ts';
import type { Req, ReqOptions } from '../views/Stardust/appsModel.ts';

type Handler = (opts: ReqOptions) => unknown;
function reqOf(handler: Handler): Req {
  return (async (opts: ReqOptions) => handler(opts)) as unknown as Req;
}
function spyReq(handler: Handler) {
  const spy = vi.fn(handler);
  return { req: (async (opts: ReqOptions) => spy(opts)) as unknown as Req, spy };
}

// 服务端 GraphViewModel（JsonResult PascalCase）
const API_PASCAL = {
  TraceId: '405',
  Title: 'order-service关系图',
  Layout: 'force',
  Categories: [{ Name: 'App' }, { Name: 'db' }],
  Nodes: [
    { Id: '11', Name: 'order-service', Value: 500, Category: 0, SymbolSize: 100 },
    { Id: '12', Name: 'order_db', Value: 300, Category: 1, SymbolSize: 60 },
  ],
  Links: [{ Source: '11', Target: '12' }],
};

// SampleData 回退行（resolveGraphData 回退路径共用；parseSampleRowsToGraph 的用例另带局部 rows）
const sampleRows = [
  { id: 1, appId: 7, appName: 'order-service', spanId: 's1', parentId: '', cost: 500, name: 'GET /order' },
  { id: 2, appId: 7, appName: 'order-service', spanId: 's2', parentId: 's1', cost: 300, name: 'db', kind: 'db', displayName: 'mysql:order_db' },
];

describe('toGraphData（API 响应归一）', () => {
  it('PascalCase 响应归一：nodes/links/categories/layout/traceId/title', () => {
    const g = toGraphData(API_PASCAL);
    expect(g).not.toBeNull();
    expect(g!.traceId).toBe('405');
    expect(g!.title).toBe('order-service关系图');
    expect(g!.layout).toBe('force');
    expect(g!.nodes).toHaveLength(2);
    expect(g!.nodes[0]).toMatchObject({ id: '11', name: 'order-service', value: 500, category: 0, symbolSize: 100 });
    expect(g!.links).toEqual([{ source: '11', target: '12' }]);
    expect(g!.categories.map((c) => c.name)).toEqual(['App', 'db']);
    expect(g!.source).toBe('api');
  });

  it('camelCase 响应归一；layout circular/none 认可、未知值退化 force；x/y 透传', () => {
    const g = toGraphData({
      traceId: 't1',
      title: '',
      layout: 'circular',
      categories: [{ name: 'App' }],
      nodes: [{ id: '1', name: 'a', value: 1, category: 0, symbolSize: 40, x: 10, y: 20 }],
      links: [],
    });
    expect(g!.layout).toBe('circular');
    expect(g!.nodes[0].x).toBe(10);
    expect(g!.nodes[0].y).toBe(20);
    expect(g!.title).toBe('关系图');
    expect(toGraphData({ layout: 'weird', nodes: [{ id: '1', name: 'a', value: 0, category: 0, symbolSize: 0 }], links: [] })!.layout).toBe('force');
  });

  it('nodes 空数组/缺失/非对象 → null（视为无数据，触发回退）', () => {
    expect(toGraphData({ nodes: [], links: [], categories: [] })).toBeNull();
    expect(toGraphData({})).toBeNull();
    expect(toGraphData(null)).toBeNull();
    expect(toGraphData('x')).toBeNull();
  });
});

describe('fetchGraphData / fetchSampleRows（请求契约）', () => {
  it('fetchGraphData：GET /trace/graph，params={id, layout}，返回归一数据', async () => {
    const { req, spy } = spyReq(() => ({ data: API_PASCAL }));
    const g = await fetchGraphData(req, '405', 'circular');
    expect(spy).toHaveBeenCalledTimes(1);
    expect(spy.mock.calls[0][0]).toMatchObject({ url: '/trace/graph', method: 'GET', params: { id: '405', layout: 'circular' } });
    expect(g!.nodes).toHaveLength(2);
  });

  it('fetchGraphData：request 抛错 → 向上抛（由 resolveGraphData 统一回退）', async () => {
    const req = reqOf(() => {
      throw new Error('boom');
    });
    await expect(fetchGraphData(req, '405')).rejects.toThrow('boom');
  });

  it('fetchSampleRows：GET /Monitors/SampleData，params={traceId, pageSize:10000}；data 数组与裸数组都认', async () => {
    const rows = [{ id: 1, appName: 'a' }];
    const r1 = await fetchSampleRows(reqOf(() => ({ data: rows })), '405');
    expect(r1).toEqual(rows);
    const r2 = await fetchSampleRows(reqOf(() => rows), '405');
    expect(r2).toEqual(rows);
  });
});

describe('parseSampleRowsToGraph（SampleData 自解析，平移自原 TraceGraph.vue）', () => {
  const rows = [
    { id: 1, appId: 7, appName: 'order-service', spanId: 's1', parentId: '', cost: 500, name: 'GET /order' },
    { id: 2, appId: 7, appName: 'order-service', spanId: 's2', parentId: 's1', cost: 300, name: 'db', kind: 'db', displayName: 'mysql:order_db' },
  ];

  it('应用首个节点归 App 类（节点名=AppName）；后续按 Kind 分类（db 取 ns[1]）', () => {
    const g = parseSampleRowsToGraph(rows);
    expect(g.categories.map((c) => c.name)).toEqual(['App', 'db']);
    expect(g.nodes[0]).toMatchObject({ id: '1', name: 'order-service', category: 0, value: 500 });
    expect(g.nodes[1]).toMatchObject({ id: '2', name: 'order_db', category: 1, value: 300 });
  });

  it('边=ParentId→SpanId；尺寸归一对齐服务端（max→100、min→40，len<=0 时 len=1）', () => {
    const g = parseSampleRowsToGraph(rows);
    expect(g.links).toEqual([{ source: '1', target: '2' }]);
    expect(g.nodes[0].symbolSize).toBe(100);
    expect(g.nodes[1].symbolSize).toBe(40);
  });

  it('同名节点聚合（value/symbolSize 累加）；自环与重复边不入图', () => {
    const g = parseSampleRowsToGraph([
      { id: 1, appId: 7, appName: 'a', spanId: 's1', parentId: 's1', cost: 100, name: 'x' },
      { id: 2, appId: 7, appName: 'a', spanId: 's2', parentId: 's1', cost: 100, name: 'x' },
    ]);
    expect(g.nodes).toHaveLength(1);
    expect(g.nodes[0].value).toBe(200);
    expect(g.links).toHaveLength(0); // s1 自环被剔除
  });

  it('无 TraceItem.Kind 降级：AppName 为空时节点名退化为操作名，类目仍为 App', () => {
    const g = parseSampleRowsToGraph([{ id: 1, appId: 7, appName: '', spanId: 's1', parentId: '', cost: 50, name: 'GET /x' }]);
    expect(g.nodes[0].name).toBe('GET /x');
    expect(g.categories).toEqual([{ name: 'App' }]);
  });
});

describe('buildGraphOption（layout 参数化）', () => {
  it('series.layout 透传；legend/series.data/links/edgeSymbol 就位', () => {
    const g = toGraphData(API_PASCAL)!;
    for (const layout of ['force', 'circular', 'none'] as const) {
      const opt = buildGraphOption({ ...g, layout });
      const series = (opt.series as Array<Record<string, unknown>>)[0];
      expect(series.layout).toBe(layout);
      expect(series.type).toBe('graph');
      expect(series.data).toEqual(g.nodes);
      expect(series.links).toEqual(g.links);
      expect((opt.legend as Array<{ data: string[] }>)[0].data).toEqual(['App', 'db']);
      expect(series.edgeSymbol).toEqual(['circle', 'arrow']);
    }
  });
});

describe('resolveGraphData（API 优先、SampleData 自解析回退）', () => {
  it('API 成功 → ok/source=api，回退取数不被调用', async () => {
    const fallback = vi.fn(async () => [] as Array<Record<string, unknown>>);
    const res = await resolveGraphData(reqOf(() => ({ data: API_PASCAL })), '405', {
      fetchSamples: fallback as never,
    });
    expect(res.status).toBe('ok');
    if (res.status === 'ok') {
      expect(res.data.source).toBe('api');
      expect(res.data.nodes).toHaveLength(2);
    }
    expect(fallback).not.toHaveBeenCalled();
  });

  it('API 无数据（nodes 空）→ 回退 SampleData 自解析 → ok/source=local', async () => {
    const res = await resolveGraphData(reqOf(() => ({ data: { nodes: [], links: [], categories: [] } })), '405', {
      fetchSamples: async () => sampleRows as never,
    });
    expect(res.status).toBe('ok');
    if (res.status === 'ok') {
      expect(res.data.source).toBe('local');
      expect(res.data.nodes.map((n) => n.name)).toEqual(['order-service', 'order_db']);
    }
  });

  it('API 抛错 + 回退成功 → ok/source=local', async () => {
    const res = await resolveGraphData(
      reqOf(() => {
        throw new Error('api down');
      }),
      '405',
      { fetchSamples: async () => sampleRows as never },
    );
    expect(res.status).toBe('ok');
    if (res.status === 'ok') expect(res.data.source).toBe('local');
  });

  it('两路都失败 → error（取回退报错文案）；两路都无数据 → empty；空 traceId → empty 不发请求', async () => {
    const err = await resolveGraphData(
      reqOf(() => {
        throw new Error('api down');
      }),
      '405',
      {
        fetchSamples: async () => {
          throw new Error('sample down');
        },
      },
    );
    expect(err).toEqual({ status: 'error', message: 'sample down' });

    const empty = await resolveGraphData(reqOf(() => ({ data: { nodes: [] } })), '405', {
      fetchSamples: async () => [],
    });
    expect(empty).toEqual({ status: 'empty' });

    const spy = vi.fn();
    const noCall = await resolveGraphData(reqOf(spy as never), '', { fetchSamples: spy as never });
    expect(noCall).toEqual({ status: 'empty' });
    expect(spy).not.toHaveBeenCalled();
  });
});
