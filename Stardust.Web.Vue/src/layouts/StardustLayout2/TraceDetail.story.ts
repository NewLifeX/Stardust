import TraceDetail from '../../../apps/stardust/src/views/Console/TraceDetail.vue';
import type { Span } from './trace.logic.ts';
import type { GraphResult } from '../../../apps/stardust/src/components/traceGraph.logic.ts';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

/**
 * F14 全局链路页（gallery 无路由环境：props.traceId 作种子，跳转退化为本地查询）。
 * 桩（fetchSpans 形：(request, traceId) → Span[]|null）：
 * 405 → 2 片段（1 失败）；369299 → 1 片段成功；bad → 抛错（取数失败）。
 */
function spansOf(traceId: string): Span[] {
  return [
    { id: 1, traceId, spanId: 's1', parentId: '', name: 'GET /order', appName: 'order-service', success: true, startTime: 1000, endTime: 1500, cost: 500, tag: '', error: '', clientId: 'c1' },
    { id: 2, traceId, spanId: 's2', parentId: 's1', name: 'db.query', appName: 'order-service', success: false, startTime: 1100, endTime: 1400, cost: 300, tag: '', error: 'timeout', clientId: 'c1' },
  ];
}

async function stubSpans(_req: Req, traceId: string): Promise<Span[] | null> {
  // 断言素材：记录每次取数的 traceId（应总是单值——多值由前端拆分逐条请求）
  (window as unknown as { __traceCalls?: string[] }).__traceCalls = (
    window as unknown as { __traceCalls?: string[] }
  ).__traceCalls ?? [];
  (window as unknown as { __traceCalls?: string[] }).__traceCalls!.push(traceId);
  if (traceId === 'bad') return null; // 与 fetchTraceSpans 失败契约一致：null=取数失败
  return traceId === '369299' ? [spansOf(traceId)[0]] : spansOf(traceId);
}

/** 关系图桩（deps.graph）：layout=none + 固定 x/y → ECharts 确定性渲染（CT 稳定）；记录调用验证「单值逐条」 */
async function stubGraph(_req: Req, traceId: string): Promise<GraphResult> {
  (window as unknown as { __graphCalls?: string[] }).__graphCalls = (
    window as unknown as { __graphCalls?: string[] }
  ).__graphCalls ?? [];
  (window as unknown as { __graphCalls?: string[] }).__graphCalls!.push(traceId);
  return {
    status: 'ok',
    data: {
      traceId,
      title: 'order-service关系图',
      layout: 'none',
      categories: [{ name: 'App' }],
      nodes: [
        { id: '1', name: 'GET /order', value: 500, category: 0, symbolSize: 80, x: 80, y: 60 },
        { id: '2', name: 'db.query', value: 300, category: 0, symbolSize: 60, x: 240, y: 150 },
      ],
      links: [{ source: '1', target: '2' }],
      source: 'api',
    },
  };
}

export const stories = [
  // 测试状态：单链路 → 瀑布 + 诊断框（含失败片段），无「多值并列」标注
  { id: 'TraceDetail/Single', component: TraceDetail, props: { traceId: '405', deps: { fetchSpans: stubSpans } } },

  // 测试状态：多值（含一条取数失败 bad）→ 两个链路卡 + 顶部部分失败提示 + 多值标注
  { id: 'TraceDetail/Multi', component: TraceDetail, props: { traceId: '405,bad', deps: { fetchSpans: stubSpans } } },

  // 测试状态：关系图视图（initialView='graph'）→ TraceGraph 经桩渲染（API 优先注入；瀑布数据仍用 stubSpans）
  { id: 'TraceDetail/Graph', component: TraceDetail, props: { traceId: '405', initialView: 'graph', deps: { fetchSpans: stubSpans, graph: stubGraph } } },
];
