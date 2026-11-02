<script setup lang="ts">
/**
 * McpTokenResourcePanel：某枚令牌的资源授权管理（最小授权收窄白名单）。
 * 选中令牌后列出其 Project/Node/App 授权；可新增单条授权、删除授权。
 * IsAll 通配不在新建入口提供（设计 v1.2：新建禁用 IsAll，存量只读保留）。
 * 纯 UI：增删都发事件，由容器调接口后刷新；按钮 busy 时禁用。
 */
import { computed, ref, watch } from 'vue';
import Btn from './Btn.vue';
import Card from './Card.vue';
import Tag from './Tag.vue';
import { filterResourcesByToken, type McpOption, type McpResourceRow, type McpTokenFullRow } from './mcpToken.logic.ts';

const props = defineProps<{
  token: McpTokenFullRow | null;
  resources: McpResourceRow[];
  projects: McpOption[];
  nodes: McpOption[];
  apps: McpOption[];
  busy?: boolean;
  /** Drawer 内嵌时隐藏卡片标题（标题由 Drawer 承担） */
  embedded?: boolean;
}>();

const emit = defineEmits<{
  add: [payload: { resourceType: string; resourceId: number }];
  remove: [id: number];
  /** 启停授权行（PUT 部分更新 enable） */
  toggle: [payload: { id: number; enable: boolean }];
}>();

const newType = ref<'Project' | 'Node' | 'App'>('Project');
const newId = ref(0);

/** 当前类型的候选列表 */
const candidates = computed<McpOption[]>(() => {
  if (newType.value === 'Project') return props.projects;
  if (newType.value === 'Node') return props.nodes;
  return props.apps;
});

/** 选中令牌的授权行 */
const rows = computed<McpResourceRow[]>(() => filterResourcesByToken(props.resources, props.token?.id ?? 0));

// 切换令牌时清空新增表单
watch(
  () => props.token?.id,
  () => { newId.value = 0; },
);

const canAdd = computed(() => newId.value > 0 && !props.busy);

function add(): void {
  if (!canAdd.value) return;
  emit('add', { resourceType: newType.value, resourceId: newId.value });
  newId.value = 0;
}

const TYPE_OPTIONS = [
  { key: 'Project', label: '项目' },
  { key: 'Node', label: '节点' },
  { key: 'App', label: '应用' },
] as const;
</script>

