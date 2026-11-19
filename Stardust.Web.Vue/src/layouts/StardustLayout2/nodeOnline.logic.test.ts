import { describe, expect, it } from 'vitest';
import {
  BATCH_ACTIONS,
  buildKeys,
  guardTexts,
  latestOnlineByNode,
  parseBatchSummary,
  runBatchAction,
  toOnlineRows,
  validateBatchInput,
  type OnlineRow,
} from './nodeOnline.logic';
import type { Req, ReqOptions } from '../../../apps/stardust/src/views/Stardust/appsModel';

// ---------- 测试数据（手算，2026-11-09 CARD-NODE-11） ----------

// 在线快照行（camelCase，同 6681 实测形态）：nodeId=1 有两条（id=9 旧、id=11 新），nodeId=2 一条
const ONLINE_RAW = [
  { id: 9, nodeID: 1, name: 'node-a', updateTime: '2026-11-08 10:00:00' },
  { id: 11, nodeID: 1, name: 'node-a', updateTime: '2026-11-09 03:00:00' },
  { id: 12, nodeID: 2, name: 'node-b', updateTime: '2026-11-09 02:30:00' },
];

// 后端契约样例（NodeOnlineController.cs :110/:289/:294 逐字形态）
const SUMMARY_OK =
  '操作成功！下发2个，响应1个，超时1个\nnode-a: 已是最新版本\nnode-b: (无返回数据)';
const SUMMARY_SKIP = '操作成功！下发2个，响应2个，超时0个，跳过1个';
const SUMMARY_PLAIN = '操作成功！下发1个，响应1个，超时0个';

// ---------- BATCH_ACTIONS：五动作端点/权限/guard ----------

describe('BATCH_ACTIONS', () => {
  it('五动作端点与权限位映射（CheckUpgrade16/SyncTime16/Restart32/Reboot64/Execute16）', () => {
    expect(BATCH_ACTIONS.map((a) => a.key)).toEqual([
      'CheckUpgrade',
      'SyncTime',
      'Restart',
      'Reboot',
      'Execute',
    ]);
    const byKey = new Map(BATCH_ACTIONS.map((a) => [a.key, a]));
    expect(byKey.get('CheckUpgrade')).toMatchObject({ path: '/Nodes/NodeOnline/CheckUpgrade', perm: 16, guard: false });
    expect(byKey.get('SyncTime')).toMatchObject({ path: '/Nodes/NodeOnline/SyncTime', perm: 16, guard: false });
    expect(byKey.get('Restart')).toMatchObject({ path: '/Nodes/NodeOnline/Restart', perm: 32, guard: false });
    expect(byKey.get('Reboot')).toMatchObject({ path: '/Nodes/NodeOnline/Reboot', perm: 64, guard: true });
    expect(byKey.get('Execute')).toMatchObject({ path: '/Nodes/NodeOnline/Execute', perm: 16, guard: true });
  });

  it('五动作 label 与按钮文案一致（检查更新/同步时间/重启服务/重启系统/执行命令）', () => {
    expect(BATCH_ACTIONS.map((a) => a.label)).toEqual([
      '检查更新',
      '同步时间',
      '重启服务',
      '重启系统',
      '执行命令',
    ]);
  });
});

// ---------- 在线索引：Node.Id → NodeOnline.Id ----------

describe('toOnlineRows / latestOnlineByNode / buildKeys', () => {
  it('归一在线行并取每节点 updateTime 最新一条的会话 Id', () => {
    const rows: OnlineRow[] = toOnlineRows(ONLINE_RAW);
    expect(rows).toHaveLength(3);
    const idx = latestOnlineByNode(rows);
    expect(idx.get(1)).toBe(11); // id=11 (11-09 03:00) 晚于 id=9 (11-08 10:00)
    expect(idx.get(2)).toBe(12);
    expect(idx.has(3)).toBe(false);
  });

  it('buildKeys：勾选 Node.Id 按索引转会话 Id 逗号串，未上线的节点记入 missing', () => {
    const idx = latestOnlineByNode(toOnlineRows(ONLINE_RAW));
    const r = buildKeys([1, 2, 3], idx);
    expect(r.keys).toBe('11,12');
    expect(r.missing).toEqual([3]);
  });

  it('buildKeys：全部未上线 → keys 空串', () => {
    const r = buildKeys([3], latestOnlineByNode(toOnlineRows(ONLINE_RAW)));
    expect(r.keys).toBe('');
    expect(r.missing).toEqual([3]);
  });
});

// ---------- 汇总契约解析 ----------

