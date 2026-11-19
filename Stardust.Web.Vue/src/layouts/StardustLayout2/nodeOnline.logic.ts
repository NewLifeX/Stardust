/**
 * nodeOnline.logic：/nodes 批量操作卡取数与契约派生（CARD-NODE-11，2026-11-09）。
 *
 * 后端事实（Stardust.Web/Areas/Nodes/Controllers/NodeOnlineController.cs，2026-11-09 预读，零后端改动）：
 *  - 批量五动作均 GET，keys=勾选的**在线会话 Id** 逗号串（后端 SelectKeys → NodeOnline.FindById 按主键取行，
 *    非节点 Node.Id）：
 *      CheckUpgrade  16  /Nodes/NodeOnline/CheckUpgrade          600s retry3（:94）
 *      SyncTime      16  /Nodes/NodeOnline/SyncTime              600s retry5（:183）
 *      Restart       32  /Nodes/NodeOnline/Restart               600s retry5（:212）
 *      Reboot        64  /Nodes/NodeOnline/Reboot                600s retry5（:241）
 *      Execute       16  /Nodes/NodeOnline/Execute?command=&argument=  300s retry0，query 参（:270）
 *  - 汇总契约（:110/:199/:228/:257/:289）：
 *      首行「操作成功！下发{N}个，响应{S}个，超时{T}个」
 *      + 每节点段「\n{name}: {reply.Data ?? "(无返回数据)"}」
 *      UpgradeTo 路径（:170，前端无入口）首行带「，跳过{K}个」尾段 → 解析容错兼容。
 *  - 在线索引：/nodes 勾选的是 Node.Id，批量 keys 需 NodeOnline.Id →
 *    GET /Nodes/NodeOnline 行按 NodeID 取 updateTime 最新一条建映射（不造数，如实映射）。
 *  - 权限如实：前端无权限位数据源（旅程 NodeOnline.md §2「未在源码确认」），不做前端权限禁用；
 *    权限不足由后端拒绝（信封 code 400 / HTTP 错误），经失败条呈现（迁移页 B1 同款）。
 *
 * 交互规范（AGENTS §11）：动作进行中按钮 loading、五动作整体禁用防重复；失败可见（失败条）；
 * 结果契约展开行替代 toast（保留逐节点回复信息）。
 */
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel';

type Rec = Record<string, unknown>;

// ---------- 基础容错取值 ----------

function num(v: unknown): number | null {
  if (v === undefined || v === null || v === '') return null;
  const n = Number(v);
  return Number.isFinite(n) ? n : null;
}
function str(v: unknown): string {
  if (v === undefined || v === null) return '';
  return String(v).trim();
}
/** 宽松解析时间（兼容 'YYYY-MM-DD HH:mm:ss' 与 ISO），失败返回 0 */
function toMs(v: unknown): number {
  const s = str(v);
  if (!s) return 0;
  const t = Date.parse(s.replace(' ', 'T'));
  return Number.isFinite(t) ? t : 0;
}
function errText(e: unknown, fallback: string): string {
  return e instanceof Error && e.message ? e.message : fallback;
}

// ---------- 批量动作定义 ----------

/** 批量动作键（与后端 Action 同名） */
export type BatchActionKey = 'CheckUpgrade' | 'SyncTime' | 'Restart' | 'Reboot' | 'Execute';

/** 批量动作定义：端点（相对无前缀路径，request 自动拼 baseURL）、后端权限位（记录用）、是否二次确认 */
export interface BatchActionDef {
  key: BatchActionKey;
  label: string;
  path: string;
  /** 后端 EntityAuthorize 权限位（NodeOnlineController.cs）：16/32/64；前端无权限位数据源，仅记录 */
  perm: number;
  /** 需 GuardModal 二次确认（定稿：重启系统/执行命令） */
  guard: boolean;
}

