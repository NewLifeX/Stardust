// 项目成员（ProjectUser）写操作与令牌表单的成员过滤。
//
// 接口（Cube 6.15 EntityController：动词打在控制器上，主键不进路径。
// 路径多出来的段会被当成 action，PUT/DELETE /{id} 在现网返回 405）：
//  - 列表  GET    /Platform/ProjectUser?projectId=&userId=&enable=&PageSize=
//  - 新增  POST   /Platform/ProjectUser          { projectId, userId, enable }
//  - 启停  PUT    /Platform/ProjectUser          { id, enable }   部分更新，未提交字段保持原值
//  - 移除  DELETE /Platform/ProjectUser?id=
//  - 用户候选 GET /Admin/User
//
// 令牌绑定：先选项目，再只列出该项目 Enable=true 的成员；UserId=0「未绑定」由调用方另留。
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
function listOf(res: { data?: unknown } | undefined): Rec[] {
  const d = res?.data;
  return Array.isArray(d) ? (d as Rec[]) : [];
}
function errText(e: unknown, fallback: string): string {
  return e instanceof Error && e.message ? e.message : fallback;
}

/** 刷新/提交再快也至少转这么久，用户才看得见（AGENTS.md §11） */
export const MEMBER_FEEDBACK_MS = 300;

export function feedbackRemainMs(elapsed: number, min = MEMBER_FEEDBACK_MS): number {
  if (!Number.isFinite(elapsed) || elapsed < 0) return min;
  return Math.max(0, min - elapsed);
}

export interface UserOption {
  id: number;
  name: string;
}

/** ProjectUser 关系行。enable 缺省视为启用（旧数据未带该字段时不误判停用）。 */
export interface MemberLink {
  id: number;
  projectId: number;
  userId: number;
  enable: boolean;
  userName: string;
}

export interface MemberOpResult {
  ok: boolean;
  message: string;
  id?: number;
}

function enableOf(v: unknown): boolean {
  if (v === undefined || v === null || v === '') return true;
  if (typeof v === 'boolean') return v;
  if (typeof v === 'number') return v !== 0;
  const s = str(v).toLowerCase();
  return s === 'true' || s === '1';
}

function toLink(r: Rec): MemberLink {
  const userId = num(pick(r, 'UserId', 'userId'));
  const userName = str(pick(r, 'UserName', 'userName', 'Name', 'name', 'DisplayName', 'displayName'));
  return {
    id: num(pick(r, 'Id', 'id')),
    projectId: num(pick(r, 'ProjectId', 'projectId')),
    userId,
    enable: enableOf(pick(r, 'Enable', 'enable')),
    userName: userName || (userId > 0 ? `#${userId}` : ''),
  };
}

function toUser(r: Rec): UserOption | null {
  const id = num(pick(r, 'Id', 'id', 'ID'));
  if (id <= 0) return null;
  const name = str(pick(r, 'DisplayName', 'displayName', 'Name', 'name', 'UserName', 'userName')) || `#${id}`;
  return { id, name };
}

/** 某项目的启用成员，供令牌表单用户下拉。未选项目时返回空（调用方只留「未绑定」）。 */
export function memberUserOptions(projectId: number, users: UserOption[], members: MemberLink[]): UserOption[] {
  if (projectId <= 0) return [];
  const names = new Map(users.map((u) => [u.id, u.name]));
  const seen = new Set<number>();
  const out: UserOption[] = [];
  for (const m of members) {
    if (m.projectId !== projectId || !m.enable || m.userId <= 0 || seen.has(m.userId)) continue;
    seen.add(m.userId);
    const fromList = names.get(m.userId);
    const name = fromList && fromList !== `#${m.userId}` ? fromList : (m.userName || fromList || `#${m.userId}`);
    out.push({ id: m.userId, name });
  }
  out.sort((a, b) => a.name.localeCompare(b.name, 'zh'));
  return out;
}

/** 添加成员时的候选：已经在当前列表里的用户不再出现。 */
export function candidateUsers(users: UserOption[], members: { userId: number }[]): UserOption[] {
  const taken = new Set(members.map((m) => m.userId));
  return users.filter((u) => u.id > 0 && !taken.has(u.id));
}

/** 添加按钮禁用原因。空串表示可以提交。 */
export function addMemberBlockReason(
  projectId: number | null | undefined,
  userId: number,
  members: { userId: number }[],
): string {
  if (projectId == null || projectId <= 0) return '请先在顶栏选择一个具体项目';
  if (userId <= 0) return '请选择要加入的用户';
  if (members.some((m) => m.userId === userId)) return '该用户已是当前项目成员';
  return '';
}

