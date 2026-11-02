import { describe, expect, it } from 'vitest';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import {
  buildObserveParams,
  emptyObserveQuery,
  fetchObserve,
  firstClientId,
  formatBytes,
  formatCpu,
  meterChart,
  observeQueryToRoute,
  sparkPoints,
  toCommandRows,
  toConsumeRows,
  toHistoryRows,
  toMeterRows,
  toObserveQuery,
  toServiceRows,
} from './appObserve.logic';

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

describe('toObserveQuery', () => {
  it('只收下拉允许的 success / enable', () => {
    const q = toObserveQuery({ success: '1', enable: 'nope', client: 'a@1', clientId: ['10.0.0.1,10.0.0.2'] });
    expect(q.success).toBe('1');
    expect(q.enable).toBe('');
    expect(q.client).toBe('a@1');
    expect(q.clientId).toBe('10.0.0.1,10.0.0.2');
  });

  it('空 query 全是空串', () => {
    expect(toObserveQuery(undefined)).toEqual(emptyObserveQuery());
  });
});

describe('行映射', () => {
  it('历史认大小写，成功可空', () => {
    const [a, b] = toHistoryRows([
      { Id: 1, Client: 'c', NodeName: 'n', Action: '登录', Success: true, TraceId: 't', Remark: 'ok', Creator: 'u', CreateTime: '2026-11-02', CreateIP: '1.1.1.1' },
      { id: 2, success: '' },
    ]);
    expect(a).toMatchObject({ id: 1, client: 'c', node: 'n', action: '登录', success: true, traceId: 't', creator: 'u', createIp: '1.1.1.1' });
    expect(b?.success).toBeNull();
  });

  it('命令、性能、注册、消费', () => {
    expect(toCommandRows([{ Id: 3, Command: 'app/start', Status: 1, Result: 'ok' }])[0]).toMatchObject({
      id: 3, command: 'app/start', status: '1', result: 'ok',
    });
    expect(toMeterRows([{ Id: 4, ClientId: 'c1', NodeId: 9, Memory: 1024, CpuUsage: 12.5, Time: '' , CreateTime: 't' }])[0]).toMatchObject({
      clientId: 'c1', node: '#9', memory: 1024, cpu: 12.5, time: 't',
    });
    expect(toServiceRows([{ Id: 5, ServiceName: 'api', Client: 'c', Enable: false, Address: 'http://x' }])[0]).toMatchObject({
      serviceName: 'api', enable: false, address: 'http://x',
    });
    expect(toConsumeRows([{ Id: 6, ServiceName: 'api', Scope: 'prod' }])[0]).toMatchObject({ id: 6, scope: 'prod' });
  });
});

describe('buildObserveParams', () => {
  it('历史带上非空筛选，空的不传', () => {
    const q = { ...emptyObserveQuery(), client: ' a ', action: '登录', success: '0' as const, dtStart: '2026-11-02' };
    expect(buildObserveParams('appHistory', 32, q)).toEqual({
      appId: 32,
      PageSize: 50,
      client: 'a',
      action: '登录',
      success: '0',
      dtStart: '2026-11-02',
    });
  });

  it('性能 PageSize 为 20，多 IP 取第一段', () => {
    const q = { ...emptyObserveQuery(), clientId: ' 10.0.0.1,10.0.0.2 ', source: 'agent' };
    expect(buildObserveParams('meter', 32, q)).toEqual({
      appId: 32,
      PageSize: 20,
      clientId: '10.0.0.1',
      source: 'agent',
    });
    expect(firstClientId('only')).toBe('only');
  });

  it('非法 appId 不发请求', () => {
    expect(buildObserveParams('services', 0, emptyObserveQuery())).toBeNull();
  });
});

describe('meterChart / spark / 格式', () => {
  it('唯一实例才出曲线，并按时间排序', () => {
    const rows = toMeterRows([
      { Id: 2, ClientId: 'c', Time: '2026-11-02 10:00', Memory: 2 },
      { Id: 1, ClientId: 'c', Time: '2026-11-02 09:00', Memory: 1 },
    ]);
    expect(meterChart(rows)?.points.map((p) => p.id)).toEqual([1, 2]);
    expect(meterChart(toMeterRows([{ ClientId: 'a' }, { ClientId: 'b' }]))).toBeNull();
    expect(meterChart([])).toBeNull();
  });

  it('折线两端贴边', () => {
    expect(sparkPoints([0, 10], 100, 40)).toBe('0.0,39.0 100.0,1.0');
    expect(sparkPoints([], 100, 40)).toBe('');
  });

  it('字节与 CPU', () => {
    expect(formatBytes(0)).toBe('0 B');
    expect(formatBytes(1536)).toBe('1.5 KB');
    expect(formatBytes(1048576)).toBe('1.0 MB');
    expect(formatBytes(-1)).toBe('—');
    expect(formatCpu(12)).toBe('12%');
    expect(formatCpu(12.5)).toBe('12.5%');
  });
});

describe('fetchObserve', () => {
  it('按类型请求并映射', async () => {
    const { req, calls } = stubReq([{ res: { data: [{ Id: 1, Action: '心跳', Success: 1 }] } }]);
    const res = await fetchObserve(req, 'appHistory', 32, emptyObserveQuery());
    expect(res).toEqual({ kind: 'appHistory', rows: [expect.objectContaining({ action: '心跳', success: true })] });
    expect(calls[0]).toMatchObject({ url: '/Registry/AppHistory', method: 'GET' });
  });

  it('data 本身是数组也能读', async () => {
    const { req } = stubReq([{ res: [{ Id: 8, Command: 'ping' }] }]);
    const res = await fetchObserve(req, 'commands', 4, emptyObserveQuery());
    expect(res?.kind).toBe('commands');
    if (res?.kind === 'commands') expect(res.rows[0]?.command).toBe('ping');
  });

  it('失败返回 null，非法 appId 返回空表且不请求', async () => {
    const fail = stubReq([{ err: new Error('500') }]);
    expect(await fetchObserve(fail.req, 'meter', 3, emptyObserveQuery())).toBeNull();
    const none = stubReq([]);
    expect(await fetchObserve(none.req, 'consume', 0, emptyObserveQuery())).toEqual({ kind: 'consume', rows: [] });
    expect(none.calls).toHaveLength(0);
  });
});

describe('observeQueryToRoute', () => {
  it('丢掉空串', () => {
    expect(observeQueryToRoute({ ...emptyObserveQuery(), client: 'c', success: '1' })).toEqual({ client: 'c', success: '1' });
  });
});
