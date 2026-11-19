// 网关证书维护纯逻辑单测（CARD-GW-04，2026-11-07）。
// 覆盖：表单默认值/Detail 回填、校验（Domain 必填）、提交体（新增全字段不带 Id / 编辑 diff 只提交变更字段）、
// **PfxPassword 四硬条款**（编辑不回显恒空 / 留空不提交 / 输入新值才提交 / insert 允许空）、
// 列表归一与本地排序（NotAfter 升序临期在前，缺值排后）、筛选参数拼装（**永不产生 projectId/domain**——C11/裁量 2）、
// Detail 拉取失败返回 null、提交先拉 Detail（Detail 不可达不 PUT，按方法记键断言）、
// fetchGateway 证书筛选透传（cert 请求 PageSize=500 且不带 projectId——C11 硬断言）与按接口归因错误。
import { describe, it, expect } from 'vitest';
import {
  emptyCertForm, detailToCertForm, validateCertForm, certInsertPayload, certUpdatePayload,
  toCertRows, emptyCertFilter, isCertFilterActive, certFilterParams,
  fetchCertDetail, submitCert, removeCert,
} from './gatewayCert.logic';
import { fetchGateway } from './mesh.logic';
import type { Req, ReqOptions } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

const DETAIL: Record<string, unknown> = {
  Id: 7, Domain: '*.example.com', Enable: true, AutoRenew: true, Provider: 'letsencrypt', RenewDays: 30,
  PfxFile: 'certs/star.pfx', PfxPassword: 'ENC(3q29fa8)', CrtFile: 'certs/star.crt', KeyFile: 'certs/star.key', PemFile: 'certs/star.pem',
  Issuer: "Let's Encrypt Authority X3", Subject: 'CN=*.example.com',
  NotBefore: '2026-09-01 00:00:00', NotAfter: '2026-12-01 00:00:00', Thumbprint: 'ABC123', LastRenew: '2026-09-01 08:00:00',
  CreateTime: '2026-09-01 08:00:00', Remark: '泛域名证书',
};

type Call = { method: string; url: string; params: Record<string, unknown>; body: Record<string, unknown> | undefined };

function stubReq(
  map: Record<string, unknown>,
  opts?: {
    throwOn?: string[];
    /** 按方法记键的请求流水（Detail GET 与 PUT 同 URL——路径键无法区分方法） */
    calls?: Call[];
  },
): Req {
  return async (opt) => {
    const url = String(opt.url ?? '');
    const params = (opt.params ?? {}) as Record<string, unknown>;
    const body = (opt as ReqOptions & { data?: Record<string, unknown> }).data;
    if (opts?.calls) opts.calls.push({ method: String(opt.method ?? 'GET').toUpperCase(), url, params, body });
    if (opts?.throwOn?.includes(url)) throw new Error(`stub 500:${url}`);
    if (url in map) return { code: 0, message: '操作成功', data: map[url] };
    return { code: 0, message: 'ok', data: [] };
  };
}

describe('表单装配：默认值与 Detail 回填', () => {
  it('emptyCertForm 默认值：Enable 开、AutoRenew 关、RenewDays 30（实体 DefaultValue）、PfxPassword 空', () => {
    const m = emptyCertForm();
    expect(m).toMatchObject({
      domain: '', enable: true, autoRenew: false, provider: '', renewDays: 30,
      pfxFile: '', pfxPassword: '', crtFile: '', keyFile: '', pemFile: '',
      issuer: '', subject: '', notBefore: '', notAfter: '', thumbprint: '', lastRenew: '', remark: '',
    });
  });

  it('detailToCertForm 回填全字段（大小写两种都认），日期归一 YYYY-MM-DD', () => {
    const m = detailToCertForm(DETAIL);
    expect(m).toMatchObject({
      domain: '*.example.com', enable: true, autoRenew: true, provider: 'letsencrypt', renewDays: 30,
      pfxFile: 'certs/star.pfx', crtFile: 'certs/star.crt', keyFile: 'certs/star.key', pemFile: 'certs/star.pem',
      issuer: "Let's Encrypt Authority X3", subject: 'CN=*.example.com',
      notBefore: '2026-09-01', notAfter: '2026-12-01', thumbprint: 'ABC123', lastRenew: '2026-09-01', remark: '泛域名证书',
    });
  });

  it('detailToCertForm：**PfxPassword 恒置空串（硬条款③编辑不回显）**，即使 Detail 带值', () => {
    expect(detailToCertForm(DETAIL).pfxPassword).toBe('');
    expect(detailToCertForm({ PfxPassword: 'whatever' }).pfxPassword).toBe('');
  });

  it('detailToCertForm：DateTime 空值（0001-01-01）→ 空串，避免空日期回传', () => {
    const m = detailToCertForm({ Domain: 'a.com', NotBefore: '0001-01-01 00:00:00', NotAfter: '0001-01-01 00:00:00', LastRenew: '0001-01-01 00:00:00' });
    expect(m.notBefore).toBe('');
    expect(m.notAfter).toBe('');
    expect(m.lastRenew).toBe('');
  });
});

