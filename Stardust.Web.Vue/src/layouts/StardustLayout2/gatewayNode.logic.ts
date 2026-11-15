// 网关节点维护纯逻辑（CARD-GW-02，来源旅程 J-GW-03；2026-11-07）。
// 路由契约（deploy.logic.ts:2-8 同款；2026-11-07 回读后端源码核实）：
//  - 控制器 GatewayNodeController.cs 45 行纯 EntityController，无自定义 Action；Search 读
//    clusterId(≥0 精确)/address(精确)/isHealthy/enable/dtStart/dtEnd(过滤 UpdateTime)/Q（控制器 :33-44，
//    网关节点.cs:254-266 八参重载）——**无 projectId 参数**：节点请求永不带 projectId
//    （fetchGatewayNodes 签名即无 projectId，gatewayNode.logic.test 固化断言）
//  - 列头 IsHealthy→「健康状态」控制器 :20 已改写，前端列头同文案（C05 硬性条目）
//  - 实体 Valid：Address 必填（ArgumentNullException，网关节点.Biz.cs:33）；Weight≤0→1 / MaxFails≤0→3 /
//    FailTimeout≤0→60（:35-37）——前端默认值预填 + min=1 双保险，不改后端
//  - 实体 BindColumn DefaultValue：Weight 1 / Enable True / IsHealthy True / MaxFails 3 / FailTimeout 60
//  - 编辑一律按 id 拉 Detail 回填（列表归一行缺字段，同 CARD-GW-03 定稿裁定 5）
// 项目上下文（CARD-GW-02 定稿裁定 1）：节点接口无 projectId → 节点区块按「当前项目可见集群」本地过滤：
// 可见集群=fetchGateway 透出的 clusters 原始行（集群请求已带 projectId 服务端过滤=项目全集）；
// 集群数据缺失（空）或集群筛选条激活（clusters 此时是筛选子集，不能代表项目全集）时不过滤——宁多勿错防假空。
// 健康三态 Tag 沿旧壳映射（gateway.model.ts:90-93）：启用且健康=运行（ok）/启用不健康=异常（err，旧壳 danger 同义红）/停用=离线（default）。
import type { Req, ReqOptions } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

type Rec = Record<string, unknown>;

function pick(r: Rec, ...keys: string[]): unknown {
  for (const k of keys) {
    const v = r[k];
    if (v !== undefined && v !== null) return v;
  }
  return undefined;
}
function num(v: unknown): number {
  const n = Number(v);
  return Number.isFinite(n) ? n : 0;
}
function str(v: unknown): string {
  return v === undefined || v === null ? '' : String(v).trim();
}
function bool(v: unknown): boolean {
  if (typeof v === 'boolean') return v;
  if (typeof v === 'number') return v !== 0;
  return str(v).toLowerCase() === 'true' || str(v) === '1';
}
function errText(e: unknown, fallback: string): string {
  const msg = e instanceof Error ? e.message : typeof e === 'string' ? e : '';
  return msg || fallback;
}
function okOf(res: { message?: unknown } | undefined, fallback: string): { ok: boolean; message: string } {
  const code = res && (res as Rec).code !== undefined ? num((res as Rec).code) : -1;
  const message = res ? str(res.message) : '';
  return { ok: code === 0, message: message || fallback };
}

/** 健康三态 Tag（旧壳旁证 gateway.model.ts:90-93；C05：与「启用」列并存，两列各自真实值，不合并掩盖）。
 * tone 用 Tag 组件语汇：ok/err/default（旧壳 danger 对应本壳 err 红色调）。 */
export function nodeHealthTag(enable: boolean, isHealthy: boolean): { label: string; tone: 'ok' | 'err' | 'default' } {
  if (!enable) return { label: '离线', tone: 'default' };
  return isHealthy ? { label: '运行', tone: 'ok' } : { label: '异常', tone: 'err' };
}

/** 节点列表归一行（区块 9 列：名称/地址/所属集群/健康状态/权重/最大失败次数/失败超时/启用/操作，无 Id/元数据列） */
export interface NodeRow {
  id: number;
  /** 实体 Name；UI 缺省显 Address（旧壳旁证） */
  name: string;
  address: string;
  clusterId: number;
  /** ClusterId 查集群表映射；解析不到为空串，组件显示「—」 */
  clusterName: string;
  enable: boolean;
  isHealthy: boolean;
  weight: number;
  maxFails: number;
  failTimeout: number;
}

/** GatewayNode 行 → 归一行；clusterMap=buildClusterMap（mesh.logic 纯函数导入复用）；按 id 升序稳定排序 */
export function toNodeRows(rows: Rec[], clusterMap: Map<number, string>): NodeRow[] {
  const out: NodeRow[] = [];
  for (const r of rows) {
    const clusterId = num(pick(r, 'ClusterId', 'clusterId'));
    out.push({
      id: num(pick(r, 'Id', 'id')),
      name: str(pick(r, 'Name', 'name')),
      address: str(pick(r, 'Address', 'address')),
      clusterId,
      clusterName: clusterMap.get(clusterId) ?? '',
      enable: bool(pick(r, 'Enable', 'enable')),
      isHealthy: bool(pick(r, 'IsHealthy', 'isHealthy')),
      weight: num(pick(r, 'Weight', 'weight')),
      maxFails: num(pick(r, 'MaxFails', 'maxFails')),
      failTimeout: num(pick(r, 'FailTimeout', 'failTimeout')),
    });
  }
  out.sort((a, b) => a.id - b.id);
  return out;
}

