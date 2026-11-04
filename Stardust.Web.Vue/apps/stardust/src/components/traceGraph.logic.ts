// 调用链关系图纯逻辑（无 Vue/DOM 依赖，request 与回退取数由调用方注入，便于单测）。
// 数据源优先级（2026-11-04 用户拍板「本轮接入 /api/trace/graph」）：
//  1) API 优先：GET /trace/graph?id=&layout= —— WebApi 根 TraceController.Graph（MVC GraphViewModel 的 JSON 抽取），
//     服务端算好 nodes/links/categories（含 TraceItem.Kind 分类、TraceData@NodeName、备份表回查、边界补前一天）
//  2) 回退：GET /Monitors/SampleData?traceId=&pageSize=10000 原始行 + parseSampleRowsToGraph 前端自解析
//     （原 TraceGraph.vue 内联 parseGraph 平移至此，含降级：无 TraceItem.Kind 时节点名退化为操作名）
// resolveData 返回三态：ok（含数据与来源）/ empty（两路都无数据）/ error（取回退路径的报错文案）。
// 响应字段大小写两种都认（后端 JSON PascalCase 与前端 camelCase 方言）。不造数据。
import type { Req } from '../views/Stardust/appsModel.ts';

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

// ---------- 数据模型 ----------

export interface GraphNode {
  id: string;
  name: string;
  /** 累计耗时（毫秒），用于 tooltip 与尺寸归一 */
  value: number;
  category: number;
  symbolSize: number;
  /** layout=none 时的固定坐标（测试确定性渲染用，API 不下发则缺省） */
  x?: number;
  y?: number;
}
export interface GraphLink {
  source: string;
  target: string;
}
export interface GraphCategory {
  name: string;
}
export type GraphLayout = 'force' | 'circular' | 'none';
export interface GraphData {
  traceId: string;
  title: string;
  layout: GraphLayout;
  categories: GraphCategory[];
  nodes: GraphNode[];
  links: GraphLink[];
  /** 数据来源：api=服务端 /trace/graph；local=前端 SampleData 自解析 */
  source: 'api' | 'local';
}
export type GraphResult =
  | { status: 'ok'; data: GraphData }
  | { status: 'empty' }
  | { status: 'error'; message: string };

// ---------- API 响应归一（GET /trace/graph → data:GraphViewModel） ----------

/** API 响应 data 归一为 GraphData；nodes 非数组或为空时返回 null（视为无数据，触发回退） */
export function toGraphData(rec: unknown): GraphData | null {
  if (!rec || typeof rec !== 'object') return null;
  const r = rec as Rec;
  const rawNodes = pick(r, 'nodes', 'Nodes');
  if (!Array.isArray(rawNodes) || rawNodes.length === 0) return null;

  const nodes: GraphNode[] = [];
  for (const raw of rawNodes) {
    const n = (raw ?? {}) as Rec;
    const node: GraphNode = {
      id: str(pick(n, 'id', 'Id')),
      name: str(pick(n, 'name', 'Name')),
      value: num(pick(n, 'value', 'Value')),
      category: num(pick(n, 'category', 'Category')),
      symbolSize: num(pick(n, 'symbolSize', 'SymbolSize')),
    };
    const x = pick(n, 'x', 'X');
    const y = pick(n, 'y', 'Y');
    if (x !== undefined) node.x = num(x);
    if (y !== undefined) node.y = num(y);
    nodes.push(node);
  }

  const rawLinks = pick(r, 'links', 'Links');
  const links: GraphLink[] = (Array.isArray(rawLinks) ? rawLinks : []).map((raw) => {
    const l = (raw ?? {}) as Rec;
    return { source: str(pick(l, 'source', 'Source')), target: str(pick(l, 'target', 'Target')) };
  });

  const rawCats = pick(r, 'categories', 'Categories');
  const categories: GraphCategory[] = (Array.isArray(rawCats) ? rawCats : []).map((raw) => {
    const c = (raw ?? {}) as Rec;
    return { name: str(pick(c, 'name', 'Name')) };
  });

  const rawLayout = str(pick(r, 'layout', 'Layout')).toLowerCase();
  const layout: GraphLayout = rawLayout === 'circular' || rawLayout === 'none' ? rawLayout : 'force';

  return {
    traceId: str(pick(r, 'traceId', 'TraceId')),
    title: str(pick(r, 'title', 'Title')) || '关系图',
    layout,
    categories,
    nodes,
    links,
    source: 'api',
  };
}

