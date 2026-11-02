// 应用工作台「监控中心」页签纯逻辑（F01）。无 Vue 依赖，request 由调用方注入。
// 真源（backend-dev 未广播 LOGIN-READY 前，字段以源码为准，登录后 curl 复核）：
//  - 字段：Stardust.Data/Monitors/应用跟踪器.cs（ID/ProjectId/AppId/Name/DisplayName/Category/
//    ItemCount/Days/Total/Enable/Mode/Period/MaxSamples/MaxErrors/EnableMeter/WhiteList/Excludes/
//    Timeout/MaxTagLength/RequestTagLength/VipClients/WebHook/AlarmThreshold/AlarmErrorRate/…/UpdateTime）
//  - 行为：Stardust.WebApi/Areas/Monitors/Controllers/AppTracerController.cs
//      Search 读 monitorId（别名）或 appId；appId 有效但无跟踪器时自动补建一条（不返回空列表）；
//      Fix/FixOldData：SelectKeys = GetRequest("Keys") 逗号串 → GET …/Fix?keys=1,2 返回 JsonRefresh 文案；
//      编辑 PUT /api/Monitors/AppTracer（体含 id，Cube 部分字段更新）；删除 DELETE /api/Monitors/AppTracer?id=。
// 跟踪模式枚举 TraceModes：0 跟踪所有项 / 1 仅新增跟踪项但不跟踪 / 2 仅跟踪已有项（应用跟踪器.Biz.cs）。
import type { Req, ReqOptions } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

type Rec = Record<string, unknown>;

/** 跟踪模式 → 文案（对齐枚举注释） */
export const TRACE_MODES: ReadonlyArray<{ value: number; label: string }> = [
  { value: 0, label: '跟踪所有项' },
  { value: 1, label: '仅新增跟踪项' },
  { value: 2, label: '仅跟踪已有项' },
];

export function traceModeLabel(mode: number): string {
  return TRACE_MODES.find((m) => m.value === mode)?.label ?? `模式${mode}`;
}

