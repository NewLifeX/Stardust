<script setup lang="ts">
/**
 * PlatformTab：治理页「平台设置」磁贴 + 项目管理 + 项目成员管理。
 * 项目管理（CARD-PLT-01）：全量列表（fetchPlatform 的 galaxyRows，零新增列表请求）+ 筛选 + 表单抽屉 + 删除确认 + 顶栏联动。
 * 成员来自 ProjectUser：展示 UserName，可添加（当前顶栏项目）、停用/启用、移除。
 * 写操作走 projectMember.logic / galaxyProject.logic 的 REST；移除/停用/删除项目经 GuardModal。顶栏未选具体项目时添加按钮禁用，原因写在按钮旁。
 */
import { computed, ref, watch } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import Btn from './Btn.vue';
import Card from './Card.vue';
import Drawer from './Drawer/Drawer.vue';
import GalaxyProjectFormDrawer from './GalaxyProjectFormDrawer.vue';
import GuardModal from './GuardModal/GuardModal.vue';
import KIcon from './KIcon.vue';
import StarServerSettingsForm from './StarServerSettingsForm.vue';
import { H3, TD, TH } from './deployView.styles.ts';
import type { MemberRow, PlatformTile, ProjectInfo } from './mesh.logic.ts';
import { toast } from './useToast';
import {
  contextAfterDelete,
  createProject,
  deleteProject,
  emptyProjectForm,
  feedbackRemainMs,
  filterProjectRows,
  type ProjectFilter,
  type ProjectFormModel,
  type ProjectRow,
  projectToForm,
  updateProject,
  validateProjectName,
} from './galaxyProject.logic.ts';
import {
  addMember,
  addMemberBlockReason,
  candidateUsers,
  fetchMemberLinks,
  fetchUserOptions,
  removeMember,
  setMemberEnable,
  type MemberLink,
  type MemberQuery,
  type UserOption,
} from './projectMember.logic.ts';
import { setStardustProject } from '@/stores/stardustContext';

const req = request as Req;

const props = withDefaults(
  defineProps<{
    tiles: PlatformTile[];
    /** 星系项目全量行（fetchPlatform 归一）；「项目管理」区块数据源 */
    projects?: ProjectRow[];
    members?: MemberRow[];
    projectInfo?: ProjectInfo | null;
    /** 顶栏当前项目；null 表示「全部项目」，不能添加成员 */
    projectId?: number | null;
    loading?: boolean;
    error?: string;
  }>(),
  { loading: false, error: '', projects: () => [], members: () => [], projectInfo: null, projectId: null },
);

const emit = defineEmits<{
  retry: [];
  edit: [];
  /** 项目增删改成功：MeshPage 重拉平台 tab 并 bump projectsVersion（顶栏重拉项目列表） */
  projectChanged: [];
}>();

const namePending = computed(() => props.members.some((m) => m.name.startsWith('#')));
const contextBlock = computed(() => (props.projectId == null || props.projectId <= 0
  ? '请先在顶栏选择一个具体项目，再添加成员'
  : ''));

/** 「环境」行：后端 GalaxyProject 无 Environment 字段，值非空才显示（CARD-PLT-01 定稿裁定 1） */
const hasEnvironment = computed(() => !!props.projectInfo?.environment && props.projectInfo.environment !== '—');

// ---- 项目管理（CARD-PLT-01）----
const projectFilter = ref<ProjectFilter>({ keyword: '', dtStart: '', dtEnd: '' });
const filteredProjects = computed(() => filterProjectRows(props.projects, projectFilter.value));
const filterActive = computed(() =>
  projectFilter.value.keyword.trim() !== '' || projectFilter.value.dtStart !== '' || projectFilter.value.dtEnd !== '');
const projectEmpty = computed(() => props.projects.length === 0);
const projectEmptyFiltered = computed(() => props.projects.length > 0 && filteredProjects.value.length === 0);
const emptyFilteredText = computed(() =>
  projectFilter.value.keyword.trim() ? `没有匹配「${projectFilter.value.keyword.trim()}」的项目` : '没有匹配筛选条件的项目');

const formOpen = ref(false);
const formBusy = ref(false);
const formNameError = ref('');
const formModel = ref<ProjectFormModel>(emptyProjectForm());
/** 编辑对象；null=新建（抽屉标题「编辑项目/新建项目」随它切换） */
const formEdit = ref<ProjectRow | null>(null);
const removeProjectTarget = ref<ProjectRow | null>(null);
const delBusy = ref(false);
/** 正在删除的行（§3.3 按钮请求中文案「删除中…」只落在被删行） */
const removingId = ref(0);

