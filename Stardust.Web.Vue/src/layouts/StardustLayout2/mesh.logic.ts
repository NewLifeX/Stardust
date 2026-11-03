// 治理页（/mesh）纯逻辑：四个 tab 的取数与字段归一（无 Vue 依赖，request 由调用方注入，便于单测）。
//
// 数据源（已读后端实体/控制器确认，2026-10-30）：
//  注册中心  GET /Registry/Service        Service 实体，自带 Providers/Consumers 聚合数与 Enable/HealthCheck
//  网关     GET /Gateway/GatewayRoute     路由；GET /Gateway/GatewayCluster 集群名映射；
//           GET /Gateway/GatewayCert     控制器实际返回 SslCertificate 实体（Deployment/SSL证书.cs）
//  AI/MCP   GET /Platform/McpToken        令牌；GET /Platform/McpTokenResource 授权范围；
//           GET /Platform/McpAudit        审计日志
//  平台设置 GET /Platform/GalaxyProject   项目（Nodes/Apps/WhiteIPs/BlackIPs）；
//           GET /Platform/ProjectUser     项目成员关系；
//           GET /Platform/StarServer      ConfigController<StarServerSetting>，返回单个设置对象（非列表）
//
// 数据原则：只接真实接口，不造演示数据。某字段无对应来源（负载均衡列、TLS 路由级、SSO/DDNS/审计磁贴）
// 一律归一为「待接入」，不在前端编造开关状态或百分比。projectId 非空时随请求 params 透传
// （全局拦截器也会注入，这里显式传以便单测断言）。每个 tab 的取数独立 try/catch，失败只回 error，
// 由该 tab 自己显示错误+重试，不拖垮整页。
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import { okRes } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

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
/** projectId 透传：非空才放进 params */
function paramsOf(projectId?: number | null): Record<string, unknown> {
  return projectId != null ? { projectId } : {};
}
/** 宽松解析时间（兼容 'YYYY-MM-DD HH:mm:ss' 与 ISO），失败返回 0 */
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
function errText(e: unknown, fallback: string): string {
  return e instanceof Error && e.message ? e.message : fallback;
}

// ==================== Tab 1 · 注册中心 ====================

export interface RegistryRow {
  id: number;
  /** 展示名（DisplayName 优先，兜底 Name） */
  name: string;
  /** 服务名（Name，mono 副行） */
  serviceName: string;
  category: string;
  /** 实例数 = Service.Providers */
  instances: number;
  /** 消费者数 = Service.Consumers */
  consumers: number;
  /** 服务启用状态（健康条按此二态渲染，不编造百分比） */
  enable: boolean;
  /** 是否开启健康监测 */
  healthCheck: boolean;
}

/** Service 行 → 注册中心表行。无真实百分比字段，健康只反映 Enable 在线/停用 */
export function toRegistryRows(rows: Rec[]): RegistryRow[] {
  const out: RegistryRow[] = [];
  for (const r of rows) {
    const name = str(pick(r, 'DisplayName', 'displayName')) || str(pick(r, 'Name', 'name'));
    out.push({
      id: num(pick(r, 'Id', 'id')),
      name,
      serviceName: str(pick(r, 'Name', 'name')),
      category: str(pick(r, 'Category', 'category')),
      instances: num(pick(r, 'Providers', 'providers')),
      consumers: num(pick(r, 'Consumers', 'consumers')),
      enable: bool(pick(r, 'Enable', 'enable')),
      healthCheck: bool(pick(r, 'HealthCheck', 'healthCheck')),
    });
  }
  return out;
}

export interface RegistryResult {
  rows: RegistryRow[];
  error: string;
}

export async function fetchRegistry(request: Req, projectId?: number | null): Promise<RegistryResult> {
  try {
    const res = await request({ url: '/Registry/Service', method: 'GET', params: { PageSize: 500, ...paramsOf(projectId) } });
    return { rows: toRegistryRows(listOf(res)), error: '' };
  } catch (e) {
    return { rows: [], error: errText(e, '注册中心加载失败') };
  }
}