export interface TracerRow {
  id: number;
  appId: number;
  name: string;
  displayName: string;
  category: string;
  itemCount: number;
  days: number;
  total: number;
  enable: boolean | null;
  mode: number;
  period: number;
  maxSamples: number;
  maxErrors: number;
  enableMeter: boolean | null;
  whiteList: string;
  excludes: string;
  timeout: number;
  vipClients: string;
  webHook: string;
  alarmThreshold: number;
  alarmErrorRate: number;
  updateTime: string;
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

/** 后端行 → 页面行（大小写双取：Cube 实体序列化大小写待 curl 复核，双取最稳） */
export function toTracerRow(r: Rec): TracerRow {
  return {
    id: num(pick(r, 'Id', 'id', 'ID')),
    appId: num(pick(r, 'AppId', 'appId')),
    name: str(pick(r, 'Name', 'name')),
    displayName: str(pick(r, 'DisplayName', 'displayName')),
    category: str(pick(r, 'Category', 'category')),
    itemCount: num(pick(r, 'ItemCount', 'itemCount')),
    days: num(pick(r, 'Days', 'days')),
    total: num(pick(r, 'Total', 'total')),
    enable: boolOrNull(pick(r, 'Enable', 'enable')),
    mode: num(pick(r, 'Mode', 'mode')),
    period: num(pick(r, 'Period', 'period')),
    maxSamples: num(pick(r, 'MaxSamples', 'maxSamples')),
    maxErrors: num(pick(r, 'MaxErrors', 'maxErrors')),
    enableMeter: boolOrNull(pick(r, 'EnableMeter', 'enableMeter')),
    whiteList: str(pick(r, 'WhiteList', 'whiteList')),
    excludes: str(pick(r, 'Excludes', 'excludes')),
    timeout: num(pick(r, 'Timeout', 'timeout')),
    vipClients: str(pick(r, 'VipClients', 'vipClients')),
    webHook: str(pick(r, 'WebHook', 'webHook')),
    alarmThreshold: num(pick(r, 'AlarmThreshold', 'alarmThreshold')),
    alarmErrorRate: num(pick(r, 'AlarmErrorRate', 'alarmErrorRate')),
    updateTime: str(pick(r, 'UpdateTime', 'updateTime')),
  };
}

function listOf(res: unknown): Rec[] {
  if (Array.isArray(res)) return res as Rec[];
  if (res && typeof res === 'object' && Array.isArray((res as { data?: unknown }).data)) {
    return (res as { data: Rec[] }).data;
  }
  return [];
}

/** 拉取应用的跟踪器列表：GET /api/Monitors/AppTracer?appId= */
export async function fetchTracers(request: Req, appId: number): Promise<TracerRow[]> {
  const res = await request({ url: '/Monitors/AppTracer', method: 'GET', params: { appId, pageSize: 50 } });
  return listOf(res).map(toTracerRow);
}

/** 批量修正 Action：GET …/Fix（或 FixOldData）?keys=1,2。keys 为跟踪器 Id。返回 JsonRefresh 文案。 */
export async function fixTracers(request: Req, ids: number[], oldData = false): Promise<string> {
  if (ids.length === 0) return '';
  const res = (await request({
    url: oldData ? '/Monitors/AppTracer/FixOldData' : '/Monitors/AppTracer/Fix',
    method: 'GET',
    params: { keys: ids.join(',') },
  })) as Rec;
  return typeof res?.message === 'string' ? res.message : '已提交修正';
}

/** 编辑跟踪器可保存的字段（Name 是唯一键但列表页编辑不提供改名，避免误伤统计关联） */
export interface TracerEditModel {
  displayName: string;
  enable: boolean;
  mode: number;
  period: number;
  maxSamples: number;
  maxErrors: number;
  enableMeter: boolean;
  timeout: number;
  whiteList: string;
  excludes: string;
  vipClients: string;
  webHook: string;
  alarmThreshold: number;
  alarmErrorRate: number;
}

/** 行 → 编辑表单初值 */
export function toTracerEditModel(r: TracerRow): TracerEditModel {
  return {
    displayName: r.displayName,
    enable: r.enable !== false,
    mode: r.mode,
    period: r.period,
    maxSamples: r.maxSamples,
    maxErrors: r.maxErrors,
    enableMeter: r.enableMeter === true,
    timeout: r.timeout,
    whiteList: r.whiteList,
    excludes: r.excludes,
    vipClients: r.vipClients,
    webHook: r.webHook,
    alarmThreshold: r.alarmThreshold,
    alarmErrorRate: r.alarmErrorRate,
  };
}

/** 编辑表单 → PUT 体。id 不入 body：EntityController.Update 的主键在路径里（/api/Monitors/AppTracer/{id}），
 *  body 带 id 时必须与路径一致，干脆不带（CopySubmitted 跳过主键，未提交字段保持库中原值） */
export function tracerPutBody(m: TracerEditModel): Rec {
  return {
    displayName: m.displayName,
    enable: m.enable,
    mode: m.mode,
    period: m.period,
    maxSamples: m.maxSamples,
    maxErrors: m.maxErrors,
    enableMeter: m.enableMeter,
    timeout: m.timeout,
    whiteList: m.whiteList,
    excludes: m.excludes,
    vipClients: m.vipClients,
    webHook: m.webHook,
    alarmThreshold: m.alarmThreshold,
    alarmErrorRate: m.alarmErrorRate,
  };
}

/** 保存编辑：PUT /api/Monitors/AppTracer/{id}（EntityController.Update：主键在路径，body 部分更新） */
export async function saveTracer(request: Req, id: number, m: TracerEditModel): Promise<void> {
  await request({ url: `/Monitors/AppTracer/${id}`, method: 'PUT', data: tracerPutBody(m) } as ReqOptions & { data: Record<string, unknown> });
}

/** 删除跟踪器：DELETE /api/Monitors/AppTracer/{id}（EntityController.Delete 主键在路径） */
export async function deleteTracer(request: Req, id: number): Promise<void> {
  await request({ url: `/Monitors/AppTracer/${id}`, method: 'DELETE' });
}

/** 表单校验：采样周期与超时必须 ≥0；错误时返回提示文案，通过返回空串 */
export function validateTracerEdit(m: TracerEditModel): string {
  if (m.period < 0) return '采样周期不能为负数';
  if (m.timeout < 0) return '超时时间不能为负数';
  if (m.maxSamples < 0) return '正常数不能为负数';
  if (m.maxErrors < 0) return '异常数不能为负数';
  if (m.alarmThreshold < 0) return '告警阈值不能为负数';
  if (m.alarmErrorRate < 0) return '告警错误率不能为负数';
  return '';
}

/**
 * 空态判定与引导文案。
 * 后端 Search 在 appId 有效时没有跟踪器会自动补建一条，真正为空只发生在
 * 列表加载成功但 appId 无效（找不到应用）或后端旧行为；文案区分这两种情况。
 */
export function tracerEmptyState(rows: TracerRow[], appId: number, appName?: string): string {
  if (rows.length > 0) return '';
  if (appId <= 0) return '应用参数无效，无法确定监控对象。请从应用列表重新进入。';
  return `「${appName || `应用 #${appId}`}」还没有跟踪器。应用开启跟踪并上报监控数据后会自动创建，可点击右上角刷新重试。`;
}
