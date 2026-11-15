// 跟踪项（CARD-MON-12）纯逻辑。无 Vue 依赖，request 由调用方注入（与 appTracer.logic 同风格）。
// 真源（后端源码为准）：
//  - 控制器：Stardust.WebApi/Areas/Monitors/Controllers/TraceItemController.cs
//      Search :46-66 GET /Monitors/TraceItem?Id=&appId=&name=&kind=&enable=&dtStart=&dtEnd=&Q=（Pager 分页）；
//        Id>0 单条直查（URL Id 单条走同一 Search）；enable 可空（不传=不过滤）。
//      删除级联 Valid :68-75：Delete(post) 时 TraceDayStat.DeleteByAppAndItem（后端既有行为）。
//      Fix :78-98 / FixDisplay :101-138：SelectKeys 回算，JsonRefresh("成功！")。
//  - 批量四动作：GET /Monitors/TraceItem/{Fix|FixDisplay|EnableSelect|DisableSelect}?keys=（逗号串，
//    基类 SelectKeys=GetRequest("Keys")，与 AppTracer Fix 同范式）。
//  - GetKinds（本卡裁定 1 新增 action）：GET /Monitors/TraceItem/GetKinds → Cube 信封，
//    data=IDictionary<String,String>（跟踪项.Biz.cs:152 GetKinds()=_KindCache.FindAllName()，10 分钟 FieldCache）。
//  - 保存语义（裁定 2 核验=后端承载）：跟踪项.Biz.cs:45-47 Valid——Kind 空或 =="redismq" → GetKind(Name)；
//    Rules 非空且无 "=" → 前缀 name=。前端直传不改写（本文件 traceItemSaveBody 只做字段装配）。
//  - 实体：Stardust.Data/Monitors/跟踪项.cs——Id Int32（无雪花）但统一 string 传参；Kind=展示名；
//    Rules 注释「如name=*/check*,*/ping*;clientId=10.10.*」；Timeout 超时时间，0=不判超时。
//  - IU_TraceItem_AppId_Name 唯一索引：同一应用下操作名唯一（保存撞唯一索引由后端报错透出）。
import type { Req, ReqOptions } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

type Rec = Record<string, unknown>;

/** 批量动作枚举（后端 action 名） */
export type TraceItemAction = 'Fix' | 'FixDisplay' | 'EnableSelect' | 'DisableSelect';

export interface TraceItemRow {
  id: string;
  appId: number;
  kind: string;
  name: string;
  displayName: string;
  enable: boolean | null;
  rules: string;
  cloned: boolean | null;
  timeout: number;
  days: number;
  total: number;
  errors: number;
  updateUser: string;
  updateTime: string;
}

export interface TraceItemQuery {
  /** URL Id 单条直查（走同一 Search，后端 Id>0 分支） */
  id?: string;
  appId?: number;
  name?: string;
  kind?: string;
  /** 三态：true 启用 / false 停用 / null|undefined 不过滤 */
  enable?: boolean | null;
  dtStart?: string;
  dtEnd?: string;
  Q?: string;
  page?: number;
  pageSize?: number;
}

/** 查询参数组装（空值不出键；enable 仅在非 null/undefined 时出键） */
export function traceItemParams(q: TraceItemQuery): Rec {
  const p: Rec = { page: q.page ?? 1, pageSize: q.pageSize ?? 20 };
  if (q.id !== undefined && q.id !== null && String(q.id) !== '') p.Id = String(q.id);
  if (q.appId !== undefined && q.appId !== null && q.appId > 0) p.appId = q.appId;
  if (q.name) p.name = q.name;
  if (q.kind) p.kind = q.kind;
  if (q.enable !== undefined && q.enable !== null) p.enable = q.enable;
  if (q.dtStart) p.dtStart = q.dtStart;
  if (q.dtEnd) p.dtEnd = q.dtEnd;
  if (q.Q) p.Q = q.Q;
  return p;
}

function num(v: unknown): number {
  const n = Number(v);
  return Number.isFinite(n) ? n : 0;
}
function str(v: unknown): string {
  return v === undefined || v === null ? '' : String(v);
}
function boolOrNull(v: unknown): boolean | null {
  if (v === undefined || v === null || v === '') return null;
  if (typeof v === 'boolean') return v;
  if (typeof v === 'number') return v !== 0;
  const s = str(v).trim().toLowerCase();
  if (s === 'true' || s === '1') return true;
  if (s === 'false' || s === '0') return false;
  return null;
}
function pick(r: Rec, ...keys: string[]): unknown {
  for (const k of keys) {
    const v = r[k];
    if (v !== undefined && v !== null) return v;
  }
  return undefined;
}

