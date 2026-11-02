// 应用列表页的纯逻辑（无 Vue 依赖，便于单测）。对照原型 page-apps.jsx 的 PageApps 与 kit-ui.jsx 的 AppRow。
// 取数、状态派生都复用 appsModel.ts（fetchAppList / toAggRows），这里只做展示层的归一：
// 分类 chip、行副标题、错误率文字、状态点语气、头像色。
import type { AppAggRow, AppMetric, AppStatus, Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import { normalizeRunStatus } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import { buildDeployMap, buildPipelineMap } from './changes.logic';

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
