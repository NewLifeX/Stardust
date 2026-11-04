<script setup lang="ts">
/**
 * TraceGraph.vue — 调用链关系图通用组件（props: traceId/height/layout）
 *
 * 数据源（2026-11-04 用户拍板接入 /api/trace/graph）：
 *  1) API 优先：GET /trace/graph —— 服务端算好 nodes/links/categories（WebApi 根 TraceController.Graph，
 *     MVC GraphViewModel 等价：TraceItem.Kind 分类、TraceData@NodeName、备份表回查、边界补前一天）
 *  2) 失败/空回退：GET /Monitors/SampleData 自解析（原内联 parseGraph/buildOption 平移至 traceGraph.logic.ts）
 *  3) 三态：ok → EChart；empty → el-empty；error → el-alert（不造数据）
 *
 * 取数决策在 traceGraph.logic.resolveGraphData（纯函数、可单测）；deps.resolve 可注入（story/CT 桩）。
 * 消费方：/Trace/Graph、/Monitors/Trace/Graph 旧页 + /trace/:traceId 契约页关系图视图。
 */
import { computed, onMounted, ref, watch } from 'vue';
import request from '@newlifex/cube-vue/core/utils/request';
import EChart from '@/components/EChart.vue';
import { buildGraphOption, fetchSampleRows, resolveGraphData } from './traceGraph.logic';
import type { GraphData, GraphLayout, GraphResult } from './traceGraph.logic';
import type { Req } from '../views/Stardust/appsModel.ts';

export interface TraceGraphDeps {
  /** 覆盖默认取数决策（resolveGraphData）；story/CT 桩用 */
  resolve?: (request: Req, traceId: string) => Promise<GraphResult>;
}

const props = defineProps<{
  traceId?: string;
  height?: string;
  /** 布局：force（默认）/ circular，透传后端与 ECharts series.layout */
  layout?: GraphLayout;
  deps?: TraceGraphDeps;
}>();

const loading = ref(false);
const state = ref<'loading' | 'ok' | 'empty' | 'error'>('loading');
const errorMsg = ref('');
const graph = ref<GraphData | null>(null);

function req(): Req {
  return request as unknown as Req;
}

async function fetchData() {
  const traceId = props.traceId?.trim();
  graph.value = null;
  if (!traceId) {
    state.value = 'empty';
    return;
  }
  loading.value = true;
  try {
    const resolve =
      props.deps?.resolve ??
      ((r: Req, id: string) => resolveGraphData(r, id, { fetchSamples: fetchSampleRows, layout: props.layout }));
    const res = await resolve(req(), traceId);
    if (res.status === 'ok') {
      graph.value = res.data;
      state.value = 'ok';
    } else if (res.status === 'empty') {
      state.value = 'empty';
    } else {
      state.value = 'error';
      errorMsg.value = res.message;
    }
  } catch (e: unknown) {
    // deps 实现抛错（桩形状错/网络异常）：按失败态处理不留 loading态
    state.value = 'error';
    errorMsg.value = e instanceof Error && e.message ? e.message : '加载失败';
  } finally {
    loading.value = false;
  }
}

const chartOption = computed<Record<string, unknown>>(() => (graph.value ? buildGraphOption(graph.value) : {}));
const isEmpty = computed(() => state.value === 'empty' || (state.value === 'ok' && !graph.value?.nodes.length));

onMounted(fetchData);
watch(
  () => props.traceId,
  () => fetchData(),
);
</script>

<template>
  <div
    class="trace-graph-wrap"
    data-spec="trace-graph"
  >
    <el-empty
      v-if="isEmpty"
      description="暂无调用链关系数据（TraceId 无采样）"
    />

    <el-alert
      v-else-if="state === 'error'"
      type="error"
      :title="errorMsg"
      :closable="false"
      show-icon
    />

    <div
      v-else
      v-loading="loading"
      class="tg-body"
    >
      <EChart
        v-if="graph"
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
