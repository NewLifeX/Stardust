<script setup lang="ts">
/**
 * Monitors/Trace/Index — 全链路追踪查询首页（自定义迁移页）
 *
 * MVC 源 Index.cshtml 还原：
 *  - 标题「全链路追踪」
 *  - 一个搜索输入框（placeholder「搜索TraceId（支持逗号分隔的多个TraceId）」）+ 查询按钮
 *  - 点击查询/回车 → router.push 跳转到详情页路由 #/Trace/<traceId>
 *    （详情页 Trace/Detail 待迁移，本次只负责跳转；跳转后可能暂为占位，不影响本搜索页）
 *
 * 纯前端交互页，不调后端列表接口；核心：搜索框输入 + 跳转。
 */
import { ref } from 'vue';
import { useRouter } from 'vue-router';

const router = useRouter();
const traceId = ref('');

function handleSearch() {
  const id = traceId.value.trim();
  if (!id) return;
  // 多个 TraceId 逗号分隔时取第一个作为路由参数（详情页支持单 TraceId 查询）
  const first = id.split(',')[0].trim();
  if (!first) return;
  router.push(`/Trace/${encodeURIComponent(first)}`);
}
</script>

<template>
  <div class="trace-search-page">
    <div class="tsp-header">
      <h3 class="tsp-title">
        全链路追踪
      </h3>
    </div>

    <div class="tsp-card">
      <div class="tsp-card-body">
        <el-input
          v-model="traceId"
          class="tsp-input"
          placeholder="搜索TraceId（支持逗号分隔的多个TraceId）"
          clearable
          @keyup.enter="handleSearch"
        >
          <template #append>
            <el-button
              type="primary"
              @click="handleSearch"
            >
              查询
            </el-button>
          </template>
        </el-input>
        <div class="tsp-hint">
          输入 TraceId 后点击「查询」或回车，进入调用链详情。
        </div>
      </div>
    </div>
  </div>
</template>

<style lang="scss" scoped>
.trace-search-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;

  .tsp-header {
    display: flex;
    align-items: center;
    padding: var(--el-main-padding, 24px) var(--el-main-padding, 24px) 0;

    .tsp-title {
      margin: 0;
      font-size: 18px;
      font-weight: 600;
      color: var(--el-text-color-primary);
    }
  }

  .tsp-card {
    flex: 1;
    margin: var(--el-main-padding, 24px);
    padding: 24px;
    background: var(--el-bg-color-overlay);
    border: 1px solid var(--el-border-color-light);
    border-radius: var(--el-border-radius-base);
    box-shadow: var(--el-box-shadow-light);
    overflow: hidden;
    display: flex;
    flex-direction: column;
  }

  .tsp-card-body {
    display: flex;
    flex-direction: column;
    gap: 16px;
    max-width: 720px;
    width: 100%;
  }

  .tsp-input {
    width: 100%;

    :deep(.el-input__wrapper) {
      min-height: 40px;
      border-radius: var(--el-border-radius-base);
      background: var(--el-bg-color);
      box-shadow: 0 0 0 1px var(--el-border-color-light) inset;
      transition:
        border-color 0.15s ease,
        box-shadow 0.15s ease;
    }

    :deep(.el-input__wrapper.is-focus) {
      background: var(--el-fill-color-blank);
      box-shadow:
        0 0 0 1px var(--el-color-primary) inset,
        0 0 0 3px var(--el-color-primary-light-8);
    }

    :deep(.el-input__inner) {
      font-size: 14px;
      color: var(--el-text-color-primary);
    }
  }

  .tsp-hint {
    font-size: 12px;
    color: var(--el-text-color-secondary);
    line-height: 1.6;
  }
}
</style>
