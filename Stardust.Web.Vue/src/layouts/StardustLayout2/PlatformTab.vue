<script setup lang="ts">
/**
 * PlatformTab：治理页「平台设置」磁贴 + 项目成员管理。
 * 成员来自 ProjectUser：展示 UserName，可添加（当前顶栏项目）、停用/启用、移除。
 * 写操作走 projectMember.logic 的 REST；移除和停用经 GuardModal。顶栏未选具体项目时添加按钮禁用，原因写在按钮旁。
 */
import { computed, ref } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import Btn from './Btn.vue';
import Card from './Card.vue';
import Drawer from './Drawer/Drawer.vue';
import GuardModal from './GuardModal/GuardModal.vue';
import KIcon from './KIcon.vue';
import { H3, TD, TH } from './deployView.styles.ts';
import type { MemberRow, PlatformTile, ProjectInfo } from './mesh.logic.ts';
import { toast } from './useToast';
import {
  addMember,
  addMemberBlockReason,
  candidateUsers,
  feedbackRemainMs,
  fetchUserOptions,
  removeMember,
  setMemberEnable,
  type UserOption,
} from './projectMember.logic.ts';

const req = request as Req;

const props = withDefaults(
  defineProps<{
    tiles: PlatformTile[];
    members?: MemberRow[];
    projectInfo?: ProjectInfo | null;
    /** 顶栏当前项目；null 表示「全部项目」，不能添加成员 */
    projectId?: number | null;
    loading?: boolean;
    error?: string;
  }>(),
  { loading: false, error: '', members: () => [], projectInfo: null, projectId: null },
);

const emit = defineEmits<{ retry: [] }>();

const namePending = computed(() => props.members.some((m) => m.name.startsWith('#')));
const contextBlock = computed(() => (props.projectId == null || props.projectId <= 0
  ? '请先在顶栏选择一个具体项目，再添加成员'
  : ''));

const drawerOpen = ref(false);
const opening = ref(false);
const users = ref<UserOption[]>([]);
const usersError = ref('');
const pickedUserId = ref(0);
const enable = ref(true);
const adding = ref(false);
const rowBusyId = ref(0);
const removeTarget = ref<MemberRow | null>(null);
const stopTarget = ref<MemberRow | null>(null);

const candidates = computed(() => candidateUsers(users.value, props.members));
const submitBlock = computed(() => addMemberBlockReason(props.projectId, pickedUserId.value, props.members));

const inputCls =
  'h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]';

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => { setTimeout(resolve, ms); });
}

async function openAdd(): Promise<void> {
  if (contextBlock.value || opening.value) return;
  opening.value = true;
  const started = Date.now();
  drawerOpen.value = true;
  pickedUserId.value = 0;
  enable.value = true;
  usersError.value = '';
  try {
    const r = await fetchUserOptions(req);
    users.value = r.users;
    usersError.value = r.error;
    if (r.error) toast(r.error, 'err');
  } finally {
    const rest = feedbackRemainMs(Date.now() - started);
    if (rest > 0) await sleep(rest);
    opening.value = false;
  }
}

async function submitAdd(): Promise<void> {
  if (submitBlock.value || adding.value || props.projectId == null) return;
  adding.value = true;
  const started = Date.now();
  try {
    const r = await addMember(req, { projectId: props.projectId, userId: pickedUserId.value, enable: enable.value });
    const rest = feedbackRemainMs(Date.now() - started);
    if (rest > 0) await sleep(rest);
    toast(r.message, r.ok ? 'ok' : 'err');
    if (r.ok) {
      drawerOpen.value = false;
      emit('retry');
    }
  } finally {
    adding.value = false;
  }
}

async function confirmRemove(): Promise<void> {
  const row = removeTarget.value;
  removeTarget.value = null;
  if (!row || rowBusyId.value) return;
  rowBusyId.value = row.id;
  const started = Date.now();
  try {
    const r = await removeMember(req, row.id, row.name);
    const rest = feedbackRemainMs(Date.now() - started);
    if (rest > 0) await sleep(rest);
    toast(r.message, r.ok ? 'ok' : 'err');
    if (r.ok) emit('retry');
  } finally {
    rowBusyId.value = 0;
  }
}

function askToggle(row: MemberRow): void {
  if (rowBusyId.value) return;
  if (row.enable) stopTarget.value = row;
  else void runToggle(row);
}

async function confirmStop(): Promise<void> {
  const row = stopTarget.value;
  stopTarget.value = null;
  if (row) await runToggle(row);
}

async function runToggle(row: MemberRow): Promise<void> {
  if (rowBusyId.value) return;
  rowBusyId.value = row.id;
  const started = Date.now();
  try {
    const r = await setMemberEnable(req, row.id, !row.enable, row.name);
    const rest = feedbackRemainMs(Date.now() - started);
    if (rest > 0) await sleep(rest);
    toast(r.message, r.ok ? 'ok' : 'err');
    if (r.ok) emit('retry');
  } finally {
    rowBusyId.value = 0;
  }
}
</script>

