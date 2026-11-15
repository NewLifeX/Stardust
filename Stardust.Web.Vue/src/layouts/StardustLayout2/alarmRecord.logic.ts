// 报警记录维护纯逻辑（CARD-MON-03，无 Vue 依赖，request 由调用方注入，便于单测）。
//
// 表单字段集（已核源码；WebApi 无通用 GetFields 端点——2026-11-07 冒烟实证：
//   /Monitors/AlarmRecord/GetFields → 404、/api/Monitors/AlarmRecord/GetFields → code=500
//   「要查看的数据[GetFields]不存在！」，与 AlarmGroup 同判例，以 Controller/实体源码为准）：
//   可写集 = Name/GroupId/Category/Action/Status/StartTime/EndTime/Duration/Times/Content
//   （AlarmRecordController.cs:18-19 对 Add/EditFormFields 移除 AckTime/AckUser/SilenceTime）。
// 校验（报警记录.Biz.cs:44）：Name 必填（空抛 ArgumentNullException）→ 前端「名称不能为空」；
//   Content 列宽 2000、超长服务端截断（Biz.cs:46-47）→ 前端 maxlength=2000 兜底。
// 请求（WebEntityController 标准，主键在路径，body 不带 id——同 appTracer.logic 先例）：
//   编辑 PUT /Monitors/AlarmRecord/{id}；删除 DELETE /Monitors/AlarmRecord/{id}。
// 本卡只做编辑/删除（记录由系统产生，无新增入口；定稿裁定 1）；AlarmHistory 通知记录只读不在本卡。
// CSV（定稿裁定 2）：前端导出当前筛选结果（最多最近 200 条已加载行），列=当前表格 7 列+组名，
//   表头中文；组名经告警组下拉数据源解析，取不到显 GroupId（定稿 5，不造数）；带 \uFEFF BOM。
import type { Req, ReqOptions } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import type { AlarmRow } from './alarms.logic.ts';

type Rec = Record<string, unknown>;

// ---------- 类型 ----------

/** 报警状态枚举（AlarmStatuses：报警记录.Biz.cs:12-19，Alarming=1 报警中 / Recovered=2 已恢复） */
export type AlarmRecordStatus = 1 | 2;

/** 编辑抽屉表单（可写字段集见头注释；时间用 datetime-local 输入形态） */
export interface AlarmRecordForm {
  /**
   * 记录 Id：字符串原串（走查缺陷②，后端雪花 Id 已字符串化，>2^53 不经 Number）；
   * 本卡只编辑既有记录，无新增入口，PUT/DELETE 路径直接拼原串。
   */
  id: string;
  name: string;
  /**
   * 所属告警组 Id：字符串透传（走查缺陷②，雪花 id 全链 string 裁定，与 AlarmRow.groupId 同形）；
   * ''=不分组（空串哨兵，alarmRecordBody 落 0 对齐后端 GroupId 列 int）。
   */
  groupId: string;
  category: string;
  /** 操作：报警对象，如应用名或节点名 */
  action: string;
  status: AlarmRecordStatus;
  /** datetime-local 形态 'YYYY-MM-DDTHH:mm'，空串=未设 */
  startTime: string;
  endTime: string;
  /** Duration 原始秒数 */
  durationSec: number;
  times: number;
  content: string;
}

// ---------- 形态换算 ----------

/** ms → datetime-local 输入值（'YYYY-MM-DDTHH:mm'）；0/无效 → '' */
export function toLocalInput(ms: number): string {
  if (!Number.isFinite(ms) || ms <= 0) return '';
  const d = new Date(ms);
  const p = (n: number): string => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
}

/** datetime-local（'YYYY-MM-DDTHH:mm[:ss]'）→ 后端 ToDateTime 可解析的 'YYYY-MM-DD HH:mm:ss'；空串原样 */
export function normDateTime(v: string): string {
  const s = (v ?? '').trim();
  if (!s) return '';
  const t = s.replace('T', ' ');
  return /^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$/.test(t) ? `${t}:00` : t;
}

// ---------- 表单映射 ----------

