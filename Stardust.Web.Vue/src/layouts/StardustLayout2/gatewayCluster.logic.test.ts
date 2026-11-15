// 网关集群维护纯逻辑单测（CARD-GW-01，2026-11-07）。
// 覆盖：表单默认值/Detail 回填、校验（Name 必填）、提交体（新增全字段不带 id / 编辑 diff 只提交变更字段）、
// LoadBalance 中文映射与未知兜底、列表归一（ActiveNodes 取不到置 null）、筛选参数拼装、
// Detail 拉取失败返回 null、提交先拉 Detail（Detail 不可达不 PUT——GW-03 裁定 5 同款，按方法记键断言）、
// fetchGateway 集群筛选透传与按接口归因错误。
import { describe, it, expect } from 'vitest';
import {
  emptyClusterForm, detailToClusterForm, validateClusterForm, clusterInsertPayload, clusterUpdatePayload,
  loadBalanceLabel, toClusterRows, emptyClusterFilter, isClusterFilterActive, clusterFilterParams,
  fetchClusterDetail, submitCluster, removeCluster,
} from './gatewayCluster.logic';
import { fetchGateway } from './mesh.logic';
import type { Req, ReqOptions } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

const DETAIL: Record<string, unknown> = {
  Id: 21, ProjectId: 3, Name: 'DeviceService', LoadBalance: 'LeastConnection',
  HealthPath: '/health', HealthInterval: 5, HealthTimeout: 2000, UnhealthyThreshold: 4, HealthyThreshold: 1,
  SessionSticky: true, SessionStickyName: 'srv', Enable: false, Remark: '边缘集群',
};

type Call = { method: string; url: string; params: Record<string, unknown>; body: Record<string, unknown> | undefined };

function stubReq(
  map: Record<string, unknown>,
  opts?: {
    throwOn?: string[];
    /** 按方法记键的请求流水（Detail GET 与 PUT 同 URL——GW-03 裁定：路径键无法区分方法） */
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
  it('emptyClusterForm 默认值=实体 DefaultValue：LoadBalance RoundRobin、间隔 10 秒、超时 3000 毫秒、阈值 3/2、Enable 开', () => {
    const m = emptyClusterForm();
    expect(m).toMatchObject({
      projectId: 0, name: '', loadBalance: 'RoundRobin', healthPath: '',
      healthInterval: 10, healthTimeout: 3000, unhealthyThreshold: 3, healthyThreshold: 2,
      sessionSticky: false, sessionStickyName: '', enable: true, remark: '',
    });
  });

  it('detailToClusterForm 回填全字段（大小写两种都认）', () => {
    const m = detailToClusterForm(DETAIL);
    expect(m).toMatchObject({
      projectId: 3, name: 'DeviceService', loadBalance: 'LeastConnection', healthPath: '/health',
      healthInterval: 5, healthTimeout: 2000, unhealthyThreshold: 4, healthyThreshold: 1,
      sessionSticky: true, sessionStickyName: 'srv', enable: false, remark: '边缘集群',
    });
  });

  it('LoadBalance 空串兜底 RoundRobin（实体默认值语义）', () => {
    expect(detailToClusterForm({ Name: 'x' }).loadBalance).toBe('RoundRobin');
  });
});

describe('校验：Name 必填', () => {
  it('Name 空（含纯空白）→ 「名称不能为空」', () => {
    expect(validateClusterForm({ ...emptyClusterForm(), name: '' }).name).toBe('名称不能为空');
    expect(validateClusterForm({ ...emptyClusterForm(), name: '  ' }).name).toBe('名称不能为空');
  });

  it('合法表单无错误', () => {
    expect(validateClusterForm({ ...emptyClusterForm(), name: 'edge' })).toEqual({});
  });
});

