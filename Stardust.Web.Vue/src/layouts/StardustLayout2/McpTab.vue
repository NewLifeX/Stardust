<script setup lang="ts">
/**
 * McpTab：/mesh AI·MCP tab —— MCP 管理页（v1.3）。
 * 区域自上而下：
 *  0) 连接指引：/mcp 端点 + Bearer 用法 + EnableMcp 总开关（StarServerSetting，PUT 全量回存）+ 示例配置复制
 *  1) MCP 服务器：远端 MCP 端点注册表管理（/Platform/McpServer 全 CRUD：新增/编辑/启停/删除）
 *  2) 令牌管理：列表 + 新增/编辑/启停/重置/删除；新令牌创建/重置后一次性展示完整 sk-mcp- 值；
 *     「绑定项目」下拉为客户端过滤（令牌是平台级资源，列表不随后端项目切换过滤）
 *  3) 资源授权面板：按选中令牌查看/增删/启停 Project/Node/App 收窄授权
 *  4) 审计日志：近期 200 条实时过滤；「查询历史」走服务端过滤（tokenId/success/toolName/时间范围）+ 分页
 *
 * 数据由 mesh.logic fetchMcpManage 传入；写操作直接调同目录 mcpToken.logic 的真接口，
 * 成功后 toast + emit('changed') 由薄壳重取数。样式只用 --el-* + Tailwind。
 */
import { computed, onMounted, ref, watch } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import { getConfig } from '@newlifex/cube-vue/core/configure';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import Btn from './Btn.vue';
import Card from './Card.vue';
import Dot from './Dot.vue';
import GuardModal from './GuardModal/GuardModal.vue';
import KIcon from './KIcon.vue';
import McpServerForm from './McpServerForm.vue';
import McpTokenForm from './McpTokenForm.vue';
import McpTokenResourceDrawer from './McpTokenResourceDrawer.vue';
import Tag from './Tag.vue';
import { toast } from './useToast';
import { fetchMemberLinks, type MemberLink } from './projectMember.logic.ts';
import type { McpServerRow } from './mesh.logic.ts';
import {
  addResource,
  createMcpServer,
  createToken,
  deleteMcpServer,
  deleteResource,
  deleteToken,
  fetchMcpAuditPage,
  fetchMcpRuntime,
  filterAudits,
  formatBoundUser,
  parseScopes,
  resetToken,
  scopeLabel,
  setMcpEnabled,
  setResourceEnable,
  setServerEnable,
  setTokenEnable,
  updateMcpServer,
  updateToken,
  type McpAuditFullRow,
  type McpAuditQuery,
  type McpOption,
  type McpResourceRow,
  type McpServerFormModel,
  type McpTokenFormModel,
  type McpTokenFullRow,
} from './mcpToken.logic.ts';

const req = request as Req;

const props = withDefaults(
  defineProps<{
    tokens: McpTokenFullRow[];
    resources: McpResourceRow[];
    audits: McpAuditFullRow[];
    projects: McpOption[];
    users: McpOption[];
    nodes: McpOption[];
    apps: McpOption[];
    servers: McpServerRow[];
    serversError?: string;
    loading?: boolean;
    error?: string;
  }>(),
  { serversError: '', loading: false, error: '' },
);

const emit = defineEmits<{
  retry: [];
  changed: [];
}>();

// ---------- 选中令牌 ----------
const selectedTokenId = ref(0);
watch(
  () => props.tokens,
  (rows) => {
    if (!rows.find((r) => r.id === selectedTokenId.value)) {
      selectedTokenId.value = rows[0]?.id ?? 0;
    }
  },
  { immediate: true },
);
const selectedToken = computed(() => props.tokens.find((r) => r.id === selectedTokenId.value) ?? null);

// ---------- 表单 Drawer ----------
const formOpen = ref(false);
const formMode = ref<'create' | 'edit'>('create');
const editingToken = ref<McpTokenFullRow | null>(null);
const submitting = ref(false);
const formMembers = ref<MemberLink[]>([]);
const formMembersReady = ref(false);
let memberLoadSeq = 0;

async function loadFormMembers(projectId: number): Promise<void> {
  const ticket = ++memberLoadSeq;
  if (projectId <= 0) {
    formMembers.value = [];
    formMembersReady.value = true;
    return;
  }
  formMembersReady.value = false;
  const r = await fetchMemberLinks(req, projectId);
  if (ticket !== memberLoadSeq) return;
  formMembers.value = r.rows;
  formMembersReady.value = true;
  if (r.error) toast(`项目成员加载失败：${r.error}`, 'err');
}

function openCreate(): void {
  formMode.value = 'create';
  editingToken.value = null;
  formOpen.value = true;
}
function openEdit(row: McpTokenFullRow): void {
  formMode.value = 'edit';
  editingToken.value = row;
  formOpen.value = true;
}

const pendingResourceTokenId = ref(0);

async function onFormSubmit(form: McpTokenFormModel): Promise<void> {
  submitting.value = true;
  try {
    const r = formMode.value === 'create'
      ? await createToken(req, form)
      : await updateToken(req, editingToken.value?.id ?? 0, form);
    toast(r.message, r.ok ? 'ok' : 'err');
    if (!r.ok) return;
    formOpen.value = false;
    if (r.tokenValue) oneTimeToken.value = r.tokenValue;
    if (formMode.value === 'create' && form.projectId > 0 && r.tokenId) {
      pendingResourceTokenId.value = r.tokenId;
    }
    emit('changed');
  } finally {
    submitting.value = false;
  }
}

watch(
  () => props.tokens,
  (rows) => {
    if (resourceDrawerToken.value) {
      const cur = rows.find((r) => r.id === resourceDrawerToken.value!.id);
      if (cur) resourceDrawerToken.value = cur;
    }
    const pid = pendingResourceTokenId.value;
    if (!pid) return;
    const t = rows.find((r) => r.id === pid);
    if (t) {
      pendingResourceTokenId.value = 0;
      openResourceAuth(t);
    }
  },
);

// ---------- 行操作（启停 / 重置 / 删除） ----------
/** `${op}:${id}`，行内按钮 loading；空串 = 无 */
const busyKey = ref('');
function isBusy(key: string): boolean { return busyKey.value === key; }

async function onToggleEnable(row: McpTokenFullRow): Promise<void> {
  busyKey.value = `enable:${row.id}`;
  try {
    const cur: McpTokenFormModel = {
      name: row.name,
      enable: row.enable,
      userId: row.userId,
      projectId: row.projectId,
      scopes: row.scopes,
      expireIso: row.expireMs ? toLocalIso(row.expireMs) : '',
      remark: row.remark,
    };
    const r = await setTokenEnable(req, row.id, !row.enable, cur);
    toast(r.message, r.ok ? 'ok' : 'err');
    if (r.ok) emit('changed');
  } finally {
    busyKey.value = '';
  }
}