/** 批量五动作（顺序即按钮顺序；CheckUpgrade「node/upgrade」600s retry3） */
export const BATCH_ACTIONS: BatchActionDef[] = [
  { key: 'CheckUpgrade', label: '检查更新', path: '/Nodes/NodeOnline/CheckUpgrade', perm: 16, guard: false },
  { key: 'SyncTime', label: '同步时间', path: '/Nodes/NodeOnline/SyncTime', perm: 16, guard: false },
  { key: 'Restart', label: '重启服务', path: '/Nodes/NodeOnline/Restart', perm: 32, guard: false },
  { key: 'Reboot', label: '重启系统', path: '/Nodes/NodeOnline/Reboot', perm: 64, guard: true },
  { key: 'Execute', label: '执行命令', path: '/Nodes/NodeOnline/Execute', perm: 16, guard: true },
];

/** 按键取动作定义 */
export function batchAction(key: BatchActionKey): BatchActionDef {
  const a = BATCH_ACTIONS.find((x) => x.key === key);
  if (!a) throw new Error(`未知批量动作：${key}`);
  return a;
}

// ---------- 在线索引 ----------

/** 在线快照行（归一：NodeOnline.Id 会话主键 + NodeID 节点主键） */
export interface OnlineRow {
  id: number;
  nodeId: number;
  name: string;
  updateTimeMs: number;
}

/** GET /Nodes/NodeOnline 载荷数组 → 归一行（宽容大小写/驼峰差异） */
export function toOnlineRows(list: Rec[]): OnlineRow[] {
  const rows: OnlineRow[] = [];
  for (const r of list) {
    const id = num(r.id ?? (r as Rec).Id);
    const nodeId = num(r.nodeID ?? r.nodeId ?? (r as Rec).NodeID ?? (r as Rec).NodeId);
    if (id === null || nodeId === null) continue;
    rows.push({
      id,
      nodeId,
      name: str(r.name ?? (r as Rec).Name),
      updateTimeMs: toMs(r.updateTime ?? (r as Rec).UpdateTime),
    });
  }
  return rows;
}

/** 节点 NodeID → 该节点 updateTime 最新一条在线会话 Id（同节点多行历史取最新） */
export function latestOnlineByNode(rows: OnlineRow[]): Map<number, number> {
  const m = new Map<number, number>();
  for (const r of rows) {
    const prev = m.get(r.nodeId);
    if (prev === undefined || r.updateTimeMs >= 0) {
      // 逐条比较：仅当当前行更新（或旧值无时间）才覆盖
      const prevRow = rows.find((x) => x.nodeId === r.nodeId && x.id === prev);
      if (prev === undefined || r.updateTimeMs >= (prevRow?.updateTimeMs ?? -1)) m.set(r.nodeId, r.id);
    }
  }
  return m;
}

/** 勾选 Node.Id 集合 → keys 逗号串 + 未能映射（未上线/无会话）的 Node.Id 列表 */
export function buildKeys(
  selected: number[],
  idx: Map<number, number>,
): { keys: string; missing: number[] } {
  const ids: number[] = [];
  const missing: number[] = [];
  for (const n of selected) {
    const id = idx.get(n);
    if (id === undefined) missing.push(n);
    else ids.push(id);
  }
  return { keys: ids.join(','), missing };
}

// ---------- 汇总契约解析 ----------

/** 逐节点回复段 */
export interface BatchNodeReply {
  name: string;
  text: string;
}

/** 汇总契约解析结果 */
export interface BatchSummary {
  sent: number;
  replied: number;
  timeout: number;
  /** 「跳过{K}个」尾段（UpgradeTo 变体）；标准契约无 → null */
  skipped: number | null;
  nodeReplies: BatchNodeReply[];
}

// 首行：操作成功！下发{N}个，响应{S}个，超时{T}个[，跳过{K}个]
const SUMMARY_HEAD =
  /^操作成功！下发(\d+)个，响应(\d+)个，超时(\d+)个(?:，跳过(\d+)个)?$/;
// 每节点段：{name}: {data}（首个冒号切分；后端 :294 分隔符为 ': '）
const NODE_REPLY = /^([^:：]+)[:：]\s*(.*)$/;

/**
 * 后端汇总消息 → 结构化契约。非契约文本（失败消息等）返回 null，由调用方按原文呈现失败态。
 * 兼容「跳过{K}个」尾段（UpgradeTo 路径 ：170）。
 */