// ==================== F13 服务详情 Drawer（提供者/消费者/Service 编辑） ====================

/** 服务提供者/消费者行（AppService 与 AppConsume 共有字段；Healthy/Version 仅提供者有） */
export interface ServiceMemberRow {
  id: number;
  /** 应用名（AppService.Client） */
  client: string;
  address: string;
  enable: boolean;
  /** 仅提供者：健康状态 */
  healthy?: boolean;
  /** 仅提供者：客户端版本 */
  version?: string;
  updateTimeText: string;
}

export function toServiceMembers(rows: Rec[], kind: 'provider' | 'consumer'): ServiceMemberRow[] {
  return rows.map((r) => ({
    id: num(pick(r, 'Id', 'id')),
    client: str(pick(r, 'Client', 'client')) || `#${num(pick(r, 'AppId', 'appId'))}`,
    address: str(pick(r, 'Address', 'address')),
    enable: bool(pick(r, 'Enable', 'enable')),
    ...(kind === 'provider'
      ? { healthy: bool(pick(r, 'Healthy', 'healthy')), version: str(pick(r, 'Version', 'version')) }
      : {}),
    updateTimeText: fmtTime(toMs(pick(r, 'UpdateTime', 'updateTime'))),
  }));
}

/** Service 编辑表单（Name 主键语义不改；Secret 列表隐藏、编辑不回显） */
export interface ServiceEditModel {
  displayName: string;
  category: string;
  enable: boolean;
  extranet: boolean;
  singleton: boolean;
  /** 实体上是 Boolean（服务信息.cs UseScope），不是字符串 */
  useScope: boolean;
  address: string;
  healthCheck: boolean;
  remark: string;
}

/** Service 详情行 → 编辑表单 */
export function toServiceEditModel(r: Rec): ServiceEditModel {
  return {
    displayName: str(pick(r, 'DisplayName', 'displayName')),
    category: str(pick(r, 'Category', 'category')),
    enable: bool(pick(r, 'Enable', 'enable')),
    extranet: bool(pick(r, 'Extranet', 'extranet')),
    singleton: bool(pick(r, 'Singleton', 'singleton')),
    useScope: bool(pick(r, 'UseScope', 'useScope')),
    address: str(pick(r, 'Address', 'address')),
    healthCheck: bool(pick(r, 'HealthCheck', 'healthCheck')),
    remark: str(pick(r, 'Remark', 'remark')),
  };
}

export function servicePutBody(m: ServiceEditModel): Record<string, unknown> {
  return {
    DisplayName: m.displayName.trim(),
    Category: m.category.trim(),
    Enable: m.enable,
    Extranet: m.extranet,
    Singleton: m.singleton,
    // UseScope 实体是 Boolean，必须发布尔（字符串 "false" 会让后端 Boolean 反序列化失败）
    UseScope: m.useScope,
    Address: m.address.trim(),
    HealthCheck: m.healthCheck,
    Remark: m.remark,
  };
}

export function validateServiceEdit(m: ServiceEditModel): string {
  if (!m.displayName.trim()) return '服务显示名必填';
  return '';
}

export interface ServiceDetail {
  /** Service.Id（PUT /{id} 用） */
  id: number;
  name: string;
  service: ServiceEditModel | null;
  providers: ServiceMemberRow[];
  consumers: ServiceMemberRow[];
}

/** 拉服务详情：GET /Registry/Service/{id} + AppService/AppConsume 按 serviceId 过滤。整体失败返回 null */
export async function fetchServiceDetail(request: Req, serviceId: number): Promise<ServiceDetail | null> {
  if (!serviceId || serviceId <= 0) return null;
  try {
    const [svcRes, provRes, consRes] = await Promise.all([
      request({ url: `/Registry/Service/${serviceId}`, method: 'GET' }),
      request({ url: '/Registry/AppService', method: 'GET', params: { serviceId, PageSize: 200 } }),
      request({ url: '/Registry/AppConsume', method: 'GET', params: { serviceId, PageSize: 200 } }),
    ]);
    const svcData = (svcRes as { data?: Rec }).data;
    return {
      id: serviceId,
      name: str(pick(svcData ?? {}, 'Name', 'name')),
      service: svcData ? toServiceEditModel(svcData) : null,
      providers: toServiceMembers(listOf(provRes), 'provider'),
      consumers: toServiceMembers(listOf(consRes), 'consumer'),
    };
  } catch {
    return null;
  }
}

