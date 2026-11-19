// CARD-MON-12 跟踪项纯逻辑单元测试（先于实现）。
// 覆盖：查询参数组装 / 行映射（Id Int32 仍 string 传参）/ SelectKeys 串 /
// GetKinds 词典消费 / 批量四动作 / CRUD 路由 / 保存语义锁（后端承载→前端直传不改写）/ 表单校验。
import { describe, expect, it, vi } from 'vitest';
import {
  deleteTraceItem,
  fetchTraceItemKinds,
  fetchTraceItems,
  runTraceItemAction,
  saveTraceItem,
  selectKeysParam,
  toTraceItemRow,
  traceItemParams,
  traceItemSaveBody,
  validateTraceItemEdit,
  type TraceItemEditModel,
} from './traceItem.logic.ts';
import type { Req, ReqOptions } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

type Rec = Record<string, unknown>;

/** 极简请求桩：记录每次调用的 ReqOptions，按注册应答依次返回 */
function stubReq(calls: ReqOptions[], replies: unknown[]): Req {
  let i = 0;
  return vi.fn(async (opt: ReqOptions) => {
    calls.push(opt);
    const r = replies[Math.min(i, replies.length - 1)];
    i++;
    if (r instanceof Error) throw r;
    return r as never;
  }) as unknown as Req;
}

describe('traceItemParams 查询参数组装', () => {
  it('空条件只带默认分页', () => {
    expect(traceItemParams({})).toEqual({ page: 1, pageSize: 20 });
  });

  it('全条件按后端 Search 契约出参（URL Id 单条用大写 Id 键）', () => {
    const p = traceItemParams({
      id: '5',
      appId: 32,
      name: '/check',
      kind: '接口',
      enable: false,
      dtStart: '2026-11-01',
      dtEnd: '2026-11-09',
      Q: 'k',
      page: 2,
      pageSize: 50,
    });
    expect(p).toEqual({
      Id: '5',
      appId: 32,
      name: '/check',
      kind: '接口',
      enable: false,
      dtStart: '2026-11-01',
      dtEnd: '2026-11-09',
      Q: 'k',
      page: 2,
      pageSize: 50,
    });
  });

  it('enable 三态：null/undefined 不出键（不过滤），false 必须出键', () => {
    expect(traceItemParams({ enable: null })).toEqual({ page: 1, pageSize: 20 });
    expect(traceItemParams({ enable: undefined })).toEqual({ page: 1, pageSize: 20 });
    expect(traceItemParams({ enable: false })).toEqual({ enable: false, page: 1, pageSize: 20 });
  });

  it('无效 appId（<=0）与空串不出键', () => {
    const p = traceItemParams({ appId: 0, name: '', kind: '', Q: '' }) as Rec;
    expect(p).not.toHaveProperty('appId');
    expect(p).not.toHaveProperty('name');
  });
});

describe('toTraceItemRow 行映射', () => {
  it('大写键后端行 → 驼峰行，Id 数字转 string（Int32 统一 string 传参口径）', () => {
    const row = toTraceItemRow({
      Id: 5,
      AppId: 32,
      Kind: '接口',
      Name: '/check',
      DisplayName: '健康检查',
      Enable: true,
      Rules: 'name=*/check*',
      Cloned: false,
      Timeout: 3,
      Days: 12,
      Total: 345,
      Errors: 6,
      UpdateUser: 'admin',
      UpdateTime: '2026-11-09 10:00:00',
    });
    expect(row).toEqual({
      id: '5',
      appId: 32,
      kind: '接口',
      name: '/check',
      displayName: '健康检查',
      enable: true,
      rules: 'name=*/check*',
      cloned: false,
      timeout: 3,
      days: 12,
      total: 345,
      errors: 6,
      updateUser: 'admin',
      updateTime: '2026-11-09 10:00:00',
    });
  });

  it('缺省字段兜底：enable/cloned 为 null，数值为 0，字符串为空串', () => {
    const row = toTraceItemRow({});
    expect(row.id).toBe('');
    expect(row.enable).toBeNull();
    expect(row.cloned).toBeNull();
    expect(row.timeout).toBe(0);
    expect(row.total).toBe(0);
    expect(row.name).toBe('');
  });
});

describe('selectKeysParam', () => {
  it('逗号串（SelectKeys=GetRequest("Keys")）', () => {
    expect(selectKeysParam(['1', '2', '3'])).toBe('1,2,3');
    expect(selectKeysParam([])).toBe('');
  });
});

describe('fetchTraceItems', () => {
  it('GET /Monitors/TraceItem 携带组装参数，data 数组映射为行', async () => {
    const calls: ReqOptions[] = [];
    const req = stubReq(calls, [
      {
        code: 0,
        data: [{ Id: 7, AppId: 32, Kind: 'Http', Name: 'http://x', Enable: false }],
      },
    ]);
    const rows = await fetchTraceItems(req, { appId: 32, enable: false });
    expect(calls[0]).toMatchObject({ url: '/Monitors/TraceItem', method: 'GET' });
    expect(calls[0]?.params).toEqual({ appId: 32, enable: false, page: 1, pageSize: 20 });
    expect(rows).toHaveLength(1);
    expect(rows[0]).toMatchObject({ id: '7', kind: 'Http', enable: false });
  });
});

