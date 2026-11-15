// 网关集群维护纯逻辑（CARD-GW-01，来源旅程 J-GW-02；2026-11-07）。
// 路由契约（deploy.logic.ts:2-8 同款，2026-11-07 冒烟实测 6681：GET /api/Gateway/GatewayCluster →
// {page, code, data: 裸数组}；CRUD 与 GatewayRoute 同为 EntityController 标准形状）：
//  - 编辑一律按 id 拉 Detail 回填（列表归一行丢健康检查等字段，同 CARD-GW-03 定稿裁定 5）
//  - 请求统一相对路径（request 拼 baseURL，勿写 /api，契约=deploy.logic.ts:2-8）
//  - 无敏感字段（盘点 §5「加密：无」）；ActiveNodes 为扩展属性（网关集群.Biz.cs:32），6681 冒烟
//    库 0 条无法核实序列化（定稿裁定 3）→ 归一取不到置 null，UI 显「—」，不造数。
// 筛选语义（后端 Search 已实现，控制器 Search 读 projectId/sessionSticky/enable/dtStart/dtEnd/Q）：
// projectId（顶栏上下文透传，不设控件）、enable/sessionSticky（开关，勾选=只看该状态）、
// dtStart/dtEnd（过滤 UpdateTime，控件注记「按更新时间过滤」）、Q。
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

/** 负载均衡枚举（实体 Model.xml:49，默认 RoundRobin）→ 中文映射沿旧壳 gateway.model.ts:60-74，未知兜底原始串 */
export function loadBalanceLabel(v: unknown): string {
  const s = str(v);
  if (s === 'RoundRobin') return '轮询（RoundRobin）';
  if (s === 'LeastConnection') return '最少连接（LeastConnection）';
  if (s === 'IPHash') return 'IP 哈希（IPHash）';
  return s;
}

/** 集群列表归一行（区块 9 列：名称/项目/负载均衡/健康检查/会话保持/启用/在线节点/更新时间/操作） */
export interface ClusterRow {
  id: number;
  projectId: number;
  /** 实体扩展属性 ProjectName；取不到显示「—」 */
  projectName: string;
  name: string;
  loadBalance: string;
  healthPath: string;
  healthInterval: number;
  healthTimeout: number;
  unhealthyThreshold: number;
  healthyThreshold: number;
  sessionSticky: boolean;
  sessionStickyName: string;
  enable: boolean;
  /** 扩展属性 ActiveNodes：序列化未核实（定稿裁定 3），取不到置 null → UI 显「—」；0 是有效值显示 0 */
  activeNodes: number | null;
  updateTime: string;
}

/** 集群表单（除 Id 外全部业务字段含 Remark，CARD-GW-01 数据契约 C11） */
export interface GatewayClusterForm {
  projectId: number;
  name: string;
  loadBalance: string;
  healthPath: string;
  healthInterval: number;
  healthTimeout: number;
  unhealthyThreshold: number;
  healthyThreshold: number;
  sessionSticky: boolean;
  sessionStickyName: string;
  enable: boolean;
  remark: string;
}

/** 新建表单默认值（=网关集群.cs:56-120 BindColumn DefaultValue：LoadBalance RoundRobin、
 * HealthInterval 10 秒、HealthTimeout 3000 毫秒、UnhealthyThreshold 3、HealthyThreshold 2、Enable 开） */
export function emptyClusterForm(): GatewayClusterForm {
  return {
    projectId: 0,
    name: '',
    loadBalance: 'RoundRobin',
    healthPath: '',
    healthInterval: 10,
    healthTimeout: 3000,
    unhealthyThreshold: 3,
    healthyThreshold: 2,
    sessionSticky: false,
    sessionStickyName: '',
    enable: true,
    remark: '',
  };
}

