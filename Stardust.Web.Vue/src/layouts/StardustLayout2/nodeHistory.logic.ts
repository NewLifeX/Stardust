/**
 * nodeHistory.logic：节点抽屉「历史」页签取数与展示映射（CARD-NODE-09，2026-11-11）。
 *
 * 后端事实（NodeHistoryController.cs READ-ONLY，2026-11-11 勘定）：
 *  - 列表：GET /Nodes/NodeHistory（NodesEntityController<NodeHistory> 标准列表路由
 *    /api/[area]/[controller]，经 API_HOST 补 /api 前缀；返回业务体 {page, code, data:[实体数组]}）；
 *  - Search 参数（WebApi :45-50）：nodeId(int)/action/success（p["success"]?.ToBoolean()，
 *    故前端传 'true'/'false' 字符串）/dtStart/dtEnd；默认排序 Id 降序（:52）——前端不传 sort；
 *  - 字段（camel/Pascal 两态兼容）：id/nodeId/nodeName/action/success(bool)/remark/traceId/
 *    createTime/createIP；
 *  - 展示契约（迁移页 index.vue:35-43 COLUMNS 同集）：7 列=节点/操作/成功/内容/追踪/创建时间/
 *    创建地址；「成功」列显示「是/否」（迁移页 :132 同款）；「城市」列不做（C07 如实，两态均无）；
 *  - 纯只读页签：无任何写操作按钮，不涉高危 GuardModal（NODE-08 红线不触发）。
 *  - 抽屉头部「在迁移页打开」：新窗口 /Nodes/Node?Id={nodeId}（与迁移页节点列下钻同目标）。
 */
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel';

type Rec = Record<string, unknown>;

// ---------- 基础容错取值 ----------

function pick(r: Rec | undefined | null, ...keys: string[]): unknown {
  if (!r) return undefined;
  for (const k of keys) {
    const v = r[k];
    if (v !== undefined && v !== null && v !== '') return v;
  }
  return undefined;
}
function str(v: unknown): string {
  if (v === undefined || v === null) return '';
  return String(v).trim();
}
function listOf(res: { data?: unknown } | undefined): Rec[] {
  const d = res?.data;
  return Array.isArray(d) ? (d as Rec[]) : [];
}
/** 宽松解析时间（兼容 'YYYY-MM-DD HH:mm:ss' 与 ISO 'T'），失败返回 0 */
function toMs(v: unknown): number {
  const s = str(v);
  if (!s) return 0;
  const t = Date.parse(s.replace(' ', 'T'));
  return Number.isFinite(t) ? t : 0;
}
/** 格式化到秒（迁移页同款：replace('T',' ').slice(0,19)；解析失败回退原串） */
function fmtTimeSec(v: unknown): string {
  const s = str(v);
  if (!s) return '—';
  const ms = toMs(s);
  if (!ms) return s;
  const d = new Date(ms);
  const p = (n: number): string => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
}
function errText(e: unknown, fallback: string): string {
  return e instanceof Error && e.message ? e.message : fallback;
}
/** 业务成功判据（nodeOnline.logic.ts:268-270 同款）：code 0/200 或 success===true */
function isBizOk(res: unknown): boolean {
  const r = res as Rec | undefined | null;
  if (!r) return false;
  const code = (r as Rec).code;
  if (code === 0 || code === 200) return true;
  return (r as Rec).success === true;
}
/** 业务失败提示：优先后端 message，兜底固定文案 */
function bizError(res: unknown, fallback: string): string {
  const r = res as Rec | undefined | null;
  const m = r?.message ?? (r as Rec | undefined | null)?.msg;
  return (m !== undefined && m !== null && m !== '' ? String(m) : fallback);
}

// ---------- 类型 ----------

/** 「历史」页签行（7 列：节点/操作/成功/内容/追踪/创建时间/创建地址） */
export interface NodeHistoryRow {
  id: number | null;
  /** 节点列：抽屉内恒为当前节点名（后端 nodeName 仅兜底） */
  nodeName: string;
  action: string;
  /** 「是/否」（bool → 中文，迁移页 :132 同款） */
  successText: '是' | '否';
  remark: string;
  traceId: string;
  createTime: string;
  createIP: string;
}

/** 页签内容状态（三态+ok）：加载中 / 失败重试 / 暂无 / 列表 */
export type HistoryListState = 'loading' | 'error' | 'empty' | 'ok';

/** success 过滤值：''=全部，否则 'true'/'false' 字符串（后端 ToBoolean 直读） */
export type SuccessFilter = '' | 'true' | 'false';

// ---------- 纯函数 ----------

/** 实体数组 → 7 列视图行（宽容大小写/驼峰差异；id 数字，空缺 null） */
export function toNodeHistoryRows(list: Rec[], nodeName: string): NodeHistoryRow[] {
  const rows: NodeHistoryRow[] = [];
  for (const r of list) {
    const rawId = pick(r, 'id', 'Id');
    const rawSuccess = pick(r, 'success', 'Success');
    rows.push({
      id: rawId !== undefined ? Number(rawId) : null,
      nodeName: nodeName || str(pick(r, 'nodeName', 'NodeName')),
      action: str(pick(r, 'action', 'Action')),
      successText: rawSuccess === true || rawSuccess === 'true' ? '是' : '否',
      remark: str(pick(r, 'remark', 'Remark')),
      traceId: str(pick(r, 'traceId', 'TraceId')),
      createTime: fmtTimeSec(pick(r, 'createTime', 'CreateTime')),
      createIP: str(pick(r, 'createIP', 'CreateIP', 'createIp', 'CreateIp')),
    });
  }
  return rows;
}

/** 三态派生：loading 优先 → error → 空 → 有数据 */
export function historyListState(loading: boolean, error: string, rows: NodeHistoryRow[]): HistoryListState {
  if (loading) return 'loading';
  if (error) return 'error';
  if (rows.length === 0) return 'empty';
  return 'ok';
}

// ---------- 取数 ----------

/**
 * 抽屉「历史」tab：按 nodeId 拉该节点操作历史（pageSize=50，只读；排序不传参——后端 Id 降序兜底）；
 * success 仅在 'true'/'false' 时携带，'' 不带参=后端全部。
 */
export async function fetchNodeHistory(
  request: Req,
  nodeId: number,
  nodeName: string,
  success: SuccessFilter,
  projectId?: number | null,
): Promise<{ rows: NodeHistoryRow[]; error: string }> {
  try {
    const params: Record<string, unknown> = { nodeId, pageSize: 50 };
    if (success === 'true' || success === 'false') params.success = success;
    if (projectId != null && Number.isFinite(projectId) && projectId >= 0) params.projectId = projectId;
    const res = await request({ url: '/Nodes/NodeHistory', method: 'GET', params });
    if (!isBizOk(res)) return { rows: [], error: bizError(res, '历史记录加载失败') };
    return { rows: toNodeHistoryRows(listOf(res), nodeName), error: '' };
  } catch (e) {
    return { rows: [], error: errText(e, '历史记录加载失败') };
  }
}
