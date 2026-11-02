// 跨应用配置中心（/configs，轻量只读版）纯逻辑（无 Vue 依赖，request 由调用方注入，便于单测）。
//
// 与应用内配置页签（appConfig.logic.ts）的区别：那里按 appId 拉一条 AppConfig；这里拉全部 AppConfig，
// 按应用名聚合展示。不做就地编辑（后端没有草稿写入接口），编辑提示跳转到对应应用的配置页签。
//
// 数据源（已读后端控制器与实体确认）：
//  - 应用配置列表：GET /Configs/AppConfig          不传 appId 返回全部；projectId 由全局拦截器注入（这里显式透传便于单测）
//  - 配置键值：    GET /Configs/ConfigData?configId=  当前生效 Value + 草稿 NewValue（后端不提供历史版本 KV 快照）
//  - 配置历史：    GET /Configs/ConfigHistory?configId= Action=Publish/Rollback/Insert/Update/Delete，
//                  Remark 仅存 AppConfig 自身 JSON（版本号），不含历史 KV，故无法做两个历史版本的 KV diff；
//                  本页的 diff 只对比「已发布 Value → 草稿 NewValue」（待发布差异），与应用内配置页签一致。
//  - 应用列表：    GET /Registry/App（经 appsModel.fetchAppList），建 appId→应用名映射。
//
// 数据原则：接真实接口，不造演示数据。keyCount 来自 AppConfig.UsedKeys（客户端已实际拉取过的配置项数，
// 字段为空时为 0，不臆造总数）；valueSummary 列表层无逐配置值，留空（Drawer 才拉全量 KV）。
import { fetchAppList } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import { historyVersionOf } from './appConfig.logic.ts';

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
/** projectId 透传：非空才放进 params（与 mesh.logic.ts 同一模式） */
function paramsOf(projectId?: number | null): Record<string, unknown> {
  return projectId != null ? { projectId } : {};
}
function errText(e: unknown, fallback: string): string {
  return e instanceof Error && e.message ? e.message : fallback;
}
/** 宽松解析时间，失败返回 0 */
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

// ---------- 类型 ----------

/** 配置中心列表行（跨应用聚合后的一行 = 一个应用的一条 AppConfig） */
export interface ConfigCenterRow {
  /** AppConfig.Id，Drawer 详情按它取 ConfigData/ConfigHistory */
  configId: number;
  appId: number;
  /** 应用显示名（Registry/App 映射，缺失时兜底 AppConfig.Name） */
  appName: string;
  /** 配置名（AppConfig.DisplayName || Name） */
  configName: string;
  /** 当前生效版本 */
  version: number;
  /** 草稿版本；> version 表示有待发布 */
  nextVersion: number;
  enable: boolean;
  /** 客户端已使用的配置项数（UsedKeys 拆分计数；无上报为 0） */
  keyCount: number;
  /** 列表层不逐配置取 KV，留空；详情见 Drawer */
  valueSummary: string;
  /** AppConfig.UpdateTime 格式化；无记录为 — */
  lastPublishText: string;
  /** 有待发布草稿（nextVersion > version） */
  pending: boolean;
}

/** 详情 Drawer 的配置项行（直接展示原始 Value/NewValue/NewStatus，不做状态颜色） */
export interface DetailConfigItem {
  id: number;
  key: string;
  /** 生效值 Value */
  value: string;
  /** 草稿值 NewValue（无草稿时与 value 相同） */
  newValue: string;
  /** 原始 NewStatus（如 [[Deleted]]）；空串表示正常 */
  newStatus: string;
}

/** 详情 Drawer 的历史行（保留全部动作，不只是 Publish/Rollback） */
export interface DetailHistoryItem {
  id: number;
  /** Publish / Rollback / Insert / Update / Delete */
  action: string;
  success: boolean;
  /** 快照里的版本号（非发布动作多为 0） */
  version: number;
  time: string;
}

/** 配置详情（Drawer 内容） */
export interface ConfigDetail {
  configId: number;
  appId: number;
  appName: string;
  items: DetailConfigItem[];
  history: DetailHistoryItem[];
  /** 与已发布值不一致的项数（待发布草稿数） */
  pendingCount: number;
}

