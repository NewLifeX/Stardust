// 星系项目（GalaxyProject）管理：行归一、表单校验、请求体构建、筛选排序、删除后上下文回退判断。
//
// 接口契约（CARD-PLT-01 定稿；2026-11-07 04:06 对 6681 实测冒烟三发全过：
// POST 不带 Id、PUT /{id} 主键在路径部分更新、DELETE /{id} 主键在路径——
// 与 Cube EntityController.cs:29/106/195 源码一致；projectMember.logic 头注释的
// 「主键不进路径」描述的是 ProjectUser 控制器历史形态，不适用于本控制器）：
//  - 列表  GET    /api/Platform/GalaxyProject?PageSize=500（fetchPlatform 已拉全量，本模块零新增列表请求）
//  - 新增  POST   /api/Platform/GalaxyProject        body 不带 Id：{ name, enable, isGlobal, tenantId, managerId, sort, whiteIPs, blackIPs, newServer, remark }
//  - 编辑  PUT    /api/Platform/GalaxyProject/{id}   部分更新：只覆盖请求 JSON 顶层字段（GetSubmittedFields）；本卡提交表单整包（等价）
//  - 删除  DELETE /api/Platform/GalaxyProject/{id}
//
// 文案逐字来源：旧壳 apps/stardust/src/views/Platform/GalaxyProject/index.vue
// （「新建项目」:105、「请输入项目名称」:32、抽屉标题「新建项目/编辑项目」:214）；
// 「项目名称（唯一）」placeholder 不照抄——后端 Valid 无唯一校验（星系项目.Biz.cs:33-39），定稿裁定 2 用「项目名称」。
import { okRes } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import type { Req, ReqOptions } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import { feedbackRemainMs } from './projectMember.logic.ts';

export { feedbackRemainMs };
export const PROJECT_FEEDBACK_MS = 300;

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
function boolOf(v: unknown, fallback: boolean): boolean {
  if (v === undefined || v === null || v === '') return fallback;
  if (typeof v === 'boolean') return v;
  if (typeof v === 'number') return v !== 0;
  const s = str(v).toLowerCase();
  return s === 'true' || s === '1';
}
function errText(e: unknown, fallback: string): string {
  return e instanceof Error && e.message ? e.message : fallback;
}

/** 'YYYY-MM-DD HH:mm:ss' / ISO 串 → epoch ms；解析失败返回 0 */
export function toMs(v: unknown): number {
  const s = str(v);
  if (!s) return 0;
  const t = Date.parse(s.includes('T') ? s : s.replace(' ', 'T'));
  return Number.isFinite(t) ? t : 0;
}

/** epoch ms → 'YYYY-MM-DD HH:mm'；取不到显示 — */
export function fmtTime(ms: number): string {
  if (!ms) return '—';
  const d = new Date(ms);
  const p = (n: number, w = 2): string => String(n).padStart(w, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`;
}

/** GalaxyProject 列表行 → 项目管理表行。TenantName/ManagerName 是后端 Map 字段，缺省退化为 — 由 UI 兜底 */
export interface ProjectRow {
  id: number;
  name: string;
  enable: boolean;
  isGlobal: boolean;
  tenantId: number;
  tenantName: string;
  managerId: number;
  managerName: string;
  nodes: number;
  apps: number;
  sort: number;
  whiteIPs: string;
  blackIPs: string;
  newServer: string;
  remark: string;
  createdMs: number;
  createdText: string;
}

/** 行归一；默认排序 Sort 降序（盘点 §2 / 后端 Search 默认，`GalaxyProjectController.cs:54-58`；同 Sort 保持输入序） */
export function toProjectRows(rows: Rec[]): ProjectRow[] {
  const out = rows.map((r): ProjectRow => {
    const createdMs = toMs(pick(r, 'CreateTime', 'createTime'));
    return {
      id: num(pick(r, 'Id', 'id')),
      name: str(pick(r, 'Name', 'name')) || (num(pick(r, 'Id', 'id')) > 0 ? `#${num(pick(r, 'Id', 'id'))}` : '—'),
      enable: boolOf(pick(r, 'Enable', 'enable'), true),
      isGlobal: boolOf(pick(r, 'IsGlobal', 'isGlobal'), false),
      tenantId: num(pick(r, 'TenantId', 'tenantId')),
      tenantName: str(pick(r, 'TenantName', 'tenantName')),
      managerId: num(pick(r, 'ManagerId', 'managerId')),
      managerName: str(pick(r, 'ManagerName', 'managerName')),
      nodes: num(pick(r, 'Nodes', 'nodes')),
      apps: num(pick(r, 'Apps', 'apps')),
      sort: num(pick(r, 'Sort', 'sort')),
      whiteIPs: str(pick(r, 'WhiteIPs', 'whiteIPs')),
      blackIPs: str(pick(r, 'BlackIPs', 'blackIPs')),
      newServer: str(pick(r, 'NewServer', 'newServer')),
      remark: str(pick(r, 'Remark', 'remark')),
      createdMs,
      createdText: fmtTime(createdMs),
    };
  });
  return out.sort((a, b) => b.sort - a.sort);
}

/** 表单模型（抽屉 <-> 请求体之间的一致形态）。数字字段已归一，字符串已 trim */
export interface ProjectFormModel {
  name: string;
  enable: boolean;
  isGlobal: boolean;
  tenantId: number;
  managerId: number;
  sort: number;
  whiteIPs: string;
  blackIPs: string;
  newServer: string;
  remark: string;
}

export function emptyProjectForm(): ProjectFormModel {
  return { name: '', enable: true, isGlobal: false, tenantId: 0, managerId: 0, sort: 0, whiteIPs: '', blackIPs: '', newServer: '', remark: '' };
}

