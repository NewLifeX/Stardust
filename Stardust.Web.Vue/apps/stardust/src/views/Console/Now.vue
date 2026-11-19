<script setup lang="ts">
/**
 * 「此刻」页薄壳（/now）。取数与版面全部在
 * @/layouts/StardustLayout2/NowPage.vue；本文件只挂载，并把组件的 go / go-trace 事件接到路由
 * （节点方块 / 应用行 / 变更行 / 星图 → 等跳转都由组件发 go，这里统一 router.push；
 * 追踪跳转走命名路由 ConsoleTrace，禁路径跳转）。
 */
import { useRouter } from 'vue-router';
import NowPage from '@/layouts/StardustLayout2/NowPage.vue';

const router = useRouter();

function go(path: string): void {
  void router.push(path);
}

function goTrace(ids: string): void {
  void router.push({ name: 'ConsoleTrace', params: { traceId: ids } });
}
</script>

<template>
  <NowPage
    data-spec="page-now"
    @go="go"
    @go-trace="goTrace"
  />
</template>
