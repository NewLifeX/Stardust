// 告警中心纯逻辑（无 Vue 依赖，request 由调用方注入，便于单测）。
//
// 数据源（已读后端控制器与实体确认，2026-10-30）：
//  - 当前告警 GET /Monitors/AlarmRecord?status=1   （AlarmStatuses.Alaraming=1，报警中）
//  - 历史告警 GET /Monitors/AlarmHistory           （已恢复的告警，全部返回）
//             两者实体同构：Id/Name/Category/Action/Status/StartTime/Duration(秒)/Times/Content/UpdateTime
//             注意：AlarmRecord 实体【没有 ProjectId 字段】（已读 Stardust.Data/Monitors/报警记录.cs），
//             后端 Search 只按 groupId/category/status/dtStart/dtEnd/Q 过滤，projectId 对该接口无实际
//             过滤效果；这里仍按页面规范显式带上（与全局拦截器同向，不报错）。
//
// 严重度是从 Category/Name 语义派生的（实体没有 severity 字段，这是诚实归一）：
//   含「下线/停止/失败」→ critical；含「高/慢/超」→ warning；其余 → info。
// appName 取 Action 字段（后端 Action 通常是应用名或节点名）。
//
// 错误策略：两路并行、任一失败即返回 error（不静默空态）；当前告警在前、历史在后，组内按 Id 倒序。
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
  return v === undefined || v === null ? '' : String(v).trim();
}
function listOf(res: { data?: unknown } | undefined): Rec[] {
  const d = res?.data;
  return Array.isArray(d) ? (d as Rec[]) : [];
}
/** 宽松解析时间字符串（兼容 'YYYY-MM-DD HH:mm:ss' 与 ISO），失败返回 0 */
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

/** 告警严重度（从 Category/Name 语义派生，实体无此字段） */
export type AlarmSeverity = 'critical' | 'warning' | 'info';

/** 告警状态：active=报警中（AlarmRecord），resolved=已恢复（AlarmHistory） */
export type AlarmStatus = 'active' | 'resolved';

/** 归一后的告警行（当前或历史） */
export interface AlarmRow {
  id: number;
  /** 告警标题：Name 字段；缺省给兜底文案 */
  title: string;
  /** 类别：应用下线/节点下线/错误数过高等 */
  category: string;
  /** 报警对象原始值（Action 字段） */
  action: string;
  /** 展示用对象名：取 Action，缺省给兜底 */
  appName: string;
  level: AlarmSeverity;
  status: AlarmStatus;
  /** 开始时间展示文案（YYYY-MM-DD HH:mm），解析失败为 — */
  startTime: string;
  /** 排序时间戳（毫秒）：StartTime，兜底 UpdateTime */
  startMs: number;
  /** 持续时长文案（由 Duration 秒数格式化），如 "5 分钟"；历史告警已结束，仍是当时持续时长 */
  durationText: string;
  /** 完整告警详情 Content；没有为空串 */
  content: string;
  /** 通知次数 */
  times: number;
}

// ---------- 级别派生 ----------

/**
 * 严重度派生（从 Category + Name 合并判断，诚实归一）：
 *   含「下线/停止/失败」→ critical；含「高/慢/超」→ warning；其余 → info。
 */
export function severityOf(category: string, name: string = ''): AlarmSeverity {
  const s = `${category} ${name}`;
  if (/下线|停止|失败/.test(s)) return 'critical';
  if (/高|慢|超/.test(s)) return 'warning';
  return 'info';
}

/** Duration 秒 → "N 秒 / N 分钟 / N 小时 N 分 / N 天 N 小时"；<=0 为 "—" */
export function fmtDuration(sec: number): string {
  if (!Number.isFinite(sec) || sec <= 0) return '—';
  if (sec < 60) return `${Math.floor(sec)} 秒`;
  if (sec < 3600) return `${Math.floor(sec / 60)} 分钟`;
  if (sec < 86400) return `${Math.floor(sec / 3600)} 小时 ${Math.floor((sec % 3600) / 60)} 分`;
  return `${Math.floor(sec / 86400)} 天 ${Math.floor((sec % 86400) / 3600)} 小时`;
}