/** 保存服务：PUT /Registry/Service/{id}（部分更新） */
export async function saveService(request: Req, id: number, m: ServiceEditModel): Promise<{ ok: boolean; message: string }> {
  if (!id || id <= 0) return { ok: false, message: '缺少服务编号' };
  try {
    const res = await request({ url: `/Registry/Service/${id}`, method: 'PUT', data: servicePutBody(m) } as Parameters<Req>[0] & { data: Record<string, unknown> });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? '服务已保存' : r.message || '服务保存失败' };
  } catch (e) {
    return { ok: false, message: errText(e, '服务保存失败') };
  }
}

// ==================== Tab 2 · 网关 ====================

export interface GatewayRouteRow {
  id: number;
  /** 路由名 = Name */
  name: string;
  domain: string;
  path: string;
  /** 匹配：Methods（空=全部）+ Path */
  methods: string;
  clusterId: number;
  /** 目标集群名（ClusterId 查集群表）；查不到为空串，组件显示「待接入」 */
  clusterName: string;
  enable: boolean;
  /** 静态路由（SPA/静态站点），不走反向代理 */
  isStatic: boolean;
  /** TLS：是否存在该域名证书；证书域与自动续期 */
  tls: { present: boolean; domain: string; autoRenew: boolean };
}

/** 集群映射：GatewayCluster.Id → Name */
export function buildClusterMap(rows: Rec[]): Map<number, string> {
  const m = new Map<number, string>();
  for (const r of rows) {
    const id = num(pick(r, 'Id', 'id'));
    if (id > 0) m.set(id, str(pick(r, 'Name', 'name')));
  }
  return m;
}

/** 域名是否命中证书域（支持 *.example.com 通配） */
export function certDomainMatches(routeDomain: string, certDomain: string): boolean {
  const rd = routeDomain.toLowerCase();
  const cd = certDomain.toLowerCase();
  if (!rd || !cd) return false;
  if (rd === cd) return true;
  if (cd.startsWith('*.')) {
    const suffix = cd.slice(1); // '.example.com'
    return rd === suffix.slice(1) || rd.endsWith(suffix);
  }
  return false;
}

/** 为路由域名挑选一张启用证书（精确优先，再通配）。无则 present=false */
export function matchCert(routeDomain: string, certs: Rec[]): { present: boolean; domain: string; autoRenew: boolean } {
  // 路由 Domain 可能逗号分隔多个，取第一个非空
  const first = routeDomain.split(',').map((s) => s.trim()).find((s) => s) ?? '';
  let wildcard: Rec | undefined;
  for (const c of certs) {
    if (!bool(pick(c, 'Enable', 'enable'))) continue;
    const cd = str(pick(c, 'Domain', 'domain'));
    if (certDomainMatches(first, cd)) {
      if (!cd.startsWith('*.')) return { present: true, domain: cd, autoRenew: bool(pick(c, 'AutoRenew', 'autoRenew')) };
      wildcard = c;
    }
  }
  if (wildcard) {
    return { present: true, domain: str(pick(wildcard, 'Domain', 'domain')), autoRenew: bool(pick(wildcard, 'AutoRenew', 'autoRenew')) };
  }
  return { present: false, domain: '', autoRenew: false };
}

/** GatewayRoute 行 → 网关路由表行；clusterMap 用于解析目标集群名 */
export function toGatewayRoutes(rows: Rec[], clusterMap: Map<number, string>, certs: Rec[]): GatewayRouteRow[] {
  const out: GatewayRouteRow[] = [];
  for (const r of rows) {
    const clusterId = num(pick(r, 'ClusterId', 'clusterId'));
    const domain = str(pick(r, 'Domain', 'domain'));
    out.push({
      id: num(pick(r, 'Id', 'id')),
      name: str(pick(r, 'Name', 'name')),
      domain,
      path: str(pick(r, 'Path', 'path')),
      methods: str(pick(r, 'Methods', 'methods')),
      clusterId,
      clusterName: clusterMap.get(clusterId) ?? '',
      enable: bool(pick(r, 'Enable', 'enable')),
      isStatic: bool(pick(r, 'IsStaticRoute', 'isStaticRoute')),
      tls: matchCert(domain, certs),
    });
  }
  return out;
}

