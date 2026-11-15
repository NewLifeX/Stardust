<script setup lang="ts">
/**
 * McpTokenResourcePanel：某枚令牌的资源授权管理（最小授权收窄白名单）。
 * 选中令牌后列出其 Project/Node/App 授权；可新增单条授权、删除授权。
 * IsAll 通配不在新建入口提供（设计 v1.2：新建禁用 IsAll，存量只读保留）。
 * 增删启停发事件由容器调接口后刷新；查询条自治（CARD-PLT-05）：服务端查询当前令牌授权，
 * 返回全部回退 props 客户端过滤；切换令牌自动清查询，避免 A 令牌结果串显。
 */
import { computed, ref, watch } from 'vue';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import Btn from './Btn.vue';
import Card from './Card.vue';
import Tag from './Tag.vue';
import { fetchMcpTokenResources, filterResourcesByToken, type McpOption, type McpResourceRow, type McpTokenFullRow } from './mcpToken.logic.ts';

const props = defineProps<{
  /** 请求函数（查询条用；增删启停仍走事件由容器执行） */
  req: Req;
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

// ==================== 查询条（CARD-PLT-05） ====================

/** 查询条状态：'' = 全部类型/全部状态 */
const rqType = ref('');
const rqEnable = ref<'' | 'on' | 'off'>('');
const rqDtStart = ref('');
const rqDtEnd = ref('');
const rqQ = ref('');
const rqLoading = ref(false);
const rqError = ref('');
/** null = 查询未生效（显示 props 客户端过滤）；非 null = 最近一次服务端查询快照 */
const rqRows = ref<McpResourceRow[] | null>(null);

/** 展示行：查询快照起底，否则 props 按 token 客户端过滤 */
const rows = computed<McpResourceRow[]>(() => rqRows.value ?? filterResourcesByToken(props.resources, props.token?.id ?? 0));

async function applyResourceQuery(): Promise<void> {
  const token = props.token;
  if (!token || rqLoading.value) return;
  rqLoading.value = true;
  try {
    const r = await fetchMcpTokenResources(
      props.req,
      { tokenId: token.id, resourceType: rqType.value, enable: rqEnable.value, dtStart: rqDtStart.value, dtEnd: rqDtEnd.value, Q: rqQ.value },
      { nodes: props.nodes, apps: props.apps, projects: props.projects },
    );
    rqRows.value = r.rows;
    rqError.value = r.error;
  } finally {
    rqLoading.value = false;
  }
}

/** 返回全部：清快照回 props 客户端过滤 */
function resetResourceQuery(): void {
  rqType.value = '';
  rqEnable.value = '';
  rqDtStart.value = '';
  rqDtEnd.value = '';
  rqQ.value = '';
  rqError.value = '';
  rqRows.value = null;
}

// 切换令牌时清空查询与新增表单（tokenId 固定透传，快照不可跨令牌复用）
watch(
  () => props.token?.id,
  () => {
    newId.value = 0;
    rqRows.value = null;
    rqError.value = '';
    rqType.value = '';
    rqEnable.value = '';
    rqDtStart.value = '';
    rqDtEnd.value = '';
    rqQ.value = '';
  },
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
      <!-- 查询条（CARD-PLT-05：服务端查询当前令牌授权） -->
      <div class="flex flex-wrap items-center gap-[8px] border-b border-[var(--el-border-color-light)] px-[14px] py-[10px]">
        <select
          v-model="rqType"
          data-spec="mcp-resource-query-type"
          class="h-[28px] cursor-pointer rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px] text-[12px] text-[var(--el-text-color-primary)] outline-none"
        >
          <option value="">
            全部类型
          </option>
          <option
            v-for="t in TYPE_OPTIONS"
            :key="t.key"
            :value="t.key"
          >
            {{ t.label }}
          </option>
        </select>
        <select
          v-model="rqEnable"
          data-spec="mcp-resource-query-enable"
          class="h-[28px] cursor-pointer rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px] text-[12px] text-[var(--el-text-color-primary)] outline-none"
        >
          <option value="">
            全部状态
          </option>
          <option value="on">
            仅启用
          </option>
          <option value="off">
            仅停用
          </option>
        </select>
        <input
          v-model="rqDtStart"
          type="date"
          data-spec="mcp-resource-query-dt"
          class="h-[28px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px] text-[12px] text-[var(--el-text-color-primary)] outline-none"
        >
        <span class="text-[11px] text-[var(--el-text-color-secondary)]">至</span>
        <input
          v-model="rqDtEnd"
          type="date"
          data-spec="mcp-resource-query-dt"
          class="h-[28px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px] text-[12px] text-[var(--el-text-color-primary)] outline-none"
        >
        <input
          v-model="rqQ"
          data-spec="mcp-resource-query-q"
          placeholder="搜索资源名 / 关键字"
          class="h-[28px] w-[150px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px] text-[12px] text-[var(--el-text-color-primary)] outline-none placeholder:text-[var(--el-text-color-placeholder)]"
        >
        <button
          type="button"
          data-spec="mcp-resource-query-apply"
          :disabled="rqLoading"
          class="cursor-pointer rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-fill-color-light)] px-[12px] py-[4px] text-[12px] text-[var(--el-text-color-primary)] hover:bg-[var(--el-fill-color)] disabled:cursor-not-allowed disabled:opacity-60"
          @click="applyResourceQuery"
        >
          {{ rqLoading ? '查询中…' : '查询' }}
        </button>
        <button
          v-if="rqRows !== null"
          type="button"
          data-spec="mcp-resource-query-reset"
          class="cursor-pointer rounded-[8px] border border-[var(--el-border-color-light)] px-[12px] py-[4px] text-[12px] text-[var(--el-text-color-secondary)] hover:bg-[var(--el-fill-color-light)]"
          @click="resetResourceQuery"
        >
          返回全部
        </button>
      </div>
      <div
        v-if="rqError"
        data-spec="mcp-resource-query-error"
        class="px-[14px] py-[8px] text-[12px] text-[var(--el-color-danger)]"
      >
        授权查询失败：{{ rqError }}
      </div>

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
              <template v-if="rqRows !== null">
                当前查询条件下没有授权。可调整条件重试，或点「返回全部」查看全部授权。
              </template>
              <template v-else>
                该令牌还没有收窄授权（Scope 域内全部资源可用）
              </template>
            </td>
          </tr>
        </tbody>
      </table>
    </template>
  </Card>
</template>
