// CARD-MON-11 追踪数据纯逻辑单测（TDD 先红）。
// 手算基准数据（4 行）：
//   r1: GET /api/user   total=10 errors=2 cost=120 totalCost=1200 hour=09:00
//   r2: GET /api/user   total=5  errors=1 cost=100 totalCost=500  hour=09:00
//   r3: POST /api/order total=3  errors=0 cost=300 totalCost=900  hour=10:00
//   r4: POST /api/order total=8  errors=4 cost=200 totalCost=1600 hour=08:00（乱序输入）
// 柱状（按 Name Top10，Total 降序）：['GET /api/user','POST /api/order'] totals=[15,11] errors=[3,4]
// 折线（按 StatHour 升序，组内 Total 加权平均 Cost）：08:00→200、09:00→round(1700/15)=113、10:00→300
// 总计行：ΣTotal=26、ΣErrors=7、ΣTotalCost=4200、avgCost=round(4200/26)=162
// 雪花用例锁：id='9007199254740993'（>Number.MAX_SAFE_INTEGER）全链 string 原样透传。
import { describe, expect, it } from 'vitest';
import {
  buildTraceDataParams,
  excludeTraceData,
  fetchTraceData,
  fmtUnixMs,
  resolveTraceDataTrace,
  showTraceDataCharts,
  traceDataCostLine,
  traceDataNameBar,
  traceDataTotalRow,
  type TraceDataRow,
  type TraceDataQuery,
} from './traceData.logic.ts';
import type { Req, ReqOptions } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

const SNOWFLAKE = '9007199254740993';

function row(over: Partial<TraceDataRow> = {}): TraceDataRow {
  return {
    id: SNOWFLAKE,
    statMinute: '2026-11-09 09:05:00',
    statHour: '2026-11-09 09:00:00',
    clientId: 'ip@proc',
    name: 'GET /api/user',
    total: 10,
    errors: 2,
    totalCost: 1200,
    cost: 120,
    maxCost: 500,
    minCost: 20,
    startTime: 1762638300000,
    endTime: 1762638360000,
    ...over,
  };
}

const BASE_ROWS: TraceDataRow[] = [
  row(),
  row({ id: '9007199254740994', total: 5, errors: 1, totalCost: 500, cost: 100 }),
  row({ id: '9007199254740995', name: 'POST /api/order', total: 3, errors: 0, totalCost: 900, cost: 300, statHour: '2026-11-09 10:00:00', statMinute: '2026-11-09 10:05:00' }),
  row({ id: '9007199254740996', name: 'POST /api/order', total: 8, errors: 4, totalCost: 1600, cost: 200, statHour: '2026-11-09 08:00:00', statMinute: '2026-11-09 08:05:00' }),
];

// ---------- 参数组装 ----------

describe('buildTraceDataParams', () => {
  it('appId 必传 + pageSize 默认 100，最小参数只含 appId/pageSize', () => {
    const p = buildTraceDataParams({ appId: 5 });
    expect(p.url).toBe('/Monitors/TraceData');
    expect(p.params).toEqual({ appId: 5, pageSize: 100 });
  });

  it('itemId 仅 >0 传；name/searchTag=false/Q 空串均不传', () => {
    const p = buildTraceDataParams({ appId: 5, itemId: 0, name: '', searchTag: false, q: '' });
    expect('itemId' in p.params).toBe(false);
    expect('name' in p.params).toBe(false);
    expect('searchTag' in p.params).toBe(false);
    expect('Q' in p.params).toBe(false);

    const p2 = buildTraceDataParams({ appId: 5, itemId: 7, name: 'GET /api/user', searchTag: true, q: 'key' });
    expect(p2.params.itemId).toBe(7);
    expect(p2.params.name).toBe('GET /api/user');
    expect(p2.params.searchTag).toBe(true);
    expect(p2.params.Q).toBe('key');
  });

  it('searchTag 仅 true 生效：显式 false 与缺省都不落参数（后端取首段 ToBoolean，如实）', () => {
    expect('searchTag' in buildTraceDataParams({ appId: 5 }).params).toBe(false);
    expect('searchTag' in buildTraceDataParams({ appId: 5, searchTag: undefined }).params).toBe(false);
  });

  it('dtStart/dtEnd 显式传入原样；pageSize 自定义覆盖默认', () => {
    const p = buildTraceDataParams({ appId: 5, dtStart: '2026-11-08', dtEnd: '2026-11-09', pageSize: 50 });
    expect(p.params.dtStart).toBe('2026-11-08');
    expect(p.params.dtEnd).toBe('2026-11-09');
    expect(p.params.pageSize).toBe(50);
  });
});

// ---------- 取数 ----------

