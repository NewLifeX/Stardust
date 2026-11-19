// 跟踪规则纯逻辑（CARD-MON-13，来源旅程 J-MON-15 / 盘点 Doc/MVC功能真源/监控调用链/TraceRule.md）。
// 无 Vue 依赖，request 由调用方注入，便于单测。
//
// 数据源（已读后端源码确认，2026-11-09；CRUD 主键形态同 alarmGroup.logic 先例）：
//  - 列表   GET /Monitors/TraceRule                （WebApi TraceRuleController.Search :45-53 只消费
//           dtStart/dtEnd/Q 三参数；isWhite/isRegex/enable 在实体层 Search 有重载但控制器未读取——
//           假筛选硬条款：前端不得提供这三项过滤，也不得在请求里带这三个参数制造生效假象）
//  - 新增   POST /Monitors/TraceRule               （WebEntityController Insert，主键不入 body）
//  - 编辑   PUT /Monitors/TraceRule/{id}           （EntityController.Update：主键在路径，body 部分更新）
//  - 删除   DELETE /Monitors/TraceRule/{id}        （EntityController.Delete，主键在路径）
//
// 实体字段（Stardust.Data/Monitors/跟踪规则.cs :24-121）：Id、Rule（200 长，规则串）、Enable、IsWhite、
// IsRegex、Priority（扩展，越大越在前面）、Remark、CreateUser/CreateTime/CreateIP、
// UpdateUser/UpdateUserID/UpdateTime/UpdateIP。列表隐藏创建类列与控制器 ListFields.RemoveCreateField()
// （TraceRuleController.cs:20）对齐——行内展开区仍保留创建信息。
//
// 匹配语义（C04 文档化，运行时在 Stardust.Server/Controllers/TraceController.cs :239-245,275，本卡不改后端）：
// GetValids 按 Priority 降序取启用且规则非空的规则，逐条 IsMatch——正则（区分大小写）或
// * 通配模糊（不区分大小写，同串相等也算命中）；命中白名单放行、命中黑名单丢弃 Span；均未命中默认放行。
// 空表首次触发实体操作时 InitData 自动插入 3 条默认白名单规则（^/Admin/[A-Z]\w+、^/Cube/[A-Z]\w+、
// /Sso/*，跟踪规则.Biz.cs :53-87，实体行为非页面动作）。
//
// 文案逐字（实体描述原文，前端不转写）：「跟踪规则。全局黑白名单，白名单放行，黑名单拦截」
// 「优先级。越大越在前面」——视图语义副题由这两句拼装；名单标签=白名单/黑名单，方式标签=正则/模糊。
import type { Req, ReqOptions } from '../../../src/views/Stardust/appsModel.ts';

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
/** 宽松解析时间字符串（兼容 'YYYY-MM-DD HH:mm:ss' 与 ISO），失败返回 —（同 alarmGroup.logic 兜底） */
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

/** 跟踪规则行（列表展示形状；创建信息归行内展开区） */
export interface TraceRuleRow {
  id: number;
  rule: string;
  enable: boolean;
  isWhite: boolean;
  isRegex: boolean;
  priority: number;
  remark: string;
  createUser: string;
  createTime: string;
  createIp: string;
  updateUser: string;
  updateTime: string;
  updateIp: string;
}

/** 新增/编辑表单可写字段集（源码核实：Rule/Enable/IsWhite/IsRegex/Priority/Remark） */
export interface TraceRuleForm {
  rule: string;
  enable: boolean;
  isWhite: boolean;
  isRegex: boolean;
  priority: number;
  remark: string;
}

// ---------- 归一 ----------

/** 一行接口数据 → TraceRuleRow；缺字段给防御性兜底，不抛错 */
export function toTraceRuleRow(r: Rec): TraceRuleRow {
  return {
    id: num(pick(r, 'Id', 'id')),
    rule: str(pick(r, 'Rule', 'rule')),
    enable: boolOf(pick(r, 'Enable', 'enable')),
    isWhite: boolOf(pick(r, 'IsWhite', 'isWhite')),
    isRegex: boolOf(pick(r, 'IsRegex', 'isRegex')),
    priority: num(pick(r, 'Priority', 'priority')),
    remark: str(pick(r, 'Remark', 'remark')),
    createUser: str(pick(r, 'CreateUser', 'createUser')),
    createTime: fmtTime(pick(r, 'CreateTime', 'createTime')),
    createIp: str(pick(r, 'CreateIP', 'createIP', 'createIp')),
    updateUser: str(pick(r, 'UpdateUser', 'updateUser')),
    updateTime: fmtTime(pick(r, 'UpdateTime', 'updateTime')),
    updateIp: str(pick(r, 'UpdateIP', 'updateIP', 'updateIp')),
  };
}