describe('校验：Domain 必填（SSL证书.Biz.cs:64 同义）', () => {
  it('Domain 空（含纯空白）→ 「域名不能为空」', () => {
    expect(validateCertForm({ ...emptyCertForm(), domain: '' }).domain).toBe('域名不能为空');
    expect(validateCertForm({ ...emptyCertForm(), domain: '  ' }).domain).toBe('域名不能为空');
  });

  it('合法表单无错误', () => {
    expect(validateCertForm({ ...emptyCertForm(), domain: 'iot.example.com' })).toEqual({});
  });
});

describe('提交体：新增全字段 / 编辑 diff', () => {
  it('certInsertPayload 全字段 PascalCase、body 不带 Id、布尔发布尔、PfxPassword 允许空串', () => {
    const m = { ...emptyCertForm(), domain: 'iot.example.com', autoRenew: true, pfxFile: 'a.pfx', pfxPassword: 's3cret' };
    const b = certInsertPayload(m);
    expect(b).toMatchObject({
      Domain: 'iot.example.com', Enable: true, AutoRenew: true, RenewDays: 30,
      PfxFile: 'a.pfx', PfxPassword: 's3cret', CrtFile: '', KeyFile: '', PemFile: '',
      NotBefore: '', NotAfter: '', Remark: '',
    });
    expect(Object.keys(b)).not.toContain('Id');
    expect(b.Enable).toBe(true);
    expect(b.AutoRenew).toBe(true);
  });

  it('certUpdatePayload 只提交变更字段（不提交=不改）', () => {
    const m = { ...detailToCertForm(DETAIL), enable: false, pemFile: 'certs/star-new.pem' };
    const b = certUpdatePayload(DETAIL, m);
    expect(b).toEqual({ Enable: false, PemFile: 'certs/star-new.pem' });
  });

  it('certUpdatePayload 无变更 → 空体', () => {
    expect(certUpdatePayload(DETAIL, detailToCertForm(DETAIL))).toEqual({});
  });

  it('certUpdatePayload：**PfxPassword 留空不提交（硬条款③）**——Detail 带密码值也不触发', () => {
    const m = detailToCertForm(DETAIL); // pfxPassword 恒空
    expect(certUpdatePayload(DETAIL, m)).toEqual({});
    const m2 = { ...detailToCertForm(DETAIL), crtFile: 'certs/x.crt' };
    expect(certUpdatePayload(DETAIL, m2)).toEqual({ CrtFile: 'certs/x.crt' });
  });

  it('certUpdatePayload：输入新密码才提交 PfxPassword（覆盖旧值）', () => {
    const m = { ...detailToCertForm(DETAIL), pfxPassword: 'new-pass' };
    expect(certUpdatePayload(DETAIL, m)).toEqual({ PfxPassword: 'new-pass' });
  });

  it('certUpdatePayload：日期已归一（与 Detail 归一值相同则不提交；用户改日期 → 提交 YYYY-MM-DD）', () => {
    expect(certUpdatePayload(DETAIL, detailToCertForm(DETAIL))).not.toHaveProperty('NotAfter');
    const m = { ...detailToCertForm(DETAIL), notAfter: '2027-01-15' };
    expect(certUpdatePayload(DETAIL, m)).toEqual({ NotAfter: '2027-01-15' });
  });
});

