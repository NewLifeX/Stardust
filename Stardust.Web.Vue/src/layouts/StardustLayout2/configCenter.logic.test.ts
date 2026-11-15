// 跨应用配置中心纯逻辑单测（Vitest）。2026-10-30。
import { describe, it, expect } from 'vitest';
import {
  buildAppMap,
  buildKeyCounts,
  countPendingItems,
  countUsedKeys,
  createAppRule,
  dateRangeToMs,
  deleteAppRule,
  diffConfigValues,
  fetchAllConfigs,
  fetchAppRules,
  fetchConfigCategories,
  fetchConfigDetail,
  fetchConfigPreview,
  filterRows,
  pendingDiff,
  saveAppRule,
  toAppRuleRows,
  toConfigCenterRows,
  toDetailHistory,
  toDetailItems,
  toPreviewInfo,
  validateAppRuleForm,
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

  it('Category 与 UpdateTime 毫秒一并归一（候选 A 筛选数据源）', () => {
    const rows = toConfigCenterRows([
      { Id: 9, AppId: 101, Name: 'OrderApi', Version: 1, NextVersion: 1, Category: '公共', UpdateTime: '2026-10-28T10:00:00' },
      { id: 8, appId: 202, name: 'IotWeb', version: 1, nextVersion: 1, category: '物联网' },
    ], appMap);
    const order = rows.find((r) => r.configId === 9)!;
    const iot = rows.find((r) => r.configId === 8)!;
    expect(order.category).toBe('公共');
    expect(order.lastPublishMs).toBe(Date.parse('2026-10-28T10:00:00'));
    expect(iot.category).toBe('物联网');
    expect(iot.lastPublishMs).toBe(0);
    expect(iot.lastPublishText).toBe('—');
  });

  it('空输入返回空数组', () => {
    expect(toConfigCenterRows([], appMap)).toEqual([]);
  });
});

