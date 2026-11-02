// 调用链页签纯逻辑单测：采样片段归一、按 TraceId 聚合列表、瀑布条时间轴、取数失败兜底。
// 后端响应大小写两种方言都要认（TraceController 旧 MVC 直出 PascalCase，api-core 新通道 camelCase）。
import { describe, expect, it } from 'vitest';
import {
  buildTraceList,
  buildWaterfall,
  fetchTraceSpansMulti,
  fetchTraceSummaries,
  fetchTraceSpans,
  parseTraceIds,
  shortId,
  summarizeTrace,
  toSpans,
} from './trace.logic.ts';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

/** 造一个假 request：按 url 返回预置响应，记录调用参数 */
function fakeRequest(handler: (url: string, params: Record<string, unknown>) => unknown): Req {
  return async ({ url, params }) => {
    const data = handler(url, params ?? {});
    return { data };
  };
}

describe('toSpans：采样片段归一（大小写两种方言）', () => {
  it('camelCase 与 PascalCase 字段都认，缺 EndTime 时用 StartTime+Cost 兜底', () => {
    const spans = toSpans([
      {
        id: 11,
        traceId: 't1',
        spanId: 's1',
        parentId: '',
        name: 'GET /api/order',
        appName: 'OrderService',
        success: true,
        startTime: 1000,
        endTime: 1400,
        cost: 400,
      },
      {
        Id: 12,
        TraceId: 't1',
        SpanId: 's2',
        ParentId: 's1',
        Name: 'mysql.query',
        AppName: 'mysql',
        Success: false,
        StartTime: 1100,
        EndTime: 1350,
        Cost: 250,
        Error: 'deadlock',
      },
    ]);
    expect(spans).toHaveLength(2);
    expect(spans[0]).toMatchObject({ id: 11, traceId: 't1', spanId: 's1', name: 'GET /api/order', success: true, startTime: 1000, endTime: 1400, cost: 400 });
    expect(spans[1]).toMatchObject({ id: 12, spanId: 's2', parentId: 's1', name: 'mysql.query', success: false, error: 'deadlock' });
  });
});

describe('buildTraceList：按 TraceId 聚合成调用链列表', () => {
  const spans = [
    // 链 t1：根 span s1（parentId 指向组外），一个失败子片段
    { id: 1, traceId: 't1', spanId: 's1', parentId: '', name: 'POST /a', appName: 'A', success: true, startTime: 1000, endTime: 1500, cost: 500, tag: '', error: '', clientId: '' },
    { id: 2, traceId: 't1', spanId: 's2', parentId: 's1', name: 'db.q', appName: 'db', success: false, startTime: 1100, endTime: 1400, cost: 300, tag: '', error: 'e', clientId: '' },
    // 链 t2：更早、全成功
    { id: 3, traceId: 't2', spanId: 's3', parentId: 'x', name: 'GET /b', appName: 'B', success: true, startTime: 500, endTime: 600, cost: 100, tag: '', error: '', clientId: '' },
    // 没有 TraceId 的脏数据不进列表
    { id: 4, traceId: '', spanId: 's4', parentId: '', name: 'orphan', appName: '', success: true, startTime: 0, endTime: 0, cost: 0, tag: '', error: '', clientId: '' },
  ];

  it('按 TraceId 分组，根片段取名称，整段墙钟耗时=maxEnd-minStart', () => {
    const list = buildTraceList(spans);
    expect(list).toHaveLength(2);
    // t1 更晚在前；任一片段失败则整链失败
    expect(list[0]).toMatchObject({ traceId: 't1', name: 'POST /a', ok: false, cost: 500, spanCount: 2 });
    expect(list[1]).toMatchObject({ traceId: 't2', name: 'GET /b', ok: true, cost: 100, spanCount: 1 });
  });

  it('limit 截断', () => {
    expect(buildTraceList(spans, 1)).toHaveLength(1);
  });
});

describe('buildWaterfall：瀑布条时间轴', () => {
  const spans = [
    { id: 1, traceId: 't', spanId: 'r', parentId: '', name: 'root', appName: 'A', success: true, startTime: 1000, endTime: 1500, cost: 500, tag: '', error: '', clientId: '' },
    { id: 2, traceId: 't', spanId: 'c', parentId: 'r', name: 'child', appName: 'db', success: false, startTime: 1200, endTime: 1400, cost: 200, tag: '', error: 'e', clientId: '' },
  ];

  it('按开始时间排序，偏移与宽度落在 0-100，深度沿 ParentId 链', () => {
    const bars = buildWaterfall(spans);
    expect(bars).toHaveLength(2);
    expect(bars[0].key).toBe('r');
    expect(bars[0].depth).toBe(0);
    expect(bars[1].depth).toBe(1);
    expect(bars[0].startOffset).toBe(0);
    expect(bars[0].widthPct).toBeCloseTo(100, 1);
    expect(bars[1].startOffset).toBeCloseTo(40, 1);
    expect(bars[1].ok).toBe(false);
    expect(bars[1].error).toBe('e');
  });

  it('空片段返回空数组', () => {
    expect(buildWaterfall([])).toEqual([]);
  });
});

