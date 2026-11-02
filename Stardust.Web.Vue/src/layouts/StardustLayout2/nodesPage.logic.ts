// 控制台「节点」页纯逻辑（与 UI 解耦，request 由调用方注入，便于单测）。
//
// 数据源（已读后端实体与控制器确认，2026-10-30）：
//  - 节点清单   GET /Nodes/Node        实体 Nodes/节点.cs（camelCase JSON：iP/oS/projectName/alarmCpuRate/lastActive…）
//    注意：实体的 Cpu 字段是「处理器核心数」，不是使用率；真实 CPU 使用率在在线表。
//  - 在线快照   GET /Nodes/NodeOnline  实体 Nodes/节点在线.cs（cpuRate 百分比、memory/memoryUsed 单位 M、pingCount、version、updateTime）
//  - 节点上应用 GET /Registry/AppOnline 实体 Entity/应用在线.cs（nodeId 关联节点；nodeName 为映射字段）
//  - CPU 走势   GET /Nodes/NodeStat    实体 Nodes/节点统计.cs：每日全局聚合（Category/StatDate/Key/Total/Actives…），
//                                       经核实没有单节点 CPU 时序 → 走势区显示空态，不造折线。
//
// 复用 apps/stardust/nodes.model.ts 的纯函数：mapNodes 做字段归一与在线/离线初判，buildOnlineIndex 从在线应用建
// 在线名集合与服务数计数；本文件在此之上合并 NodeOnline 实时指标，归一成控制台三态（正常/高负载/离线）。
//
// NodeController 自定义动作只有 Trace/SetAlarm/ResetAlarm/Fix/RefreshDns，没有批量升级 Agent / 安装 .NET / 下发指令
// 的端点，也没有「接入节点向导」接口 → 这些按钮一律 disabled 并在视线内显示「待接入」，不发假请求。
import {
  mapNodes,
  type NodeRow,
  type NodeView,
} from '../../../apps/stardust/src/views/Stardust/nodes.model';
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
function num(v: unknown): number | null {
  if (v === undefined || v === null || v === '') return null;
  const n = Number(v);
  return Number.isFinite(n) ? n : null;
}
function str(v: unknown): string {
  if (v === undefined || v === null) return '';
  return String(v).trim();
}
function listOf(res: { data?: unknown } | undefined): Rec[] {
  const d = res?.data;
  return Array.isArray(d) ? (d as Rec[]) : [];
}

// ---------- 类型 ----------

/** 控制台三态：正常(运行) / 高负载(CPU 超阈) / 离线 */
export type NodeTone = 'running' | 'high' | 'offline';

/** 节点行（控制台表格/星图共用的视图模型） */
export interface NodeRowView {
  id: number;
  name: string;
  code: string;
  ip: string;
  /** 集群/机房：projectName 兜底 address，再兜底「默认集群」 */
  cluster: string;
  tone: NodeTone;
  /** 系统：OS + OSVersion */
  os: string;
  /** 机器名 */
  machineName: string;
  /** Agent 版本（Node.Version） */
  agentVer: string;
  /** 框架（Node.Framework） */
  framework: string;
  /** .NET 运行时（Node.Runtime） */
  runtime: string;
  /** 实时 CPU 使用率 %（NodeOnline.cpuRate）；离线/无快照为 null */
  cpu: number | null;
  /** 实时内存使用率 %（memoryUsed/memory）；无快照为 null */
  mem: number | null;
  /** 该节点上在线应用数（AppOnline 按 nodeId/nodeName 计数） */
  services: number;
  /** 心跳次数（NodeOnline.pingCount） */
  ping: number | null;
  /** 最后活跃时间原文（用于 relTime） */
  lastActive: string;
  /** 原始行（抽屉 KV 用） */
  raw: Rec;
}

/** 高负载阈值（百分比）：节点自配 alarmCpuRate 优先，否则 85 */
export function cpuThreshold(row: Rec): number {
  const t = num(pick(row, 'alarmCpuRate', 'AlarmCpuRate'));
  return t && t > 0 ? t : 85;
}

// ---------- 实时指标合并 ----------

