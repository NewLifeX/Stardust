// 跨应用配置中心纯逻辑单测（Vitest）。2026-10-30。
import { describe, it, expect } from 'vitest';
import {
  buildAppMap,
  countPendingItems,
  countUsedKeys,
  diffConfigValues,
  fetchAllConfigs,
  fetchConfigDetail,
  filterRows,
  pendingDiff,
  toConfigCenterRows,
  toDetailHistory,
  toDetailItems,
} from './configCenter.logic';
import type { ConfigCenterRow } from './configCenter.logic';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

/** 按 URL 路由的桩 request：未登记的 URL 返回空列表，需要时抛错 */
function routingStub(handlers: Record<string, (opt: { params?: Record<string, unknown> }) => unknown>): Req {
  return async (opt) => {
    const url = String(opt.url ?? '');
    const h = handlers[url];
    if (h) return h(opt) as Awaited<ReturnType<Req>>;
    // fetchAppList 内部打的其余接口默认空数组，避免它 reject
    return { data: [] } as Awaited<ReturnType<Req>>;
  };
}

describe('countUsedKeys · UsedKeys 拆分计数', () => {
  it('逗号/分号/换行分隔的非空段计数', () => {
    expect(countUsedKeys('a,b,c')).toBe(3);
    expect(countUsedKeys('a;b\nc,d')).toBe(4);
    expect(countUsedKeys('')).toBe(0);
    expect(countUsedKeys(null)).toBe(0);
    expect(countUsedKeys('a,, ,b')).toBe(2);
  });
});

describe('buildAppMap · appId→名 映射', () => {
  it('DisplayName 优先，兜底 Name，大小写都认', () => {
    const m = buildAppMap([
      { Id: 1, DisplayName: '订单服务', Name: 'OrderApi' },
      { id: 2, displayName: '', name: 'IocWeb' },
      { Id: 0, Name: '无主' },
    ]);
    expect(m.get(1)).toBe('订单服务');
    expect(m.get(2)).toBe('IocWeb');
    expect(m.has(0)).toBe(false);
  });
});

describe('toConfigCenterRows · 归一', () => {
  const appMap = new Map<number, string>([[101, '订单服务'], [202, 'IocWeb']]);

  it('字段大小写都认，pending 由 nextVersion>version 推导，UsedKeys 计 keyCount', () => {
    const rows = toConfigCenterRows([
      { Id: 9, AppId: 101, Name: 'OrderApi', Version: 1, NextVersion: 2, Enable: true, UsedKeys: 'a,b', UpdateTime: '2026-10-28T10:00:00' },
      { id: 8, appId: 202, displayName: 'Iot 后台', version: 3, nextVersion: 3, enable: false, updateTime: '2026-10-27T09:00:00' },
    ], appMap);
    expect(rows).toHaveLength(2);
    // pending 行排前面
    expect(rows[0].appName).toBe('订单服务');
    expect(rows[0]).toMatchObject({ configId: 9, appId: 101, configName: 'OrderApi', version: 1, nextVersion: 2, pending: true, keyCount: 2 });
    expect(rows[0].lastPublishText).toBe('2026-10-28 10:00');
    expect(rows[1]).toMatchObject({ configId: 8, appId: 202, appName: 'IocWeb', configName: 'Iot 后台', pending: false, keyCount: 0 });
  });

  it('appMap 缺失时 appName 兜底用 AppConfig.Name', () => {
    const rows = toConfigCenterRows([
      { Id: 5, AppId: 999, Name: 'Standalone', Version: 1, NextVersion: 1 },
    ], new Map());
    expect(rows[0].appName).toBe('Standalone');
    expect(rows[0].configName).toBe('Standalone');
  });

  it('空输入返回空数组', () => {
    expect(toConfigCenterRows([], appMap)).toEqual([]);
  });
});

