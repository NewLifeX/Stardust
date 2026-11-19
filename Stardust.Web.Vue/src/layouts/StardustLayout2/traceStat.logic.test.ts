// traceStat.logic 单测（Vitest）——追踪三统计（CARD-MON-07 路径 B，TDD 先红）
// 覆盖：粒度定义与参数组装、埋点下拉、行映射+错误率分级、总计行前端聚合、Trace 解析成功/三失败文案
import { describe, it, expect } from 'vitest';
import {
  TRACE_GRANULARITIES,
  buildTraceStatParams,
  fetchTraceItems,
  fetchTraceStats,
  fmtRingRate,
  resolveTrace,
  traceGranularityOf,
  traceRateClass,
  traceStatChartOption,
  traceStatTotalRow,
  type ChartPalette,
  type TraceGranularity,
  type TraceItemOption,
} from './traceStat.logic';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

describe('粒度定义（Day=90/Hour=168/Minute=288，卡片裁定）', () => {
  it('三粒度 entity/PageSize/时间字段映射', () => {
    expect(traceGranularityOf('day')).toMatchObject({ entity: 'TraceDayStat', pageSize: 90, timeField: 'statDate', label: '每天' });
    expect(traceGranularityOf('hour')).toMatchObject({ entity: 'TraceHourStat', pageSize: 168, timeField: 'statTime', label: '每小时' });
    expect(traceGranularityOf('minute')).toMatchObject({ entity: 'TraceMinuteStat', pageSize: 288, timeField: 'statTime', label: '每5分钟' });
    expect(traceGranularityOf('week')).toBeUndefined();
    expect(TRACE_GRANULARITIES).toHaveLength(3);
  });
});

describe('fetchTraceItems · 埋点下拉', () => {
  it('GET /Monitors/TraceItem?appId=&pageSize=，映射 Id/Name/Kind', async () => {
    let seen: { url?: string; params?: Record<string, unknown> } = {};
    const request = (async (cfg: { url: string; params: Record<string, unknown> }) => {
      seen = cfg;
      return {
        data: [
          { ID: 11, Name: 'POST api/user/login', Kind: 'Api', Enable: true },
          { ID: 22, Name: 'mysql SELECT user', Kind: 'Db' },
        ],
      };
    }) as Req;
    const r = await fetchTraceItems(request, 4, 100);
    expect(seen.url).toBe('/Monitors/TraceItem');
    expect(seen.params?.appId).toBe(4);
    expect(seen.params?.pageSize).toBe(100);
    expect(r.error).toBe('');
    expect(r.options.map((o: TraceItemOption) => o.id)).toEqual([11, 22]);
    expect(r.options[0]).toMatchObject({ name: 'POST api/user/login', kind: 'Api', enable: true });
    expect(r.options[1].kind).toBe('Db');
  });

  it('请求失败返回 error 文案', async () => {
    const request = (async () => {
      throw new Error('网络错误');
    }) as Req;
    const r = await fetchTraceItems(request, 4);
    expect(r.options).toEqual([]);
    expect(r.error).toBe('网络错误');
  });
});

describe('buildTraceStatParams · 参数组装', () => {
  it('day：url=/Monitors/TraceDayStat，pageSize=90，type 透传，空区间剔除', () => {
    const p = buildTraceStatParams('day', { appId: 4, itemId: 11, type: 'Db' });
    expect(p.url).toBe('/Monitors/TraceDayStat');
    expect(p.params).toEqual({ appId: 4, itemId: 11, pageSize: 90, type: 'Db' });
  });

  it('hour/minute：TraceHourStat 168 / TraceMinuteStat 288', () => {
    expect(buildTraceStatParams('hour', { appId: 4, itemId: 11 }).url).toBe('/Monitors/TraceHourStat');
    expect(buildTraceStatParams('hour', { appId: 4, itemId: 11 }).params.pageSize).toBe(168);
    expect(buildTraceStatParams('minute', { appId: 4, itemId: 11 }).url).toBe('/Monitors/TraceMinuteStat');
    expect(buildTraceStatParams('minute', { appId: 4, itemId: 11 }).params.pageSize).toBe(288);
  });

  it('区间与 pageSize 覆盖传入', () => {
    const p = buildTraceStatParams('day', { appId: 4, itemId: 11, dtStart: '2026-11-01', dtEnd: '2026-11-08', pageSize: 30 });
    expect(p.params.dtStart).toBe('2026-11-01');
    expect(p.params.dtEnd).toBe('2026-11-08');
    expect(p.params.pageSize).toBe(30);
  });

  it('itemId<=0 不传（AppDayStat C17 列链：按天+种类查，不选埋点）', () => {
    const p = buildTraceStatParams('day', { appId: 4, itemId: 0, dtStart: '2026-11-07', dtEnd: '2026-11-07', type: 'Db' });
    expect(p.params).toEqual({ appId: 4, pageSize: 90, dtStart: '2026-11-07', dtEnd: '2026-11-07', type: 'Db' });
  });

  it('未知粒度抛错', () => {
    expect(() => buildTraceStatParams('week' as TraceGranularity, { appId: 4, itemId: 11 })).toThrow();
  });
});