const removeProjectLabel = computed(() => (removeProjectTarget.value
  ? `删除项目「${removeProjectTarget.value.name}」？删除后该项目将从顶栏选择器消失，其下节点与应用失去项目归属。此操作不可恢复。`
  : ''));

function openCreate(): void {
  formModel.value = emptyProjectForm();
  formNameError.value = '';
  formEdit.value = null;
  formOpen.value = true;
}

function openEdit(row: ProjectRow): void {
  formModel.value = projectToForm(row);
  formNameError.value = '';
  formEdit.value = row;
  formOpen.value = true;
}

async function submitProject(): Promise<void> {
  if (formBusy.value) return;
  const invalid = validateProjectName(formModel.value.name);
  formNameError.value = invalid;
  if (invalid) return;
  formBusy.value = true;
  const started = Date.now();
  try {
    const edit = formEdit.value;
    const r = edit
      ? await updateProject(req, edit.id, formModel.value)
      : await createProject(req, formModel.value);
    const rest = feedbackRemainMs(Date.now() - started);
    if (rest > 0) await sleep(rest);
    if (r.ok) {
      toast(r.message, 'ok');
      formOpen.value = false;
      // 联动 C14：改名的是当前顶栏项目 → 上下文显示名同步（触发页面按新名称刷新）
      if (edit && props.projectId != null && edit.id === props.projectId && formModel.value.name !== edit.name) {
        setStardustProject(edit.id, formModel.value.name);
      }
      emit('projectChanged');
    } else {
      toast(r.message, 'err');
    }
  } finally {
    formBusy.value = false;
  }
}

function askRemoveProject(row: ProjectRow): void {
  if (delBusy.value) return;
  removeProjectTarget.value = row;
}

async function confirmRemoveProject(): Promise<void> {
  const row = removeProjectTarget.value;
  removeProjectTarget.value = null;
  if (!row || delBusy.value) return;
  delBusy.value = true;
  removingId.value = row.id;
  const started = Date.now();
  try {
    const r = await deleteProject(req, row.id, row.name);
    const rest = feedbackRemainMs(Date.now() - started);
    if (rest > 0) await sleep(rest);
    if (r.ok) {
      toast(r.message, 'ok');
      // 联动 C13：删除的是当前顶栏所选项目 → 上下文自动切回「全部项目」并提示
      const ctx = contextAfterDelete(row.id, props.projectId, props.projectInfo?.name ?? '');
      if (ctx.switched) {
        setStardustProject(null, '全部项目');
        toast(ctx.toast, 'ok');
      }
      emit('projectChanged');
    } else {
      toast(r.message, 'err');
    }
  } finally {
    delBusy.value = false;
    removingId.value = 0;
  }
}

const drawerOpen = ref(false);
const settingsOpen = ref(false); // CARD-PLT-07：完整平台设置表单抽屉
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

// ---- 成员卡查询条（CARD-PLT-06：服务端查询 enable/dt/Q，projectId 顶栏透传；userId 记差异不做控件） ----
const memberQueryEnable = ref<'' | 'on' | 'off'>('');
const memberQueryDtStart = ref('');
const memberQueryDtEnd = ref('');
const memberQueryQ = ref('');
const memberQueryLoading = ref(false);
const memberQueryError = ref('');
/** 服务端查询行集快照；null=未在查询态，回落 props.members（MeshPage 按顶栏项目重拉的全量） */
const memberQueryRows = ref<MemberRow[] | null>(null);

const displayMembers = computed(() => memberQueryRows.value ?? props.members);
const membersEmptyText = computed(() => (memberQueryRows.value !== null
  ? '当前查询条件下没有成员。可调整条件重试，或点「返回全部」查看全部成员。'
  : '项目还没有成员'));

/** 查询行集 MemberLink 无 CreateTime 归一，加入时间列诚实显示 —（卡面未要求时间列，轻量落地） */
function queryRowToMember(m: MemberLink): MemberRow {
  return {
    id: m.id,
    userId: m.userId,
    name: m.userName || (m.userId > 0 ? `#${m.userId}` : '—'),
    role: '成员',
    enable: m.enable,
    joinedText: '—',
  };
}

async function applyMemberQuery(): Promise<void> {
  if (memberQueryLoading.value) return;
  memberQueryLoading.value = true;
  memberQueryError.value = '';
  try {
    const q: MemberQuery = {
      enable: memberQueryEnable.value,
      dtStart: memberQueryDtStart.value,
      dtEnd: memberQueryDtEnd.value,
      Q: memberQueryQ.value,
    };
    const r = await fetchMemberLinks(req, props.projectId, q);
    memberQueryRows.value = r.rows.map(queryRowToMember);
    memberQueryError.value = r.error;
  } finally {
    memberQueryLoading.value = false;
  }
}