/** GET /trace/graph：失败时抛错（由 resolveGraphData 统一回退），无数据返回 null */
export async function fetchGraphData(request: Req, traceId: string, layout: GraphLayout = 'force'): Promise<GraphData | null> {
  const res = await request({ url: '/trace/graph', method: 'GET', params: { id: traceId, layout } });
  return toGraphData((res as { data?: unknown })?.data);
}

// ---------- 回退路径：SampleData 原始行自解析（自 TraceGraph.vue 平移） ----------

export interface SampleRow {
  id?: number | string;
  appId?: number | string;
  appName?: string | null;
  spanId?: string | null;
  parentId?: string | null;
  cost?: number;
  name?: string | null;
  clientId?: string | null;
  kind?: string | null;
  displayName?: string | null;
  traceItem?: { kind?: string; name?: string; displayName?: string } | null;
  [k: string]: unknown;
}

/** GET /Monitors/SampleData?traceId=：原始行（失败抛错，由 resolveGraphData 统一处理） */
export async function fetchSampleRows(request: Req, traceId: string, pageSize = 10000): Promise<SampleRow[]> {
  const res = await request({ url: '/Monitors/SampleData', method: 'GET', params: { traceId, pageSize } });
  const d = (res as { data?: unknown })?.data ?? res;
  return Array.isArray(d) ? (d as SampleRow[]) : [];
}

const KIND_SET = new Set([
  'api', 'http', 'rps', 'rpc', 'db', 'redis', 'mq', 'event', 'mqtt', 'modbus', 'biz', 'other',
]);

/**
 * 复刻 MVC Graph：SampleData 行 → nodes/links/categories。
 * - 每个应用的第一个节点以 App 分类展示，其余按 TraceItem.Kind 分类
 * - 节点名 = AppName 或 TraceItem.DisplayName/Name（http/db/mq/modbus 特殊处理）；无 Kind 时退化为操作名
 * - 边 = ParentId → SpanId（去重、无自环）；节点按名称聚合（value/symbolSize 累加）
 * - 尺寸归一对齐服务端：Round(100*(value-min)/(max-min))，下限 40，len<=0 时 len=1
 */
export function parseSampleRowsToGraph(list: SampleRow[]): {
  nodes: GraphNode[];
  links: GraphLink[];
  categories: GraphCategory[];
} {
  const appids: number[] = [];
  const cats: string[] = [];
  const nodes: GraphNode[] = [];
  const links = new Map<string, GraphLink>();

  // 动态索引：SpanId -> nodeId（用于建边）
  const spanToNodeId = new Map<string, string>();

  for (const item of list) {
    let cat = 'App';
    let name: string | null = item.appName || null;

    const appId = typeof item.appId === 'number' ? item.appId : Number(item.appId ?? -1);
    const ti = item.traceItem;
    const tiKind = (item.kind || ti?.kind || '') as string;
    const tiName = item.displayName || ti?.displayName || ti?.name || null;
    if (appids.includes(appId) && tiKind && KIND_SET.has(tiKind.toLowerCase())) {
      cat = tiKind.toLowerCase();
      name = tiName || item.appName || null;

      const ns = (name || '').split(':');
      switch (tiKind.toLowerCase()) {
        case 'http':
          if (name) {
            const p = name.indexOf('/', 'https://'.length);
            name = p > 0 ? name.slice(0, p) : name;
          }
          break;
        case 'db':
          if (ns.length >= 2) name = ns[1];
          break;
        case 'mq':
          if (ns.length >= 2) name = `${ns[0]}:${ns[1]}`;
          if (ns.length >= 3 && ns[0] === 'redismq' && ns[1] === 'Add') name = `${ns[0]}:${ns[2]}`;
          else if (ns.length >= 4 && ns[0] === 'redismq' && ns[2] === 'Add') name = `${ns[0]}:${ns[3]}`;
          break;
        case 'modbus':
          name = ns.length >= 3 ? ns[1] : 'IoTDevice';
          break;
        default:
          if (ns.length >= 2) name = `${ns[0]}:${ns[1]}`;
          break;
      }
    }
    if (!appids.includes(appId)) appids.push(appId);

    // 降级：无 TraceItem.Kind（后端未下发关联）时，节点名优先用操作名（name），使调用链图仍有意义
    name = name || item.name || item.appName || String(item.id);

    // NodeName：前端列表无 TraceData 关联，忽略 @NodeName（保持节点名干净）

    let idx = cats.indexOf(cat);
    if (idx < 0) {
      cats.push(cat);
      idx = cats.length - 1;
    }

    const nodeId = String(item.id);
    let node = nodes.find((e) => e.name === name);
    if (!node) {
      node = {
        id: nodeId,
        name,
        value: item.cost ?? 0,
        category: idx,
        symbolSize: item.cost ?? 0,
      };
      nodes.push(node);
    } else {
      node.value += item.cost ?? 0;
      node.symbolSize += item.cost ?? 0;
    }
    spanToNodeId.set(String(item.spanId ?? ''), node.id);

    // 建边：parent 的 span -> 当前 span
    const parentSpan = String(item.parentId ?? '');
    if (parentSpan && spanToNodeId.has(parentSpan)) {
      const src = spanToNodeId.get(parentSpan)!;
      const dst = node.id;
      const key = `${src}-${dst}`;
      if (src !== dst && !links.has(key)) {
        links.set(key, { source: src, target: dst });
      }
    }
  }

  // 处理图标大小（对齐 MVC：Round(100*(value-min)/(max-min))，下限 40，len<=0 时 len=1 防除零）
  const maxCost = nodes.length ? Math.max(...nodes.map((e) => e.value)) : 0;
  const minCost = nodes.length ? Math.min(...nodes.map((e) => e.value)) : 0;
  let len = maxCost - minCost;
  if (len <= 0) len = 1;
  for (const node of nodes) {
    const cost = Math.round((100 * (node.value - minCost)) / len);
    node.symbolSize = cost < 40 ? 40 : cost;
  }

  const categories = cats.map((c) => ({ name: c }));
  return { nodes, links: Array.from(links.values()), categories };
}