function toLocalIso(ms: number): string {
  const d = new Date(ms);
  const p = (n: number): string => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
}

const resetTarget = ref<McpTokenFullRow | null>(null);

async function onResetConfirm(): Promise<void> {
  const row = resetTarget.value;
  if (!row) return;
  busyKey.value = `reset:${row.id}`;
  try {
    const r = await resetToken(req, row.id);
    toast(r.message, r.ok ? 'ok' : 'err');
    if (r.ok) {
      resetTarget.value = null;
      if (r.tokenValue) oneTimeToken.value = r.tokenValue;
      emit('changed');
    }
  } finally {
    busyKey.value = '';
  }
}

const resourceDrawerOpen = ref(false);
const resourceDrawerToken = ref<McpTokenFullRow | null>(null);

function openResourceAuth(row: McpTokenFullRow): void {
  selectedTokenId.value = row.id;
  resourceDrawerToken.value = row;
  resourceDrawerOpen.value = true;
}

function closeResourceDrawer(): void {
  resourceDrawerOpen.value = false;
  resourceDrawerToken.value = null;
}

/** 行点击：仅同步选中态（审计筛选），不展开页底资源面板 */
function onTokenRowClick(row: McpTokenFullRow): void {
  selectedTokenId.value = row.id;
  auditTokenId.value = row.id;
}

const serversSectionOpen = ref(false);

const deleteTarget = ref<McpTokenFullRow | null>(null);
async function onDeleteConfirm(): Promise<void> {
  const t = deleteTarget.value;
  if (!t) return;
  busyKey.value = `del:${t.id}`;
  try {
    const r = await deleteToken(req, t.id);
    toast(r.message, r.ok ? 'ok' : 'err');
    if (r.ok) {
      deleteTarget.value = null;
      emit('changed');
    }
  } finally {
    busyKey.value = '';
  }
}

// ---------- 一次性完整 Token 弹窗 ----------
const oneTimeToken = ref('');
async function copyToken(): Promise<void> {
  try {
    await navigator.clipboard.writeText(oneTimeToken.value);
    toast('完整 Token 已复制', 'ok');
  } catch {
    toast('复制失败，请手动选择文本复制', 'err');
  }
}

// ---------- 资源授权 ----------
const resourceBusy = ref(false);
async function onAddResource(p: { resourceType: string; resourceId: number }): Promise<void> {
  const tok = resourceDrawerToken.value ?? selectedToken.value;
  if (!tok) return;
  resourceBusy.value = true;
  try {
    const r = await addResource(req, tok.id, p.resourceType, p.resourceId);
    toast(r.message, r.ok ? 'ok' : 'err');
    if (r.ok) emit('changed');
  } finally {
    resourceBusy.value = false;
  }
}
async function onRemoveResource(id: number): Promise<void> {
  resourceBusy.value = true;
  try {
    const r = await deleteResource(req, id);
    toast(r.message, r.ok ? 'ok' : 'err');
    if (r.ok) emit('changed');
  } finally {
    resourceBusy.value = false;
  }
}
/** 启停授权行（PUT 部分更新 enable） */
async function onToggleResource(p: { id: number; enable: boolean }): Promise<void> {
  resourceBusy.value = true;
  try {
    const r = await setResourceEnable(req, p.id, p.enable);
    toast(r.message, r.ok ? 'ok' : 'err');
    if (r.ok) emit('changed');
  } finally {
    resourceBusy.value = false;
  }
}

// ---------- MCP 服务器管理（G-A：后端 McpServerController 全 CRUD） ----------
const serverFormOpen = ref(false);
const serverFormMode = ref<'create' | 'edit'>('create');
const editingServer = ref<McpServerRow | null>(null);
const serverSubmitting = ref(false);

function openServerCreate(): void {
  serverFormMode.value = 'create';
  editingServer.value = null;
  serverFormOpen.value = true;
}
function openServerEdit(row: McpServerRow): void {
  serverFormMode.value = 'edit';
  editingServer.value = row;
  serverFormOpen.value = true;
}

async function onServerSubmit(form: McpServerFormModel): Promise<void> {
  serverSubmitting.value = true;
  try {
    const r = serverFormMode.value === 'create'
      ? await createMcpServer(req, form)
      : await updateMcpServer(req, editingServer.value?.id ?? 0, form);
    toast(r.message, r.ok ? 'ok' : 'err');
    if (!r.ok) return;
    serverFormOpen.value = false;
    emit('changed');
  } finally {
    serverSubmitting.value = false;
  }
}

async function onServerToggle(row: McpServerRow): Promise<void> {
  busyKey.value = `srv-enable:${row.id}`;
  try {
    const r = await setServerEnable(req, row.id, !row.enable);
    toast(r.message, r.ok ? 'ok' : 'err');
    if (r.ok) emit('changed');
  } finally {
    busyKey.value = '';
  }
}

const serverDeleteTarget = ref<McpServerRow | null>(null);
async function onServerDeleteConfirm(): Promise<void> {
  const s = serverDeleteTarget.value;
  if (!s) return;
  busyKey.value = `srv-del:${s.id}`;
  try {
    const r = await deleteMcpServer(req, s.id);
    toast(r.message, r.ok ? 'ok' : 'err');
    if (r.ok) {
      serverDeleteTarget.value = null;
      emit('changed');
    }
  } finally {
    busyKey.value = '';
  }
}

/** 项目名解析（服务器归属项目列） */
const projectMap = computed(() => new Map(props.projects.map((p) => [p.id, p.name])));
function projectNameOf(projectId: number): string {
  if (!projectId || projectId <= 0) return '—';
  return projectMap.value.get(projectId) ?? `#${projectId}`;
}

// ---------- 连接指引（G-B：/mcp 端点 + Bearer + EnableMcp 总开关） ----------
const runtime = ref<Awaited<ReturnType<typeof fetchMcpRuntime>> | null>(null);
const runtimeBusy = ref(false);

onMounted(() => { void loadRuntime(); });
async function loadRuntime(): Promise<void> {
  runtime.value = await fetchMcpRuntime(req);
}

/** MCP 端点地址 = 后端 API 基地址 + /mcp（与治理页接口同源） */
const mcpEndpoint = computed(() => {
  const base = (getConfig()?.request?.baseUrl ?? '').replace(/\/+$/, '');
  return `${base}/mcp`;
});
const sampleConfigJson = computed(() =>
  JSON.stringify({ mcpServers: { stardust: { url: mcpEndpoint.value, headers: { Authorization: 'Bearer sk-mcp-xxxxxxxx' } } } }, null, 2),
);

