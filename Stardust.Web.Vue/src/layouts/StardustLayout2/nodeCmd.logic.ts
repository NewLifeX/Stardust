/**
 * nodeCmd.logic：节点抽屉「命令」页签取数与三态派生（NODE-06 B1，2026-11-09）。
 *
 * 后端事实（2026-11-09 实测 6681 生产载荷，nodeId=1 共 2 条）：
 *  - 列表：GET /Nodes/NodeCommand?nodeId=&pageSize=（Cube 列表路由 /api/[area]/[controller]，
 *    经 API_HOST 补 /api 前缀；返回业务体 {page:{totalCount}, code:0, data:[实体数组]}，data 即数组）；
 *  - 字段（camelCase）：id/nodeID/command/argument/startTime/expire/status/times/result/traceId/
 *    createUser/createTime/updateTime —— 无 nodeName，节点列由抽屉当前节点名注入；
 *  - 下发：POST /Nodes/NodeCommand/Send（EntityAuthorize((PermissionFlags)16)，信封 Code 400/500）。
 *    本页签只读展示，不下发按钮（NODE-06 卡裁定：列表页不暴露下发入口）。
 *  - 追踪列 C06：有 traceId 链契约页 /trace/{traceId}（与 AppInstances/DeployHistory 同款），无值「—」。
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
/** 宽松解析时间（兼容 'YYYY-MM-DD HH:mm:ss' 与 ISO），失败返回 0 */
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
function errText(e: unknown, fallback: string): string {
  return e instanceof Error && e.message ? e.message : fallback;
}

// ---------- 类型 ----------

/** 「命令」页签行（9 列：编号/节点/命令/参数/结果/追踪/创建者/创建时间/更新时间） */
export interface NodeCmdRow {
  id: number | null;
  /** 节点列：列表载荷无 nodeName，用抽屉当前节点名 */
  nodeName: string;
  command: string;
  argument: string;
  result: string;
  traceId: string;
  createUser: string;
  createTime: string;
  updateTime: string;
}

/** 页签内容状态（三态+ok）：加载中 / 失败重试 / 暂无 / 列表 */
export type CmdListState = 'loading' | 'error' | 'empty' | 'ok';

// ---------- 纯函数 ----------

/** 实体数组 → 9 列视图行（宽容大小写/驼峰差异；id 数字，空缺 null） */
export function toNodeCmdRows(list: Rec[], nodeName: string): NodeCmdRow[] {
  const rows: NodeCmdRow[] = [];
  for (const r of list) {
    const rawId = pick(r, 'id', 'Id');
    rows.push({
      id: rawId !== undefined ? Number(rawId) : null,
      nodeName: nodeName || str(pick(r, 'nodeName', 'NodeName')),
      command: str(pick(r, 'command', 'Command')),
      argument: str(pick(r, 'argument', 'Argument')),
      result: str(pick(r, 'result', 'Result')),
      traceId: str(pick(r, 'traceId', 'TraceId')),
      createUser: str(pick(r, 'createUser', 'CreateUser')),
      updateTime: fmtTime(toMs(pick(r, 'updateTime', 'UpdateTime'))),
      createTime: fmtTime(toMs(pick(r, 'createTime', 'CreateTime'))),
    });
  }
  return rows;
}

/** 三态派生：loading 优先 → error → 空 → 有数据 */
export function cmdListState(loading: boolean, error: string, rows: NodeCmdRow[]): CmdListState {
  if (loading) return 'loading';
  if (error) return 'error';
  if (rows.length === 0) return 'empty';
  return 'ok';
}

// ---------- 取数 ----------

/** 抽屉「命令」tab：按 nodeId 拉该节点最近命令（pageSize=20，只读；projectId 非空时透传） */
export async function fetchNodeCommands(
  request: Req,
  nodeId: number,
  nodeName: string,
  projectId?: number | null,
): Promise<{ rows: NodeCmdRow[]; error: string }> {
  try {
    const params: Record<string, unknown> = { nodeId, pageSize: 20 };
    if (projectId != null && Number.isFinite(projectId) && projectId >= 0) params.projectId = projectId;
    const res = await request({ url: '/Nodes/NodeCommand', method: 'GET', params });
    return { rows: toNodeCmdRows(listOf(res), nodeName), error: '' };
  } catch (e) {
    return { rows: [], error: errText(e, '命令记录加载失败') };
  }
}
