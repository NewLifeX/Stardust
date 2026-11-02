// MCP 令牌管理（/mesh AI·MCP tab）纯逻辑（无 Vue 依赖，request 由调用方注入，便于单测）。
//
// 数据源与接口（已读后端 McpTokenController / McpTokenResourceController / McpAuditController
// 与 Cube EntityController 基类确认，2026-10-30）：
//  - 令牌列表   GET  /Platform/McpToken?PageSize=        （IndexView JSON 序列化完整实体，含明文 Token；
//                  前端只在创建/重置后一次性读出完整值，其余一律 maskTokenFull 脱敏展示）
//  - 资源授权   GET  /Platform/McpTokenResource?PageSize=
//  - 审计日志   GET  /Platform/McpAudit?tokenId=&toolName=&success=&dtStart=&dtEnd=&PageSize=
//  - 项目下拉   GET  /Platform/GalaxyProject
//  - 用户下拉   GET  /Admin/User（后端无该控制器时降级空列表，诚实空态）
//  - 节点下拉   GET  /Nodes/Node
//  - 应用下拉   GET  /Registry/App
//
// 写接口（运行中 Cube（NewLife.Cube.dll）基类为 RESTful 特性路由，2026-10-30 实测确认）：
//  - 创建令牌   POST /Platform/McpToken            JSON body {name,enable,userId,projectId,scopes,remark,expireTime?}
//                  OnInsert 自动生成 sdmcp_ Token；ProjectId>0 且 Scopes 空时后端填默认只读 scope 串。
//                  响应 Data 即新实体（含 Token），优先直接读，缺 Token 再兜底重拉列表 max(Id)。
//  - 更新令牌   PUT  /Platform/McpToken/{id}        JSON body（OnUpdate 强制 Token 字段恢复原值，不可改）
//  - 删除令牌   DELETE /Platform/McpToken/{id}
//  - 启停令牌   PUT  /Platform/McpToken/{id}        与更新同路由，body 带 enable 新值
//  - 重置令牌   GET  /Platform/McpToken/Reset?id=    （自定义动作，约定路由；响应 code/message，新值需重拉列表）
//  - 新增授权   POST /Platform/McpTokenResource     JSON body {tokenId,resourceType,resourceId,isAll,enable}
//  - 删除授权   DELETE /Platform/McpTokenResource/{id}
//  - 启停授权   PUT  /Platform/McpTokenResource/{id} JSON body {id,enable}（EntityController.Update 部分更新）
//  - 服务器CRUD POST/PUT/DELETE /Platform/McpServer[/{id}]（McpServerController 全 REST；PUT 启停仅提交 {id,enable}）
//  - 审计历史   GET  /Platform/McpAudit              Search 支持 tokenId/toolName/actionName/success/dtStart/dtEnd/Q + 分页
//  - 运行时     GET/PUT /Platform/StarServer        ConfigController：GET 返回整设置对象；PUT 全量覆盖保存（必须回传完整对象）
//
// 数据原则：只接真实接口，不造演示数据。下拉接口失败一律 catch 成空列表，由组件显示诚实空态。
import { okRes } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
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
function listOf(res: { data?: unknown } | undefined): Rec[] {
  const d = res?.data;
  return Array.isArray(d) ? (d as Rec[]) : [];
}
/** projectId 透传：非空才放进 params（与 mesh.logic.ts 同一模式） */
function paramsOf(projectId?: number | null): Record<string, unknown> {
  return projectId != null ? { projectId } : {};
}
function errText(e: unknown, fallback: string): string {
  return e instanceof Error && e.message ? e.message : fallback;
}
/** 宽松解析时间，失败返回 0 */
function toMs(v: unknown): number {
  const s = str(v);
  if (!s) return 0;
  const t = Date.parse(s.replace(' ', 'T'));
  return Number.isFinite(t) ? t : 0;
}
function fmtTime(ms: number): string {
  if (!ms) return '—';
  const d = new Date(ms);
  const p = (n: number): string => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`;
}

// ==================== 行类型 ====================

/** 令牌表行（管理页用，含绑定用户/项目/scope 等新字段） */
export interface McpTokenFullRow {
  id: number;
  name: string;
  /** 掩码令牌：前 8 位 + … + 后 4 位，正常列表永远不显示明文 */
  masked: string;
  enable: boolean;
  /** 绑定用户 Membership.User.Id；0 = 未绑定 */
  userId: number;
  /** 绑定用户显示名（users 候选解析；缺失时 —） */
  userName: string;
  /** 绑定项目 GalaxyProject.Id；0 = 未绑定 */
  projectId: number;
  /** 项目显示名（projectMap 解析，缺失时 #projectId） */
  projectName: string;
  /** 原始 scope csv，如 apps:read,nodes:read */
  scopes: string;
  /** 过期时间毫秒戳；0 = 永不过期 */
  expireMs: number;
  expireText: string;
  callCount: number;
  lastText: string;
  lastIP: string;
  remark: string;
}

/** 资源授权行 */
export interface McpResourceRow {
  id: number;
  tokenId: number;
  /** Project / Node / App */
  resourceType: string;
  resourceTypeLabel: string;
  resourceId: number;
  /** 该资源在候选列表里解析出的名字；解析不到为空串，组件显示 #resourceId */
  resourceName: string;
  isAll: boolean;
  enable: boolean;
}

/** 审计行（完整字段，展开详情用） */
export interface McpAuditFullRow {
  id: string;
  tokenId: number;
  tokenName: string;
  tool: string;
  action: string;
  /** tool · action 合并显示 */
  toolLabel: string;
  ip: string;
  ua: string;
  /** 入参 JSON（后端已脱敏） */
  args: string;
  success: boolean;
  errorMessage: string;
  duration: number;
  traceId: string;
  time: string;
  /** 风险级别。0=L0只读 1=L1可逆写 2=L2高危（MCP审计日志.RiskLevel） */
  riskLevel: number;
  /** 项目编号快照（MCP审计日志.ProjectId） */
  projectId: number;
  /** 调用时生效的 scope 集合，逗号分隔（MCP审计日志.ScopesUsed） */
  scopesUsed: string;
  /** 写操作前快照（MCP审计日志.BeforeValue） */
  beforeValue: string;
  /** 写操作后快照（MCP审计日志.AfterValue） */
  afterValue: string;
  /** CreateTime 毫秒戳（分页查询排序用；近列表路径可能缺省 0） */
  timeMs: number;
}

/** 下拉候选项（项目/用户/节点/应用共用） */
export interface McpOption {
  id: number;
  name: string;
}

export interface McpManageData {
  tokens: McpTokenFullRow[];
  resources: McpResourceRow[];
  audits: McpAuditFullRow[];
  projects: McpOption[];
  users: McpOption[];
  nodes: McpOption[];
  apps: McpOption[];
  error: string;
}

// ==================== 纯推导 ====================

/** 令牌掩码：前 8 位 + … + 后 4 位；过短只显示前 4 位加星；空值显示 — */
export function maskTokenFull(token: unknown): string {
  const t = str(token);
  if (!t) return '—';
  if (t.length <= 12) return t.length > 4 ? `${t.slice(0, 4)}****` : '****';
  return `${t.slice(0, 8)}…${t.slice(-4)}`;
}

/** scope 域 → 中文名（与后端 OnInsert 默认 scope 串同域） */
export const SCOPE_DOMAINS: { key: string; label: string }[] = [
  { key: 'apps', label: '应用' },
  { key: 'nodes', label: '节点' },
  { key: 'config', label: '配置' },
  { key: 'deploy', label: '部署' },
  { key: 'monitor', label: '监控' },
  { key: 'now', label: '变更' },
  { key: 'platform', label: '平台' },
];

export const SCOPE_LEVELS: { key: string; label: string; hint: string }[] = [
  { key: 'read', label: '读', hint: '只读查询，无副作用' },
  { key: 'write', label: '写', hint: '可逆写操作（发布/回滚/触发流水线）' },
  { key: 'admin', label: '高危', hint: '不可逆或影响运行时（停应用/执行命令）' },
];

/** 与后端 McpToken.DefaultProjectReadScopes 一致：绑项目且 Scope 留空时默认写入 */
export const DEFAULT_PROJECT_READ_SCOPES =
  'apps:read,nodes:read,config:read,deploy:read,monitor:read,now:read,platform:read';

/** 列表/表单展示：绑定用户文案 */
export function formatBoundUser(userId: number, userName: string): string {
  if (userId <= 0) return '—';
  const n = userName.trim();
  return n ? `${n} (#${userId})` : `#${userId}`;
}

/** 创建表单：未勾 Scope 且已选项目时，提示将应用的默认域（中文 Tag 串） */
export function previewDefaultProjectScopes(projectId: number, scopesCsv: string): string {
  if (projectId <= 0 || parseScopes(scopesCsv).length > 0) return '';
  return parseScopes(DEFAULT_PROJECT_READ_SCOPES).map(scopeLabel).join('、');
}

/** "apps:read,config:write" → ["apps:read","config:write"]；去空白去空项 */
export function parseScopes(scopes: unknown): string[] {
  return str(scopes)
    .split(',')
    .map((s) => s.trim())
    .filter((s) => s.length > 0);
}

/** "apps:read" → "应用·读"；未知域原样返回 */
export function scopeLabel(scope: string): string {
  const [dom, level] = scope.split(':');
  const d = SCOPE_DOMAINS.find((x) => x.key === dom);
  const lv = SCOPE_LEVELS.find((x) => x.key === level);
  if (d && lv) return `${d.label}·${lv.label}`;
  return scope;
}

/** 资源类型 → 中文名 */
export function resourceLabel(type: unknown): string {
  const t = str(type);
  const map: Record<string, string> = { Project: '项目', Node: '节点', App: '应用' };
  return map[t] ?? (t || '—');
}

/** 候选列表 → id→name 映射 */
function buildOptionMap(options: McpOption[]): Map<number, string> {
  const m = new Map<number, string>();
  for (const o of options) {
    if (o.id > 0) m.set(o.id, o.name);
  }
  return m;
}

/** McpToken 原始行 → 令牌表行 */
export function toTokenRows(
  rows: Rec[],
  projectMap: Map<number, string>,
  userMap: Map<number, string> = new Map(),
): McpTokenFullRow[] {
  const out: McpTokenFullRow[] = [];
  for (const r of rows) {
    const id = num(pick(r, 'Id', 'id'));
    const projectId = num(pick(r, 'ProjectId', 'projectId'));
    const userId = num(pick(r, 'UserId', 'userId'));
    const expireMs = toMs(pick(r, 'ExpireTime', 'expireTime'));
    out.push({
      id,
      name: str(pick(r, 'Name', 'name')),
      masked: maskTokenFull(pick(r, 'Token', 'token')),
      enable: bool(pick(r, 'Enable', 'enable')),
      userId,
      userName: userId > 0 ? userMap.get(userId) ?? '' : '',
      projectId,
      projectName: projectId > 0 ? projectMap.get(projectId) ?? `#${projectId}` : '—',
      scopes: str(pick(r, 'Scopes', 'scopes')),
      expireMs,
      expireText: expireMs ? fmtTime(expireMs) : '永不过期',
      callCount: num(pick(r, 'CallCount', 'callCount')),
      lastText: fmtTime(toMs(pick(r, 'LastTime', 'lastTime'))),
      lastIP: str(pick(r, 'LastIP', 'lastIP')),
      remark: str(pick(r, 'Remark', 'remark')),
    });
  }
  return out;
}

/** McpTokenResource 原始行 → 授权行；按类型用对应候选表解析资源名 */
export function toResourceRows(rows: Rec[], nodes: McpOption[], apps: McpOption[], projects: McpOption[]): McpResourceRow[] {
  const nodeMap = buildOptionMap(nodes);
  const appMap = buildOptionMap(apps);
  const projectMap = buildOptionMap(projects);
  const out: McpResourceRow[] = [];
  for (const r of rows) {
    const type = str(pick(r, 'ResourceType', 'resourceType'));
    const rid = num(pick(r, 'ResourceId', 'resourceId'));
    const name = type === 'Node' ? nodeMap.get(rid) : type === 'App' ? appMap.get(rid) : type === 'Project' ? projectMap.get(rid) : '';
    out.push({
      id: num(pick(r, 'Id', 'id')),
      tokenId: num(pick(r, 'TokenId', 'tokenId')),
      resourceType: type,
      resourceTypeLabel: resourceLabel(type),
      resourceId: rid,
      resourceName: name ?? '',
      isAll: bool(pick(r, 'IsAll', 'isAll')),
      enable: bool(pick(r, 'Enable', 'enable')),
    });
  }
  return out;
}

/** McpAudit 原始行 → 审计行，按调用时间倒序 */
export function toAuditRows(rows: Rec[]): McpAuditFullRow[] {
  const out: McpAuditFullRow[] = [];
  for (const r of rows) {
    const tool = str(pick(r, 'ToolName', 'toolName'));
    const action = str(pick(r, 'ActionName', 'actionName'));
    const timeMs = toMs(pick(r, 'CreateTime', 'createTime'));
    out.push({
      id: String(pick(r, 'Id', 'id') ?? out.length),
      tokenId: num(pick(r, 'TokenId', 'tokenId')),
      tokenName: str(pick(r, 'TokenName', 'tokenName')),
      tool,
      action,
      toolLabel: action ? `${tool} · ${action}` : tool,
      ip: str(pick(r, 'CallerIp', 'callerIp')),
      ua: str(pick(r, 'CallerUserAgent', 'callerUserAgent')),
      args: str(pick(r, 'Arguments', 'arguments')),
      success: bool(pick(r, 'Success', 'success')),
      errorMessage: str(pick(r, 'ErrorMessage', 'errorMessage')),
      duration: num(pick(r, 'Duration', 'duration')),
      traceId: str(pick(r, 'TraceId', 'traceId')),
      time: fmtTime(timeMs),
      riskLevel: num(pick(r, 'RiskLevel', 'riskLevel')),
      projectId: num(pick(r, 'ProjectId', 'projectId')),
      scopesUsed: str(pick(r, 'ScopesUsed', 'scopesUsed')),
      beforeValue: str(pick(r, 'BeforeValue', 'beforeValue')),
      afterValue: str(pick(r, 'AfterValue', 'afterValue')),
      timeMs,
    });
  }
  out.sort((a, b) => (a.time < b.time ? 1 : -1));
  return out;
}

/** 审计过滤：按令牌 / IP 包含 / 成败。tokenId<=0 不过滤 */
export interface AuditFilter {
  tokenId: number;
  ipQ: string;
  level: 'all' | 'ok' | 'failed';
}

export function filterAudits(rows: McpAuditFullRow[], f: AuditFilter): McpAuditFullRow[] {
  const ipQ = f.ipQ.trim().toLowerCase();
  return rows.filter((r) => {
    if (f.tokenId > 0 && r.tokenId !== f.tokenId) return false;
    if (f.level === 'failed' && r.success) return false;
    if (f.level === 'ok' && !r.success) return false;
    if (ipQ && !r.ip.toLowerCase().includes(ipQ)) return false;
    return true;
  });
}

/** 资源行按令牌过滤（选中令牌后展示其授权） */
export function filterResourcesByToken(rows: McpResourceRow[], tokenId: number): McpResourceRow[] {
  if (tokenId <= 0) return [];
  return rows.filter((r) => r.tokenId === tokenId);
}

function toOptions(rows: Rec[], nameKeys: string[]): McpOption[] {
  const out: McpOption[] = [];
  for (const r of rows) {
    const id = num(pick(r, 'Id', 'id'));
    if (id <= 0) continue;
    // 第一个非空 key 即名字（DisplayName 优先，Name 兜底）
    let name = '';
    for (const k of nameKeys) {
      const v = str(pick(r, k));
      if (v) { name = v; break; }
    }
    if (!name) name = `#${id}`;
    out.push({ id, name });
  }
  return out;
}

// ==================== 取数 ====================

/** 拉 MCP 管理页全部数据：令牌/授权/审计 + 四个下拉候选。任一软依赖失败降级空列表 */
export async function fetchMcpManage(request: Req, projectId?: number | null): Promise<McpManageData> {
  try {
    const pp = paramsOf(projectId);
    const [tokRes, resRes, audRes, projRes, userRes, nodeRes, appRes] = await Promise.all([
      request({ url: '/Platform/McpToken', method: 'GET', params: { PageSize: 500, ...pp } }),
      request({ url: '/Platform/McpTokenResource', method: 'GET', params: { PageSize: 500, ...pp } }),
      request({ url: '/Platform/McpAudit', method: 'GET', params: { PageSize: 200, ...pp } }),
      request({ url: '/Platform/GalaxyProject', method: 'GET', params: { PageSize: 500 } }).catch(() => ({ data: [] })),
      // 用户列表：旧探测 404，新管理中心已核实可用；失败降级空列表
      request({ url: '/Admin/User', method: 'GET', params: { PageSize: 500 } }).catch(() => ({ data: [] })),
      request({ url: '/Nodes/Node', method: 'GET', params: { PageSize: 500 } }).catch(() => ({ data: [] })),
      request({ url: '/Registry/App', method: 'GET', params: { PageSize: 500 } }).catch(() => ({ data: [] })),
    ]);
    const projects = toOptions(listOf(projRes), ['DisplayName', 'displayName', 'Name', 'name']);
    const users = toOptions(listOf(userRes), ['Name', 'name', 'DisplayName', 'displayName']);
    const projectMap = buildOptionMap(projects);
    const userMap = buildOptionMap(users);
    return {
      tokens: toTokenRows(listOf(tokRes), projectMap, userMap),
      resources: toResourceRows(listOf(resRes), toOptions(listOf(nodeRes), ['Name', 'name']), toOptions(listOf(appRes), ['DisplayName', 'displayName', 'Name', 'name']), projects),
      audits: toAuditRows(listOf(audRes)),
      projects,
      users,
      nodes: toOptions(listOf(nodeRes), ['Name', 'name']),
      apps: toOptions(listOf(appRes), ['DisplayName', 'displayName', 'Name', 'name']),
      error: '',
    };
  } catch (e) {
    return { tokens: [], resources: [], audits: [], projects: [], users: [], nodes: [], apps: [], error: errText(e, 'MCP 数据加载失败') };
  }
}

// ==================== 写操作 ====================

/** 表单模型（McpTokenForm.vue 提交） */
export interface McpTokenFormModel {
  name: string;
  enable: boolean;
  userId: number;
  projectId: number;
  /** scope csv；空串 = 零授权（后端只允许 whoami） */
  scopes: string;
  /** datetime-local 值 "YYYY-MM-DDTHH:mm"；空串 = 永不过期（不发该字段） */
  expireIso: string;
  remark: string;
}

export interface OpResult {
  ok: boolean;
  message: string;
  /** 创建/重置成功后的完整 Token 值；之后不再可获得 */
  tokenValue?: string;
  /** 创建成功后的令牌 Id（便于打开资源授权 Drawer） */
  tokenId?: number;
}

function bodyOf(form: McpTokenFormModel, id?: number): Rec {
  const p: Rec = {
    name: form.name.trim(),
    enable: form.enable,
    userId: form.userId || 0,
    projectId: form.projectId || 0,
    scopes: form.scopes,
    remark: form.remark,
  };
  if (id) p.id = id;
  if (form.expireIso) p.expireTime = form.expireIso;
  return p;
}

/** 从令牌列表里按编号读明文 Token（列表 JSON 含完整实体）；找不到返回空串 */
async function fetchTokenValue(request: Req, tokenId: number): Promise<string> {
  try {
    const res = await request({ url: '/Platform/McpToken', method: 'GET', params: { PageSize: 500 } });
    for (const row of listOf(res)) {
      if (num(pick(row, 'Id', 'id')) === tokenId) return str(pick(row, 'Token', 'token'));
    }
  } catch {
    /* 读完失败就不给完整值，列表刷新后只显示掩码 */
  }
  return '';
}

/** 创建令牌。优先直接读 POST 响应 Data 里的完整实体（ProcessInsert 返回带 Token 的实体）；
 *  响应缺 Token 时兜底重拉列表取 max(Id) 行（旧宿主行为），一次性读出完整 sdmcp_ 值 */
export async function createToken(request: Req, form: McpTokenFormModel): Promise<OpResult> {
  if (!form.name.trim()) return { ok: false, message: '请填写令牌名称' };
  try {
    const res = (await request({ url: '/Platform/McpToken', method: 'POST', data: bodyOf(form) } as ReqOptions & { data: Rec }));
    const r = okRes(res);
    if (!r.ok) return { ok: false, message: r.message || '令牌创建失败' };
    // 首选：创建响应 Data 即新实体（含 Token）；无 Token 再兜底重拉列表找 max(Id)
    const created = (res as { data?: Rec } | undefined)?.data ?? {};
    let tokenId = num(pick(created, 'Id', 'id'));
    let tokenValue = str(pick(created, 'Token', 'token'));
    if (!tokenValue) {
      let maxId = 0;
      const listRes = await request({ url: '/Platform/McpToken', method: 'GET', params: { PageSize: 500 } });
      for (const row of listOf(listRes)) {
        const id = num(pick(row, 'Id', 'id'));
        if (id > maxId) maxId = id;
      }
      tokenId = maxId;
      tokenValue = maxId > 0 ? await fetchTokenValue(request, maxId) : '';
    }
    return { ok: true, message: '令牌已创建', tokenValue, tokenId: tokenId > 0 ? tokenId : undefined };
  } catch (e) {
    return { ok: false, message: errText(e, '令牌创建失败') };
  }
}

/** 更新令牌（名称/启用/绑定/scope/过期/备注；Token 字段后端强制不可改） */
export async function updateToken(request: Req, id: number, form: McpTokenFormModel): Promise<OpResult> {
  if (!id || id <= 0) return { ok: false, message: '缺少令牌编号' };
  if (!form.name.trim()) return { ok: false, message: '请填写令牌名称' };
  try {
    const res = (await request({ url: `/Platform/McpToken/${id}`, method: 'PUT', data: bodyOf(form, id) } as ReqOptions & { data: Rec }));
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? '令牌已更新' : r.message || '令牌更新失败' };
  } catch (e) {
    return { ok: false, message: errText(e, '令牌更新失败') };
  }
}

