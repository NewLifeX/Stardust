// 配置页签纯逻辑（无 Vue 依赖，request 由调用方注入，便于单测）。
// 数据源（已读后端控制器与实体确认）：
//  - 应用配置：GET /Configs/AppConfig?appId=        （AppConfigController.Search 支持 appId；
//      该应用没有配置记录时服务端会自动建一条，所以一般总能取到）
//  - 配置键值：GET /Configs/ConfigData?configId=    （ConfigDataController.Search 支持 configId）
//  - 配置历史：GET /Configs/ConfigHistory?configId=
//  - 发布：POST /Configs/AppConfig/Publish?configId=  （ConfigService.Publish，热更新通知在线实例）
//  - 回滚：POST /Configs/ConfigHistory/Rollback?id=    （仅 Action=Publish 的历史可回滚）
// 在线实例数：GET /Registry/AppOnline?appId=
//
// 后端草稿模型（ConfigData 实体 + ConfigDataController.OnUpdate/OnDelete，2026-11-03 回读源码）：
// Value 是生效值，NewValue 是草稿值；Version 是生效版本，NewVersion 是草稿版本；
// NewStatus 为 [[Deleted]] 表示待删除。AppConfig.Version < NextVersion 即有待发布。取数失败返回 null/0，
// 由页面显示「加载失败」，不造数据。
//
// 写入语义（源码 ConfigDataController.OnUpdate/OnDelete）：
//  - 已发布行（Version>0）：PUT 改 Key/Scope/Value 会被后端拒绝（400 文案），只能写 NewValue 草稿
//    （后端自动 AcquireNewVersion）；Enable 翻转会转成 NewStatus 启用/禁用草稿——因此 body 里
//    Enable 只在值变化时才提交，避免无谓的 NewStatus 变更；
//  - 未发布行（Version==0）：Key/Scope/Value 可直接改；
//  - DELETE：未发布真删；已发布不删行，改标 NewStatus=[[Deleted]]（发布后生效）。
// 放弃草稿（后端没有丢弃草稿的接口）、推送进度（Publish 只返回通知的应用数）仍标待接入。
import { okRes } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import type { Req, ReqOptions } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import { requestFailText } from './deploy.logic.ts';

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
  return v === undefined || v === null ? '' : String(v);
}
function bool(v: unknown): boolean {
  if (typeof v === 'boolean') return v;
  if (typeof v === 'number') return v !== 0;
  const s = str(v).trim().toLowerCase();
  return s === 'true' || s === '1';
}
function listOf(res: { data?: unknown } | undefined): Rec[] {
  const d = res?.data;
  return Array.isArray(d) ? (d as Rec[]) : [];
}

// ---------- 类型 ----------

/** 应用配置（AppConfig 行） */
export interface ConfigApp {
  /** AppConfig.Id，后续 ConfigData/ConfigHistory 按它过滤 */
  id: number;
  appId: number;
  name: string;
  /** 当前生效版本 */
  version: number;
  /** 草稿版本；> version 表示有待发布 */
  nextVersion: number;
  enable: boolean;
}

export type ConfigItemState = 'published' | 'modified' | 'added' | 'deleted';

/** 配置键值行（ConfigData） */
export interface ConfigItem {
  id: number;
  key: string;
  scope: string;
  /** 生效值（Value） */
  value: string;
  /** 草稿值（NewValue），无草稿时与 value 相同 */
  newValue: string;
  /** 启用状态（Enable；已发布行的启用变更走 NewStatus 草稿） */
  enable: boolean;
  state: ConfigItemState;
  /** 有待发布改动（newVersion > 生效版本） */
  pending: boolean;
}

/** 配置历史行（ConfigHistory） */
export interface ConfigHistoryItem {
  id: number;
  /** Publish / Rollback / Insert / Update / Delete */
  action: string;
  success: boolean;
  /** 快照 Remark 里的版本号（NextVersion 兜底 Version），解析失败为 0 */
  version: number;
  time: string;
}

// ---------- 纯推导 ----------

/** ConfigData.NewStatus 常量（配置数据.Biz.cs）：[[Deleted]] / [[Enabled]] / [[Disabled]] */
export const NEW_STATUS_DELETED = '[[Deleted]]';

/** 推导管行状态：待删除 > 有待发布改动（原行已发布=modified，新行=added）> 已发布 */
export function configItemState(row: Rec, publishedVersion: number): ConfigItemState {
  const newStatus = str(pick(row, 'NewStatus', 'newStatus')).trim();
  if (newStatus && newStatus.toLowerCase() === NEW_STATUS_DELETED.toLowerCase()) return 'deleted';
  const newVersion = num(pick(row, 'NewVersion', 'newVersion'));
  if (newVersion > publishedVersion) return num(pick(row, 'Version', 'version')) > 0 ? 'modified' : 'added';
  return 'published';
}

/** 有待发布改动的行 */
export function filterPending(items: ConfigItem[]): ConfigItem[] {
  return items.filter((i) => i.pending);
}