describe('提交体：新增全字段 / 编辑 diff', () => {
  it('clusterInsertPayload 全字段 PascalCase、body 不带 Id、布尔发布尔', () => {
    const m = { ...emptyClusterForm(), name: 'edge', sessionSticky: true, enable: false, projectId: 3 };
    const b = clusterInsertPayload(m);
    expect(b).toMatchObject({
      ProjectId: 3, Name: 'edge', LoadBalance: 'RoundRobin', HealthInterval: 10, HealthTimeout: 3000,
      UnhealthyThreshold: 3, HealthyThreshold: 2, SessionSticky: true, SessionStickyName: '', Enable: false, Remark: '',
    });
    expect(Object.keys(b)).not.toContain('Id');
    expect(b.SessionSticky).toBe(true);
    expect(b.Enable).toBe(false);
  });

  it('clusterUpdatePayload 只提交变更字段（不提交=不改）', () => {
    const m = { ...detailToClusterForm(DETAIL), name: 'DeviceService-2', healthInterval: 15 };
    const b = clusterUpdatePayload(DETAIL, m);
    expect(b).toEqual({ Name: 'DeviceService-2', HealthInterval: 15 });
  });

  it('clusterUpdatePayload 无变更 → 空体', () => {
    expect(clusterUpdatePayload(DETAIL, detailToClusterForm(DETAIL))).toEqual({});
  });
});

describe('LoadBalance 中文映射（旧壳 gateway.model.ts:60-74）', () => {
  it('三种已知值映射，未知值兜底原始串', () => {
    expect(loadBalanceLabel('RoundRobin')).toBe('轮询（RoundRobin）');
    expect(loadBalanceLabel('LeastConnection')).toBe('最少连接（LeastConnection）');
    expect(loadBalanceLabel('IPHash')).toBe('IP 哈希（IPHash）');
    expect(loadBalanceLabel('Custom')).toBe('Custom');
    expect(loadBalanceLabel('')).toBe('');
  });
});

describe('列表归一 toClusterRows', () => {
  it('全字段行：ActiveNodes 出值显示数字（0 是有效值）、时间格式化', () => {
    const [r] = toClusterRows([{ Id: 1, Name: 'edge', ProjectId: 3, ProjectName: '演示项目', LoadBalance: 'IPHash', HealthPath: '/health', HealthInterval: 10, HealthTimeout: 3000, UnhealthyThreshold: 3, HealthyThreshold: 2, SessionSticky: false, Enable: true, ActiveNodes: 0, UpdateTime: '2026-11-07 10:30:00' }]);
    expect(r).toMatchObject({ id: 1, name: 'edge', projectName: '演示项目', loadBalance: 'IPHash', activeNodes: 0, updateTime: '2026-11-07 10:30' });
  });

  it('ActiveNodes 缺失/空 → null（UI 显「—」，定稿裁定 3 不造数）；ProjectName/时间缺 → 兜底', () => {
    const [a] = toClusterRows([{ Id: 2, Name: 'x' }]);
    expect(a.activeNodes).toBeNull();
    expect(a.projectName).toBe('');
    expect(a.updateTime).toBe('—');
    const [b] = toClusterRows([{ Id: 3, Name: 'y', ActiveNodes: '' }]);
    expect(b.activeNodes).toBeNull();
  });
});

describe('筛选参数拼装（开关勾选=传 true，不勾=不传）', () => {
  it('全空 → 不传任何参数', () => {
    expect(clusterFilterParams(emptyClusterFilter())).toEqual({});
    expect(isClusterFilterActive(emptyClusterFilter())).toBe(false);
  });

  it('勾选/时间/关键字逐项透传，Q 映射后端参数名', () => {
    const f = { enable: true, sessionSticky: true, dtStart: '2026-11-01', dtEnd: '2026-11-07', q: 'edge' };
    expect(clusterFilterParams(f)).toEqual({ enable: true, sessionSticky: true, dtStart: '2026-11-01', dtEnd: '2026-11-07', Q: 'edge' });
    expect(isClusterFilterActive(f)).toBe(true);
  });

  it('只勾 enable → 只传 enable', () => {
    expect(clusterFilterParams({ ...emptyClusterFilter(), enable: true })).toEqual({ enable: true });
  });
});

