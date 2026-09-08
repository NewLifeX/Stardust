<script setup lang="ts">
/**
 * Trace/Graph — 调用链关系图（自定义迁移页，根 Area 隐藏页兜底）
 *
 * MVC 源 TraceController.Graph → ECharts graph 关系图。
 * 路由：#/Trace/Graph?traceId=<id> 或 #/Trace/<id>/graph
 * 复用通用组件 TraceGraph（props traceId，内部拉 SampleData + 解析 nodes/links/categories + ECharts）。
 *
 * 路由兜底：#/Trace/Graph 为隐藏二级路由，须在 src/main.ts 中 addRoute('/Trace/Graph', ...) 指向本页。
 */
import { computed } from 'vue';
import { useRoute } from 'vue-router';
import TraceGraph from '../../components/TraceGraph.vue';

const route = useRoute();

const traceId = computed<string>(() => {
  const q = (route.query.traceId as string) || '';
  const p = (route.params.id as string) || '';
  return (q || p || '').trim();
});
</script>

<template>
  <div class="trace-graph-page">
    <div class="tgp-header">
      <h3 class="tgp-title">
        调用链关系图 · {{ traceId || '未知 TraceId' }}
      </h3>
    </div>
    <div class="tgp-card">
      <TraceGraph :trace-id="traceId" />
    </div>
  </div>
</template>

<style lang="scss" scoped>
.trace-graph-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;

  .tgp-header {
    display: flex;
    align-items: center;
    padding: var(--el-main-padding, 24px) var(--el-main-padding, 24px) 0;

    .tgp-title {
      margin: 0;
      font-size: 18px;
      font-weight: 600;
      color: var(--el-text-color-primary);
      word-break: break-all;
    }
  }

  .tgp-card {
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