/** 列表行 → 编辑表单初值（仅记录行可编辑：active/recovered；status 映射 recovered=2、active=1） */
export function alarmRecordFormFromRow(row: AlarmRow): AlarmRecordForm {
  return {
    id: row.id,
    name: row.title,
    groupId: row.groupId,
    category: row.category,
    action: row.action,
    status: row.status === 'recovered' ? 2 : 1,
    startTime: toLocalInput(row.startMs),
    endTime: toLocalInput(row.endTimeMs ?? 0),
    durationSec: row.durationSec ?? 0,
    times: row.times,
    content: row.content,
  };
}

/** 表单校验（对齐实体 Valid：Name 必填）；通过返回空串 */
export function validateAlarmRecordForm(form: AlarmRecordForm): string {
  if (!form.name.trim()) return '名称不能为空';
  return '';
}

// ---------- 请求体与写操作 ----------

/** 表单 → PUT body（主键在路径不入 body；时间归一 'YYYY-MM-DD HH:mm:ss'，camelCase 同请求层先例） */
export function alarmRecordBody(form: AlarmRecordForm): Rec {
  return {
    name: form.name.trim(),
    // GroupId：无组（''）落 0 对齐后端 GroupId 列 int（0=无组，同改前 wire 行为）；
    // 有组传原串由后端 lenient 绑定回 int（走查缺陷②：id 类字段前端不再经 Number 往返）
    groupId: form.groupId === '' ? 0 : form.groupId,
    category: form.category.trim(),
    action: form.action.trim(),
    status: form.status,
    startTime: normDateTime(form.startTime),
    endTime: normDateTime(form.endTime),
    duration: form.durationSec,
    times: form.times,
    content: form.content,
  };
}

/** 保存编辑：PUT /Monitors/AlarmRecord/{id}（EntityController.Update：主键在路径，body 部分更新） */
export async function saveAlarmRecord(request: Req, form: AlarmRecordForm): Promise<void> {
  await request({
    url: `/Monitors/AlarmRecord/${form.id}`,
    method: 'PUT',
    data: alarmRecordBody(form),
  } as ReqOptions & { data: Record<string, unknown> });
}

/** 删除：DELETE /Monitors/AlarmRecord/{id}（EntityController.Delete 主键在路径；id=雪花原串，走查缺陷②） */
export async function removeAlarmRecord(request: Req, id: string): Promise<void> {
  await request({ url: `/Monitors/AlarmRecord/${id}`, method: 'DELETE' });
}

// ---------- CSV 导出（C17） ----------

/** 状态 → 展示文案（与 AlarmsList Tag 逐字一致） */
export function alarmStatusLabel(status: AlarmRow['status']): string {
  if (status === 'active') return '未处理';
  if (status === 'recovered') return '已恢复';
  return '已处理';
}

/** CSV 列头：当前表格 7 列 + 组名（C17） */
export const ALARM_CSV_HEADERS = [
  '级别',
  '告警标题',
  '对象',
  '状态',
  '持续时间',
  '通知次数',
  '开始时间',
  '告警组',
] as const;

function csvCell(v: string): string {
  return `"${v.replace(/"/g, '""')}"`;
}

/**
 * 当前筛选结果 → CSV 文本（C17：内容=当前过滤结果，行集与列集一致，表头中文，含组名列）。
 * 组名经 resolveGroupName（告警组下拉数据源）解析；取不到显 GroupId（定稿 5），无组=空。
 * 首字符带 \uFEFF BOM（Excel 打开中文不乱码）；行分隔 \r\n；单元格全量引号包裹（逗号/引号安全）。
 */
export function buildAlarmCsv(
  rows: AlarmRow[],
  resolveGroupName: (groupId: string) => string,
): string {
  const lines: string[] = [ALARM_CSV_HEADERS.map((h) => csvCell(h)).join(',')];
  for (const r of rows) {
    const group = r.groupId !== '' ? resolveGroupName(r.groupId) : '';
    lines.push(
      [
        csvCell(r.level === 'critical' ? 'Critical' : r.level === 'warning' ? 'Warning' : 'Info'),
        csvCell(r.title),
        csvCell(r.appName),
        csvCell(alarmStatusLabel(r.status)),
        csvCell(r.durationText),
        csvCell(String(r.times)),
        csvCell(r.startTime),
        csvCell(group),
      ].join(','),
    );
  }
  return `\uFEFF${lines.join('\r\n')}`;
}
