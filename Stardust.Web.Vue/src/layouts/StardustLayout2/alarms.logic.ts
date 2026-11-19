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
// 组/时间过滤（CARD-MON-02，已读后端控制器确认）：两个 Search 都支持 groupId/dtStart/dtEnd
//   （AlarmRecordController.cs:35-45、AlarmHistoryController.cs:17-25）；选中组/时间时带参重取
//   （服务端过滤），不选=全量（与后端 groupId<0 同语义，不造默认时间窗）；关键字 Q 维持客户端过滤。
// 深链：/alarms?groupId={id} → 组过滤预置（parseAlarmsQuery）；无参或非法 = 不过滤。
//
// 已恢复视图（CARD-MON-03，定稿裁定 3）：状态下拉加「已恢复」→ fetchAlarms 以 recordStatus=2 重取
//   GET /Monitors/AlarmRecord?status=2（服务端过滤）；该视图只拉记录路、不并 AlarmHistory（通知记录
//   与生命周期记录两实体，并显会重复）；默认视图仍=报警中（status=1，与 /now KPI 同口径，保 C09）。
//   行 Status 字段自描述：Status=2 的记录行归一为 recovered（Tag「已恢复」ok 语气），历史行无
//   Status 字段、落调用方默认 resolved；AlarmRecord 可编辑/可删除（记录行），AlarmHistory 只读。
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

/**
 * 告警状态：
 *   active=报警中（AlarmRecord Status=1，Tag「未处理」warn）；
 *   recovered=已恢复（AlarmRecord Status=2，Tag「已恢复」ok，CARD-MON-03）；
 *   resolved=历史行（AlarmHistory，Tag「已处理」default 灰）。
 */
export type AlarmStatus = 'active' | 'resolved' | 'recovered';