/** 删除令牌（级联 McpTokenResource；McpAudit 保留） */
export async function deleteToken(request: Req, id: number): Promise<OpResult> {
  if (!id || id <= 0) return { ok: false, message: '缺少令牌编号' };
  try {
    const res = await request({ url: `/Platform/McpToken/${id}`, method: 'DELETE' });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? '令牌已删除' : r.message || '令牌删除失败' };
  } catch (e) {
    return { ok: false, message: errText(e, '令牌删除失败') };
  }
}

/** 启停令牌（PUT 同更新路由，body 带 enable 新值，其余字段回带现值） */
export async function setTokenEnable(request: Req, id: number, enable: boolean, current: Partial<McpTokenFormModel>): Promise<OpResult> {
  if (!id || id <= 0) return { ok: false, message: '缺少令牌编号' };
  try {
    const res = (await request({
      url: `/Platform/McpToken/${id}`,
      method: 'PUT',
      data: bodyOf({ ...(current as McpTokenFormModel), enable }),
    } as ReqOptions & { data: Rec }));
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? (enable ? '令牌已启用' : '令牌已停用') : r.message || '状态更新失败' };
  } catch (e) {
    return { ok: false, message: errText(e, '状态更新失败') };
  }
}

/** 重置令牌：旧值立即失效，新值一次性展示 */
export async function resetToken(request: Req, id: number): Promise<OpResult> {
  if (!id || id <= 0) return { ok: false, message: '缺少令牌编号' };
  try {
    const res = await request({ url: '/Platform/McpToken/Reset', method: 'GET', params: { id } });
    const r = okRes(res);
    if (!r.ok) return { ok: false, message: r.message || '令牌重置失败' };
    const payload = ((res as { data?: Rec } | undefined)?.data ?? r.data) as Rec | undefined;
    let tokenValue = payload ? str(pick(payload, 'Token', 'token')) : '';
    if (!tokenValue) tokenValue = await fetchTokenValue(request, id);
    return { ok: true, message: '令牌已重置，旧值立即失效', tokenValue };
  } catch (e) {
    return { ok: false, message: errText(e, '令牌重置失败') };
  }
}

