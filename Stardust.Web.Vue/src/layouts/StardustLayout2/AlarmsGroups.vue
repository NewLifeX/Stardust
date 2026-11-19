<script setup lang="ts">
/**
 * AlarmsGroups：告警中心「告警组」视图区块（CARD-MON-01，来源旅程 J-MON-01）。
 * 区块卡形态：工具条（搜索框+启用下拉+新增）+ 表格 + 行操作（测试/告警历史/编辑/删除）。
 * 自取数：fetchAlarmGroups（真实接口，无演示数据）；项目下拉 fetchProjectOptions 由本组件注入给抽屉。
 * 纯 UI 原则的落地：取数/校验/写操作编排在本组件（容器），分支逻辑在 alarmGroup.logic（可单测）；
 * 「告警历史」不导航，上抛 history 事件由 Alarms.vue 处理（切历史态+组过滤，联动 CARD-MON-02）。
 * 删除走本地 GuardModal（McpTab/PlatformTab 同款模式）：标题含组名、正文写后果、按钮「取消/确认执行」。
 * §11：所有带请求的按钮 loading + 禁用；结果 toast；失败错误条 + 重试（不静默空态）。
 * data-spec 前缀 alarm-group-（已查重，与 alarms- 与 alarm- 两族既有前缀无冲突）。
 */
import { computed, onMounted, ref } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import Btn from './Btn.vue';
import Card from './Card.vue';
import KIcon from './KIcon.vue';
import Tag from './Tag.vue';
import GuardModal from './GuardModal/GuardModal.vue';
import { toast } from './useToast.ts';
import { SELECT, TD, TH } from './deployView.styles.ts';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import {
  deleteAlarmGroup,
  executeAlarmTest,
  fetchAlarmGroups,
  fetchProjectOptions,
  filterGroupRows,
  GROUP_EMPTY_CREATE_TEXT,
  GROUP_EMPTY_TEXT,
  GROUP_FILTER_EMPTY_TEXT,
  saveAlarmGroup,
  toAlarmGroupForm,
  validateAlarmGroupForm,
  DISABLED_GROUP_TEST_TITLE,
  type AlarmGroupForm,
  type AlarmGroupRow,
  type ProjectOption,
} from './alarmGroup.logic.ts';
import AlarmGroupDrawer from './AlarmGroupDrawer.vue';

const props = defineProps<{
  deps?: { request?: Req };
}>();

const emit = defineEmits<{ history: [row: AlarmGroupRow] }>();

function req(): Req {
  return props.deps?.request ?? (request as Req);
}

// ---------- 取数 ----------

const rows = ref<AlarmGroupRow[]>([]);
const loading = ref(true);
const errorText = ref('');

async function load(): Promise<void> {
  loading.value = true;
  errorText.value = '';
  const r = await fetchAlarmGroups(req());
  rows.value = r.rows;
  errorText.value = r.error;
  loading.value = false;
}

// ---------- 过滤（客户端） ----------

const query = ref('');
const enableFilter = ref('');

const visible = computed<AlarmGroupRow[]>(() => filterGroupRows(rows.value, query.value, enableFilter.value));

// ---------- 行内展开（内容全文 + 创建/更新 IP） ----------

const openId = ref<number | null>(null);
function toggle(id: number): void {
  openId.value = openId.value === id ? null : id;
}

// ---------- 测试发送 ----------

const testing = ref<number | null>(null);
async function runTest(row: AlarmGroupRow): Promise<void> {
  if (testing.value !== null) return;
  testing.value = row.id;
  const r = await executeAlarmTest(req(), row.id);
  testing.value = null;
  if (r.ok) toast(`测试发送成功：${r.message}`, 'ok');
  else toast(r.message, 'err');
}

// ---------- 新 / 编辑屉 ----------

const drawerOpen = ref(false);
const drawerId = ref<number | null>(null);
const drawerInitial = ref<AlarmGroupForm | null>(null);
const saveBusy = ref(false);

const projectOptions = ref<ProjectOption[]>([]);
const projectsLoading = ref(false);

async function loadProjects(): Promise<void> {
  projectsLoading.value = true;
  const r = await fetchProjectOptions(req());
  if (r.options.length > 0 || !r.error) projectOptions.value = r.options;
  projectsLoading.value = false;
}

function openCreate(): void {
  drawerId.value = null;
  drawerInitial.value = null;
  drawerOpen.value = true;
  if (!projectsLoading.value && projectOptions.value.length === 0) void loadProjects();
}

function openEdit(row: AlarmGroupRow): void {
  drawerId.value = row.id;
  drawerInitial.value = toAlarmGroupForm(row);
  drawerOpen.value = true;
  if (!projectsLoading.value && projectOptions.value.length === 0) void loadProjects();
}

function drawerTitle(): string {
  const init = drawerInitial.value;
  const name = init?.name ?? '';
  return drawerId.value && drawerId.value > 0 ? (name ? `编辑告警组 · ${name}` : '编辑告警组') : '新增告警组';
}

