import TraceDetail from '../../../apps/stardust/src/views/Console/TraceDetail.vue';
import type { Span } from './trace.logic.ts';
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

export const stories = [
  // 测试状态：单链路 → 瀑布 + 诊断框（含失败片段），无「多值并列」标注
  { id: 'TraceDetail/Single', component: TraceDetail, props: { traceId: '405', deps: { fetchSpans: stubSpans } } },

  // 测试状态：多值（含一条取数失败 bad）→ 两个链路卡 + 顶部部分失败提示 + 多值标注
  { id: 'TraceDetail/Multi', component: TraceDetail, props: { traceId: '405,bad', deps: { fetchSpans: stubSpans } } },
];