/** Detail 实体 → 表单（编辑回填全字段；字段大小写两种都认） */
export function detailToClusterForm(d: Rec): GatewayClusterForm {
  return {
    projectId: num(pick(d, 'ProjectId', 'projectId')),
    name: str(pick(d, 'Name', 'name')),
    loadBalance: str(pick(d, 'LoadBalance', 'loadBalance')) || 'RoundRobin',
    healthPath: str(pick(d, 'HealthPath', 'healthPath')),
    healthInterval: num(pick(d, 'HealthInterval', 'healthInterval')),
    healthTimeout: num(pick(d, 'HealthTimeout', 'healthTimeout')),
    unhealthyThreshold: num(pick(d, 'UnhealthyThreshold', 'unhealthyThreshold')),
    healthyThreshold: num(pick(d, 'HealthyThreshold', 'healthyThreshold')),
    sessionSticky: bool(pick(d, 'SessionSticky', 'sessionSticky')),
    sessionStickyName: str(pick(d, 'SessionStickyName', 'sessionStickyName')),
    enable: bool(pick(d, 'Enable', 'enable')),
    remark: str(pick(d, 'Remark', 'remark')),
  };
}

/** 列表原始行 → 归一行（ActiveNodes 缺失置 null；ProjectName 扩展属性取不到为空串） */
export function toClusterRows(rows: Rec[]): ClusterRow[] {
  const out: ClusterRow[] = [];
  for (const r of rows) {
    const rawNodes = pick(r, 'ActiveNodes', 'activeNodes');
    const n = rawNodes === undefined || rawNodes === null || str(rawNodes) === '' ? NaN : Number(rawNodes);
    out.push({
      id: num(pick(r, 'Id', 'id')),
      projectId: num(pick(r, 'ProjectId', 'projectId')),
      projectName: str(pick(r, 'ProjectName', 'projectName')),
      name: str(pick(r, 'Name', 'name')),
      loadBalance: str(pick(r, 'LoadBalance', 'loadBalance')),
      healthPath: str(pick(r, 'HealthPath', 'healthPath')),
      healthInterval: num(pick(r, 'HealthInterval', 'healthInterval')),
      healthTimeout: num(pick(r, 'HealthTimeout', 'healthTimeout')),
      unhealthyThreshold: num(pick(r, 'UnhealthyThreshold', 'unhealthyThreshold')),
      healthyThreshold: num(pick(r, 'HealthyThreshold', 'healthyThreshold')),
      sessionSticky: bool(pick(r, 'SessionSticky', 'sessionSticky')),
      sessionStickyName: str(pick(r, 'SessionStickyName', 'sessionStickyName')),
      enable: bool(pick(r, 'Enable', 'enable')),
      activeNodes: Number.isFinite(n) ? n : null,
      updateTime: fmtTime(str(pick(r, 'UpdateTime', 'updateTime'))),
    });
  }
  return out;
}