async function copyText(text: string, okMsg: string): Promise<void> {
  try {
    await navigator.clipboard.writeText(text);
    toast(okMsg, 'ok');
  } catch {
    toast('复制失败，请手动选择文本复制', 'err');
  }
}

/** 切换 EnableMcp：GET 到的完整设置对象回传 PUT（ConfigController 全量覆盖），只翻转开关字段 */
const enableMcpTarget = ref<boolean | null>(null);
async function onEnableMcpConfirm(): Promise<void> {
  const raw = runtime.value?.raw;
  const enable = enableMcpTarget.value;
  if (!raw || enable === null) return;
  runtimeBusy.value = true;
  try {
    const r = await setMcpEnabled(req, raw, enable);
    toast(r.message, r.ok ? 'ok' : 'err');
    enableMcpTarget.value = null;
    if (r.ok) await loadRuntime();
  } finally {
    runtimeBusy.value = false;
  }
}

// ---------- 令牌项目过滤（G-D：令牌为平台级资源，客户端按绑定项目过滤） ----------
/** 0=全部项目；-1=未绑定；>0=项目 Id */
const tokenProjectFilter = ref(0);
const displayTokens = computed(() => {
  const f = tokenProjectFilter.value;
  if (!f) return props.tokens;
  return props.tokens.filter((t) => (f === -1 ? t.projectId <= 0 : t.projectId === f));
});

// ---------- 审计过滤（近期 200 条客户端过滤 + 历史服务端查询分页） ----------
const auditTokenId = ref(0);
const auditIpQ = ref('');
const auditLevel = ref<'all' | 'ok' | 'failed'>('all');
const expandedAuditId = ref<string | null>(null);
/** recent=props 近期记录（客户端过滤）；query=服务端历史检索结果 */
const auditMode = ref<'recent' | 'query'>('recent');
const auditToolName = ref('');
const auditDtStart = ref('');
const auditDtEnd = ref('');
const AUDIT_PAGE_SIZE = 50;
const auditPageIndex = ref(0);
const auditTotal = ref(0);
const queryAudits = ref<McpAuditFullRow[]>([]);
const queryLoading = ref(false);
const queryError = ref('');

const clientFilter = computed(() => ({ tokenId: auditTokenId.value, ipQ: auditIpQ.value, level: auditLevel.value }));
const visibleAudits = computed(() =>
  auditMode.value === 'recent'
    ? filterAudits(props.audits, clientFilter.value)
    : filterAudits(queryAudits.value, clientFilter.value),
);

/** 服务端历史查询。条件在点击「查询」时生效；翻页沿用当前条件 */
async function loadAuditPage(pageIndex: number): Promise<void> {
  queryLoading.value = true;
  queryError.value = '';
  const q: McpAuditQuery = {
    tokenId: auditTokenId.value,
    toolName: auditToolName.value,
    success: auditLevel.value === 'all' ? undefined : auditLevel.value === 'ok',
    dtStart: auditDtStart.value,
    dtEnd: auditDtEnd.value,
    pageIndex,
    pageSize: AUDIT_PAGE_SIZE,
  };
  const r = await fetchMcpAuditPage(req, q);
  queryAudits.value = r.rows;
  auditTotal.value = r.total;
  auditPageIndex.value = pageIndex;
  queryError.value = r.error;
  queryLoading.value = false;
}

function enterAuditQuery(): void {
  auditMode.value = 'query';
  expandedAuditId.value = null;
  void loadAuditPage(0);
}
function exitAuditQuery(): void {
  auditMode.value = 'recent';
  expandedAuditId.value = null;
}
function auditPrevPage(): void {
  if (auditPageIndex.value > 0 && !queryLoading.value) void loadAuditPage(auditPageIndex.value - 1);
}
function auditNextPage(): void {
  const maxPage = Math.max(0, Math.ceil(auditTotal.value / AUDIT_PAGE_SIZE) - 1);
  if (auditPageIndex.value < maxPage && !queryLoading.value) void loadAuditPage(auditPageIndex.value + 1);
}
const auditHasPrev = computed(() => auditPageIndex.value > 0);
const auditHasNext = computed(() => auditPageIndex.value < Math.ceil(auditTotal.value / AUDIT_PAGE_SIZE) - 1);

// 令牌增删改后 props.audits 会整体刷新；若正处于历史检索模式，静默重查当前页保持一致
watch(
  () => props.audits,
  () => {
    if (auditMode.value === 'query' && !queryLoading.value) void loadAuditPage(auditPageIndex.value);
  },
);

/** 风险级别显示名。0=L0只读 1=L1可逆写 2=L2高危，未知回退 — */
function riskLabel(level: number): string {
  if (level === 0) return 'L0 只读';
  if (level === 1) return 'L1 可逆写';
  if (level === 2) return 'L2 高危';
  return '—';
}
</script>