function resetMemberQuery(): void {
  memberQueryEnable.value = '';
  memberQueryDtStart.value = '';
  memberQueryDtEnd.value = '';
  memberQueryQ.value = '';
  memberQueryError.value = '';
  memberQueryRows.value = null;
}

// 行内启停/移除成功后父层 emit('retry') 重拉 props.members；若正处于查询态，静默重查保持快照一致（照 McpTab audit watch 先例）
watch(
  () => props.members,
  () => {
    if (memberQueryRows.value !== null && !memberQueryLoading.value) void applyMemberQuery();
  },
);

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
    >
      <div class="mb-[10px] flex items-center justify-between px-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">运行时、数据保留与级联设置</span>
        <Btn
          size="sm"
          variant="primary"
          data-spec="platform-edit"
          @click="emit('edit')"
        >
          <template #icon>
            <KIcon
              name="Sliders"
              :size="12"
            />
          </template>
          编辑平台设置
        </Btn>
      </div>

      <div class="grid grid-cols-2 gap-[12px] md:grid-cols-4">
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
    </div>

    <!-- CARD-PLT-07：完整平台设置入口（磁贴只放常用项，全部字段收在此表单） -->
    <div
      v-if="!loading && !error"
      class="mt-[12px] px-[4px]"
    >
      <Btn
        variant="ghost"
        data-spec="platform-settings-open"
        @click="settingsOpen = true"
      >
        完整平台设置
      </Btn>
    </div>

    <Card
      v-if="!loading && !error"
      padding="none"
      data-spec="platform-projects"
      class="mt-[12px]"
    >
      <div class="flex flex-wrap items-center gap-2 px-[14px] pt-[12px]">
        <h3 :class="H3">
          项目管理
          <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">{{ projects.length }} 个项目</span>
        </h3>
        <span class="ml-auto">
          <Btn
            size="sm"
            variant="primary"
            data-spec="platform-project-add"
            :disabled="formBusy"
            @click="openCreate"
          >
            <template #icon>
              <KIcon
                name="Plus"
                :size="12"
              />
            </template>
            新建项目
          </Btn>
        </span>
      </div>

      <div
        data-spec="platform-project-filters"
        class="flex flex-wrap items-center gap-[8px] px-[14px] pt-[10px]"
      >
        <input
          v-model="projectFilter.keyword"
          type="text"
          data-spec="platform-project-filter-key"
          placeholder="按名称 / 租户 / 管理者筛选"
          class="h-[30px] w-[180px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
        >
        <input
          v-model="projectFilter.dtStart"
          type="date"
          data-spec="platform-project-filter-start"
          class="h-[30px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[8px] text-[12px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
        >
        <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">~</span>
        <input
          v-model="projectFilter.dtEnd"
          type="date"
          data-spec="platform-project-filter-end"
          class="h-[30px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[8px] text-[12px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
        >
        <button
          type="button"
          data-spec="platform-project-filter-reset"
          :disabled="!filterActive"
          class="h-[28px] cursor-pointer rounded-[6px] border border-[var(--el-border-color-light)] px-[10px] text-[11.5px] text-[var(--el-text-color-regular)] hover:bg-[var(--el-bg-color-overlay)] disabled:cursor-not-allowed disabled:opacity-60"
          @click="projectFilter = { keyword: '', dtStart: '', dtEnd: '' }"
        >
          重置
        </button>
      </div>

      <table class="w-full border-collapse">
        <thead>
          <tr>
            <th :class="TH">
              名称
            </th>
            <th :class="TH">
              启用
            </th>
            <th :class="TH">
              租户
            </th>
            <th :class="TH">
              管理者
            </th>
            <th :class="TH">
              节点数
            </th>
            <th :class="TH">
              应用数
            </th>
            <th :class="TH">
              排序
            </th>
            <th :class="TH">
              全局
            </th>
            <th :class="TH">
              IP白名单
            </th>
            <th :class="TH">
              IP黑名单
            </th>
            <th :class="TH">
              新服务器
            </th>
            <th :class="TH">
              操作
            </th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="p in filteredProjects"
            :key="p.id"
            data-spec="platform-project-row"
            class="hover:bg-[var(--el-bg-color-overlay)]"
          >
            <td :class="TD">
              <button
                type="button"
                data-spec="platform-project-edit"
                class="cursor-pointer border-0 bg-transparent p-0 text-[12.5px] font-medium text-[var(--el-color-primary)] hover:underline"
                @click="openEdit(p)"
              >
                {{ p.name }}
              </button>
            </td>
            <td :class="TD">
              <span
                class="text-[12.5px]"
                :class="p.enable ? 'text-[var(--el-color-success)]' : 'text-[var(--el-text-color-secondary)]'"
              >{{ p.enable ? '是' : '否' }}</span>
            </td>
            <td :class="TD">
              <span class="text-[12.5px] text-[var(--el-text-color-regular)]">{{ p.tenantName || '—' }}</span>
            </td>
            <td :class="TD">
              <span class="text-[12.5px] text-[var(--el-text-color-regular)]">{{ p.managerName || '—' }}</span>
            </td>
            <td :class="TD">
              <span
                class="text-[12.5px] text-[var(--el-text-color-regular)]"
                :title="`${p.name}（节点）`"
              >{{ p.nodes }}</span>
            </td>
            <td :class="TD">
              <span
                class="text-[12.5px] text-[var(--el-text-color-regular)]"
                :title="`${p.name}（应用）`"
              >{{ p.apps }}</span>
            </td>
            <td :class="TD">
              <span class="text-[12.5px] text-[var(--el-text-color-regular)]">{{ p.sort }}</span>
            </td>
            <td :class="TD">
              <span
                class="text-[12.5px]"
                :class="p.isGlobal ? 'text-[var(--el-color-success)]' : 'text-[var(--el-text-color-secondary)]'"
              >{{ p.isGlobal ? '是' : '否' }}</span>
            </td>
            <td :class="TD">
              <span
                class="block max-w-[140px] truncate font-mono text-[11.5px] text-[var(--el-text-color-secondary)]"
                :title="p.whiteIPs"
              >{{ p.whiteIPs || '—' }}</span>
            </td>
            <td :class="TD">
              <span
                class="block max-w-[140px] truncate font-mono text-[11.5px] text-[var(--el-text-color-secondary)]"
                :title="p.blackIPs"
              >{{ p.blackIPs || '—' }}</span>
            </td>
            <td :class="TD">
              <span
                class="block max-w-[140px] truncate font-mono text-[11.5px] text-[var(--el-text-color-secondary)]"
                :title="p.newServer"
              >{{ p.newServer || '—' }}</span>
            </td>
            <td :class="TD">
              <span class="inline-flex gap-[6px]">
                <button
                  type="button"
                  data-spec="platform-project-edit-row"
                  class="cursor-pointer rounded-[6px] border border-[var(--el-border-color-light)] px-[8px] py-[3px] text-[11.5px] text-[var(--el-text-color-regular)] hover:bg-[var(--el-bg-color-overlay)] disabled:cursor-not-allowed disabled:opacity-60"
                  :disabled="delBusy"
                  @click="openEdit(p)"
                >
                  编辑
                </button>
                <button
                  type="button"
                  data-spec="platform-project-remove"
                  class="cursor-pointer rounded-[6px] border border-[var(--el-border-color-light)] px-[8px] py-[3px] text-[11.5px] text-[var(--el-color-danger)] hover:bg-[var(--el-bg-color-overlay)] disabled:cursor-not-allowed disabled:opacity-60"
                  :disabled="delBusy"
                  @click="askRemoveProject(p)"
                >
                  <span
                    v-if="delBusy && removingId === p.id"
                    class="inline-flex items-center gap-[5px]"
                  >
                    <span class="h-[10px] w-[10px] animate-spin rounded-full border-[1.5px] border-[var(--el-border-color)] border-t-[var(--el-color-danger)]" />
                    删除中…
                  </span>
                  <template v-else>
                    删除
                  </template>
                </button>
              </span>
            </td>
          </tr>
          <tr v-if="projectEmpty">
            <td
              colspan="12"
              data-spec="platform-project-empty"
              class="px-[14px] py-[22px] text-center"
            >
              <div class="text-[12.5px] text-[var(--el-text-color-secondary)]">
                还没有项目
              </div>
              <Btn
                size="sm"
                variant="primary"
                data-spec="platform-project-empty-create"
                class="mt-[10px]"
                @click="openCreate"
              >
                <template #icon>
                  <KIcon
                    name="Plus"
                    :size="12"
                  />
                </template>
                创建项目
              </Btn>
            </td>
          </tr>
          <tr v-else-if="projectEmptyFiltered">
            <td
              colspan="12"
              data-spec="platform-project-empty-filtered"
              class="px-[14px] py-[18px] text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
            >
              {{ emptyFilteredText }}
            </td>
          </tr>
        </tbody>
      </table>
    </Card>

    <Card
      v-if="!loading && !error"
      padding="none"
      data-spec="platform-members"
      class="mt-[12px]"
    >
      <div class="flex flex-wrap items-center gap-2 px-[14px] pt-[12px]">
        <h3 :class="H3">
          项目成员
          <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">{{ displayMembers.length }} 位</span>
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

      <!-- 成员卡查询条（CARD-PLT-06）：enable/dt/Q 服务端查询，projectId 随顶栏上下文在 apply 时透传 -->
      <div
        data-spec="platform-member-query-bar"
        class="flex flex-wrap items-center gap-[8px] px-[14px] pt-[10px]"
      >
        <select
          v-model="memberQueryEnable"
          data-spec="platform-member-query-enable"
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
        <span
          data-spec="platform-member-query-dt"
          class="inline-flex items-center gap-[4px]"
        >
          <input
            v-model="memberQueryDtStart"
            type="date"
            class="h-[28px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[8px] text-[12px] text-[var(--el-text-color-primary)] outline-none"
          >
          <span class="text-[11px] text-[var(--el-text-color-secondary)]">至</span>
          <input
            v-model="memberQueryDtEnd"
            type="date"
            class="h-[28px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[8px] text-[12px] text-[var(--el-text-color-primary)] outline-none"
          >
        </span>
        <div class="flex h-[28px] items-center gap-[6px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px]">
          <KIcon
            name="Search"
            :size="13"
            class="text-[var(--el-text-color)]"
          />
          <input
            v-model="memberQueryQ"
            type="text"
            placeholder="按用户名检索"
            data-spec="platform-member-query-q"
            class="w-[130px] bg-transparent text-[12px] text-[var(--el-text-color-primary)] outline-none placeholder:text-[var(--el-text-color-placeholder)]"
          >
        </div>
        <Btn
          size="sm"
          data-spec="platform-member-query-apply"
          :disabled="memberQueryLoading"
          @click="applyMemberQuery"
        >
          {{ memberQueryLoading ? '查询中…' : '查询' }}
        </Btn>
        <Btn
          v-if="memberQueryRows !== null"
          size="sm"
          variant="ghost"
          data-spec="platform-member-query-reset"
          @click="resetMemberQuery"
        >
          返回全部
        </Btn>
      </div>
      <div
        v-if="memberQueryError"
        data-spec="platform-member-query-error"
        class="flex items-center gap-[10px] px-[14px] pt-[10px] text-[12.5px] text-[var(--el-color-danger)]"
      >
        成员查询失败：{{ memberQueryError }}
      </div>

      <div
        v-if="projectInfo"
        data-spec="platform-project"
        class="flex flex-wrap gap-x-[18px] gap-y-[4px] px-[14px] pt-[10px] text-[11.5px] text-[var(--el-text-color-secondary)]"
      >
        <span>
          项目 <b class="font-medium text-[var(--el-text-color-primary)]">{{ projectInfo.name }}</b>
        </span>
        <span
          v-if="hasEnvironment"
          data-spec="platform-project-environment"
        >环境 {{ projectInfo.environment }}</span>
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
            v-for="m in displayMembers"
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
          <tr v-if="!displayMembers.length">
            <td
              colspan="4"
              data-spec="platform-members-empty"
              class="px-[14px] py-[18px] text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
            >
              {{ membersEmptyText }}
            </td>
          </tr>
        </tbody>
      </table>

      <div
        v-if="displayMembers.length && namePending"
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

    <!-- CARD-PLT-07：完整平台设置表单（打开时挂载，关闭即卸载，重开重新拉取） -->
    <StarServerSettingsForm
      v-if="settingsOpen"
      :req="req"
      :open="settingsOpen"
      @close="settingsOpen = false"
      @saved="emit('retry')"
    />

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

    <!-- CARD-PLT-01：项目 新建/编辑表单抽屉 + 删除确认（GuardModal） -->
    <GalaxyProjectFormDrawer
      :open="formOpen"
      :edit-name="formEdit?.name ?? null"
      :model="formModel"
      :name-error="formNameError"
      :busy="formBusy"
      @close="formOpen = false"
      @update:model="formModel = $event"
      @submit="submitProject"
    />
    <GuardModal
      v-if="removeProjectTarget"
      :label="removeProjectLabel"
      @cancel="removeProjectTarget = null"
      @confirm="confirmRemoveProject"
    />
  </div>
</template>