/** 节点表单（除 Id 外全部业务字段含 Remark 与 IsHealthy，盘点 §8 硬性条目；C08） */
export interface GatewayNodeForm {
  clusterId: number;
  name: string;
  address: string;
  weight: number;
  enable: boolean;
  /** 运行时状态：进表单但旁注明示语义（定稿裁定 2，C15） */
  isHealthy: boolean;
  maxFails: number;
  failTimeout: number;
  remark: string;
}

/** 新建表单默认值 =实体 DefaultValue + Valid 兜底（Weight 1 / MaxFails 3 / FailTimeout 60 / Enable 开 / IsHealthy 开） */
export function emptyNodeForm(): GatewayNodeForm {
  return {
    clusterId: 0,
    name: '',
    address: '',
    weight: 1,
    enable: true,
    isHealthy: true,
    maxFails: 3,
    failTimeout: 60,
    remark: '',
  };
}

/** Detail 实体 → 表单（编辑回填全字段；字段大小写两种都认） */
export function detailToNodeForm(d: Rec): GatewayNodeForm {
  return {
    clusterId: num(pick(d, 'ClusterId', 'clusterId')),
    name: str(pick(d, 'Name', 'name')),
    address: str(pick(d, 'Address', 'address')),
    weight: num(pick(d, 'Weight', 'weight')) || 1,
    enable: bool(pick(d, 'Enable', 'enable')),
    isHealthy: bool(pick(d, 'IsHealthy', 'isHealthy')),
    maxFails: num(pick(d, 'MaxFails', 'maxFails')) || 3,
    failTimeout: num(pick(d, 'FailTimeout', 'failTimeout')) || 60,
    remark: str(pick(d, 'Remark', 'remark')),
  };
}

/** 表单字段 → 提交体字段名（实体 PascalCase；GetSubmittedFields 大小写不敏感） */
const FORM_FIELDS: ReadonlyArray<readonly [keyof GatewayNodeForm, string]> = [
  ['clusterId', 'ClusterId'],
  ['name', 'Name'],
  ['address', 'Address'],
  ['weight', 'Weight'],
  ['enable', 'Enable'],
  ['isHealthy', 'IsHealthy'],
  ['maxFails', 'MaxFails'],
  ['failTimeout', 'FailTimeout'],
  ['remark', 'Remark'],
];

/** 内联校验：Address 必填（后端 ArgumentNullException 同义，网关节点.Biz.cs:33，C07）；
 * ClusterId 必选（卡数据契约标注 *；实体不强制，前端收紧，缺省 0 视为未选择） */
export function validateNodeForm(m: GatewayNodeForm): { address?: string; clusterId?: string } {
  const errs: { address?: string; clusterId?: string } = {};
  if (!m.address.trim()) errs.address = '地址不能为空';
  if (!(m.clusterId > 0)) errs.clusterId = '请选择所属集群';
  return errs;
}

/** 新建提交体：全字段（body 不带 id，布尔发布尔） */
export function nodeInsertPayload(m: GatewayNodeForm): Rec {
  const body: Rec = {};
  for (const [key, backing] of FORM_FIELDS) body[backing] = m[key];
  return body;
}

/** 编辑提交体：部分更新——与 Detail 逐字段比对，只提交变更顶层字段（不提交=不改，机制同 CARD-GW-03） */
export function nodeUpdatePayload(detail: Rec, m: GatewayNodeForm): Rec {
  const base = detailToNodeForm(detail);
  const body: Rec = {};
  for (const [key, backing] of FORM_FIELDS) {
    if (m[key] !== base[key]) body[backing] = m[key];
  }
  return body;
}

/** 节点筛选条状态（C04 查询语义：clusterId/address/isHealthy/enable/dtStart-dtEnd/Q；projectId 不设控件也不传参） */
export interface GatewayNodeFilter {
  /** 所属集群下拉；0=全部集群（不传参；后端 clusterId ≥0 精确，传 0 会过滤无主节点，故 0=不传） */
  clusterId: number;
  /** 地址（后端 Address 精确匹配） */
  address: string;
  /** ''=全部；'healthy'=健康(isHealthy=true)；'unhealthy'=异常(isHealthy=false) */
  isHealthy: '' | 'healthy' | 'unhealthy';
  /** 勾选=只看启用节点；不勾=不过滤 */
  enable: boolean;
  /** 时间区间，后端过滤 UpdateTime（控件注记「按更新时间过滤」） */
  dtStart: string;
  dtEnd: string;
  /** 关键字 → Q */
  q: string;
}

export function emptyNodeFilter(): GatewayNodeFilter {
  return { clusterId: 0, address: '', isHealthy: '', enable: false, dtStart: '', dtEnd: '', q: '' };
}