<template>
  <Card
    padding="none"
    data-spec="mcp-resource-panel"
    :class="embedded ? '!border-0 !shadow-none !bg-transparent' : ''"
  >
    <div
      v-if="!embedded"
      class="flex flex-wrap items-center gap-2 px-[14px] pt-[12px]"
    >
      <h3 class="text-[14px] font-semibold text-[var(--el-text-color-primary)]">
        资源白名单
        <span
          v-if="token"
          class="ml-[6px] text-[11.5px] font-normal text-[var(--el-text-color-secondary)]"
        >
          {{ token.name }} · {{ rows.length }} 条
        </span>
      </h3>
      <span class="flex-1" />
    </div>

    <div
      v-if="!token"
      data-spec="mcp-resource-empty-token"
      class="px-[14px] py-[18px] text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      {{ embedded ? '未指定令牌' : '在令牌表点「资源授权」管理白名单' }}
    </div>

    <template v-else>
      <!-- 新增授权行 -->
      <div class="flex flex-wrap items-center gap-[8px] border-b border-[var(--el-border-color-light)] px-[14px] py-[10px]">
        <select
          v-model="newType"
          data-spec="mcp-resource-type"
          class="h-[28px] cursor-pointer rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px] text-[12px] text-[var(--el-text-color-primary)] outline-none"
        >
          <option
            v-for="t in TYPE_OPTIONS"
            :key="t.key"
            :value="t.key"
          >
            {{ t.label }}
          </option>
        </select>
        <select
          v-model.number="newId"
          data-spec="mcp-resource-id"
          :disabled="!candidates.length"
          class="h-[28px] cursor-pointer rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px] text-[12px] text-[var(--el-text-color-primary)] outline-none disabled:cursor-not-allowed disabled:opacity-60"
        >
          <option :value="0">
            {{ candidates.length ? '选择资源…' : '该类型候选为空' }}
          </option>
          <option
            v-for="c in candidates"
            :key="c.id"
            :value="c.id"
          >
            {{ c.name }}
          </option>
        </select>
        <Btn
          size="sm"
          :disabled="!canAdd"
          data-spec="mcp-resource-add"
          @click="add"
        >
          <template #icon>
            <span
              v-if="busy"
              class="inline-flex animate-spin"
            >…</span>
          </template>
          添加授权
        </Btn>
        <span class="text-[11px] text-[var(--el-text-color-secondary)]">
          不添加行 = 不额外收窄（仍受 Scope 与绑定项目约束）
        </span>
      </div>

      <!-- 授权表 -->
      <table class="w-full border-collapse">
        <thead>
          <tr>
            <th class="border-b border-[var(--el-border-color-light)] px-[14px] py-[8px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              类型
            </th>
            <th class="border-b border-[var(--el-border-color-light)] px-[14px] py-[8px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              资源
            </th>
            <th class="border-b border-[var(--el-border-color-light)] px-[14px] py-[8px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              范围
            </th>
            <th class="border-b border-[var(--el-border-color-light)] px-[14px] py-[8px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              状态
            </th>
            <th class="border-b border-[var(--el-border-color-light)] px-[14px] py-[8px] text-right text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              操作
            </th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="r in rows"
            :key="r.id"
            data-spec="mcp-resource-row"
            class="hover:bg-[var(--el-bg-color-overlay)]"
          >
            <td class="border-b border-[var(--el-border-color-light)] px-[14px] py-[9px] text-[12.5px] text-[var(--el-text-color-primary)]">
              {{ r.resourceTypeLabel }}
            </td>
            <td class="border-b border-[var(--el-border-color-light)] px-[14px] py-[9px] text-[12.5px] text-[var(--el-text-color-primary)]">
              {{ r.resourceName || `#${r.resourceId}` }}
            </td>
            <td class="border-b border-[var(--el-border-color-light)] px-[14px] py-[9px]">
              <Tag
                v-if="r.isAll"
                tone="warn"
              >
                全部
              </Tag>
              <span
                v-else
                class="text-[12px] text-[var(--el-text-color-secondary)]"
              >
                指定资源
              </span>
            </td>
            <td class="border-b border-[var(--el-border-color-light)] px-[14px] py-[9px]">
              <Tag :tone="r.enable ? 'ok' : 'default'">
                {{ r.enable ? '启用' : '停用' }}
              </Tag>
            </td>
            <td class="border-b border-[var(--el-border-color-light)] px-[14px] py-[9px] text-right">
              <span class="inline-flex items-center gap-[6px]">
                <button
                  type="button"
                  data-spec="mcp-resource-toggle"
                  :disabled="busy"
                  class="cursor-pointer rounded-[6px] border border-[var(--el-border-color-light)] px-[8px] py-[3px] text-[11.5px] text-[var(--el-text-color-regular)] hover:bg-[var(--el-fill-color-light)] disabled:cursor-not-allowed disabled:opacity-50"
                  @click="emit('toggle', { id: r.id, enable: !r.enable })"
                >
                  {{ r.enable ? '停用' : '启用' }}
                </button>
                <button
                  type="button"
                  data-spec="mcp-resource-remove"
                  :disabled="busy"
                  class="cursor-pointer rounded-[6px] border border-[var(--el-border-color-light)] px-[8px] py-[3px] text-[11.5px] text-[var(--el-color-danger)] hover:bg-[var(--el-fill-color-light)] disabled:cursor-not-allowed disabled:opacity-50"
                  @click="emit('remove', r.id)"
                >
                  删除
                </button>
              </span>
            </td>
          </tr>
          <tr v-if="!rows.length">
            <td
              colspan="5"
              data-spec="mcp-resource-empty"
              class="px-[14px] py-[16px] text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
            >
              该令牌还没有收窄授权（Scope 域内全部资源可用）
            </td>
          </tr>
        </tbody>
      </table>
    </template>
  </Card>
</template>
