<script setup lang="ts">
/**
 * Trace/Index — 调用链树（自定义迁移页，根 Area 隐藏页兜底）
 *
 * MVC 源 TraceController.Index（layout != detail）→ 调用链树。
 * 路由：#/Trace/<traceId>（traceId 取 route.params.id 或 route.query.id）
 * 也支持 #/Trace/<traceId>?layout=detail 切换到追踪明细（纯表格）。
 *
 * - 调用链树：复用通用组件 TraceTree（props traceId，内部拉 SampleData + 构建树）
 * - 追踪明细（layout=detail）：复用 Trace/Detail 逻辑，路由 #/Trace/Detail?id= 或本页切换
 *
 * 路由兜底：#/Trace/<id> 为隐藏三级路由，须在 src/main.ts 中 addRoute('/Trace/:id', ...)
 * 指向本 index.vue（参考 Doc/待迁移页面.md 第 3.6 节「隐藏页三级路由」经验）。
 */
import { computed } from 'vue';
import { useRoute } from 'vue-router';
import TraceTree from '../../components/TraceTree.vue';
import TraceDetail from './detail.vue';

const route = useRoute();

const traceId = computed<string>(() => {
  const p = (route.params.id as string) || '';
  const q = (route.query.id as string) || '';
  return (p || q || '').trim();
});

const layout = computed<string>(() => {
  const l = (route.query.layout as string) || '';
  return (l || '').toLowerCase();
});

const isDetail = computed(() => layout.value === 'detail');
</script>

<template>
  <div class="trace-index-page">
    <div class="tip-header">
      <h3 class="tip-title">
        调用链 · {{ traceId || '未知 TraceId' }}
      </h3>
      <div class="tip-tabs">
        <router-link
          :to="`/Trace/${encodeURIComponent(traceId)}`"
          class="tip-tab"
          :class="{ active: !isDetail }"
        >
          调用链树
        </router-link>
        <router-link
          :to="`/Trace/${encodeURIComponent(traceId)}?layout=detail`"
          class="tip-tab"
          :class="{ active: isDetail }"
        >
          追踪明细
        </router-link>
        <router-link
          :to="`/Trace/Graph?traceId=${encodeURIComponent(traceId)}`"
          class="tip-tab"
        >
          关系图
        </router-link>
      </div>
    </div>

    <div class="tip-card">
      <TraceTree
        v-if="!isDetail"
        :trace-id="traceId"
      />
      <TraceDetail
        v-else
        :trace-id="traceId"
      />
    </div>
  </div>
</template>

<style lang="scss" scoped>
.trace-index-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;

  .tip-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    flex-wrap: wrap;
    gap: 12px;
    padding: var(--el-main-padding, 24px) var(--el-main-padding, 24px) 0;

    .tip-title {
      margin: 0;
      font-size: 18px;
      font-weight: 600;
      color: var(--el-text-color-primary);
      word-break: break-all;
    }

    .tip-tabs {
      display: flex;
      gap: 4px;

      .tip-tab {
        padding: 6px 14px;
        font-size: 13px;
        color: var(--el-text-color-regular);
        text-decoration: none;
        border-radius: var(--el-border-radius-base);
        border: 1px solid var(--el-border-color-light);
        background: var(--el-bg-color);

        &:hover {
          color: var(--el-color-primary);
          border-color: var(--el-color-primary-light-5);
        }

        &.active {
          color: var(--el-color-primary);
          border-color: var(--el-color-primary);
          background: var(--el-color-primary-light-9);
        }
      }
    }
  }

  .tip-card {
    flex: 1;
    margin: var(--el-main-padding, 24px);
    padding: 16px;
    background: var(--el-bg-color-overlay);
    border: 1px solid var(--el-border-color-light);
    border-radius: var(--el-border-radius-base);
    box-shadow: var(--el-box-shadow-light);
    overflow: auto;
  }
}
</style>
