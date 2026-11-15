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
// NodeController 自定义动作只有 Trace/SetAlarm/ResetAlarm/Fix/RefreshDns，没有单节点「升级 Agent /
// 安装 .NET」的端点，也没有「接入节点向导」接口 → 这些按钮一律 disabled 并在视线内显示「待接入」，不发假请求。
// 批量运维五动作（CARD-NODE-11，2026-11-09）：不落本文件，独立为 nodeOnline.logic.ts ——
// NodeOnlineController 的 CheckUpgrade/SyncTime/Restart/Reboot/Execute（GET+keys=在线会话 Id），
// 勾选 Node.Id 经 /Nodes/NodeOnline 快照行（nodeID+updateTime 最新）映射；契约汇总条+逐节点展开行；
// 前端无权限位数据源 → 不做前端权限禁用，权限不足由后端拒绝经失败条呈现（旅程 NodeOnline.md S3 如实）。
//
// 增量登记（共享文件，按任务追加）：
//  - NODE-01 节点页骨架：mergeNodes/toneCounts/filterConsole/toggleSelect/barLevel/relTime/layoutNodes
//  - NODE-11 批量运维：nodeOnline.logic.ts（独立文件，不在此登记）
//  - NODE-13 抽屉：toNodeAppRows/cpuPoints/drawerKv/fetchNodes/fetchNodeApps/fetchCpuHistory（参数 fetchCpuHistory/request/nodeId/projectId）
//  - NODE-06 抽屉命令：nodeCmd.logic.ts（独立文件，不在此登记）
//  - CARD-NODE-07 抽屉「性能」页签（2026-11-10）：pageSizeFor24h(:414)/sampleEvenly(:424)/NODE_DATA_SERIES(:436)/
//    toNodeDataChart(:470)/buildNodeDataChartOption(:495)/toNodeLovItem(:513)/fetchNodeSearchOptions(:527)/fetchNodeData(:551)；
//    契约=NodeDataController.cs:37-47（Period>0?24*3600/Period:1440）+ NodeController.cs:128-142（NodeSearch Lov）。
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

// ==================== 抽屉「性能」页签（CARD-NODE-07，2026-11-10 定稿） ====================
// 24h 采样换算取全量点 + 时间轴等距取点 + 13 序列单 Y 轴按值显隐；Lov=NodeSearch（与 NODE-01 共用）。
// 后端契约：NodeDataController.cs:37-47（nodeId>0 且 PageSize==20 → PageSize=Period>0?24*3600/Period:1440）；
// 节点实体 Period 默认 60 秒（节点.cs:449）；序列字段映射实抄迁移页 NodeData/index.vue:189-201。

/** 首字母小写（SystemLoad→systemLoad；NewLife camelCase 序列化兜底，DiskIOPS→diskIOPS 保留后续大小写） */
function camel(name: string): string {
  return name ? name[0].toLowerCase() + name.slice(1) : name;
}

/**
 * 24h 采样换算（C11a，BL-NData-03）：Period>0 → 24*3600/Period（向下取整，与 C# 整数除法一致），
 * 否则 1440（每分钟一点）。前端主动换算带上目标 PageSize，不依赖后端「PageSize==20 时魔法扩大」。
 */
export function pageSizeFor24h(period?: number | null): number {
  const p = num(period);
  if (p !== null && p > 0) return Math.floor((24 * 3600) / p);
  return 1440;
}

/**
 * 时间轴等距取点（C11b）：超过 max 时按索引等距抽样（含首尾两点），否则原样返回；空集/非法 max → 空数组。
 * 用于把 24h 全量采样（Period<60 时最多 2880+ 点）降密到可读密度。
 */
export function sampleEvenly<T>(rows: T[], max: number): T[] {
  if (!Array.isArray(rows) || rows.length === 0) return [];
  if (!(max > 0)) return [];
  if (rows.length <= max) return rows;
  if (max === 1) return [rows[0]];
  const step = (rows.length - 1) / (max - 1);
  const out: T[] = [];
  for (let i = 0; i < max; i++) out.push(rows[Math.round(i * step)]);
  return out;
}

/** 13 序列定义（迁移页 legend 原文 + 后端字段；scale=百分比字段 ×100，其余原值） */
export const NODE_DATA_SERIES: readonly { key: string; field: string; scale: number }[] = [
  { key: '系统负载(%)', field: 'SystemLoad', scale: 100 },
  { key: 'CPU率(%)', field: 'CpuRate', scale: 100 },
  { key: '空闲内存(M)', field: 'FreeMemory', scale: 1 },
  { key: '可用磁盘(M)', field: 'AvailableFreeSpace', scale: 1 },
  { key: '磁盘活动(%)', field: 'DiskActiveTime', scale: 100 },
  { key: '电量(%)', field: 'Battery', scale: 100 },
  { key: '上行(B/s)', field: 'UplinkSpeed', scale: 1 },
  { key: '下行(B/s)', field: 'DownlinkSpeed', scale: 1 },
  { key: '温度', field: 'Temperature', scale: 1 },
  { key: '磁盘IOPS', field: 'DiskIOPS', scale: 1 },
  { key: '连接数', field: 'TcpConnections', scale: 1 },
  { key: '内网质量(%)', field: 'IntranetScore', scale: 100 },
  { key: '外网质量(%)', field: 'InternetScore', scale: 100 },
];

