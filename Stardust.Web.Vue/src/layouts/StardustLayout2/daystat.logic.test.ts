// daystat.logic 单测（Vitest）——参数、映射、排序、图表 option、汇总
import { describe, it, expect, vi } from 'vitest';
import {
  buildDayStatParams,
  buildMinuteStatParams,
  dayCallsChartOption,
  dayCostChartOption,
  dayStatSummary,
  fetchDayStats,
  fetchMinuteStats,
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