describe('列表归一 toCertRows（本地排序=过期时间升序临期在前，定稿裁定）', () => {
  it('全字段行归一；NotAfter 升序：临期在前、缺值排最后（保持相对次序）', () => {
    const rows = toCertRows([
      { Id: 1, Domain: 'no-date.example.com', Enable: true, PfxFile: 'a.pfx', CrtFile: 'a.crt', KeyFile: 'a.key', PemFile: 'a.pem' },
      { Id: 2, Domain: 'late.example.com', Enable: false, NotAfter: '2027-06-01 00:00:00' },
      { Id: 3, Domain: 'soon.example.com', Enable: true, NotAfter: '2026-12-01 00:00:00' },
    ]);
    expect(rows.map((r) => r.id)).toEqual([3, 2, 1]);
    expect(rows[0]).toMatchObject({ id: 3, domain: 'soon.example.com', enable: true, pfxFile: '', crtFile: '', keyFile: '', pemFile: '' });
    expect(rows[2]).toMatchObject({ id: 1, domain: 'no-date.example.com' });
  });

  it('全部缺 NotAfter → 保持原序（列表序列化未核实，排序无值时退化不造数）', () => {
    const rows = toCertRows([{ Id: 9, Domain: 'a.com' }, { Id: 8, Domain: 'b.com' }]);
    expect(rows.map((r) => r.id)).toEqual([9, 8]);
    expect(rows[0].notAfterMs).toBe(0);
  });
});

describe('筛选参数拼装（C04；**永不产生 projectId/domain/autoRenew**——C11 硬断言+裁量 2）', () => {
  it('全空 → 不传任何参数', () => {
    expect(certFilterParams(emptyCertFilter())).toEqual({});
    expect(isCertFilterActive(emptyCertFilter())).toBe(false);
  });

  it('勾选/时间/关键字逐项透传，Q 映射后端参数名', () => {
    const f = { enable: true, dtStart: '2026-11-01', dtEnd: '2026-12-31', q: 'example' };
    expect(certFilterParams(f)).toEqual({ enable: true, dtStart: '2026-11-01', dtEnd: '2026-12-31', Q: 'example' });
    expect(isCertFilterActive(f)).toBe(true);
  });

  it('只勾 enable → 只传 enable；参数键集合恒不含 projectId/domain/autoRenew', () => {
    const p = certFilterParams({ ...emptyCertFilter(), enable: true, q: 'x' });
    expect(Object.keys(p)).not.toContain('projectId');
    expect(Object.keys(p)).not.toContain('domain');
    expect(Object.keys(p)).not.toContain('autoRenew');
  });
});

describe('Detail 拉取与提交（先 Detail 后 PUT）', () => {
  it('fetchCertDetail：实体返回 / data 为数组返回 null / HTTP 错返回 null', async () => {
    expect(await fetchCertDetail(stubReq({ '/Gateway/GatewayCert/7': DETAIL }), 7)).toEqual(DETAIL);
    expect(await fetchCertDetail(stubReq({ '/Gateway/GatewayCert/7': [] }), 7)).toBeNull();
    expect(await fetchCertDetail(stubReq({}, { throwOn: ['/Gateway/GatewayCert/7'] }), 7)).toBeNull();
  });

  it('submitCert 编辑：先 GET Detail 再 PUT diff；Detail 不可达 → ok=false 且不发 PUT', async () => {
    const calls: Call[] = [];
    const req = stubReq({ '/Gateway/GatewayCert/7': DETAIL }, { calls });
    const m = { ...detailToCertForm(DETAIL), domain: 'renamed.example.com' };
    const r = await submitCert(req, m, 7);
    expect(r.ok).toBe(true);
    expect(calls.map((c) => `${c.method} ${c.url}`)).toEqual(['GET /Gateway/GatewayCert/7', 'PUT /Gateway/GatewayCert/7']);
    expect(calls[1].body).toEqual({ Domain: 'renamed.example.com' });

    const calls2: Call[] = [];
    const r2 = await submitCert(stubReq({}, { calls: calls2 }), m, 99);
    expect(r2.ok).toBe(false);
    expect(r2.message).toBe('证书不存在或已被删除');
    expect(calls2.map((c) => `${c.method} ${c.url}`)).toEqual(['GET /Gateway/GatewayCert/99']);
  });

  it('submitCert 新增：POST 不带 id 全字段', async () => {
    const calls: Call[] = [];
    const m = { ...emptyCertForm(), domain: 'iot.example.com' };
    const r = await submitCert(stubReq({ '/Gateway/GatewayCert': {} }, { calls }), m);
    expect(r.ok).toBe(true);
    expect(r.message).toBe('操作成功');
    expect(calls).toEqual([{ method: 'POST', url: '/Gateway/GatewayCert', params: {}, body: certInsertPayload(m) }]);
  });

  it('removeCert：DELETE /{id}', async () => {
    const calls: Call[] = [];
    const r = await removeCert(stubReq({ '/Gateway/GatewayCert/7': {} }, { calls }), 7);
    expect(r.ok).toBe(true);
    expect(r.message).toBe('操作成功');
    expect(calls).toEqual([{ method: 'DELETE', url: '/Gateway/GatewayCert/7', params: {}, body: undefined }]);
  });
});