export interface GatewayResult {
  rows: GatewayRouteRow[];
  error: string;
}

export async function fetchGateway(request: Req, projectId?: number | null): Promise<GatewayResult> {
  try {
    const pp = paramsOf(projectId);
    const [routeRes, clusterRes, certRes] = await Promise.all([
      request({ url: '/Gateway/GatewayRoute', method: 'GET', params: { PageSize: 500, ...pp } }),
      request({ url: '/Gateway/GatewayCluster', method: 'GET', params: { PageSize: 500, ...pp } }),
      request({ url: '/Gateway/GatewayCert', method: 'GET', params: { PageSize: 500 } }),
    ]);
    const clusterMap = buildClusterMap(listOf(clusterRes));
    const rows = toGatewayRoutes(listOf(routeRes), clusterMap, listOf(certRes));
    return { rows, error: '' };
  } catch (e) {
    return { rows: [], error: errText(e, '网关路由加载失败') };
  }
}

// ==================== Tab 3 · AI / MCP ====================

export interface McpTokenRow {
  id: number;
  name: string;
  /** 掩码令牌，如 sdmcp_****，不明文展示 */
  masked: string;
  enable: boolean;
  /** 过期时间文案；永不过期显示「永不过期」 */
  expireText: string;
  callCount: number;
  lastText: string;
  /** 授权范围（按资源类型聚合）；无授权记录显示「—」 */
  scopeText: string;
}

export interface McpAuditRow {
  id: string;
  /** 工具名（invoke_action 时附加动作名） */
  tool: string;
  ok: boolean;
  /** 结果说明：失败显示 ErrorMessage，成功显示调用方 IP */
  detail: string;
  time: string;
}

/** 令牌掩码：保留到第一个下划线前缀，其后打码；无下划线则前 4 位打码 */
export function maskToken(token: unknown): string {
  const t = str(token);
  if (!t) return '—';
  const i = t.indexOf('_');
  if (i > 0) return `${t.slice(0, i + 1)}****`;
  return t.length > 4 ? `${t.slice(0, 4)}****` : '****';
}

const RES_TYPE_LABEL: Record<string, string> = {
  Project: '项目',
  Node: '节点',
  App: '应用',
};

/** 按 TokenId 聚合授权范围：「全部」或去重的资源类型名 */
export function buildScopeMap(resources: Rec[]): Map<number, string> {
  const byToken = new Map<number, { all: boolean; types: Set<string> }>();
  for (const r of resources) {
    const tid = num(pick(r, 'TokenId', 'tokenId'));
    if (tid <= 0) continue;
    const e = byToken.get(tid) ?? { all: false, types: new Set<string>() };
    if (bool(pick(r, 'IsAll', 'isAll'))) e.all = true;
    const rt = str(pick(r, 'ResourceType', 'resourceType'));
    if (rt) e.types.add(RES_TYPE_LABEL[rt] ?? rt);
    byToken.set(tid, e);
  }
  const out = new Map<number, string>();
  for (const [tid, e] of byToken) {
    if (e.all) out.set(tid, '全部资源');
    else if (e.types.size > 0) out.set(tid, [...e.types].join(' + '));
  }
  return out;
}

/** McpToken 行 → Token 表行 */
export function toMcpTokens(rows: Rec[], scopeMap: Map<number, string>): McpTokenRow[] {
  const out: McpTokenRow[] = [];
  for (const r of rows) {
    const id = num(pick(r, 'Id', 'id'));
    const expireMs = toMs(pick(r, 'ExpireTime', 'expireTime'));
    out.push({
      id,
      name: str(pick(r, 'Name', 'name')),
      masked: maskToken(pick(r, 'Token', 'token')),
      enable: bool(pick(r, 'Enable', 'enable')),
      expireText: expireMs ? fmtTime(expireMs) : '永不过期',
      callCount: num(pick(r, 'CallCount', 'callCount')),
      lastText: fmtTime(toMs(pick(r, 'LastTime', 'lastTime'))),
      scopeText: scopeMap.get(id) ?? '—',
    });
  }
  return out;
}