describe('fetchTraceItemKinds（CARD-MON-12 裁定 1：后端 GetKinds action 消费）', () => {
  it('GET /Monitors/TraceItem/GetKinds，data 词典 → value/label 数组', async () => {
    const calls: ReqOptions[] = [];
    const req = stubReq(calls, [{ code: 0, data: { Api: '接口', Http: 'Http', Db: '数据库' } }]);
    const kinds = await fetchTraceItemKinds(req);
    expect(calls[0]).toMatchObject({ url: '/Monitors/TraceItem/GetKinds', method: 'GET' });
    expect(kinds).toEqual([
      { value: 'Api', label: '接口' },
      { value: 'Http', label: 'Http' },
      { value: 'Db', label: '数据库' },
    ]);
  });

  it('data 非对象（空/null）→ 空数组兜底', async () => {
    const req = stubReq([], [{ code: 0, data: null }]);
    expect(await fetchTraceItemKinds(req)).toEqual([]);
  });
});

describe('批量四动作（Fix/FixDisplay/EnableSelect/DisableSelect）', () => {
  it.each([
    ['Fix', '/Monitors/TraceItem/Fix'],
    ['FixDisplay', '/Monitors/TraceItem/FixDisplay'],
    ['EnableSelect', '/Monitors/TraceItem/EnableSelect'],
    ['DisableSelect', '/Monitors/TraceItem/DisableSelect'],
  ] as const)('%s 走 GET keys=逗号串，返回 JsonRefresh 文案', async (action, url) => {
    const calls: ReqOptions[] = [];
    const req = stubReq(calls, [{ code: 0, message: '成功！' }]);
    const msg = await runTraceItemAction(req, action, ['1', '2']);
    expect(calls[0]).toMatchObject({ url, method: 'GET', params: { keys: '1,2' } });
    expect(msg).toBe('成功！');
  });

  it('空选择不请求', async () => {
    const calls: ReqOptions[] = [];
    const req = stubReq(calls, [{}]);
    await runTraceItemAction(req, 'Fix', []);
    expect(calls).toHaveLength(0);
  });
});

describe('saveTraceItem / deleteTraceItem', () => {
  const form: TraceItemEditModel = {
    appId: 32,
    name: '/status',
    kind: 'Http',
    displayName: '',
    enable: true,
    cloned: false,
    timeout: 0,
    rules: '*/status*',
  };

  it('新增 POST /Monitors/TraceItem 全字段体', async () => {
    const calls: ReqOptions[] = [];
    const req = stubReq(calls, [{ code: 0, data: 9 }]);
    await saveTraceItem(req, form);
    expect(calls[0]).toMatchObject({ url: '/Monitors/TraceItem', method: 'POST' });
    expect((calls[0] as ReqOptions & { data?: Record<string, unknown> }).data).toEqual({
      appId: 32,
      name: '/status',
      kind: 'Http',
      displayName: '',
      enable: true,
      cloned: false,
      timeout: 0,
      rules: '*/status*',
    });
  });

  it('编辑 PUT /Monitors/TraceItem/{id}（主键在路径，body 部分更新）', async () => {
    const calls: ReqOptions[] = [];
    const req = stubReq(calls, [{ code: 0 }]);
    await saveTraceItem(req, form, '9');
    expect(calls[0]).toMatchObject({ url: '/Monitors/TraceItem/9', method: 'PUT' });
    expect((calls[0] as ReqOptions & { data?: Record<string, unknown> }).data).not.toHaveProperty('id');
  });

  it('删除 DELETE /Monitors/TraceItem/{id}', async () => {
    const calls: ReqOptions[] = [];
    const req = stubReq(calls, [{ code: 0 }]);
    await deleteTraceItem(req, '9');
    expect(calls[0]).toMatchObject({ url: '/Monitors/TraceItem/9', method: 'DELETE' });
  });
});

describe('保存语义锁（CARD-MON-12 裁定 2 核验：后端承载 Kind 补齐/Rules 前缀 → 前端直传不改写）', () => {
  it('Kind 空、Rules 无 = 时保存体保持原值（由后端 Valid 补齐，前端不预改写）', () => {
    const body = traceItemSaveBody({
      appId: 32,
      name: '/check',
      kind: '',
      displayName: '',
      enable: true,
      cloned: false,
      timeout: 0,
      rules: '*/check*,*/ping*',
    });
    expect(body.kind).toBe('');
    expect(body.rules).toBe('*/check*,*/ping*');
  });

  it('已有前缀/已有 Kind 原样直传', () => {
    const body = traceItemSaveBody({
      appId: 5,
      name: 'x',
      kind: 'redismq',
      displayName: 'd',
      enable: false,
      cloned: true,
      timeout: 30,
      rules: 'clientId=10.10.*',
    });
    expect(body.kind).toBe('redismq');
    expect(body.rules).toBe('clientId=10.10.*');
  });
});

describe('validateTraceItemEdit', () => {
  it('Name 必填', () => {
    expect(validateTraceItemEdit({ appId: 1, name: '', kind: '', displayName: '', enable: true, cloned: false, timeout: 0, rules: '' })).toBe('操作名不能为空');
    expect(validateTraceItemEdit({ appId: 1, name: ' ', kind: '', displayName: '', enable: true, cloned: false, timeout: 0, rules: '' })).toBe('操作名不能为空');
  });

  it('超时必须 >=0', () => {
    expect(validateTraceItemEdit({ appId: 1, name: 'x', kind: '', displayName: '', enable: true, cloned: false, timeout: -1, rules: '' })).toBe('超时时间不能为负数');
  });

  it('通过返回空串', () => {
    expect(validateTraceItemEdit({ appId: 1, name: 'x', kind: 'Http', displayName: '', enable: true, cloned: false, timeout: 0, rules: '' })).toBe('');
  });
});