/** 新增资源授权（Project/Node/App，单条） */
export async function addResource(request: Req, tokenId: number, resourceType: string, resourceId: number): Promise<OpResult> {
  if (!tokenId || tokenId <= 0) return { ok: false, message: '缺少令牌编号' };
  if (!resourceType) return { ok: false, message: '请选择资源类型' };
  if (!resourceId || resourceId <= 0) return { ok: false, message: '请选择资源' };
  try {
    const res = (await request({
      url: '/Platform/McpTokenResource',
      method: 'POST',
      data: { tokenId, resourceType, resourceId, isAll: false, enable: true },
    } as ReqOptions & { data: Rec }));
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? '授权已添加' : r.message || '授权添加失败' };
  } catch (e) {
    return { ok: false, message: errText(e, '授权添加失败') };
  }
}

/** 删除资源授权 */
export async function deleteResource(request: Req, id: number): Promise<OpResult> {
  if (!id || id <= 0) return { ok: false, message: '缺少授权编号' };
  try {
    const res = await request({ url: `/Platform/McpTokenResource/${id}`, method: 'DELETE' });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? '授权已删除' : r.message || '授权删除失败' };
  } catch (e) {
    return { ok: false, message: errText(e, '授权删除失败') };
  }
}

/** 启停资源授权（PUT /{id} 部分更新，仅提交 enable；后端 EntityController.Update 只覆盖提交的字段） */
export async function setResourceEnable(request: Req, id: number, enable: boolean): Promise<OpResult> {
  if (!id || id <= 0) return { ok: false, message: '缺少授权编号' };
  try {
    const res = (await request({
      url: `/Platform/McpTokenResource/${id}`,
      method: 'PUT',
      data: { id, enable },
    } as ReqOptions & { data: Rec }));
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? (enable ? '授权已启用' : '授权已停用') : r.message || '状态更新失败' };
  } catch (e) {
    return { ok: false, message: errText(e, '状态更新失败') };
  }
}