/**
 * 令牌保存前与 McpGuard.NeedProject 对齐。
 * UserId=0（未绑定）不拦。membersReady 为 false 时只提示正在核对，避免空列表误判。
 */
export function memberSaveBlock(
  projectId: number,
  userId: number,
  members: MemberLink[],
  membersReady: boolean,
): string {
  if (userId <= 0) return '';
  if (projectId <= 0) return '绑定了用户就必须先选择项目';
  if (!membersReady) return '正在核对项目成员…';
  const hit = members.some((m) => m.projectId === projectId && m.userId === userId && m.enable);
  if (!hit) return '该用户不是所选项目的启用成员，不能保存。请先到平台设置添加成员，或改为「未绑定」。';
  return '';
}

export async function fetchMemberLinks(request: Req, projectId?: number | null): Promise<{ rows: MemberLink[]; error: string }> {
  try {
    const params: Record<string, unknown> = { PageSize: 500 };
    if (projectId != null && projectId > 0) params.projectId = projectId;
    const res = await request({ url: '/Platform/ProjectUser', method: 'GET', params });
    return { rows: listOf(res).map(toLink), error: '' };
  } catch (e) {
    return { rows: [], error: errText(e, '项目成员加载失败') };
  }
}

export async function fetchUserOptions(request: Req): Promise<{ users: UserOption[]; error: string }> {
  try {
    const res = await request({ url: '/Admin/User', method: 'GET', params: { PageSize: 500 } });
    const users: UserOption[] = [];
    for (const row of listOf(res)) {
      const u = toUser(row);
      if (u) users.push(u);
    }
    return { users, error: '' };
  } catch (e) {
    return { users: [], error: errText(e, '用户列表加载失败') };
  }
}

export async function addMember(
  request: Req,
  input: { projectId: number; userId: number; enable: boolean },
): Promise<MemberOpResult> {
  if (input.projectId <= 0) return { ok: false, message: '请先在顶栏选择一个具体项目' };
  if (input.userId <= 0) return { ok: false, message: '请选择要加入的用户' };
  try {
    const res = await request({
      url: '/Platform/ProjectUser',
      method: 'POST',
      data: { projectId: input.projectId, userId: input.userId, enable: input.enable },
    } as ReqOptions & { data: Rec });
    const r = okRes(res);
    const id = num(pick((r.data ?? {}) as Rec, 'Id', 'id'));
    if (!r.ok) return { ok: false, message: r.message || `添加成员失败（用户 ${input.userId}，项目 ${input.projectId}）` };
    return { ok: true, message: `已把用户 ${input.userId} 加入项目 ${input.projectId}`, id };
  } catch (e) {
    return { ok: false, message: `添加成员失败（用户 ${input.userId}，项目 ${input.projectId}）：${errText(e, '请求失败')}` };
  }
}

export async function removeMember(request: Req, id: number, label = ''): Promise<MemberOpResult> {
  if (!id || id <= 0) return { ok: false, message: '缺少成员关系编号' };
  const who = label || `#${id}`;
  try {
    const res = await request({ url: '/Platform/ProjectUser', method: 'DELETE', params: { id } });
    const r = okRes(res);
    if (!r.ok) return { ok: false, message: r.message || `移除成员 ${who} 失败` };
    return { ok: true, message: `已移除成员 ${who}` };
  } catch (e) {
    return { ok: false, message: `移除成员 ${who} 失败：${errText(e, '请求失败')}` };
  }
}

export async function setMemberEnable(request: Req, id: number, enable: boolean, label = ''): Promise<MemberOpResult> {
  if (!id || id <= 0) return { ok: false, message: '缺少成员关系编号' };
  const who = label || `#${id}`;
  const verb = enable ? '启用' : '停用';
  try {
    const res = await request({
      url: '/Platform/ProjectUser',
      method: 'PUT',
      data: { id, enable },
    } as ReqOptions & { data: Rec });
    const r = okRes(res);
    if (!r.ok) return { ok: false, message: r.message || `${verb}成员 ${who} 失败` };
    return { ok: true, message: `已${verb}成员 ${who}` };
  } catch (e) {
    return { ok: false, message: `${verb}成员 ${who} 失败：${errText(e, '请求失败')}` };
  }
}