describe('parseBatchSummary', () => {
  it('标准契约：首行逐字三数 + 每节点回复段', () => {
    const s = parseBatchSummary(SUMMARY_OK);
    expect(s).not.toBeNull();
    expect(s!.sent).toBe(2);
    expect(s!.replied).toBe(1);
    expect(s!.timeout).toBe(1);
    expect(s!.skipped).toBeNull();
    expect(s!.nodeReplies).toEqual([
      { name: 'node-a', text: '已是最新版本' },
      { name: 'node-b', text: '(无返回数据)' },
    ]);
  });

  it('UpgradeTo「跳过{K}个」尾段容错解析', () => {
    const s = parseBatchSummary(SUMMARY_SKIP);
    expect(s).not.toBeNull();
    expect(s!.skipped).toBe(1);
    expect(s!.sent).toBe(2);
    expect(s!.nodeReplies).toEqual([]);
  });

  it('无逐节点段的纯汇总行', () => {
    const s = parseBatchSummary(SUMMARY_PLAIN);
    expect(s).not.toBeNull();
    expect(s!.sent).toBe(1);
    expect(s!.replied).toBe(1);
    expect(s!.timeout).toBe(0);
    expect(s!.nodeReplies).toEqual([]);
  });

  it('非契约文本（失败消息等）→ null', () => {
    expect(parseBatchSummary('批量操作失败：权限不足')).toBeNull();
    expect(parseBatchSummary('')).toBeNull();
  });
});

// ---------- 输入校验与确认框文案 ----------

describe('validateBatchInput', () => {
  it('未勾选 → 请先勾选节点', () => {
    expect(validateBatchInput(0, 'deploy/start')).toBe('请先勾选节点');
  });
  it('勾选但未输命令（仅 Execute 校验命令）→ 请输入命令', () => {
    expect(validateBatchInput(2, '')).toBe('请输入命令');
  });
  it('勾选且有命令 → 空串放行', () => {
    expect(validateBatchInput(2, 'deploy/start')).toBe('');
  });
});

describe('guardTexts', () => {
  it('重启系统确认文案：含台数与离线后果（C18）', () => {
    const g = guardTexts('Reboot', 3, '', '');
    expect(g.label).toContain('重启系统');
    expect(g.detail).toContain('3 台');
    expect(g.detail).toContain('短暂离线后自动恢复');
  });

  it('执行命令确认文案：回显命令与参数，参数空显「无」（C19）', () => {
    const g = guardTexts('Execute', 2, 'deploy/start', '{"name":"order-api"}');
    expect(g.label).toContain('执行命令');
    expect(g.detail).toContain('2 台');
    expect(g.detail).toContain('deploy/start');
    expect(g.detail).toContain('{"name":"order-api"}');
    expect(guardTexts('Execute', 2, 'deploy/start', '').detail).toContain('（参数：无）');
  });

  it('非 guard 动作返回空文案', () => {
    expect(guardTexts('CheckUpgrade', 1, '', '').label).toBe('');
  });
});

// ---------- 动作请求：GET + keys + 信封判定（迁移页 :178 同款） ----------

describe('runBatchAction', () => {
  const okReq: Req = (async (opts: ReqOptions) => {
    expect(opts.url).toBe('/Nodes/NodeOnline/CheckUpgrade');
    expect(opts.method).toBe('GET');
    expect(opts.params).toEqual({ keys: '11,12' });
    return { code: 0, message: SUMMARY_OK, data: null };
  }) as unknown as Req;

  it('成功：code 0 → ok，message=后端契约原文', async () => {
    const r = await runBatchAction(okReq, '/Nodes/NodeOnline/CheckUpgrade', '11,12');
    expect(r.ok).toBe(true);
    expect(r.message).toBe(SUMMARY_OK);
    expect(r.error).toBe('');
  });

  it('失败：code 400（200 锁定语义）→ 不 ok，error=message', async () => {
    const r = await runBatchAction(
      (async () => ({ code: 400, message: '权限不足', data: null })) as unknown as Req,
      '/Nodes/NodeOnline/Restart',
      '11',
    );
    expect(r.ok).toBe(false);
    expect(r.error).toBe('权限不足');
    expect(r.message).toBe('');
  });

  it('异常（网络/HTTP 错误）→ error 取 Error.message', async () => {
    const r = await runBatchAction(
      (async () => {
        throw new Error('Request failed with status code 500');
      }) as unknown as Req,
      '/Nodes/NodeOnline/Reboot',
      '11',
    );
    expect(r.ok).toBe(false);
    expect(r.error).toBe('Request failed with status code 500');
  });

  it('Execute：extra 注入 command/argument query 参', async () => {
    const seen: Array<Record<string, unknown>> = [];
    const req: Req = (async (opts: ReqOptions) => {
      seen.push(opts.params as Record<string, unknown>);
      return { code: 0, message: SUMMARY_OK, data: null };
    }) as unknown as Req;
    await runBatchAction(req, '/Nodes/NodeOnline/Execute', '11,12', {
      command: 'deploy/start',
      argument: '{"name":"order-api"}',
    });
    expect(seen[0]).toEqual({
      keys: '11,12',
      command: 'deploy/start',
      argument: '{"name":"order-api"}',
    });
  });
});