export function parseBatchSummary(msg: string): BatchSummary | null {
  const s = str(msg);
  if (!s) return null;
  const lines = s.split('\n');
  const m = SUMMARY_HEAD.exec(lines[0].trim());
  if (!m) return null;
  const nodeReplies: BatchNodeReply[] = [];
  for (let i = 1; i < lines.length; i++) {
    const line = lines[i].trim();
    if (!line) continue;
    const r = NODE_REPLY.exec(line);
    if (r) nodeReplies.push({ name: r[1].trim(), text: r[2].trim() });
  }
  return {
    sent: Number(m[1]),
    replied: Number(m[2]),
    timeout: Number(m[3]),
    skipped: m[4] !== undefined ? Number(m[4]) : null,
    nodeReplies,
  };
}

// ---------- 输入校验与确认框文案 ----------

/**
 * 批量动作入口校验：返回警告文案（''=放行）。
 * 旅程定稿文案：未勾选「请先勾选节点」；执行命令未输入「请输入命令」。
 */
export function validateBatchInput(selectedCount: number, command: string): string {
  if (selectedCount <= 0) return '请先勾选节点';
  if (!str(command)) return '请输入命令';
  return '';
}

/** GuardModal 确认框文案（label 短语 + detail 后果）；非 guard 动作 label='' */
export function guardTexts(
  actionKey: BatchActionKey,
  count: number,
  command: string,
  argument: string,
): { label: string; detail: string } {
  switch (actionKey) {
    case 'Reboot':
      return {
        label: '重启系统',
        detail: `将对勾选的 ${count} 台节点下发系统重启指令，节点将短暂离线后自动恢复。确认？`,
      };
    case 'Execute':
      return {
        label: '执行命令',
        detail: `将在勾选的 ${count} 台节点上执行「${str(command) || '(未输入命令)'}」（参数：${
          str(argument) || '无'
        }）。确认？`,
      };
    default:
      return { label: '', detail: '' };
  }
}

// ---------- 在线索引取数 ----------

/** 拉取在线快照建 NodeID→会话 Id 索引（projectId 非空时透传，与 /nodes 其它请求一致） */
export async function fetchOnlineIndex(
  request: Req,
  projectId?: number | null,
): Promise<{ index: Map<number, number>; error: string }> {
  try {
    const params: Record<string, unknown> = {};
    if (projectId != null && Number.isFinite(projectId) && projectId >= 0) params.projectId = projectId;
    const res = await request({ url: '/Nodes/NodeOnline', method: 'GET', params });
    const d = res?.data;
    const list: Rec[] = Array.isArray(d) ? (d as Rec[]) : [];
    return { index: latestOnlineByNode(toOnlineRows(list)), error: '' };
  } catch (e) {
    return { index: new Map(), error: errText(e, '在线快照加载失败') };
  }
}

// ---------- 批量动作请求 ----------

/** 批量动作结果：ok=信封成功；message=后端契约原文；error=失败原因（三态反馈） */
export interface BatchActionResult {
  ok: boolean;
  message: string;
  error: string;
}

/**
 * 执行批量动作：GET {path}?keys={keys}&extra。
 * 信封判定与迁移页一致（index.vue:178）：code 0/200 或 success → 成功；
 * code 400（HTTP 200 锁定语义）与异常均归失败，error 取 message / Error.message。
 */
export async function runBatchAction(
  request: Req,
  path: string,
  keys: string,
  extra?: Record<string, unknown>,
): Promise<BatchActionResult> {
  try {
    const params: Record<string, unknown> = { keys, ...extra };
    const res = await request({ url: path, method: 'GET', params });
    const r = res as { code?: unknown; message?: unknown; success?: unknown } | undefined;
    const ok = !!r && (r.code === 0 || r.code === 200 || r.success === true);
    if (ok) return { ok: true, message: str(r?.message), error: '' };
    return { ok: false, message: '', error: str(r?.message) || '操作失败' };
  } catch (e) {
    return { ok: false, message: '', error: errText(e, '操作失败') };
  }
}
