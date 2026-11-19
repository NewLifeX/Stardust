// 网关节点维护纯逻辑单测（CARD-GW-02，2026-11-07）。
// 覆盖：健康三态映射（旧壳旁证）、列表归一与 ClusterName 解析「—」兜底、id 升序稳定排序、
// 表单默认值（实体 DefaultValue 1/3/60 + Enable/IsHealthy 开）、Detail 回填（Pascal/camel 双认）、
// 校验（Address 必填 C07 + ClusterId 必选）、提交体（新增全字段不带 Id / 编辑 diff 只提交变更字段）、
// 筛选参数拼装（**永不产生 projectId**——接口语义差异，定稿裁定 1 注记）、
// 节点取数（PageSize=500、params 无 projectId、原始行透传、错误归因）、
// 本地过滤（定稿裁定 1：按可见集群收节点 / 集群缺失不过滤 / 集群筛选激活不过滤 / 无主节点保留）、
// Detail 拉取失败返回 null、提交先拉 Detail（Detail 不可达不 PUT，按方法记键断言）、删除。
import { describe, it, expect } from 'vitest';
import {
  nodeHealthTag, toNodeRows, emptyNodeForm, detailToNodeForm, validateNodeForm,
  nodeInsertPayload, nodeUpdatePayload, emptyNodeFilter, isNodeFilterActive, nodeFilterParams,
  fetchGatewayNodes, filterNodesByVisibleClusters, fetchNodeDetail, submitNode, removeNode,
} from './gatewayNode.logic';
import { buildClusterMap } from './mesh.logic';
import type { Req, ReqOptions } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

const DETAIL: Record<string, unknown> = {
  Id: 41, ClusterId: 2, Name: 'api-node', Address: 'http://10.0.0.12:8080',
  Weight: 2, Enable: true, IsHealthy: false, MaxFails: 5, FailTimeout: 30, Remark: '备用节点',
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
    if (url in map) return { code: 0, message: '', data: map[url] };
    return { code: 0, message: '', data: [] };
  };
}

const CLUSTERS: Record<string, unknown>[] = [{ Id: 2, Name: 'iot-cluster', ProjectId: 3 }, { Id: 5, Name: 'api-cluster', ProjectId: 3 }];

describe('健康三态与列表归一', () => {
  it('nodeHealthTag 三态：启用且健康=运行(ok)/启用不健康=异常(err)/停用=离线(default)', () => {
    expect(nodeHealthTag(true, true)).toEqual({ label: '运行', tone: 'ok' });
    expect(nodeHealthTag(true, false)).toEqual({ label: '异常', tone: 'err' });
    expect(nodeHealthTag(false, true)).toEqual({ label: '离线', tone: 'default' });
    expect(nodeHealthTag(false, false)).toEqual({ label: '离线', tone: 'default' });
  });

  it('toNodeRows 归一 + ClusterName 按集群映射解析，解析不到为空串（组件显「—」）', () => {
    const rows = toNodeRows(
      [{ Id: 41, ClusterId: 2, Name: 'api-node', Address: 'http://10.0.0.12:8080', Enable: true, IsHealthy: false, Weight: 2, MaxFails: 5, FailTimeout: 30 },
       { id: 42, clusterId: 9, name: '', address: '10.0.0.13:80', enable: false, isHealthy: true }],
      buildClusterMap(CLUSTERS),
    );
    expect(rows).toHaveLength(2);
    expect(rows[0]).toMatchObject({ id: 41, clusterId: 2, clusterName: 'iot-cluster', enable: true, isHealthy: false, weight: 2, maxFails: 5, failTimeout: 30 });
    expect(rows[1].clusterName).toBe('');
    expect(rows[1].name).toBe('');
  });

  it('toNodeRows 按 id 升序稳定排序（乱序入参也输出有序）', () => {
    const rows = toNodeRows(
      [{ Id: 45, Address: 'a' }, { Id: 43, Address: 'b' }, { Id: 44, Address: 'c' }],
      new Map(),
    );
    expect(rows.map((r) => r.id)).toEqual([43, 44, 45]);
  });
});