async function onSubmit(form: AlarmGroupForm): Promise<void> {
  const err = validateAlarmGroupForm(form);
  if (err) {
    toast(err, 'warn');
    return;
  }
  saveBusy.value = true;
  try {
    await saveAlarmGroup(req(), form, drawerId.value ?? undefined);
    saveBusy.value = false;
    drawerOpen.value = false;
    toast(drawerId.value && drawerId.value > 0 ? '告警组已保存' : '告警组已创建', 'ok');
    await load();
  } catch (e) {
    saveBusy.value = false;
    toast(`告警组保存失败：${e instanceof Error && e.message ? e.message : String(e)}`, 'err');
  }
}

// ---------- 删除（本地 Guard） ----------

const removeTarget = ref<AlarmGroupRow | null>(null);
const removeBusy = ref(false);

function askRemove(row: AlarmGroupRow): void {
  removeTarget.value = row;
}

async function confirmRemove(): Promise<void> {
  const row = removeTarget.value;
  if (!row || removeBusy.value) return;
  removeBusy.value = true;
  try {
    await deleteAlarmGroup(req(), row.id);
    removeBusy.value = false;
    removeTarget.value = null;
    toast(`告警组「${row.name}」已删除`, 'ok');
    await load();
  } catch (e) {
    removeBusy.value = false;
    removeTarget.value = null;
    toast(`删除「${row.name}」失败：${e instanceof Error && e.message ? e.message : String(e)}`, 'err');
  }
}

onMounted(() => void load());
</script>