/** 后端行 → 页面行（大小写双取；Id Int32 统一 string 传参口径） */
export function toTraceItemRow(r: Rec): TraceItemRow {
  const idv = pick(r, 'Id', 'id', 'ID');
  return {
    id: idv === undefined ? '' : String(idv),
    appId: num(pick(r, 'AppId', 'appId')),
    kind: str(pick(r, 'Kind', 'kind')),
    name: str(pick(r, 'Name', 'name')),
    displayName: str(pick(r, 'DisplayName', 'displayName')),
    enable: boolOrNull(pick(r, 'Enable', 'enable')),
    rules: str(pick(r, 'Rules', 'rules')),
    cloned: boolOrNull(pick(r, 'Cloned', 'cloned')),
    timeout: num(pick(r, 'Timeout', 'timeout')),
    days: num(pick(r, 'Days', 'days')),
    total: num(pick(r, 'Total', 'total')),
    errors: num(pick(r, 'Errors', 'errors')),
    updateUser: str(pick(r, 'UpdateUser', 'updateUser')),
    updateTime: str(pick(r, 'UpdateTime', 'updateTime')),
  };
}

/** SelectKeys 串（后端 GetRequest("Keys") 逗号串） */
export function selectKeysParam(ids: string[]): string {
  return ids.join(',');
}

/** 搜索：GET /Monitors/TraceItem，返回映射后的行（信封 data 数组） */
export async function fetchTraceItems(request: Req, q: TraceItemQuery = {}): Promise<TraceItemRow[]> {
  const res = (await request({ url: '/Monitors/TraceItem', method: 'GET', params: traceItemParams(q) })) as Rec;
  const list = Array.isArray(res?.data) ? res.data : [];
  return list.map((r) => toTraceItemRow(r as Rec));
}

export interface TraceItemKind {
  value: string;
  label: string;
}

/** 类别下拉数据源：GET /Monitors/TraceItem/GetKinds（后端词典 value=Kind 值 / label=展示名） */
export async function fetchTraceItemKinds(request: Req): Promise<TraceItemKind[]> {
  const res = (await request({ url: '/Monitors/TraceItem/GetKinds', method: 'GET' })) as Rec;
  const dict = res?.data;
  if (!dict || typeof dict !== 'object' || Array.isArray(dict)) return [];
  return Object.entries(dict as Rec).map(([value, label]) => ({ value, label: str(label) }));
}

/** 批量四动作：GET /Monitors/TraceItem/{action}?keys=…，返回 JsonRefresh 文案 */
export async function runTraceItemAction(request: Req, action: TraceItemAction, ids: string[]): Promise<string> {
  if (ids.length === 0) return '';
  const res = (await request({
    url: `/Monitors/TraceItem/${action}`,
    method: 'GET',
    params: { keys: selectKeysParam(ids) },
  })) as Rec;
  return typeof res?.message === 'string' ? res.message : '已提交';
}

/** 编辑表单模型（Name 必填；Kind/Rules/DisplayName/Enable/Cloned/Timeout 可编辑） */
export interface TraceItemEditModel {
  appId: number;
  name: string;
  kind: string;
  displayName: string;
  enable: boolean;
  cloned: boolean;
  timeout: number;
  rules: string;
}

/** 行 → 表单初值（appId 沿用宿主；编辑时 kind/rules 原样回显） */
export function toTraceItemEditModel(r: TraceItemRow, appId: number): TraceItemEditModel {
  return {
    appId: r.appId > 0 ? r.appId : appId,
    name: r.name,
    kind: r.kind,
    displayName: r.displayName,
    enable: r.enable !== false,
    cloned: r.cloned === true,
    timeout: r.timeout,
    rules: r.rules,
  };
}

/**
 * 表单 → 保存体。**只装配不改写**（裁定 2 核验：Kind 补齐与 Rules 前缀由后端 Valid 承载
 * ——跟踪项.Biz.cs:45-47；前端预改写会与后端语义双轨）。
 */
export function traceItemSaveBody(m: TraceItemEditModel): Rec {
  return {
    appId: m.appId,
    name: m.name,
    kind: m.kind,
    displayName: m.displayName,
    enable: m.enable,
    cloned: m.cloned,
    timeout: m.timeout,
    rules: m.rules,
  };
}

/** 保存：有 id → PUT /Monitors/TraceItem/{id}（主键在路径，body 部分更新）；无 → POST /Monitors/TraceItem */
export async function saveTraceItem(request: Req, m: TraceItemEditModel, id?: string): Promise<void> {
  if (id) {
    await request({ url: `/Monitors/TraceItem/${id}`, method: 'PUT', data: traceItemSaveBody(m) } as ReqOptions & { data: Rec });
  } else {
    await request({ url: '/Monitors/TraceItem', method: 'POST', data: traceItemSaveBody(m) } as ReqOptions & { data: Rec });
  }
}

/** 删除：DELETE /Monitors/TraceItem/{id}（后端级联清理 TraceDayStat） */
export async function deleteTraceItem(request: Req, id: string): Promise<void> {
  await request({ url: `/Monitors/TraceItem/${id}`, method: 'DELETE' });
}

/** 表单校验：Name 必填、超时 >=0；通过返回空串 */
export function validateTraceItemEdit(m: TraceItemEditModel): string {
  if (!m.name || !m.name.trim()) return '操作名不能为空';
  if (m.timeout < 0) return '超时时间不能为负数';
  return '';
}