// ==================== MCP 服务器管理（/Platform/McpServer 全 CRUD，后端 WebEntityController） ====================

/** MCP 服务器表单模型（McpServerForm.vue 提交） */
export interface McpServerFormModel {
  name: string;
  /** 远端 MCP 端点 URL，如 http://node:6680/api/mcp */
  url: string;
  enable: boolean;
  /** 归属项目 GalaxyProject.Id；0 = 未归属 */
  projectId: number;
  remark: string;
}

function bodyOfServer(form: McpServerFormModel, id?: number): Rec {
  const p: Rec = {
    name: form.name.trim(),
    url: form.url.trim(),
    enable: form.enable,
    projectId: form.projectId || 0,
    remark: form.remark,
  };
  if (id) p.id = id;
  return p;
}

/** 新增 MCP 服务器（POST 集合 URL） */
export async function createMcpServer(request: Req, form: McpServerFormModel): Promise<OpResult> {
  if (!form.name.trim()) return { ok: false, message: '请填写服务器名称' };
  if (!form.url.trim()) return { ok: false, message: '请填写远端地址' };
  try {
    const res = (await request({ url: '/Platform/McpServer', method: 'POST', data: bodyOfServer(form) } as ReqOptions & { data: Rec }));
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? '服务器已登记' : r.message || '服务器登记失败' };
  } catch (e) {
    return { ok: false, message: errText(e, '服务器登记失败') };
  }
}

