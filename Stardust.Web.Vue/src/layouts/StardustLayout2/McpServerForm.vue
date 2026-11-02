<script setup lang="ts">
/**
 * McpServerForm：新增/编辑 MCP 服务器（远端 MCP 端点注册表）的 Drawer 表单（纯 UI + 本地状态，提交只发事件）。
 * 字段：名称*、远端地址*、启用、归属项目、备注。工具数为服务端快照，不在表单内编辑。
 * 提交按钮在名称/地址为空或提交中时禁用；样式只用 --el-* + Tailwind。
 */
import { computed, ref, watch } from 'vue';
import Btn from './Btn.vue';
import Drawer from './Drawer/Drawer.vue';
import type { McpOption, McpServerFormModel } from './mcpToken.logic.ts';
import type { McpServerRow } from './mesh.logic.ts';

const props = defineProps<{
  open: boolean;
  mode: 'create' | 'edit';
  /** 编辑模式的初始值 */
  server?: McpServerRow | null;
  projects: McpOption[];
  submitting?: boolean;
}>();

const emit = defineEmits<{
  close: [];
  submit: [form: McpServerFormModel];
}>();

const inputCls =
  'h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]';

// ---------- 表单状态（每次打开时按 server 重置） ----------
const name = ref('');
const url = ref('');
const enable = ref(true);
const projectId = ref(0);
const remark = ref('');

function resetForm(): void {
  const s = props.server;
  name.value = s?.name ?? '';
  url.value = s?.url ?? '';
  enable.value = s?.enable ?? true;
  projectId.value = s?.projectId ?? 0;
  remark.value = s?.remark ?? '';
}

watch(
  () => props.open,
  (v) => { if (v) resetForm(); },
  { immediate: true },
);

const canSubmit = computed(() => name.value.trim().length > 0 && url.value.trim().length > 0 && !props.submitting);

function submit(): void {
  if (!canSubmit.value) return;
  emit('submit', {
    name: name.value,
    url: url.value,
    enable: enable.value,
    projectId: projectId.value,
    remark: remark.value,
  });
}
</script>

<template>
  <Drawer
    :open="open"
    :title="mode === 'create' ? '登记 MCP 服务器' : '编辑 MCP 服务器'"
    sub="登记远端 MCP 服务端点（URL），供平台展示与选择；工具数为服务端快照"
    @close="emit('close')"
  >
    <div class="flex flex-col gap-[14px]">
      <!-- 名称 -->
      <label class="block">
        <div class="mb-[5px] text-[12px] font-medium text-[var(--el-text-color-regular)]">
          名称 <span class="text-[var(--el-color-danger)]">*</span>
        </div>
        <input
          v-model="name"
          type="text"
          data-spec="mcp-server-form-name"
          placeholder="如 filesystem"
          :class="inputCls"
        >
      </label>

      <!-- 远端地址 -->
      <label class="block">
        <div class="mb-[5px] text-[12px] font-medium text-[var(--el-text-color-regular)]">
          远端地址 <span class="text-[var(--el-color-danger)]">*</span>
        </div>
        <input
          v-model="url"
          type="text"
          data-spec="mcp-server-form-url"
          placeholder="如 http://node:6680/api/mcp"
          :class="inputCls"
        >
      </label>

      <!-- 启用 -->
      <label class="flex cursor-pointer items-center gap-[8px] text-[12.5px] text-[var(--el-text-color-regular)]">
        <input
          v-model="enable"
          type="checkbox"
          data-spec="mcp-server-form-enable"
          class="h-[14px] w-[14px] accent-[var(--el-color-primary)]"
        >
        启用
      </label>

      <!-- 归属项目 -->
      <label class="block">
        <div class="mb-[5px] text-[12px] font-medium text-[var(--el-text-color-regular)]">
          归属项目
        </div>
        <select
          v-model.number="projectId"
          data-spec="mcp-server-form-project"
          :class="inputCls"
        >
          <option :value="0">
            未归属
          </option>
          <option
            v-for="p in projects"
            :key="p.id"
            :value="p.id"
          >
            {{ p.name }}
          </option>
        </select>
      </label>

      <!-- 备注 -->
      <label class="block">
        <div class="mb-[5px] text-[12px] font-medium text-[var(--el-text-color-regular)]">
          备注
        </div>
        <textarea
          v-model="remark"
          data-spec="mcp-server-form-remark"
          rows="3"
          placeholder="用途、负责人等（可选）"
          :class="inputCls.replace('h-[30px]', 'h-auto py-[7px]')"
        />
      </label>
    </div>

    <template #actions>
      <Btn
        variant="ghost"
        data-spec="mcp-server-form-cancel"
        @click="emit('close')"
      >
        取消
      </Btn>
      <Btn
        variant="primary"
        :disabled="!canSubmit"
        data-spec="mcp-server-form-submit"
        @click="submit"
      >
        <template
          v-if="submitting"
          #icon
        >
          <span class="inline-flex animate-spin">
            <span class="h-[12px] w-[12px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-white)]" />
          </span>
        </template>
        {{ submitting ? '保存中…' : mode === 'create' ? '登记服务器' : '保存修改' }}
      </Btn>
    </template>
  </Drawer>
</template>
