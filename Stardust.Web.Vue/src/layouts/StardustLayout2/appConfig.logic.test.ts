// 配置页签纯逻辑单测（Vitest）。2026-10-29：子代理 B 配置/实例两页签，先红后绿。
import { describe, it, expect } from 'vitest';
import {
  configItemState,
  fetchConfigApp,
  fetchConfigHistory,
  fetchConfigItems,
  fetchOnlineCount,
  filterPending,
  historyVersionOf,
  publishConfig,
  rollbackHistory,
  toConfigHistory,
  toConfigItems,
} from './appConfig.logic';
import type { ConfigItem } from './appConfig.logic';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

/** 桩请求：按预设响应队列依次返回；抛错模拟网络失败 */
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

describe('configItemState · 行状态推导', () => {
  it('已发布：NewVersion 不大于生效版本、NewStatus 空', () => {
    expect(configItemState({ Version: 1, NewVersion: 0, Value: 'a' }, 1)).toBe('published');
    expect(configItemState({ version: 1, newVersion: 1 }, 1)).toBe('published');
  });

  it('已修改：原行已发布（Version>0）且草稿版本大于生效版本', () => {
    expect(configItemState({ Version: 1, NewVersion: 2 }, 1)).toBe('modified');
  });

  it('新增：行本身没有生效版本（Version=0），草稿版本大于生效版本', () => {
    expect(configItemState({ Version: 0, NewVersion: 2, NewValue: 'x' }, 1)).toBe('added');
  });

  it('待删除：NewStatus=[[Deleted]]，优先于其它判定', () => {
    expect(configItemState({ Version: 1, NewVersion: 2, NewStatus: '[[Deleted]]' }, 1)).toBe('deleted');
    expect(configItemState({ Version: 1, NewVersion: 0, NewStatus: '[[Deleted]]' }, 1)).toBe('deleted');
  });
});

describe('filterPending · 待发布行', () => {
  it('只留 pending 行', () => {
    const items: ConfigItem[] = [
      { id: 1, key: 'a', scope: '', value: '1', newValue: '1', state: 'published', pending: false },
      { id: 2, key: 'b', scope: '', value: '1', newValue: '2', state: 'modified', pending: true },
      { id: 3, key: 'c', scope: '', value: '', newValue: '3', state: 'added', pending: true },
    ];
    expect(filterPending(items).map((i) => i.key)).toEqual(['b', 'c']);
  });
});

describe('historyVersionOf · 快照解析', () => {
  it('优先取 NextVersion', () => {
    expect(historyVersionOf({ Remark: '{"Version":1,"NextVersion":2}' })).toBe(2);
  });
  it('NextVersion 无效时兜底 Version', () => {
    expect(historyVersionOf({ Remark: '{"Version":3}' })).toBe(3);
  });
  it('Remark 空或非法 JSON 返回 0', () => {
    expect(historyVersionOf({})).toBe(0);
    expect(historyVersionOf({ Remark: 'not-json' })).toBe(0);
  });
});

describe('fetchConfigApp · 取应用配置', () => {
  it('按 appId 命中行，字段大小写都认', async () => {
    const { req } = stubReq([
      { res: { data: [{ Id: 9, AppId: 32, Name: 'demo', Version: 1, NextVersion: 1, Enable: true }] } },
    ]);
    const cfg = await fetchConfigApp(req, 32);
    expect(cfg).toMatchObject({ id: 9, appId: 32, name: 'demo', version: 1, nextVersion: 1 });
  });

  it('appId 非法返回 null，不发请求', async () => {
    const { req, calls } = stubReq([]);
    expect(await fetchConfigApp(req, 0)).toBeNull();
    expect(calls).toHaveLength(0);
  });

  it('请求失败返回 null', async () => {
    const { req } = stubReq([{ err: new Error('401') }]);
    expect(await fetchConfigApp(req, 32)).toBeNull();
  });
});