/** 更新 MCP 服务器（PUT /{id}，全字段提交） */
export async function updateMcpServer(request: Req, id: number, form: McpServerFormModel): Promise<OpResult> {
  if (!id || id <= 0) return { ok: false, message: '缺少服务器编号' };
  if (!form.name.trim()) return { ok: false, message: '请填写服务器名称' };
  if (!form.url.trim()) return { ok: false, message: '请填写远端地址' };
  try {
    const res = (await request({ url: `/Platform/McpServer/${id}`, method: 'PUT', data: bodyOfServer(form, id) } as ReqOptions & { data: Rec }));
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? '服务器已更新' : r.message || '服务器更新失败' };
  } catch (e) {
    return { ok: false, message: errText(e, '服务器更新失败') };
  }
}

/** 删除 MCP 服务器 */
export async function deleteMcpServer(request: Req, id: number): Promise<OpResult> {
  if (!id || id <= 0) return { ok: false, message: '缺少服务器编号' };
  try {
    const res = await request({ url: `/Platform/McpServer/${id}`, method: 'DELETE' });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? '服务器已删除' : r.message || '服务器删除失败' };
  } catch (e) {
    return { ok: false, message: errText(e, '服务器删除失败') };
  }
}

/** 启停 MCP 服务器（PUT /{id} 部分更新，仅提交 enable） */
export async function setServerEnable(request: Req, id: number, enable: boolean): Promise<OpResult> {
  if (!id || id <= 0) return { ok: false, message: '缺少服务器编号' };
  try {
    const res = (await request({
      url: `/Platform/McpServer/${id}`,
      method: 'PUT',
      data: { id, enable },
    } as ReqOptions & { data: Rec }));
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? (enable ? '服务器已启用' : '服务器已停用') : r.message || '状态更新失败' };
  } catch (e) {
    return { ok: false, message: errText(e, '状态更新失败') };
  }
}