describe('fetchGateway 集成（GW-04，C11 硬断言）', () => {
  it('证书筛选参数并入 cert 请求；**cert 请求永不带 projectId**（route/cluster 带顶栏 projectId）', async () => {
    const calls: Call[] = [];
    await fetchGateway(stubReq({}, { calls }), 3, undefined, undefined, { ...emptyCertFilter(), enable: true, q: 'example', dtStart: '2026-11-01', dtEnd: '2026-12-31' });
    const cert = calls.find((c) => c.url === '/Gateway/GatewayCert');
    expect(cert?.method).toBe('GET');
    expect(cert?.params).toEqual({ PageSize: 500, enable: true, Q: 'example', dtStart: '2026-11-01', dtEnd: '2026-12-31' });
    expect(cert?.params).not.toHaveProperty('projectId');
    const route = calls.find((c) => c.url === '/Gateway/GatewayRoute');
    expect(route?.params).toMatchObject({ PageSize: 500, projectId: 3 });
    const cluster = calls.find((c) => c.url === '/Gateway/GatewayCluster');
    expect(cluster?.params).toMatchObject({ PageSize: 500, projectId: 3 });
  });

  it('无筛选时 cert 请求仅 PageSize:500（不带 projectId，回归固化 C11 现状）', async () => {
    const calls: Call[] = [];
    await fetchGateway(stubReq({}, { calls }), 3);
    const cert = calls.find((c) => c.url === '/Gateway/GatewayCert');
    expect(cert?.params).toEqual({ PageSize: 500 });
  });

  it('GatewayResult.certs 透出原始行；仅证书失败 → certError 有值，路由行仍解析（TLS 列退化为待接入）', async () => {
    const req: Req = async (opt) => {
      if (String(opt.url ?? '') === '/Gateway/GatewayCert') throw new Error('cert down');
      return { code: 0, message: 'ok', data: [{ Id: 1, Name: 'r1', Enable: true }] };
    };
    const r = await fetchGateway(req, 3);
    expect(r.certError).toBe('cert down');
    expect(r.routeError).toBe('');
    expect(r.clusterError).toBe('');
    expect(r.error).toBe('cert down');
    expect(r.certs).toEqual([]);
    expect(r.rows).toHaveLength(1);
    expect(r.rows[0].tls.present).toBe(false);
  });

  it('证书原始行透出 certs + 路由 TLS 按启用证书匹配（数据源不变，C09/C10 防退化）', async () => {
    const req: Req = async (opt) => {
      const u = String(opt.url ?? '');
      if (u === '/Gateway/GatewayCert') return { code: 0, message: 'ok', data: [{ Id: 7, Domain: 'iot.example.com', Enable: true }] };
      if (u === '/Gateway/GatewayRoute') return { code: 0, message: 'ok', data: [{ Id: 1, Name: 'r1', Domain: 'iot.example.com', Path: '/x', Enable: true }] };
      return { code: 0, message: 'ok', data: [] };
    };
    const r = await fetchGateway(req, 3);
    expect(r.certs).toEqual([{ Id: 7, Domain: 'iot.example.com', Enable: true }]);
    expect(r.rows[0].tls).toMatchObject({ present: true, domain: 'iot.example.com', autoRenew: false });
  });
});