describe('filterRows · 搜索 + 应用过滤', () => {
  const rows: ConfigCenterRow[] = [
    { configId: 1, appId: 101, appName: '订单服务', configName: 'OrderApi', version: 1, nextVersion: 1, enable: true, keyCount: 2, valueSummary: '', lastPublishText: '2026-10-28 10:00', pending: false },
    { configId: 2, appId: 202, appName: 'IotWeb', configName: 'IotWeb', version: 1, nextVersion: 2, enable: true, keyCount: 0, valueSummary: '', lastPublishText: '—', pending: true },
    { configId: 3, appId: 303, appName: '支付网关', configName: 'PayGw', version: 1, nextVersion: 1, enable: true, keyCount: 5, valueSummary: '', lastPublishText: '—', pending: false },
  ];

  it('空关键词 + appId<=0 返回全部', () => {
    expect(filterRows(rows, { keyword: '', appId: 0 })).toHaveLength(3);
  });
  it('关键词匹配应用名或配置名（大小写不敏感）', () => {
    expect(filterRows(rows, { keyword: 'iot', appId: 0 }).map((r) => r.configId)).toEqual([2]);
    expect(filterRows(rows, { keyword: '订单', appId: 0 }).map((r) => r.configId)).toEqual([1]);
  });
  it('按 appId 过滤', () => {
    expect(filterRows(rows, { keyword: '', appId: 202 }).map((r) => r.configId)).toEqual([2]);
  });
  it('关键词与 appId 同时生效（AND）', () => {
    expect(filterRows(rows, { keyword: 'iot', appId: 101 })).toHaveLength(0);
    expect(filterRows(rows, { keyword: '', appId: 303 }).map((r) => r.configId)).toEqual([3]);
  });
});

describe('fetchAllConfigs · 拉列表', () => {
  it('并行拉 AppConfig + 应用列表，归一为行', async () => {
    const req = routingStub({
      '/Registry/App': () => ({ data: [{ Id: 101, DisplayName: '订单服务', Name: 'OrderApi' }] }),
      '/Configs/AppConfig': () => ({ data: [{ Id: 9, AppId: 101, Name: 'OrderApi', Version: 1, NextVersion: 2, Enable: true, UsedKeys: 'a,b', UpdateTime: '2026-10-28T10:00:00' }] }),
    });
    const r = await fetchAllConfigs(req, 7);
    expect(r.error).toBe('');
    expect(r.rows).toHaveLength(1);
    expect(r.rows[0]).toMatchObject({ appName: '订单服务', pending: true, keyCount: 2 });
  });

  it('应用列表失败不阻塞（名字兜底），AppConfig 正常', async () => {
    const req = routingStub({
      '/Registry/App': () => { throw new Error('boom'); },
      '/Configs/AppConfig': () => ({ data: [{ Id: 9, AppId: 101, Name: 'OrderApi', Version: 1, NextVersion: 1 }] }),
    });
    const r = await fetchAllConfigs(req, null);
    expect(r.error).toBe('');
    expect(r.rows[0].appName).toBe('OrderApi');
  });

  it('AppConfig 请求失败返回 error，rows 为空', async () => {
    const req = routingStub({
      '/Configs/AppConfig': () => { throw new Error('500'); },
    });
    const r = await fetchAllConfigs(req, 7);
    expect(r.rows).toEqual([]);
    expect(r.error).toContain('500');
  });
});

describe('fetchConfigDetail · 拉详情', () => {
  it('拉 ConfigData + ConfigHistory，归一 items/history/pendingCount', async () => {
    const req = routingStub({
      '/Configs/ConfigData': () => ({
        data: [
          { Id: 1, Key: 'a.b', Value: '1', NewValue: '1', Version: 1, NewVersion: 1 },
          { Id: 2, Key: 'c', Value: 'x', NewValue: 'y', Version: 1, NewVersion: 2 },
        ],
      }),
      '/Configs/ConfigHistory': () => ({
        data: [
          { Id: 21, Action: 'Publish', Success: true, Remark: '{"Version":1,"NextVersion":2}', CreateTime: '2026-10-28T09:30:00' },
          { Id: 20, Action: 'Insert', Success: true, CreateTime: '2026-10-20T09:30:00' },
        ],
      }),
    });
    const d = await fetchConfigDetail(req, 9, 101, '订单服务');
    expect(d).not.toBeNull();
    expect(d!.items.map((i) => [i.key, i.value, i.newValue])).toEqual([['a.b', '1', '1'], ['c', 'x', 'y']]);
    expect(d!.pendingCount).toBe(1); // c: x→y 不同
    expect(d!.history.map((h) => [h.action, h.version, h.time])).toEqual([
      ['Publish', 2, '2026-10-28 09:30'],
      ['Insert', 0, '2026-10-20 09:30'],
    ]);
  });

  it('configId<=0 返回 null，不发请求', async () => {
    let called = 0;
    const req: Req = async () => { called++; return { data: [] }; };
    expect(await fetchConfigDetail(req, 0, 1, 'x')).toBeNull();
    expect(called).toBe(0);
  });

  it('请求失败返回 null', async () => {
    const req = routingStub({
      '/Configs/ConfigData': () => { throw new Error('500'); },
    });
    expect(await fetchConfigDetail(req, 9, 101, 'x')).toBeNull();
  });
});

