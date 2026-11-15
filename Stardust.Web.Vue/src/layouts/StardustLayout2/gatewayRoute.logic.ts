// 网关路由维护纯逻辑（CARD-GW-03，来源旅程 J-GW-04；2026-11-07）。
// 路由契约（deploy.logic.ts:2-8 同款，2026-11-07 冒烟实测 6681：POST 无 id body / PUT /{id} 部分更新
// 只覆盖提交的顶层字段 / DELETE /{id} 全通）：
//  - 编辑一律按 id 拉 Detail 回填（列表归一行丢 Headers/Priority 等 9 字段，CARD-GW-03 定稿裁定 5）
//  - 请求统一相对路径（request 拼 baseURL，勿写 /api，契约=deploy.logic.ts:2-8）
//  - 无敏感字段（盘点 §5「加密：无」）；Headers/AddHeaders 自由文本不脱敏（CARD-GW-03 定稿裁定 6）
// 筛选语义（后端 Search 已实现，控制器 :36-49）：projectId（顶栏上下文透传，不设控件）、
// priority、clusterId（≥0 精确）、domain（精确）、stripPrefix/enable（布尔）、dtStart/dtEnd（过滤 UpdateTime）、Q。
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

/** 路由表单（除 Id 外全部业务字段，CARD-GW-03 数据契约；分组呈现见 Drawer） */
export interface GatewayRouteForm {
  projectId: number;
  name: string;
  enable: boolean;
  priority: number;
  clusterId: number;
  domain: string;
  path: string;
  methods: string;
  headers: string;
  stripPrefix: boolean;
  addHeaders: string;
  webSocket: boolean;
  isStaticRoute: boolean;
  staticRoot: string;
  indexFile: string;
  directoryBrowse: boolean;
  spaFallback: boolean;
  remark: string;
}

/** 新建表单默认值（Enable 开、Priority 0，其余空——实体默认值语义，CARD-GW-03） */
export function emptyRouteForm(): GatewayRouteForm {
  return {
    projectId: 0,
    name: '',
    enable: true,
    priority: 0,
    clusterId: 0,
    domain: '',
    path: '',
    methods: '',
    headers: '',
    stripPrefix: false,
    addHeaders: '',
    webSocket: false,
    isStaticRoute: false,
    staticRoot: '',
    indexFile: '',
    directoryBrowse: false,
    spaFallback: false,
    remark: '',
  };
}

/** Detail 实体 → 表单（编辑回填全字段；字段大小写两种都认） */
export function detailToRouteForm(d: Rec): GatewayRouteForm {
  return {
    projectId: num(pick(d, 'ProjectId', 'projectId')),
    name: str(pick(d, 'Name', 'name')),
    enable: bool(pick(d, 'Enable', 'enable')),
    priority: num(pick(d, 'Priority', 'priority')),
    clusterId: num(pick(d, 'ClusterId', 'clusterId')),
    domain: str(pick(d, 'Domain', 'domain')),
    path: str(pick(d, 'Path', 'path')),
    methods: str(pick(d, 'Methods', 'methods')),
    headers: str(pick(d, 'Headers', 'headers')),
    stripPrefix: bool(pick(d, 'StripPrefix', 'stripPrefix')),
    addHeaders: str(pick(d, 'AddHeaders', 'addHeaders')),
    webSocket: bool(pick(d, 'WebSocket', 'webSocket')),
    isStaticRoute: bool(pick(d, 'IsStaticRoute', 'isStaticRoute')),
    staticRoot: str(pick(d, 'StaticRoot', 'staticRoot')),
    indexFile: str(pick(d, 'IndexFile', 'indexFile')),
    directoryBrowse: bool(pick(d, 'DirectoryBrowse', 'directoryBrowse')),
    spaFallback: bool(pick(d, 'SPAFallback', 'spaFallback', 'SpaFallback')),
    remark: str(pick(d, 'Remark', 'remark')),
  };
}

/** 表单字段 → 提交体字段名（实体 PascalCase；GetSubmittedFields 大小写不敏感） */
const FORM_FIELDS: ReadonlyArray<readonly [keyof GatewayRouteForm, string]> = [
  ['projectId', 'ProjectId'],
  ['name', 'Name'],
  ['enable', 'Enable'],
  ['priority', 'Priority'],
  ['clusterId', 'ClusterId'],
  ['domain', 'Domain'],
  ['path', 'Path'],
  ['methods', 'Methods'],
  ['headers', 'Headers'],
  ['stripPrefix', 'StripPrefix'],
  ['addHeaders', 'AddHeaders'],
  ['webSocket', 'WebSocket'],
  ['isStaticRoute', 'IsStaticRoute'],
  ['staticRoot', 'StaticRoot'],
  ['indexFile', 'IndexFile'],
  ['directoryBrowse', 'DirectoryBrowse'],
  ['spaFallback', 'SPAFallback'],
  ['remark', 'Remark'],
];

/** 内联校验：Name 必填；IsStaticRoute=true 时 StaticRoot 必填（后端 ArgumentNullException / InvalidOperationException 同义，网关路由.Biz.cs:34-38） */
export function validateRouteForm(m: GatewayRouteForm): { name?: string; staticRoot?: string } {
  const errs: { name?: string; staticRoot?: string } = {};
  if (!m.name.trim()) errs.name = '名称不能为空';
  if (m.isStaticRoute && !m.staticRoot.trim()) errs.staticRoot = '开启静态文件路由时，静态文件根目录不能为空';
  return errs;
}

