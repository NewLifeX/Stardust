// daystat.logic 单测（Vitest）——参数、映射、排序、图表 option、汇总
import { describe, it, expect, vi } from 'vitest';
import {
  buildDayStatParams,
  buildDayStatRangeParams,
  buildMinuteStatParams,
  dayCallsChartOption,
  dayCostChartOption,
  dayStatSummary,
  dayStatTotalRow,
  fetchDayStats,
  fetchDayStatsRange,
  fetchMinuteStats,
  createLoadGuard,
  fmtErrorRate,
  localDateStr,
  localDateTimeStr,
  minuteChartOption,
  minuteLabel,
  retryDayStat,
  toDayStatRow,
  toMinuteStatRow,
} from './daystat.logic';
import type { ChartPalette } from './daystat.logic';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

const PALETTE: ChartPalette = { primary: '#1f', danger: '#d1', cost: '#c1', costRange: '#c2', axis: '#a1', splitLine: '#s1' };

describe('localDateStr / localDateTimeStr', () => {
  it('补零成 YYYY-MM-DD / YYYY-MM-DD HH:mm:ss', () => {
    expect(localDateStr(new Date(2026, 0, 3))).toBe('2026-01-03');
    expect(localDateTimeStr(new Date(2026, 10, 3, 5, 6, 7))).toBe('2026-11-03 05:06:07');
  });
});

describe('buildDayStatParams / fetchDayStats', () => {
  it('默认最近 7 天（含今天），升序返回', async () => {
    const now = new Date(2026, 10, 3, 12, 0, 0);
    const p = buildDayStatParams(32, 7, now);
    expect(p.appId).toBe(32);
    expect(p.dtStart).toBe('2026-10-28');
    expect(p.dtEnd).toBe('2026-11-03');

    const rows = [
      toDayStatRow({ StatDate: '2026-11-03', Total: 1 }),
      toDayStatRow({ StatDate: '2026-11-01', Total: 2 }),
      toDayStatRow({ StatDate: '2026-11-02', Total: 3 }),
    ];
    const request = (async () => ({ data: rows })) as Req;
    const list = await fetchDayStats(request, 32, 7, now);
    expect(list.map((r) => r.statDate)).toEqual(['2026-11-01', '2026-11-02', '2026-11-03']);
  });

  it('days=1 只查当天', () => {
    const p = buildDayStatParams(5, 1, new Date(2026, 10, 3));
    expect(p.dtStart).toBe('2026-11-03');
  });
});

describe('fetchMinuteStats · 最近24h', () => {
  it('窗口与 pageSize=288，结果按时间升序（后端按 Total 倒序返回）', async () => {
    const now = new Date(2026, 10, 3, 10, 30, 0);
    const p = buildMinuteStatParams(32, 24, now);
    expect(p.dtStart).toBe('2026-11-02 10:30:00');
    expect(p.dtEnd).toBe('2026-11-03 10:30:00');

    let seen: { url?: string; params?: Record<string, unknown> } = {};
    const request = (async (cfg: { url: string; params: Record<string, unknown> }) => {
      seen = cfg;
      return {
        data: [
          { StatTime: '2026-11-03 10:05:00', Total: 500, Errors: 2, Cost: 12, MaxCost: 40, MinCost: 3 },
          { StatTime: '2026-11-03 09:55:00', Total: 300, Errors: 0, Cost: 9, MaxCost: 30, MinCost: 2 },
        ],
      };
    }) as Req;
    const rows = await fetchMinuteStats(request, 32, 24, now);
    expect(seen.url).toBe('/Monitors/AppMinuteStat');
    expect(seen.params?.pageSize).toBe(288);
    expect(rows.map((r) => minuteLabel(r.statTime))).toEqual(['09:55', '10:05']);
    expect(rows[1].total).toBe(500);
  });
});

