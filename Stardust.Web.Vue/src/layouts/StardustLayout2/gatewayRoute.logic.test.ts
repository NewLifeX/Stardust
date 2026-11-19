// 网关路由维护纯逻辑单测（CARD-GW-03，2026-11-07）。
// 覆盖：表单默认值/Detail 回填、校验（Name 必填、IsStaticRoute→StaticRoot）、
// 提交体（新增全字段不带 id / 编辑 diff 只提交变更字段）、筛选参数拼装（空值不传、stripPrefix 开才传）、
// fetchGateway 筛选参数透传、Detail 拉取失败返回 null。
import { describe, it, expect } from 'vitest';
import {
  emptyRouteForm, detailToRouteForm, validateRouteForm, routeInsertPayload, routeUpdatePayload,
  emptyRouteFilter, isRouteFilterActive, routeFilterParams, fetchRouteDetail, submitRoute, removeRoute,
} from './gatewayRoute.logic';
import { fetchGateway } from './mesh.logic';
import type { Req, ReqOptions } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

const DETAIL: Record<string, unknown> = {
  Id: 7, ProjectId: 3, Name: 'iot-api', Enable: true, Priority: 5, ClusterId: 11,
  Domain: 'iot.example.com', Path: '/api/**', Methods: 'GET', Headers: 'X-A: 1',
  StripPrefix: true, AddHeaders: 'X-B: 2', WebSocket: true,
  IsStaticRoute: true, StaticRoot: '/www/iot', IndexFile: 'index.html', DirectoryBrowse: false, SPAFallback: true,
  Remark: '主链路',
};

function stubReq(
  map: Record<string, unknown>,
  opts?: {
    throwOn?: string[];
    seen?: Record<string, Record<string, unknown>>;
    /** 按方法记键的请求流水（Detail GET 与 PUT 同 URL，路径键无法区分方法——裁定 2026-11-07） */
    calls?: Array<{ method: string; url: string; body: Record<string, unknown> | undefined }>;
  },
): Req {
  return async (opt) => {
    const url = String(opt.url ?? '');
    const body = (opt as ReqOptions & { data?: Record<string, unknown> }).data;
    if (opts?.seen) opts.seen[url] = { ...(opt.params as Record<string, unknown>), ...body };
    if (opts?.calls) opts.calls.push({ method: String(opt.method ?? 'GET').toUpperCase(), url, body });
    if (opts?.throwOn?.includes(url)) throw new Error(`stub 500:${url}`);
    if (url in map) return { code: 0, message: '操作成功', data: map[url] };
    return { code: 0, message: 'ok', data: [] };
  };
}

describe('表单装配：默认值与 Detail 回填', () => {
  it('emptyRouteForm 默认值：Enable 开、Priority 0、其余空', () => {
    const m = emptyRouteForm();
    expect(m.enable).toBe(true);
    expect(m.priority).toBe(0);
    expect(m.name).toBe('');
    expect(m.isStaticRoute).toBe(false);
    expect(m.projectId).toBe(0);
  });

  it('detailToRouteForm 回填全字段（大小写两种都认、SPAFallback 双命名兼容）', () => {
    const m = detailToRouteForm(DETAIL);
    expect(m).toMatchObject({
      projectId: 3, name: 'iot-api', enable: true, priority: 5, clusterId: 11,
      domain: 'iot.example.com', path: '/api/**', methods: 'GET', headers: 'X-A: 1',
      stripPrefix: true, addHeaders: 'X-B: 2', webSocket: true,
      isStaticRoute: true, staticRoot: '/www/iot', indexFile: 'index.html', directoryBrowse: false, spaFallback: true,
      remark: '主链路',
    });
  });
});

describe('校验：Name 必填与 IsStaticRoute→StaticRoot 必填', () => {
  it('Name 空 → 「名称不能为空」', () => {
    const errs = validateRouteForm({ ...emptyRouteForm(), name: '  ' });
    expect(errs.name).toBe('名称不能为空');
  });

  it('IsStaticRoute 开且 StaticRoot 空 → 静态根目录错误（后端 InvalidOperationException 同义）', () => {
    const errs = validateRouteForm({ ...emptyRouteForm(), name: 'a', isStaticRoute: true, staticRoot: '' });
    expect(errs.staticRoot).toBe('开启静态文件路由时，静态文件根目录不能为空');
    expect(errs.name).toBeUndefined();
  });

  it('合法表单无错误', () => {
    expect(validateRouteForm({ ...emptyRouteForm(), name: 'a', staticRoot: '/www' })).toEqual({});
  });
});

describe('提交体：新增全字段（不带 id）/ 编辑 diff（不提交=不改）', () => {
  it('routeInsertPayload 全字段且无 Id；布尔发布尔', () => {
    const m = { ...emptyRouteForm(), projectId: 3, name: 'x', clusterId: 11, webSocket: true };
    const body = routeInsertPayload(m);
    expect('Id' in body).toBe(false);
    expect(body).toMatchObject({ ProjectId: 3, Name: 'x', ClusterId: 11, Enable: true, WebSocket: true, Priority: 0 });
  });

  it('routeUpdatePayload 只提交变更字段（部分更新，6681 冒烟机制）', () => {
    const base = detailToRouteForm(DETAIL);
    const m = { ...base, name: 'iot-api-2', priority: 9, staticRoot: '/www/iot2' };
    const body = routeUpdatePayload(DETAIL, m);
    expect(body).toEqual({ Name: 'iot-api-2', Priority: 9, StaticRoot: '/www/iot2' });
  });

  it('无变更 → 空 body（不发起覆盖写）', () => {
    expect(routeUpdatePayload(DETAIL, detailToRouteForm(DETAIL))).toEqual({});
  });
});

