/**
 * nodeCmd.logic 单测（NODE-06 B1，2026-11-09）。
 * 载荷契约取自 2026-11-09 6681 实测：GET /api/Nodes/NodeCommand → {code:0, page:{…}, data:[实体数组]}，
 * 字段 camelCase（id/nodeID/command/argument/…/traceId/createUser/createTime/updateTime）。
 */
import { describe, expect, it, vi } from 'vitest';
import { cmdListState, fetchNodeCommands, toNodeCmdRows, type NodeCmdRow } from './nodeCmd.logic.ts';

type Rec = Record<string, unknown>;

/** 6681 实测样例行（nodeId=1 真实载荷节选，字段名保持原样） */
const RAW_A: Rec = {
  id: 2,
  nodeID: 1,
  command: 'deploy/start',
  argument: '{"name":"test"}',
  startTime: '2026-11-09 01:00:00',
  expire: 3600,
  status: 0,
  times: 0,
  result: null,
  traceId: 'trc-abc-123',
  createUser: '星尘管理平台 API',
  createUserID: 1,
  createTime: '2026-11-09 01:00:05',
  createIP: '127.0.0.1',
  updateUserID: 1,
  updateTime: '2026-11-09 01:00:05',
  updateIP: '127.0.0.1',
};

describe('toNodeCmdRows', () => {
  it('映射完整实体为 9 列视图行并注入 nodeName', () => {
    const rows = toNodeCmdRows([RAW_A], '节点一');
    expect(rows).toHaveLength(1);
    const r = rows[0]!;
    expect(r.id).toBe(2);
    expect(r.nodeName).toBe('节点一');
    expect(r.command).toBe('deploy/start');
    expect(r.argument).toBe('{"name":"test"}');
    expect(r.result).toBe('');
    expect(r.traceId).toBe('trc-abc-123');
    expect(r.createUser).toBe('星尘管理平台 API');
    expect(r.createTime).toBe('2026-11-09 01:00');
    expect(r.updateTime).toBe('2026-11-09 01:00');
  });

  it('容错：缺字段/空数组/PascalCase 兜底', () => {
    expect(toNodeCmdRows([], '节点一')).toEqual([]);
    const rows = toNodeCmdRows([{ Id: 7, Command: 'check', TraceId: 't1' }], '');
    const r = rows[0]!;
    expect(r.id).toBe(7);
    expect(r.nodeName).toBe('');
    expect(r.command).toBe('check');
    expect(r.argument).toBe('');
    expect(r.traceId).toBe('t1');
    expect(r.createTime).toBe('—');
    // id 为 null（无 id 字段时）允许 null
    const noId = toNodeCmdRows([{ command: 'x' }], 'n');
    expect(noId[0]!.id).toBeNull();
  });
});

describe('cmdListState', () => {
  it('三态优先级：loading > error > empty > ok', () => {
    const rows: NodeCmdRow[] = [{ id: 1, nodeName: 'n', command: 'c', argument: '', result: '', traceId: '', createUser: '', createTime: '', updateTime: '' }];
    expect(cmdListState(true, '', [])).toBe('loading');
    expect(cmdListState(false, 'boom', [])).toBe('error');
    expect(cmdListState(false, '', [])).toBe('empty');
    expect(cmdListState(false, '', rows)).toBe('ok');
  });
});

describe('fetchNodeCommands', () => {
  it('成功：GET /Nodes/NodeCommand，params 带 nodeId+pageSize=20，data 数组转行', async () => {
    const request = vi.fn(async () => ({ data: [RAW_A] }));
    const r = await fetchNodeCommands(request as never, 1, '节点一', 3);
    expect(request).toHaveBeenCalledWith(
      expect.objectContaining({
        url: '/Nodes/NodeCommand',
        method: 'GET',
        params: { nodeId: 1, pageSize: 20, projectId: 3 },
      }),
    );
    expect(r.error).toBe('');
    expect(r.rows).toHaveLength(1);
    expect(r.rows[0]!.nodeName).toBe('节点一');
  });

  it('失败：Error 透出 message；非 Error 用兜底文案', async () => {
    const bad = vi.fn(async () => {
      throw new Error('网络断开');
    });
    const r1 = await fetchNodeCommands(bad as never, 1, '', null);
    expect(r1).toEqual({ rows: [], error: '网络断开' });

    const worse = vi.fn(async () => {
      throw 'x';
    });
    const r2 = await fetchNodeCommands(worse as never, 1, '', null);
    expect(r2).toEqual({ rows: [], error: '命令记录加载失败' });
  });
});