describe('fetchTraceData', () => {
  it('日期缺省时前端补今天规范值（后端另有 <2000 回退保护，前端仍传规范值）', async () => {
    const calls: Array<{ url: string; method: string; params: Record<string, unknown> }> = [];
    const req: Req = async (o) => {
      calls.push(o as unknown as { url: string; method: string; params: Record<string, unknown> });
      return { data: [], total: 0 };
    };
    await fetchTraceData(req, { appId: 5 });
    expect(calls).toHaveLength(1);
    expect(calls[0].method).toBe('GET');
    expect(calls[0].url).toBe('/Monitors/TraceData');
    const today = new Date();
    const ymd = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;
    expect(calls[0].params.dtStart).toBe(ymd);
    expect(calls[0].params.dtEnd).toBe(ymd);
    expect(calls[0].params.appId).toBe(5);
    expect(calls[0].params.pageSize).toBe(100);
  });

  it('行映射：雪花 Id string 原样透传不丢精度；数值字段 Number 化', async () => {
    const req: Req = (async () => ({
      data: [
        {
          Id: SNOWFLAKE,
          StatMinute: '2026-11-09 09:05:00',
          StatHour: '2026-11-09 09:00:00',
          ClientId: 'ip@proc',
          Name: 'GET /api/user',
          Total: 10,
          Errors: 2,
          TotalCost: 1200,
          Cost: 120,
          MaxCost: 500,
          MinCost: 20,
          StartTime: 1762638300000,
          EndTime: 1762638360000,
        },
      ],
      total: 1,
    })) as Req;
    const { rows, total, error } = await fetchTraceData(req, { appId: 5 });
    expect(error).toBe('');
    expect(total).toBe(1);
    expect(rows).toHaveLength(1);
    expect(typeof rows[0].id).toBe('string');
    expect(rows[0].id).toBe(SNOWFLAKE);
    expect(rows[0].total).toBe(10);
    expect(rows[0].statHour).toBe('2026-11-09 09:00:00');
  });

  it('失败透传后端 message，不转写', async () => {
    const req: Req = (async () => {
      throw new Error('数据库不可用');
    }) as Req;
    const { rows, error } = await fetchTraceData(req, { appId: 5 });
    expect(rows).toEqual([]);
    expect(error).toBe('数据库不可用');
  });
});

// ---------- 双图条件与聚合 ----------

describe('showTraceDataCharts', () => {
  it('对齐后端 MVC 旧条件：itemId 查询值非空 && 本页行数 > 1', () => {
    expect(showTraceDataCharts(0, BASE_ROWS)).toBe(false);
    expect(showTraceDataCharts(5, [BASE_ROWS[0]])).toBe(false);
    expect(showTraceDataCharts(5, BASE_ROWS)).toBe(true);
  });
});

describe('traceDataNameBar', () => {
  it('按 Name 聚合 Total/Errors，Total 降序，两序列柱状', () => {
    const o = traceDataNameBar(BASE_ROWS, { primary: '#c0', danger: '#d0', axis: '#a0', splitLine: '#s0' });
    const xAxis = o.xAxis as { data: string[] };
    expect(xAxis.data).toEqual(['GET /api/user', 'POST /api/order']);
    const series = o.series as Array<{ name: string; type: string; data: number[] }>;
    expect(series).toHaveLength(2);
    expect(series[0].name).toBe('调用次数');
    expect(series[0].type).toBe('bar');
    expect(series[0].data).toEqual([15, 11]);
    expect(series[1].name).toBe('错误数');
    expect(series[1].data).toEqual([3, 4]);
  });

  it('Top10 截断：12 个操作名只留调用量前 10', () => {
    const many: TraceDataRow[] = Array.from({ length: 12 }, (_, i) =>
      row({ id: `9007199254740${String(900 + i)}`, name: `op-${String(i).padStart(2, '0')}`, total: i + 1, errors: 0, totalCost: 0, cost: 0, statHour: '2026-11-09 09:00:00' }),
    );
    const o = traceDataNameBar(many, { primary: '#c0', danger: '#d0', axis: '#a0', splitLine: '#s0' });
    const xAxis = o.xAxis as { data: string[] };
    expect(xAxis.data).toHaveLength(10);
    // total 降序：op-11(12) … op-02(3)
    expect(xAxis.data[0]).toBe('op-11');
    expect(xAxis.data[9]).toBe('op-02');
  });
});

describe('traceDataCostLine', () => {
  it('按 StatHour 升序分组，组内 Total 加权平均 Cost，X 轴截 HH:mm', () => {
    const o = traceDataCostLine(BASE_ROWS, { primary: '#c0', danger: '#d0', axis: '#a0', splitLine: '#s0' });
    const xAxis = o.xAxis as { data: string[] };
    expect(xAxis.data).toEqual(['08:00', '09:00', '10:00']);
    const series = o.series as Array<{ name: string; type: string; data: number[] }>;
    expect(series).toHaveLength(1);
    expect(series[0].type).toBe('line');
    expect(series[0].data).toEqual([200, 113, 300]);
  });

  it('组内 Total 全为 0 时回退算术平均（不产生 NaN）', () => {
    const zero = [
      row({ total: 0, errors: 0, totalCost: 0, cost: 111, statHour: '2026-11-09 09:00:00' }),
      row({ total: 0, errors: 0, totalCost: 0, cost: 333, statHour: '2026-11-09 09:00:00' }),
    ];
    const o = traceDataCostLine(zero, { primary: '#c0', danger: '#d0', axis: '#a0', splitLine: '#s0' });
    const series = o.series as Array<{ data: number[] }>;
    expect(series[0].data).toEqual([222]);
  });
});