describe('筛选参数拼装与透传（J-GW-04 C15/C21）', () => {
  it('默认筛选 = 未激活，params 全空', () => {
    const f = emptyRouteFilter();
    expect(isRouteFilterActive(f)).toBe(false);
    expect(routeFilterParams(f)).toEqual({});
  });

  it('逐项激活：enable 三态/clusterId>0/domain/q/priority/stripPrefix/时间', () => {
    const f = { ...emptyRouteFilter(), enable: 'true', clusterId: 11, domain: 'a.com', q: 'iot', priority: 5, stripPrefix: true, dtStart: '2026-11-01', dtEnd: '2026-11-07' };
    expect(isRouteFilterActive(f)).toBe(true);
    expect(routeFilterParams(f)).toEqual({ enable: true, clusterId: 11, domain: 'a.com', Q: 'iot', priority: 5, stripPrefix: true, dtStart: '2026-11-01', dtEnd: '2026-11-07' });
  });

  it('enable=false 传布尔 false；clusterId=0/优先级空/stripPrefix 关/时间空不传', () => {
    const f = { ...emptyRouteFilter(), enable: 'false', priority: null };
    expect(routeFilterParams(f)).toEqual({ enable: false });
  });

  it('fetchGateway 把筛选参数与 projectId 一起放进路由请求（仅路由请求带筛选）', async () => {
    const seen: Record<string, Record<string, unknown>> = {};
    const req = stubReq({}, { seen });
    const f = { ...emptyRouteFilter(), enable: 'true', clusterId: 11, q: 'iot' };
    await fetchGateway(req, 42, f);
    expect(seen['/Gateway/GatewayRoute']).toMatchObject({ PageSize: 500, projectId: 42, enable: true, clusterId: 11, Q: 'iot' });
    expect(seen['/Gateway/GatewayCluster']).toEqual({ PageSize: 500, projectId: 42 });
    expect(seen['/Gateway/GatewayCert']).toEqual({ PageSize: 500 }); // C11：cert 永不带 projectId
  });
});

describe('Detail 拉取与 CRUD 封装', () => {
  it('fetchRouteDetail 返回实体；失败/空数据返回 null', async () => {
    const ok = await fetchRouteDetail(stubReq({ '/Gateway/GatewayRoute/7': DETAIL }), 7);
    expect(ok).toMatchObject({ Id: 7, Name: 'iot-api' });

    const fail = await fetchRouteDetail(stubReq({}, { throwOn: ['/Gateway/GatewayRoute/7'] }), 7);
    expect(fail).toBeNull();

    const empty = await fetchRouteDetail(stubReq({ '/Gateway/GatewayRoute/7': [] }), 7);
    expect(empty).toBeNull();
  });

  it('submitRoute 新增：POST 全字段；编辑：先拉 Detail 再 PUT diff（方法按 calls 记键）', async () => {
    const seen: Record<string, Record<string, unknown>> = {};
    const calls: Array<{ method: string; url: string; body: Record<string, unknown> | undefined }> = [];
    const req = stubReq({ '/Gateway/GatewayRoute/7': DETAIL }, { seen, calls });
    const m = detailToRouteForm(DETAIL);
    const created = await submitRoute(req, { ...m, name: 'new' });
    expect(created.ok).toBe(true);
    expect(seen['/Gateway/GatewayRoute']).toMatchObject({ Name: 'new', Enable: true });

    const updated = await submitRoute(req, { ...m, name: 'renamed' }, 7);
    expect(updated.ok).toBe(true);
    expect(seen['/Gateway/GatewayRoute/7']).toEqual({ Name: 'renamed' });
    expect(calls.map((c) => `${c.method} ${c.url}`)).toEqual([
      'POST /Gateway/GatewayRoute',
      'GET /Gateway/GatewayRoute/7',
      'PUT /Gateway/GatewayRoute/7',
    ]);
  });

  it('submitRoute 编辑时 Detail 拉不到 → ok=false 且不发 PUT（断言按方法记键：同 URL 的 GET Detail 不再混记，2026-11-07 裁定）', async () => {
    const calls: Array<{ method: string; url: string; body: Record<string, unknown> | undefined }> = [];
    const req = stubReq({}, { calls, throwOn: ['/Gateway/GatewayRoute/9'] });
    const r = await submitRoute(req, emptyRouteForm(), 9);
    expect(r.ok).toBe(false);
    expect(r.message).toBe('路由不存在或已被删除');
    expect(calls.map((c) => `${c.method} ${c.url}`)).toEqual(['GET /Gateway/GatewayRoute/9']);
  });

  it('removeRoute：DELETE /{id}，成功透传后端 message', async () => {
    const seen: Record<string, Record<string, unknown>> = {};
    const req = stubReq({ '/Gateway/GatewayRoute/7': true }, { seen });
    const r = await removeRoute(req, 7);
    expect(r.ok).toBe(true);
    expect(r.message).toBe('操作成功');
  });

  it('removeRoute 失败：ok=false 且 message 带错误信息（不静默）', async () => {
    const r = await removeRoute(stubReq({}, { throwOn: ['/Gateway/GatewayRoute/7'] }), 7);
    expect(r.ok).toBe(false);
    expect(r.message).toContain('stub 500');
  });
});