// ==================== 审计服务端查询（McpAuditController.Search 全参数 + 分页） ====================

/** 审计历史查询条件（服务端过滤：tokenId/success/toolName/dtStart/dtEnd + 分页） */
export interface McpAuditQuery {
  /** 令牌过滤；0 = 全部 */
  tokenId: number;
  /** 工具名（精确匹配 ToolName） */
  toolName: string;
  /** 执行结果；undefined = 全部 */
  success?: boolean;
  /** 起始日期 "YYYY-MM-DD"；空 = 不限 */
  dtStart: string;
  /** 结束日期 "YYYY-MM-DD"；空 = 不限（自动补到当日 23:59:59） */
  dtEnd: string;
  /** 页码，从 0 起（后端 pageIndex） */
  pageIndex: number;
  pageSize: number;
}

/** 查询条件 → 请求 params。空值不下发；dtEnd 补 23:59:59 保证含当日（XCode Between 含端点） */
export function buildAuditQueryParams(q: McpAuditQuery): Record<string, unknown> {
  const p: Record<string, unknown> = {
    pageIndex: Math.max(0, q.pageIndex),
    pageSize: q.pageSize > 0 ? q.pageSize : 50,
  };
  if (q.tokenId > 0) p.tokenId = q.tokenId;
  if (q.toolName.trim()) p.toolName = q.toolName.trim();
  if (q.success === true || q.success === false) p.success = q.success;
  if (q.dtStart.trim()) p.dtStart = q.dtStart.trim();
  const end = q.dtEnd.trim();
  if (end) p.dtEnd = end.includes(' ') ? end : `${end} 23:59:59`;
  return p;
}

