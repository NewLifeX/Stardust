<script setup lang="ts">
/**
 * TraceGraph.vue — 调用链关系图通用组件（props: traceId）
 *
 * 复刻 MVC TraceController.Graph 的 nodes/links/categories 解析（纯前端 JS）：
 *  - 拉 GET /Monitors/SampleData?traceId=&pageSize=10000 拿到 SampleData 列表
 *  - 每个应用的第一个节点以 App 分类展示，其余按 TraceItem.Kind 分类
 *    （api/http/rps/rpc/db/redis/mq/event/mqtt/modbus/biz/other）
 *  - 节点名 = AppName（App 类）或 TraceItem.DisplayName/Name（特殊处理 http/db/mq/modbus 等）
 *  - 节点名 + "@" + NodeName（来自 TraceData，前端无则忽略）
 *  - 边 = ParentId → SpanId 关系（去重，且无自环）
 *  - EChart series[type=graph]（force 布局，categories 图例）
 *  - 空数据 → el-empty
 *
 * 注：TraceItem / TraceData 为后端关联实体，前端列表可能不含；
 *  - TraceItem 缺失 → 该节点统一归为 App 类（安全兜底，与 MVC 一致）
 *  - NodeName 缺失 → 节点名不带 @后缀
 */
import { ref, onMounted, watch, computed } from 'vue';
import request from '@newlifex/cube-vue/core/utils/request';
import EChart from '@/components/EChart.vue';

const props = defineProps<{ traceId?: string; height?: string }>();

interface SampleRow {
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

interface GraphNode {
  id: string;
  name: string;
  value: number;
  category: number;
  symbolSize: number;
}
interface GraphLink {
  source: string;
  target: string;
}
interface GraphCategory {
  name: string;
}

const KIND_SET = new Set([
  'api', 'http', 'rps', 'rpc', 'db', 'redis', 'mq', 'event', 'mqtt', 'modbus', 'biz', 'other',
]);

const loading = ref(false);
const error = ref('');
const rows = ref<SampleRow[]>([]);
const chartOption = ref<Record<string, unknown>>({});
const hasGraph = ref(false);

/**
 * 复刻 MVC Graph：解析 nodes/links/categories。
 * 返回 { nodes, links, categories }。
 */
function parseGraph(list: SampleRow[]): {
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

function buildOption() {
  const { nodes, links, categories } = parseGraph(rows.value);

  const option: Record<string, unknown> = {
    tooltip: {},
    legend: [
      {
        data: categories.map((c) => c.name),
        textStyle: { color: 'var(--el-text-color-secondary)' },
        top: 8,
      },
    ],
    series: [
      {
        name: '关系图',
        type: 'graph',
        layout: 'force',
        roam: true,
        draggable: true,
        categories,
        data: nodes,
        links,
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
  chartOption.value = option;
  hasGraph.value = nodes.length > 0;
}

async function fetchData() {
  const traceId = props.traceId?.trim();
  if (!traceId) {
    rows.value = [];
    hasGraph.value = false;
    chartOption.value = {};
    return;
  }
  loading.value = true;
  error.value = '';
  try {
    const res: any = await request({
      url: '/Monitors/SampleData',
      params: { traceId, pageSize: 10000 },
    });
    const list: SampleRow[] = res?.data ?? res ?? [];
    rows.value = Array.isArray(list) ? list : [];
    buildOption();
  } catch (e: any) {
    console.error('[TraceGraph] fetch failed', e);
    error.value = e?.message || '加载失败';
    rows.value = [];
    hasGraph.value = false;
    chartOption.value = {};
  } finally {
    loading.value = false;
  }
}

const isEmpty = computed(() => !loading.value && !hasGraph.value);

onMounted(fetchData);
watch(
  () => props.traceId,
  () => fetchData(),
);
</script>

<template>
  <div class="trace-graph-wrap">
    <el-empty
      v-if="isEmpty"
      description="暂无调用链关系数据（TraceId 无采样）"
    />

    <el-alert
      v-else-if="error"
      type="error"
      :title="error"
      :closable="false"
      show-icon
    />

    <div
      v-else
      v-loading="loading"
      class="tg-body"
    >
      <EChart
        v-if="hasGraph"
        :option="chartOption"
        :height="height || '460px'"
      />
    </div>
  </div>
</template>

<style lang="scss" scoped>
.trace-graph-wrap {
  width: 100%;

  .tg-body {
    min-height: 200px;
    background: var(--el-bg-color-overlay);
    border: 1px solid var(--el-border-color-light);
    border-radius: var(--el-border-radius-base);
    padding: 8px;
  }
}
</style>
