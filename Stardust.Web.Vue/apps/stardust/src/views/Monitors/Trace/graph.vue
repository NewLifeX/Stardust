<script setup lang="ts">
/**
 * Monitors/Trace/Graph — 调用链关系图（自定义迁移页，Monitors Area 隐藏页兜底）
 *
 * 与 Trace/Graph 内容一致（复用同一个通用图解析组件 TraceGraph）。
 * 路由：#/Monitors/Trace/Graph?traceId=<id>
 * 复用通用组件 TraceGraph（props traceId，内部拉 SampleData + 解析 nodes/links/categories + ECharts）。
 *
 * 路由兜底：#/Monitors/Trace/Graph 为隐藏三级路由，须在 src/main.ts 中
 * addRoute('/Monitors/Trace/Graph', ...) 指向本页（import.meta.glob 只扫描各目录下的 index.vue，
 * 本页文件名为 graph.vue，不会被 catch-all 命中，必须手动 addRoute）。
 */
import { computed } from 'vue';
import { useRoute } from 'vue-router';
import TraceGraph from '../../../components/TraceGraph.vue';

const route = useRoute();

const traceId = computed<string>(() => {
  const q = (route.query.traceId as string) || '';
  const p = (route.params.id as string) || '';
  return (q || p || '').trim();
});
</script>

<template>
  <div class="monitors-trace-graph-page">
    <div class="mtgp-header">
      <h3 class="mtgp-title">
        调用链关系图 · {{ traceId || '未知 TraceId' }}
      </h3>
    </div>
    <div class="mtgp-card">
      <TraceGraph :trace-id="traceId" />
    </div>
  </div>
</template>

<style lang="scss" scoped>
.monitors-trace-graph-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;

  .mtgp-header {
    display: flex;
    align-items: center;
    padding: var(--el-main-padding, 24px) var(--el-main-padding, 24px) 0;

    .mtgp-title {
      margin: 0;
      font-size: 18px;
      font-weight: 600;
      color: var(--el-text-color-primary);
      word-break: break-all;
    }
  }

  .mtgp-card {
    flex: 1;
    margin: var(--el-main-padding, 24px);
    background: var(--el-bg-color-overlay);
    border: 1px solid var(--el-border-color-light);
    border-radius: var(--el-border-radius-base);
    box-shadow: var(--el-box-shadow-light);
    overflow: hidden;
    display: flex;
    flex-direction: column;
  }
}
</style>