describe('表单装配与校验', () => {
  it('emptyNodeForm 默认值：Weight 1 / MaxFails 3 / FailTimeout 60 / Enable 开 / IsHealthy 开（实体 DefaultValue）', () => {
    expect(emptyNodeForm()).toEqual({
      clusterId: 0, name: '', address: '', weight: 1, enable: true, isHealthy: true, maxFails: 3, failTimeout: 60, remark: '',
    });
  });

  it('detailToNodeForm 回填全字段（大小写两种都认）', () => {
    expect(detailToNodeForm(DETAIL)).toEqual({
      clusterId: 2, name: 'api-node', address: 'http://10.0.0.12:8080', weight: 2, enable: true,
      isHealthy: false, maxFails: 5, failTimeout: 30, remark: '备用节点',
    });
    expect(detailToNodeForm({ clusterId: 1, name: 'n', address: 'a', weight: 0, maxFails: 0, failTimeout: 0 }).weight).toBe(1);
    expect(detailToNodeForm({ clusterId: 1, name: 'n', address: 'a', weight: 0 }).maxFails).toBe(3);
    expect(detailToNodeForm({ clusterId: 1, name: 'n', address: 'a', weight: 0 }).failTimeout).toBe(60);
  });

  it('validateNodeForm：地址空 →「地址不能为空」（C07）；集群未选 →「请选择所属集群」；全填 → 无错误', () => {
    expect(validateNodeForm(emptyNodeForm())).toEqual({ address: '地址不能为空', clusterId: '请选择所属集群' });
    expect(validateNodeForm({ ...emptyNodeForm(), clusterId: 2 }).address).toBe('地址不能为空');
    expect(validateNodeForm({ ...emptyNodeForm(), address: ' ' }).clusterId).toBe('请选择所属集群');
    expect(validateNodeForm({ ...emptyNodeForm(), clusterId: 2, address: 'http://10.0.0.1:80' })).toEqual({});
  });
});

describe('提交体', () => {
  it('nodeInsertPayload：全字段 PascalCase，body 不含 Id', () => {
    const body = nodeInsertPayload({ ...emptyNodeForm(), clusterId: 2, name: 'n1', address: 'http://10.0.0.1:80' });
    expect(Object.keys(body).sort()).toEqual(['Address', 'ClusterId', 'Enable', 'FailTimeout', 'IsHealthy', 'MaxFails', 'Name', 'Remark', 'Weight']);
    expect(body).toMatchObject({ ClusterId: 2, Name: 'n1', Address: 'http://10.0.0.1:80', Weight: 1, Enable: true, IsHealthy: true, MaxFails: 3, FailTimeout: 60 });
    expect('Id' in body).toBe(false);
  });

  it('nodeUpdatePayload：diff 只提交变更字段；无变更 → 空体（不提交=不改）', () => {
    const changed = nodeUpdatePayload(DETAIL, { ...detailToNodeForm(DETAIL), weight: 9, remark: '改备注' });
    expect(changed).toEqual({ Weight: 9, Remark: '改备注' });
    expect(nodeUpdatePayload(DETAIL, detailToNodeForm(DETAIL))).toEqual({});
  });
});

describe('筛选参数：C04 语义且永不带 projectId', () => {
  it('nodeFilterParams 全量拼装：clusterId/address/isHealthy/enable/dtStart/dtEnd/Q；三态映射布尔', () => {
    const p = nodeFilterParams({ clusterId: 2, address: '10.0.0.1', isHealthy: 'unhealthy', enable: true, dtStart: '2026-11-01', dtEnd: '2026-11-07', q: 'api' });
    expect(p).toEqual({ clusterId: 2, address: '10.0.0.1', isHealthy: false, enable: true, dtStart: '2026-11-01', dtEnd: '2026-11-07', Q: 'api' });
    expect(nodeFilterParams({ ...emptyNodeFilter(), isHealthy: 'healthy' })).toEqual({ isHealthy: true });
  });

  it('空筛选=空参数；projectId 即使外部污染入参也不会出现在输出键（接口无该参数——定稿裁定 1）', () => {
    expect(nodeFilterParams(emptyNodeFilter())).toEqual({});
    const p = nodeFilterParams(emptyNodeFilter());
    expect('projectId' in p).toBe(false);
    expect(isNodeFilterActive(emptyNodeFilter())).toBe(false);
    expect(isNodeFilterActive({ ...emptyNodeFilter(), address: 'x' })).toBe(true);
  });
});

