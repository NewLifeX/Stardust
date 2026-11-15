<script setup lang="ts">
/**
 * 资源白名单 Drawer：由令牌行「资源授权」按钮打开，替代「点行 + 页底面板」。
 */
import Drawer from './Drawer/Drawer.vue';
import McpTokenResourcePanel from './McpTokenResourcePanel.vue';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import type { McpOption, McpResourceRow, McpTokenFullRow } from './mcpToken.logic.ts';

defineProps<{
  open: boolean;
  token: McpTokenFullRow | null;
  resources: McpResourceRow[];
  projects: McpOption[];
  nodes: McpOption[];
  apps: McpOption[];
  busy?: boolean;
  /** 请求函数（透传面板查询条） */
  req: Req;
}>();

const emit = defineEmits<{
  close: [];
  add: [payload: { resourceType: string; resourceId: number }];
  remove: [id: number];
  toggle: [payload: { id: number; enable: boolean }];
}>();
</script>

<template>
  <Drawer
    :open="open"
    :title="token ? `资源白名单 · ${token.name}` : '资源白名单'"
    sub="可选：在 Scope 工具域权限之上，进一步限定可访问的项目/节点/应用；不添加表示不额外收窄"
    data-spec="mcp-resource-drawer"
    @close="emit('close')"
  >
    <McpTokenResourcePanel
      embedded
      :req="req"
      :token="token"
      :resources="resources"
      :projects="projects"
      :nodes="nodes"
      :apps="apps"
      :busy="busy"
      @add="emit('add', $event)"
      @remove="emit('remove', $event)"
      @toggle="emit('toggle', $event)"
    />
    <template #actions>
      <button
        type="button"
        class="h-[32px] cursor-pointer rounded-[8px] border border-[var(--el-border-color-light)] px-[14px] text-[12.5px] text-[var(--el-text-color-primary)] hover:bg-[var(--el-fill-color-light)]"
        data-spec="mcp-resource-drawer-close"
        @click="emit('close')"
      >
        完成
      </button>
    </template>
  </Drawer>
</template>