describe('fetchTraceStats · 行映射与错误率分级', () => {
  it('day 行映射（StatDate/Type/Total/RingRate/ErrorRate/Cost…）+ rateClass', async () => {
    let seen: { url?: string } = {};
    const request = (async (cfg: { url: string }) => {
      seen = cfg;
      return {
        data: [
          { ID: 101, StatDate: '2026-11-08', Name: 'POST api/user/login', Type: 'Api', Total: 1378, RingRate: -0.2, Errors: 0, ErrorRate: 0, Cost: 55, MaxCost: 900, MinCost: 1, TotalValue: 0, TraceId: '' },
          { ID: 102, StatDate: '2026-11-08', Name: 'mysql SELECT user', Type: 'Db', Total: 100, Errors: 3, ErrorRate: 0.03, Cost: 120, MaxCost: 5000, MinCost: 2, TotalValue: 7 },
        ],
      };
    }) as Req;
    const r = await fetchTraceStats(request, 'day', { appId: 4, itemId: 11 });
    expect(seen.url).toBe('/Monitors/TraceDayStat');
    expect(r.error).toBe('');
    expect(r.rows[0]).toMatchObject({ id: 101, statTime: '2026-11-08', name: 'POST api/user/login', type: 'Api', total: 1378, ringRate: -0.2, errors: 0, errorRate: 0, cost: 55, maxCost: 900, minCost: 1 });
    expect(r.rows[0].rateClass).toBe('');
    expect(r.rows[1].rateClass).toBe('danger');
  });

  it('hour 行取 StatTime；0.005→warning、0.0005→无色', async () => {
    const request = (async () => ({
      data: [
        { ID: 201, StatTime: '2026-11-08 09:00:00', Name: 'http GET baidu', Type: 'Http', Total: 10, Errors: 1, ErrorRate: 0.1, Cost: 30, MaxCost: 90, MinCost: 5 },
      ],
    })) as Req;
    const r = await fetchTraceStats(request, 'hour', { appId: 4, itemId: 11 });
    expect(r.rows[0].statTime).toBe('2026-11-08 09:00:00');
    expect(r.rows[0].rateClass).toBe('danger');
    expect(traceRateClass(0.005)).toBe('warning');
    expect(traceRateClass(0.0005)).toBe('');
  });
});

describe('traceStatTotalRow · 总计行前端聚合（与 MON-04 同口径）', () => {
  it('Σtotal/Σerrors、rate=ΣE/ΣT、cost 均值、max/max、min/min、label 带 N', () => {
    const rows = [
      { id: 1, statTime: '2026-11-08', name: 'a', type: 'Api', total: 100, ringRate: 0, errors: 2, errorRate: 0.02, cost: 10, maxCost: 50, minCost: 2, totalValue: 0, traceId: '', rateClass: 'danger' as const },
      { id: 2, statTime: '2026-11-08', name: 'b', type: 'Db', total: 300, ringRate: 0, errors: 1, errorRate: 0.003, cost: 20, maxCost: 80, minCost: 4, totalValue: 5, traceId: '', rateClass: 'warning' as const },
    ];
    const t = traceStatTotalRow(rows);
    expect(t).not.toBeNull();
    expect(t!.label).toBe('总计（已加载 2 行）');
    expect(t!.total).toBe(400);
    expect(t!.errors).toBe(3);
    expect(t!.errorRate).toBeCloseTo(0.0075, 6);
    expect(t!.cost).toBe(15);
    expect(t!.maxCost).toBe(80);
    expect(t!.minCost).toBe(2);
  });

  it('空行返回 null（不渲染总计行）', () => {
    expect(traceStatTotalRow([])).toBeNull();
  });
});