/** 内存使用率 % = memoryUsed / memory；缺任一为 null */
export function memPct(live: Rec | undefined): number | null {
  const used = num(pick(live, 'memoryUsed', 'MemoryUsed'));
  const total = num(pick(live, 'memory', 'Memory'));
  if (used === null || total === null || total <= 0) return null;
  return Math.min(100, Math.max(0, (used / total) * 100));
}

/** 三态判定：mapNodes 已判离线；在线节点再看实时 CPU 是否超阈 */
export function toTone(base: NodeView, live: Rec | undefined, _now: number): NodeTone {
  if (base.status === 'offline') return 'offline';
  // mapNodes 判在线但没拿到实时快照，仍视为在线（心跳新鲜由 deriveNodeStatus 兜过）
  const cpu = num(pick(live, 'cpuRate', 'CpuRate'));
  const thr = cpuThreshold(base.raw as Rec);
  if (cpu !== null && cpu >= thr) return 'high';
  return 'running';
}

/**
 * 归一：Node 清单 + NodeOnline 实时快照 + AppOnline 在线应用 → 控制台视图行。
 * 先经 nodes.model.mapNodes 做字段归一与在线/离线初判，再合并实时 CPU/内存/心跳。
 * 缺字段、空数组、脏值均容错，不抛错。
 */
export function mergeNodes(nodeRows: Rec[], onlineRows: Rec[], appRows: Rec[], now: number): NodeRowView[] {
  const nodes = nodeRows ?? [];
  const liveRows = onlineRows ?? [];
  const apps = appRows ?? [];

  // nodeId → 节点名（AppOnline 只有 nodeId，反查节点名才能喂给 mapNodes 的按名计数）
  const nodeIdToName = new Map<number, string>();
  for (const r of nodes) {
    const id = num(pick(r, 'id', 'ID'));
    const nm = str(pick(r, 'name', 'Name'));
    if (id !== null && nm) nodeIdToName.set(id, nm);
  }
  // 在线名集合：以 NodeOnline 快照的节点名为准（NodeOnline.Name 就是节点名）
  const onlineNames = new Set<string>();
  const liveById = new Map<number, Rec>();
  const liveByName = new Map<string, Rec>();
  for (const r of liveRows) {
    const id = num(pick(r, 'nodeId', 'NodeId'));
    const nm = str(pick(r, 'name', 'Name'));
    if (id !== null) liveById.set(id, r);
    if (nm) {
      onlineNames.add(nm);
      liveByName.set(nm, r);
    }
  }
  // 服务数：AppOnline 按 nodeId 归到节点名（mapNodes 期望 Map<节点名, 数>）
  const serviceCounts = new Map<string, number>();
  for (const r of apps) {
    const nid = num(pick(r, 'nodeId', 'NodeId'));
    const nm = nid !== null ? nodeIdToName.get(nid) : str(pick(r, 'nodeName', 'NodeName'));
    if (nm) serviceCounts.set(nm, (serviceCounts.get(nm) ?? 0) + 1);
  }

  // mapNodes 归一成 NodeView[]（id/name/ip/cluster/status/cpu/services/raw）
  const baseList = mapNodes(nodes as NodeRow[], onlineNames, serviceCounts, now);

  return baseList.map((b) => {
    const raw = b.raw as Rec;
    // 实时快照：先按 nodeId 命中，再按节点名兜底
    let liveRow = liveById.get(b.id);
    if (!liveRow) {
      const nm = str(pick(raw, 'name', 'Name'));
      if (nm) liveRow = liveByName.get(nm);
    }
    const cpu = num(pick(liveRow, 'cpuRate', 'CpuRate'));
    return {
      id: b.id,
      name: b.name,
      code: str(pick(raw, 'code', 'Code')),
      ip: b.ip,
      cluster: b.cluster,
      tone: toTone(b, liveRow, now),
      os: [str(pick(raw, 'oS', 'OS')), str(pick(raw, 'oSVersion', 'OSVersion'))].filter(Boolean).join(' '),
      machineName: str(pick(raw, 'machineName', 'MachineName')),
      agentVer: str(pick(raw, 'version', 'Version')),
      framework: str(pick(raw, 'framework', 'Framework')),
      runtime: str(pick(raw, 'runtime', 'Runtime')),
      cpu,
      mem: memPct(liveRow),
      services: b.services,
      ping: num(pick(liveRow, 'pingCount', 'PingCount')),
      lastActive: str(pick(raw, 'lastActive', 'LastActive')),
      raw,
    };
  });
}