/** 两版本 KV diff 行 */
export interface ConfigDiffRow {
  key: string;
  oldVal: string;
  newVal: string;
  state: 'unchanged' | 'added' | 'removed' | 'changed';
}

// ---------- 纯推导 ----------

/** UsedKeys 是逗号/分号/换行分隔的已用键；拆分后数非空段，空串返回 0 */
export function countUsedKeys(v: unknown): number {
  const s = str(v);
  if (!s) return 0;
  return s.split(/[,;\n]/).map((x) => x.trim()).filter((x) => x.length > 0).length;
}

/** 应用列表行 → appId→应用名 映射（DisplayName 优先，兜底 Name） */
export function buildAppMap(apps: Rec[]): Map<number, string> {
  const m = new Map<number, string>();
  for (const a of apps) {
    const id = num(pick(a, 'Id', 'id'));
    const name = str(pick(a, 'DisplayName', 'displayName')) || str(pick(a, 'Name', 'name'));
    if (id > 0 && name) m.set(id, name);
  }
  return m;
}

/** AppConfig 行 → 配置中心行。pending 行排前面，再按应用名排序 */
export function toConfigCenterRows(appConfigs: Rec[], appMap: Map<number, string>): ConfigCenterRow[] {
  const out: ConfigCenterRow[] = [];
  for (const r of appConfigs) {
    const appId = num(pick(r, 'AppId', 'appId'));
    const cfgName = str(pick(r, 'DisplayName', 'displayName')) || str(pick(r, 'Name', 'name'));
    const version = num(pick(r, 'Version', 'version'));
    const nextVersion = num(pick(r, 'NextVersion', 'nextVersion'));
    out.push({
      configId: num(pick(r, 'Id', 'id')),
      appId,
      appName: appMap.get(appId) || cfgName,
      configName: cfgName,
      version,
      nextVersion,
      enable: bool(pick(r, 'Enable', 'enable')),
      keyCount: countUsedKeys(pick(r, 'UsedKeys', 'usedKeys')),
      valueSummary: '',
      lastPublishText: fmtTime(toMs(pick(r, 'UpdateTime', 'updateTime'))),
      pending: nextVersion > version,
    });
  }
  out.sort((a, b) => (a.pending === b.pending ? a.appName.localeCompare(b.appName) : a.pending ? -1 : 1));
  return out;
}

/** 搜索 + 应用过滤纯函数。keyword 匹配应用名/配置名（小写包含）；appId<=0 不过滤 */
export function filterRows(
  rows: ConfigCenterRow[],
  f: { keyword: string; appId: number },
): ConfigCenterRow[] {
  const k = f.keyword.trim().toLowerCase();
  return rows.filter((r) => {
    if (f.appId > 0 && r.appId !== f.appId) return false;
    if (!k) return true;
    return r.appName.toLowerCase().includes(k) || r.configName.toLowerCase().includes(k);
  });
}

// ---------- 取数 ----------

export interface FetchAllResult {
  rows: ConfigCenterRow[];
  error: string;
}

/** 拉全部 AppConfig + 应用列表（建 appId→名映射）。应用列表失败不阻塞（名字兜底用 AppConfig.Name） */
export async function fetchAllConfigs(request: Req, projectId?: number | null): Promise<FetchAllResult> {
  try {
    const pp = paramsOf(projectId);
    const [appList, cfgRes] = await Promise.all([
      // 应用名映射是锦上添花，失败降级为空映射，不拖垮列表
      fetchAppList(request).catch(() => ({ rows: [], online: [] })),
      request({ url: '/Configs/AppConfig', method: 'GET', params: { PageSize: 500, ...pp } }),
    ]);
    const appMap = buildAppMap(appList.rows as Rec[]);
    return { rows: toConfigCenterRows(listOf(cfgRes), appMap), error: '' };
  } catch (e) {
    return { rows: [], error: errText(e, '配置中心加载失败') };
  }
}