/** 新建提交体：全字段（body 不带 id，布尔发布尔） */
export function routeInsertPayload(m: GatewayRouteForm): Rec {
  const body: Rec = {};
  for (const [key, backing] of FORM_FIELDS) body[backing] = m[key];
  return body;
}

/** 编辑提交体：部分更新——与 Detail 逐字段比对，只提交变更字段（不提交=不改，CARD-GW-03 定稿裁定 5） */
export function routeUpdatePayload(detail: Rec, m: GatewayRouteForm): Rec {
  const base = detailToRouteForm(detail);
  const body: Rec = {};
  for (const [key, backing] of FORM_FIELDS) {
    if (m[key] !== base[key]) body[backing] = m[key];
  }
  return body;
}

/** 筛选条状态（C21：常用项 + 「更多筛选」折叠，语义与后端 Search 逐项一致） */
export interface GatewayRouteFilter {
  /** 启用状态：''=全部 / 'true' / 'false' */
  enable: string;
  /** 目标集群：0=全部 */
  clusterId: number;
  /** 域名（精确） */
  domain: string;
  /** 关键字 → Q */
  q: string;
  /** 优先级：null=不筛（更多筛选） */
  priority: number | null;
  /** 去前缀：false=不筛；开启=只看 stripPrefix=true（更多筛选） */
  stripPrefix: boolean;
  /** 时间区间，后端过滤 UpdateTime（更多筛选；控件注记「按更新时间过滤」） */
  dtStart: string;
  dtEnd: string;
}

export function emptyRouteFilter(): GatewayRouteFilter {
  return { enable: '', clusterId: 0, domain: '', q: '', priority: null, stripPrefix: false, dtStart: '', dtEnd: '' };
}

export function isRouteFilterActive(f: GatewayRouteFilter): boolean {
  const d = emptyRouteFilter();
  return (
    f.enable !== d.enable ||
    f.clusterId !== d.clusterId ||
    f.domain !== d.domain ||
    f.q !== d.q ||
    f.priority !== d.priority ||
    f.stripPrefix !== d.stripPrefix ||
    f.dtStart !== d.dtStart ||
    f.dtEnd !== d.dtEnd
  );
}

/** 筛选参数拼装：空值语义=不传（enable 全部/clusterId 0/优先级 null/stripPrefix 关/时间空） */
export function routeFilterParams(f: GatewayRouteFilter): Record<string, unknown> {
  const p: Record<string, unknown> = {};
  if (f.enable === 'true' || f.enable === 'false') p.enable = f.enable === 'true';
  if (f.clusterId > 0) p.clusterId = f.clusterId;
  if (f.domain.trim()) p.domain = f.domain.trim();
  if (f.q.trim()) p.Q = f.q.trim();
  if (f.priority !== null) p.priority = f.priority;
  if (f.stripPrefix) p.stripPrefix = true;
  if (f.dtStart.trim()) p.dtStart = f.dtStart.trim();
  if (f.dtEnd.trim()) p.dtEnd = f.dtEnd.trim();
  return p;
}

/** 单条 Detail：GET /Gateway/GatewayRoute/{id}；无数据/HTTP 错 → null（调用方显示失败态） */
export async function fetchRouteDetail(request: Req, id: number): Promise<Rec | null> {
  try {
    const res = await request({ url: `/Gateway/GatewayRoute/${id}`, method: 'GET' });
    const d = (res as Rec)?.data;
    return d && !Array.isArray(d) ? (d as Rec) : null;
  } catch {
    return null;
  }
}

/** 项目下拉数据源（延迟加载：抽屉首次打开才拉，避免给网关页签加第四请求——保 J-GW-04 C05「三请求」断言） */
export async function fetchProjects(request: Req): Promise<Rec[]> {
  try {
    const res = await request({ url: '/Platform/GalaxyProject', method: 'GET', params: { PageSize: 500 } });
    const d = (res as Rec)?.data;
    return Array.isArray(d) ? (d as Rec[]) : [];
  } catch {
    return [];
  }
}

/** 新增/编辑提交：编辑只提交变更字段（PUT /{id}）；失败抛错由调用方 toast */
export async function submitRoute(request: Req, m: GatewayRouteForm, id?: number): Promise<{ ok: boolean; message: string }> {
  try {
    if (id) {
      const detail = await fetchRouteDetail(request, id);
      if (!detail) return { ok: false, message: '路由不存在或已被删除' };
      const res = await request({ url: `/Gateway/GatewayRoute/${id}`, method: 'PUT', data: routeUpdatePayload(detail, m) } as ReqOptions & { data: Rec });
      return okOf(res as Rec, '路由已保存');
    }
    const res = await request({ url: '/Gateway/GatewayRoute', method: 'POST', data: routeInsertPayload(m) } as ReqOptions & { data: Rec });
    return okOf(res as Rec, '路由已创建');
  } catch (e) {
    return { ok: false, message: errText(e, '路由保存失败') };
  }
}

/** 删除：DELETE /Gateway/GatewayRoute/{id}（后端无级联，路由删除不影响集群/证书） */
export async function removeRoute(request: Req, id: number): Promise<{ ok: boolean; message: string }> {
  try {
    const res = await request({ url: `/Gateway/GatewayRoute/${id}`, method: 'DELETE' });
    return okOf(res as Rec, '路由已删除');
  } catch (e) {
    return { ok: false, message: errText(e, '路由删除失败') };
  }
}
