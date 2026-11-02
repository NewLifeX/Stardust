<script setup lang="ts">
/**
 * McpTokenForm：新增/编辑 MCP 令牌的 Drawer 表单（纯 UI + 本地状态，提交只发事件）。
 * 字段：名称、启用、绑定用户、绑定项目、Scope（域 × 读/写/高危勾选）、过期时间、备注。
 * 创建后由调用方一次性展示完整 Token 值；本表单不接触明文 Token。
 * 提交按钮在名称为空或提交中时禁用；样式只用 --el-* + Tailwind。
 */
import { computed, ref, watch } from 'vue';
import Btn from './Btn.vue';
import Drawer from './Drawer/Drawer.vue';
import {
  parseScopes,
  previewDefaultProjectScopes,
  SCOPE_DOMAINS,
  SCOPE_LEVELS,
  type McpOption,
  type McpTokenFullRow,
  type McpTokenFormModel,
} from './mcpToken.logic.ts';
import { memberSaveBlock, memberUserOptions, type MemberLink } from './projectMember.logic.ts';

const props = withDefaults(defineProps<{
  open: boolean;
  mode: 'create' | 'edit';
  /** 编辑模式的初始值 */
  token?: McpTokenFullRow | null;
  projects: McpOption[];
  users: McpOption[];
  /** 各项目的 ProjectUser。用户下拉只显示当前所选项目的启用成员。 */
  members?: MemberLink[];
  /** 成员列表是否已经按当前项目拉完。未完成时不把空列表当成「不是成员」。 */
  membersReady?: boolean;
  submitting?: boolean;
}>(), {
  token: null,
  members: () => [],
  membersReady: true,
  submitting: false,
});

const emit = defineEmits<{
  close: [];
  submit: [form: McpTokenFormModel];
  /** 绑定项目变化（含打开时的初值），父组件据此重拉该项目的成员 */
  project: [projectId: number];
}>();

const inputCls =
  'h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]';

// ---------- 表单状态（每次打开时按 token 重置） ----------
const name = ref('');
const enable = ref(true);
const userId = ref(0);
const projectId = ref(0);
const expireIso = ref('');
const remark = ref('');
/** 勾选集合："apps:read" 形式 */
const checked = ref<Set<string>>(new Set());

/** 毫秒戳 → datetime-local 需要的本地 "YYYY-MM-DDTHH:mm" */
function toLocalInput(ms: number): string {
  const d = new Date(ms);
  const p = (n: number): string => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
}

function resetForm(): void {
  const t = props.token;
  name.value = t?.name ?? '';
  enable.value = t?.enable ?? true;
  userId.value = t?.userId ?? 0;
  projectId.value = t?.projectId ?? 0;
  expireIso.value = t?.expireMs ? toLocalInput(t.expireMs) : '';
  remark.value = t?.remark ?? '';
  checked.value = new Set(parseScopes(t?.scopes ?? ''));
  emit('project', projectId.value);
}

watch(
  () => props.open,
  (v) => { if (v) resetForm(); },
  { immediate: true },
);

function toggleScope(domain: string, level: string): void {
  const key = `${domain}:${level}`;
  if (checked.value.has(key)) checked.value.delete(key);
  else checked.value.add(key);
}

const filteredUsers = computed(() => memberUserOptions(projectId.value, props.users, props.members ?? []));

const userHint = computed(() => {
  if (projectId.value <= 0) return '请先选择绑定项目，再选择该项目的成员';
  if (props.membersReady === false) return '正在加载该项目的成员…';
  if (!filteredUsers.value.length) return '该项目还没有启用的成员。可先到治理 → 平台设置添加，或保持「未绑定」。';
  return '';
});

const saveBlock = computed(() => memberSaveBlock(projectId.value, userId.value, props.members ?? [], props.membersReady !== false));

const orphanUser = computed(() => userId.value > 0 && !filteredUsers.value.some((u) => u.id === userId.value));

/** 用户改了项目之后，等新成员列表回来再丢掉不属于该项目的用户。打开编辑时不自动改绑定。 */
const dropStaleUser = ref(false);

watch(
  () => props.membersReady,
  (ready) => {
    if (!ready || !dropStaleUser.value) return;
    dropStaleUser.value = false;
    if (userId.value > 0 && !filteredUsers.value.some((u) => u.id === userId.value)) userId.value = 0;
  },
);

function onProjectChange(): void {
  if (projectId.value <= 0) userId.value = 0;
  else dropStaleUser.value = true;
  emit('project', projectId.value);
}

const canSubmit = computed(() => name.value.trim().length > 0 && !props.submitting && !saveBlock.value);

const scopePreview = computed(() => {
  const csv = [...checked.value].join(',');
  return previewDefaultProjectScopes(projectId.value, csv);
});

function submit(): void {
  if (!canSubmit.value) return;
  emit('submit', {
    name: name.value,
    enable: enable.value,
    userId: userId.value,
    projectId: projectId.value,
    scopes: [...checked.value].join(','),
    expireIso: expireIso.value,
    remark: remark.value,
  });
}
</script>

