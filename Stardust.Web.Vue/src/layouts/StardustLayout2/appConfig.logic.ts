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
// 后端草稿模型（ConfigData 实体）：Value 是生效值，NewValue 是草稿值；Version 是生效版本，
// NewVersion 是草稿版本；NewStatus 为 [[Deleted]] 表示待删除。AppConfig.Version < NextVersion
// 即有待发布。取数失败返回 null/0，由页面显示「加载失败」，不造数据。
//
// 未接入（后端没有对应入口或未在本层验证）：就地编辑写草稿（旧配置中心也把「保存草稿」标为
// 待接入）、放弃草稿（后端没有丢弃草稿的接口）、推送进度（Publish 只返回通知的应用数）。
import { okRes } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

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
