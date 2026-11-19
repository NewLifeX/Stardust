<script setup lang="ts">
/**
 * Agent 远程部署页薄壳（/nodes/agent-deploy，CARD-NODE-03）。表单、校验、确认框、
 * 结果表与输出弹窗全部在 @/layouts/StardustLayout2/AgentDeployPage.vue，本文件只做挂载。
 */
import AgentDeployPage from '@/layouts/StardustLayout2/AgentDeployPage.vue';
</script>

<template>
  <AgentDeployPage data-spec="page-agent-deploy" />
</template>