describe('toDayStatRow / toMinuteStatRow · 映射', () => {
  it('PascalCase 行', () => {
    const r = toDayStatRow({ ID: 1, StatDate: '2026-11-03 00:00:00', AppId: 5, Total: 10, ErrorRate: 12.5 });
    expect(r.id).toBe(1);
    expect(r.statDate).toBe('2026-11-03');
    expect(r.total).toBe(10);
    expect(r.errorRate).toBeCloseTo(12.5);
  });

  it('缺字段为 0', () => {
    const r = toMinuteStatRow({});
    expect(r.id).toBe(0);
    expect(r.total).toBe(0);
    expect(r.statTime).toBe('');
  });

  it('裸数组与 {data} 都能映射', async () => {
    const req1 = (async () => [{}]) as Req;
    const req2 = (async () => ({ data: [{}] })) as Req;
    expect(await fetchDayStats(req1, 1)).toHaveLength(1);
    expect(await fetchDayStats(req2, 1)).toHaveLength(1);
  });
});

describe('retryDayStat · 批量重算', () => {
  it('POST 到 RetryStat，keys 走 query 逗号串（SelectKeys 读 GetRequest）', async () => {
    let seen: { method?: string; url?: string; params?: Record<string, unknown> } = {};
    const request = (async (cfg: { method: string; url: string; params: Record<string, unknown> }) => {
      seen = cfg;
      return { code: 0, message: '成功！' };
    }) as Req;
    const msg = await retryDayStat(request, [11, 22]);
    expect(msg).toBe('成功！');
    expect(seen.method).toBe('POST');
    expect(seen.url).toBe('/Monitors/AppDayStat/RetryStat');
    expect(seen.params?.keys).toBe('11,22');
  });

  it('空选择不发请求', async () => {
    const request = vi.fn() as unknown as Req;
    expect(await retryDayStat(request, [])).toBe('');
    expect(request).not.toHaveBeenCalled();
  });
});

describe('图表 option · ECharts option 组装', () => {
  it('调用/错误双序列：x 轴日期、两条 line', () => {
    const rows = [
      toDayStatRow({ StatDate: '2026-11-01', Total: 1, Errors: 0 }),
      toDayStatRow({ StatDate: '2026-11-02', Total: 4, Errors: 2 }),
    ];
    const opt = dayCallsChartOption(rows, PALETTE) as { xAxis: { data: string[] }; series: Array<{ name: string; data: number[]; type: string }> };
    expect(opt.xAxis.data).toEqual(['2026-11-01', '2026-11-02']);
    expect(opt.series.map((s) => s.name)).toEqual(['调用次数', '错误数']);
    expect(opt.series[0].data).toEqual([1, 4]);
    expect(opt.series[1].data).toEqual([0, 2]);
    expect(opt.series.every((s) => s.type === 'line')).toBe(true);
  });

  it('耗时图三条线：平均/最大/最小', () => {
    const rows = [toDayStatRow({ StatDate: '2026-11-01', Cost: 5, MaxCost: 9, MinCost: 1 })];
    const opt = dayCostChartOption(rows, PALETTE) as { series: Array<{ name: string; data: number[] }> };
    expect(opt.series.map((s) => s.name)).toEqual(['平均耗时', '最大耗时', '最小耗时']);
    expect(opt.series[0].data).toEqual([5]);
  });

  it('分钟图 x 轴用 HH:mm 短标签', () => {
    const rows = [
      toMinuteStatRow({ StatTime: '2026-11-03 09:05:00', Total: 1 }),
      toMinuteStatRow({ StatTime: '2026-11-03 09:10:00', Total: 2 }),
    ];
    const opt = minuteChartOption(rows, PALETTE) as { xAxis: { data: string[] } };
    expect(opt.xAxis.data).toEqual(['09:05', '09:10']);
  });

  it('minuteLabel 兜底：没有时间片段时原样返回', () => {
    expect(minuteLabel('bad')).toBe('bad');
  });
});

