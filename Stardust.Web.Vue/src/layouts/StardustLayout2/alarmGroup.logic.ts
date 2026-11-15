// 告警组纯逻辑（CARD-MON-01，来源旅程 J-MON-01 / 定稿卡 CARD-MON-01）。无 Vue 依赖，request 由调用方注入，便于单测。
//
// 数据源（已读后端源码确认，2026-11-07；GET 实测 /api 前缀 200，CRUD 主键形态同 appTracer.logic 先例）：
//  - 列表   GET /Monitors/AlarmGroup?name=&enable=&Q=&pageSize=    （AlarmGroupController.Search :45-60；另支持 Id 精确与 dtStart/dtEnd——UI 未提供，保留为 URL 直达能力）
//  - 新增   POST /Monitors/AlarmGroup                              （WebEntityController Insert，主键不入 body）
//  - 编辑   PUT /Monitors/AlarmGroup/{id}                          （EntityController.Update：主键在路径，body 部分更新；CopySubmitted 跳过主键，未提交字段保持库中原值）
//  - 删除   DELETE /Monitors/AlarmGroup/{id}                       （EntityController.Delete，主键在路径）
//  - 测试   POST /Monitors/AlarmGroup/ExecuteNow?id={id}           （AlarmGroupController.cs :66-84；需 Update 权限；成功 JsonRefresh("OK",0)→message 文案；
//           失败 Json(500,"执行失败！"+原因)→request 抛错；实体不存在或未启用仍 OK 不发送——后端契约，前端不禁用停用组的测试按钮，只加 title 说明）
//  - 项目下拉 GET /Platform/GalaxyProject?Q=&pageSize=             （GalaxyProjectController.Search :47-61）
//
// 实体字段（告警组.cs :28-143）：Id、ProjectId（Map 映射 GalaxyProject.Name → ProjectName）、Name（Master，必填：告警组.Biz.cs :39，
// 空抛 ArgumentNullException）、Enable、WebHook、Content、CreateUser/CreateTime/CreateIP、UpdateUser/UpdateTime/UpdateIP。
// WebApi 无通用 GetFields HTTP 端点（实测 /api/Monitors/AlarmGroup/GetFields 404/500；PipelineWebhook 的 GetFields 是该控制器
// 自定义 Action，非框架端点）——表单字段集以控制器/实体源码为准：可写=ProjectId/Name/Enable/WebHook/Content。
//
// 逐字文案（后端原文，前端不转写）：测试成功 message 取后端（"OK"）；失败 message 取后端（"执行失败！"+原因）。
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
function boolOf(v: unknown): boolean {
  if (v === undefined || v === null) return false;
  if (typeof v === 'boolean') return v;
  if (typeof v === 'number') return v !== 0;
  const s = String(v).toLowerCase();
  return s === 'true' || s === '1';
}
function listOf(res: { data?: unknown } | undefined): Rec[] {
  const d = res?.data;
  return Array.isArray(d) ? (d as Rec[]) : [];
}
/** 宽松解析时间字符串（兼容 'YYYY-MM-DD HH:mm:ss' 与 ISO），失败返回 —（AlarmGroup 时间列由拦截器维护，理论非空，兜底防御） */
function fmtTime(v: unknown): string {
  const s = str(v);
  if (!s) return '—';
  const t = Date.parse(s.replace(' ', 'T'));
  if (!Number.isFinite(t)) return s;
  const d = new Date(t);
  const p = (n: number): string => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`;
}

// ---------- 类型 ----------

/** 告警组行（列表展示形状；创建/更新 IP 归行内展开区） */
export interface AlarmGroupRow {
  id: number;
  projectId: number;
  /** 映射 GalaxyProject.Name（后端 Map 列）；缺省展示 #ProjectId，不造数 */
  projectName: string;
  name: string;
  enable: boolean;
  webHook: string;
  content: string;
  createUser: string;
  createTime: string;
  createIp: string;
  updateUser: string;
  updateTime: string;
  updateIp: string;
}

/** 新增/编辑表单可写字段集（源码核实：ProjectId/Name/Enable/WebHook/Content） */
export interface AlarmGroupForm {
  name: string;
  /** 0=不绑定项目 */
  projectId: number;
  enable: boolean;
  webHook: string;
  content: string;
}

/** 项目下拉选项 */
export interface ProjectOption {
  id: number;
  name: string;
}

// ---------- 归一 ----------

/** 一行接口数据 → AlarmGroupRow；缺字段给防御性兜底，不抛错 */
export function toAlarmGroupRow(r: Rec): AlarmGroupRow {
  return {
    id: num(pick(r, 'Id', 'id')),
    projectId: num(pick(r, 'ProjectId', 'projectId')),
    projectName: str(pick(r, 'ProjectName', 'projectName')),
    name: str(pick(r, 'Name', 'name')) || '未命名告警组',
    enable: boolOf(pick(r, 'Enable', 'enable')),
    webHook: str(pick(r, 'WebHook', 'webHook')),
    content: str(pick(r, 'Content', 'content')),
    createUser: str(pick(r, 'CreateUser', 'createUser')),
    createTime: fmtTime(pick(r, 'CreateTime', 'createTime')),
    createIp: str(pick(r, 'CreateIP', 'createIP', 'createIp')),
    updateUser: str(pick(r, 'UpdateUser', 'updateUser')),
    updateTime: fmtTime(pick(r, 'UpdateTime', 'updateTime')),
    updateIp: str(pick(r, 'UpdateIP', 'updateIP', 'updateIp')),
  };
}

/** 表单初值（编辑=行回填；新增=空表单） */
export function emptyAlarmGroupForm(): AlarmGroupForm {
  return { name: '', projectId: 0, enable: true, webHook: '', content: '' };
}

export function toAlarmGroupForm(row: AlarmGroupRow): AlarmGroupForm {
  return {
    name: row.name === '未命名告警组' ? '' : row.name,
    projectId: row.projectId,
    enable: row.enable,
    webHook: row.webHook,
    content: row.content,
  };
}

// ---------- 过滤 ----------

/**
 * 客户端过滤：q 匹配名称/项目名（不区分大小写）；enableFilter ''=全部、'true'/'false'。
 * 数据量为配置级（告警组是少量配置行），客户端过滤即可；服务端 name/enable/Q 参数保留在 fetchAlarmGroups 签名上供直达。
 */
export function filterGroupRows(rows: AlarmGroupRow[], q: string, enableFilter: string): AlarmGroupRow[] {
  const key = q.trim().toLowerCase();
  return rows.filter((r) => {
    if (enableFilter === 'true' && !r.enable) return false;
    if (enableFilter === 'false' && r.enable) return false;
    if (key) {
      const hay = `${r.name} ${r.projectName}`.toLowerCase();
      if (!hay.includes(key)) return false;
    }
    return true;
  });
}

// ---------- 校验 ----------

/** 表单校验：Name 必填（后端 ArgumentNullException 同语义）；通过返回空串 */
export function validateAlarmGroupForm(form: AlarmGroupForm): string {
  if (!form.name.trim()) return '名称不能为空';
  return '';
}

// ---------- 参数与请求体 ----------

/** 编辑表单 → PUT/POST body（主键不入 body：Update 的主键在路径里） */
export function groupBody(form: AlarmGroupForm): Rec {
  return {
    name: form.name.trim(),
    projectId: form.projectId,
    enable: form.enable,
    webHook: form.webHook.trim(),
    content: form.content,
  };
}

// ---------- 取数与写操作（request 注入） ----------

/**
 * 告警组列表：GET /Monitors/AlarmGroup（PageSize 200 客户端兜底）。
 * 服务端过滤参数保留（name/enable/Q），UI 当前用客户端过滤（filterGroupRows）。
 */
export async function fetchAlarmGroups(
  request: Req,
  opts: { name?: string; enable?: boolean | ''; Q?: string; pageSize?: number } = {},
): Promise<{ rows: AlarmGroupRow[]; error: string }> {
  const params: Rec = { PageSize: opts.pageSize ?? 200 };
  if (opts.name) params.name = opts.name;
  if (opts.enable === true || opts.enable === false) params.enable = opts.enable;
  if (opts.Q) params.Q = opts.Q;
  try {
    const res = await request({ url: '/Monitors/AlarmGroup', method: 'GET', params });
    return { rows: listOf(res).map(toAlarmGroupRow), error: '' };
  } catch (e) {
    return { rows: [], error: e instanceof Error && e.message ? e.message : '告警组加载失败' };
  }
}

/** 项目下拉数据源：GET /Platform/GalaxyProject?Q=&pageSize=200 */
export async function fetchProjectOptions(request: Req, q = ''): Promise<{ options: ProjectOption[]; error: string }> {
  const params: Rec = { pageSize: 200 };
  if (q) params.Q = q;
  try {
    const res = await request({ url: '/Platform/GalaxyProject', method: 'GET', params });
    const options = listOf(res).map((r) => ({
      id: num(pick(r, 'Id', 'id')),
      name: str(pick(r, 'Name', 'name')) || `#${num(pick(r, 'Id', 'id'))}`,
    }));
    return { options, error: '' };
  } catch (e) {
    return { options: [], error: e instanceof Error && e.message ? e.message : '项目列表加载失败' };
  }
}

