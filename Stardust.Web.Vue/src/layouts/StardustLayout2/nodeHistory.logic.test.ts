/**
 * nodeHistory.logic 单测（CARD-NODE-09）。
 * 契约：7 列视图行（nodeName 抽屉名注入/successText 是/否/createTime 到秒）；
 * success 仅 'true'/'false' 字符串携带（后端 ToBoolean 直读，NodeHistoryController.cs:47）；
 * 排序参数不传（后端 Id 降序兜底 :52）；三态派生；取数容错。
 */
import { describe, expect, it } from 'vitest';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel';
import { fetchNodeHistory, historyListState, toNodeHistoryRows } from './nodeHistory.logic';

type Rec = Record<string, unknown>;

function mockReq(handler: (opts: Rec) => Promise<unknown> | unknown): Req {
  return (async (opts: Rec) => handler(opts)) as unknown as Req;
}

describe('toNodeHistoryRows', () => {
  it('全字段映射：success true→是 / false→否，camel/Pascal 两态兼容，id 数字', () => {
    const rows = toNodeHistoryRows(
      [
        { id: 11, nodeId: 1, action: '登录', success: true, remark: '上线', traceId: 't-1', createTime: '2026-11-11T08:00:05', createIP: '127.0.0.1' },
        { Id: 12, Action: '登出', Success: false, Remark: '下线', TraceId: 't-2', CreateTime: '2026-11-11 09:10:20', CreateIP: '192.168.1.2' },
      ],
      'node-a',
    );
    expect(rows).toEqual([
      { id: 11, nodeName: 'node-a', action: '登录', successText: '是', remark: '上线', traceId: 't-1', createTime: '2026-11-11 08:00:05', createIP: '127.0.0.1' },
      { id: 12, nodeName: 'node-a', action: '登出', successText: '否', remark: '下线', traceId: 't-2', createTime: '2026-11-11 09:10:20', createIP: '192.168.1.2' },
    ]);
  });

  it('抽屉节点名优先注入；后端缺 id 归 null；success 缺失归「否」；时间空归「—」', () => {
    const rows = toNodeHistoryRows([{ action: '升级' }], '抽屉名');
    expect(rows[0]).toEqual({
      id: null, nodeName: '抽屉名', action: '升级', successText: '否', remark: '', traceId: '', createTime: '—', createIP: '',
    });
  });

  it('后端 nodeName 兜底（抽屉名空时）', () => {
    const rows = toNodeHistoryRows([{ nodeName: 'node-b' }], '');
    expect(rows[0].nodeName).toBe('node-b');
  });
});

describe('historyListState', () => {
  it('loading 优先 → error → 空 → ok 四态', () => {
    expect(historyListState(true, '', [])).toBe('loading');
    expect(historyListState(false, '炸了', [])).toBe('error');
    expect(historyListState(false, '', [])).toBe('empty');
    expect(historyListState(false, '', [{ id: 1, nodeName: '', action: '', successText: '是', remark: '', traceId: '', createTime: '', createIP: '' }])).toBe('ok');
  });
});

describe('fetchNodeHistory', () => {
  it('success=true：GET /Nodes/NodeHistory，params 带 nodeId/pageSize/success 字符串，不传排序', async () => {
    let captured: Rec | null = null;
    const req = mockReq((o) => {
      captured = o;
      return { code: 0, data: [{ id: 1, nodeName: 'node-a', action: '登录', success: true }] };
    });
    const r = await fetchNodeHistory(req, 1, 'node-a', 'true');
    expect(captured).toMatchObject({ url: '/Nodes/NodeHistory', method: 'GET' });
    expect((captured as unknown as Rec).params).toEqual({ nodeId: 1, pageSize: 50, success: 'true' });
    expect(r.error).toBe('');
    expect(r.rows).toHaveLength(1);
    expect(r.rows[0].successText).toBe('是');
  });

  it('success=空：不带 success 参数（后端全部）', async () => {
    let captured: Rec | null = null;
    const req = mockReq((o) => {
      captured = o;
      return { code: 0, data: [] };
    });
    await fetchNodeHistory(req, 2, 'node-b', '');
    expect((captured as unknown as Rec).params).toEqual({ nodeId: 2, pageSize: 50 });
  });

  it('业务失败 code!==0：归 error（rows 空）', async () => {
    const req = mockReq(() => ({ code: 400, msg: '权限不足' }));
    const r = await fetchNodeHistory(req, 1, 'node-a', 'false');
    expect(r.rows).toEqual([]);
    expect(r.error).toBe('权限不足');
  });

  it('请求抛错：error 兜底文案', async () => {
    const req = mockReq(() => {
      throw new Error('network down');
    });
    const r = await fetchNodeHistory(req, 1, 'node-a', '');
    expect(r.error).toBe('network down');
  });

  it('data 非数组：归空行不炸', async () => {
    const req = mockReq(() => ({ code: 0, data: null }));
    const r = await fetchNodeHistory(req, 1, 'node-a', '');
    expect(r.rows).toEqual([]);
  });
});