<template>
  <Drawer
    :open="open"
    :title="mode === 'create' ? '新增 MCP 令牌' : '编辑 MCP 令牌'"
    sub="权限 = 绑定用户在项目内的权限 ∩ 令牌 Scope"
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
          data-spec="mcp-form-name"
          placeholder="如 运维助手-小萌"
          :class="inputCls"
        >
      </label>

      <!-- 启用 -->
      <label class="flex cursor-pointer items-center gap-[8px] text-[12.5px] text-[var(--el-text-color-regular)]">
        <input
          v-model="enable"
          type="checkbox"
          data-spec="mcp-form-enable"
          class="h-[14px] w-[14px] accent-[var(--el-color-primary)]"
        >
        创建后立即启用（停用后所有调用拒绝）
      </label>

      <!-- 先选项目，再只列出该项目的启用成员 -->
      <div class="grid grid-cols-1 gap-[12px] min-[480px]:grid-cols-2">
        <label class="block">
          <div class="mb-[5px] text-[12px] font-medium text-[var(--el-text-color-regular)]">
            绑定项目
          </div>
          <select
            v-model.number="projectId"
            data-spec="mcp-form-project"
            :class="[inputCls, 'cursor-pointer']"
            @change="onProjectChange"
          >
            <option :value="0">
              未绑定（仅自检 whoami 可用）
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
        <label class="block">
          <div class="mb-[5px] text-[12px] font-medium text-[var(--el-text-color-regular)]">
            绑定用户
          </div>
          <select
            v-model.number="userId"
            data-spec="mcp-form-user"
            :class="[inputCls, 'cursor-pointer']"
          >
            <option :value="0">
              未绑定（不继承用户权限）
            </option>
            <option
              v-if="orphanUser"
              :value="userId"
            >
              当前用户 #{{ userId }}（不是项目成员）
            </option>
            <option
              v-for="u in filteredUsers"
              :key="u.id"
              :value="u.id"
            >
              {{ u.name }} (#{{ u.id }})
            </option>
          </select>
          <div
            v-if="userHint"
            data-spec="mcp-form-user-hint"
            class="mt-[4px] text-[11px] text-[var(--el-text-color-secondary)]"
          >
            {{ userHint }}
          </div>
        </label>
      </div>
      <div
        v-if="saveBlock"
        data-spec="mcp-form-member-block"
        class="rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-fill-color-light)] px-[10px] py-[8px] text-[12px] text-[var(--el-text-color-regular)]"
      >
        {{ saveBlock }}
      </div>

      <!-- 工具域权限（Scope） -->
      <div>
        <div class="mb-[5px] text-[12px] font-medium text-[var(--el-text-color-regular)]">
          工具域权限（Scope）
          <span class="ml-[6px] text-[11px] font-normal text-[var(--el-text-color-secondary)]">不勾选且已绑项目时，保存后应用平台默认 7 域只读</span>
        </div>
        <div
          v-if="scopePreview"
          class="mb-[8px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-fill-color-light)] px-[10px] py-[8px] text-[11.5px] leading-relaxed text-[var(--el-text-color-regular)]"
          data-spec="mcp-form-scope-preview"
        >
          将写入：{{ scopePreview }}
        </div>
        <div class="rounded-[10px] border border-[var(--el-border-color-light)] p-[10px]">
          <div
            v-for="dom in SCOPE_DOMAINS"
            :key="dom.key"
            class="flex flex-wrap items-center gap-x-[14px] gap-y-[6px] border-b border-[var(--el-border-color-light)] py-[6px] last:border-0"
          >
            <span class="w-[52px] text-[12px] font-medium text-[var(--el-text-color-primary)]">{{ dom.label }}</span>
            <label
              v-for="lv in SCOPE_LEVELS"
              :key="lv.key"
              class="flex cursor-pointer items-center gap-[5px] text-[12px] text-[var(--el-text-color-regular)]"
              :title="lv.hint"
            >
              <input
                type="checkbox"
                :data-spec="`mcp-form-scope-${dom.key}-${lv.key}`"
                :checked="checked.has(`${dom.key}:${lv.key}`)"
                class="h-[13px] w-[13px] accent-[var(--el-color-primary)]"
                @change="toggleScope(dom.key, lv.key)"
              >
              {{ lv.label }}
            </label>
          </div>
        </div>
      </div>

      <!-- 过期时间 -->
      <label class="block">
        <div class="mb-[5px] text-[12px] font-medium text-[var(--el-text-color-regular)]">
          过期时间
          <span class="ml-[6px] text-[11px] font-normal text-[var(--el-text-color-secondary)]">留空 = 永不过期</span>
        </div>
        <input
          v-model="expireIso"
          type="datetime-local"
          data-spec="mcp-form-expire"
          :class="inputCls"
        >
      </label>

      <!-- 备注 -->
      <label class="block">
        <div class="mb-[5px] text-[12px] font-medium text-[var(--el-text-color-regular)]">
          备注
        </div>
        <textarea
          v-model="remark"
          rows="2"
          data-spec="mcp-form-remark"
          placeholder="这枚令牌给谁、做什么"
          class="w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] py-[7px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
        />
      </label>
    </div>

    <template #actions>
      <Btn
        variant="ghost"
        data-spec="mcp-form-cancel"
        @click="emit('close')"
      >
        取消
      </Btn>
      <Btn
        variant="primary"
        :disabled="!canSubmit"
        data-spec="mcp-form-submit"
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
        {{ submitting ? '保存中…' : mode === 'create' ? '创建令牌' : '保存修改' }}
      </Btn>
    </template>
  </Drawer>
</template>