describe('traceStatChartOption · 单图 260px 调用/错误双序列（裁定：不做上下双图+DataZoom）', () => {
  const PALETTE: ChartPalette = { primary: '#1f', danger: '#d1', axis: '#a1', splitLine: '#s1' };

  it('x 轴=行 statTime，series=调用次数+错误数，注入调色板', () => {
    const rows = [
      { id: 1, statTime: '2026-11-07', name: 'a', type: 'Api', total: 100, ringRate: 0, errors: 2, errorRate: 0.02, cost: 10, maxCost: 50, minCost: 2, totalValue: 0, traceId: '', rateClass: 'danger' as const },
      { id: 2, statTime: '2026-11-08', name: 'b', type: 'Db', total: 300, ringRate: 0, errors: 1, errorRate: 0.003, cost: 20, maxCost: 80, minCost: 4, totalValue: 5, traceId: '', rateClass: 'warning' as const },
    ];
    const opt = traceStatChartOption(rows, PALETTE) as { xAxis: { data: string[] }; series: { name: string; data: number[]; lineStyle: { color: string } }[] };
    expect(opt.xAxis.data).toEqual(['2026-11-07', '2026-11-08']);
    expect(opt.series.map((s) => s.name)).toEqual(['调用次数', '错误数']);
    expect(opt.series[0].data).toEqual([100, 300]);
    expect(opt.series[1].lineStyle.color).toBe('#d1');
  });

  it('空行安全（x 轴与 series 空数组）', () => {
    const opt = traceStatChartOption([], PALETTE) as { xAxis: { data: unknown[] }; series: unknown[] };
    expect(opt.xAxis.data).toEqual([]);
    expect(opt.series).toHaveLength(2);
  });

  it('hour/minute x 轴截取 HH:mm，day 日期原样', () => {
    const mk = (statTime: string) => ({ id: 1, statTime, name: 'a', type: 'Api', total: 1, ringRate: 0, errors: 0, errorRate: 0, cost: 1, maxCost: 1, minCost: 1, totalValue: 0, traceId: '', rateClass: '' as const });
    const opt = traceStatChartOption([mk('2026-11-08 09:00:00'), mk('2026-11-08'), mk('2026-11-08 09:05:00')], PALETTE) as { xAxis: { data: string[] } };
    expect(opt.xAxis.data).toEqual(['09:00', '2026-11-08', '09:05']);
  });
});

describe('resolveTrace · 追踪解析（成功跳 /trace?id=，失败三文案）', () => {
  it('day：GET /Monitors/TraceDayStat/Trace?id=，成功取 data.traceId', async () => {
    let seen: { url?: string; params?: Record<string, unknown> } = {};
    const request = (async (cfg: { url: string; params: Record<string, unknown> }) => {
      seen = cfg;
      return { code: 0, message: 'OK', data: { traceId: 'f1e2d3c4' } };
    }) as Req;
    const r = await resolveTrace(request, 'day', 101);
    expect(seen.url).toBe('/Monitors/TraceDayStat/Trace');
    expect(seen.params?.id).toBe(101);
    expect(r).toEqual({ ok: true, traceId: 'f1e2d3c4' });
  });

  it('hour 平铺 traceId 兜底；失败文案透传（找不到统计数据/找不到追踪数据/采样数据不存在）', async () => {
    const okReq = (async () => ({ code: 0, traceId: 'abc123' })) as Req;
    expect(await resolveTrace(okReq, 'hour', 5)).toEqual({ ok: true, traceId: 'abc123' });

    for (const msg of ['找不到统计数据', '找不到追踪数据', '采样数据不存在']) {
      const failReq = (async () => {
        throw new Error(msg);
      }) as Req;
      const r = await resolveTrace(failReq, 'minute', 5);
      expect(r.ok).toBe(false);
      if (!r.ok) expect(r.message).toBe(msg);
    }
  });
});

describe('fmtRingRate · 环比倍率显示口径（RingRate=1.2 表示超 20%）', () => {
  it.each([
    [1.2, '+20%'],
    [0.75, '-25%'],
    [1.055, '+5.5%'],
    [1, '0%'],
    [0, '—'],
    [NaN, '—'],
  ])('fmtRingRate(%p) === %p', (input, expected) => {
    expect(fmtRingRate(input as number)).toBe(expected);
  });
});
