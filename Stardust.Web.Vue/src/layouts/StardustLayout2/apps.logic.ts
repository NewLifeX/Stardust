// 应用列表页的纯逻辑（无 Vue 依赖，便于单测）。对照原型 page-apps.jsx 的 PageApps 与 kit-ui.jsx 的 AppRow。
// 取数、状态派生都复用 appsModel.ts（fetchAppList / toAggRows），这里只做展示层的归一：
// 分类 chip、行副标题、错误率文字、状态点语气、头像色。
import type { AppAggRow, AppMetric, AppStatus, Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import { normalizeRunStatus } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import { buildDeployMap, buildPipelineMap } from './changes.logic';
import { requestFailText } from './deploy.logic.ts';

type Rec = Record<string, unknown>;

function num(v: unknown): number {
  const n = Number(v);
  return Number.isFinite(n) ? n : 0;
}

function str(v: unknown): string {
  return v === undefined || v === null ? '' : String(v).trim();
}

export const ALL_CATEGORY = '全部';
export const NO_CATEGORY = '未分类';

/** 状态点语气：对应原型 .x-dot 的 ok / err / warn / off */
export type DotTone = 'ok' | 'err' | 'warn' | 'off';

/** 应用所属分类；后端没填时归入「未分类」 */
export function categoryOf(row: Pick<AppAggRow, 'category'>): string {
  return (row.category ?? '').trim() || NO_CATEGORY;
}

/** 分类 chip 列表：「全部」在最前，其余按首次出现的顺序（与原型 new Set 的顺序一致） */
export function listCategories(rows: Pick<AppAggRow, 'category'>[]): string[] {
  const seen = new Set<string>();
  for (const r of rows) seen.add(categoryOf(r));
  return [ALL_CATEGORY, ...seen];
}

/** 按分类过滤；「全部」返回原数组 */
export function filterByCategory<T extends Pick<AppAggRow, 'category'>>(rows: T[], category: string): T[] {
  if (category === ALL_CATEGORY) return rows;
  return rows.filter((r) => categoryOf(r) === category);
}

/** 行副标题：分类 · 版本 · N 实例；没有版本显示「无版本」，不留空 */
export function rowSubtitle(row: Pick<AppAggRow, 'category' | 'version' | 'instances'>): string {
  const version = (row.version ?? '').trim() || '无版本';
  return `${categoryOf(row)} · ${version} · ${row.instances} 实例`;
}

/** 错误率文字：metric 里的 errorRate 是比例（0.003 = 0.3%），与旧 Apps.vue 一致；没有统计显示「—」 */
export function formatErrorRate(metric: AppMetric | null | undefined): string {
  if (!metric) return '—';
  const pct = metric.errorRate * 100;
  if (!Number.isFinite(pct)) return '—';
  return `${Number(pct.toFixed(2))}%`;
}

/** 错误率是否偏高：原型 a.err > 1（单位是百分数），这里即比例 > 0.01 */
export function isErrorRateHigh(metric: AppMetric | null | undefined): boolean {
  if (!metric) return false;
  return Number.isFinite(metric.errorRate) && metric.errorRate * 100 > 1;
}

/**
 * 状态 → 状态点语气。
 * 目前真实数据只能判出运行与离线；error / publishFailed / deploying 先映射好，接入告警与流水线状态后无需再改。
 */
export function dotToneOf(status: AppStatus): DotTone {
  switch (status) {
    case 'running':
      return 'ok';
    case 'error':
    case 'publishFailed':
      return 'err';
    case 'deploying':
    case 'maintenance':
      return 'warn';
    default:
      return 'off';
  }
}

// 头像底色：不含 danger，红色在这个页面只表示异常，头像色只是装饰
const AVATAR_TONES = [
  'var(--el-color-primary)',
  'var(--el-color-primary-light-3)',
  'var(--el-color-success)',
  'var(--el-color-warning)',
  'var(--el-color-info)',
] as const;

/** 按应用名稳定地选一个头像色：同名永远同色，刷新不跳 */
export function avatarTone(name: string): string {
  let h = 5381;
  for (const ch of Array.from(name ?? '')) h = ((h * 33) ^ (ch.codePointAt(0) ?? 0)) >>> 0;
  return AVATAR_TONES[h % AVATAR_TONES.length];
}

// ===== 构建状态小标（复用 changes.logic 的全局聚合模式，避免逐应用 N+1） =====

/** 构建状态：进行中（Pending/Building/Deploying）或失败（Failed）；成功/取消不显示 */
export type BuildStatus = 'building' | 'failed';

const BUILDING_SET = new Set(['Pending', 'Building', 'Deploying']);

function runSortAt(r: Rec): number {
  const raw = str(r.UpdateTime ?? r.updateTime ?? r.BuildStartedTime ?? r.buildStartedTime ?? r.CreateTime ?? r.createTime);
  if (!raw) return 0;
  const t = Date.parse(raw.replace(' ', 'T'));
  return Number.isFinite(t) ? t : 0;
}

/**
 * 纯逻辑：从一批原始 AppPipelineRun 行 + pipelineMap(pipelineId→deployId) + deployMap(deployId→{appId})，
 * 算出每个应用「最近一次」运行的构建状态。同一应用可能有多条 run，取时间最新的；
 * 只返回 building/failed，成功/取消/无状态不出现在 map 里（不显示 Tag）。
 */
export function buildStatusByApp(
  runRows: Rec[],
  pipelineMap: Map<number, number>,
  deployMap: Map<number, { appId: number; appName: string }>,
): Map<number, BuildStatus> {
  const best = new Map<number, { status: BuildStatus; sortAt: number }>();
  for (const r of runRows) {
    const pipelineId = num(r.PipelineId ?? r.pipelineId);
    const deployId = pipelineMap.get(pipelineId);
    if (!deployId) continue;
    const entry = deployMap.get(deployId);
    if (!entry) continue;
    const status = normalizeRunStatus(r.Status ?? r.status);
    let cls: BuildStatus | null = null;
    if (BUILDING_SET.has(status)) cls = 'building';
    else if (status === 'Failed') cls = 'failed';
    if (!cls) continue;
    const sortAt = runSortAt(r);
    const cur = best.get(entry.appId);
    if (!cur || sortAt > cur.sortAt) best.set(entry.appId, { status: cls, sortAt });
  }
  const out = new Map<number, BuildStatus>();
  for (const [appId, v] of best) out.set(appId, v.status);
  return out;
}

/**
 * 端到端取数：并行拉 AppDeploy（项目内 deployId→appId）、AppPipeline（全局 pipelineId→deployId）、
 * AppPipelineRun（全局最近 100 条），客户端按项目 deployId 集合过滤，输出 appId→构建状态。
 * 任一请求失败返回空 Map（不阻塞应用列表）。
 */
export async function fetchAppBuildStatus(request: Req): Promise<Map<number, BuildStatus>> {
  try {
    const [deployRes, pipeRes, runRes] = await Promise.all([
      request({ url: '/Deployment/AppDeploy', method: 'GET', params: { PageSize: 500 } }),
      request({ url: '/Deployment/AppPipeline', method: 'GET', params: { PageSize: 500 } }),
      request({ url: '/Deployment/AppPipelineRun', method: 'GET', params: { PageSize: 100 } }),
    ]);
    const deployRows = Array.isArray(deployRes?.data) ? (deployRes.data as Rec[]) : [];
    const pipeRows = Array.isArray(pipeRes?.data) ? (pipeRes.data as Rec[]) : [];
    const runRows = Array.isArray(runRes?.data) ? (runRes.data as Rec[]) : [];
    return buildStatusByApp(runRows, buildPipelineMap(pipeRows), buildDeployMap(deployRows));
  } catch {
    return new Map();
  }
}

// ===== 趋势小图（AppDayStat 日统计） =====

/** 趋势数据点：日期 + 当日总调用次数 */
export interface TrendPoint {
  date: string;
  total: number;
}

/**
 * 纯逻辑：把 AppDayStat 原始行按 appId 分组、按日期升序、取最近 limit 天的 total 值。
 * 字段兼容 PascalCase（后端实体）与 camelCase。无数据返回空数组。
 */
export function trendPointsByApp(rows: Rec[], limit = 14): Map<number, TrendPoint[]> {
  const byApp = new Map<number, TrendPoint[]>();
  for (const r of rows) {
    const appId = num(r.AppId ?? r.appId);
    if (!appId) continue;
    const date = str(r.StatDate ?? r.statDate).slice(0, 10);
    if (!date) continue;
    const total = num(r.Total ?? r.total);
    const arr = byApp.get(appId) ?? [];
    arr.push({ date, total });
    byApp.set(appId, arr);
  }
  for (const [id, pts] of byApp) {
    pts.sort((a, b) => a.date.localeCompare(b.date));
    byApp.set(id, pts.slice(-limit));
  }
  return byApp;
}

/**
 * 纯逻辑：把数值序列生成 SVG polyline 的 path 字符串（M...L...）。
 * values 少于 2 个返回空串（不渲染）。自动缩放至 [2, height-2] 的纵向区间，左右撑满。
 */
export function sparklinePath(values: number[], width = 60, height = 20): string {
  if (values.length < 2) return '';
  const nums = values.map((v) => (Number.isFinite(v) ? v : 0));
  let min = Math.min(...nums);
  let max = Math.max(...nums);
  if (min === max) {
    min -= 1;
    max += 1;
  }
  const range = max - min;
  const padY = 2;
  const usableH = height - padY * 2;
  const stepX = width / (nums.length - 1);
  return nums
    .map((v, i) => {
      const x = i * stepX;
      const y = padY + (1 - (v - min) / range) * usableH;
      return `${i === 0 ? 'M' : 'L'}${x.toFixed(1)},${y.toFixed(1)}`;
    })
    .join(' ');
}

// ==================== F12 应用 CRUD（/Registry/App，PUT 主键在路径部分更新） ====================

/** 应用表单。Secret 列表隐藏、表单可见；编辑时留空=不修改 */
export interface AppForm {
  name: string;
  displayName: string;
  category: string;
  enable: boolean;
  /** 客户端轮询间隔（秒） */
  period: number;
  secret: string;
  remark: string;
}

export function validateAppForm(m: AppForm, isCreate: boolean): string {
  if (!m.name.trim()) return '应用名必填（接入端要用它来注册）';
  // 编辑态 Name 只读不回传，仅创建态校验格式
  if (isCreate) {
    if (m.name.trim() !== m.name.trim().toLowerCase()) return '应用名建议全小写（服务注册与配置中心按名称匹配）';
    if (/\s/.test(m.name.trim())) return '应用名不能包含空格';
  }
  if (!Number.isFinite(m.period) || m.period < 0) return '轮询间隔必须是不小于 0 的秒数';
  return '';
}

/** 新增 body（POST /Registry/App，返回 data 含新 id）；ProjectId 由全局拦截器注入时可不传 */
export function appCreateBody(m: AppForm, projectId?: number | null): Record<string, unknown> {
  const body: Record<string, unknown> = {
    Name: m.name.trim(),
    DisplayName: m.displayName.trim(),
    Category: m.category.trim(),
    Enable: m.enable,
    Period: m.period,
    Remark: m.remark,
  };
  if (m.secret.trim()) body.Secret = m.secret.trim();
  if (projectId != null && projectId > 0) body.ProjectId = projectId;
  return body;
}

/** 编辑 body（PUT /Registry/App/{id} 部分更新）：Secret 留空=不修改，不提交 */
export function appUpdateBody(m: AppForm): Record<string, unknown> {
  const body: Record<string, unknown> = {
    DisplayName: m.displayName.trim(),
    Category: m.category.trim(),
    Enable: m.enable,
    Period: m.period,
    Remark: m.remark,
  };
  if (m.secret.trim()) body.Secret = m.secret.trim();
  return body;
}

function errOf(e: unknown, fallback: string): { ok: false; message: string } {
  // 404/405 →「接口部署中」，其余透传 e.message（与 appConfig/mesh/deploy 各 logic 一致）
  return { ok: false, message: requestFailText(e, fallback) };
}

/** 新建应用：POST /Registry/App → data.id */
export async function createApp(request: Req, m: AppForm, projectId?: number | null): Promise<{ ok: boolean; message: string; id?: number }> {
  try {
    const res = await request({ url: '/Registry/App', method: 'POST', data: appCreateBody(m, projectId) } as Parameters<Req>[0] & { data: Record<string, unknown> });
    const r = res as { code?: number; message?: string; data?: unknown };
    const ok = r.code === 0 || r.code === 200 || r.code === 1;
    const d = (r.data ?? {}) as Record<string, unknown>;
    const id = Number(d.Id ?? d.id ?? 0) || undefined;
    return { ok, message: ok ? `应用「${m.name.trim()}」已创建` : r.message || '应用创建失败', id };
  } catch (e) {
    return errOf(e, '应用创建失败');
  }
}

/** 保存应用：PUT /Registry/App/{id}（部分更新） */
export async function saveApp(request: Req, id: number, m: AppForm): Promise<{ ok: boolean; message: string }> {
  if (!id || id <= 0) return { ok: false, message: '缺少应用编号' };
  try {
    const res = await request({ url: `/Registry/App/${id}`, method: 'PUT', data: appUpdateBody(m) } as Parameters<Req>[0] & { data: Record<string, unknown> });
    const r = res as { code?: number; message?: string };
    const ok = r.code === 0 || r.code === 200 || r.code === 1;
    return { ok, message: ok ? '应用已保存' : r.message || '应用保存失败' };
  } catch (e) {
    return errOf(e, '应用保存失败');
  }
}

/** 启用/停用应用：PUT /Registry/App/{id} 只提交 Enable（部分更新，未提交字段不动） */
export async function setAppEnable(request: Req, id: number, enable: boolean): Promise<{ ok: boolean; message: string }> {
  if (!id || id <= 0) return { ok: false, message: '缺少应用编号' };
  try {
    const res = await request({ url: `/Registry/App/${id}`, method: 'PUT', data: { Enable: enable } } as Parameters<Req>[0] & { data: Record<string, unknown> });
    const r = res as { code?: number; message?: string };
    const ok = r.code === 0 || r.code === 200 || r.code === 1;
    return { ok, message: ok ? (enable ? '已启用' : '已停用') : r.message || '操作失败' };
  } catch (e) {
    return errOf(e, '操作失败');
  }
}

/** 删除应用：DELETE /Registry/App/{id}（不可恢复；接入端再用同名注册会被当成新应用） */
export async function deleteApp(request: Req, id: number): Promise<{ ok: boolean; message: string }> {
  if (!id || id <= 0) return { ok: false, message: '缺少应用编号' };
  try {
    const res = await request({ url: `/Registry/App/${id}`, method: 'DELETE' });
    const r = res as { code?: number; message?: string };
    const ok = r.code === 0 || r.code === 200 || r.code === 1;
    return { ok, message: ok ? '应用已删除' : r.message || '应用删除失败' };
  } catch (e) {
    return errOf(e, '应用删除失败');
  }
}