describe('summarizeTrace：诊断框（最耗时片段）', () => {
  it('找出最耗时片段及其占比', () => {
    const diag = summarizeTrace([
      { id: 1, traceId: 't', spanId: 'r', parentId: '', name: 'root', appName: 'A', success: true, startTime: 0, endTime: 200, cost: 200, tag: '', error: '', clientId: '' },
      { id: 2, traceId: 't', spanId: 'c', parentId: 'r', name: 'slow-db', appName: 'db', success: true, startTime: 100, endTime: 800, cost: 700, tag: '', error: '', clientId: '' },
    ]);
    expect(diag).toMatchObject({ spanCount: 2, errorCount: 0, cost: 800, slowestName: 'slow-db', slowestCost: 700 });
    expect(diag.slowestPct).toBeCloseTo(88, 1);
  });
});

describe('shortId', () => {
  it('超过 16 位截断加省略号，短串原样返回', () => {
    expect(shortId('ac15452e0123456789abcd')).toBe('ac15452e01234567…');
    expect(shortId('ab12')).toBe('ab12');
  });
});

describe('fetchTraceSummaries / fetchTraceSpans：取数（request 注入）', () => {
  it('按 appId 取采样片段并聚合成列表，携带 time 参数（分表下钻必传）', async () => {
    const req = fakeRequest((url, params) => {
      expect(url).toBe('/Monitors/SampleData');
      expect(params.appId).toBe(32);
      expect(String(params.time)).toMatch(/^\d{4}-\d{2}-\d{2}$/);
      return [
        { traceId: 't1', spanId: 's1', name: 'GET /x', success: true, startTime: 1000, endTime: 1200, cost: 200 },
        { traceId: 't1', spanId: 's2', parentId: 's1', name: 'db', success: true, startTime: 1050, endTime: 1150, cost: 100 },
      ];
    });
    const list = await fetchTraceSummaries(req, 32);
    expect(list).toHaveLength(1);
    expect(list?.[0]).toMatchObject({ traceId: 't1', name: 'GET /x', spanCount: 2 });
  });

  it('按 traceId 取整段链路片段', async () => {
    const req = fakeRequest((url, params) => {
      expect(url).toBe('/Monitors/SampleData');
      expect(params.traceId).toBe('t9');
      return [{ traceId: 't9', spanId: 's9', name: 'x', success: true, startTime: 1, endTime: 2, cost: 1 }];
    });
    const spans = await fetchTraceSpans(req, 't9');
    expect(spans).toHaveLength(1);
    expect(spans?.[0].spanId).toBe('s9');
  });

  it('请求失败返回 null，由组件显示加载失败', async () => {
    const bad: Req = async () => {
      throw new Error('network');
    };
    expect(await fetchTraceSummaries(bad, 32)).toBeNull();
    expect(await fetchTraceSpans(bad, 't9')).toBeNull();
  });

  it('appId 无效时不取数，返回空列表', async () => {
    let called = 0;
    const req = fakeRequest(() => {
      called++;
      return [];
    });
    expect(await fetchTraceSummaries(req, 0)).toEqual([]);
    expect(called).toBe(0);
  });
});

describe('F14 parseTraceIds：多值输入拆分（逗号/分号/空白，去空去重，上限 5）', () => {
  it('逗号+空格混合拆分并去重保序', () => {
    expect(parseTraceIds('405, 369299,,405;77')).toEqual(['405', '369299', '77']);
  });

  it('换行/制表符也算分隔；超 5 个截断', () => {
    expect(parseTraceIds('a\nb\tc')).toEqual(['a', 'b', 'c']);
    expect(parseTraceIds('1,2,3,4,5,6,7,8')).toHaveLength(5);
  });

  it('空串/纯分隔符返回空数组', () => {
    expect(parseTraceIds('')).toEqual([]);
    expect(parseTraceIds(' , ; ')).toEqual([]);
  });
});

describe('F14 fetchTraceSpansMulti：多值并行拉取（单条失败不拖垮其它）', () => {
  it('每个 traceId 独立请求，Map 按 id 收结果', async () => {
    const seen: string[] = [];
    const req = fakeRequest((url, params) => {
      expect(url).toBe('/Monitors/SampleData');
      seen.push(String(params.traceId));
      if (params.traceId === 'bad') throw new Error('net');
      return [{ id: 1, traceId: params.traceId, spanId: 's1', parentId: '', name: 'op', startTime: 1000, endTime: 1500, success: true }];
    });
    const m = await fetchTraceSpansMulti(req, ['t1', 'bad', 't2']);
    expect(seen.sort()).toEqual(['bad', 't1', 't2']);
    expect(m.get('t1')).not.toBeNull();
    expect(m.get('bad')).toBeNull();
    expect(m.get('t2')?.[0].traceId).toBe('t2');
  });

  it('空 ids 返回空 Map（不发请求）', async () => {
    let calls = 0;
    const req = fakeRequest(() => {
      calls++;
      return [];
    });
    expect((await fetchTraceSpansMulti(req, [])).size).toBe(0);
    expect(calls).toBe(0);
  });
});