/** 历史行 Remark 是 AppConfig 的 JSON 快照；取这次发布引入的版本号（NextVersion 兜底 Version） */
export function historyVersionOf(row: Rec): number {
  const remark = str(pick(row, 'Remark', 'remark'));
  if (!remark) return 0;
  try {
    const o = JSON.parse(remark) as Rec;
    return num(pick(o, 'NextVersion', 'nextVersion')) || num(pick(o, 'Version', 'version'));
  } catch {
    return 0;
  }
}

// ---------- 取数 ----------

export async function fetchConfigApp(request: Req, appId: number): Promise<ConfigApp | null> {
  if (!appId || appId <= 0) return null;
  try {
    const res = await request({ url: '/Configs/AppConfig', method: 'GET', params: { appId, PageSize: 20 } });
    const hit = listOf(res).find((r) => num(pick(r, 'AppId', 'appId')) === appId);
    if (!hit) return null;
    return {
      id: num(pick(hit, 'Id', 'id')),
      appId,
      name: str(pick(hit, 'Name', 'name')),
      version: num(pick(hit, 'Version', 'version')),
      nextVersion: num(pick(hit, 'NextVersion', 'nextVersion')),
      enable: bool(pick(hit, 'Enable', 'enable')),
    };
  } catch {
    return null;
  }
}

export function toConfigItems(rows: Rec[], publishedVersion: number): ConfigItem[] {
  return rows.map((r) => {
    const state = configItemState(r, publishedVersion);
    const value = str(pick(r, 'Value', 'value'));
    return {
      id: num(pick(r, 'Id', 'id')),
      key: str(pick(r, 'Key', 'key')),
      scope: str(pick(r, 'Scope', 'scope')),
      value,
      // 草稿值：有待发布改动且是新增行时，生效值为空，草稿值在 NewValue
      newValue: str(pick(r, 'NewValue', 'newValue')),
      enable: bool(pick(r, 'Enable', 'enable')),
      state,
      pending: state !== 'published',
    };
  });
}

export async function fetchConfigItems(
  request: Req,
  configId: number,
  publishedVersion: number,
): Promise<ConfigItem[] | null> {
  if (!configId || configId <= 0) return [];
  try {
    const res = await request({ url: '/Configs/ConfigData', method: 'GET', params: { configId, PageSize: 500 } });
    return toConfigItems(listOf(res), publishedVersion);
  } catch {
    return null;
  }
}

/** 历史列表只保留发布与回滚（Insert/Update/Delete 是配置项级中间操作，不是版本），按时间倒序 */
export function toConfigHistory(rows: Rec[]): ConfigHistoryItem[] {
  return rows
    .filter((r) => {
      const a = str(pick(r, 'Action', 'action')).toLowerCase();
      return a === 'publish' || a === 'rollback';
    })
    .map((r) => {
      const at = str(pick(r, 'CreateTime', 'createTime'));
      return {
        id: num(pick(r, 'Id', 'id')),
        action: str(pick(r, 'Action', 'action')),
        success: bool(pick(r, 'Success', 'success')),
        version: historyVersionOf(r),
        time: at ? at.replace('T', ' ').slice(0, 16) : '—',
      };
    })
    .sort((a, b) => (a.time === b.time ? b.id - a.id : a.time < b.time ? 1 : -1));
}

export async function fetchConfigHistory(request: Req, configId: number): Promise<ConfigHistoryItem[] | null> {
  if (!configId || configId <= 0) return [];
  try {
    const res = await request({ url: '/Configs/ConfigHistory', method: 'GET', params: { configId, PageSize: 50 } });
    return toConfigHistory(listOf(res));
  } catch {
    return null;
  }
}

/** 在线实例数：AppOnline 列表按 appId 过滤后的行数（PageSize 500，与应用列表同一上限） */
export async function fetchOnlineCount(request: Req, appId: number): Promise<number> {
  if (!appId || appId <= 0) return 0;
  try {
    const res = await request({ url: '/Registry/AppOnline', method: 'GET', params: { appId, PageSize: 500 } });
    return listOf(res).length;
  } catch {
    return 0;
  }
}

/** 发布：POST /Configs/AppConfig/Publish?configId=。响应是 JsonRefresh，按 okRes 判成败 */
export async function publishConfig(request: Req, configId: number): Promise<{ ok: boolean; message: string }> {
  if (!configId || configId <= 0) return { ok: false, message: '缺少应用配置编号' };
  try {
    const res = await request({ url: '/Configs/AppConfig/Publish', method: 'POST', params: { configId } });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? r.message || '已发布' : r.message || '发布失败，请到配置中心核对' };
  } catch (e) {
    return { ok: false, message: e instanceof Error && e.message ? e.message : '请求失败' };
  }
}