describe('dayStatSummary · 顶部汇总', () => {
  it('求和与错误率手算', () => {
    const rows = [
      toDayStatRow({ Total: 100, Errors: 5, Cost: 20 }),
      toDayStatRow({ Total: 300, Errors: 10, Cost: 40 }),
    ];
    const s = dayStatSummary(rows);
    expect(s.total).toBe(400);
    expect(s.errors).toBe(15);
    expect(s.errorRate).toBeCloseTo(3.75);
    expect(s.avgCost).toBe(30);
  });

  it('空数据全 0', () => {
    const s = dayStatSummary([]);
    expect(s.total).toBe(0);
    expect(s.errors).toBe(0);
    expect(s.avgCost).toBe(0);
    expect(s.errorRate).toBe(0);
  });

  it('total=0 时错误率为 0 不除零', () => {
    const s = dayStatSummary([toDayStatRow({ Total: 0, Errors: 3 })]);
    expect(s.errorRate).toBe(0);
  });
});

describe('buildDayStatRangeParams / fetchDayStatsRange · 自定义区间（CARD-MON-04）', () => {
  it('区间参数原样透传，pageSize 按区间天数对齐（span+2，下限 30）', () => {
    const p = buildDayStatRangeParams(32, '2026-10-01', '2026-10-14');
    expect(p.appId).toBe(32);
    expect(p.dtStart).toBe('2026-10-01');
    expect(p.dtEnd).toBe('2026-10-14');
    expect(p.pageSize).toBe(30); // 14 天 → max(16, 30)

    const long = buildDayStatRangeParams(32, '2026-08-01', '2026-11-08'); // 100 天
    expect(long.pageSize).toBe(102);
  });

  it('逆序区间（起晚于止）天数兜底为 1，pageSize 仍为下限 30', () => {
    const p = buildDayStatRangeParams(5, '2026-11-08', '2026-11-01');
    expect(p.pageSize).toBe(30);
    expect(p.dtStart).toBe('2026-11-08');
    expect(p.dtEnd).toBe('2026-11-01');
  });

  it('fetchDayStatsRange 请求 /Monitors/AppDayStat 带区间参数，结果按日期升序', async () => {
    let seen: { url?: string; params?: Record<string, unknown> } = {};
    const request = (async (cfg: { url: string; params: Record<string, unknown> }) => {
      seen = cfg;
      return {
        data: [
          { StatDate: '2026-10-03', Total: 1 },
          { StatDate: '2026-10-01', Total: 2 },
          { StatDate: '2026-10-02', Total: 3 },
        ],
      };
    }) as Req;
    const list = await fetchDayStatsRange(request, 32, '2026-10-01', '2026-10-14');
    expect(seen.url).toBe('/Monitors/AppDayStat');
    expect(seen.params?.dtStart).toBe('2026-10-01');
    expect(seen.params?.dtEnd).toBe('2026-10-14');
    expect(list.map((r) => r.statDate)).toEqual(['2026-10-01', '2026-10-02', '2026-10-03']);
  });
});

