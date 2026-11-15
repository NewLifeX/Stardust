// 中间件页（/mesh 中间件 tab，CARD-MW-01/02 起）纯逻辑：MySQL 数据/节点取数与字段归一（无 Vue 依赖，request 由调用方注入，便于单测）。
//
// 数据源（已读后端实体/控制器确认，2026-11-09）：
//  MySQL 数据  GET /MySql/MySqlData   只读分页。控制器（Areas/MySql/MySqlDataController.cs:23-31）读
//             mysqlId/dtStart/dtEnd/Q；实体 MySqlData.Search（MySql数据.cs:312-321）：
//             mySqlId>=0 才过滤（-1=全部，0 是合法过滤值），Id.Between 雪花区间（双 MinValue=不过滤），Q=名称 like
//  MySQL 节点  GET /MySql/MySqlNode   MW-01 节点候选 + MW-02 节点管理列表（同 URL）；
//             GET /{id} Detail / PUT /{id} 保存 / POST /Refresh?id= 手动刷新。
//             控制器 Search（MySqlNodeController.cs:62-80）读 Id（>0 单条直达先行，其余条件短路，深链非控件）/
//             server（实体 MySql节点.cs:368 精确 ==）/port/projectId（:369-370 >=0 即等值过滤，-1=不过滤）/
//             enable（可空布尔）/dtStart/dtEnd（UpdateTime.Between）/Q（SearchWhereByKeys=Name like）
//
// 数据原则：只接真实接口，不造演示数据。本地库常态无采样 → 空态是一等公民；
// MySqlData 雪花 Id≈10^18 超出 JS 安全整数，以 string 承载防进一步精度损失；
// mysqlId「全部」必须省略不传（传 0 会过滤 MySqlId==0 的采样）；
// 节点实体 Id=Int32（MySql节点.cs:31，非雪花）→ number 承载即可，无 string 需求（C03 如实差异）；
// 节点 port/projectId「全部」必须省略不传（后端 >=0 即等值过滤，传 0 会过滤 Port==0/ProjectId==0）；
// dtEnd 日期自动补 ' 23:59:59'（含端点，保证当日入窗，同 mcpToken 审计先例）；
// 服务端 FindAll(exp, page)：MySqlData 按雪花 Id 倒序（最新采样在前）；MySqlNode Id=Int32 默认主键序，前端不重排。
// 写路径（MW-02，gatewayCert.logic.ts 先例）：编辑=GET /{id} Detail 回填 → PUT /{id} 全量合并提交（Detail+表单覆盖）；
// 无新增（控制器 RemoveCreateField，与旧列表一致）；Refresh 成功信封 {code:0,message:'刷新[node]成功！'} 透传 toast
// （JsonRefresh，BaseControllerX.cs:124）；节点 Password 明文在库，旧列表未裁该列——新卡任何只读路径不渲染（安全记差异）。
import type { Req, ReqOptions } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