/** 编辑回显：ProjectRow → 表单模型（节点数/应用数为后端统计字段，不进表单） */
export function projectToForm(row: ProjectRow): ProjectFormModel {
  return {
    name: row.name.startsWith('#') ? '' : row.name,
    enable: row.enable,
    isGlobal: row.isGlobal,
    tenantId: row.tenantId,
    managerId: row.managerId,
    sort: row.sort,
    whiteIPs: row.whiteIPs,
    blackIPs: row.blackIPs,
    newServer: row.newServer,
    remark: row.remark,
  };
}

/** 名称必填：空返回「请输入项目名称」（旧壳 :32 逐字），否则空串表示可提交 */
export function validateProjectName(name: string): string {
  return str(name) ? '' : '请输入项目名称';
}

function digitsOf(v: unknown): number {
  const n = Number(v);
  return Number.isFinite(n) && n > 0 ? Math.floor(n) : 0;
}

/** POST 请求体：不带 Id（冒烟 2026-11-07 实证，小写驼峰字段名可识别） */
export function buildCreatePayload(m: ProjectFormModel): Rec {
  return {
    name: str(m.name),
    enable: m.enable,
    isGlobal: m.isGlobal,
    tenantId: digitsOf(m.tenantId),
    managerId: digitsOf(m.managerId),
    sort: digitsOf(m.sort),
    whiteIPs: str(m.whiteIPs),
    blackIPs: str(m.blackIPs),
    newServer: str(m.newServer),
    remark: str(m.remark),
  };
}

/** PUT 请求体：表单整包（Id 在路径；GetSubmittedFields 只认顶层字段，整包与部分更新等价——冒烟实证） */
export function buildUpdatePayload(m: ProjectFormModel): Rec {
  return buildCreatePayload(m);
}

export interface ProjectOpResult {
  ok: boolean;
  message: string;
  id?: number;
}

export async function createProject(request: Req, m: ProjectFormModel): Promise<ProjectOpResult> {
  const invalid = validateProjectName(m.name);
  if (invalid) return { ok: false, message: invalid };
  try {
    const res = await request({ url: '/Platform/GalaxyProject', method: 'POST', data: buildCreatePayload(m) } as ReqOptions & { data: Rec });
    const r = okRes(res);
    const id = num(pick((r.data ?? {}) as Rec, 'Id', 'id'));
    // 成功 toast 用定稿 §3.3 固定文案（后端「添加成功！」不照抄——与全仓 toast 含对象风格统一；失败才透传后端 message）
    if (!r.ok) return { ok: false, message: r.message || '创建项目失败' };
    return { ok: true, message: '项目已创建', id };
  } catch (e) {
    return { ok: false, message: `创建项目失败：${errText(e, '请求失败')}` };
  }
}

export async function updateProject(request: Req, id: number, m: ProjectFormModel): Promise<ProjectOpResult> {
  if (!id || id <= 0) return { ok: false, message: '缺少项目编号' };
  const invalid = validateProjectName(m.name);
  if (invalid) return { ok: false, message: invalid };
  try {
    const res = await request({ url: `/Platform/GalaxyProject/${id}`, method: 'PUT', data: buildUpdatePayload(m) } as ReqOptions & { data: Rec });
    const r = okRes(res);
    if (!r.ok) return { ok: false, message: r.message || '保存项目失败' };
    return { ok: true, message: '项目已保存' };
  } catch (e) {
    return { ok: false, message: `保存项目失败：${errText(e, '请求失败')}` };
  }
}

export async function deleteProject(request: Req, id: number, label = ''): Promise<ProjectOpResult> {
  if (!id || id <= 0) return { ok: false, message: '缺少项目编号' };
  const who = label || `#${id}`;
  try {
    const res = await request({ url: `/Platform/GalaxyProject/${id}`, method: 'DELETE' });
    const r = okRes(res);
    if (!r.ok) return { ok: false, message: r.message || `删除项目 ${who} 失败` };
    return { ok: true, message: `已删除项目 ${who}` };
  } catch (e) {
    return { ok: false, message: `删除项目 ${who} 失败：${errText(e, '请求失败')}` };
  }
}

/** 筛选条件；时间为 'YYYY-MM-DD'（date input），空表示不限 */
export interface ProjectFilter {
  keyword: string;
  dtStart: string;
  dtEnd: string;
}

/** 本地筛选：关键字命中名称/租户/管理者（大小写不敏感），时间区间按创建时间（含当日首尾）；不改变 Sort 降序 */
export function filterProjectRows(rows: ProjectRow[], f: ProjectFilter): ProjectRow[] {
  const kw = str(f.keyword).toLowerCase();
  const startMs = f.dtStart ? toMs(`${f.dtStart} 00:00:00`) : 0;
  const endMs = f.dtEnd ? toMs(`${f.dtEnd} 23:59:59`) : 0;
  return rows.filter((r) => {
    if (kw && ![r.name, r.tenantName, r.managerName].some((s) => s.toLowerCase().includes(kw))) return false;
    if (startMs && (!r.createdMs || r.createdMs < startMs)) return false;
    if (endMs && (!r.createdMs || r.createdMs > endMs)) return false;
    return true;
  });
}

/** 删除后的上下文回退：删除的是当前顶栏所选项目 → 切回「全部项目」（定稿联动断言 C13） */
export function contextAfterDelete(deletedId: number, currentProjectId: number | null | undefined, currentName: string): { switched: boolean; toast: string } {
  if (currentProjectId != null && deletedId === currentProjectId) {
    return { switched: true, toast: '已删除当前项目，已切回「全部项目」' };
  }
  void currentName;
  return { switched: false, toast: '' };
}