<template>
  <div>
    <div
      v-if="loading"
      data-spec="platform-loading"
      class="flex items-center gap-[8px] px-[4px] py-[18px] text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      <span class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
      平台设置加载中…
    </div>

    <div
      v-else-if="error"
      data-spec="platform-failed"
      class="px-[4px] py-[14px]"
    >
      <div class="text-[12.5px] text-[var(--el-color-danger)]">
        平台设置加载失败：{{ error }}
      </div>
      <button
        type="button"
        data-spec="platform-retry"
        class="mt-[8px] cursor-pointer rounded-[6px] border border-[var(--el-border-color-light)] px-[10px] py-[4px] text-[12px] text-[var(--el-text-color-regular)] hover:bg-[var(--el-bg-color-overlay)]"
        @click="emit('retry')"
      >
        重试
      </button>
    </div>

    <div
      v-else
      class="grid grid-cols-2 gap-[12px] md:grid-cols-4"
    >
      <div
        v-for="t in tiles"
        :key="t.key"
        data-spec="platform-tile"
        :data-tile="t.key"
      >
        <Card>
          <KIcon
            :name="t.icon as never"
            :size="16"
            class="mb-[8px]"
          />
          <div class="mb-[3px] block text-[12.5px] font-medium text-[var(--el-text-color-primary)]">
            {{ t.title }}
          </div>
          <span
            class="block text-[11.5px] leading-[1.5] text-[var(--el-text-color-secondary)]"
            :data-pending="t.status === 'pending'"
          >{{ t.detail }}</span>
        </Card>
      </div>
    </div>

    <Card
      v-if="!loading && !error"
      padding="none"
      data-spec="platform-members"
      class="mt-[12px]"
    >
      <div class="flex flex-wrap items-center gap-2 px-[14px] pt-[12px]">
        <h3 :class="H3">
          项目成员
          <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">{{ members.length }} 位</span>
        </h3>
        <span class="ml-auto inline-flex items-center gap-[8px]">
          <span
            v-if="contextBlock"
            data-spec="platform-member-add-reason"
            class="text-[11.5px] text-[var(--el-color-warning)]"
          >{{ contextBlock }}</span>
          <Btn
            size="sm"
            variant="primary"
            data-spec="platform-member-add"
            :disabled="!!contextBlock || opening"
            :aria-busy="opening"
            @click="openAdd"
          >
            <template #icon>
              <span :class="opening ? 'inline-flex animate-spin' : 'inline-flex'">
                <KIcon
                  name="Plus"
                  :size="12"
                />
              </span>
            </template>
            {{ opening ? '加载用户…' : '添加成员' }}
          </Btn>
        </span>
      </div>

      <div
        v-if="projectInfo"
        data-spec="platform-project"
        class="flex flex-wrap gap-x-[18px] gap-y-[4px] px-[14px] pt-[10px] text-[11.5px] text-[var(--el-text-color-secondary)]"
      >
        <span>
          项目 <b class="font-medium text-[var(--el-text-color-primary)]">{{ projectInfo.name }}</b>
        </span>
        <span>环境 {{ projectInfo.environment }}</span>
        <span>创建 {{ projectInfo.createdText }}</span>
      </div>

      <table class="w-full border-collapse">
        <thead>
          <tr>
            <th :class="TH">
              用户名
            </th>
            <th :class="TH">
              状态
            </th>
            <th :class="TH">
              加入时间
            </th>
            <th :class="TH">
              操作
            </th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="m in members"
            :key="m.id"
            data-spec="platform-member-row"
            class="hover:bg-[var(--el-bg-color-overlay)]"
          >
            <td :class="TD">
              <span class="text-[12.5px] font-medium text-[var(--el-text-color-primary)]">{{ m.name }}</span>
              <span
                v-if="m.role && m.role !== '成员'"
                class="ml-[6px] text-[11px] text-[var(--el-text-color-secondary)]"
              >{{ m.role }}</span>
            </td>
            <td :class="TD">
              <span
                class="text-[12.5px]"
                :class="m.enable ? 'text-[var(--el-color-success)]' : 'text-[var(--el-text-color-secondary)]'"
              >{{ m.enable ? '启用' : '已停用' }}</span>
            </td>
            <td :class="TD">
              <span class="font-mono text-[11.5px] text-[var(--el-text-color-secondary)]">{{ m.joinedText }}</span>
            </td>
            <td :class="TD">
              <span class="inline-flex gap-[6px]">
                <button
                  type="button"
                  data-spec="platform-member-toggle"
                  class="cursor-pointer rounded-[6px] border border-[var(--el-border-color-light)] px-[8px] py-[3px] text-[11.5px] text-[var(--el-text-color-regular)] hover:bg-[var(--el-bg-color-overlay)] disabled:cursor-not-allowed disabled:opacity-60"
                  :disabled="rowBusyId === m.id"
                  :aria-busy="rowBusyId === m.id"
                  @click="askToggle(m)"
                >
                  <span :class="rowBusyId === m.id ? 'inline-block animate-spin' : ''">{{ m.enable ? '停用' : '启用' }}</span>
                </button>
                <button
                  type="button"
                  data-spec="platform-member-remove"
                  class="cursor-pointer rounded-[6px] border border-[var(--el-border-color-light)] px-[8px] py-[3px] text-[11.5px] text-[var(--el-color-danger)] hover:bg-[var(--el-bg-color-overlay)] disabled:cursor-not-allowed disabled:opacity-60"
                  :disabled="rowBusyId === m.id"
                  @click="removeTarget = m"
                >
                  移除
                </button>
              </span>
            </td>
          </tr>
          <tr v-if="!members.length">
            <td
              colspan="4"
              data-spec="platform-members-empty"
              class="px-[14px] py-[18px] text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
            >
              项目还没有成员
            </td>
          </tr>
        </tbody>
      </table>

      <div
        v-if="members.length && namePending"
        data-spec="platform-member-name-pending"
        class="px-[14px] pb-[12px] text-[11px] leading-[1.5] text-[var(--el-text-color-secondary)]"
      >
        列表没有返回 UserName，这些行按 UserId 显示。
      </div>
    </Card>

    <Drawer
      :open="drawerOpen"
      title="添加项目成员"
      sub="加入后，该用户绑定到此项目的 MCP 令牌才能调用业务工具"
      @close="drawerOpen = false"
    >
      <div class="flex flex-col gap-[14px]">
        <div class="text-[12.5px] text-[var(--el-text-color-regular)]">
          项目
          <b class="font-medium text-[var(--el-text-color-primary)]">{{ projectInfo?.name || (projectId ? `#${projectId}` : '—') }}</b>
        </div>
        <label class="block">
          <div class="mb-[5px] text-[12px] font-medium text-[var(--el-text-color-regular)]">
            用户 <span class="text-[var(--el-color-danger)]">*</span>
          </div>
          <select
            v-model.number="pickedUserId"
            data-spec="platform-member-user"
            :class="[inputCls, 'cursor-pointer']"
            :disabled="opening"
          >
            <option :value="0">
              请选择用户
            </option>
            <option
              v-for="u in candidates"
              :key="u.id"
              :value="u.id"
            >
              {{ u.name }} (#{{ u.id }})
            </option>
          </select>
          <div
            v-if="usersError"
            class="mt-[4px] text-[11.5px] text-[var(--el-color-danger)]"
          >
            {{ usersError }}
          </div>
          <div
            v-else-if="!opening && !candidates.length"
            class="mt-[4px] text-[11.5px] text-[var(--el-text-color-secondary)]"
          >
            没有可添加的用户（用户列表为空，或都已是成员）。
          </div>
        </label>
        <label class="flex cursor-pointer items-center gap-[8px] text-[12.5px] text-[var(--el-text-color-regular)]">
          <input
            v-model="enable"
            type="checkbox"
            data-spec="platform-member-enable"
            class="h-[14px] w-[14px] accent-[var(--el-color-primary)]"
          >
          立即启用（停用的成员不能通过 MCP 访问该项目）
        </label>
        <div
          v-if="submitBlock"
          data-spec="platform-member-submit-reason"
          class="rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-fill-color-light)] px-[10px] py-[8px] text-[12px] text-[var(--el-text-color-regular)]"
        >
          {{ submitBlock }}
        </div>
      </div>
      <template #actions>
        <Btn
          variant="ghost"
          data-spec="platform-member-cancel"
          @click="drawerOpen = false"
        >
          取消
        </Btn>
        <Btn
          variant="primary"
          data-spec="platform-member-submit"
          :disabled="!!submitBlock || adding"
          :aria-busy="adding"
          @click="submitAdd"
        >
          <template
            v-if="adding"
            #icon
          >
            <span class="inline-flex animate-spin">
              <span class="h-[12px] w-[12px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-white)]" />
            </span>
          </template>
          {{ adding ? '添加中…' : '添加成员' }}
        </Btn>
      </template>
    </Drawer>

    <GuardModal
      v-if="removeTarget"
      :label="`把「${removeTarget.name}」移出当前项目？该用户已绑定此项目的 MCP 令牌将不能再调用业务工具。`"
      @cancel="removeTarget = null"
      @confirm="confirmRemove"
    />
    <GuardModal
      v-if="stopTarget"
      :label="`停用成员「${stopTarget.name}」？停用后该用户访问此项目的 MCP 业务工具会被拒绝。`"
      @cancel="stopTarget = null"
      @confirm="confirmStop"
    />
  </div>
</template>
