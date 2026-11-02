// 实例页签纯逻辑（无 Vue 依赖，request 由调用方注入，便于单测）。
// 数据源（已读后端控制器与实体确认）：
//  - 在线实例：GET /Registry/AppOnline?appId=  （AppOnlineController.Search 支持 appId 过滤；
//      列表带 Map 出来的 NodeName 字段）
//
// 现实约束（不造数字）：
//  - AppOnline 主表没有 CPU/内存字段；AppMeter（/Registry/AppMeter?appId=&clientId=）是
//    时序采样（默认最近 24 小时上千行），没有「每实例最新快照」接口，所以 CPU/内存两列
//    在页面标「待接入」，不在前端拼近似值。
//  - 重启 / 终端按钮只发事件，不在这里发请求（Execute 需要 keys 列表 + StarAgent 命令通道，
//    不在本页签范围）。
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
function bool(v: unknown): boolean {
  if (typeof v === 'boolean') return v;
  if (typeof v === 'number') return v !== 0;
  const s = str(v).trim().toLowerCase();
  return s === 'true' || s === '1';
}
function listOf(res: { data?: unknown } | undefined): Rec[] {
  const d = res?.data;
  return Array.isArray(d) ? (d as Rec[]) : [];
}

// ---------- 类型 ----------

/** 在线实例行（AppOnline） */
export interface InstanceRow {
  id: number;
  nodeId: number;
  /** 节点名（列表 Map 出来的 NodeName） */
  node: string;
  /** 本地 IP（IP 字段） */
  ip: string;
  /** 客户端标识 IP@进程，与 Client 字段一致 */
  client: string;
  /** 客户端版本（Version 字段） */
  version: string;
  /** 是否长连接 */
  longLink: boolean;
}

// ---------- 取数 ----------

export function toInstances(rows: Rec[]): InstanceRow[] {
  return rows.map((r) => ({
    id: num(pick(r, 'Id', 'id')),
    nodeId: num(pick(r, 'NodeId', 'nodeId')),
    node: str(pick(r, 'NodeName', 'nodeName')),
    ip: str(pick(r, 'IP', 'ip')),
    client: str(pick(r, 'Client', 'client')),
    version: str(pick(r, 'Version', 'version')),
    longLink: bool(pick(r, 'LongLink', 'longLink')),
  }));
}

export async function fetchInstances(request: Req, appId: number): Promise<InstanceRow[] | null> {
  if (!appId || appId <= 0) return [];
  try {
    const res = await request({ url: '/Registry/AppOnline', method: 'GET', params: { appId, PageSize: 500 } });
    return toInstances(listOf(res));
  } catch {
    return null;
  }
}