describe('diffConfigValues · 两版 KV diff', () => {
  it('added：新有旧无', () => {
    const d = diffConfigValues([], [{ Key: 'k1', Value: 'v1' }]);
    expect(d).toEqual([{ key: 'k1', oldVal: '', newVal: 'v1', state: 'added' }]);
  });
  it('removed：旧有新无', () => {
    const d = diffConfigValues([{ Key: 'k1', Value: 'v1' }], []);
    expect(d).toEqual([{ key: 'k1', oldVal: 'v1', newVal: '', state: 'removed' }]);
  });
  it('changed：都有但值不同', () => {
    const d = diffConfigValues([{ Key: 'k1', Value: 'old' }], [{ Key: 'k1', Value: 'new' }]);
    expect(d[0].state).toBe('changed');
    expect(d[0].oldVal).toBe('old');
    expect(d[0].newVal).toBe('new');
  });
  it('unchanged：都有且值相同', () => {
    const d = diffConfigValues([{ Key: 'k1', Value: 'same' }], [{ Key: 'k1', Value: 'same' }]);
    expect(d[0].state).toBe('unchanged');
  });
  it('混合四态并按 key 排序', () => {
    const oldRows = [{ Key: 'b', Value: '1' }, { Key: 'a', Value: 'keep' }, { Key: 'c', Value: 'gone' }];
    const newRows = [{ Key: 'a', Value: 'keep' }, { Key: 'b', Value: '2' }, { Key: 'd', Value: 'new' }];
    const d = diffConfigValues(oldRows, newRows);
    expect(d.map((x) => [x.key, x.state])).toEqual([
      ['a', 'unchanged'],
      ['b', 'changed'],
      ['c', 'removed'],
      ['d', 'added'],
    ]);
  });
});

describe('pendingDiff / countPendingItems · 待发布差异', () => {
  it('只返回非 unchanged，NewStatus 删除也算待发布', () => {
    const items = toDetailItems([
      { Id: 1, Key: 'a', Value: '1', NewValue: '1' },
      { Id: 2, Key: 'b', Value: 'x', NewValue: 'y' },
      { Id: 3, Key: 'c', Value: '1', NewValue: '', NewStatus: '[[Deleted]]' },
    ]);
    expect(countPendingItems(items)).toBe(2);
    const d = pendingDiff(items);
    // c 行仍在（Value=1,NewValue=''），Value→NewValue 对比为 changed（值清空），不是 removed
    expect(d.map((x) => [x.key, x.state])).toEqual([
      ['b', 'changed'],
      ['c', 'changed'],
    ]);
  });
});

describe('toDetailHistory · 保留全部动作', () => {
  it('Publish/Rollback/Insert/Update/Delete 都保留，按时间倒序', () => {
    const h = toDetailHistory([
      { Id: 1, Action: 'Insert', CreateTime: '2026-10-01T10:00:00' },
      { Id: 2, Action: 'Publish', Success: true, Remark: '{"NextVersion":2}', CreateTime: '2026-10-03T10:00:00' },
      { Id: 3, Action: 'Update', CreateTime: '2026-10-02T10:00:00' },
    ]);
    expect(h.map((x) => x.action)).toEqual(['Publish', 'Update', 'Insert']);
    expect(h[0].version).toBe(2);
  });
});
