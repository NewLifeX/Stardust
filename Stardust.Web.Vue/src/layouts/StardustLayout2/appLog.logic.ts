// 日志页签纯逻辑（无 Vue 依赖，request 由调用方注入，便于单测）。
// 真实数据来源：GET /Registry/AppClientLog?appId=&kind=&dtStart=&dtEnd=&Q=
// 应用客户端日志：服务端 Search 支持 appId（分表感知必填）/clientId/threadId/kind 过滤，
// dtStart/dtEnd 均空时默认当天 00:00 起；缺表环境返回空数据（SearchSafe 兜底）。
// kind 服务端是精确匹配（_.Kind == kind，Biz 注释：Info/Warn/Error/Fatal）→ 级别 chip 改服务端过滤，
// chip 值映射 Error/Warn/Info/Debug；Kind 为空或其它值（如 Fatal）只在「全部」下可见（UI 不隐藏事实）。
// 响应字段大小写两种都认。取数失败返回 null，由组件显示「加载失败」，不造数据。
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
function listOf(res: { data?: unknown } | undefined): Rec[] {
  const d = res?.data;
  return Array.isArray(d) ? (d as Rec[]) : [];
}

// ---------- 级别 ----------

export type LogLevel = 'debug' | 'info' | 'warn' | 'error';

/** 识别不出的级别返回空串（在「全部」里仍展示，在具体级别 chip 下隐藏） */
export function normalizeLevel(kind: unknown): LogLevel | '' {
  const s = str(kind).trim().toLowerCase();
  if (!s) return '';
  if (s.includes('error') || s === 'fatal') return 'error';
  if (s.includes('warn')) return 'warn';
  if (s.includes('debug')) return 'debug';
  if (s.includes('info')) return 'info';
  return '';
}

/** 工具条级别 chip（与原型 §4.3 一致：全部 / Error / Warn / Info / Debug） */
export const LOG_LEVEL_CHIPS: { key: 'all' | LogLevel; label: string }[] = [
  { key: 'all', label: '全部' },
  { key: 'error', label: 'Error' },
  { key: 'warn', label: 'Warn' },
  { key: 'info', label: 'Info' },
  { key: 'debug', label: 'Debug' },
];

// ---------- 日志行 ----------

export interface LogRow {
  id: number;
  appId: number;
  clientId: string;
  /** 日志头自带的时间串（原样展示，截断到毫秒） */
  time: string;
  level: LogLevel | '';
  name: string;
  message: string;
}

function fmtTime(t: string): string {
  return t.replace('T', ' ').slice(0, 23);
}

/** 服务端返回的是时间窗内全部应用的日志：本地按 appId 过滤，并按时间升序（新日志在底部） */
export function toAppLogRows(rows: Rec[], appId: number): LogRow[] {
  return rows
    .filter((r) => num(pick(r, 'AppId', 'appId')) === appId)
    .map((r) => ({
      id: num(pick(r, 'Id', 'id')),
      appId,
      clientId: str(pick(r, 'ClientId', 'clientId')),
      time: fmtTime(str(pick(r, 'Time', 'time'))),
      level: normalizeLevel(pick(r, 'Kind', 'kind')),
      name: str(pick(r, 'Name', 'name')),
      message: str(pick(r, 'Message', 'message')),
    }))
    .sort((a, b) => a.id - b.id);
}

/** 级别 chip 过滤：all 含级别识别不出的行，具体级别只留匹配行 */
export function filterLogsByLevel(rows: LogRow[], level: 'all' | LogLevel): LogRow[] {
  if (level === 'all') return rows;
  return rows.filter((r) => r.level === level);
}

// ---------- 时间窗与取数 ----------

export interface LogRange {
  dtStart: string;
  dtEnd: string;
}

/** 默认查今天（后端按雪花 Id 在时间窗内分表查询，不传时间窗查不到） */
export function todayRange(now: Date = new Date()): LogRange {
  const y = now.getFullYear();
  const m = `${now.getMonth() + 1}`.padStart(2, '0');
  const d = `${now.getDate()}`.padStart(2, '0');
  return { dtStart: `${y}-${m}-${d}`, dtEnd: `${y}-${m}-${d} 23:59:59` };
}

/** 级别 chip → 服务端 kind 精确匹配值（_.Kind == kind；后端注释 Info/Warn/Error/Fatal，Debug 按同风格首字母大写） */
export function kindParam(level: 'all' | LogLevel): string | undefined {
  if (level === 'all') return undefined;
  return level.charAt(0).toUpperCase() + level.slice(1);
}

/**
 * GET /Registry/AppClientLog?appId=&kind=&dtStart=&dtEnd=
 * appId/kind 服务端过滤（appId 是分表感知必填）；时间窗默认当天。
 * 失败返回 null。
 */
export async function fetchAppLogs(
  request: Req,
  appId: number,
  range: LogRange,
  kind: 'all' | LogLevel = 'all',
  pageSize = 200,
): Promise<LogRow[] | null> {
  if (!appId || appId <= 0) return [];
  try {
    const res = await request({
      url: '/Registry/AppClientLog',
      method: 'GET',
      params: {
        appId,
        kind: kindParam(kind),
        dtStart: range.dtStart,
        dtEnd: range.dtEnd,
        PageSize: pageSize,
      },
    });
    return toAppLogRows(listOf(res), appId);
  } catch {
    return null;
  }
}