// ---------- 统计 / 过滤 / 勾选 ----------

/** 三态计数（PageHead 与 chip 计数共用） */
export function toneCounts(rows: NodeRowView[]): { total: number; running: number; high: number; offline: number } {
  const out = { total: rows.length, running: 0, high: 0, offline: 0 };
  for (const r of rows) {
    if (r.tone === 'running') out.running += 1;
    else if (r.tone === 'high') out.high += 1;
    else out.offline += 1;
  }
  return out;
}

export type ToneFilter = 'all' | NodeTone;

/** 状态过滤 + 名称/IP 关键字搜索（前端过滤） */
export function filterConsole(rows: NodeRowView[], filter: ToneFilter, keyword: string): NodeRowView[] {
  let arr = rows;
  if (filter !== 'all') arr = arr.filter((r) => r.tone === filter);
  const k = keyword.trim().toLowerCase();
  if (k) arr = arr.filter((r) => r.name.toLowerCase().includes(k) || r.ip.toLowerCase().includes(k));
  return arr;
}

/** 勾选集合切换：返回新 Set（不可变） */
export function toggleSelect(sel: Set<number>, id: number): Set<number> {
  const next = new Set(sel);
  if (next.has(id)) next.delete(id);
  else next.add(id);
  return next;
}

/** 百分比条着色档：<60 正常(success) / 60-85 警示(warning) / ≥85 危险(danger)；null 空 */
export type BarLevel = 'ok' | 'warn' | 'danger';
export function barLevel(pct: number | null): BarLevel | null {
  if (pct === null || Number.isNaN(pct)) return null;
  if (pct >= 85) return 'danger';
  if (pct >= 60) return 'warn';
  return 'ok';
}

/** 相对时间（中文）：<60s 刚刚 / <60m X 分钟前 / <24h X 小时前 / 否则 X 天前 */
export function relTime(iso: string | undefined, now: number): string {
  if (!iso) return '—';
  const t = new Date(iso).getTime();
  if (Number.isNaN(t)) return '—';
  const s = Math.max(0, Math.floor((now - t) / 1000));
  if (s < 60) return '刚刚';
  if (s < 3600) return `${Math.floor(s / 60)} 分钟前`;
  if (s < 86400) return `${Math.floor(s / 3600)} 小时前`;
  return `${Math.floor(s / 86400)} 天前`;
}

// ---------- 星图确定性布局（不随机） ----------

export interface SkyNode {
  id: number;
  x: number; // 归一化 0..1
  y: number;
  tone: NodeTone;
  cpu: number | null;
  name: string;
  ip: string;
  services: number;
}

/**
 * 节点光点位置：按集群分四列锚点（机房连线分组），列内用索引做确定性散布（无 Math.random，不跳动）。
 */
export function layoutNodes(rows: NodeRowView[]): SkyNode[] {
  const clusters: string[] = [];
  for (const r of rows) if (!clusters.includes(r.cluster)) clusters.push(r.cluster);
  return rows.map((r, i) => {
    const ci = clusters.indexOf(r.cluster) % 4;
    const anchorX = 0.14 + ci * 0.24;
    // 确定性散布：由索引派生 ±0.06 内偏移，纵向按索引在 0.16..0.82 间均匀排
    const jitter = (((i * 37) % 10) / 10 - 0.5) * 0.12;
    const x = Math.min(0.94, Math.max(0.06, anchorX + jitter));
    const y = 0.16 + (((i * 53) % 10) / 10) * 0.66;
    return { id: r.id, x, y, tone: r.tone, cpu: r.cpu, name: r.name, ip: r.ip, services: r.services };
  });
}

// ---------- 抽屉：应用进程 / CPU 走势 ----------

/** 节点上的在线应用（/Registry/AppOnline?nodeId=）归一为抽屉行 */
export interface NodeAppRow {
  id: number;
  name: string;
  version: string;
  process: string;
  pid: number | null;
  ip: string;
}

