// 配置页签纯逻辑单测（Vitest）。2026-10-29：子代理 B 配置/实例两页签，先红后绿。
import { describe, it, expect } from 'vitest';
import {
  buildDiffRows,
  configItemPutBody,
  configItemState,
  createConfigItem,
  deleteConfigItem,
  fetchConfigApp,
  fetchConfigHistory,
  fetchConfigHistoryDiff,
  fetchConfigItems,
  fetchOnlineCount,
  filterPending,
  historyVersionOf,
  publishConfig,
  rollbackHistory,
  saveConfigItem,
  syncAppConfig,
  toConfigHistory,
  toConfigHistoryDiff,
  toConfigItems,
  validateConfigItemForm,
} from './appConfig.logic';
import type { ConfigHistoryDiff, ConfigItem } from './appConfig.logic';
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
      { id: 1, key: 'a', scope: '', value: '1', newValue: '1', enable: true, state: 'published', pending: false },
      { id: 2, key: 'b', scope: '', value: '1', newValue: '2', enable: true, state: 'modified', pending: true },
      { id: 3, key: 'c', scope: '', value: '', newValue: '3', enable: true, state: 'added', pending: true },
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

describe('toConfigHistoryDiff / buildDiffRows / fetchConfigHistoryDiff · 历史版本对比', () => {
  const diff: ConfigHistoryDiff = {
    id: 21,
    configId: 10,
    oldJson: '{"Id":10,"Version":1,"NextVersion":2,"Enable":true,"Name":"demo"}',
    newJson: '{"Id":10,"Version":2,"NextVersion":3,"Enable":false,"Name":"demo"}',
    changedKeys: ['Enable', 'NextVersion', 'Version'],
  };

  it('响应解析：data 五字段，oldJson 空→null，changedKeys 非数组兜底 []，缺 newJson 无效', () => {
    expect(toConfigHistoryDiff({ data: { id: 21, configId: 10, newJson: '{"a":1}', oldJson: null, changedKeys: ['a'] } })).toEqual({
      id: 21,
      configId: 10,
      oldJson: null,
      newJson: '{"a":1}',
      changedKeys: ['a'],
    });
    expect(toConfigHistoryDiff({ data: { newJson: 'x', oldJson: '', changedKeys: 'bad' } })).toEqual({
      id: 0,
      configId: 0,
      oldJson: null,
      newJson: 'x',
      changedKeys: [],
    });
    expect(toConfigHistoryDiff({ data: { oldJson: 'x' } })).toBeNull();
    expect(toConfigHistoryDiff(undefined)).toBeNull();
  });

  it('buildDiffRows：changed 值序列化 + added/removed 归类', () => {
    expect(buildDiffRows(diff)).toEqual([
      { key: 'Enable', kind: 'changed', oldValue: 'true', newValue: 'false' },
      { key: 'NextVersion', kind: 'changed', oldValue: '2', newValue: '3' },
      { key: 'Version', kind: 'changed', oldValue: '1', newValue: '2' },
    ]);
    const d2: ConfigHistoryDiff = {
      id: 1,
      configId: 1,
      oldJson: '{"Keep":"v","Gone":1,"Empty":""}',
      newJson: '{"Keep":"v2","Fresh":{"a":1},"Empty":""}',
      changedKeys: ['Empty', 'Fresh', 'Gone', 'Keep'],
    };
    expect(buildDiffRows(d2)).toEqual([
      { key: 'Empty', kind: 'changed', oldValue: '（空）', newValue: '（空）' },
      { key: 'Fresh', kind: 'added', oldValue: '—', newValue: '{"a":1}' },
      { key: 'Gone', kind: 'removed', oldValue: '1', newValue: '—' },
      { key: 'Keep', kind: 'changed', oldValue: 'v', newValue: 'v2' },
    ]);
  });

  it('buildDiffRows：首次发布 oldJson=null 全键 added；坏 JSON 侧按缺键；changedKeys 空=无差异', () => {
    const first: ConfigHistoryDiff = { id: 1, configId: 1, oldJson: null, newJson: '{"Version":1}', changedKeys: ['Version'] };
    expect(buildDiffRows(first)).toEqual([{ key: 'Version', kind: 'added', oldValue: '—', newValue: '1' }]);
    const broken: ConfigHistoryDiff = { id: 1, configId: 1, oldJson: '{bad', newJson: '{"Version":1}', changedKeys: ['Version'] };
    expect(buildDiffRows(broken)[0]).toMatchObject({ kind: 'added' });
    const brokenNew: ConfigHistoryDiff = { id: 1, configId: 1, oldJson: '{"Version":1}', newJson: 'not-json', changedKeys: ['Version'] };
    expect(buildDiffRows(brokenNew)[0]).toMatchObject({ kind: 'removed' });
    expect(buildDiffRows({ ...first, changedKeys: [] })).toEqual([]);
  });

  it('fetchConfigHistoryDiff：GET Diff?id= 透传；失败/缺参返回 null', async () => {
    const { req, calls } = stubReq([
      { res: { data: { id: 21, configId: 10, newJson: '{"Version":2}', oldJson: '{"Version":1}', changedKeys: ['Version'] } } },
    ]);
    const d = await fetchConfigHistoryDiff(req, 21);
    expect(d).toMatchObject({ id: 21, changedKeys: ['Version'] });
    expect(calls[0]).toMatchObject({ url: '/Configs/ConfigHistory/Diff', method: 'GET', params: { id: 21 } });
    const { req: req2 } = stubReq([{ err: new Error('500') }]);
    expect(await fetchConfigHistoryDiff(req2, 21)).toBeNull();
    const { req: req3 } = stubReq([]);
    expect(await fetchConfigHistoryDiff(req3, 0)).toBeNull();
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

  it('enable 取自行 Enable 字段', () => {
    const rows = toConfigItems([{ Id: 1, Key: 'a', Value: '1', NewValue: '1', Version: 1, NewVersion: 0, Enable: false }], 1);
    expect(rows[0].enable).toBe(false);
  });
});

// ==================== F10 配置项就地编辑（ConfigData CRUD + SyncApp） ====================

describe('F10 validateConfigItemForm / configItemPutBody', () => {
  it('Key 必填', () => {
    const m = { key: '  ', scope: '', value: '1', newValue: '', enable: true, remark: '' };
    expect(validateConfigItemForm(m, false)).toContain('必填');
    expect(validateConfigItemForm({ ...m, key: 'a' }, false)).toBe('');
  });

  it('已发布行改 Key/Scope 前置拦截', () => {
    const m = { key: 'b', scope: 'x', value: '1', newValue: '', enable: true, remark: '' };
    expect(validateConfigItemForm(m, true, { key: 'a', scope: 'x' })).toContain('不能修改名称与作用域');
    expect(validateConfigItemForm(m, true, { key: 'b', scope: 'x' })).toBe('');
  });

  it('PUT body 分支：已发布只动 NewValue/Remark，Enable 变化才提交', () => {
    const m = { key: 'a', scope: '', value: '1', newValue: '2', enable: true, remark: 'r' };
    expect(configItemPutBody(m, true, true)).toEqual({ NewValue: '2', Remark: 'r' });
    expect(configItemPutBody(m, true, false)).toEqual({ NewValue: '2', Remark: 'r', Enable: true });
  });

  it('PUT body 分支：未发布行直接改 Key/Scope/Value/Enable', () => {
    const m = { key: 'a', scope: 'dev', value: '2', newValue: '', enable: false, remark: '' };
    expect(configItemPutBody(m, false, true)).toEqual({ Key: 'a', Scope: 'dev', Value: '2', Enable: false, Remark: '' });
  });
});

describe('F10 配置项 CRUD', () => {
  it('saveConfigItem：PUT /Configs/ConfigData/{id}，body 按发布态', async () => {
    const { req, calls } = stubReq([{ res: { code: 0, message: '操作成功' } }]);
    const r = await saveConfigItem(req, { id: 5, published: true, originalEnable: true }, { key: 'a', scope: '', value: '1', newValue: '2', enable: true, remark: '' });
    expect(r.ok).toBe(true);
    expect(calls[0]).toMatchObject({ url: '/Configs/ConfigData/5', method: 'PUT' });
    expect((calls[0] as { data: Record<string, unknown> }).data).toEqual({ NewValue: '2', Remark: '' });
  });

  it('saveConfigItem：Enable 翻转双向进 body，未变不进（originalEnable 基准，DEF-C12 回归）', async () => {
    const { req, calls } = stubReq([{ res: { code: 0, message: '操作成功' } }, { res: { code: 0, message: '操作成功' } }, { res: { code: 0, message: '操作成功' } }]);
    const form0 = { key: 'a', scope: '', value: '1', newValue: '2', remark: '' };
    // true→false：原启用，现 uncheck → body.Enable=false（后端 OnUpdate 转 NewStatus 禁用草稿）
    await saveConfigItem(req, { id: 9, published: true, originalEnable: true }, { ...form0, enable: false });
    expect((calls[0] as { data: Record<string, unknown> }).data).toEqual({ NewValue: '2', Remark: '', Enable: false });
    // false→true：原禁用，现勾上 → body.Enable=true（NewStatus 启用草稿）
    await saveConfigItem(req, { id: 9, published: true, originalEnable: false }, { ...form0, enable: true });
    expect((calls[1] as { data: Record<string, unknown> }).data).toEqual({ NewValue: '2', Remark: '', Enable: true });
    // 未变：Enable 不进 body
    await saveConfigItem(req, { id: 9, published: true, originalEnable: true }, { ...form0, enable: true });
    expect('Enable' in (calls[2] as { data: Record<string, unknown> }).data).toBe(false);
  });

  it('saveConfigItem：失败 code 500 透传消息', async () => {
    const { req } = stubReq([{ res: { code: 500, message: '在用值不能修改！' } }]);
    const r = await saveConfigItem(req, { id: 5, published: true, originalEnable: true }, { key: 'a', scope: '', value: '1', newValue: '2', enable: true, remark: '' });
    expect(r).toMatchObject({ ok: false, message: '在用值不能修改！' });
  });

  it('saveConfigItem：缺 id 不发请求', async () => {
    const { req, calls } = stubReq([]);
    expect((await saveConfigItem(req, { id: 0, published: false, originalEnable: true }, { key: 'a', scope: '', value: '', newValue: '', enable: true, remark: '' })).ok).toBe(false);
    expect(calls).toHaveLength(0);
  });

  it('createConfigItem：POST /Configs/ConfigData 带 ConfigId', async () => {
    const { req, calls } = stubReq([{ res: { code: 0, message: '操作成功' } }]);
    const r = await createConfigItem(req, 9, { key: 'k1', scope: '', value: 'v1', newValue: '', enable: true, remark: '' });
    expect(r.ok).toBe(true);
    expect(calls[0]).toMatchObject({ url: '/Configs/ConfigData', method: 'POST' });
    expect((calls[0] as { data: Record<string, unknown> }).data).toEqual({ ConfigId: 9, Key: 'k1', Scope: '', Value: 'v1', Enable: true, Remark: '' });
  });

  it('createConfigItem：缺 configId 不发请求', async () => {
    const { req, calls } = stubReq([]);
    expect((await createConfigItem(req, 0, { key: 'k', scope: '', value: '', newValue: '', enable: true, remark: '' })).ok).toBe(false);
    expect(calls).toHaveLength(0);
  });

  it('deleteConfigItem：DELETE /{id}；404 走「接口部署中」', async () => {
    const { req, calls } = stubReq([{ res: { code: 0, message: '操作成功' } }]);
    expect((await deleteConfigItem(req, 7)).ok).toBe(true);
    expect(calls[0]).toEqual({ url: '/Configs/ConfigData/7', method: 'DELETE' });
    const err = Object.assign(new Error('Request failed with status code 404'), { status: 404 });
    const bad = stubReq([{ err }]);
    const r = await deleteConfigItem(bad.req, 7);
    expect(r.ok).toBe(false);
    expect(r.message).toContain('接口部署中');
  });

  it('syncAppConfig：POST /Configs/AppConfig/SyncApp 透传「成功同步N个应用！」', async () => {
    const { req, calls } = stubReq([{ res: { code: 0, message: '成功同步[3]个应用！' } }]);
    const r = await syncAppConfig(req);
    expect(r).toMatchObject({ ok: true, message: '成功同步[3]个应用！' });
    expect(calls[0]).toEqual({ url: '/Configs/AppConfig/SyncApp', method: 'POST' });
  });

  it('syncAppConfig：网络失败给出失败文案', async () => {
    const { req } = stubReq([{ err: new Error('boom') }]);
    expect((await syncAppConfig(req)).ok).toBe(false);
  });
});
