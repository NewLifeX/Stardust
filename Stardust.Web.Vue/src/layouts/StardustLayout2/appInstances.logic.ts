// 实例页签纯逻辑（无 Vue 依赖，request 由调用方注入，便于单测）。
// 数据源（已读后端控制器与实体确认）：
//  - 在线实例：GET /Registry/AppOnline?appId=  （AppOnlineController.Search 支持 appId 过滤；
//      列表带 Map 出来的 NodeName 字段）
//  - 实例快照：GET /Registry/AppOnline/Snapshot?appId=（AppMeter 最新一条，字段 camelCase）：
//      每行按 clientId 取 AppMeter 最新一条 + 节点 NodeOnline.AvailableMemory；
//      cpu/memory/threads/connections/updateTime 无性能数据时为 null（不造 0）；online 由
//      服务端按会话超时判定。camelCase 字段。
//  - F03 批量命令：GET /Registry/AppOnline/Execute?command=&argument=&keys=（SelectKeys 逗号串），
//      GET /Registry/AppOnline/FreeMemory?keys=；两者返回 JsonRefresh → {code:0, message:汇总}，
//      汇总含「下发N个，响应N个，超时N个」+ 各实例返回（\n 分隔）。
import { okRes } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
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

// ---------- F04 实例快照（CPU/内存真实值） ----------

/** 实例快照行（Snapshot 返回，camelCase；无性能数据的字段为 null） */
export interface InstanceSnapshot {
  id: number;
  clientId: string;
  nodeId: number;
  name: string;
  ip: string;
  online: boolean;
  /** CPU 使用率（%），无性能数据为 null */
  cpu: number | null;
  /** 工作集内存（字节），无性能数据为 null */
  memory: number | null;
  /** 实例所在节点可用内存（字节），节点无在线数据为 null */
  availableMemory: number | null;
  threads: number | null;
  connections: number | null;
  /** 最近性能采样时间（无数据为 null） */
  updateTime: string | null;
}

export function toSnapshot(row: Rec): InstanceSnapshot {
  const numOrNull = (v: unknown): number | null => {
    const n = Number(v);
    return v === undefined || v === null || v === '' || !Number.isFinite(n) ? null : n;
  };
  const time = str(pick(row, 'updateTime'));
  return {
    id: num(pick(row, 'id')),
    clientId: str(pick(row, 'clientId')),
    nodeId: num(pick(row, 'nodeId')),
    name: str(pick(row, 'name')),
    ip: str(pick(row, 'ip')),
    online: bool(pick(row, 'online')),
    cpu: numOrNull(pick(row, 'cpu')),
    memory: numOrNull(pick(row, 'memory')),
    availableMemory: numOrNull(pick(row, 'availableMemory')),
    threads: numOrNull(pick(row, 'threads')),
    connections: numOrNull(pick(row, 'connections')),
    updateTime: time ? time : null,
  };
}

export async function fetchSnapshots(request: Req, appId: number): Promise<Map<number, InstanceSnapshot> | null> {
  if (!appId || appId <= 0) return new Map();
  try {
    const res = await request({ url: '/Registry/AppOnline/Snapshot', method: 'GET', params: { appId } });
    const map = new Map<number, InstanceSnapshot>();
    for (const s of listOf(res)) {
      const snap = toSnapshot(s);
      if (snap.id > 0) map.set(snap.id, snap);
    }
    return map;
  } catch {
    return null;
  }
}

/** MB → 字节（Snapshot 的 memory/availableMemory 单位为 MB，合并后统一走 formatBytes） */
export function mbToBytes(mb: number | null): number | null {
  if (mb === null || !Number.isFinite(mb)) return null;
  return Math.round(mb * 1024 * 1024);
}