/** McpAudit 行 → 审计列表行，按调用时间倒序取前 limit */
export function toMcpAudits(rows: Rec[], limit = 20): McpAuditRow[] {
  const out: McpAuditRow[] = [];
  for (const r of rows) {
    const tool = str(pick(r, 'ToolName', 'toolName'));
    const action = str(pick(r, 'ActionName', 'actionName'));
    const ok = bool(pick(r, 'Success', 'success'));
    out.push({
      id: String(pick(r, 'Id', 'id') ?? out.length),
      tool: action ? `${tool} · ${action}` : tool,
      ok,
      detail: ok ? str(pick(r, 'CallerIp', 'callerIp')) || '已放行' : str(pick(r, 'ErrorMessage', 'errorMessage')) || '调用失败',
      time: fmtTime(toMs(pick(r, 'CreateTime', 'createTime'))),
    });
  }
  out.sort((a, b) => (a.time < b.time ? 1 : -1));
  return out.slice(0, limit);
}

// ==================== Tab 3.1 · MCP 服务器 ====================

export interface McpServerRow {
  id: number;
  /** 服务器名 = Name */
  name: string;
  /** 远端 URL = Url/Address/Endpoint */
  url: string;
  enable: boolean;
  /** 工具数（后端返回 ToolCount 时归一，否则 0） */
  tools: number;
  /** 归属项目 GalaxyProject.Id；0 = 未归属 */
  projectId: number;
  /** 备注 */
  remark: string;
}

/** McpServer 行 → 服务器表行 */
export function toMcpServerRows(rows: Rec[]): McpServerRow[] {
  const out: McpServerRow[] = [];
  for (const r of rows) {
    out.push({
      id: num(pick(r, 'Id', 'id')),
      name: str(pick(r, 'Name', 'name')),
      url: str(pick(r, 'Url', 'url', 'Address', 'address', 'Endpoint', 'endpoint')),
      enable: bool(pick(r, 'Enable', 'enable')),
      tools: num(pick(r, 'ToolCount', 'toolCount', 'Tools', 'tools')),
      projectId: num(pick(r, 'ProjectId', 'projectId')),
      remark: str(pick(r, 'Remark', 'remark')),
    });
  }
  return out;
}

export interface McpServerResult {
  rows: McpServerRow[];
  /** 拉取失败时的文案，由 McpTab 展示；成功但为空由空态文案展示 */
  error: string;
}

/**
 * 拉取 MCP 服务器列表（GET /Platform/McpServer）。后端已提供 McpServerController；
 * 失败时 catch 住返回文案，不向用户抛栈。
 */
export async function fetchMcpServers(request: Req, projectId?: number | null): Promise<McpServerResult> {
  try {
    const res = await request({ url: '/Platform/McpServer', method: 'GET', params: { PageSize: 500, ...paramsOf(projectId) } });
    return { rows: toMcpServerRows(listOf(res)), error: '' };
  } catch {
    return { rows: [], error: 'MCP 服务器列表加载失败' };
  }
}

export interface McpResult {
  tokens: McpTokenRow[];
  audits: McpAuditRow[];
  error: string;
}

export async function fetchMcp(request: Req, projectId?: number | null): Promise<McpResult> {
  try {
    const pp = paramsOf(projectId);
    const [tokenRes, resRes, auditRes] = await Promise.all([
      request({ url: '/Platform/McpToken', method: 'GET', params: { PageSize: 500, ...pp } }),
      request({ url: '/Platform/McpTokenResource', method: 'GET', params: { PageSize: 500, ...pp } }),
      request({ url: '/Platform/McpAudit', method: 'GET', params: { PageSize: 100, ...pp } }),
    ]);
    const scopeMap = buildScopeMap(listOf(resRes));
    return {
      tokens: toMcpTokens(listOf(tokenRes), scopeMap),
      audits: toMcpAudits(listOf(auditRes)),
      error: '',
    };
  } catch (e) {
    return { tokens: [], audits: [], error: errText(e, 'MCP 数据加载失败') };
  }
}