/** ConfigData 行 → 详情项（直接透传 Value/NewValue/NewStatus，不算状态颜色） */
export function toDetailItems(rows: Rec[]): DetailConfigItem[] {
  return rows.map((r) => ({
    id: num(pick(r, 'Id', 'id')),
    key: str(pick(r, 'Key', 'key')),
    value: str(pick(r, 'Value', 'value')),
    newValue: str(pick(r, 'NewValue', 'newValue')),
    newStatus: str(pick(r, 'NewStatus', 'newStatus')),
  }));
}

/** ConfigHistory 行 → 详情历史行（保留全部动作，按时间倒序；版本号从 Remark 快照解析） */
export function toDetailHistory(rows: Rec[]): DetailHistoryItem[] {
  return rows
    .map((r) => ({
      id: num(pick(r, 'Id', 'id')),
      action: str(pick(r, 'Action', 'action')),
      success: bool(pick(r, 'Success', 'success')),
      version: historyVersionOf(r),
      time: (() => {
        const at = str(pick(r, 'CreateTime', 'createTime'));
        return at ? at.replace('T', ' ').slice(0, 16) : '—';
      })(),
    }))
    .sort((a, b) => (a.time === b.time ? b.id - a.id : a.time < b.time ? 1 : -1));
}

/** 待发布项数：草稿值与生效值不同，或 NewStatus 标记删除/新增 */
export function countPendingItems(items: DetailConfigItem[]): number {
  return items.filter((it) => {
    if (it.newStatus) return true;
    return it.newValue !== it.value;
  }).length;
}

/** 拉详情：ConfigData + ConfigHistory。任一失败返回 null（由 Drawer 显示加载失败） */
export async function fetchConfigDetail(
  request: Req,
  configId: number,
  appId: number,
  appName: string,
): Promise<ConfigDetail | null> {
  if (!configId || configId <= 0) return null;
  try {
    const [dataRes, histRes] = await Promise.all([
      request({ url: '/Configs/ConfigData', method: 'GET', params: { configId, PageSize: 500 } }),
      request({ url: '/Configs/ConfigHistory', method: 'GET', params: { configId, PageSize: 50 } }),
    ]);
    const items = toDetailItems(listOf(dataRes));
    return {
      configId,
      appId,
      appName,
      items,
      history: toDetailHistory(listOf(histRes)),
      pendingCount: countPendingItems(items),
    };
  } catch {
    return null;
  }
}

/**
 * 两版 KV 按 Key diff。oldRows/newRows 各为 {Key|key, Value|value} 行。
 * 旧有新无=removed；新有旧无=added；都有且相等=unchanged；都有不等=changed。
 * 键集合取并集，按 key 排序。
 */
export function diffConfigValues(oldRows: Rec[], newRows: Rec[]): ConfigDiffRow[] {
  const oldMap = new Map<string, string>();
  const newMap = new Map<string, string>();
  for (const r of oldRows) {
    const k = str(pick(r, 'Key', 'key'));
    if (k) oldMap.set(k, str(pick(r, 'Value', 'value')));
  }
  for (const r of newRows) {
    const k = str(pick(r, 'Key', 'key'));
    if (k) newMap.set(k, str(pick(r, 'Value', 'value')));
  }
  const keys = new Set<string>([...oldMap.keys(), ...newMap.keys()]);
  const out: ConfigDiffRow[] = [];
  for (const key of keys) {
    const hasOld = oldMap.has(key);
    const hasNew = newMap.has(key);
    const oldVal = oldMap.get(key) ?? '';
    const newVal = newMap.get(key) ?? '';
    let state: ConfigDiffRow['state'];
    if (hasOld && !hasNew) state = 'removed';
    else if (!hasOld && hasNew) state = 'added';
    else if (oldVal === newVal) state = 'unchanged';
    else state = 'changed';
    out.push({ key, oldVal, newVal, state });
  }
  out.sort((a, b) => a.key.localeCompare(b.key));
  return out;
}

/** 详情项 → 待发布 diff（Value 为旧 / NewValue 为新），只返回非 unchanged 行 */
export function pendingDiff(items: DetailConfigItem[]): ConfigDiffRow[] {
  const oldRows = items.map((i) => ({ Key: i.key, Value: i.value }));
  const newRows = items.map((i) => ({ Key: i.key, Value: i.newValue }));
  return diffConfigValues(oldRows, newRows).filter((d) => d.state !== 'unchanged');
}