describe('dayStatTotalRow · 总计行聚合（前端聚合已加载行，CARD-MON-04）', () => {
  it('求和/错误率/平均耗时/最大耗时手算', () => {
    const rows = [
      toDayStatRow({ StatDate: '2026-11-01', Total: 100, Errors: 5, Cost: 20, MaxCost: 50, Names: 3, Apis: 1, Https: 2, Dbs: 3, Mqs: 4, Redis: 5 }),
      toDayStatRow({ StatDate: '2026-11-02', Total: 300, Errors: 10, Cost: 40, MaxCost: 70, Names: 4, Apis: 2, Https: 3, Dbs: 4, Mqs: 5, Redis: 6 }),
    ];
    const t = dayStatTotalRow(rows);
    expect(t.days).toBe(2);
    expect(t.total).toBe(400);
    expect(t.errors).toBe(15);
    expect(t.errorRate).toBeCloseTo(3.75);
    expect(t.avgCost).toBe(30);
    expect(t.maxCost).toBe(70);
    expect(t.names).toBe(7);
    expect(t.apis).toBe(3);
    expect(t.https).toBe(5);
    expect(t.dbs).toBe(7);
    expect(t.mqs).toBe(9);
    expect(t.redis).toBe(11);
  });

  it('空行全 0 不除零', () => {
    const t = dayStatTotalRow([]);
    expect(t.days).toBe(0);
    expect(t.total).toBe(0);
    expect(t.errors).toBe(0);
    expect(t.errorRate).toBe(0);
    expect(t.avgCost).toBe(0);
    expect(t.maxCost).toBe(0);
  });

  it('total=0 行错误率 0；avgCost 过滤无调用行（与汇总卡同口径）', () => {
    const t = dayStatTotalRow([toDayStatRow({ Total: 0, Errors: 3, Cost: 99, MaxCost: 120 })]);
    expect(t.errorRate).toBe(0);
    expect(t.avgCost).toBe(0);
    expect(t.errors).toBe(3);
    expect(t.maxCost).toBe(120);
  });
});

describe('fmtErrorRate · 单行错误率百分比口径（C13 修复：后端 ErrorRate 存小数，×100 渲染）', () => {
  it('0.0043 → 0.43%（与总计行同数量级），0.0208 → 2.08%', () => {
    expect(fmtErrorRate(0.0043)).toBe('0.43%');
    expect(fmtErrorRate(0.0208)).toBe('2.08%');
  });

  it('0 → 0%；负值与超界值也按比例；NaN/非有限 → —', () => {
    expect(fmtErrorRate(0)).toBe('0%');
    expect(fmtErrorRate(0.125)).toBe('12.5%');
    expect(fmtErrorRate(Number.NaN)).toBe('—');
    expect(fmtErrorRate(Number.POSITIVE_INFINITY)).toBe('—');
  });
});

describe('createLoadGuard · 双加载域竞态守卫（CARD-MON-DEF-01）', () => {
  it('同域后发使先发 stale（守卫本职）', () => {
    const g = createLoadGuard();
    const t1 = g.begin();
    const t2 = g.begin();
    expect(g.isStale(t1)).toBe(true);
    expect(g.isStale(t2)).toBe(false);
  });

  it('跨域实例互不影响：他域 begin 不误伤本域 fresh 判定（缺陷语义=共用 token 时分钟域 begin 使日统计域 loading 永不复位）', () => {
    const gLoad = createLoadGuard();
    const gMinute = createLoadGuard();
    const tLoad = gLoad.begin();
    const tMinute = gMinute.begin();
    expect(gLoad.isStale(tLoad)).toBe(false);
    expect(gMinute.isStale(tMinute)).toBe(false);
  });

  it('跨域先后交错仍互不干扰（模拟 load 在飞期间 loadMinute begin+完成）', () => {
    const gLoad = createLoadGuard();
    const gMinute = createLoadGuard();
    const tLoad = gLoad.begin();
    const tMinute1 = gMinute.begin();
    expect(gLoad.isStale(tLoad)).toBe(false);
    const tMinute2 = gMinute.begin();
    expect(gMinute.isStale(tMinute1)).toBe(true);
    expect(gMinute.isStale(tMinute2)).toBe(false);
    expect(gLoad.isStale(tLoad)).toBe(false);
  });

  it('ticket 唯一递增，实例间互不共享计数', () => {
    const g1 = createLoadGuard();
    const g2 = createLoadGuard();
    const a1 = g1.begin();
    const a2 = g1.begin();
    const b1 = g2.begin();
    expect(a2).toBe(a1 + 1);
    expect(b1).toBe(1);
    expect(g2.isStale(a1)).toBe(false);
  });
});
