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
import { fetchAppList, okRes } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import type { Req, ReqOptions } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import { getAccessToken } from '@newlifex/cube-vue/core/utils/token';
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
  /** AppConfig.UpdateTime 毫秒时间戳（日期区间筛选用；无记录为 0） */
  lastPublishMs: number;
  /** 应用类别（AppConfig.Category；与 Registry/App 的 Category 同源，选项接口 /Configs/AppConfig/Categories） */
  category: string;
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

/**
 * ConfigData 全量行 → configId→键数 映射（/configs「Key 数」列的真实来源）。
 * 只数未删除的键：NewStatus 为 [[Deleted]]（待删除草稿）不计入。
 * UsedKeys 是客户端上报字段（很多应用从没上报过，列表会全 0），所以键数改从 ConfigData 实际行数取。
 */
export function buildKeyCounts(rows: Rec[]): Map<number, number> {
  const m = new Map<number, number>();
  const touched = new Set<number>();
  for (const r of rows) {
    const id = num(pick(r, 'ConfigId', 'configId'));
    if (id <= 0) continue;
    // DEF-2：该 configId 在 ConfigData 里出现过（即使全部行待删除）也要入 map 计 0，
    // 否则会回退到 UsedKeys 上报值，把「生效 0 键」错显示成上报的旧值。
    touched.add(id);
    if (str(pick(r, 'NewStatus', 'newStatus')).toLowerCase() === '[[deleted]]') continue;
    m.set(id, (m.get(id) ?? 0) + 1);
  }
  for (const id of touched) if (!m.has(id)) m.set(id, 0);
  return m;
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
export function toConfigCenterRows(appConfigs: Rec[], appMap: Map<number, string>, keyCounts?: Map<number, number>): ConfigCenterRow[] {
  const out: ConfigCenterRow[] = [];
  for (const r of appConfigs) {
    const appId = num(pick(r, 'AppId', 'appId'));
    const cfgName = str(pick(r, 'DisplayName', 'displayName')) || str(pick(r, 'Name', 'name'));
    const version = num(pick(r, 'Version', 'version'));
    const nextVersion = num(pick(r, 'NextVersion', 'nextVersion'));
    const id = num(pick(r, 'Id', 'id'));
    // 键数优先取 ConfigData 实际行数；拉不到时回退 UsedKeys 上报计数（可能为 0）
    const fromData = keyCounts?.get(id);
    const updateMs = toMs(pick(r, 'UpdateTime', 'updateTime'));
    out.push({
      configId: id,
      appId,
      appName: appMap.get(appId) || cfgName,
      configName: cfgName,
      version,
      nextVersion,
      enable: bool(pick(r, 'Enable', 'enable')),
      keyCount: fromData ?? countUsedKeys(pick(r, 'UsedKeys', 'usedKeys')),
      valueSummary: '',
      lastPublishText: fmtTime(updateMs),
      lastPublishMs: updateMs,
      category: str(pick(r, 'Category', 'category')),
      pending: nextVersion > version,
    });
  }
  out.sort((a, b) => (a.pending === b.pending ? a.appName.localeCompare(b.appName) : a.pending ? -1 : 1));
  return out;
}

/** 列表筛选条件（候选 A：关键字 + 应用 + 类别 + 最近更新日期区间，均为前端过滤） */
export interface ConfigRowFilter {
  keyword: string;
  appId: number;
  /** 类别；'' = 全部类别（不过滤） */
  category?: string;
  /** 最近更新区间起点（含，毫秒）；0/缺省 = 不限 */
  dateFrom?: number;
  /** 最近更新区间终点（含，毫秒）；0/缺省 = 不限 */
  dateTo?: number;
}

/** 日期控件 yyyy-MM-dd → 当地时区当天 [起点ms, 终点ms]；空串返回 0（不过滤） */
export function dateRangeToMs(start: string, end: string): { from: number; to: number } {
  const dayMs = (s: string, edge: 'from' | 'to'): number => {
    const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(s.trim());
    if (!m) return 0;
    const d = new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
    if (Number.isNaN(d.getTime())) return 0;
    return edge === 'from' ? d.getTime() : d.getTime() + 24 * 3600 * 1000 - 1;
  };
  return { from: dayMs(start, 'from'), to: dayMs(end, 'to') };
}

/**
 * 列表筛选纯函数（C05 断言）：关键字匹配应用名/配置名（小写包含）；appId<=0 不过滤；
 * category 空串不过滤（首项「全部类别」）；dateFrom/dateTo 为 0 表示不限，
 * 区间为闭区间 [from, to]，过滤对象是行内的 lastPublishMs（最近更新）。
 */
export function filterRows(
  rows: ConfigCenterRow[],
  f: ConfigRowFilter,
): ConfigCenterRow[] {
  const k = f.keyword.trim().toLowerCase();
  const cat = (f.category ?? '').trim();
  const from = f.dateFrom ?? 0;
  const to = f.dateTo ?? 0;
  return rows.filter((r) => {
    if (f.appId > 0 && r.appId !== f.appId) return false;
    if (cat && r.category !== cat) return false;
    // 日期区间过滤 = 「最近更新在区间内」：从未更新（lastPublishMs=0）不算命中
    if ((from > 0 || to > 0) && r.lastPublishMs <= 0) return false;
    if (from > 0 && r.lastPublishMs < from) return false;
    if (to > 0 && r.lastPublishMs > to) return false;
    if (!k) return true;
    return r.appName.toLowerCase().includes(k) || r.configName.toLowerCase().includes(k);
  });
}

// ---------- 取数 ----------

export interface FetchAllResult {
  rows: ConfigCenterRow[];
  error: string;
}

/** 拉全部 AppConfig + 应用列表（建 appId→名映射）+ ConfigData 键数。应用列表失败不阻塞（名字兜底用 AppConfig.Name） */
export async function fetchAllConfigs(request: Req, projectId?: number | null): Promise<FetchAllResult> {
  try {
    const pp = paramsOf(projectId);
    const [appList, cfgRes, dataRes] = await Promise.all([
      // 应用名映射是锦上添花，失败降级为空映射，不拖垮列表
      fetchAppList(request).catch(() => ({ rows: [], online: [] })),
      request({ url: '/Configs/AppConfig', method: 'GET', params: { PageSize: 500, ...pp } }),
      // Key 数来源：不传 configId 时后端返回全部 ConfigData（configId=-1 不加过滤），一次请求按 configId 分组计数
      request({ url: '/Configs/ConfigData', method: 'GET', params: { PageSize: 1000 } }).catch(() => ({ data: [] })),
    ]);
    const appMap = buildAppMap(appList.rows as Rec[]);
    const keyCounts = buildKeyCounts(listOf(dataRes));
    return { rows: toConfigCenterRows(listOf(cfgRes), appMap, keyCounts), error: '' };
  } catch (e) {
    return { rows: [], error: errText(e, '配置中心加载失败') };
  }
}

/**
 * 类别选项：GET /Configs/AppConfig/Categories（C05 断言「类别选项来自该接口」）。
 * F-CFG-1 P2（2026-11-10 review-walk 实锤）：后端真返字典 {类别: " (N)"}（live：{"":" (1)",...}），
 * 旧实现只认数组 → 恒 null → 下拉恒降级行内去重。现双形状兼容：
 * 字典取键为选项（去空白、排除空键——空串与「全部类别」哨兵 '' 冲突，无类别行经「全部」本就可达）、
 * 数组维持旧去空白去空项；均保序去重。失败/标量返回 null（调用方降级为行内 category 去重，不拖垮列表）。
 */
export async function fetchConfigCategories(request: Req): Promise<string[] | null> {
  try {
    const res = await request({ url: '/Configs/AppConfig/Categories', method: 'GET' });
    const d: unknown = res?.data;
    if (Array.isArray(d)) {
      // 去空白去空项，保序去重
      const out: string[] = [];
      for (const x of d) {
        const s = str(x);
        if (s && !out.includes(s)) out.push(s);
      }
      return out;
    }
    if (d && typeof d === 'object') {
      const out: string[] = [];
      for (const k of Object.keys(d as Rec)) {
        const s = str(k);
        if (s && !out.includes(s)) out.push(s);
      }
      return out;
    }
    return null;
  } catch {
    return null;
  }
}

/** 预览弹层内容（/config/getall 实拉结果，C19 断言） */
export interface ConfigPreview {
  /** AppConfig.Id（行appId，用于请求） */
  appId: number;
  /** 应用名（App.Name，即 /config/getall 的 appId 参数） */
  appName: string;
  version: number;
  scope: string;
  nextVersion: number;
  nextPublish: string;
  updateTime: string;
  /** 生效键值对（Configs 字典；无数据为空对象） */
  configs: Record<string, string>;
}

/**
 * /config/getall 响应 → ConfigPreview。响应形态见 Stardust.WebApi/ConfigController.cs
 * （ConfigInfo：Version / Scope / NextVersion / NextPublish / UpdateTime / Configs 字典；
 * 前端预览不传 version，后端恒回 Configs）。
 */
export function toPreviewInfo(data: unknown): Omit<ConfigPreview, 'appId'> {
  const d = (data ?? {}) as Rec;
  const raw = (pick(d, 'Configs', 'configs') ?? {}) as Rec;
  const configs: Record<string, string> = {};
  for (const [k, v] of Object.entries(raw)) configs[k] = String(v ?? '');
  return {
    appName: str(pick(d, 'AppName', 'appName')),
    version: num(pick(d, 'Version', 'version')),
    scope: str(pick(d, 'Scope', 'scope')),
    nextVersion: num(pick(d, 'NextVersion', 'nextVersion')),
    nextPublish: str(pick(d, 'NextPublish', 'nextPublish')),
    updateTime: str(pick(d, 'UpdateTime', 'updateTime')),
    configs,
  };
}

/**
 * DEF-6（2026-11-10）直连通道：/config/getall 为后端裸根路由（ConfigController，无 /api 前缀），
 * 实体 request 通道（api-core resolveRequestUrl 对非服务路径一律拼 /api）会打出 /api/config/getall → 404
 * （j-appreg live 定谳：同窗 /api/Registry/App 200 vs /api/config/getall 404 铁证）。
 * 与 fetchWebhookFields（pipelineWebhook.logic.ts，同一缺陷家族）同型：原生 fetch 直连
 * VITE_APP_BASE_API host；base/fetch 可注入（单测）；无 base（纯单测环境）降级 null。
 * 鉴权（F-CFG-2 P1 补丁，2026-11-10 review-walk 实锤）：6681 实体通道实走 authorization: bearer JWT，
 * 「credentials:'include' 的会话 cookie 可鉴权本裸根路由」被 live 否定（fetch 无 cookie 无 bearer →
 * HTTP 200 但信封 code:401「未登录！」）。token 与 cubeAxios 拦截器同源：core/utils/token.getAccessToken
 * （localStorage key=config.auth.tokenKey，头前缀同 request.ts tokenHeaderPrefix 'bearer '）；无 token 仅退化为现状。
 * 信封 {code,data}：带 code/success 字段一律按 okRes 判定（与实体 request 通道 unwrap 同语义），
 * 非成功抛错由上层 catch 归 null（弹层失败态）——严禁把 401 信封透传给 toPreviewInfo 伪造成空态；
 * 无信封字段的裸 body 维持透传兼容。坏 body/非 2xx 同样抛错归 null。
 */
async function fetchGetallDirect(
  params: Record<string, string>,
  opts: { base?: string; fetchImpl?: typeof fetch; path?: string },
): Promise<{ data?: unknown } | null> {
  const base = String(opts.base ?? import.meta.env?.VITE_APP_BASE_API ?? '').replace(/\/+$/, '');
  if (!base) return null;
  const doFetch = opts.fetchImpl ?? ((...a: Parameters<typeof fetch>) => globalThis.fetch(...a));
  const qs = new URLSearchParams(params).toString();
  const headers: Record<string, string> = { Accept: 'application/json' };
  const token = getAccessToken();
  if (token) headers['Authorization'] = `bearer ${token}`;
  const res = await doFetch(`${base}${opts.path ?? '/config/getall'}${qs ? `?${qs}` : ''}`, {
    method: 'GET',
    credentials: 'include',
    headers,
  });
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  const body = (await res.json()) as Rec | null;
  const env = body ?? {};
  if ('code' in env || 'success' in env) {
    if (!okRes(env).ok) throw new Error(`信封 code=${str(env.code)} ${str(env.message)}`.trim());
  }
  return { data: body?.data ?? body };
}

/**
 * 候选 B：预览链取数（task-95 二期刀1 改道，2026-11-12）。
 * 旧链 `/config/getall?appId={Name}&secret={AppSecret}`：App LIST 行读明文 Secret（行 JSON 泄漏面）
 * 且 Secret 入 URL 访问日志——刀2 将裁 App 行 Secret，故先行改道专用端点
 * `GET /Config/GetAllByApp?appId={Id}`（后端 ConfigController 同类复用 GetAll：
 * Secret 服务端内解析，浏览器与访问日志零经手；心跳/作用域语义与旧链完全一致）。
 * 直连形态保留（DEF-6：非服务路径实体 request 通道拼 /api 会 404，同 fetchWebhookFields 家族）。
 * App 行仍经实体通道查一次（DEF-4：Search 只认 p["Id"]），仅取 Name/DisplayName 供预览头，
 * 不再读取 Secret（刀2 裁剪后此链不破）。失败返回 null（弹层显示加载失败）。
 */
export async function fetchConfigPreview(
  request: Req,
  appId: number,
  opts: { base?: string; fetchImpl?: typeof fetch } = {},
): Promise<ConfigPreview | null> {
  if (!appId || appId <= 0) return null;
  try {
    const appRes = await request({ url: '/Registry/App', method: 'GET', params: { Id: appId, PageSize: 1 } });
    const app = listOf(appRes)[0];
    const name = app ? str(pick(app, 'Name', 'name')) : '';
    if (!name) return null;
    const res = await fetchGetallDirect({ appId: String(appId), version: '0' }, { ...opts, path: '/Config/GetAllByApp' });
    if (!res) return null;
    return { appId, ...toPreviewInfo(res?.data), appName: str(pick(app, 'DisplayName', 'displayName')) || name };
  } catch {
    return null;
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
  return pendingDiff(items).length;
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

/** 详情项 → 待发布 diff（Value 为旧 / NewValue 为新），只返回非 unchanged 行。
 * DEF-3：NewValue 为 null（后端未返回草稿，toDetailItems 归一为 ''）且 NewStatus 为空 = 无草稿，
 * 新值侧按生效值处理 → unchanged，不产生「草稿=空串」的伪差异。 */
export function pendingDiff(items: DetailConfigItem[]): ConfigDiffRow[] {
  const oldRows = items.map((i) => ({ Key: i.key, Value: i.value }));
  const newRows = items.map((i) => ({
    Key: i.key,
    Value: i.newStatus === '' && i.newValue === '' ? i.value : i.newValue,
  }));
  return diffConfigValues(oldRows, newRows).filter((d) => d.state !== 'unchanged');
}

// ==================== F11 应用规则（AppRule，全局规则表，无 AppId 列） ====================

/** 应用规则行（AppRule 实体：Rule/Result/Priority/Enable） */
export interface AppRuleRow {
  id: number;
  /** 规则表达式，如 IP=10.*,172.*;LocalIP=192.* */
  rule: string;
  /** 命中结果，如 Scope=dev */
  result: string;
  /** 数字越大优先级越高 */
  priority: number;
  enable: boolean;
  remark: string;
  updateTimeText: string;
}

export function toAppRuleRows(rows: Rec[]): AppRuleRow[] {
  return rows
    .map((r) => ({
      id: num(pick(r, 'Id', 'id')),
      rule: str(pick(r, 'Rule', 'rule')),
      result: str(pick(r, 'Result', 'result')),
      priority: num(pick(r, 'Priority', 'priority')),
      enable: bool(pick(r, 'Enable', 'enable')),
      remark: str(pick(r, 'Remark', 'remark')),
      updateTimeText: fmtTime(toMs(pick(r, 'UpdateTime', 'updateTime'))),
    }))
    .sort((a, b) => b.priority - a.priority || b.id - a.id);
}

export interface AppRuleForm {
  rule: string;
  result: string;
  priority: number;
  enable: boolean;
  remark: string;
}

export function validateAppRuleForm(m: AppRuleForm): string {
  if (!m.rule.trim()) return '规则表达式必填（如 IP=10.*;LocalIP=192.*）';
  if (!m.result.trim()) return '命中结果必填（如 Scope=dev）';
  if (!Number.isFinite(m.priority) || m.priority < 0) return '优先级必须是不小于 0 的数字';
  return '';
}

export function appRuleBody(m: AppRuleForm): Record<string, unknown> {
  return { Rule: m.rule.trim(), Result: m.result.trim(), Priority: m.priority, Enable: m.enable, Remark: m.remark };
}

/** 应用规则列表：GET /Configs/AppRule */
export async function fetchAppRules(request: Req): Promise<AppRuleRow[] | null> {
  try {
    const res = await request({ url: '/Configs/AppRule', method: 'GET', params: { PageSize: 200 } });
    return toAppRuleRows(listOf(res));
  } catch {
    return null;
  }
}

/** 新增应用规则：POST /Configs/AppRule */
export async function createAppRule(request: Req, m: AppRuleForm): Promise<{ ok: boolean; message: string }> {
  try {
    const res = await request({ url: '/Configs/AppRule', method: 'POST', data: appRuleBody(m) } as ReqOptions & { data: Record<string, unknown> });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? '应用规则已新增' : r.message || '应用规则新增失败' };
  } catch (e) {
    return { ok: false, message: errText(e, '应用规则新增失败') };
  }
}

/** 保存应用规则：PUT /Configs/AppRule/{id}（部分更新） */
export async function saveAppRule(request: Req, id: number, m: AppRuleForm): Promise<{ ok: boolean; message: string }> {
  if (!id || id <= 0) return { ok: false, message: '缺少规则编号' };
  try {
    const res = await request({ url: `/Configs/AppRule/${id}`, method: 'PUT', data: appRuleBody(m) } as ReqOptions & { data: Record<string, unknown> });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? '应用规则已保存' : r.message || '应用规则保存失败' };
  } catch (e) {
    return { ok: false, message: errText(e, '应用规则保存失败') };
  }
}

/** 删除应用规则：DELETE /Configs/AppRule/{id} */
export async function deleteAppRule(request: Req, id: number): Promise<{ ok: boolean; message: string }> {
  if (!id || id <= 0) return { ok: false, message: '缺少规则编号' };
  try {
    const res = await request({ url: `/Configs/AppRule/${id}`, method: 'DELETE' });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? '应用规则已删除' : r.message || '应用规则删除失败' };
  } catch (e) {
    return { ok: false, message: errText(e, '应用规则删除失败') };
  }
}