export function toNodeAppRows(rows: Rec[]): NodeAppRow[] {
  return (rows ?? [])
    .map((r) => ({
      id: num(pick(r, 'id', 'Id', 'ID')) ?? 0,
      name: str(pick(r, 'name', 'Name')),
      version: str(pick(r, 'version', 'Version')),
      process: str(pick(r, 'processName', 'ProcessName')),
      pid: num(pick(r, 'processId', 'ProcessId')),
      ip: str(pick(r, 'iP', 'IP', 'ip')),
    }))
    .filter((a) => a.name);
}

/** CPU 走势点（NodeStat 是每日全局聚合，无单节点时序；这里仅防御性抽取，取不到即空） */
export interface CpuPoint {
  t: string;
  cpu: number | null;
}
export function cpuPoints(rows: Rec[]): CpuPoint[] {
  const out: CpuPoint[] = [];
  for (const r of rows ?? []) {
    const cpu = num(pick(r, 'cpu', 'cpuRate', 'CpuRate', 'value', 'Value'));
    if (cpu === null) continue; // 无 CPU 数值的行（如 NodeStat 每日聚合）不产生走势点
    const t = str(pick(r, 'statDate', 'StatDate', 'createTime', 'CreateTime'));
    out.push({ t, cpu });
  }
  return out;
}

/** 抽屉 KV：IP/机房/版本/框架/运行时/机器名/最后在线 */
export function drawerKv(row: NodeRowView): { k: string; v: string }[] {
  return [
    { k: 'IP', v: row.ip || '—' },
    { k: '机房 / 集群', v: row.cluster || '—' },
    { k: 'Agent 版本', v: row.agentVer || '—' },
    { k: '框架', v: row.framework || '—' },
    { k: '.NET 运行时', v: row.runtime || '—' },
    { k: '机器名', v: row.machineName || '—' },
    { k: '最后在线', v: row.lastActive || '—' },
  ];
}

// ---------- 取数（request 注入，projectId 非空透传） ----------

function paramsOf(projectId?: number | null, extra: Record<string, unknown> = {}): Record<string, unknown> {
  const p: Record<string, unknown> = { PageSize: 500, ...extra };
  if (projectId != null) p.projectId = projectId;
  return p;
}

/**
 * 端到端取数：并行拉 节点清单 / 在线快照 / 节点上应用，合并归一。
 * 任一请求失败 → 返回 error（不静默空态）。projectId 非空时随请求透传。
 */
export async function fetchNodes(
  request: Req,
  projectId?: number | null,
): Promise<{ rows: NodeRowView[]; error: string }> {
  try {
    const [nodeRes, onlineRes, appRes] = await Promise.all([
      request({ url: '/Nodes/Node', method: 'GET', params: paramsOf(projectId) }),
      request({ url: '/Nodes/NodeOnline', method: 'GET', params: paramsOf(projectId) }),
      request({ url: '/Registry/AppOnline', method: 'GET', params: paramsOf(projectId) }),
    ]);
    const rows = mergeNodes(listOf(nodeRes), listOf(onlineRes), listOf(appRes), Date.now());
    return { rows, error: '' };
  } catch (e) {
    return { rows: [], error: e instanceof Error && e.message ? e.message : '节点加载失败' };
  }
}

/** 抽屉「应用进程」tab：按 nodeId 拉该节点在线应用 */
export async function fetchNodeApps(
  request: Req,
  nodeId: number,
  projectId?: number | null,
): Promise<{ rows: NodeAppRow[]; error: string }> {
  try {
    const res = await request({
      url: '/Registry/AppOnline',
      method: 'GET',
      params: paramsOf(projectId, { nodeId }),
    });
    return { rows: toNodeAppRows(listOf(res)), error: '' };
  } catch (e) {
    return { rows: [], error: e instanceof Error && e.message ? e.message : '应用列表加载失败' };
  }
}

/** 抽屉 CPU 走势：/Nodes/NodeStat（每日全局聚合，无单节点时序 → 多半为空，走空态） */
export async function fetchCpuHistory(
  request: Req,
  nodeId: number,
  projectId?: number | null,
): Promise<{ points: CpuPoint[]; error: string }> {
  try {
    const res = await request({
      url: '/Nodes/NodeStat',
      method: 'GET',
      params: paramsOf(projectId, { nodeId }),
    });
    return { points: cpuPoints(listOf(res)), error: '' };
  } catch (e) {
    return { points: [], error: e instanceof Error && e.message ? e.message : 'CPU 历史加载失败' };
  }
}