<template>
  <div class="flex flex-col gap-[16px]">
    <!-- ===== 区域 0：连接指引（/mcp 端点 + Bearer + EnableMcp 总开关） ===== -->
    <Card
      padding="none"
      data-spec="mcp-guide-card"
    >
      <div class="flex flex-wrap items-center gap-2 px-[14px] pt-[12px]">
        <h3 class="text-[14px] font-semibold text-[var(--el-text-color-primary)]">
          连接指引
        </h3>
        <span class="flex-1" />
        <span
          v-if="runtime?.error"
          data-spec="mcp-guide-runtime-error"
          class="text-[11.5px] text-[var(--el-color-danger)]"
        >运行时状态：{{ runtime.error }}</span>
        <template v-else-if="runtime">
          <Tag
            v-if="runtime.enableMcp === undefined"
            tone="default"
            data-spec="mcp-guide-switch-unknown"
          >
            EnableMcp：未知
          </Tag>
          <template v-else>
            <Tag
              :tone="runtime.enableMcp ? 'ok' : 'warn'"
              data-spec="mcp-guide-switch-state"
            >
              EnableMcp：{{ runtime.enableMcp ? '已开启' : '已关闭（/mcp 不可用）' }}
            </Tag>
            <Btn
              size="sm"
              variant="ghost"
              data-spec="mcp-guide-switch"
              :disabled="runtimeBusy"
              @click="enableMcpTarget = !runtime.enableMcp"
            >
              <template #icon>
                <span
                  v-if="runtimeBusy"
                  class="inline-flex animate-spin"
                >…</span>
              </template>
              {{ runtime.enableMcp ? '关闭 MCP 服务' : '开启 MCP 服务' }}
            </Btn>
          </template>
        </template>
      </div>
      <div class="grid grid-cols-1 gap-[12px] px-[14px] pb-[14px] pt-[10px] min-[760px]:grid-cols-2">
        <div class="flex flex-col gap-[8px]">
          <div>
            <div class="text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              MCP 端点（与治理页接口同源，POST）
            </div>
            <div class="mt-[4px] flex items-center gap-[8px]">
              <code
                data-spec="mcp-guide-endpoint"
                class="min-w-0 flex-1 truncate rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-fill-color-light)] px-[10px] py-[6px] font-mono text-[12.5px] text-[var(--el-text-color-primary)]"
              >{{ mcpEndpoint }}</code>
              <Btn
                size="sm"
                variant="ghost"
                data-spec="mcp-guide-endpoint-copy"
                @click="copyText(mcpEndpoint, '端点地址已复制')"
              >
                <template #icon>
                  <KIcon
                    name="Copy"
                    :size="13"
                  />
                </template>
                复制
              </Btn>
            </div>
          </div>
          <div>
            <div class="text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              鉴权（HTTP 头）
            </div>
            <div class="mt-[4px] flex items-center gap-[8px]">
              <code class="min-w-0 flex-1 truncate rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-fill-color-light)] px-[10px] py-[6px] font-mono text-[12.5px] text-[var(--el-text-color-primary)]">Authorization: Bearer &lt;sk-mcp-…&gt;</code>
              <span class="text-[11px] leading-[1.5] text-[var(--el-text-color-secondary)]">用下方令牌列表创建的 Token 值替换</span>
            </div>
          </div>
          <div class="flex flex-wrap items-center gap-[6px] text-[11px] text-[var(--el-text-color-secondary)]">
            <span>域白名单 McpActionSet：</span>
            <code
              data-spec="mcp-guide-actionset"
              class="rounded-[6px] bg-[var(--el-fill-color-light)] px-[6px] py-[2px] font-mono text-[11px] text-[var(--el-text-color-regular)]"
            >{{ runtime?.actionSet || '—' }}</code>
            <span class="ml-[8px]">敏感词关键词：</span>
            <code
              data-spec="mcp-guide-sensitive"
              class="rounded-[6px] bg-[var(--el-fill-color-light)] px-[6px] py-[2px] font-mono text-[11px] text-[var(--el-text-color-regular)]"
            >{{ runtime?.sensitiveKeywords ? runtime.sensitiveKeywords.split(',').filter(Boolean).length + ' 个' : '—' }}</code>
          </div>
        </div>
        <div>
          <div class="flex items-center gap-[8px]">
            <div class="text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              MCP 客户端配置示例
            </div>
            <span class="flex-1" />
            <Btn
              size="sm"
              variant="ghost"
              data-spec="mcp-guide-sample-copy"
              @click="copyText(sampleConfigJson, '示例配置已复制')"
            >
              <template #icon>
                <KIcon
                  name="Copy"
                  :size="13"
                />
              </template>
              复制示例
            </Btn>
          </div>
          <pre
            data-spec="mcp-guide-sample"
            class="mt-[4px] overflow-x-auto rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-fill-color-light)] p-[10px] font-mono text-[11.5px] leading-relaxed text-[var(--el-text-color-regular)]"
          >{{ sampleConfigJson }}</pre>
        </div>
      </div>
    </Card>

    <!-- ===== 区域 1：MCP 服务器（高级，默认折叠） ===== -->
    <Card padding="none">
      <div class="flex flex-wrap items-center gap-2 px-[14px] pt-[12px]">
        <button
          type="button"
          class="flex cursor-pointer items-center gap-[6px] text-left"
          data-spec="mcp-server-section-toggle"
          @click="serversSectionOpen = !serversSectionOpen"
        >
          <KIcon
            :name="serversSectionOpen ? 'ChevronDown' : 'ChevronRight'"
            :size="14"
            class="text-[var(--el-text-color-secondary)]"
          />
          <h3 class="text-[14px] font-semibold text-[var(--el-text-color-primary)]">
            远端 MCP 服务器
            <span class="ml-[6px] text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">{{ servers.length }} 个 · 高级</span>
          </h3>
        </button>
        <span class="flex-1" />
        <Btn
          v-if="serversSectionOpen"
          variant="primary"
          data-spec="mcp-server-create"
          @click="openServerCreate"
        >
          <template #icon>
            <KIcon
              name="Plus"
              :size="13"
            />
          </template>
          新增服务器
        </Btn>
      </div>
      <div
        v-if="!serversSectionOpen"
        class="px-[14px] pb-[12px] text-[11.5px] text-[var(--el-text-color-secondary)]"
      >
        登记外部 MCP 端点时使用；平台 Agent 接入只需上方「连接指引 + 令牌」，无需配置此项。
      </div>
      <template v-else>
        <div
          v-if="serversError"
          class="px-[14px] py-[14px] text-[12.5px] text-[var(--el-color-danger)]"
        >
          服务器列表加载失败：{{ serversError }}
        </div>
        <div
          v-else-if="!servers.length"
          data-spec="mcp-server-empty"
          class="px-[14px] py-[16px] text-[12.5px] text-[var(--el-text-color-secondary)]"
        >
          暂无注册的 MCP 服务器。点右上「新增服务器」登记远端 MCP 服务端点后在此显示。
        </div>
        <table
          v-else
          class="w-full border-collapse"
        >
          <thead>
            <tr>
              <th class="border-b border-[var(--el-border-color-light)] px-[14px] py-[8px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
                服务器
              </th>
              <th class="border-b border-[var(--el-border-color-light)] px-[14px] py-[8px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
                URL
              </th>
              <th class="border-b border-[var(--el-border-color-light)] px-[14px] py-[8px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
                归属项目
              </th>
              <th class="border-b border-[var(--el-border-color-light)] px-[14px] py-[8px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
                状态
              </th>
              <th class="border-b border-[var(--el-border-color-light)] px-[14px] py-[8px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
                工具数
              </th>
              <th class="border-b border-[var(--el-border-color-light)] px-[14px] py-[8px] text-right text-[11px] font-medium text-[var(--el-text-color-secondary)]">
                操作
              </th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="s in servers"
              :key="s.id"
              data-spec="mcp-server-row"
              class="hover:bg-[var(--el-bg-color-overlay)]"
            >
              <td class="border-b border-[var(--el-border-color-light)] px-[14px] py-[9px]">
                <div class="text-[12.5px] font-medium text-[var(--el-text-color-primary)]">
                  {{ s.name }}
                </div>
                <div
                  v-if="s.remark"
                  class="mt-[2px] text-[11px] text-[var(--el-text-color-secondary)]"
                >
                  {{ s.remark }}
                </div>
              </td>
              <td class="border-b border-[var(--el-border-color-light)] px-[14px] py-[9px] font-mono text-[12px] text-[var(--el-text-color-secondary)]">
                {{ s.url || '—' }}
              </td>
              <td class="border-b border-[var(--el-border-color-light)] px-[14px] py-[9px] text-[12px] text-[var(--el-text-color-secondary)]">
                {{ projectNameOf(s.projectId) }}
              </td>
              <td class="border-b border-[var(--el-border-color-light)] px-[14px] py-[9px]">
                <Tag :tone="s.enable ? 'ok' : 'default'">
                  {{ s.enable ? '启用' : '停用' }}
                </Tag>
              </td>
              <td class="border-b border-[var(--el-border-color-light)] px-[14px] py-[9px] font-mono text-[11.5px] tabular-nums text-[var(--el-text-color-secondary)]">
                {{ s.tools || '—' }}
              </td>
              <td class="border-b border-[var(--el-border-color-light)] px-[14px] py-[9px] text-right">
                <div class="flex justify-end gap-[6px]">
                  <button
                    type="button"
                    data-spec="mcp-server-toggle"
                    :disabled="isBusy('srv-enable:' + s.id)"
                    class="cursor-pointer rounded-[6px] border border-[var(--el-border-color-light)] px-[8px] py-[3px] text-[11.5px] text-[var(--el-text-color-primary)] hover:bg-[var(--el-fill-color-light)] disabled:cursor-not-allowed disabled:opacity-50"
                    @click="onServerToggle(s)"
                  >
                    <span
                      v-if="isBusy('srv-enable:' + s.id)"
                      class="inline-flex animate-spin"
                    >…</span>{{ s.enable ? '停用' : '启用' }}
                  </button>
                  <button
                    type="button"
                    data-spec="mcp-server-edit"
                    class="cursor-pointer rounded-[6px] border border-[var(--el-border-color-light)] px-[8px] py-[3px] text-[11.5px] text-[var(--el-color-primary)] hover:bg-[var(--el-fill-color-light)]"
                    @click="openServerEdit(s)"
                  >
                    编辑
                  </button>
                  <button
                    type="button"
                    data-spec="mcp-server-delete"
                    :disabled="isBusy('srv-del:' + s.id)"
                    class="cursor-pointer rounded-[6px] border border-[var(--el-border-color-light)] px-[8px] py-[3px] text-[11.5px] text-[var(--el-color-danger)] hover:bg-[var(--el-fill-color-light)] disabled:cursor-not-allowed disabled:opacity-50"
                    @click="serverDeleteTarget = s"
                  >
                    删除
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </template>
    </Card>

    <!-- ===== 区域 2：令牌管理 ===== -->
    <Card padding="none">
      <div class="flex flex-wrap items-center gap-2 px-[14px] pt-[12px]">
        <h3 class="text-[14px] font-semibold text-[var(--el-text-color-primary)]">
          MCP 令牌
          <span class="ml-[6px] text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">{{ displayTokens.length }} 枚</span>
        </h3>
        <span class="flex-1" />
        <select
          v-model.number="tokenProjectFilter"
          data-spec="mcp-token-project-filter"
          class="h-[28px] cursor-pointer rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px] text-[12px] text-[var(--el-text-color-primary)] outline-none"
        >
          <option :value="0">
            全部项目
          </option>
          <option :value="-1">
            未绑定项目
          </option>
          <option
            v-for="p in projects"
            :key="p.id"
            :value="p.id"
          >
            {{ p.name }}
          </option>
        </select>
        <Btn
          variant="primary"
          data-spec="mcp-token-create"
          @click="openCreate"
        >
          <template #icon>
            <KIcon
              name="Plus"
              :size="13"
            />
          </template>
          新增令牌
        </Btn>
      </div>
      <div class="px-[14px] pb-[8px] text-[11px] text-[var(--el-text-color-secondary)]">
        令牌为平台级资源；「按绑定项目过滤」仅筛列表，与表单里的「绑定项目」不是同一含义。资源白名单请点行内「资源授权」。
      </div>

      <!-- 加载 / 错误 -->
      <div
        v-if="loading"
        data-spec="mcp-token-loading"
        class="flex items-center gap-[8px] px-[14px] py-[18px] text-[12.5px] text-[var(--el-text-color-secondary)]"
      >
        <span class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
        令牌加载中…
      </div>
      <div
        v-else-if="error"
        data-spec="mcp-token-failed"
        class="flex items-center gap-[10px] px-[14px] py-[14px] text-[12.5px] text-[var(--el-color-danger)]"
      >
        令牌加载失败：{{ error }}
        <Btn
          size="sm"
          @click="emit('retry')"
        >
          重试
        </Btn>
      </div>
      <div
        v-else-if="!tokens.length"
        data-spec="mcp-token-empty"
        class="px-[14px] py-[22px] text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
      >
        还没有 MCP 令牌。点右上「新增令牌」创建一枚给外部 Agent 用。
      </div>

      <table
        v-else
        class="w-full border-collapse"
      >
        <thead>
          <tr>
            <th class="border-b border-[var(--el-border-color-light)] px-[14px] py-[8px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              令牌
            </th>
            <th class="border-b border-[var(--el-border-color-light)] px-[14px] py-[8px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              绑定
            </th>
            <th class="border-b border-[var(--el-border-color-light)] px-[14px] py-[8px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              Scope
            </th>
            <th class="border-b border-[var(--el-border-color-light)] px-[14px] py-[8px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              过期
            </th>
            <th class="border-b border-[var(--el-border-color-light)] px-[14px] py-[8px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              调用
            </th>
            <th class="border-b border-[var(--el-border-color-light)] px-[14px] py-[8px] text-right text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              操作
            </th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="row in displayTokens"
            :key="row.id"
            data-spec="mcp-token-row"
            :data-selected="selectedTokenId === row.id"
            class="cursor-pointer transition-colors hover:bg-[var(--el-bg-color-overlay)]"
            :class="selectedTokenId === row.id ? 'bg-[var(--el-fill-color-light)]' : ''"
            @click="onTokenRowClick(row)"
          >
            <td class="border-b border-[var(--el-border-color-light)] px-[14px] py-[10px]">
              <div class="flex items-center gap-[6px]">
                <b class="text-[12.5px] font-medium text-[var(--el-text-color-primary)]">{{ row.name }}</b>
                <Tag :tone="row.enable ? 'ok' : 'default'">
                  {{ row.enable ? '启用' : '停用' }}
                </Tag>
              </div>
              <div class="mt-[2px] font-mono text-[11px] text-[var(--el-text-color-secondary)]">
                {{ row.masked }}
              </div>
              <div
                v-if="row.remark"
                class="mt-[2px] text-[11px] text-[var(--el-text-color-secondary)]"
              >
                {{ row.remark }}
              </div>
            </td>
            <td class="border-b border-[var(--el-border-color-light)] px-[14px] py-[10px] text-[12px] text-[var(--el-text-color-secondary)]">
              <div>用户 {{ formatBoundUser(row.userId, row.userName) }}</div>
              <div>项目 {{ row.projectName }}</div>
            </td>
            <td class="border-b border-[var(--el-border-color-light)] px-[14px] py-[10px]">
              <div
                v-if="parseScopes(row.scopes).length"
                class="flex max-w-[220px] flex-wrap gap-[4px]"
              >
                <Tag
                  v-for="s in parseScopes(row.scopes)"
                  :key="s"
                  tone="default"
                >
                  {{ scopeLabel(s) }}
                </Tag>
              </div>
              <span
                v-else
                class="text-[12px] text-[var(--el-text-color-secondary)]"
              >仅 whoami</span>
            </td>
            <td class="border-b border-[var(--el-border-color-light)] px-[14px] py-[10px] font-mono text-[11.5px] text-[var(--el-text-color-secondary)]">
              {{ row.expireText }}
            </td>
            <td class="border-b border-[var(--el-border-color-light)] px-[14px] py-[10px]">
              <div class="font-mono text-[11.5px] tabular-nums text-[var(--el-text-color-primary)]">
                {{ row.callCount }} 次
              </div>
              <div
                v-if="row.lastText !== '—'"
                class="font-mono text-[11px] text-[var(--el-text-color-secondary)]"
              >
                {{ row.lastText }}
              </div>
              <div
                v-if="row.lastIP"
                class="font-mono text-[11px] text-[var(--el-text-color-secondary)]"
              >
                {{ row.lastIP }}
              </div>
            </td>
            <td
              class="border-b border-[var(--el-border-color-light)] px-[14px] py-[10px]"
              @click.stop
            >
              <div class="flex justify-end gap-[6px]">
                <button
                  type="button"
                  data-spec="mcp-token-toggle"
                  :disabled="isBusy('enable:' + row.id)"
                  class="cursor-pointer rounded-[6px] border border-[var(--el-border-color-light)] px-[8px] py-[3px] text-[11.5px] text-[var(--el-text-color-primary)] hover:bg-[var(--el-fill-color-light)] disabled:cursor-not-allowed disabled:opacity-50"
                  @click="onToggleEnable(row)"
                >
                  <span
                    v-if="isBusy('enable:' + row.id)"
                    class="inline-flex animate-spin"
                  >…</span>{{ row.enable ? '停用' : '启用' }}
                </button>
                <button
                  type="button"
                  data-spec="mcp-token-resources"
                  class="cursor-pointer rounded-[6px] border border-[var(--el-border-color-light)] px-[8px] py-[3px] text-[11.5px] text-[var(--el-text-color-primary)] hover:bg-[var(--el-fill-color-light)]"
                  @click="openResourceAuth(row)"
                >
                  资源授权
                </button>
                <button
                  type="button"
                  data-spec="mcp-token-edit"
                  class="cursor-pointer rounded-[6px] border border-[var(--el-border-color-light)] px-[8px] py-[3px] text-[11.5px] text-[var(--el-color-primary)] hover:bg-[var(--el-fill-color-light)]"
                  @click="openEdit(row)"
                >
                  编辑
                </button>
                <button
                  type="button"
                  data-spec="mcp-token-reset"
                  :disabled="isBusy('reset:' + row.id)"
                  class="cursor-pointer rounded-[6px] border border-[var(--el-border-color-light)] px-[8px] py-[3px] text-[11.5px] text-[var(--el-color-warning)] hover:bg-[var(--el-fill-color-light)] disabled:cursor-not-allowed disabled:opacity-50"
                  @click="resetTarget = row"
                >
                  <span
                    v-if="isBusy('reset:' + row.id)"
                    class="inline-flex animate-spin"
                  >…</span>重置
                </button>
                <button
                  type="button"
                  data-spec="mcp-token-delete"
                  :disabled="isBusy('del:' + row.id)"
                  class="cursor-pointer rounded-[6px] border border-[var(--el-border-color-light)] px-[8px] py-[3px] text-[11.5px] text-[var(--el-color-danger)] hover:bg-[var(--el-fill-color-light)] disabled:cursor-not-allowed disabled:opacity-50"
                  @click="deleteTarget = row"
                >
                  删除
                </button>
              </div>
            </td>
          </tr>
          <tr v-if="!displayTokens.length">
            <td
              colspan="6"
              data-spec="mcp-token-filter-empty"
              class="px-[14px] py-[14px] text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
            >
              当前项目过滤条件下没有令牌
            </td>
          </tr>
        </tbody>
      </table>
    </Card>

    <!-- ===== 区域 3：审计日志 ===== -->
    <Card padding="none">
      <div class="flex flex-wrap items-center gap-[8px] border-b border-[var(--el-border-color-light)] px-[14px] py-[10px]">
        <h3 class="text-[14px] font-semibold text-[var(--el-text-color-primary)]">
          调用审计
          <span
            v-if="auditMode === 'recent'"
            class="ml-[6px] text-[11.5px] font-normal text-[var(--el-text-color-secondary)]"
          >近期 {{ audits.length }} 条（平台级，跨项目）</span>
          <span
            v-else
            class="ml-[6px] text-[11.5px] font-normal text-[var(--el-text-color-secondary)]"
          >历史检索 共 {{ auditTotal }} 条</span>
        </h3>
        <span class="flex-1" />
        <select
          v-model.number="auditTokenId"
          data-spec="mcp-audit-token"
          class="h-[28px] cursor-pointer rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px] text-[12px] text-[var(--el-text-color-primary)] outline-none"
        >
          <option :value="0">
            全部令牌
          </option>
          <option
            v-for="t in tokens"
            :key="t.id"
            :value="t.id"
          >
            {{ t.name }}
          </option>
        </select>
        <div class="flex h-[28px] items-center gap-[6px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px]">
          <KIcon
            name="Search"
            :size="13"
            class="text-[var(--el-text-color)]"
          />
          <input
            v-model="auditIpQ"
            type="text"
            placeholder="按 IP 过滤（当前页）"
            data-spec="mcp-audit-ip"
            class="w-[130px] bg-transparent text-[12px] text-[var(--el-text-color-primary)] outline-none placeholder:text-[var(--el-text-color-placeholder)]"
          >
        </div>
        <select
          v-model="auditLevel"
          data-spec="mcp-audit-level"
          class="h-[28px] cursor-pointer rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px] text-[12px] text-[var(--el-text-color-primary)] outline-none"
        >
          <option value="all">
            全部结果
          </option>
          <option value="failed">
            仅失败
          </option>
          <option value="ok">
            仅成功
          </option>
        </select>
        <!-- 历史检索条件（服务端过滤） -->
        <template v-if="auditMode === 'query'">
          <input
            v-model="auditToolName"
            type="text"
            placeholder="工具名，如 stardust_node_list"
            data-spec="mcp-audit-tool"
            class="h-[28px] w-[200px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px] text-[12px] text-[var(--el-text-color-primary)] outline-none placeholder:text-[var(--el-text-color-placeholder)]"
          >
          <input
            v-model="auditDtStart"
            type="date"
            data-spec="mcp-audit-dtstart"
            class="h-[28px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[8px] text-[12px] text-[var(--el-text-color-primary)] outline-none"
          >
          <span class="text-[11px] text-[var(--el-text-color-secondary)]">至</span>
          <input
            v-model="auditDtEnd"
            type="date"
            data-spec="mcp-audit-dtend"
            class="h-[28px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[8px] text-[12px] text-[var(--el-text-color-primary)] outline-none"
          >
          <Btn
            size="sm"
            data-spec="mcp-audit-query"
            @click="enterAuditQuery"
          >
            查询
          </Btn>
          <Btn
            size="sm"
            variant="ghost"
            data-spec="mcp-audit-back"
            @click="exitAuditQuery"
          >
            返回近期
          </Btn>
        </template>
        <Btn
          v-else
          size="sm"
          variant="ghost"
          data-spec="mcp-audit-history"
          @click="enterAuditQuery"
        >
          查询历史…
        </Btn>
      </div>

      <!-- 历史检索：加载 / 错误 -->
      <div
        v-if="auditMode === 'query' && queryLoading"
        data-spec="mcp-audit-query-loading"
        class="flex items-center gap-[8px] px-[14px] py-[16px] text-[12.5px] text-[var(--el-text-color-secondary)]"
      >
        <span class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
        审计查询中…
      </div>
      <div
        v-else-if="auditMode === 'query' && queryError"
        data-spec="mcp-audit-query-failed"
        class="flex items-center gap-[10px] px-[14px] py-[14px] text-[12.5px] text-[var(--el-color-danger)]"
      >
        审计查询失败：{{ queryError }}
        <Btn
          size="sm"
          @click="enterAuditQuery"
        >
          重试
        </Btn>
      </div>

      <div
        v-else-if="!visibleAudits.length && (auditMode === 'recent' ? !audits.length : true)"
        data-spec="mcp-audit-empty"
        class="px-[14px] py-[20px] text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
      >
        {{ auditMode === 'recent' ? '还没有 MCP 调用记录' : '当前条件下没有记录（服务端已查全量历史）' }}
      </div>
      <table
        v-else
        class="w-full border-collapse"
      >
        <thead>
          <tr>
            <th class="w-[50px] border-b border-[var(--el-border-color-light)] px-[14px] py-[8px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]" />
            <th class="border-b border-[var(--el-border-color-light)] px-[14px] py-[8px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              工具调用
            </th>
            <th class="border-b border-[var(--el-border-color-light)] px-[14px] py-[8px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              令牌
            </th>
            <th class="border-b border-[var(--el-border-color-light)] px-[14px] py-[8px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              调用方
            </th>
            <th class="border-b border-[var(--el-border-color-light)] px-[14px] py-[8px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              耗时
            </th>
            <th class="border-b border-[var(--el-border-color-light)] px-[14px] py-[8px] text-right text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              时间
            </th>
          </tr>
        </thead>
        <tbody>
          <template
            v-for="r in visibleAudits"
            :key="r.id"
          >
            <tr
              data-spec="mcp-audit-row"
              class="cursor-pointer transition-colors hover:bg-[var(--el-bg-color-overlay)]"
              @click="expandedAuditId = expandedAuditId === r.id ? null : r.id"
            >
              <td class="border-b border-[var(--el-border-color-light)] px-[14px] py-[9px]">
                <Dot :tone="r.success ? 'ok' : 'err'" />
              </td>
              <td class="border-b border-[var(--el-border-color-light)] px-[14px] py-[9px]">
                <div class="flex items-center gap-[6px]">
                  <span class="text-[12.5px] font-medium text-[var(--el-text-color-primary)]">{{ r.toolLabel }}</span>
                  <KIcon
                    :name="expandedAuditId === r.id ? 'ChevronDown' : 'ChevronRight'"
                    :size="13"
                    class="text-[var(--el-text-color-secondary)]"
                  />
                </div>
                <div
                  v-if="!r.success && r.errorMessage"
                  class="mt-[2px] text-[11.5px] text-[var(--el-color-danger)]"
                >
                  {{ r.errorMessage }}
                </div>
              </td>
              <td class="border-b border-[var(--el-border-color-light)] px-[14px] py-[9px] text-[12px] text-[var(--el-text-color-secondary)]">
                {{ r.tokenName || `#${r.tokenId}` }}
              </td>
              <td class="border-b border-[var(--el-border-color-light)] px-[14px] py-[9px] font-mono text-[11.5px] text-[var(--el-text-color-secondary)]">
                {{ r.ip || '—' }}
              </td>
              <td class="border-b border-[var(--el-border-color-light)] px-[14px] py-[9px] font-mono text-[11.5px] tabular-nums text-[var(--el-text-color-secondary)]">
                {{ r.duration ? `${r.duration}ms` : '—' }}
              </td>
              <td class="border-b border-[var(--el-border-color-light)] px-[14px] py-[9px] text-right font-mono text-[11.5px] tabular-nums text-[var(--el-text-color-secondary)]">
                {{ r.time }}
              </td>
            </tr>
            <tr v-if="expandedAuditId === r.id">
              <td
                colspan="6"
                class="border-b border-[var(--el-border-color-light)] bg-[var(--el-fill-color-light)] px-[18px] py-[12px]"
              >
                <div class="text-[11px] font-medium text-[var(--el-text-color-secondary)]">
                  Arguments（入参，后端已脱敏）
                </div>
                <pre
                  v-if="r.args"
                  data-spec="mcp-audit-args"
                  class="mt-[4px] whitespace-pre-wrap rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color)] p-[10px] font-mono text-[12px] leading-relaxed text-[var(--el-text-color-regular)]"
                >{{ r.args }}</pre>
                <div
                  v-else
                  class="mt-[4px] text-[12px] text-[var(--el-text-color-secondary)]"
                >
                  无入参
                </div>
                <div class="mt-[8px] grid grid-cols-2 gap-x-[16px] gap-y-[4px] text-[11.5px] text-[var(--el-text-color-secondary)] min-[720px]:grid-cols-4">
                  <span>TraceId：{{ r.traceId || '—' }}</span>
                  <span>结果：{{ r.success ? '成功' : '失败' }}</span>
                  <span>风险级别：{{ riskLabel(r.riskLevel) }}</span>
                  <span>项目快照：{{ r.projectId || '—' }}</span>
                  <span>生效 Scope：{{ r.scopesUsed || '—' }}</span>
                  <span>UA：{{ r.ua || '—' }}</span>
                </div>
                <div
                  v-if="r.beforeValue || r.afterValue"
                  class="mt-[8px] grid grid-cols-1 gap-[8px] min-[720px]:grid-cols-2"
                >
                  <div v-if="r.beforeValue">
                    <div class="text-[11px] font-medium text-[var(--el-text-color-secondary)]">
                      Before（写操作前快照）
                    </div>
                    <pre
                      data-spec="mcp-audit-before"
                      class="mt-[4px] whitespace-pre-wrap rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color)] p-[10px] font-mono text-[12px] leading-relaxed text-[var(--el-text-color-regular)]"
                    >{{ r.beforeValue }}</pre>
                  </div>
                  <div v-if="r.afterValue">
                    <div class="text-[11px] font-medium text-[var(--el-text-color-secondary)]">
                      After（写操作后快照）
                    </div>
                    <pre
                      data-spec="mcp-audit-after"
                      class="mt-[4px] whitespace-pre-wrap rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color)] p-[10px] font-mono text-[12px] leading-relaxed text-[var(--el-text-color-regular)]"
                    >{{ r.afterValue }}</pre>
                  </div>
                </div>
              </td>
            </tr>
          </template>
          <tr v-if="!visibleAudits.length">
            <td
              colspan="6"
              class="px-[14px] py-[14px] text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
            >
              当前过滤条件下没有记录
            </td>
          </tr>
        </tbody>
      </table>
      <!-- 历史检索分页 -->
      <div
        v-if="auditMode === 'query' && !queryLoading && !queryError"
        class="flex items-center justify-end gap-[10px] border-t border-[var(--el-border-color-light)] px-[14px] py-[8px] text-[11.5px] text-[var(--el-text-color-secondary)]"
      >
        <span>共 {{ auditTotal }} 条 · 第 {{ auditPageIndex + 1 }} 页 / 每页 {{ AUDIT_PAGE_SIZE }} 条</span>
        <Btn
          size="sm"
          variant="ghost"
          data-spec="mcp-audit-prev"
          :disabled="!auditHasPrev"
          @click="auditPrevPage"
        >
          上一页
        </Btn>
        <Btn
          size="sm"
          variant="ghost"
          data-spec="mcp-audit-next"
          :disabled="!auditHasNext"
          @click="auditNextPage"
        >
          下一页
        </Btn>
      </div>
    </Card>

    <!-- ===== 新建/编辑令牌 Drawer ===== -->
    <McpTokenForm
      :open="formOpen"
      :mode="formMode"
      :token="editingToken"
      :projects="projects"
      :users="users"
      :members="formMembers"
      :members-ready="formMembersReady"
      :submitting="submitting"
      @close="formOpen = false"
      @submit="onFormSubmit"
      @project="loadFormMembers"
    />

    <!-- ===== 新建/编辑服务器 Drawer ===== -->
    <McpServerForm
      :open="serverFormOpen"
      :mode="serverFormMode"
      :server="editingServer"
      :projects="projects"
      :submitting="serverSubmitting"
      @close="serverFormOpen = false"
      @submit="onServerSubmit"
    />

    <!-- ===== 删除确认（令牌） ===== -->
    <McpTokenResourceDrawer
      :open="resourceDrawerOpen"
      :token="resourceDrawerToken"
      :resources="resources"
      :projects="projects"
      :nodes="nodes"
      :apps="apps"
      :busy="resourceBusy"
      @close="closeResourceDrawer"
      @add="onAddResource"
      @remove="onRemoveResource"
      @toggle="onToggleResource"
    />

    <GuardModal
      v-if="deleteTarget"
      :label="`删除 MCP 令牌「${deleteTarget.name}」？审计日志保留`"
      @cancel="deleteTarget = null"
      @confirm="onDeleteConfirm"
    />

    <GuardModal
      v-if="resetTarget"
      :label="`重置令牌「${resetTarget.name}」？旧 API Key 立即失效，所有客户端需更换 Bearer。`"
      @cancel="resetTarget = null"
      @confirm="onResetConfirm"
    />

    <!-- ===== 删除确认（服务器） ===== -->
    <GuardModal
      v-if="serverDeleteTarget"
      :label="`删除 MCP 服务器「${serverDeleteTarget.name}」？`"
      @cancel="serverDeleteTarget = null"
      @confirm="onServerDeleteConfirm"
    />

    <!-- ===== EnableMcp 总开关确认（写 StarServerSetting 配置文件） ===== -->
    <GuardModal
      v-if="enableMcpTarget !== null"
      :label="enableMcpTarget ? '开启 MCP 服务（EnableMcp）？开启后 /mcp 端点对外可用，配置立即保存生效。' : '关闭 MCP 服务（EnableMcp）？关闭后所有 MCP 客户端立即无法连接，配置立即保存生效。'"
      @cancel="enableMcpTarget = null"
      @confirm="onEnableMcpConfirm"
    />

    <!-- ===== 一次性完整 Token 展示 ===== -->
    <Teleport to="body">
      <div
        v-if="oneTimeToken"
        data-spec="mcp-once-token"
        class="fixed inset-0 z-[999] flex items-center justify-center bg-[var(--el-mask-color)] p-4"
        @click.self="oneTimeToken = ''"
      >
        <div class="w-full max-w-[520px] rounded-[12px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color)] p-[20px] shadow-xl">
          <div class="text-[14px] font-semibold text-[var(--el-text-color-primary)]">
            完整 Token 只显示这一次
          </div>
          <p class="mt-[6px] text-[12px] leading-relaxed text-[var(--el-text-color-secondary)]">
            请立即复制并妥善保存。关闭后页面只保留掩码值，完整值不再展示。
          </p>
          <div
            class="mt-[12px] select-all break-all rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-fill-color-light)] p-[12px] font-mono text-[13px] leading-relaxed text-[var(--el-text-color-primary)]"
            data-spec="mcp-once-token-value"
          >
            {{ oneTimeToken }}
          </div>
          <div class="mt-[14px] flex justify-end gap-[8px]">
            <Btn
              variant="ghost"
              @click="oneTimeToken = ''"
            >
              关闭
            </Btn>
            <Btn
              variant="primary"
              data-spec="mcp-once-copy"
              @click="copyToken"
            >
              <template #icon>
                <KIcon
                  name="Copy"
                  :size="13"
                />
              </template>
              复制 Token
            </Btn>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>