/** 归一后的告警行（当前或历史） */
export interface AlarmRow {
  /**
   * 记录 Id：字符串原样透传（走查缺陷②）。后端雪花 Id 已字符串化（"id":"7524…"），
   * >2^53 经 Number 会损毁尾段（编辑 PUT / 删除 DELETE 路径错 id → 500「无法取得编号」），
   * 故不经 num()、保持原串；缺失兜底空串。
   */
  id: string;
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
  /**
   * 所属告警组 Id：字符串透传（走查缺陷②，雪花 id 全链 string 裁定，C18）。
   * AlarmRecord/AlarmHistory 均有 GroupId；无组=''（空串哨兵），行内「告警历史」链接据此显隐。
   */
  groupId: string;
  /** 开始时间展示文案（YYYY-MM-DD HH:mm），解析失败为 — */
  startTime: string;
  /** 排序时间戳（毫秒）：StartTime，兜底 UpdateTime */
  startMs: number;
  /** 结束时间时间戳（毫秒，EndTime 原值；报警中=0）——编辑抽屉 EndTime 初值用（CARD-MON-03） */
  endTimeMs?: number;
  /** Duration 原始秒数（durationText 的来源）——编辑抽屉 Duration 初值用（CARD-MON-03） */
  durationSec?: number;
  /** 持续时长文案（由 Duration 秒数格式化），如 "5 分钟"；历史告警已结束，仍是当时持续时长 */
  durationText: string;
  /** 完整告警详情 Content；没有为空串 */
  content: string;
  /** 执行错误原文（Error 字段，仅进展开区「错误」格；空=展示层给「（空）」兜底） */
  error: string;
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

/**
 * 一行 AlarmRecord/AlarmHistory → AlarmRow；缺字段给防御性兜底，不抛错。
 * status：行自带 Status 字段优先自描述（Status=2 → recovered，CARD-MON-03）；
 *   无 Status 字段（AlarmHistory 实体没有）落调用方默认（records→active、history→resolved）。
 */
export function toAlarmRow(r: Rec, defaultStatus: AlarmStatus): AlarmRow {
  const duration = num(pick(r, 'Duration', 'duration'));
  const category = str(pick(r, 'Category', 'category'));
  const title = str(pick(r, 'Name', 'name'));
  const action = str(pick(r, 'Action', 'action'));
  const startMs =
    toMs(pick(r, 'StartTime', 'startTime')) || toMs(pick(r, 'UpdateTime', 'updateTime'));
  const status: AlarmStatus = num(pick(r, 'Status', 'status')) === 2 ? 'recovered' : defaultStatus;
  return {
    id: str(pick(r, 'Id', 'id')),
    title: title || '未命名告警',
    category,
    action,
    appName: action || '—',
    level: severityOf(category, title),
    status,
    groupId: str(pick(r, 'GroupId', 'groupId')),
    startTime: fmtTime(startMs),
    startMs,
    endTimeMs: toMs(pick(r, 'EndTime', 'endTime')),
    durationSec: duration,
    durationText: fmtDuration(duration),
    content: str(pick(r, 'Content', 'content')),
    error: str(pick(r, 'Error', 'error')),
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
  // 同刻并列按 Id 数字语义倒序（走查缺陷②）：id 是字符串透传（雪花 >2^53），
  // 比较按「位数优先、等位数字典序」，与数字序一致且不经 Number 损毁
  const byIdDesc = (x: string, y: string): number => {
    if (x.length !== y.length) return y.length - x.length;
    return x < y ? 1 : x > y ? -1 : 0;
  };
  const byRecent = (a: AlarmRow, b: AlarmRow): number => {
    if (b.startMs !== a.startMs) return b.startMs - a.startMs;
    return byIdDesc(a.id, b.id);
  };
  return [...active.sort(byRecent), ...resolved.sort(byRecent)];
}

// ---------- 过滤 ----------

/**
 * 纯函数过滤：level='' 不过滤；status='all'|'' 不过滤；
 * status='recovered' 不做客户端状态过滤（CARD-MON-03：已恢复是服务端视图标记，行集已由
 * GET /Monitors/AlarmRecord?status=2 服务端筛好，客户端级别/关键字仍照常叠加）；
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
    if (status && status !== 'all' && status !== 'recovered' && r.status !== status) return false;
    if (q) {
      const hay = `${r.appName} ${r.title} ${r.category}`.toLowerCase();
      if (!hay.includes(q)) return false;
    }
    return true;
  });
}

// ---------- 深链解析（CARD-MON-02） ----------

/**
 * /alarms?groupId={id} 深链解析：groupId 为纯数字串时原样返回（组过滤预置）；
 * 无参、空或非数字返回 null=不过滤（与后端 Search groupId<0 不加条件的语义一致）。
 * 入参为路由 query（值可能是 string | string[] | null），宽松解析不抛错。
 * 走查缺陷②（2026-11-07 增量裁定）：groupId 全链 string 透传、不经 Number 往返——
 * 现网组 Id 是自增小整数无精度风险，但深链与行 Id/导出/过滤同链同形（C18），组主键若改雪花零改动。
 */
export function parseAlarmsQuery(query: Record<string, unknown>): string | null {
  const raw = query.groupId;
  if (raw === undefined || raw === null) return null;
  const s = String((Array.isArray(raw) ? raw[0] : raw) ?? '').trim();
  return /^\d+$/.test(s) ? s : null;
}

// ---------- 组下拉数据源（CARD-MON-02） ----------

/** 告警组下拉选项（过滤条 alarms-filter-group；来自 GET /Monitors/AlarmGroup） */
export interface AlarmGroupOption {
  /** 组 Id：字符串透传（走查缺陷②，id 类字段不经 num()；下拉 value/比较与过滤链同形） */
  id: string;
  name: string;
}

/** 告警组下拉数据源：GET /Monitors/AlarmGroup?PageSize=200（与 alarmGroup.logic 同款取数形态） */
export async function fetchAlarmGroupOptions(
  request: Req,
  projectId?: number | null,
): Promise<{ options: AlarmGroupOption[]; error: string }> {
  try {
    const res = await request({
      url: '/Monitors/AlarmGroup',
      method: 'GET',
      params: withProject({ PageSize: 200 }, projectId),
    });
    const options = listOf(res)
      .map((g) => ({ id: str(pick(g, 'Id', 'id')), name: str(pick(g, 'Name', 'name')) }))
      .filter((g) => g.id !== '');
    return { options, error: '' };
  } catch (e) {
    return { options: [], error: e instanceof Error && e.message ? e.message : '告警组列表加载失败' };
  }
}

// ---------- 取数（request 注入） ----------

/** 显式 projectId：非空时合并进 params（与全局拦截器同向；对 AlarmRecord 实际无过滤效果，仅规范一致） */
function withProject(params: Record<string, unknown>, projectId?: number | null): Record<string, unknown> {
  return projectId ? { ...params, projectId } : { ...params };
}

/**
 * 端到端取数：默认并行拉当前告警（status=1 报警中）+ 历史告警（全部）；已恢复视图（opts.recordStatus=2）
 * 只拉记录路 status=2、不并历史（CARD-MON-03 定稿裁定 3）。
 * 任一请求失败 → 返回 error、rows 为空（不静默空态，由页面给可见错误 + 重试）。
 *
 * filter（CARD-MON-02 服务端过滤，随选中组/时间带参重取，两个 Search 均支持）：
 *   groupId 纯数字串才携带（空/缺省=不过滤，与后端 groupId<0 同语义）；dtStart/dtEnd 非空串才携带；
 *   不传 filter（或全空）= 请求不带任何过滤参数（全量语义，C12）。
 */
export interface AlarmFetchFilter {
  groupId?: string | null;
  dtStart?: string;
  dtEnd?: string;
}

/** 组/时间过滤 → 请求参数（空值不入参，C12：两者都未选时请求不带 dtStart/dtEnd） */
export function alarmFilterParams(filter?: AlarmFetchFilter): Record<string, unknown> {
  const params: Record<string, unknown> = {};
  if (!filter) return params;
  const g = filter.groupId;
  if (g !== undefined && g !== null && /^\d+$/.test(g)) params.groupId = g;
  const start = (filter.dtStart ?? '').trim();
  if (start) params.dtStart = start;
  const end = (filter.dtEnd ?? '').trim();
  if (end) params.dtEnd = end;
  return params;
}

/** 取数选项（CARD-MON-03）：recordStatus=2 时只拉记录路（已恢复视图），不并历史路 */
export interface AlarmOpts {
  /** 记录路 status：1=报警中（默认，与 /now 同口径）+ 历史合并；2=已恢复（仅记录路，避免与通知记录重复） */
  recordStatus?: 1 | 2;
}

export async function fetchAlarms(
  request: Req,
  projectId?: number | null,
  filter?: AlarmFetchFilter,
  opts?: AlarmOpts,
): Promise<{ rows: AlarmRow[]; error: string }> {
  try {
    const filterParams = alarmFilterParams(filter);
    const recordStatus = opts?.recordStatus === 2 ? 2 : 1;
    const recordJob = request({
      url: '/Monitors/AlarmRecord',
      method: 'GET',
      params: withProject({ status: recordStatus, PageSize: 200, ...filterParams }, projectId),
    });
    // 已恢复视图只拉记录路：AlarmHistory 是通知记录（另一实体），并显会与生命周期记录重复
    const historyJob =
      recordStatus === 2
        ? null
        : request({
            url: '/Monitors/AlarmHistory',
            method: 'GET',
            params: withProject({ PageSize: 200, ...filterParams }, projectId),
          });
    const [recordRes, historyRes] = await Promise.all([recordJob, historyJob]);
    return {
      rows: toAlarmRows(listOf(recordRes), historyRes ? listOf(historyRes) : []),
      error: '',
    };
  } catch (e) {
    return { rows: [], error: e instanceof Error && e.message ? e.message : '告警加载失败' };
  }
}