/** 审计历史查询结果 */
export interface McpAuditPageResult {
  rows: McpAuditFullRow[];
  total: number;
  error: string;
}

/** 审计历史服务端查询：GET /Platform/McpAudit（Search 支持 tokenId/toolName/actionName/success/dtStart/dtEnd/Q）。
 *  服务端默认排序不可靠，回读后按 CreateTime 毫秒戳倒序（同毫秒按 Id 倒序兜底）。 */
export async function fetchMcpAuditPage(request: Req, q: McpAuditQuery): Promise<McpAuditPageResult> {
  try {
    const res = await request({ url: '/Platform/McpAudit', method: 'GET', params: buildAuditQueryParams(q) });
    const rows = toAuditRows(listOf(res));
    rows.sort((a, b) => (b.timeMs - a.timeMs) || (Number(b.id) - Number(a.id)));
    const page = (res as { page?: { totalCount?: unknown; total?: unknown } } | undefined)?.page;
    const total = num(page?.totalCount ?? page?.total) || rows.length;
    return { rows, total, error: '' };
  } catch (e) {
    return { rows: [], total: 0, error: errText(e, '审计查询失败') };
  }
}

// ==================== MCP 运行时（StarServerSetting.EnableMcp 等，连接指引卡用） ====================

/** MCP 运行时信息（GET /Platform/StarServer 的子集 + 原始对象） */
export interface McpRuntimeInfo {
  /** 原始设置对象（开修改时整对象回传 PUT，ConfigController.Update 全量覆盖保存） */
  raw: Rec | null;
  /** 启用 MCP 服务（StarServerSetting.EnableMcp）；raw 为空时 undefined = 未知 */
  enableMcp?: boolean;
  /** 域白名单（StarServerSetting.McpActionSet，* = 全部域） */
  actionSet: string;
  /** 敏感 key 关键词 csv（StarServerSetting.SensitiveKeywords） */
  sensitiveKeywords: string;
  error: string;
}