export type Rec = Record<string, unknown>;

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
function listOf(res: { data?: unknown } | undefined): Rec[] {
  const d = res?.data;
  return Array.isArray(d) ? (d as Rec[]) : [];
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

// ==================== 中间件 · MySQL 数据（MW-01） ====================

/** MySQL 采样行（展示归一后） */
export interface MySqlDataRow {
  /** 雪花主键；JS 安全整数之外，以 string 承载 */
  id: string;
  /** 所属 MySQL 节点编号 */
  mysqlId: number;
  /** 节点名（前端由候选列表映射，采样实体无此列） */
  nodeName: string;
  /** 采样名（实体 Name，通常 = 节点名@实例） */
  name: string;
  /** 启动时长（Uptime 秒 → 人话） */
  uptime: string;
  /** 当前连接数 */
  connections: number;
  /** 最大连接数（与 connections 组合展示 x / y） */
  maxConnections: number;
  qps: number;
  tps: number;
  /** 收流量（BytesReceived → 人话） */
  bytesIn: string;
  /** 发流量（BytesSent → 人话） */
  bytesOut: string;
  /** InnoDB 缓冲池（size MB · pages 页） */
  bufferPool: string;
  /** InnoDB 缓冲池命中率（0=无数据显示 —） */
  hitRate: string;
  slowQueries: number;
  threads: number;
  threadsRunning: number;
  /** 采样时间（CreateTime → 'YYYY-MM-DD HH:mm'） */
  sampledAt: string;
}

/** 启动时长（秒 → 人话）：天/小时两级起展示，其余分钟/秒。0 → '—' */
export function formatUptime(seconds: number): string {
  if (!(seconds > 0)) return '—';
  const d = Math.floor(seconds / 86400);
  const h = Math.floor((seconds % 86400) / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  if (d > 0) return `${d} 天 ${h} 小时`;
  if (h > 0) return `${h} 小时 ${m} 分`;
  if (m > 0) return `${m} 分`;
  return `${seconds} 秒`;
}

/** 字节数 → 人话（B/KB/MB/GB，两位阶梯，去尾零）。负数按 0 */
export function formatBytes(n: number): string {
  const v = Number.isFinite(n) && n > 0 ? n : 0;
  const units = ['B', 'KB', 'MB', 'GB'];
  let i = 0;
  let x = v;
  while (x >= 1024 && i < units.length - 1) {
    x /= 1024;
    i++;
  }
  const s = i === 0 ? String(x) : x.toFixed(1).replace(/\.0$/, '');
  return `${s} ${units[i]}`;
}

/** InnoDB 缓冲池：`128 MB · 65536 页`；无数据（双 0）→ '—' */
export function formatBufferPool(sizeMB: number, pages: number): string {
  if (!(sizeMB > 0) && !(pages > 0)) return '—';
  return `${sizeMB} MB · ${pages} 页`;
}

/** MySQL 数据筛选（服务端过滤：mysqlId/dtStart-dtEnd/Q + 分页） */
export interface MySqlDataFilter {
  /** 节点编号；0 = 全部（不下发，服务端默认 -1 不过滤） */
  mysqlId: number;
  /** 起始日期 'YYYY-MM-DD'；空 = 不限 */
  dtStart: string;
  /** 结束日期 'YYYY-MM-DD'；空 = 不限（自动补到当日 23:59:59） */
  dtEnd: string;
  /** 名称关键字（服务端 SearchWhereByKeys=Name like） */
  q: string;
  /** 页码，从 0 起（后端 pageIndex） */
  pageIndex: number;
  pageSize: number;
}

export function emptyMySqlDataFilter(): MySqlDataFilter {
  return { mysqlId: 0, dtStart: '', dtEnd: '', q: '', pageIndex: 0, pageSize: 20 };
}

/** 查询条件 → 请求 params。空值不下发；mysqlId>0 才带（0/-1 都会或可能变成等值过滤语义） */
export function mysqlDataFilterParams(f: MySqlDataFilter): Record<string, unknown> {
  const p: Record<string, unknown> = {
    pageIndex: Math.max(0, f.pageIndex),
    pageSize: f.pageSize > 0 ? f.pageSize : 20,
  };
  if (f.mysqlId > 0) p.mysqlId = f.mysqlId;
  if (f.dtStart.trim()) p.dtStart = f.dtStart.trim();
  const end = f.dtEnd.trim();
  if (end) p.dtEnd = end.includes(' ') ? end : `${end} 23:59:59`;
  if (f.q.trim()) p.Q = f.q.trim();
  return p;
}

/** 实体行 → 展示行。PascalCase/camelCase 双兼容；nodeName 由候选映射，映射不到留空 */
export function toMySqlDataRows(rows: Rec[], nodeNames?: Map<number, string>): MySqlDataRow[] {
  return rows.map((r) => {
    const mysqlId = num(pick(r, 'MySqlId', 'mySqlId', 'mysqlId'));
    const poolSize = num(pick(r, 'InnodbBufferPoolSize', 'innodbBufferPoolSize'));
    const poolPages = num(pick(r, 'InnodbBufferPoolPages', 'innodbBufferPoolPages'));
    const hit = num(pick(r, 'InnodbBufferPoolHitRate', 'innodbBufferPoolHitRate'));
    const ms = toMs(pick(r, 'CreateTime', 'createTime'));
    return {
      id: str(pick(r, 'Id', 'id')),
      mysqlId,
      nodeName: nodeNames?.get(mysqlId) || '',
      name: str(pick(r, 'Name', 'name')),
      uptime: formatUptime(num(pick(r, 'Uptime', 'uptime'))),
      connections: num(pick(r, 'Connections', 'connections')),
      maxConnections: num(pick(r, 'MaxConnections', 'maxConnections')),
      qps: num(pick(r, 'Qps', 'qps')),
      tps: num(pick(r, 'Tps', 'tps')),
      bytesIn: formatBytes(num(pick(r, 'BytesReceived', 'bytesReceived'))),
      bytesOut: formatBytes(num(pick(r, 'BytesSent', 'bytesSent'))),
      bufferPool: formatBufferPool(poolSize, poolPages),
      hitRate: hit > 0 ? `${hit}%` : '—',
      slowQueries: num(pick(r, 'SlowQueries', 'slowQueries')),
      threads: num(pick(r, 'Threads', 'threads')),
      threadsRunning: num(pick(r, 'ThreadsRunning', 'threadsRunning')),
      sampledAt: ms ? fmtTime(ms) : '—',
    };
  });
}

/** 总页数（向上取整；total<=0 或 pageSize<=0 → 1） */
export function mysqlDataTotalPages(total: number, pageSize: number): number {
  if (total <= 0 || pageSize <= 0) return 1;
  return Math.ceil(total / pageSize);
}

export interface MySqlDataPageResult {
  rows: MySqlDataRow[];
  total: number;
  error: string;
}

/** MySQL 数据服务端查询：GET /MySql/MySqlData（只读分页，雪花倒序=最新在前） */
export async function fetchMySqlData(req: Req, f: MySqlDataFilter, nodeNames?: Map<number, string>): Promise<MySqlDataPageResult> {
  try {
    const res = await req({ url: '/MySql/MySqlData', method: 'GET', params: mysqlDataFilterParams(f) });
    const rows = toMySqlDataRows(listOf(res), nodeNames);
    const page = (res as { page?: { totalCount?: unknown; total?: unknown } } | undefined)?.page;
    const total = num(page?.totalCount ?? page?.total) || rows.length;
    return { rows, total, error: '' };
  } catch (e) {
    return { rows: [], total: 0, error: errText(e, 'MySQL 数据查询失败') };
  }
}

// ==================== 中间件 · MySQL 节点候选（MW-01 筛选数据源） ====================

export interface MySqlNodeOption {
  id: number;
  name: string;
}

/** 节点实体行 → 候选项（丢掉无 Id/无名字的脏行） */
export function toMySqlNodeOptions(rows: Rec[]): MySqlNodeOption[] {
  return rows
    .map((r) => ({ id: num(pick(r, 'Id', 'id')), name: str(pick(r, 'Name', 'name')) }))
    .filter((o) => o.id > 0 && !!o.name);
}

export async function fetchMySqlNodes(req: Req): Promise<{ options: MySqlNodeOption[]; error: string }> {
  try {
    // pageSize=0 → 服务端不分页全量（卡片契约；enable=true 只留启用节点）
    const res = await req({ url: '/MySql/MySqlNode', method: 'GET', params: { enable: true, pageSize: 0 } });
    return { options: toMySqlNodeOptions(listOf(res)), error: '' };
  } catch (e) {
    return { options: [], error: errText(e, '节点候选加载失败') };
  }
}

// ==================== 中间件 · MySQL 节点（MW-02） ====================

function boolOf(v: unknown): boolean {
  if (typeof v === 'boolean') return v;
  if (typeof v === 'number') return v !== 0;
  const s = str(v).toLowerCase();
  return s === 'true' || s === '1';
}

/** 实体信封 → {ok,message}：code===0 即成功，message 透传后端（JsonRefresh 刷新[node]成功！；BaseControllerX.cs:124） */
function okOf(res: unknown, fallback: string): { ok: boolean; message: string } {
  const r = (res ?? {}) as Rec;
  const code = r.code === undefined ? -1 : num(r.code);
  const message = str(r.message);
  return { ok: code === 0, message: message || fallback };
}

/** MySQL 节点行（展示归一后）。Id=Int32 非雪花（MySql节点.cs:31）；Password 有值但任何只读路径不渲染（安全记差异） */
export interface MySqlNodeRow {
  id: number;
  projectId: number;
  /** 项目名（实体 ProjectName 扩展属性）；空 → 卡片渲染 '—'（链接待 PLT-01 接入，记差异） */
  projectName: string;
  name: string;
  category: string;
  server: string;
  port: number;
  userName: string;
  databaseName: string;
  version: string;
  enable: boolean;
  /** 更新时间（UpdateTime → 'YYYY-MM-DD HH:mm'） */
  updateTime: string;
}

export function toMySqlNodeRows(rows: Rec[]): MySqlNodeRow[] {
  return rows.map((r) => {
    const uMs = toMs(pick(r, 'UpdateTime', 'updateTime'));
    return {
      id: num(pick(r, 'Id', 'id')),
      projectId: num(pick(r, 'ProjectId', 'projectId')),
      projectName: str(pick(r, 'ProjectName', 'projectName')),
      name: str(pick(r, 'Name', 'name')),
      category: str(pick(r, 'Category', 'category')),
      server: str(pick(r, 'Server', 'server')),
      port: num(pick(r, 'Port', 'port')),
      userName: str(pick(r, 'UserName', 'userName')),
      databaseName: str(pick(r, 'DatabaseName', 'databaseName')),
      version: str(pick(r, 'Version', 'version')),
      enable: boolOf(pick(r, 'Enable', 'enable')),
      updateTime: uMs ? fmtTime(uMs) : '—',
    };
  });
}

/** MySQL 节点筛选（服务端过滤：id 深链/server 精确/port/projectId/enable/时间区间/Q + 分页） */
export interface MySqlNodeFilter {
  /** 深链单条直达（URL ?id=，定稿裁定：不作控件）；>0 随查下发；用户一旦改动筛选即失效（容器置 0） */
  id: number;
  /** 服务器地址（服务端精确 ==，控件注记「精确匹配」）；空 = 不限 */
  server: string;
  /** 端口（原样字符串便于空态输入；空 = 不限，纯数字才下发，0 也是合法等值过滤） */
  port: string;
  /** 项目编号（原样字符串；空 = 不限，纯数字才下发；项目下拉待 PLT-01，暂手输） */
  projectId: string;
  /** 启用：''=全部（不下发）/'true'/'false' */
  enable: '' | 'true' | 'false';
  dtStart: string;
  dtEnd: string;
  /** 名称关键字（Q → SearchWhereByKeys=Name like） */
  q: string;
  pageIndex: number;
  pageSize: number;
}

export function emptyMySqlNodeFilter(): MySqlNodeFilter {
  return { id: 0, server: '', port: '', projectId: '', enable: '', dtStart: '', dtEnd: '', q: '', pageIndex: 0, pageSize: 20 };
}

/** 筛选 → 请求 params。空值不下发；port/projectId 仅纯数字才带（后端 >=0 即等值过滤，传 0 是合法过滤不能滥发）；
 * enable=''+id<=0 不传；dtEnd 日期自动补 ' 23:59:59'（UpdateTime.Between 含端点） */
export function mysqlNodeFilterParams(f: MySqlNodeFilter): Record<string, unknown> {
  const p: Record<string, unknown> = {
    pageIndex: Math.max(0, f.pageIndex),
    pageSize: f.pageSize > 0 ? f.pageSize : 20,
  };
  if (f.id > 0) p.id = f.id;
  if (f.server.trim()) p.server = f.server.trim();
  const port = f.port.trim();
  if (port && Number.isFinite(Number(port))) p.port = Number(port);
  const pid = f.projectId.trim();
  if (pid && Number.isFinite(Number(pid))) p.projectId = Number(pid);
  if (f.enable === 'true' || f.enable === 'false') p.enable = f.enable === 'true';
  if (f.dtStart.trim()) p.dtStart = f.dtStart.trim();
  const end = f.dtEnd.trim();
  if (end) p.dtEnd = end.includes(' ') ? end : `${end} 23:59:59`;
  if (f.q.trim()) p.Q = f.q.trim();
  return p;
}

export interface MySqlNodePageResult {
  rows: MySqlNodeRow[];
  total: number;
  error: string;
}

/** MySQL 节点服务端查询：GET /MySql/MySqlNode（分页；Id=Int32 默认主键序，前端不重排） */
export async function fetchMySqlNodeList(req: Req, f: MySqlNodeFilter): Promise<MySqlNodePageResult> {
  try {
    const res = await req({ url: '/MySql/MySqlNode', method: 'GET', params: mysqlNodeFilterParams(f) });
    const rows = toMySqlNodeRows(listOf(res));
    const page = (res as { page?: { totalCount?: unknown; total?: unknown } } | undefined)?.page;
    const total = num(page?.totalCount ?? page?.total) || rows.length;
    return { rows, total, error: '' };
  } catch (e) {
    return { rows: [], total: 0, error: errText(e, 'MySQL 节点查询失败') };
  }
}

/** 单条 Detail：GET /MySql/MySqlNode/{id}（编辑回填源，同 gatewayCert.fetchCertDetail 先例）；无数据/异常 → null */
export async function fetchMySqlNodeDetail(req: Req, id: number): Promise<Rec | null> {
  try {
    const res = await req({ url: `/MySql/MySqlNode/${id}`, method: 'GET' });
    const d = (res as Rec)?.data;
    return d && !Array.isArray(d) ? (d as Rec) : null;
  } catch {
    return null;
  }
}

/** 节点编辑表单（实体可编辑业务字段，除 Id 与审计列；控制器 RemoveCreateField → 无新增入口，仅编辑） */
export interface MySqlNodeForm {
  projectId: number;
  name: string;
  category: string;
  server: string;
  port: number;
  userName: string;
  /** 密码：掩码输入回显，随 PUT 全量提交（实体无「留空=不修改」契约，同 Cube 默认表单）；只读路径不渲染 */
  password: string;
  databaseName: string;
  version: string;
  enable: boolean;
  webHook: string;
  alarmConnections: number;
  alarmQps: number;
  alarmSlowQuery: number;
  remark: string;
}

export function nodeDetailToForm(d: Rec): MySqlNodeForm {
  return {
    projectId: num(pick(d, 'ProjectId', 'projectId')),
    name: str(pick(d, 'Name', 'name')),
    category: str(pick(d, 'Category', 'category')),
    server: str(pick(d, 'Server', 'server')),
    port: num(pick(d, 'Port', 'port')),
    userName: str(pick(d, 'UserName', 'userName')),
    password: str(pick(d, 'Password', 'password')),
    databaseName: str(pick(d, 'DatabaseName', 'databaseName')),
    version: str(pick(d, 'Version', 'version')),
    enable: boolOf(pick(d, 'Enable', 'enable')),
    webHook: str(pick(d, 'WebHook', 'webHook')),
    alarmConnections: num(pick(d, 'AlarmConnections', 'alarmConnections')),
    alarmQps: num(pick(d, 'AlarmQPS', 'alarmQPS', 'AlarmQps', 'alarmQps')),
    alarmSlowQuery: num(pick(d, 'AlarmSlowQuery', 'alarmSlowQuery')),
    remark: str(pick(d, 'Remark', 'remark')),
  };
}

/** 抽屉校验：Name 必填「名称不能为空」（实体 Valid 无强校验〔MySql节点.Biz.cs:53-80 全注释〕，前端最小必填=展示键） */
export function validateMySqlNodeForm(m: MySqlNodeForm): { name?: string } {
  const errs: { name?: string } = {};
  if (!m.name.trim()) errs.name = '名称不能为空';
  return errs;
}

/** 编辑提交体：Detail 全量 + 表单字段覆盖（PUT 合并语义；Id/审计列随 Detail 原样保留） */
export function nodeUpdatePayload(d: Rec, m: MySqlNodeForm): Rec {
  return {
    ...d,
    ProjectId: m.projectId,
    Name: m.name,
    Category: m.category,
    Server: m.server,
    Port: m.port,
    UserName: m.userName,
    Password: m.password,
    DatabaseName: m.databaseName,
    Version: m.version,
    Enable: m.enable,
    WebHook: m.webHook,
    AlarmConnections: m.alarmConnections,
    AlarmQPS: m.alarmQps,
    AlarmSlowQuery: m.alarmSlowQuery,
    Remark: m.remark,
  };
}

/** 保存节点编辑：PUT /MySql/MySqlNode/{id}（detail 缺 Id → 明确失败不盲发） */
export async function submitMySqlNode(req: Req, d: Rec, m: MySqlNodeForm): Promise<{ ok: boolean; message: string }> {
  const id = num(pick(d, 'Id', 'id'));
  if (!(id > 0)) return { ok: false, message: '节点不存在或已被删除' };
  try {
    const res = await req({ url: `/MySql/MySqlNode/${id}`, method: 'PUT', data: nodeUpdatePayload(d, m) } as ReqOptions & { data: Rec });
    return okOf(res as Rec, '保存失败');
  } catch (e) {
    return { ok: false, message: errText(e, '保存失败') };
  }
}

/** 手动刷新（C05）：POST /MySql/MySqlNode/Refresh?id=；成功 message 透传后端「刷新[node]成功！」供 §3.5 toast */
export async function refreshMySqlNode(req: Req, id: number): Promise<{ ok: boolean; message: string }> {
  try {
    const res = await req({ url: '/MySql/MySqlNode/Refresh', method: 'POST', params: { id } });
    return okOf(res, '刷新失败');
  } catch (e) {
    return { ok: false, message: errText(e, '刷新失败') };
  }
}

// ==================== 中间件 · Redis 数据（MW-03） ====================

/**
 * Redis 采样行（展示归一后）。数据源（已读后端确认，2026-11-09）：
 *  GET /Redis/RedisData   只读分页（RedisDataController.cs:24-44）：
 *    读 redisId/dtStart/dtEnd；Q 透传（RedisData.Search 第 4 参=keys like）；
 *    魔法分页（:33-37）：redisId>0 且请求 PageSize==20 → 服务端改 PageSize=24*60=1440
 *    （一天一节点最多 1440 个分钟采样）——因此前端请求**绝不携带 pageSize**（含 20），
 *    靠「缺省」触发魔法；选中节点时一次拉全 1440 行本地切片。
 *  GET /Redis/RedisNode/NodeSearch   节点候选（Lov，RedisNodeController.cs:92-109）：
 *    服务端固定 enable=true，返回 Json(0,null,[{Id,Name,Server,Category}])；category 空缺省=全类型。
 */
export interface RedisDataRow {
  /** 雪花主键（Int64）；JS 安全整数之外，以 string 承载 */
  id: string;
  /** 所属 Redis 节点编号 */
  redisId: number;
  /** 节点名（前端由候选列表映射，采样实体无此列） */
  nodeName: string;
  /** 采样名（实体 Name，通常 = 节点名@实例） */
  name: string;
  /** 每秒操作数（instantaneous_ops_per_sec） */
  speed: number;
  /** 入流量（kbps） */
  inputKbps: number;
  /** 出流量（kbps） */
  outputKbps: number;
  /** 当前连接数（ConnectedClients） */
  connections: number;
  /** 已用内存（MB） */
  usedMemory: number;
  /** Keys 数 */
  keys: number;
  /** 命令数（仅列表展示，不进曲线——定稿卡 C03 裁定） */
  commands: number;
  /** 采样时间（CreateTime → 'YYYY-MM-DD HH:mm'） */
  sampledAt: string;
}

/** Redis 数据筛选（分页语义随节点选择切换：无节点=服务端分页；选中=本地切片） */
export interface RedisDataFilter {
  /** 节点编号；0 = 全部（不下发；控制器 :30-32 redisId>0 才入条件） */
  redisId: number;
  /** 起始日期 'YYYY-MM-DD'；空 = 不限 */
  dtStart: string;
  /** 结束日期 'YYYY-MM-DD'；空 = 不限（自动补到当日 23:59:59） */
  dtEnd: string;
  /** 名称关键字（Q 透传 like） */
  q: string;
  /** 页码，从 0 起（后端 pageIndex） */
  pageIndex: number;
  /** 本地切片大小（**只用于前端切片，绝不进请求参数**——C04 定稿裁定①） */
  pageSize: number;
}

export function emptyRedisDataFilter(): RedisDataFilter {
  return { redisId: 0, dtStart: '', dtEnd: '', q: '', pageIndex: 0, pageSize: 20 };
}

/** 查询条件 → 请求 params。**硬性负向：任何分支都不产生 pageSize 键**（C04 硬项）。
 * 节点选中（redisId>0）→ 一次拉全 1440，不下发 pageIndex；无节点 → 服务端分页下发 pageIndex。 */
export function redisDataFilterParams(f: RedisDataFilter): Record<string, unknown> {
  const p: Record<string, unknown> = {};
  if (f.redisId > 0) p.redisId = f.redisId;
  if (f.dtStart.trim()) p.dtStart = f.dtStart.trim();
  const end = f.dtEnd.trim();
  if (end) p.dtEnd = end.includes(' ') ? end : `${end} 23:59:59`;
  if (f.q.trim()) p.Q = f.q.trim();
  if (!(f.redisId > 0)) p.pageIndex = Math.max(0, f.pageIndex);
  return p;
}

/** 实体行 → 展示行。PascalCase/camelCase 双兼容；nodeName 由候选映射，映射不到留空 */
export function toRedisDataRows(rows: Rec[], nodeNames?: Map<number, string>): RedisDataRow[] {
  return rows.map((r) => {
    const redisId = num(pick(r, 'RedisId', 'redisId'));
    const ms = toMs(pick(r, 'CreateTime', 'createTime'));
    return {
      id: str(pick(r, 'Id', 'id')),
      redisId,
      nodeName: nodeNames?.get(redisId) || '',
      name: str(pick(r, 'Name', 'name')),
      speed: num(pick(r, 'Speed', 'speed')),
      inputKbps: num(pick(r, 'InputKbps', 'inputKbps')),
      outputKbps: num(pick(r, 'OutputKbps', 'outputKbps')),
      connections: num(pick(r, 'ConnectedClients', 'connectedClients')),
      usedMemory: num(pick(r, 'UsedMemory', 'usedMemory')),
      keys: num(pick(r, 'Keys', 'keys')),
      commands: num(pick(r, 'Commands', 'commands')),
      sampledAt: ms ? fmtTime(ms) : '—',
    };
  });
}

/** 总页数（向上取整；total<=0 或 pageSize<=0 → 1。语义同 mysqlDataTotalPages） */
export function redisDataTotalPages(total: number, pageSize: number): number {
  if (total <= 0 || pageSize <= 0) return 1;
  return Math.ceil(total / pageSize);
}

export interface RedisDataPageResult {
  /** 当前页列表行（节点选中=本地切片；无节点=服务端分页结果） */
  rows: RedisDataRow[];
  /** 曲线数据源（节点选中=全量行；无节点=空数组 → 曲线区空） */
  chartRows: RedisDataRow[];
  total: number;
  error: string;
}

/** Redis 数据查询：GET /Redis/RedisData。节点选中 → 本地分页切片 + 全量 chartRows；无节点 → 服务端分页 */
export async function fetchRedisData(req: Req, f: RedisDataFilter, nodeNames?: Map<number, string>): Promise<RedisDataPageResult> {
  try {
    const res = await req({ url: '/Redis/RedisData', method: 'GET', params: redisDataFilterParams(f) });
    const all = toRedisDataRows(listOf(res), nodeNames);
    const page = (res as { page?: { totalCount?: unknown; total?: unknown } } | undefined)?.page;
    const total = num(page?.totalCount ?? page?.total) || all.length;
    if (f.redisId > 0) {
      const start = Math.max(0, f.pageIndex) * f.pageSize;
      return { rows: all.slice(start, start + f.pageSize), chartRows: all, total, error: '' };
    }
    return { rows: all, chartRows: [], total, error: '' };
  } catch (e) {
    return { rows: [], chartRows: [], total: 0, error: errText(e, 'Redis 数据查询失败') };
  }
}

export interface RedisNodeOption {
  id: number;
  name: string;
  /** 节点地址（server:port，供下拉展示辅助信息） */
  server: string;
}

/** NodeSearch 候选行 → 候选项（丢掉无 Id/无名字的脏行） */
export function toRedisNodeOptions(rows: Rec[]): RedisNodeOption[] {
  return rows
    .map((r) => ({ id: num(pick(r, 'Id', 'id')), name: str(pick(r, 'Name', 'name')), server: str(pick(r, 'Server', 'server')) }))
    .filter((o) => o.id > 0 && !!o.name);
}

/** Redis 节点候选：GET /Redis/RedisNode/NodeSearch（Lov 端点，服务端固定 enable=true；无参=全类型） */
export async function fetchRedisNodes(req: Req): Promise<{ options: RedisNodeOption[]; error: string }> {
  try {
    const res = await req({ url: '/Redis/RedisNode/NodeSearch', method: 'GET' });
    return { options: toRedisNodeOptions(listOf(res)), error: '' };
  } catch (e) {
    return { options: [], error: errText(e, '节点候选加载失败') };
  }
}

/** 曲线六序列（C03 定稿裁定②：速度/入流量/出流量/连接数/已用内存/Keys 数，**不含 Commands**；X=CreateTime） */
export const REDIS_DATA_SERIES = [
  { key: 'speed', label: '速度' },
  { key: 'inputKbps', label: '入流量' },
  { key: 'outputKbps', label: '出流量' },
  { key: 'connections', label: '连接数' },
  { key: 'usedMemory', label: '已用内存' },
  { key: 'keys', label: 'Keys 数' },
] as const;

/** 采样行 → ECharts option（供 src/components/EChart.vue 消费；颜色走组件默认中性调色板，不在此硬编码） */
export function buildRedisDataChartOption(rows: RedisDataRow[]): Record<string, unknown> {
  return {
    tooltip: { trigger: 'axis' },
    legend: { data: REDIS_DATA_SERIES.map((s) => s.label) },
    grid: { left: 56, right: 24, top: 40, bottom: 40 },
    xAxis: { type: 'category', data: rows.map((r) => r.sampledAt) },
    yAxis: { type: 'value' },
    series: REDIS_DATA_SERIES.map((s) => ({
      name: s.label,
      type: 'line',
      showSymbol: false,
      data: rows.map((r) => num(r[s.key])),
    })),
  };
}

// ==================== 中间件 · Redis 消息队列（MW-04） ====================
//
// 数据源（已读后端确认，2026-11-09）：
//   GET /Redis/RedisMessageQueue（WebApi RedisMessageQueueController.cs 52 行，Web 面同 FQN）：
//   WebEntityController<RedisMessageQueue> 标准 CRUD 就绪；Search 覆写（:42-51）读
//   redisId/category/dtStart/dtEnd/Q → RedisMessageQueue.Search(...)。
//   Biz Search（Redis消息队列.Biz.cs:95-106）：redisId>=0 才过滤（-1=全部，**0 是合法等值过滤值** → 前端
//   「全部」必须省略，同 MW-01 mysqlId 语义）；category 精确 ==；dtStart/dtEnd=**UpdateTime** Between（筛选标签
//   按实写「更新时间」）；Q=Name/Type/CreateUser/CreateIP/UpdateUser/UpdateIP/Remark contains（不含 Topic/Groups）。
//   **标准服务端分页**：FindAll(exp, page)，pageIndex+pageSize 恒发——无 RedisData 1440 魔法（CARD-MW-04 裁定 1）。
//   Id=Int32 自增（Redis消息队列.cs:29）→ number 承载，无 string 需求（同 MySqlNode，如实差异）。
//   追踪列：有 TraceId → 命名路由 ConsoleTrace（/trace/:traceId，routes.ts:28；**禁路径跳转**——打分撞
//   /Trace/:id → RangeError 白屏铁律），无 TraceId 禁用+原因（裁定 1）；审计列=【缺·待接入】不做假入口（裁定 3）。

/** Redis 队列展示行（CARD-MW-04 BL-7 列集：RedisName 由候选映射；消费组/类型/积压/启用/时间全带） */
export interface RedisQueueRow {
  id: number;
  redisId: number;
  /** Redis 节点名（RedisId 候选映射；映射不到留空 → 卡片渲染 '—'） */
  nodeName: string;
  name: string;
  category: string;
  db: number;
  topic: string;
  type: string;
  groups: string;
  consumers: number;
  /** 总消费（现有消费者的消费总数，实体 Int64） */
  total: number;
  /** 消息数（积压待消费个数） */
  messages: number;
  /** 最大积压告警阈值（0=不启用） */
  maxMessages: number;
  enable: boolean;
  firstConsume: string;
  lastActive: string;
  traceId: string;
  updateTime: string;
}

export interface RedisQueueFilter {
  /** 节点编号；0 = 全部（不下发——Biz redisId>=0 即等值过滤，0 非「全部」） */
  redisId: number;
  /** 分类（精确等值）；空 = 不限 */
  category: string;
  /** 起始日期 'YYYY-MM-DD'；空 = 不限 */
  dtStart: string;
  /** 结束日期 'YYYY-MM-DD'；空 = 不限（自动补到当日 23:59:59） */
  dtEnd: string;
  /** 关键字（Q 透传，服务端七字段 contains） */
  q: string;
  /** 页码，从 0 起（后端 pageIndex） */
  pageIndex: number;
  pageSize: number;
}

export function emptyRedisQueueFilter(): RedisQueueFilter {
  return { redisId: 0, category: '', dtStart: '', dtEnd: '', q: '', pageIndex: 0, pageSize: 20 };
}

/** 查询条件 → 请求 params。**标准服务端分页：pageIndex/pageSize 恒发**（无 RedisData 魔法）；
 * redisId>0 才带（0/-1 都会或可能变成等值过滤语义，同 MW-01）；dt 区间=更新时间。 */
export function redisQueueFilterParams(f: RedisQueueFilter): Record<string, unknown> {
  const p: Record<string, unknown> = {
    pageIndex: Math.max(0, f.pageIndex),
    pageSize: f.pageSize > 0 ? f.pageSize : 20,
  };
  if (f.redisId > 0) p.redisId = f.redisId;
  if (f.category.trim()) p.category = f.category.trim();
  if (f.dtStart.trim()) p.dtStart = f.dtStart.trim();
  const end = f.dtEnd.trim();
  if (end) p.dtEnd = end.includes(' ') ? end : `${end} 23:59:59`;
  if (f.q.trim()) p.Q = f.q.trim();
  return p;
}

/** 实体行 → 展示行。PascalCase/camelCase 双兼容；nodeName 由候选映射，映射不到留空 */
export function toRedisQueueRows(rows: Rec[], nodeNames?: Map<number, string>): RedisQueueRow[] {
  return rows.map((r) => {
    const redisId = num(pick(r, 'RedisId', 'redisId'));
    const fMs = toMs(pick(r, 'FirstConsumer', 'firstConsumer'));
    const aMs = toMs(pick(r, 'LastActive', 'lastActive'));
    const uMs = toMs(pick(r, 'UpdateTime', 'updateTime'));
    return {
      id: num(pick(r, 'Id', 'id')),
      redisId,
      nodeName: nodeNames?.get(redisId) || '',
      name: str(pick(r, 'Name', 'name')),
      category: str(pick(r, 'Category', 'category')),
      db: num(pick(r, 'Db', 'db')),
      topic: str(pick(r, 'Topic', 'topic')),
      type: str(pick(r, 'Type', 'type')),
      groups: str(pick(r, 'Groups', 'groups')),
      consumers: num(pick(r, 'Consumers', 'consumers')),
      total: num(pick(r, 'Total', 'total')),
      messages: num(pick(r, 'Messages', 'messages')),
      maxMessages: num(pick(r, 'MaxMessages', 'maxMessages')),
      enable: boolOf(pick(r, 'Enable', 'enable')),
      firstConsume: fMs ? fmtTime(fMs) : '—',
      lastActive: aMs ? fmtTime(aMs) : '—',
      traceId: str(pick(r, 'TraceId', 'traceId')),
      updateTime: uMs ? fmtTime(uMs) : '—',
    };
  });
}

/** 总页数（向上取整；total<=0 或 pageSize<=0 → 1。语义同 mysqlDataTotalPages） */
export function redisQueueTotalPages(total: number, pageSize: number): number {
  if (total <= 0 || pageSize <= 0) return 1;
  return Math.ceil(total / pageSize);
}

export interface RedisQueuePageResult {
  rows: RedisQueueRow[];
  total: number;
  error: string;
}

/** Redis 队列服务端查询：GET /Redis/RedisMessageQueue（只读分页） */
export async function fetchRedisQueues(req: Req, f: RedisQueueFilter, nodeNames?: Map<number, string>): Promise<RedisQueuePageResult> {
  try {
    const res = await req({ url: '/Redis/RedisMessageQueue', method: 'GET', params: redisQueueFilterParams(f) });
    const rows = toRedisQueueRows(listOf(res), nodeNames);
    const page = (res as { page?: { totalCount?: unknown; total?: unknown } } | undefined)?.page;
    const total = num(page?.totalCount ?? page?.total) || rows.length;
    return { rows, total, error: '' };
  } catch (e) {
    return { rows: [], total: 0, error: errText(e, 'Redis 队列查询失败') };
  }
}

// ==================== 中间件 · Redis 节点（MW-05，MW 块枢纽） ====================

/** Redis 节点行（展示归一后）。Id=Int32 非雪花（Redis节点.cs:31）；Password 不映射——任何只读路径不渲染（MW-02 安全先例） */
export interface RedisNodeRow {
  id: number;
  projectId: number;
  /** 项目名（ProjectName 扩展属性）；空 → 卡片渲染 '—'（BL-11：文本不外链，GalaxyProject 待 PLT-01 记差异） */
  projectName: string;
  name: string;
  category: string;
  /** 地址 server:port（服务端精确 == 筛选，控件注记「精确匹配」） */
  server: string;
  userName: string;
  version: string;
  /** 模式（standalone/cluster/sentinel，监控服务 Fill 回填） */
  mode: string;
  /** 角色（master/slave，Fill 回填） */
  role: string;
  enable: boolean;
  /** 自动扫描发现消息队列（默认 true） */
  scanQueue: boolean;
  /** 更新时间（UpdateTime → 'YYYY-MM-DD HH:mm'） */
  updateTime: string;
}

export function toRedisNodeRows(rows: Rec[]): RedisNodeRow[] {
  return rows.map((r) => {
    const uMs = toMs(pick(r, 'UpdateTime', 'updateTime'));
    return {
      id: num(pick(r, 'Id', 'id')),
      projectId: num(pick(r, 'ProjectId', 'projectId')),
      projectName: str(pick(r, 'ProjectName', 'projectName')),
      name: str(pick(r, 'Name', 'name')),
      category: str(pick(r, 'Category', 'category')),
      server: str(pick(r, 'Server', 'server')),
      userName: str(pick(r, 'UserName', 'userName')),
      version: str(pick(r, 'Version', 'version')),
      mode: str(pick(r, 'Mode', 'mode')),
      role: str(pick(r, 'Role', 'role')),
      enable: boolOf(pick(r, 'Enable', 'enable')),
      scanQueue: boolOf(pick(r, 'ScanQueue', 'scanQueue')),
      updateTime: uMs ? fmtTime(uMs) : '—',
    };
  });
}

/** Redis 节点筛选（服务端过滤：id 深链/category 精确/server 精确/enable/时间区间/Q + 分页；Biz Search :123-135 语义） */
export interface RedisNodeFilter {
  /** 深链单条直达（Controller :70-75 Id>0 单条；定稿裁定：不作控件）；>0 随查下发；用户改动筛选即失效 */
  id: number;
  /** 分类（服务端精确 ==；下拉从列表聚合——GetCategoryList 为字段缓存无 WebApi 端点，前端聚合是唯一路） */
  category: string;
  /** 地址（服务端精确 ==，控件注记「精确匹配」）；空 = 不限 */
  server: string;
  /** 启用：''=全部（不下发）/true/false（Controller :79 ToBoolean，空=不过滤） */
  enable: '' | 'true' | 'false';
  dtStart: string;
  dtEnd: string;
  /** 名称关键字（Q → SearchWhereByKeys 12 字段 Contains，含 Password；前端仅透传） */
  q: string;
  pageIndex: number;
  pageSize: number;
}

export function emptyRedisNodeFilter(): RedisNodeFilter {
  return { id: 0, category: '', server: '', enable: '', dtStart: '', dtEnd: '', q: '', pageIndex: 0, pageSize: 20 };
}

/** 筛选 → 请求 params。空值不下发；enable=''+id<=0 不传；dtEnd 日期自动补 ' 23:59:59'（UpdateTime.Between 含端点）；Q 上传大写 p.Q */
export function redisNodeFilterParams(f: RedisNodeFilter): Record<string, unknown> {
  const p: Record<string, unknown> = { pageIndex: Math.max(0, f.pageIndex), pageSize: f.pageSize > 0 ? f.pageSize : 20 };
  if (f.id > 0) p.id = f.id;
  if (f.category.trim()) p.category = f.category.trim();
  if (f.server.trim()) p.server = f.server.trim();
  if (f.enable === 'true' || f.enable === 'false') p.enable = f.enable === 'true';
  if (f.dtStart.trim()) p.dtStart = f.dtStart.trim();
  const end = f.dtEnd.trim();
  if (end) p.dtEnd = end.includes(' ') ? end : `${end} 23:59:59`;
  if (f.q.trim()) p.Q = f.q.trim();
  return p;
}

export interface RedisNodePageResult {
  rows: RedisNodeRow[];
  total: number;
  error: string;
}

/** 总页数（向上取整；total<=0 或 pageSize<=0 → 1。语义同 mysqlDataTotalPages/redisQueueTotalPages） */
export function redisNodeTotalPages(total: number, pageSize: number): number {
  if (total <= 0 || pageSize <= 0) return 1;
  return Math.ceil(total / pageSize);
}

/** Redis 节点服务端查询：GET /Redis/RedisNode（分页） */
export async function fetchRedisNodeList(req: Req, f: RedisNodeFilter): Promise<RedisNodePageResult> {
  try {
    const res = await req({ url: '/Redis/RedisNode', method: 'GET', params: redisNodeFilterParams(f) });
    const rows = toRedisNodeRows(listOf(res));
    const page = (res as { page?: { totalCount?: unknown; total?: unknown } } | undefined)?.page;
    const total = num(page?.totalCount ?? page?.total) || rows.length;
    return { rows, total, error: '' };
  } catch (e) {
    return { rows: [], total: 0, error: errText(e, 'Redis 节点查询失败') };
  }
}

/** 编辑抽屉 Detail：GET /Redis/RedisNode/{id}；data 非 Rec（null/数组/异常）一律 null（抽屉回空表单） */
export async function fetchRedisNodeDetail(req: Req, id: number): Promise<Rec | null> {
  try {
    const res = await req({ url: `/Redis/RedisNode/${id}`, method: 'GET' });
    const data = (res as { data?: unknown } | undefined)?.data;
    return data && typeof data === 'object' && !Array.isArray(data) ? (data as Rec) : null;
  } catch {
    return null;
  }
}

/** Redis 节点表单（编辑抽屉）：业务字段全带（告警五项=实体字段，表单保留；列表已裁剪=列表口径） */
export interface RedisNodeForm {
  projectId: number;
  name: string;
  category: string;
  server: string;
  userName: string;
  password: string;
  version: string;
  mode: string;
  role: string;
  maxMemory: number;
  memoryPolicy: string;
  memoryAllocator: string;
  enable: boolean;
  scanQueue: boolean;
  webHook: string;
  alarmMemoryRate: number;
  alarmConnections: number;
  alarmSpeed: number;
  alarmInputKbps: number;
  alarmOutputKbps: number;
  remark: string;
}

/** Detail → 表单（Pascal/camel 双兼容；ScanQueue 缺省 true=实体默认「自动扫描发现消息队列，默认true」） */
export function nodeDetailToRedisForm(d: Rec): RedisNodeForm {
  return {
    projectId: num(pick(d, 'ProjectId', 'projectId')),
    name: str(pick(d, 'Name', 'name')),
    category: str(pick(d, 'Category', 'category')),
    server: str(pick(d, 'Server', 'server')),
    userName: str(pick(d, 'UserName', 'userName')),
    password: str(pick(d, 'Password', 'password')),
    version: str(pick(d, 'Version', 'version')),
    mode: str(pick(d, 'Mode', 'mode')),
    role: str(pick(d, 'Role', 'role')),
    maxMemory: num(pick(d, 'MaxMemory', 'maxMemory')),
    memoryPolicy: str(pick(d, 'MemoryPolicy', 'memoryPolicy')),
    memoryAllocator: str(pick(d, 'MemoryAllocator', 'memoryAllocator')),
    enable: boolOf(pick(d, 'Enable', 'enable')),
    scanQueue: pick(d, 'ScanQueue', 'scanQueue') === undefined ? true : boolOf(pick(d, 'ScanQueue', 'scanQueue')),
    webHook: str(pick(d, 'WebHook', 'webHook')),
    alarmMemoryRate: num(pick(d, 'AlarmMemoryRate', 'alarmMemoryRate')),
    alarmConnections: num(pick(d, 'AlarmConnections', 'alarmConnections')),
    alarmSpeed: num(pick(d, 'AlarmSpeed', 'alarmSpeed')),
    alarmInputKbps: num(pick(d, 'AlarmInputKbps', 'alarmInputKbps')),
    alarmOutputKbps: num(pick(d, 'AlarmOutputKbps', 'alarmOutputKbps')),
    remark: str(pick(d, 'Remark', 'remark')),
  };
}

/** 表单校验：Name 必填（Master 字段=展示键；Valid 无额外业务，其余交后端） */
export function validateRedisNodeForm(m: RedisNodeForm): { name?: string } {
  if (!m.name.trim()) return { name: '名称不能为空' };
  return {};
}

/** 提交体 = Detail 全量 + 表单覆盖（Pascal 键）；Id 与未入表单的审计字段原样保留（MW-02 先例语义） */
export function redisNodeUpdatePayload(d: Rec, m: RedisNodeForm): Rec {
  return {
    ...(d as Rec),
    ProjectId: m.projectId,
    Name: m.name,
    Category: m.category,
    Server: m.server,
    UserName: m.userName,
    Password: m.password,
    Version: m.version,
    Mode: m.mode,
    Role: m.role,
    MaxMemory: m.maxMemory,
    MemoryPolicy: m.memoryPolicy,
    MemoryAllocator: m.memoryAllocator,
    Enable: m.enable,
    ScanQueue: m.scanQueue,
    WebHook: m.webHook,
    AlarmMemoryRate: m.alarmMemoryRate,
    AlarmConnections: m.alarmConnections,
    AlarmSpeed: m.alarmSpeed,
    AlarmInputKbps: m.alarmInputKbps,
    AlarmOutputKbps: m.alarmOutputKbps,
    Remark: m.remark,
  };
}

/** 保存编辑：PUT /Redis/RedisNode/{id}（id<=0 明确失败不盲发）；成功 code===0（okOf） */
export async function submitRedisNode(req: Req, d: Rec, m: RedisNodeForm): Promise<{ ok: boolean; message: string }> {
  const id = num(pick(d, 'Id', 'id'));
  if (!(id > 0)) return { ok: false, message: '节点不存在或已被删除' };
  try {
    const res = await req({ url: `/Redis/RedisNode/${id}`, method: 'PUT', data: redisNodeUpdatePayload(d, m) } as ReqOptions & { data: Rec });
    return okOf(res as Rec, '保存失败');
  } catch (e) {
    return { ok: false, message: errText(e, '保存失败') };
  }
}

/** 手动刷新（C04）：卡 §3 契约 GET /Redis/RedisNode/Refresh?id=；成功 message 透传后端「刷新[node]成功！」供 §3.5 toast */
export async function refreshRedisNode(req: Req, id: number): Promise<{ ok: boolean; message: string }> {
  try {
    const res = await req({ url: '/Redis/RedisNode/Refresh', method: 'GET', params: { id } });
    return okOf(res, '刷新失败');
  } catch (e) {
    return { ok: false, message: errText(e, '刷新失败') };
  }
}

/** 新增节点：POST /Redis/RedisNode（标准实体接口；控制器无 RemoveCreateField，标准 CRUD 就绪=卡 §1） */
export async function createRedisNode(req: Req, m: RedisNodeForm): Promise<{ ok: boolean; message: string }> {
  try {
    const res = await req({ url: '/Redis/RedisNode', method: 'POST', data: redisNodeUpdatePayload({}, m) } as ReqOptions & { data: Rec });
    return okOf(res as Rec, '新增失败');
  } catch (e) {
    return { ok: false, message: errText(e, '新增失败') };
  }
}

/** 删除节点：DELETE /Redis/RedisNode/{id}（GuardModal 确认后调用） */
export async function deleteRedisNode(req: Req, id: number): Promise<{ ok: boolean; message: string }> {
  try {
    const res = await req({ url: `/Redis/RedisNode/${id}`, method: 'DELETE' });
    return okOf(res as Rec, '删除失败');
  } catch (e) {
    return { ok: false, message: errText(e, '删除失败') };
  }
}
