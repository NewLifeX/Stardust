import TraceList from './TraceList.vue';
import type { Span, TraceSummary } from './trace.logic.ts';

// 固定骨架：两条调用链（首条成功、第二条失败）；选中首条时返回它的瀑布片段
const TRACES: TraceSummary[] = [
  { traceId: 'ac15452e01234567', name: 'POST /api/order', ok: true, cost: 420, startAt: 1761700000000, spanCount: 2 },
  { traceId: 'ac15452e89abcdef0', name: 'GET /api/pay', ok: false, cost: 1350, startAt: 1761699000000, spanCount: 1 },
];

const SPANS_OK: Span[] = [
  { id: 1, traceId: 'ac15452e01234567', spanId: 'r1', parentId: '', name: 'POST /api/order', appName: 'OrderService', success: true, startTime: 0, endTime: 420, cost: 420, tag: '', error: '', clientId: 'ip@1' },
  { id: 2, traceId: 'ac15452e01234567', spanId: 'c1', parentId: 'r1', name: 'mysql.query', appName: 'mysql', success: true, startTime: 60, endTime: 300, cost: 240, tag: '', error: '', clientId: 'ip@1' },
];

const SPANS_ERR: Span[] = [
  { id: 3, traceId: 'ac15452e89abcdef0', spanId: 'r2', parentId: '', name: 'GET /api/pay', appName: 'PayService', success: false, startTime: 0, endTime: 1350, cost: 1350, tag: '', error: 'timeout', clientId: 'ip@2' },
];

function depsFor(detailFor: (traceId: string) => Span[] = () => SPANS_OK) {
  return {
    fetchTraces: async () => TRACES,
    fetchDetail: async (_req: unknown, traceId: string) => detailFor(traceId),
  };
}

export const stories = [
  // 测试状态：两条调用链，首条选中并渲染成功 Tag + 瀑布条 + 诊断框 + 底部两按钮
  { id: 'TraceList/WithData', component: TraceList, props: { appId: 32, deps: depsFor((id) => (id === TRACES[0].traceId ? SPANS_OK : SPANS_ERR)) } },

  // 测试状态：该应用今天没有采样记录，空态文案
  { id: 'TraceList/Empty', component: TraceList, props: { appId: 32, deps: { fetchTraces: async () => [], fetchDetail: async () => [] } } },

  // 测试状态：列表请求失败，错误态
  { id: 'TraceList/Failed', component: TraceList, props: { appId: 32, deps: { fetchTraces: async () => null, fetchDetail: async () => null } } },

  // 测试事件：底部两按钮只发事件（追加进 window.__traceEvents），不实现跳转
  {
    id: 'TraceList/EventRecorder',
    component: TraceList,
    props: {
      appId: 32,
      deps: depsFor(),
      onOpenConfig: () => {
        const w = window as unknown as { __traceEvents?: string[] };
        (w.__traceEvents ??= []).push('openConfig');
      },
      onLocateInstance: () => {
        const w = window as unknown as { __traceEvents?: string[] };
        (w.__traceEvents ??= []).push('locateInstance');
      },
    },
  },
];