// ---------- 归一 ----------

/** 一行 AlarmRecord/AlarmHistory → AlarmRow；缺字段给防御性兜底，不抛错 */
export function toAlarmRow(r: Rec, status: AlarmStatus): AlarmRow {
  const duration = num(pick(r, 'Duration', 'duration'));
  const category = str(pick(r, 'Category', 'category'));
  const title = str(pick(r, 'Name', 'name'));
  const action = str(pick(r, 'Action', 'action'));
  const startMs =
    toMs(pick(r, 'StartTime', 'startTime')) || toMs(pick(r, 'UpdateTime', 'updateTime'));
  return {
    id: num(pick(r, 'Id', 'id')),
    title: title || '未命名告警',
    category,
    action,
    appName: action || '—',
    level: severityOf(category, title),
    status,
    startTime: fmtTime(startMs),
    startMs,
    durationText: fmtDuration(duration),
    content: str(pick(r, 'Content', 'content')),
    times: num(pick(r, 'Times', 'times')),
  };
}

/**
 * 合并当前告警（AlarmRecord）+ 历史告警（AlarmHistory）并归一。
 * 排序：当前告警在前（status=active），历史在后；组内按 startMs 倒序（新的在前），同刻按 id 倒序。
 */
export function toAlarmRows(records: Rec[], history: Rec[]): AlarmRow[] {
  const active = records.map((r) => toAlarmRow(r, 'active'));
  const resolved = history.map((r) => toAlarmRow(r, 'resolved'));
  const byRecent = (a: AlarmRow, b: AlarmRow): number => {
    if (b.startMs !== a.startMs) return b.startMs - a.startMs;
    return b.id - a.id;
  };
  return [...active.sort(byRecent), ...resolved.sort(byRecent)];
}

// ---------- 过滤 ----------

/**
 * 纯函数过滤：level='' 不过滤；status='all'|'' 不过滤；
 * appQ 不区分大小写，匹配 appName/title/category 任一包含。
 */
export function filterAlarms(
  rows: AlarmRow[],
  level: string,
  status: string,
  appQ: string,
): AlarmRow[] {
  const q = appQ.trim().toLowerCase();
  return rows.filter((r) => {
    if (level && level !== 'all' && r.level !== level) return false;
    if (status && status !== 'all' && r.status !== status) return false;
    if (q) {
      const hay = `${r.appName} ${r.title} ${r.category}`.toLowerCase();
      if (!hay.includes(q)) return false;
    }
    return true;
  });
}

// ---------- 取数（request 注入） ----------

/** 显式 projectId：非空时合并进 params（与全局拦截器同向；对 AlarmRecord 实际无过滤效果，仅规范一致） */
function withProject(params: Record<string, unknown>, projectId?: number | null): Record<string, unknown> {
  return projectId ? { ...params, projectId } : { ...params };
}

/**
 * 端到端取数：并行拉当前告警（status=1 报警中）+ 历史告警（全部）。
 * 任一请求失败 → 返回 error、rows 为空（不静默空态，由页面给可见错误 + 重试）。
 */
export async function fetchAlarms(
  request: Req,
  projectId?: number | null,
): Promise<{ rows: AlarmRow[]; error: string }> {
  try {
    const [activeRes, historyRes] = await Promise.all([
      request({
        url: '/Monitors/AlarmRecord',
        method: 'GET',
        params: withProject({ status: 1, PageSize: 200 }, projectId),
      }),
      request({
        url: '/Monitors/AlarmHistory',
        method: 'GET',
        params: withProject({ PageSize: 200 }, projectId),
      }),
    ]);
    return { rows: toAlarmRows(listOf(activeRes), listOf(historyRes)), error: '' };
  } catch (e) {
    return { rows: [], error: e instanceof Error && e.message ? e.message : '告警加载失败' };
  }
}