describe('filterRows · 搜索 + 应用 + 类别 + 日期区间过滤', () => {
  const rows: ConfigCenterRow[] = [
    { configId: 1, appId: 101, appName: '订单服务', configName: 'OrderApi', version: 1, nextVersion: 1, enable: true, keyCount: 2, valueSummary: '', lastPublishText: '2026-10-28 10:00', lastPublishMs: Date.parse('2026-10-28T10:00:00'), category: '公共', pending: false },
    { configId: 2, appId: 202, appName: 'IotWeb', configName: 'IotWeb', version: 1, nextVersion: 2, enable: true, keyCount: 0, valueSummary: '', lastPublishText: '—', lastPublishMs: 0, category: '物联网', pending: true },
    { configId: 3, appId: 303, appName: '支付网关', configName: 'PayGw', version: 1, nextVersion: 1, enable: true, keyCount: 5, valueSummary: '', lastPublishText: '—', lastPublishMs: 0, category: '公共', pending: false },
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
  it('category 空串/缺省 = 全部类别；指定类别只留匹配行', () => {
    expect(filterRows(rows, { keyword: '', appId: 0, category: '' })).toHaveLength(3);
    expect(filterRows(rows, { keyword: '', appId: 0 }).map((r) => r.configId)).toEqual([1, 2, 3]);
    expect(filterRows(rows, { keyword: '', appId: 0, category: '公共' }).map((r) => r.configId)).toEqual([1, 3]);
    expect(filterRows(rows, { keyword: '', appId: 0, category: '物联网' }).map((r) => r.configId)).toEqual([2]);
  });
  it('dateFrom/dateTo 0 = 不限；闭区间过滤 lastPublishMs（lastPublishMs=0 的行在区间内不算命中）', () => {
    const from = Date.parse('2026-10-28T00:00:00');
    const to = Date.parse('2026-10-28T23:59:59.999');
    expect(filterRows(rows, { keyword: '', appId: 0 })).toHaveLength(3);
    expect(filterRows(rows, { keyword: '', appId: 0, dateFrom: from, dateTo: to }).map((r) => r.configId)).toEqual([1]);
    expect(filterRows(rows, { keyword: '', appId: 0, dateFrom: from }).map((r) => r.configId)).toEqual([1]);
    expect(filterRows(rows, { keyword: '', appId: 0, dateTo: to }).map((r) => r.configId)).toEqual([1]);
  });
  it('类别 + 关键字 + 应用组合查询（AND）', () => {
    expect(filterRows(rows, { keyword: 'pay', appId: 0, category: '公共' }).map((r) => r.configId)).toEqual([3]);
    expect(filterRows(rows, { keyword: 'iot', appId: 0, category: '公共' })).toHaveLength(0);
    expect(filterRows(rows, { keyword: '', appId: 101, category: '公共' }).map((r) => r.configId)).toEqual([1]);
  });
});

describe('dateRangeToMs · 日期控件 → 当地时区当天闭区间', () => {
  it('yyyy-MM-dd → 当天 00:00:00.000 ~ 23:59:59.999', () => {
    const { from, to } = dateRangeToMs('2026-10-28', '2026-10-29');
    expect(from).toBe(new Date(2026, 9, 28).getTime());
    expect(to).toBe(new Date(2026, 9, 29).getTime() + 24 * 3600 * 1000 - 1);
    expect(to - from).toBe(2 * 24 * 3600 * 1000 - 1);
  });
  it('空串/非法格式返回 0（不过滤）', () => {
    expect(dateRangeToMs('', '')).toEqual({ from: 0, to: 0 });
    expect(dateRangeToMs('abc', '2026-10-28')).toEqual({ from: 0, to: new Date(2026, 9, 28).getTime() + 24 * 3600 * 1000 - 1 });
    expect(dateRangeToMs('2026-10-28', '')).toEqual({ from: new Date(2026, 9, 28).getTime(), to: 0 });
  });
});

describe('fetchAllConfigs · 拉列表', () => {
  it('并行拉 AppConfig + 应用列表，归一为行（PageSize：AppConfig 500 / ConfigData 1000，B2 分页断言）', async () => {
    const paramsSeen: Record<string, Record<string, unknown> | undefined> = {};
    const req = routingStub({
      '/Registry/App': () => ({ data: [{ Id: 101, DisplayName: '订单服务', Name: 'OrderApi' }] }),
      '/Configs/AppConfig': (opt) => {
        paramsSeen['/Configs/AppConfig'] = opt.params;
        return { data: [{ Id: 9, AppId: 101, Name: 'OrderApi', Version: 1, NextVersion: 2, Enable: true, UsedKeys: 'a,b', UpdateTime: '2026-10-28T10:00:00' }] };
      },
      '/Configs/ConfigData': (opt) => {
        paramsSeen['/Configs/ConfigData'] = opt.params;
        return { data: [] };
      },
    });
    const r = await fetchAllConfigs(req, 7);
    expect(r.error).toBe('');
    expect(r.rows).toHaveLength(1);
    expect(r.rows[0]).toMatchObject({ appName: '订单服务', pending: true, keyCount: 2 });
    expect(paramsSeen['/Configs/AppConfig']).toMatchObject({ PageSize: 500 });
    expect(paramsSeen['/Configs/ConfigData']).toMatchObject({ PageSize: 1000 });
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

describe('fetchConfigCategories · 类别选项（C05）', () => {
  // F-CFG-1（2026-11-10 review-walk 实锤）：后端 Categories 真返字典 {类别: " (N)"}（live 实证 {"":" (1)",...}），
  // 旧实现只认数组 → 恒 null → 下拉恒走行内去重降级。主用例改用字典形状防掩盖；数组分支保留兼容。
  it('字典形状（后端真形）：取键为选项，去空白、排除空键（空串与「全部类别」哨兵冲突）', async () => {
    const req = routingStub({
      '/Configs/AppConfig/Categories': () => ({ data: { '': ' (1)', '公共': ' (2)', ' 物联网 ': ' (1)' } }),
    });
    expect(await fetchConfigCategories(req)).toEqual(['公共', '物联网']);
  });
  it('数组形状兼容（历史行为）：去空白去空项', async () => {
    const req = routingStub({
      '/Configs/AppConfig/Categories': () => ({ data: ['公共', ' 物联网 ', '', null, '公共'] }),
    });
    expect(await fetchConfigCategories(req)).toEqual(['公共', '物联网']);
  });
  it('请求失败/非数组非字典（标量）返回 null（调用方降级行内去重）', async () => {
    const req: Req = async () => { throw new Error('boom'); };
    expect(await fetchConfigCategories(req)).toBeNull();
    const scalar: Req = async () => ({ data: 'nope' });
    expect(await fetchConfigCategories(scalar)).toBeNull();
  });
});

describe('toPreviewInfo · /config/getall 响应归一（C19）', () => {
  it('ConfigInfo 字段归一，Configs 字典浅拷贝为键值对象', () => {
    const p = toPreviewInfo({
      Version: 5, Scope: 'dev', NextVersion: 6, NextPublish: '2026-11-01 08:00',
      UpdateTime: '2026-11-01 09:00', Configs: { 'a.b': '1', empty: null },
    });
    expect(p).toEqual({
      appName: '', version: 5, scope: 'dev', nextVersion: 6,
      nextPublish: '2026-11-01 08:00', updateTime: '2026-11-01 09:00',
      configs: { 'a.b': '1', empty: '' },
    });
  });
  it('空输入/缺 Configs 返回空配置对象', () => {
    expect(toPreviewInfo(undefined).configs).toEqual({});
    expect(toPreviewInfo({ Version: 1 }).configs).toEqual({});
  });
});

describe('fetchConfigPreview · 预览链取数（候选 B）', () => {
  it('先查 App 行（Id 参数=后端 Search 只认 p["Id"]，DEF-4）仅取 Name/DisplayName，再直连 GET {base}/Config/GetAllByApp?appId={Id}（刀1：Secret 服务端内解析零经手，2026-11-12）', async () => {
    const calls: { url: string; params?: Record<string, unknown> }[] = [];
    const req: Req = async (opt) => {
      calls.push({ url: String(opt.url), params: opt.params });
      // DEF-4：AppController.Search 只读 p["Id"]（AppController.cs:53-58）；传 appId 会被忽略，
      // 返回全表第一页第一行（live 实证：appId=11 返回 id=12）。
      expect(opt.params).toMatchObject({ Id: 101, PageSize: 1 });
      expect(opt.params).not.toHaveProperty('appId');
      // 刀2 前瞻：行内无 Secret（裁剪态）本链不破——Secret 已不从行读取
      return { data: [{ Id: 101, Name: 'OrderApi', DisplayName: '订单服务' }] };
    };
    // DEF-6（2026-11-10，j-appreg live 定谳）：实体 request 通道对非服务路径一律拼 /api 前缀
    // （api-core resolveRequestUrl）→ 专用端点走独立直连通道（同 fetchWebhookFields 范式）。
    // 刀1：/Config/GetAllByApp 按 Id 取数，appId/version 走 querystring，无 secret 键。
    const fetchCalls: string[] = [];
    const fetchImpl = (async (url: string) => {
      fetchCalls.push(String(url));
      return { ok: true, status: 200, json: async () => ({ code: 0, data: { Version: 5, Scope: 'dev', NextVersion: 5, Configs: { 'a.b': '1' } } }) };
    }) as unknown as typeof fetch;
    const p = await fetchConfigPreview(req, 101, { base: 'http://6681', fetchImpl });
    expect(p).not.toBeNull();
    // 实体通道只发 App 查找（配置实拉走直连通道）。
    expect(calls).toHaveLength(1);
    expect(calls[0]).toMatchObject({ url: '/Registry/App', params: { Id: 101, PageSize: 1 } });
    expect(fetchCalls).toEqual(['http://6681/Config/GetAllByApp?appId=101&version=0']);
    expect(fetchCalls[0]).not.toContain('secret');
    expect(p!.appName).toBe('订单服务');
    expect(p!.version).toBe(5);
    expect(p!.configs).toEqual({ 'a.b': '1' });
  });
  it('App 行缺失（查不到 Name）返回 null', async () => {
    const req: Req = async () => ({ data: [] });
    expect(await fetchConfigPreview(req, 404)).toBeNull();
  });
  it('appId<=0 直接返回 null，不发请求', async () => {
    let called = 0;
    const req: Req = async () => { called++; return { data: [] }; };
    expect(await fetchConfigPreview(req, 0)).toBeNull();
    expect(called).toBe(0);
  });
  it('getall 直连请求失败（非 2xx/抛错/无 base）返回 null（弹层显示加载失败）', async () => {
    const req: Req = async () => ({ data: [{ Id: 1, Name: 'X' }] });
    const notOk = (async () => ({ ok: false, status: 404, json: async () => ({}) })) as unknown as typeof fetch;
    expect(await fetchConfigPreview(req, 1, { base: 'http://x', fetchImpl: notOk })).toBeNull();
    const boom = (async () => { throw new Error('net'); }) as unknown as typeof fetch;
    expect(await fetchConfigPreview(req, 1, { base: 'http://x', fetchImpl: boom })).toBeNull();
    // 无 base（单测环境无 VITE_APP_BASE_API 兜底）→ 直接降级
    expect(await fetchConfigPreview(req, 1)).toBeNull();
  });
  it('DEF-6 直连带 Bearer：token 取自 cubeAxios 同源 getAccessToken（localStorage key=config tokenKey），头=「bearer {token}」', async () => {
    const req: Req = async () => ({ data: [{ Id: 1, Name: 'X' }] });
    let capturedInit: RequestInit | undefined;
    const capture = (async (_url: string | URL, init?: RequestInit) => {
      capturedInit = init;
      return { ok: true, status: 200, json: async () => ({ code: 0, data: { Version: 1, Configs: { k: 'v' } } }) };
    }) as unknown as typeof fetch;
    localStorage.setItem('token', 'jwt-def6-test');
    try {
      await fetchConfigPreview(req, 1, { base: 'http://6681', fetchImpl: capture });
      const headers = (capturedInit?.headers ?? {}) as Record<string, string>;
      expect(headers['Authorization']).toBe('bearer jwt-def6-test');
      expect(headers['Accept']).toBe('application/json');
    } finally {
      localStorage.removeItem('token');
    }
  });
  it('DEF-6 信封失败：HTTP 200 但 body code=401（未登录）→ null（弹层失败态），严禁伪空态', async () => {
    const req: Req = async () => ({ data: [{ Id: 1, Name: 'X' }] });
    const unauthorized = (async () => ({
      ok: true,
      status: 200,
      json: async () => ({ code: 401, message: '未登录！' }),
    })) as unknown as typeof fetch;
    const p = await fetchConfigPreview(req, 1, { base: 'http://6681', fetchImpl: unauthorized });
    expect(p).toBeNull();
  });
  it('DEF-6 真空态不误伤：code=0 且 Configs 为空 → 非 null、configs={}（真空态文案，非失败态）', async () => {
    const req: Req = async () => ({ data: [{ Id: 1, Name: 'X', Secret: 's' }] });
    const emptyOk = (async () => ({
      ok: true,
      status: 200,
      json: async () => ({ code: 0, data: { Version: 1, Configs: {} } }),
    })) as unknown as typeof fetch;
    const p = await fetchConfigPreview(req, 1, { base: 'http://6681', fetchImpl: emptyOk });
    expect(p).not.toBeNull();
    expect(p!.configs).toEqual({});
  });
});

describe('fetchConfigDetail · 拉详情', () => {
  it('拉 ConfigData + ConfigHistory，归一 items/history/pendingCount（PageSize：ConfigData 500 / ConfigHistory 50，B2 分页断言）', async () => {
    const paramsSeen: Record<string, Record<string, unknown> | undefined> = {};
    const req = routingStub({
      '/Configs/ConfigData': (opt) => {
        paramsSeen['/Configs/ConfigData'] = opt.params;
        return {
          data: [
            { Id: 1, Key: 'a.b', Value: '1', NewValue: '1', Version: 1, NewVersion: 1 },
            { Id: 2, Key: 'c', Value: 'x', NewValue: 'y', Version: 1, NewVersion: 2 },
          ],
        };
      },
      '/Configs/ConfigHistory': (opt) => {
        paramsSeen['/Configs/ConfigHistory'] = opt.params;
        return {
          data: [
            { Id: 21, Action: 'Publish', Success: true, Remark: '{"Version":1,"NextVersion":2}', CreateTime: '2026-10-28T09:30:00' },
            { Id: 20, Action: 'Insert', Success: true, CreateTime: '2026-10-20T09:30:00' },
          ],
        };
      },
    });
    const d = await fetchConfigDetail(req, 9, 101, '订单服务');
    expect(d).not.toBeNull();
    expect(paramsSeen['/Configs/ConfigData']).toMatchObject({ configId: 9, PageSize: 500 });
    expect(paramsSeen['/Configs/ConfigHistory']).toMatchObject({ configId: 9, PageSize: 50 });
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

  it('DEF-3：NewValue 为 null（后端未给草稿）不算差异（configId=9 实况）', () => {
    const items = toDetailItems([
      { Id: 1, Key: 'ServerName', Value: 'mcp-api-value' }, // NewValue 未返回 = null → ''，无草稿
      { Id: 2, Key: 'Password', Value: 'top-secret-password' },
      { Id: 3, Key: 'McpApiTestKey', Value: 'value' },
    ]);
    expect(pendingDiff(items)).toEqual([]);
    expect(countPendingItems(items)).toBe(0);
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

// ==================== F10 /configs「Key 数」修复 ====================

describe('buildKeyCounts · ConfigData 行数计 Key 数', () => {
  it('按 configId 分组计数，[[Deleted]] 不计', () => {
    const m = buildKeyCounts([
      { ConfigId: 9, NewStatus: '' },
      { ConfigId: 9, NewStatus: '' },
      { ConfigId: 9, NewStatus: '[[Deleted]]' },
      { ConfigId: 8, NewStatus: '' },
      { ConfigId: 0 },
    ]);
    expect(m.get(9)).toBe(2);
    expect(m.get(8)).toBe(1);
    expect(m.size).toBe(2);
  });

  it('DEF-2：全部行都待删除时计 0（不回退 UsedKeys 上报值）', () => {
    const m = buildKeyCounts([
      { ConfigId: 6, NewStatus: '[[Deleted]]' },
      { ConfigId: 6, NewStatus: '[[Deleted]]' },
    ]);
    expect(m.get(6)).toBe(0);
    expect(m.size).toBe(1);
  });
});

describe('fetchAllConfigs · Key 数取 ConfigData 行数（F10 修复）', () => {
  it('多拉一枪 /Configs/ConfigData 全量，按 configId 计数并覆盖 UsedKeys', async () => {
    const req = routingStub({
      '/Registry/App': () => ({ data: [{ Id: 101, DisplayName: '订单服务' }] }),
      '/Configs/AppConfig': () => ({ data: [{ Id: 9, AppId: 101, Name: 'OrderApi', Version: 1, NextVersion: 2, UsedKeys: 'a,b,c' }] }),
      '/Configs/ConfigData': () => ({ data: [{ ConfigId: 9 }, { ConfigId: 9 }, { ConfigId: 9, NewStatus: '[[Deleted]]' }, { ConfigId: 8 }] }),
    });
    const r = await fetchAllConfigs(req, 7);
    expect(r.error).toBe('');
    expect(r.rows[0].keyCount).toBe(2); // 3 行里 1 行待删除 → 2，而非 UsedKeys 的 3
  });

  it('ConfigData 拉取失败时回退 UsedKeys 计数', async () => {
    const req = routingStub({
      '/Registry/App': () => ({ data: [] }),
      '/Configs/AppConfig': () => ({ data: [{ Id: 9, AppId: 1, Name: 'X', Version: 1, NextVersion: 1, UsedKeys: 'a,b' }] }),
      '/Configs/ConfigData': () => { throw new Error('boom'); },
    });
    const r = await fetchAllConfigs(req, 7);
    expect(r.error).toBe('');
    expect(r.rows[0].keyCount).toBe(2);
  });
});

// ==================== F11 应用规则（AppRule） ====================

describe('F11 toAppRuleRows / validateAppRuleForm', () => {
  it('行归一 + 按优先级倒序', () => {
    const rows = toAppRuleRows([
      { Id: 1, Rule: 'IP=10.*', Result: 'Scope=prod', Priority: 1, Enable: true, UpdateTime: '2026-11-03T10:00:00' },
      { Id: 2, Rule: 'IP=172.*', Result: 'Scope=dev', Priority: 9, Enable: false, Remark: '测试段' },
    ]);
    expect(rows.map((r) => r.id)).toEqual([2, 1]);
    expect(rows[0]).toMatchObject({ rule: 'IP=172.*', result: 'Scope=dev', priority: 9, enable: false, remark: '测试段' });
    expect(rows[1].updateTimeText).toBe('2026-11-03 10:00');
  });

  it('校验：Rule/Result 必填，Priority 非负数字', () => {
    expect(validateAppRuleForm({ rule: '', result: 'a', priority: 0, enable: true, remark: '' })).toContain('规则表达式必填');
    expect(validateAppRuleForm({ rule: 'a', result: '', priority: 0, enable: true, remark: '' })).toContain('命中结果必填');
    expect(validateAppRuleForm({ rule: 'a', result: 'b', priority: -1, enable: true, remark: '' })).toContain('优先级');
    expect(validateAppRuleForm({ rule: 'a', result: 'b', priority: 0, enable: true, remark: '' })).toBe('');
  });
});

describe('F11 AppRule CRUD', () => {
  it('fetchAppRules：GET /Configs/AppRule，失败返回 null', async () => {
    const req = routingStub({ '/Configs/AppRule': () => ({ data: [{ Id: 1, Rule: 'a', Result: 'b', Priority: 0, Enable: true }] }) });
    const rows = await fetchAppRules(req);
    expect(rows).toHaveLength(1);
    const bad: Req = async () => {
      throw new Error('x');
    };
    expect(await fetchAppRules(bad)).toBeNull();
  });

  it('createAppRule：POST body 含 Rule/Result/Priority/Enable/Remark', async () => {
    const calls: unknown[] = [];
    const req: Req = async (opt) => {
      calls.push(opt);
      return { code: 0, message: '操作成功' } as Awaited<ReturnType<Req>>;
    };
    const r = await createAppRule(req, { rule: ' IP=10.* ', result: 'Scope=dev', priority: 5, enable: true, remark: '' });
    expect(r.ok).toBe(true);
    expect(calls[0]).toMatchObject({ url: '/Configs/AppRule', method: 'POST' });
    expect((calls[0] as { data: Record<string, unknown> }).data).toEqual({ Rule: 'IP=10.*', Result: 'Scope=dev', Priority: 5, Enable: true, Remark: '' });
  });

  it('saveAppRule：PUT /{id} 部分更新；缺 id 不发请求', async () => {
    const calls: unknown[] = [];
    const req: Req = async (opt) => {
      calls.push(opt);
      return { code: 0 } as Awaited<ReturnType<Req>>;
    };
    const r = await saveAppRule(req, 3, { rule: 'a', result: 'b', priority: 1, enable: false, remark: 'r' });
    expect(r.ok).toBe(true);
    expect(calls[0]).toMatchObject({ url: '/Configs/AppRule/3', method: 'PUT' });
    const noId = stubNoCall();
    expect((await saveAppRule(noId.req, 0, { rule: 'a', result: 'b', priority: 0, enable: true, remark: '' })).ok).toBe(false);
    expect(noId.calls).toHaveLength(0);
  });

  it('deleteAppRule：DELETE /{id}；失败透传消息', async () => {
    const calls: unknown[] = [];
    const req: Req = async (opt) => {
      calls.push(opt);
      return { code: 0, message: '操作成功' } as Awaited<ReturnType<Req>>;
    };
    expect((await deleteAppRule(req, 4)).ok).toBe(true);
    expect(calls[0]).toEqual({ url: '/Configs/AppRule/4', method: 'DELETE' });
    const bad = routingStub({});
    const r = await deleteAppRule(bad as Req, 4);
    expect(r.ok).toBe(false);
  });
});

/** 无调用记录桩：断言「不发请求」用 */
function stubNoCall(): { req: Req; calls: unknown[] } {
  const calls: unknown[] = [];
  const req: Req = async (opt) => {
    calls.push(opt);
    return { code: 0 } as Awaited<ReturnType<Req>>;
  };
  return { req, calls };
}