// ---------- ECharts option 构建（自 TraceGraph.vue buildOption 平移，layout 参数化） ----------

export function buildGraphOption(g: Pick<GraphData, 'nodes' | 'links' | 'categories' | 'layout'>): Record<string, unknown> {
  const option: Record<string, unknown> = {
    tooltip: {},
    legend: [
      {
        data: g.categories.map((c) => c.name),
        textStyle: { color: 'var(--el-text-color-secondary)' },
        top: 8,
      },
    ],
    series: [
      {
        name: '关系图',
        type: 'graph',
        layout: g.layout,
        roam: true,
        draggable: true,
        categories: g.categories,
        data: g.nodes,
        links: g.links,
        label: {
          show: true,
          position: 'right',
          formatter: '{b}',
          color: 'var(--el-text-color-primary)',
          fontSize: 12,
        },
        labelLayout: {
          hideOverlap: true,
          moveOverlap: true,
        },
        edgeSymbol: ['circle', 'arrow'],
        edgeSymbolSize: [4, 10],
        lineStyle: {
          color: 'target',
          curveness: 0.3,
          opacity: 0.8,
          width: 3,
        },
        emphasis: {
          focus: 'adjacency',
          lineStyle: { width: 3 },
        },
        force: {
          initLayout: 'circular',
          repulsion: 300,
          gravity: 0,
          edgeLength: [50, 300],
          layoutAnimation: true,
          friction: 0.2,
 },
      },
    ],
  };
  return option;
}

// ---------- 取数决策：API 优先、SampleData 自解析回退 ----------

export interface ResolveGraphOpts {
  /** 回退取数（注入便于单测）；生产用 fetchSampleRows */
  fetchSamples: (request: Req, traceId: string) => Promise<SampleRow[]>;
  layout?: GraphLayout;
}

/**
 * API 优先、失败回退：
 *  - API 成功且 nodes 非空 → ok（source=api），不触发回退
 *  - API 抛错/无数据 → 回退 SampleData；回退有数据 → ok（source=local）
 *  - 回退也抛错 → error（取回退路径报错文案）；回退无数据 → empty
 */
export async function resolveGraphData(request: Req, traceId: string, opts: ResolveGraphOpts): Promise<GraphResult> {
  if (!traceId) return { status: 'empty' };
  const layout: GraphLayout = opts.layout ?? 'force';

  let api: GraphData | null = null;
  try {
    api = await fetchGraphData(request, traceId, layout);
  } catch {
    api = null; // API 失败不直接报错，走回退
  }
  if (api) return { status: 'ok', data: { ...api, layout, source: 'api' } };

  try {
    const rows = await opts.fetchSamples(request, traceId);
    if (rows.length) {
      const trio = parseSampleRowsToGraph(rows);
      if (trio.nodes.length) {
        return {
          status: 'ok',
          data: { traceId, title: '关系图', layout, categories: trio.categories, nodes: trio.nodes, links: trio.links, source: 'local' },
        };
      }
    }
    return { status: 'empty' };
  } catch (e) {
    return { status: 'error', message: e instanceof Error && e.message ? e.message : '加载失败' };
  }
}