/** 回滚：POST /Configs/ConfigHistory/Rollback?id=。仅发布历史支持，后端会拒绝其它动作 */
export async function rollbackHistory(request: Req, historyId: number): Promise<{ ok: boolean; message: string }> {
  if (!historyId || historyId <= 0) return { ok: false, message: '缺少历史编号' };
  try {
    const res = await request({ url: '/Configs/ConfigHistory/Rollback', method: 'POST', params: { id: historyId } });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? r.message || '已回滚' : r.message || '回滚失败，请到配置中心核对' };
  } catch (e) {
    return { ok: false, message: e instanceof Error && e.message ? e.message : '请求失败' };
  }
}

// ---------- F10 配置项就地编辑（ConfigData CRUD + SyncApp） ----------

/** 配置项编辑表单。published 行的 Key/Scope 只读、Value 只读，改动写进 NewValue */
export interface ConfigItemForm {
  key: string;
  scope: string;
  /** 未发布行的直接值；已发布行只读展示 */
  value: string;
  /** 已发布行的草稿值 */
  newValue: string;
  enable: boolean;
  remark: string;
}

export interface ConfigItemEditTarget {
  id: number;
  /** 该行是否已发布（Version>0） */
  published: boolean;
  enable: boolean;
}

/** 表单校验：Key 必填；已发布行的 Key/Scope 不可改（后端也会拒绝，这里前置拦） */
export function validateConfigItemForm(m: ConfigItemForm, published: boolean, original?: { key: string; scope: string }): string {
  if (!m.key.trim()) return '配置项名称（Key）必填';
  if (published && original && (m.key.trim() !== original.key || m.scope.trim() !== original.scope)) {
    return '已发布的配置项不能修改名称与作用域，请新增一条配置';
  }
  return '';
}

/** PUT body 按发布态分支（源码语义：已发布只动草稿字段，Enable 变化才提交） */
export function configItemPutBody(m: ConfigItemForm, published: boolean, originalEnable: boolean): Record<string, unknown> {
  if (published) {
    const body: Record<string, unknown> = { NewValue: m.newValue, Remark: m.remark };
    if (m.enable !== originalEnable) body.Enable = m.enable;
    return body;
  }
  return { Key: m.key.trim(), Scope: m.scope.trim(), Value: m.value, Enable: m.enable, Remark: m.remark };
}

/** 保存配置项：PUT /Configs/ConfigData/{id}（部分更新，主键在路径） */
export async function saveConfigItem(
  request: Req,
  target: ConfigItemEditTarget,
  m: ConfigItemForm,
): Promise<{ ok: boolean; message: string }> {
  if (!target.id || target.id <= 0) return { ok: false, message: '缺少配置项编号' };
  try {
    const res = await request({
      url: `/Configs/ConfigData/${target.id}`,
      method: 'PUT',
      data: configItemPutBody(m, target.published, target.enable),
    } as ReqOptions & { data: Record<string, unknown> });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? '配置项已保存' : r.message || '配置项保存失败' };
  } catch (e) {
    return { ok: false, message: requestFailText(e, '配置项保存失败') };
  }
}

/** 新增配置项：POST /Configs/ConfigData（ConfigId 必带；后端 OnInsert 自动分配 NewVersion） */
export async function createConfigItem(
  request: Req,
  configId: number,
  m: ConfigItemForm,
): Promise<{ ok: boolean; message: string }> {
  if (!configId || configId <= 0) return { ok: false, message: '缺少应用配置编号' };
  try {
    const res = await request({
      url: '/Configs/ConfigData',
      method: 'POST',
      data: { ConfigId: configId, Key: m.key.trim(), Scope: m.scope.trim(), Value: m.value, Enable: m.enable, Remark: m.remark },
    } as ReqOptions & { data: Record<string, unknown> });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? '配置项已新增' : r.message || '配置项新增失败' };
  } catch (e) {
    return { ok: false, message: requestFailText(e, '配置项新增失败') };
  }
}

/**
 * 删除配置项：DELETE /Configs/ConfigData/{id}。
 * 未发布（Version==0）真删；已发布后端改标 [[Deleted]]（前端文案按 isPublished 区分说明）。
 */
export async function deleteConfigItem(request: Req, id: number): Promise<{ ok: boolean; message: string }> {
  if (!id || id <= 0) return { ok: false, message: '缺少配置项编号' };
  try {
    const res = await request({ url: `/Configs/ConfigData/${id}`, method: 'DELETE' });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? '配置项已删除' : r.message || '配置项删除失败' };
  } catch (e) {
    return { ok: false, message: requestFailText(e, '配置项删除失败') };
  }
}

/** 同步应用：POST /Configs/AppConfig/SyncApp（把注册表应用同步成 AppConfig，全局动作） */
export async function syncAppConfig(request: Req): Promise<{ ok: boolean; message: string }> {
  try {
    const res = await request({ url: '/Configs/AppConfig/SyncApp', method: 'POST' });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? r.message || '同步完成' : r.message || '同步失败' };
  } catch (e) {
    return { ok: false, message: requestFailText(e, '同步失败') };
  }
}
