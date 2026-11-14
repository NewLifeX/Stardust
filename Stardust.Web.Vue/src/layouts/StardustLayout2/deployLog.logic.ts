// 部署节点「文件日志」：经 StarAgent deploy/log/* 读节点本地日志（见 Doc/DEPLOY-1-节点应用日志.md）
import { okRes } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import { requestFailText } from './deploy.logic.ts';

type Rec = Record<string, unknown>;

function pick(r: Rec, ...keys: string[]): unknown {
  for (const k of keys) {
    const v = r[k];
    if (v !== undefined && v !== null) return v;
  }
  return undefined;
}
function str(v: unknown): string {
  return v === undefined || v === null ? '' : String(v);
}
function num(v: unknown): number {
  const n = Number(v);
  return Number.isFinite(n) ? n : 0;
}
function bool(v: unknown): boolean {
  if (typeof v === 'boolean') return v;
  const s = str(v).trim().toLowerCase();
  return s === 'true' || s === '1';
}

export interface DeployLogFileRow {
  name: string;
  size: number;
  lastWriteUtc: string;
}

export interface DeployLogTailView {
  content: string;
  offset: number;
  fileSize: number;
  truncated: boolean;
}

function mapFiles(raw: unknown): DeployLogFileRow[] {
  const list = (raw as { files?: unknown[] })?.files ?? (Array.isArray(raw) ? raw : []);
  if (!Array.isArray(list)) return [];
  return list.map((item) => {
    const r = item as Rec;
    return {
      name: str(pick(r, 'Name', 'name')),
      size: num(pick(r, 'Size', 'size')),
      lastWriteUtc: str(pick(r, 'LastWriteUtc', 'lastWriteUtc')),
    };
  }).filter((f) => f.name);
}

export function parseLogTailPayload(data: unknown): DeployLogTailView {
  const r = (data ?? {}) as Rec;
  return {
    content: str(pick(r, 'Content', 'content')),
    offset: num(pick(r, 'Offset', 'offset')),
    fileSize: num(pick(r, 'FileSize', 'fileSize')),
    truncated: bool(pick(r, 'Truncated', 'truncated')),
  };
}

/** GET /Deployment/AppDeployNode/LogFiles?id= */
export async function fetchDeployLogFiles(
  request: Req,
  deployNodeId: number,
): Promise<{ ok: boolean; message: string; files: DeployLogFileRow[] }> {
  if (!deployNodeId || deployNodeId <= 0) return { ok: false, message: '缺少部署节点编号', files: [] };
  try {
    const res = await request({
      url: '/Deployment/AppDeployNode/LogFiles',
      method: 'GET',
      params: { id: deployNodeId },
    });
    const envelope = okRes(res);
    if (!envelope.ok) {
      const msg = envelope.message || str((res as Rec)?.message) || '日志文件列表加载失败';
      return { ok: false, message: msg, files: [] };
    }
    const data = (res as { data?: unknown })?.data;
    return { ok: true, message: '', files: mapFiles(data) };
  } catch (e) {
    return { ok: false, message: requestFailText(e, '日志文件列表加载失败'), files: [] };
  }
}

/** GET /Deployment/AppDeployNode/LogTail?id=&file=&offset=&maxBytes= */
export async function fetchDeployLogTail(
  request: Req,
  deployNodeId: number,
  file: string,
  offset = 0,
  maxBytes = 262144,
): Promise<{ ok: boolean; message: string; tail: DeployLogTailView | null }> {
  if (!deployNodeId || deployNodeId <= 0) return { ok: false, message: '缺少部署节点编号', tail: null };
  if (!file.trim()) return { ok: false, message: '请选择日志文件', tail: null };
  try {
    const res = await request({
      url: '/Deployment/AppDeployNode/LogTail',
      method: 'GET',
      params: { id: deployNodeId, file, offset, maxBytes },
    });
    const envelope = okRes(res);
    if (!envelope.ok) {
      const msg = envelope.message || str((res as Rec)?.message) || '日志读取失败';
      return { ok: false, message: msg, tail: null };
    }
    const data = (res as { data?: unknown })?.data;
    return { ok: true, message: '', tail: parseLogTailPayload(data) };
  } catch (e) {
    return { ok: false, message: requestFailText(e, '日志读取失败'), tail: null };
  }
}

/** 轮询增量拼接的内容上限：超过后从头截断，避免长开抽屉时内存无界增长 */
export const MAX_TAIL_KEEP = 1024 * 1024;

/**
 * 合并一次轮询结果（P2/P3 准实时增量追新）：
 * - 上次 fileSize>0 且本次 offset===上次 fileSize 且文件未回卷 → 追加新增（Agent 端 offset>0 从该处向后读）；
 * - 其余情况（首读、无新增时回读尾部、截断超窗、文件滚动变小）→ 整体替换为本次快照。
 * 注意：Agent 端 truncated=true 只表示「非全文件」，从 offset 增量读时也是 true，故不作为替换依据。
 */
export function applyTailSnapshot(
  prevContent: string,
  prevFileSize: number,
  tail: DeployLogTailView,
): { content: string; appended: boolean } {
  const canAppend = prevFileSize > 0 && tail.offset === prevFileSize && tail.fileSize >= prevFileSize;
  if (!canAppend) return { content: tail.content, appended: false };
  const content = (prevContent + tail.content).slice(-MAX_TAIL_KEEP);
  return { content, appended: tail.content.length > 0 };
}

/** 向上翻页步长与累计上限：每页 256KB，累计内容超过 2MB 后不再加载更早 */
export const PAGE_UP_CHUNK = 256 * 1024;
export const PAGE_UP_KEEP = 2 * 1024 * 1024;

/** 文件大小友好格式：B/KB/MB/GB/TB，1 位小数（≥10 或字节取整） */
export function formatFileSize(n: number): string {
  if (!Number.isFinite(n) || n <= 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let v = n;
  let i = 0;
  while (v >= 1024 && i < units.length - 1) {
    v /= 1024;
    i++;
  }
  const text = i === 0 || v >= 10 ? `${Math.round(v)}` : v.toFixed(1);
  return `${text} ${units[i]}`;
}

/** 下一次向上翻页的请求偏移：窗口起点向前一页；Agent 端 offset=0 语义是「读尾部」，头部窗口用 offset=1 表达 */
export function earlierOffset(windowStart: number, chunk = PAGE_UP_CHUNK): number {
  return Math.max(1, windowStart - chunk);
}

/** 是否还能加载更早：窗口起点在字节 1 之后（Agent offset≥1 才会按起点读），且累计内容未超上限 */
export function canLoadEarlier(windowStart: number, contentLength: number): boolean {
  return windowStart > 1 && contentLength < PAGE_UP_KEEP;
}

/**
 * 合并一次向上翻页结果：本次返回 [tail.offset, tail.offset+长度)，与当前窗口起点 [windowStart, …)
 * 取交集裁齐后前置拼接，返回新内容与新窗口起点（下次向前翻以此为准）。
 */
export function prependTailPage(
  prevContent: string,
  windowStart: number,
  tail: DeployLogTailView,
): { content: string; start: number } {
  const keep = Math.max(0, windowStart - tail.offset);
  const head = tail.content.slice(0, keep);
  return { content: head + prevContent, start: tail.offset };
}