// ==================== Tab 4 · 平台设置 ====================

export type TileStatus = 'real' | 'pending';

export interface PlatformTile {
  key: string;
  /** KIcon 图标名 */
  icon: string;
  title: string;
  /** real=有真实数据；pending=控制台暂无配置入口，detail 写明原因 */
  status: TileStatus;
  /** 真实摘要；pending 时为诚实说明（哪个入口有/在哪个后台配），不再只写「待接入」 */
  detail: string;
}

/** 逗号分隔的 IP 列表计数（空串=0） */
export function countCsv(v: unknown): number {
  const s = str(v);
  if (!s) return 0;
  return s.split(',').map((x) => x.trim()).filter((x) => x).length;
}

// ==================== Tab 4.1 · 项目成员（ProjectUser 不关联 User 表，用户名诚实降级） ====================

export interface MemberRow {
  id: number;
  /** 用户 ID（ProjectUser.UserId） */
  userId: number;
  /** 展示名：UserName（Map 自 Membership）优先；都没有时退化为「#UserId」 */
  name: string;
  /** 角色：实体没有 Role，仅当行里带了 Role/Kind 才显示，否则「成员」 */
  role: string;
  /** Enable 缺省视为启用 */
  enable: boolean;
  joinedText: string;
}

function memberEnable(v: unknown): boolean {
  if (v === undefined || v === null || v === '') return true;
  return bool(v);
}

/** ProjectUser 行 → 成员表行。用户名读 UserName（后端 Map），没有再退到 Name。 */
export function toMembers(rows: Rec[]): MemberRow[] {
  const out: MemberRow[] = [];
  for (const r of rows) {
    const userId = num(pick(r, 'UserId', 'userId'));
    const realName = str(pick(r, 'UserName', 'userName', 'Name', 'name', 'DisplayName', 'displayName'));
    out.push({
      id: num(pick(r, 'Id', 'id')),
      userId,
      name: realName || (userId > 0 ? `#${userId}` : '—'),
      role: str(pick(r, 'Role', 'role', 'Kind', 'kind')) || '成员',
      enable: memberEnable(pick(r, 'Enable', 'enable')),
      joinedText: fmtTime(toMs(pick(r, 'CreateTime', 'createTime'))),
    });
  }
  return out;
}

export interface ProjectInfo {
  id: number;
  name: string;
  /** 环境；取不到显示 — */
  environment: string;
  createdText: string;
}

/** 从 GalaxyProject 行里选当前项目并归一为只读项目信息；无项目行返回 null */
export function toProjectInfo(rows: Rec[], projectId?: number | null): ProjectInfo | null {
  const proj = pickProject(rows, projectId);
  if (!proj) return null;
  const env = str(pick(proj, 'Environment', 'environment', 'Env', 'env'));
  return {
    id: num(pick(proj, 'Id', 'id')),
    name: str(pick(proj, 'Name', 'name')) || '—',
    environment: env || '—',
    createdText: fmtTime(toMs(pick(proj, 'CreateTime', 'createTime'))),
  };
}

/** 从 GalaxyProject 列表里取当前项目（按 projectId 精确匹配，否则首行） */
export function pickProject(rows: Rec[], projectId?: number | null): Rec | undefined {
  if (projectId != null) {
    const hit = rows.find((r) => num(pick(r, 'Id', 'id')) === projectId);
    if (hit) return hit;
  }
  return rows[0];
}