describe('节点取数与本地过滤（独立于三接口聚合，定稿裁定 5）', () => {
  it('fetchGatewayNodes：GET /Gateway/GatewayNode，PageSize=500 且 **params 永无 projectId**；原始行透传', async () => {
    const calls: Call[] = [];
    const req = stubReq({ '/Gateway/GatewayNode': [{ Id: 41, Address: 'a' }] }, { calls });
    const r = await fetchGatewayNodes(req, { ...emptyNodeFilter(), enable: true });
    expect(r.rows).toEqual([{ Id: 41, Address: 'a' }]);
    expect(r.error).toBe('');
    expect(calls[0].method).toBe('GET');
    expect(calls[0].url).toBe('/Gateway/GatewayNode');
    expect(calls[0].params).toEqual({ PageSize: 500, enable: true });
    expect('projectId' in calls[0].params).toBe(false);
  });

  it('fetchGatewayNodes 失败：rows=[] + error 归因文案（不抛栈、不并入路由聚合）', async () => {
    const r = await fetchGatewayNodes(stubReq({}, { throwOn: ['/Gateway/GatewayNode'] }));
    expect(r.rows).toEqual([]);
    expect(r.error).toBe('stub 500:/Gateway/GatewayNode');
  });

  it('filterNodesByVisibleClusters：按可见集群 id 收节点；集群缺失或筛选激活时不过滤（宁多勿错）；无主节点保留', () => {
    const nodes = toNodeRows(
      [{ Id: 41, ClusterId: 2, Address: 'a' }, { Id: 42, ClusterId: 9, Address: 'b' }, { Id: 43, ClusterId: 0, Address: 'c' }],
      new Map(),
    );
    const visible = [{ Id: 2, Name: 'iot-cluster' }];
    expect(filterNodesByVisibleClusters(nodes, visible, false).map((n) => n.id)).toEqual([41, 43]);
    // 集群数据缺失 → 不过滤
    expect(filterNodesByVisibleClusters(nodes, [], false)).toHaveLength(3);
    // 集群筛选条激活（clusters 为子集）→ 不过滤
    expect(filterNodesByVisibleClusters(nodes, visible, true)).toHaveLength(3);
  });
});

describe('Detail / 提交 / 删除', () => {
  it('fetchNodeDetail：返回 data 对象；404/错误 → null', async () => {
    expect(await fetchNodeDetail(stubReq({ '/Gateway/GatewayNode/41': DETAIL }), 41)).toEqual(DETAIL);
    expect(await fetchNodeDetail(stubReq({}, { throwOn: ['/Gateway/GatewayNode/41'] }), 41)).toBeNull();
  });

  it('submitNode 新增：POST /Gateway/GatewayNode，body 不带 Id', async () => {
    const calls: Call[] = [];
    const req = stubReq({}, { calls });
    const r = await submitNode(req, { ...emptyNodeForm(), clusterId: 2, address: 'http://10.0.0.1:80' });
    expect(r.ok).toBe(true);
    expect(r.message).toBe('节点已创建');
    expect(calls[0].method).toBe('POST');
    expect(calls[0].url).toBe('/Gateway/GatewayNode');
    expect('Id' in (calls[0].body ?? {})).toBe(false);
  });

  it('submitNode 编辑：先 GET Detail 再 PUT /{id} diff（按方法记键断言调用序列）；Detail 不可达不 PUT', async () => {
    const calls: Call[] = [];
    const req = stubReq({ '/Gateway/GatewayNode/41': DETAIL }, { calls });
    const r = await submitNode(req, { ...detailToNodeForm(DETAIL), weight: 9 }, 41);
    expect(r.ok).toBe(true);
    expect(r.message).toBe('节点已保存');
    expect(calls.map((c) => `${c.method} ${c.url}`)).toEqual(['GET /Gateway/GatewayNode/41', 'PUT /Gateway/GatewayNode/41']);
    expect(calls[1].body).toEqual({ Weight: 9 });

    const calls2: Call[] = [];
    const r2 = await submitNode(stubReq({}, { calls: calls2 }), { ...emptyNodeForm() }, 41);
    expect(r2.ok).toBe(false);
    expect(r2.message).toBe('节点不存在或已被删除');
    expect(calls2).toHaveLength(1);
    expect(calls2[0].method).toBe('GET');
  });

  it('removeNode：DELETE /Gateway/GatewayNode/{id}', async () => {
    const calls: Call[] = [];
    const req = stubReq({}, { calls });
    const r = await removeNode(req, 41);
    expect(r.ok).toBe(true);
    expect(r.message).toBe('节点已删除');
    expect(calls[0]).toMatchObject({ method: 'DELETE', url: '/Gateway/GatewayNode/41' });
  });
});