/** 单序列：图例名 + 值列（与 times 对齐；该行无值为 null，ECharts 断点） */
export interface NodeSeries {
  name: string;
  values: (number | null)[];
}

/** 性能页签图表视图模型：times 升序 + 按值显隐后的序列 + 换算取回的原始行数 */
export interface NodeDataChart {
  times: string[];
  series: NodeSeries[];
  total: number;
}

/**
 * NodeData 采样行 → 图表数据（C11b/c，BL-NData-05）：X=LocalTime 升序，13 序列单 Y 轴，
 * 按值显隐=任一采样行该字段有数值才收进 series（全空序列不画、不进图例）；
 * 超过 maxPoints 按时间轴等距取点降密；空集 → { times:[], series:[], total:0 }（空态不画假曲线）。
 */
export function toNodeDataChart(rows: Rec[], maxPoints = 1440): NodeDataChart {
  const ordered = [...(rows ?? [])].sort((a, b) =>
    str(pick(a, 'localTime', 'LocalTime')).localeCompare(str(pick(b, 'localTime', 'LocalTime'))),
  );
  const sampled = sampleEvenly(ordered, maxPoints);
  const times = sampled.map((r) => str(pick(r, 'localTime', 'LocalTime')));
  const series: NodeSeries[] = [];
  for (const def of NODE_DATA_SERIES) {
    const values: (number | null)[] = [];
    let hasValue = false;
    for (const r of sampled) {
      const n = num(pick(r, def.field, camel(def.field)));
      if (n === null) {
        values.push(null);
        continue;
      }
      hasValue = true;
      values.push(n * def.scale);
    }
    if (hasValue) series.push({ name: def.key, values });
  }
  return { times, series, total: ordered.length };
}

/** 采样行 → ECharts option（供 src/components/EChart.vue；单 Y 轴，颜色走组件默认调色板） */
export function buildNodeDataChartOption(chart: NodeDataChart): Record<string, unknown> {
  return {
    tooltip: { trigger: 'axis' },
    legend: { data: chart.series.map((s) => s.name), type: 'scroll', bottom: 0 },
    grid: { left: 48, right: 24, top: 24, bottom: 56, containLabel: true },
    xAxis: { type: 'category', boundaryGap: false, data: chart.times },
    yAxis: { type: 'value' },
    series: chart.series.map((s) => ({ name: s.name, type: 'line', smooth: true, showSymbol: false, data: s.values })),
  };
}

/** 节点搜索 Lov 选项（AppLov 同形：{id,value}，el-autocomplete value-key='value'） */
export interface NodeOption {
  id: number;
  value: string;
}

/** NodeSearch 候选行 → Lov 选项（name 优先，兜底 code/IP；丢掉无 Id 行） */
export function toNodeLovItem(r: Rec): NodeOption | null {
  const id = num(pick(r, 'iD', 'ID', 'id', 'Id'));
  if (id === null || id <= 0) return null;
  const name = str(pick(r, 'name', 'Name'));
  const code = str(pick(r, 'code', 'Code'));
  const ip = str(pick(r, 'iP', 'IP', 'ip'));
  return { id, value: name || code || ip || `#${id}` };
}

/**
 * Lov 远程联想（CARD-NODE-07 §9.3，与 NODE-01 共用组件）：GET /Nodes/Node/NodeSearch
 * （NodeController.cs:128-142 [LovList]，params key/pageSize 20 服务端固定/LastActive desc）。
 * 空关键字不发请求；失败静默空列表（联想框可改词重试）。
 */
export async function fetchNodeSearchOptions(
  request: Req,
  key: string,
  projectId?: number | null,
): Promise<NodeOption[]> {
  const q = str(key);
  if (!q) return [];
  try {
    // PageSize 固定 20 对齐后端契约（NodeController.cs:131 new PageParameter{PageSize=20}），不走 paramsOf 默认 500
    const p: Record<string, unknown> = { key: q, PageSize: 20 };
    if (projectId != null) p.projectId = projectId;
    const res = await request({ url: '/Nodes/Node/NodeSearch', method: 'GET', params: p });
    return listOf(res)
      .map(toNodeLovItem)
      .filter((o): o is NodeOption => o !== null);
  } catch {
    return [];
  }
}

/**
 * 抽屉「性能」页签取数（C11a）：GET /Nodes/NodeData，PageSize=24h 采样换算值（period 取节点实体 Period）。
 * 返回原始采样行（Pascal/camel 双形态字段），供 toNodeDataChart 消费。
 */
export async function fetchNodeData(
  request: Req,
  nodeId: number,
  period?: number | null,
  projectId?: number | null,
): Promise<{ rows: Rec[]; error: string }> {
  try {
    const res = await request({
      url: '/Nodes/NodeData',
      method: 'GET',
      params: paramsOf(projectId, { nodeId, PageSize: pageSizeFor24h(period) }),
    });
    return { rows: listOf(res), error: '' };
  } catch (e) {
    return { rows: [], error: e instanceof Error && e.message ? e.message : '性能数据加载失败' };
  }
}
