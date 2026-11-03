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