/** 把三张源数据归一为 8 个磁贴。取不到真实值的磁贴标 pending，不编造开关 */
export function buildTiles(
  projectRows: Rec[],
  memberRows: Rec[],
  setting: Rec | undefined,
  projectId?: number | null,
): PlatformTile[] {
  const proj = pickProject(projectRows, projectId);
  // 项目数 = GalaxyProject 可见行数（该控制器不过滤 projectId，返回当前账号可见的全部项目）
  const projectCount = projectRows.length;
  const nodes = proj ? num(pick(proj, 'Nodes', 'nodes')) : 0;
  const apps = proj ? num(pick(proj, 'Apps', 'apps')) : 0;
  const white = proj ? countCsv(pick(proj, 'WhiteIPs', 'whiteIPs')) : 0;
  const black = proj ? countCsv(pick(proj, 'BlackIPs', 'blackIPs')) : 0;
  const members = memberRows.length;

  const dr1 = setting ? num(pick(setting, 'DataRetention', 'dataRetention')) : 0;
  const dr2 = setting ? num(pick(setting, 'DataRetention2', 'dataRetention2')) : 0;
  const dr3 = setting ? num(pick(setting, 'DataRetention3', 'dataRetention3')) : 0;
  const dotNetPeriod = setting ? num(pick(setting, 'DotNetSyncPeriod', 'dotNetSyncPeriod')) : 0;
  const uplink = setting ? str(pick(setting, 'UplinkServer', 'uplinkServer')) : '';

  const real = (key: string, icon: string, title: string, detail: string): PlatformTile => ({ key, icon, title, status: 'real', detail });
  const pending = (key: string, icon: string, title: string, detail: string): PlatformTile => ({ key, icon, title, status: 'pending', detail });

  return [
    real('members', 'Users', '成员与权限', `${members} 位成员 · ${projectCount} 个项目 · ${nodes} 节点 / ${apps} 应用`),
    pending('sso', 'Key', 'OAuth 单点登录', '控制台暂未开放配置入口，请到后端管理后台配置'),
    real('retention', 'Database', '数据保留', `明细 ${dr1} 天 · 小时 ${dr2} 天 · 历史 ${dr3} 天`),
    pending('ddns', 'Globe', '动态域名 DDNS', '控制台暂未开放配置入口，请到后端管理后台配置'),
    real('dotnet', 'Zap', '.NET 运行时', dotNetPeriod > 0 ? `每 ${Math.round(dotNetPeriod / 3600)}h 同步官方版本` : '自动同步已禁用'),
    pending('audit', 'ScrollText', '审计日志', 'MCP 调用审计见「AI / MCP」页签'),
    real('firewall', 'Shield', '节点防火墙', `白名单 ${white} 条 · 黑名单 ${black} 条`),
    real('cascade', 'GitBranch', '级联 / Apollo', uplink ? `上级 ${uplink}` : '未配置上级服务器'),
  ];
}

export interface PlatformResult {
  tiles: PlatformTile[];
  /** 项目成员列表（ProjectUser 行归一；无用户名字段时 name 退化为 #UserId） */
  members: MemberRow[];
  /** 当前项目只读信息；无项目行时为 null */
  projectInfo: ProjectInfo | null;
  error: string;
}

export async function fetchPlatform(request: Req, projectId?: number | null): Promise<PlatformResult> {
  try {
    const pp = paramsOf(projectId);
    const [galaxyRes, userRes, settingRes] = await Promise.all([
      request({ url: '/Platform/GalaxyProject', method: 'GET', params: { PageSize: 500, ...pp } }),
      request({ url: '/Platform/ProjectUser', method: 'GET', params: { PageSize: 500, ...pp } }),
      // ConfigController 返回单个设置对象（非列表）
      request({ url: '/Platform/StarServer', method: 'GET', params: { ...pp } }),
    ]);
    const galaxyRows = listOf(galaxyRes);
    const userRows = listOf(userRes);
    const data = settingRes?.data;
    const setting = data && !Array.isArray(data) ? (data as Rec) : undefined;
    return {
      tiles: buildTiles(galaxyRows, userRows, setting, projectId),
      members: toMembers(userRows),
      projectInfo: toProjectInfo(galaxyRows, projectId),
      error: '',
    };
  } catch (e) {
    return { tiles: [], members: [], projectInfo: null, error: errText(e, '平台设置加载失败') };
  }
}