/** 表单初值（编辑=行回填；新增=空表单，默认启用+白名单——白名单放行是主要场景） */
export function emptyTraceRuleForm(): TraceRuleForm {
  return { rule: '', enable: true, isWhite: true, isRegex: false, priority: 0, remark: '' };
}

export function toTraceRuleForm(row: TraceRuleRow): TraceRuleForm {
  return {
    rule: row.rule,
    enable: row.enable,
    isWhite: row.isWhite,
    isRegex: row.isRegex,
    priority: row.priority,
    remark: row.remark,
  };
}

// ---------- 过滤 ----------

/**
 * 客户端过滤：q 匹配规则串/备注（不区分大小写）。跟踪规则是全局配置级小表，
 * 客户端过滤即可；服务端只消费 dtStart/dtEnd/Q（控制器 :45-53），UI 不提供
 * isWhite/isRegex/enable 过滤（假筛选硬条款）。
 */
export function filterTraceRuleRows(rows: TraceRuleRow[], q: string): TraceRuleRow[] {
  const key = q.trim().toLowerCase();
  if (!key) return rows;
  return rows.filter((r) => `${r.rule} ${r.remark}`.toLowerCase().includes(key));
}

// ---------- 校验 ----------

/** 表单校验：Rule 必填；优先级为不小于 0 的整数；通过返回空串 */
export function validateTraceRuleForm(form: TraceRuleForm): string {
  if (!form.rule.trim()) return '规则不能为空';
  if (!Number.isInteger(form.priority) || form.priority < 0) return '优先级需为不小于 0 的整数';
  return '';
}

// ---------- 参数与请求体 ----------

/** 编辑表单 → PUT/POST body（主键不入 body：Update 的主键在路径里） */
export function ruleBody(form: TraceRuleForm): Rec {
  return {
    rule: form.rule.trim(),
    enable: form.enable,
    isWhite: form.isWhite,
    isRegex: form.isRegex,
    priority: form.priority,
    remark: form.remark,
  };
}

// ---------- 取数与写操作（request 注入） ----------

/**
 * 跟踪规则列表：GET /Monitors/TraceRule（PageSize 200 客户端兜底）。
 * 假筛选硬条款：params 只带 PageSize——isWhite/isRegex/enable 控制器未消费，
 * dtStart/dtEnd/Q 本视图无对应控件，一律不带，防止「参数在但无效」的假象。
 */
export async function fetchTraceRules(request: Req, opts: { pageSize?: number } = {}): Promise<{ rows: TraceRuleRow[]; error: string }> {
  const params: Rec = { PageSize: opts.pageSize ?? 200 };
  try {
    const res = await request({ url: '/Monitors/TraceRule', method: 'GET', params });
    return { rows: listOf(res).map(toTraceRuleRow), error: '' };
  } catch (e) {
    return { rows: [], error: e instanceof Error && e.message ? e.message : '跟踪规则加载失败' };
  }
}

/** 保存（新增 POST / 编辑 PUT /{id}）：主键不入 body */
export async function saveTraceRule(request: Req, form: TraceRuleForm, id?: number): Promise<void> {
  if (id && id > 0) {
    await request({ url: `/Monitors/TraceRule/${id}`, method: 'PUT', data: ruleBody(form) } as ReqOptions & { data: Record<string, unknown> });
  } else {
    await request({ url: '/Monitors/TraceRule', method: 'POST', data: ruleBody(form) } as ReqOptions & { data: Record<string, unknown> });
  }
}

/** 删除：DELETE /Monitors/TraceRule/{id}（主键在路径） */
export async function deleteTraceRule(request: Req, id: number): Promise<void> {
  await request({ url: `/Monitors/TraceRule/${id}`, method: 'DELETE' });
}

// ---------- 标签文案（实体描述逐字） ----------

/** 名单标签：IsWhite 白名单（Tag success）/否则黑名单（Tag danger）；实体描述「白名单。否则是黑名单」 */
export function listLabel(isWhite: boolean): string {
  return isWhite ? '白名单' : '黑名单';
}
/** 方式标签：IsRegex 正则 / 否则模糊（* 通配）；实体描述「正则。是否使用正则表达式，此时区分大小写」 */
export function modeLabel(isRegex: boolean): string {
  return isRegex ? '正则' : '模糊';
}

// ---------- 文案常量（实现规范 §3.3 空态两模式） ----------

export const TRACE_RULE_EMPTY_TEXT = '还没有跟踪规则';
export const TRACE_RULE_EMPTY_CREATE_TEXT = '新建规则';
/** 有数据但过滤后无匹配的纯列表空态 */
export const TRACE_RULE_FILTER_EMPTY_TEXT = '暂无匹配的跟踪规则';