/** 拉取 MCP 运行时设置。无数据（data 为空/非对象，含 CT mock）降级 raw=null → UI 显示「未知」；仅请求异常记 error */
export async function fetchMcpRuntime(request: Req): Promise<McpRuntimeInfo> {
  try {
    const res = await request({ url: '/Platform/StarServer', method: 'GET' });
    const raw = (res as { data?: unknown } | undefined)?.data;
    if (!raw || Array.isArray(raw)) return { raw: null, actionSet: '', sensitiveKeywords: '', error: '' };
    const o = raw as Rec;
    return {
      raw: o,
      enableMcp: bool(pick(o, 'EnableMcp', 'enableMcp')),
      actionSet: str(pick(o, 'McpActionSet', 'mcpActionSet')),
      sensitiveKeywords: str(pick(o, 'SensitiveKeywords', 'sensitiveKeywords')),
      error: '',
    };
  } catch (e) {
    return { raw: null, actionSet: '', sensitiveKeywords: '', error: errText(e, '运行时设置加载失败') };
  }
}

/**
 * 切换 EnableMcp 总开关。ConfigController.Update 是**全量覆盖**（cfg.Copy(value) 后 Save），
 * 因此必须把 GET 到的完整设置对象回传，仅翻转 enableMcp 字段，避免把未提交字段重置为默认值。
 */
export async function setMcpEnabled(request: Req, raw: Rec | null, enable: boolean): Promise<OpResult> {
  if (!raw) return { ok: false, message: '取不到当前设置，不能修改' };
  try {
    const body: Rec = { ...raw };
    // 同时写 PascalCase/camelCase 两个 key，与后端反序列化绑定兼容
    body.EnableMcp = enable;
    body.enableMcp = enable;
    const res = (await request({ url: '/Platform/StarServer', method: 'PUT', data: body } as ReqOptions & { data: Rec }));
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? (enable ? 'MCP 服务已开启' : 'MCP 服务已关闭') : r.message || '开关更新失败' };
  } catch (e) {
    return { ok: false, message: errText(e, '开关更新失败') };
  }
}