// ---------- 总计行 ----------

describe('traceDataTotalRow', () => {
  it('本页合计：ΣTotal/ΣErrors/ΣTotalCost，avgCost=ΣTotalCost/ΣTotal 加权', () => {
    const t = traceDataTotalRow(BASE_ROWS);
    expect(t).not.toBeNull();
    expect(t?.total).toBe(26);
    expect(t?.errors).toBe(7);
    expect(t?.totalCost).toBe(4200);
    expect(t?.avgCost).toBe(162);
    expect(t?.label).toContain('本页');
  });

  it('空列表返回 null（不渲染总计行）', () => {
    expect(traceDataTotalRow([])).toBeNull();
  });
});

// ---------- 行操作 ----------

describe('resolveTraceDataTrace', () => {
  it('POST Trace?id=（雪花 string 透传）→ {traceId}', async () => {
    const calls: Array<{ url: string; method: string; params: Record<string, unknown> }> = [];
    const req: Req = async (o) => {
      calls.push(o as unknown as { url: string; method: string; params: Record<string, unknown> });
      return { code: 200, message: 'ok', data: { traceId: 'trace-abc' } };
    };
    const r = await resolveTraceDataTrace(req, SNOWFLAKE);
    expect(calls[0].url).toBe('/Monitors/TraceData/Trace');
    expect(calls[0].method).toBe('POST');
    expect(calls[0].params.id).toBe(SNOWFLAKE);
    expect(r).toEqual({ ok: true, traceId: 'trace-abc' });
    if (r.ok) expect(typeof r.traceId).toBe('string');
  });

  it('失败透传后端原文（如「Sample data not found.」），不转写', async () => {
    const req: Req = (async () => {
      throw new Error('Sample data not found.');
    }) as Req;
    const r = await resolveTraceDataTrace(req, SNOWFLAKE);
    expect(r).toEqual({ ok: false, message: 'Sample data not found.' });
  });
});

describe('excludeTraceData', () => {
  it('POST Exclude?id= → 成功 message 原样返回（裸字符串：拦截器剥壳直回）', async () => {
    const calls: Array<{ url: string; method: string; params: Record<string, unknown> }> = [];
    // 裸字符串直回（拦截器剥壳形态）不满足 Req 返回类型，双断言保留测试语义
    const req = (async (o: ReqOptions) => {
      calls.push(o as unknown as { url: string; method: string; params: Record<string, unknown> });
      return '成功！';
    }) as unknown as Req;
    const r = await excludeTraceData(req, SNOWFLAKE);
    expect(calls[0].url).toBe('/Monitors/TraceData/Exclude');
    expect(calls[0].method).toBe('POST');
    expect(calls[0].params.id).toBe(SNOWFLAKE);
    expect(r).toEqual({ ok: true, message: '成功！' });
  });

  it('POST Exclude 信封形态（{code:200,data:"成功！"}）同样解包', async () => {
    const req: Req = (async () => ({ code: 200, message: 'ok', data: '成功！' })) as Req;
    const r = await excludeTraceData(req, SNOWFLAKE);
    expect(r).toEqual({ ok: true, message: '成功！' });
  });

  it('失败透传 message', async () => {
    const req: Req = (async () => {
      throw new Error('没有权限');
    }) as Req;
    const r = await excludeTraceData(req, SNOWFLAKE);
    expect(r).toEqual({ ok: false, message: '没有权限' });
  });
});

// ---------- 展示格式化 ----------

describe('fmtUnixMs', () => {
  it('Unix 毫秒 → MM-dd HH:mm:ss 展示（本地时区构造，自洽）', () => {
    expect(fmtUnixMs(new Date(2026, 10, 9, 9, 5, 0).getTime())).toBe('11-09 09:05:00');
  });
  it('0/负值显示 —（不格式化出 1970）', () => {
    expect(fmtUnixMs(0)).toBe('—');
    expect(fmtUnixMs(-1)).toBe('—');
  });
});

// ---------- Query 类型冒烟 ----------

describe('TraceDataQuery 形态', () => {
  it('可选字段齐全（编译期形态锁，运行时冒烟）', () => {
    const q: TraceDataQuery = { appId: 5, itemId: 7, name: 'n', searchTag: true, dtStart: '2026-11-09', dtEnd: '2026-11-09', q: 'k', pageSize: 100 };
    expect(q.appId).toBe(5);
  });
});