<template>
  <Card
    padding="none"
    data-spec="alarms-groups"
  >
    <!-- 工具条：搜索 + 启用下拉 + 新增 -->
    <div class="flex flex-wrap items-center gap-[8px] border-b border-[var(--el-border-color-light)] px-[14px] py-[10px]">
      <div class="flex h-[28px] items-center gap-[6px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px]">
        <KIcon
          name="Search"
          :size="12"
          class="text-[var(--el-text-color-secondary)]"
        />
        <input
          v-model="query"
          type="text"
          data-spec="alarm-group-search"
          placeholder="搜索名称 / 项目"
          class="w-[150px] bg-transparent text-[12px] text-[var(--el-text-color-primary)] outline-none placeholder:text-[var(--el-text-color-secondary)]"
        >
      </div>
      <select
        v-model="enableFilter"
        data-spec="alarm-group-filter-enable"
        :class="SELECT"
      >
        <option value="">
          全部状态
        </option>
        <option value="true">
          启用
        </option>
        <option value="false">
          停用
        </option>
      </select>
      <span class="flex-1" />
      <Btn
        size="sm"
        variant="primary"
        data-spec="alarm-group-create"
        @click="openCreate"
      >
        新增告警组
      </Btn>
    </div>

    <!-- 三态：loading / failed / 内容 -->
    <div
      v-if="loading"
      data-spec="alarm-group-loading"
      class="flex items-center justify-center gap-[8px] py-10 text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      <span class="inline-flex animate-spin">
        <span class="h-[14px] w-[14px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
      </span>
      告警组加载中…
    </div>

    <div
      v-else-if="errorText"
      data-spec="alarm-group-failed"
      class="flex flex-wrap items-center justify-center gap-[10px] py-8 text-[12.5px] text-[var(--el-color-danger)]"
    >
      告警组加载失败：{{ errorText }}
      <Btn
        size="sm"
        data-spec="alarm-group-retry"
        @click="load"
      >
        重试
      </Btn>
    </div>

    <!-- 空态两模式（实现规范 §3.3）：无数据=引导「还没有告警组」+「创建告警组」；有数据过滤后无=纯列表空态 -->
    <div
      v-else-if="visible.length === 0 && rows.length === 0"
      data-spec="alarm-group-empty"
      class="flex flex-col items-center gap-[10px] py-10 text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      <span>{{ GROUP_EMPTY_TEXT }}</span>
      <Btn
        size="sm"
        variant="primary"
        data-spec="alarm-group-empty-create"
        @click="openCreate"
      >
        {{ GROUP_EMPTY_CREATE_TEXT }}
      </Btn>
    </div>
    <div
      v-else-if="visible.length === 0"
      data-spec="alarm-group-filter-empty"
      class="py-10 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      {{ GROUP_FILTER_EMPTY_TEXT }}
    </div>

    <!-- 表格 -->
    <table
      v-else
      class="w-full border-collapse"
      data-spec="alarm-group-table"
    >
      <thead>
        <tr>
          <th :class="TH">
            名称
          </th>
          <th :class="TH">
            项目
          </th>
          <th :class="TH">
            启用
          </th>
          <th :class="TH">
            告警机器人
          </th>
          <th :class="TH">
            内容
          </th>
          <th :class="TH">
            更新者
          </th>
          <th :class="TH">
            更新时间
          </th>
          <th :class="TH">
            操作
          </th>
        </tr>
      </thead>
      <tbody>
        <template
          v-for="row in visible"
          :key="row.id"
        >
          <tr
            :data-spec="'alarm-group-row'"
            class="group cursor-pointer hover:bg-[var(--el-fill-color-light)]"
            @click="toggle(row.id)"
          >
            <td :class="TD">
              <span class="font-medium text-[var(--el-text-color-primary)]">{{ row.name }}</span>
            </td>
            <td :class="TD">
              {{ row.projectName || (row.projectId > 0 ? `#${row.projectId}` : '—') }}
            </td>
            <td :class="TD">
              <Tag :tone="row.enable ? 'ok' : 'default'">
                {{ row.enable ? '启用' : '停用' }}
              </Tag>
            </td>
            <td :class="TD">
              <span
                class="block max-w-[220px] truncate font-mono text-[11.5px] text-[var(--el-text-color-secondary)]"
                :title="row.webHook || ''"
              >{{ row.webHook || '—' }}</span>
            </td>
            <td :class="TD">
              <span
                class="block max-w-[200px] truncate text-[var(--el-text-color-regular)]"
                :title="row.content || ''"
              >{{ row.content || '—' }}</span>
            </td>
            <td :class="TD">
              {{ row.updateUser || '—' }}
            </td>
            <td :class="TD">
              <span class="font-mono text-[11.5px] tabular-nums text-[var(--el-text-color-secondary)]">{{ row.updateTime }}</span>
            </td>
            <td
              :class="TD"
              @click.stop
            >
              <div class="flex items-center gap-[4px]">
                <!-- 停用组不禁用「测试」（CARD-MON-01 定稿裁定 4：后端契约已兜底 OK 不发送），加 title 说明 -->
                <Btn
                  size="sm"
                  variant="ghost"
                  data-spec="alarm-group-test"
                  :disabled="testing !== null"
                  :title="!row.enable ? DISABLED_GROUP_TEST_TITLE : `向「${row.name}」的机器人发送一条测试告警`"
                  @click="runTest(row)"
                >
                  <template #icon>
                    <span
                      v-if="testing === row.id"
                      class="inline-flex animate-spin"
                    >
                      <span class="h-[11px] w-[11px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
                    </span>
                  </template>
                  测试
                </Btn>
                <Btn
                  size="sm"
                  variant="ghost"
                  data-spec="alarm-group-history"
                  :title="`查看「${row.name}」的告警历史`"
                  @click="emit('history', row)"
                >
                  告警历史
                </Btn>
                <Btn
                  size="sm"
                  variant="ghost"
                  data-spec="alarm-group-edit"
                  @click="openEdit(row)"
                >
                  编辑
                </Btn>
                <Btn
                  size="sm"
                  variant="ghost"
                  data-spec="alarm-group-remove"
                  @click="askRemove(row)"
                >
                  删除
                </Btn>
              </div>
            </td>
          </tr>
          <!-- 行内展开：内容全文 + 创建/更新 IP（列表不单独成列的字段在此观察） -->
          <tr
            v-if="openId === row.id"
            data-spec="alarm-group-detail"
          >
            <td
              :class="TD"
              colspan="8"
            >
              <pre
                v-if="row.content"
                data-spec="alarm-group-content"
                class="m-0 whitespace-pre-wrap rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color)] p-[10px] font-mono text-[12px] leading-relaxed text-[var(--el-text-color-regular)]"
              >{{ row.content }}</pre>
              <div class="mt-[8px] grid grid-cols-2 gap-x-[16px] gap-y-[4px] text-[11.5px] text-[var(--el-text-color-secondary)] min-[720px]:grid-cols-4">
                <span>创建者：{{ row.createUser || '—' }}</span>
                <span>创建时间：{{ row.createTime }}</span>
                <span>创建地址：{{ row.createIp || '—' }}</span>
                <span>更新地址：{{ row.updateIp || '—' }}</span>
              </div>
            </td>
          </tr>
        </template>
      </tbody>
    </table>

    <!-- 新增/编辑抽屉 -->
    <AlarmGroupDrawer
      :open="drawerOpen"
      :initial="drawerInitial"
      :title="drawerTitle()"
      sub="保存后该组即可在监控规则中选择投递"
      :project-options="projectOptions"
      :projects-loading="projectsLoading"
      :busy="saveBusy"
      @close="drawerOpen = false"
      @submit="onSubmit"
    />

    <!-- 删除（GuardModal 两段式：短标题 label + 后果 detail，记录删除同款；文案=旅程 C14 逐字） -->
    <GuardModal
      v-if="removeTarget"
      :label="`删除告警组「${removeTarget.name}」？`"
      detail="删除后引用该组的监控项将无法投递"
      @cancel="removeTarget = null"
      @confirm="confirmRemove"
    />
  </Card>
</template>