/** 宽松解析时间（兼容 'YYYY-MM-DD HH:mm:ss' 与 ISO），失败返回「—」（mesh.logic 同款） */
function fmtTime(v: string): string {
  if (!v) return '—';
  const t = Date.parse(v.replace(' ', 'T'));
  if (!Number.isFinite(t)) return '—';
  const d = new Date(t);
  const p = (x: number): string => String(x).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`;
}

/** 表单字段 → 提交体字段名（实体 PascalCase；GetSubmittedFields 大小写不敏感） */
const FORM_FIELDS: ReadonlyArray<readonly [keyof GatewayClusterForm, string]> = [
  ['projectId', 'ProjectId'],
  ['name', 'Name'],
  ['loadBalance', 'LoadBalance'],
  ['healthPath', 'HealthPath'],
  ['healthInterval', 'HealthInterval'],
  ['healthTimeout', 'HealthTimeout'],
  ['unhealthyThreshold', 'UnhealthyThreshold'],
  ['healthyThreshold', 'HealthyThreshold'],
  ['sessionSticky', 'SessionSticky'],
  ['sessionStickyName', 'SessionStickyName'],
  ['enable', 'Enable'],
  ['remark', 'Remark'],
];

/** 内联校验：Name 必填（后端 ArgumentNullException 同义，网关集群.Biz.cs:25） */
export function validateClusterForm(m: GatewayClusterForm): { name?: string } {
  const errs: { name?: string } = {};
  if (!m.name.trim()) errs.name = '名称不能为空';
  return errs;
}

/** 新建提交体：全字段（body 不带 id，布尔发布尔） */
export function clusterInsertPayload(m: GatewayClusterForm): Rec {
  const body: Rec = {};
  for (const [key, backing] of FORM_FIELDS) body[backing] = m[key];
  return body;
}

/** 编辑提交体：部分更新——与 Detail 逐字段比对，只提交变更顶层字段（不提交=不改，机制同 CARD-GW-03） */
export function clusterUpdatePayload(detail: Rec, m: GatewayClusterForm): Rec {
  const base = detailToClusterForm(detail);
  const body: Rec = {};
  for (const [key, backing] of FORM_FIELDS) {
    if (m[key] !== base[key]) body[backing] = m[key];
  }
  return body;
}

/** 集群筛选条状态（C07：开关勾选=只看该状态；时间区间注记「按更新时间过滤」；projectId 走顶栏不设控件） */
export interface GatewayClusterFilter {
  /** 勾选=只看启用集群；不勾=不过滤 */
  enable: boolean;
  /** 勾选=只看会话保持集群；不勾=不过滤 */
  sessionSticky: boolean;
  /** 时间区间，后端过滤 UpdateTime（控件注记「按更新时间过滤」） */
  dtStart: string;
  dtEnd: string;
  /** 关键字 → Q */
  q: string;
}

export function emptyClusterFilter(): GatewayClusterFilter {
  return { enable: false, sessionSticky: false, dtStart: '', dtEnd: '', q: '' };
}

export function isClusterFilterActive(f: GatewayClusterFilter): boolean {
  const d = emptyClusterFilter();
  return f.enable !== d.enable || f.sessionSticky !== d.sessionSticky || f.dtStart !== d.dtStart || f.dtEnd !== d.dtEnd || f.q !== d.q;
}

/** 筛选参数拼装：开关不勾/时间空/Q 空=不传（Q 映射后端参数名） */
export function clusterFilterParams(f: GatewayClusterFilter): Record<string, unknown> {
  const p: Record<string, unknown> = {};
  if (f.enable) p.enable = true;
  if (f.sessionSticky) p.sessionSticky = true;
  if (f.dtStart.trim()) p.dtStart = f.dtStart.trim();
  if (f.dtEnd.trim()) p.dtEnd = f.dtEnd.trim();
  if (f.q.trim()) p.Q = f.q.trim();
  return p;
}

/** 单条 Detail：GET /Gateway/GatewayCluster/{id}；无数据/HTTP 错 → null（调用方显示失败态） */
export async function fetchClusterDetail(request: Req, id: number): Promise<Rec | null> {
  try {
    const res = await request({ url: `/Gateway/GatewayCluster/${id}`, method: 'GET' });
    const d = (res as Rec)?.data;
    return d && !Array.isArray(d) ? (d as Rec) : null;
  } catch {
    return null;
  }
}

/** 新增/编辑提交：编辑只提交变更字段（PUT /{id}）；失败返回 ok=false 由调用方 toast */
export async function submitCluster(request: Req, m: GatewayClusterForm, id?: number): Promise<{ ok: boolean; message: string }> {
  try {
    if (id) {
      const detail = await fetchClusterDetail(request, id);
      if (!detail) return { ok: false, message: '集群不存在或已被删除' };
      const res = await request({ url: `/Gateway/GatewayCluster/${id}`, method: 'PUT', data: clusterUpdatePayload(detail, m) } as ReqOptions & { data: Rec });
      return okOf(res as Rec, '集群已保存');
    }
    const res = await request({ url: '/Gateway/GatewayCluster', method: 'POST', data: clusterInsertPayload(m) } as ReqOptions & { data: Rec });
    return okOf(res as Rec, '集群已创建');
  } catch (e) {
    return { ok: false, message: errText(e, '集群保存失败') };
  }
}

/** 删除：DELETE /Gateway/GatewayCluster/{id}（后端级联删其下 GatewayNode，网关集群.Biz.cs:47-50） */
export async function removeCluster(request: Req, id: number): Promise<{ ok: boolean; message: string }> {
  try {
    const res = await request({ url: `/Gateway/GatewayCluster/${id}`, method: 'DELETE' });
    return okOf(res as Rec, '集群已删除');
  } catch (e) {
    return { ok: false, message: errText(e, '集群删除失败') };
  }
}