/** 列表行 + 快照合并：快照按 AppOnline.Id 覆盖 online 与性能字段；快照失败时保持列表行（性能仍待接入） */
export interface InstanceDetail extends InstanceRow {
  online: boolean;
  cpu: number | null;
  /** 工作集内存（字节；Snapshot 的 MB 值已在此转字节） */
  memory: number | null;
  /** 节点可用内存（字节；Snapshot 的 MB 值已在此转字节） */
  availableMemory: number | null;
  threads: number | null;
  connections: number | null;
  /** 最近性能采样时间文本 */
  updateTimeText: string;
}

export function mergeSnapshots(rows: InstanceRow[], snaps: Map<number, InstanceSnapshot> | null): InstanceDetail[] {
  return rows.map((r) => {
    const s = snaps?.get(r.id);
    return {
      ...r,
      online: s ? s.online : true,
      cpu: s ? s.cpu : null,
      // Snapshot memory/availableMemory 单位为 MB（例如 189→「189 MB」、5119→约 5 GB），
      // 在数据层统一转字节，展示层继续用 formatBytes
      memory: s ? mbToBytes(s.memory) : null,
      availableMemory: s ? mbToBytes(s.availableMemory) : null,
      threads: s ? s.threads : null,
      connections: s ? s.connections : null,
      updateTimeText: s?.updateTime ? s.updateTime.replace('T', ' ').slice(0, 16) : '',
    };
  });
}

/** CPU 显示：null→空串（列里渲染「待接入」），数字→一位小数百分比 */
export function cpuText(cpu: number | null): string {
  if (cpu === null || !Number.isFinite(cpu)) return '';
  return `${cpu.toFixed(1)}%`;
}

// ---------- F03 批量命令（Execute / FreeMemory） ----------

/** 已知危险命令清单（Guard 二次确认）；不区分大小写，含子串即命中 */
export const DANGEROUS_COMMANDS = ['restart', 'reboot', 'shutdown', 'poweroff', 'stop', 'kill', 'del', 'rm', 'format', 'uninstall'];

export function isDangerousCommand(command: string): boolean {
  const c = command.trim().toLowerCase();
  if (!c) return false;
  return DANGEROUS_COMMANDS.some((d) => c === d || c.includes(d));
}

/**
 * 执行命令：GET /Registry/AppOnline/Execute?command=&argument=&keys=（SelectKeys 逗号串）。
 * 返回 JsonRefresh 汇总（「操作成功！下发N个，响应N个，超时N个」+ 各实例返回，\n 分隔）。
 * 命令成功下发但实例超时也走 ok（code 0），汇总文案由页面完整展示。
 */
export async function executeCommand(
  request: Req,
  keys: number[],
  command: string,
  argument: string,
): Promise<{ ok: boolean; message: string }> {
  const ks = keys.filter((k) => k > 0);
  if (!ks.length) return { ok: false, message: '请先勾选要操作的实例' };
  if (!command.trim()) return { ok: false, message: '请输入要下发的命令' };
  try {
    const res = await request({
      url: '/Registry/AppOnline/Execute',
      method: 'GET',
      params: { command: command.trim(), argument: argument.trim(), keys: ks.join(',') },
    });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? r.message || '命令已下发' : r.message || '命令下发失败' };
  } catch (e) {
    return { ok: false, message: e instanceof Error && e.message ? e.message : '命令下发失败' };
  }
}

/** 释放内存：GET /Registry/AppOnline/FreeMemory?keys=（后端固定下发 app/freeMemory） */
export async function freeMemory(request: Req, keys: number[]): Promise<{ ok: boolean; message: string }> {
  const ks = keys.filter((k) => k > 0);
  if (!ks.length) return { ok: false, message: '请先勾选要操作的实例' };
  try {
    const res = await request({ url: '/Registry/AppOnline/FreeMemory', method: 'GET', params: { keys: ks.join(',') } });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? r.message || '释放内存命令已下发' : r.message || '释放内存失败' };
  } catch (e) {
    return { ok: false, message: e instanceof Error && e.message ? e.message : '释放内存失败' };
  }
}