export function isNodeFilterActive(f: GatewayNodeFilter): boolean {
  const d = emptyNodeFilter();
  return (
    f.clusterId !== d.clusterId ||
    f.address !== d.address ||
    f.isHealthy !== d.isHealthy ||
    f.enable !== d.enable ||
    f.dtStart !== d.dtStart ||
    f.dtEnd !== d.dtEnd ||
    f.q !== d.q
  );
}

/** 筛选参数拼装：空值语义=不传（Q 映射后端参数名）。**永不带 projectId**（接口语义差异，定稿裁定 1 注记） */
export function nodeFilterParams(f: GatewayNodeFilter): Record<string, unknown> {
  const p: Record<string, unknown> = {};
  if (f.clusterId > 0) p.clusterId = f.clusterId;
  if (f.address.trim()) p.address = f.address.trim();
  if (f.isHealthy === 'healthy') p.isHealthy = true;
  if (f.isHealthy === 'unhealthy') p.isHealthy = false;
  if (f.enable) p.enable = true;
  if (f.dtStart.trim()) p.dtStart = f.dtStart.trim();
  if (f.dtEnd.trim()) p.dtEnd = f.dtEnd.trim();
  if (f.q.trim()) p.Q = f.q.trim();
  return p;
}

/**
 * 节点列表：GET /Gateway/GatewayNode（PageSize=500 全量 + 本地排序，不做服务端分页 UI）。
 * 独立于 mesh.logic fetchGateway 三接口错误聚合（定稿裁定 5：节点区块自带三态，不并入「网关路由加载失败」，
 * 保 J-GW-04 C13）。原始行透传，ClusterName 由调用方用 buildClusterMap 归一（同 clusters/certs 透传形态）。
 */
export async function fetchGatewayNodes(request: Req, filter?: GatewayNodeFilter): Promise<{ rows: Rec[]; error: string }> {
  try {
    const fp = filter ? nodeFilterParams(filter) : {};
    const res = await request({ url: '/Gateway/GatewayNode', method: 'GET', params: { PageSize: 500, ...fp } });
    const d = res?.data;
    return { rows: Array.isArray(d) ? (d as Rec[]) : [], error: '' };
  } catch (e) {
    return { rows: [], error: errText(e, '节点加载失败') };
  }
}

/**
 * 定稿裁定 1 本地过滤：按「当前项目可见集群」收节点——可见集群 id 集=clusters 原始行（集群请求已带
 * projectId 服务端过滤）。集群数据缺失（空列表）或集群筛选条激活（clusters 是筛选子集，不代表项目全集）
 * 时不过滤——宁多勿错防假空；无主节点（clusterId≤0）不属于任何集群链，保留显示。
 */
export function filterNodesByVisibleClusters(nodes: NodeRow[], clusters: Rec[], clusterFilterActive: boolean): NodeRow[] {
  if (!clusters.length || clusterFilterActive) return nodes;
  const ids = new Set<number>();
  for (const c of clusters) {
    const id = num(pick(c, 'Id', 'id'));
    if (id > 0) ids.add(id);
  }
  return nodes.filter((n) => n.clusterId <= 0 || ids.has(n.clusterId));
}

/** 单条 Detail：GET /Gateway/GatewayNode/{id}；无数据/HTTP 错 → null（调用方显示失败态） */
export async function fetchNodeDetail(request: Req, id: number): Promise<Rec | null> {
  try {
    const res = await request({ url: `/Gateway/GatewayNode/${id}`, method: 'GET' });
    const d = (res as Rec)?.data;
    return d && !Array.isArray(d) ? (d as Rec) : null;
  } catch {
    return null;
  }
}

/** 新增/编辑提交：编辑只提交变更字段（PUT /{id}）；失败返回 ok=false 由调用方 toast */
export async function submitNode(request: Req, m: GatewayNodeForm, id?: number): Promise<{ ok: boolean; message: string }> {
  try {
    if (id) {
      const detail = await fetchNodeDetail(request, id);
      if (!detail) return { ok: false, message: '节点不存在或已被删除' };
      const res = await request({ url: `/Gateway/GatewayNode/${id}`, method: 'PUT', data: nodeUpdatePayload(detail, m) } as ReqOptions & { data: Rec });
      return okOf(res as Rec, '节点已保存');
    }
    const res = await request({ url: '/Gateway/GatewayNode', method: 'POST', data: nodeInsertPayload(m) } as ReqOptions & { data: Rec });
    return okOf(res as Rec, '节点已创建');
  } catch (e) {
    return { ok: false, message: errText(e, '节点保存失败') };
  }
}

/** 删除：DELETE /Gateway/GatewayNode/{id}（级联语义只发生在删集群侧，节点单删无级联） */
export async function removeNode(request: Req, id: number): Promise<{ ok: boolean; message: string }> {
  try {
    const res = await request({ url: `/Gateway/GatewayNode/${id}`, method: 'DELETE' });
    return okOf(res as Rec, '节点已删除');
  } catch (e) {
    return { ok: false, message: errText(e, '节点删除失败') };
  }
}