describe('Detail 拉取与提交（先 Detail 后 PUT）', () => {
  it('fetchClusterDetail：实体返回 / data 为数组返回 null / HTTP 错返回 null', async () => {
    expect(await fetchClusterDetail(stubReq({ '/Gateway/GatewayCluster/21': DETAIL }), 21)).toEqual(DETAIL);
    expect(await fetchClusterDetail(stubReq({ '/Gateway/GatewayCluster/21': [] }), 21)).toBeNull();
    expect(await fetchClusterDetail(stubReq({}, { throwOn: ['/Gateway/GatewayCluster/21'] }), 21)).toBeNull();
  });

  it('submitCluster 编辑：先 GET Detail 再 PUT diff；Detail 不可达 → ok=false 且不发 PUT', async () => {
    const calls: Call[] = [];
    const req = stubReq({ '/Gateway/GatewayCluster/21': DETAIL }, { calls });
    const m = { ...detailToClusterForm(DETAIL), name: 'renamed' };
    const r = await submitCluster(req, m, 21);
    expect(r.ok).toBe(true);
    expect(calls.map((c) => `${c.method} ${c.url}`)).toEqual(['GET /Gateway/GatewayCluster/21', 'PUT /Gateway/GatewayCluster/21']);
    expect(calls[1].body).toEqual({ Name: 'renamed' });

    const calls2: Call[] = [];
    const r2 = await submitCluster(stubReq({}, { calls: calls2 }), m, 99);
    expect(r2.ok).toBe(false);
    expect(r2.message).toBe('集群不存在或已被删除');
    expect(calls2.map((c) => `${c.method} ${c.url}`)).toEqual(['GET /Gateway/GatewayCluster/99']);
  });

  it('submitCluster 新增：POST 不带 id 全字段', async () => {
    const calls: Call[] = [];
    const m = { ...emptyClusterForm(), name: 'edge' };
    const r = await submitCluster(stubReq({ '/Gateway/GatewayCluster': {} }, { calls }), m);
    expect(r.ok).toBe(true);
    expect(r.message).toBe('操作成功');
    expect(calls).toEqual([{ method: 'POST', url: '/Gateway/GatewayCluster', params: {}, body: clusterInsertPayload(m) }]);
  });

  it('removeCluster：DELETE /{id}', async () => {
    const calls: Call[] = [];
    const r = await removeCluster(stubReq({ '/Gateway/GatewayCluster/21': {} }, { calls }), 21);
    expect(r.ok).toBe(true);
    expect(r.message).toBe('操作成功');
    expect(calls).toEqual([{ method: 'DELETE', url: '/Gateway/GatewayCluster/21', params: {}, body: undefined }]);
  });
});

describe('fetchGateway 集成（GW-01）', () => {
  it('集群筛选参数并入集群请求（projectId 顶栏透传不变）', async () => {
    const calls: Call[] = [];
    await fetchGateway(stubReq({}, { calls }), 3, undefined, { ...emptyClusterFilter(), enable: true, q: 'edge' });
    const cluster = calls.find((c) => c.url === '/Gateway/GatewayCluster');
    expect(cluster?.method).toBe('GET');
    expect(cluster?.params).toMatchObject({ PageSize: 500, projectId: 3, enable: true, Q: 'edge' });
    const route = calls.find((c) => c.url === '/Gateway/GatewayRoute');
    expect(route?.params).not.toHaveProperty('Q');
    const cert = calls.find((c) => c.url === '/Gateway/GatewayCert');
    expect(cert?.params).toMatchObject({ PageSize: 500 });
  });

  it('按接口归因：仅路由失败 → routeError 有值、clusterError/certError 空、clusters 仍透出', async () => {
    const req: Req = async (opt) => {
      if (String(opt.url ?? '') === '/Gateway/GatewayRoute') throw new Error('route down');
      return { code: 0, message: 'ok', data: [{ Id: 21, Name: 'edge' }] };
    };
    const r = await fetchGateway(req, 3);
    expect(r.routeError).toBe('route down');
    expect(r.clusterError).toBe('');
    expect(r.certError).toBe('');
    expect(r.error).toBe('route down');
    expect(r.clusters).toHaveLength(1);
    expect(r.rows).toEqual([]);
  });

  it('按接口归因：仅集群失败 → clusterError 有值，路由行仍解析（目标集群列退化为待接入）', async () => {
    const req: Req = async (opt) => {
      if (String(opt.url ?? '') === '/Gateway/GatewayCluster') throw new Error('cluster down');
      return { code: 0, message: 'ok', data: [{ Id: 1, Name: 'r1', Enable: true }] };
    };
    const r = await fetchGateway(req, 3);
    expect(r.clusterError).toBe('cluster down');
    expect(r.routeError).toBe('');
    expect(r.rows).toHaveLength(1);
    expect(r.rows[0].clusterName).toBe('');
  });
});