describe('fetchConfigItems · 取配置键值', () => {
  it('映射 Key/Value/NewValue，并按生效版本算状态', async () => {
    const { req } = stubReq([
      {
        res: {
          data: [
            { Id: 1, Key: 'a.b', Value: '1', NewValue: '', Version: 1, NewVersion: 1 },
            { Id: 2, Key: 'c', Value: 'x', NewValue: 'y', Version: 1, NewVersion: 2 },
            { Id: 3, Key: 'd', Value: '', NewValue: 'z', Version: 0, NewVersion: 2 },
            { Id: 4, Key: 'e', Value: '1', NewVersion: 2, NewStatus: '[[Deleted]]' },
          ],
        },
      },
    ]);
    const items = await fetchConfigItems(req, 9, 1);
    expect(items?.map((i) => [i.key, i.state, i.pending])).toEqual([
      ['a.b', 'published', false],
      ['c', 'modified', true],
      ['d', 'added', true],
      ['e', 'deleted', true],
    ]);
  });

  it('configId<=0 返回空数组，请求失败返回 null', async () => {
    const ok = stubReq([]);
    expect(await fetchConfigItems(ok.req, 0, 1)).toEqual([]);
    expect(ok.calls).toHaveLength(0);
    const bad = stubReq([{ err: new Error('500') }]);
    expect(await fetchConfigItems(bad.req, 9, 1)).toBeNull();
  });
});

describe('toConfigHistory / fetchConfigHistory', () => {
  it('只留 Publish 与 Rollback，按时间倒序，解析快照版本', () => {
    const rows = toConfigHistory([
      { Id: 1, Action: 'Insert', CreateTime: '2026-10-01T10:00:00' },
      { Id: 2, Action: 'Update', CreateTime: '2026-10-02T10:00:00' },
      { Id: 3, Action: 'Publish', Success: true, Remark: '{"Version":1,"NextVersion":2}', CreateTime: '2026-10-03T10:00:00' },
      { Id: 4, Action: 'Publish', Success: true, Remark: '{"NextVersion":1}', CreateTime: '2026-10-02T10:00:00' },
    ]);
    expect(rows.map((r) => [r.id, r.action, r.version])).toEqual([
      [3, 'Publish', 2],
      [4, 'Publish', 1],
    ]);
    expect(rows[0].time).toBe('2026-10-03 10:00');
  });

  it('请求失败返回 null', async () => {
    const { req } = stubReq([{ err: new Error('500') }]);
    expect(await fetchConfigHistory(req, 9)).toBeNull();
  });
});

describe('fetchOnlineCount · 在线实例数', () => {
  it('返回行数', async () => {
    const { req } = stubReq([{ res: { data: [{ Id: 1 }, { Id: 2 }, { Id: 3 }] } }]);
    expect(await fetchOnlineCount(req, 32)).toBe(3);
  });
  it('失败返回 0', async () => {
    const { req } = stubReq([{ err: new Error('x') }]);
    expect(await fetchOnlineCount(req, 32)).toBe(0);
  });
});

describe('publishConfig / rollbackHistory', () => {
  it('发布成功：code 0 透传消息', async () => {
    const { req, calls } = stubReq([{ res: { code: 0, message: '发布成功！共通知2个应用' } }]);
    const r = await publishConfig(req, 9);
    expect(r.ok).toBe(true);
    expect(r.message).toContain('通知2个');
    expect(calls[0]).toMatchObject({ url: '/Configs/AppConfig/Publish', method: 'POST' });
  });

  it('发布失败：code 500 与网络错误都给出失败文案', async () => {
    const { req } = stubReq([{ res: { code: 500, message: '校验不过' } }]);
    expect((await publishConfig(req, 9)).ok).toBe(false);
    const bad = stubReq([{ err: new Error('timeout') }]);
    expect((await publishConfig(bad.req, 9)).ok).toBe(false);
  });

  it('configId<=0 不发请求', async () => {
    const { req, calls } = stubReq([]);
    expect((await publishConfig(req, 0)).ok).toBe(false);
    expect(calls).toHaveLength(0);
  });

  it('回滚成功与失败路径', async () => {
    const { req } = stubReq([{ res: { code: 0, message: '已回滚到版本 2，重发布 3 项' } }]);
    expect(await rollbackHistory(req, 7)).toMatchObject({ ok: true, message: '已回滚到版本 2，重发布 3 项' });
    const bad = stubReq([{ res: { code: 500, message: '仅发布历史支持回滚！' } }]);
    expect(await rollbackHistory(bad.req, 7)).toMatchObject({ ok: false, message: '仅发布历史支持回滚！' });
  });

  it('historyId<=0 不发请求', async () => {
    const { req, calls } = stubReq([]);
    expect((await rollbackHistory(req, 0)).ok).toBe(false);
    expect(calls).toHaveLength(0);
  });
});

describe('toConfigItems · 行映射细节', () => {
  it('空行兜底', () => {
    expect(toConfigItems([], 1)).toEqual([]);
  });
});