/** 保存（新增 POST / 编辑 PUT /{id}）：主键不入 body */
export async function saveAlarmGroup(request: Req, form: AlarmGroupForm, id?: number): Promise<void> {
  if (id && id > 0) {
    await request({ url: `/Monitors/AlarmGroup/${id}`, method: 'PUT', data: groupBody(form) } as ReqOptions & { data: Record<string, unknown> });
  } else {
    await request({ url: '/Monitors/AlarmGroup', method: 'POST', data: groupBody(form) } as ReqOptions & { data: Record<string, unknown> });
  }
}

/** 删除：DELETE /Monitors/AlarmGroup/{id}（主键在路径） */
export async function deleteAlarmGroup(request: Req, id: number): Promise<void> {
  await request({ url: `/Monitors/AlarmGroup/${id}`, method: 'DELETE' });
}

/**
 * 测试发送：POST /Monitors/AlarmGroup/ExecuteNow?id={id}。
 * 成功（含未启用/不存在 OK 不发送的后端契约）→ ok=true，message 取后端（"OK"）；
 * 失败 → ok=false，message 取后端原文（"执行失败！"+原因）。
 */
export async function executeAlarmTest(request: Req, id: number): Promise<{ ok: boolean; message: string }> {
  try {
    const res = (await request({
      url: '/Monitors/AlarmGroup/ExecuteNow',
      method: 'POST',
      params: { id },
    })) as Rec;
    const msg = typeof res?.message === 'string' && res.message ? res.message : 'OK';
    return { ok: true, message: msg };
  } catch (e) {
    return { ok: false, message: e instanceof Error && e.message ? e.message : '执行失败！未知原因' };
  }
}

/** 停用组「测试」按钮的 title 逐字文案（CARD-MON-01 定稿裁定 4：不禁用 + title 补说明） */
export const DISABLED_GROUP_TEST_TITLE = '未启用的组不会真实发送（后端直接返回 OK）';

/** 告警组空态引导文案（实现规范 §3.3 两模式；CARD-MON-01 定稿） */
export const GROUP_EMPTY_TEXT = '还没有告警组';
export const GROUP_EMPTY_CREATE_TEXT = '创建告警组';
/** 有数据但过滤后无匹配的纯列表空态 */
export const GROUP_FILTER_EMPTY_TEXT = '暂无告警组记录';
