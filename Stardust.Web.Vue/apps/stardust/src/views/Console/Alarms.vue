<script setup lang="ts">
/**
 * 告警中心薄壳（/alarms）。取数由 alarms.logic.fetchAlarms 编排：
 * 并行拉当前告警（AlarmRecord?status=1）+ 历史告警（AlarmHistory），归一合并。
 * 取数随 stardustContext（项目/环境）失效重取（useContextReload）；
 * AlarmRecord 实体无 ProjectId 字段，projectId 透传但对该接口无过滤效果（已读源码确认）。
 * 视图切换（CARD-MON-01 定稿裁定 2）：页内 Btn 组「当前与历史」（默认）｜「告警组」，切换状态由本页持有。
 * 组/时间服务端过滤（CARD-MON-02 定稿）：受控值在本页，选中即 400ms 防抖重取（refreshSoon 同款
 * 串行链语义，重复变更由最后一次收敛上屏）；不选=全量（裁定 1），关键字维持客户端过滤（裁定 2）。
 * 刷新按钮（§11，走查缺陷①）：refreshing 与列表骨架 loading 分离，请求再快也至少旋转 ~300ms。
 * 深链 /alarms?groupId={id} → 组过滤预置（parseAlarmsQuery）；组视图行「告警历史」上抛与
 * 列表记录行「告警历史」链接同归：设置组过滤 + 切回列表视图 + 滚动到列表。
 * 记录行编辑/删除（CARD-MON-03 定稿 1）：行操作「编辑」开 AlarmRecordDrawer（alarmRecordFormFromRow
 * 回填，保存 PUT /Monitors/AlarmRecord/{id}）；「删除」本地 GuardModal 确认后 DELETE。
 * 状态下拉「已恢复」（定稿 3）：按 recordStatus=2 服务端重取（GET /Monitors/AlarmRecord?status=2，
 * 不并 AlarmHistory——通知记录与生命周期记录两实体并显会重复）。
 */
import { onScopeDispose, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import { request } from '@newlifex/cube-vue/core/utils/request';
import Btn from '@/layouts/StardustLayout2/Btn.vue';
import KIcon from '@/layouts/StardustLayout2/KIcon.vue';
import PageHead from '@/layouts/StardustLayout2/PageHead.vue';
import AlarmsList from '@/layouts/StardustLayout2/AlarmsList.vue';
import AlarmsGroups from '@/layouts/StardustLayout2/AlarmsGroups.vue';
import AlarmsRules from './AlarmsRules.vue';
import AlarmRecordDrawer from '@/layouts/StardustLayout2/AlarmRecordDrawer.vue';
import GuardModal from '@/layouts/StardustLayout2/GuardModal/GuardModal.vue';
import { toast } from '@/layouts/StardustLayout2/useToast';
import {
  fetchAlarmGroupOptions,
  fetchAlarms,
  parseAlarmsQuery,
  type AlarmFetchFilter,
  type AlarmGroupOption,
  type AlarmRow,
} from '@/layouts/StardustLayout2/alarms.logic';
import {
  alarmRecordFormFromRow,
  removeAlarmRecord,
  saveAlarmRecord,
  type AlarmRecordForm,
} from '@/layouts/StardustLayout2/alarmRecord.logic';
import { stardustContext } from '@/stores/stardustContext';
import { useContextReload } from '@/layouts/StardustLayout2/useContextReload';

/** 页内视图：live=当前与历史（默认），groups=告警组（CARD-MON-01 定稿裁定 2），rules=跟踪规则（CARD-MON-13） */
const view = ref<'live' | 'groups' | 'rules'>('live');

const rows = ref<AlarmRow[]>([]);
const loading = ref(false);
const errorText = ref('');
/** 刷新按钮反馈（§11，走查缺陷①）：与列表骨架 loading 分离，请求再快也至少旋转 ~300ms（NowPage 同款） */
const refreshing = ref(false);

// ---------- 组/时间服务端过滤（CARD-MON-02） ----------

const route = useRoute();
/** 深链 /alarms?groupId={id} 预置；null=全部组（与后端 groupId<0 同语义；组 Id 字符串透传，走查缺陷②） */
const groupId = ref<string | null>(parseAlarmsQuery(route.query as Record<string, unknown>));
const dtStart = ref('');
const dtEnd = ref('');
/** 状态过滤（CARD-MON-03 受控；''=全部状态；'recovered' → recordStatus=2 服务端重取） */
const statusFilter = ref('');
const groupOptions = ref<AlarmGroupOption[]>([]);
const groupError = ref('');

/** 当前服务端过滤（全空=undefined，请求不带过滤参数） */
function currentFilter(): AlarmFetchFilter | undefined {
  if (groupId.value === null && !dtStart.value && !dtEnd.value) return undefined;
  return {
    ...(groupId.value !== null ? { groupId: groupId.value } : {}),
    ...(dtStart.value ? { dtStart: dtStart.value } : {}),
    ...(dtEnd.value ? { dtEnd: dtEnd.value } : {}),
  };
}

async function load(isStale: () => boolean): Promise<void> {
  loading.value = true;
  refreshing.value = true;
  const started = Date.now();
  errorText.value = '';
  try {
    // 「已恢复」视图（CARD-MON-03 定稿 3）：recordStatus=2 只拉记录路（status=2），不并 AlarmHistory
    const r = await fetchAlarms(
      request,
      stardustContext.projectId,
      currentFilter(),
      statusFilter.value === 'recovered' ? { recordStatus: 2 } : undefined,
    );
    if (isStale()) return;
    rows.value = r.rows;
    errorText.value = r.error;
  } catch (e) {
    if (isStale()) return;
    errorText.value = e instanceof Error ? e.message : '告警加载失败';
  } finally {
    if (!isStale()) loading.value = false;
  }
  // §11 300ms 保底（走查缺陷①）：请求再快也让「刷新中…」至少可见 ~300ms（NowPage 同款内联；
  // stale 早退不误伤——try 内 return 已跳过本段；组下拉随后再取，选项晚到不阻塞列表）
  const rest = 300 - (Date.now() - started);
  if (rest > 0) await new Promise((r) => setTimeout(r, rest));
  if (isStale()) return;
  refreshing.value = false;
  // 告警组下拉数据源（非致命：失败在过滤条给可见提示，刷新可重试；不阻塞告警列表）
  const g = await fetchAlarmGroupOptions(request, stardustContext.projectId);
  if (isStale()) return;
  groupOptions.value = g.options;
  groupError.value = g.error;
}

// 上下文（项目、环境）就绪后首轮取数，之后切换项目或环境自动重取
const { reload } = useContextReload(load, stardustContext);

// 组/时间/状态选中即服务端重取（CARD-MON-02 定稿裁定 4 + CARD-MON-03 定稿 3）：400ms 防抖串行链
// 与上下文重取共用 reload 守卫（后启动的作废在飞的旧响应，最后完成的收敛上屏，参照 useDeployData.refreshSoon 语义）
let refetchTimer: ReturnType<typeof setTimeout> | undefined;
watch([groupId, dtStart, dtEnd, statusFilter], () => {
  if (refetchTimer) clearTimeout(refetchTimer);
  refetchTimer = setTimeout(() => {
    refetchTimer = undefined;
    reload();
  }, 400);
});
onScopeDispose(() => {
  if (refetchTimer) clearTimeout(refetchTimer);
});

const listAnchor = ref<HTMLElement | null>(null);
function scrollToList(): void {
  listAnchor.value?.scrollIntoView({ behavior: 'smooth', block: 'start' });
}

/** 组视图行「告警历史」上抛（CARD-MON-01→02）：切回列表视图 + 设置组过滤并滚动 */
function onGroupHistory(row: { id: number }): void {
  view.value = 'live';
  // 组实体 Id 是自增小整数（已核实体源码），边界转 string 与组过滤链同形（走查缺陷②，C18）
  const gid = String(row.id);
  if (groupId.value !== gid) groupId.value = gid;
  scrollToList();
}

/** 列表记录行「告警历史」链接（CARD-MON-02）：设置组过滤并滚动（组 Id 字符串透传，走查缺陷②） */
function onRowHistory(id: string): void {
  if (groupId.value !== id) groupId.value = id;
  scrollToList();
}

// ---------- 记录行编辑抽屉（CARD-MON-03 定稿 1：只编辑，无新增入口） ----------

const drawerOpen = ref(false);
const drawerInitial = ref<AlarmRecordForm | null>(null);
const saveBusy = ref(false);

function drawerTitle(): string {
  const name = drawerInitial.value?.name ?? '';
  return name ? `编辑告警记录 · ${name}` : '编辑告警记录';
}

function openEdit(row: AlarmRow): void {
  drawerInitial.value = alarmRecordFormFromRow(row);
  drawerOpen.value = true;
}

function closeDrawer(): void {
  if (saveBusy.value) return;
  drawerOpen.value = false;
}

async function onDrawerSubmit(form: AlarmRecordForm): Promise<void> {
  if (saveBusy.value) return;
  saveBusy.value = true;
  try {
    await saveAlarmRecord(request, form);
    saveBusy.value = false;
    drawerOpen.value = false;
    toast('告警记录已保存', 'ok');
    await reload();
  } catch (e) {
    saveBusy.value = false;
    toast(`告警记录保存失败：${e instanceof Error && e.message ? e.message : String(e)}`, 'err');
  }
}

// ---------- 记录行删除（本地 Guard，McpTab/PlatformTab 同款模式；文案=旅程 C12 逐字） ----------

const removeTarget = ref<AlarmRow | null>(null);
const removeBusy = ref(false);

function askRemove(row: AlarmRow): void {
  removeTarget.value = row;
}

async function confirmRemove(): Promise<void> {
  const row = removeTarget.value;
  if (!row || removeBusy.value) return;
  removeBusy.value = true;
  try {
    await removeAlarmRecord(request, row.id);
    removeBusy.value = false;
    removeTarget.value = null;
    toast('告警记录已删除', 'ok');
    await reload();
  } catch (e) {
    removeBusy.value = false;
    removeTarget.value = null;
    toast(`告警记录删除失败：${e instanceof Error && e.message ? e.message : String(e)}`, 'err');
  }
}
</script>

<template>
  <div data-spec="page-alarms">
    <PageHead
      eyebrow="监控 · 告警中心"
      title="告警中心"
      sub="当前告警与历史告警合并视图：按级别、状态、应用过滤，点行展开详情；切换「告警组」可维护告警投递配置。"
    >
      <template #actions>
        <Btn
          size="sm"
          :disabled="refreshing"
          data-spec="alarms-refresh"
          @click="reload"
        >
          <template #icon>
            <KIcon
              name="Refresh"
              :size="13"
              :class="refreshing ? 'animate-spin' : ''"
            />
          </template>
          {{ refreshing ? '刷新中…' : '刷新' }}
        </Btn>
      </template>
    </PageHead>

    <!-- 视图切换（CARD-MON-01 定稿裁定 2；rules=跟踪规则 CARD-MON-13）：「当前与历史」默认｜「告警组」｜「跟踪规则」 -->
    <div
      data-spec="alarms-view-toggle"
      class="mb-[12px] flex items-center gap-[6px]"
      role="tablist"
      aria-label="告警中心视图切换"
    >
      <Btn
        size="sm"
        :variant="view === 'live' ? 'primary' : 'default'"
        data-spec="alarms-view-live"
        role="tab"
        :aria-selected="view === 'live'"
        @click="view = 'live'"
      >
        当前与历史
      </Btn>
      <Btn
        size="sm"
        :variant="view === 'groups' ? 'primary' : 'default'"
        data-spec="alarms-view-groups"
        role="tab"
        :aria-selected="view === 'groups'"
        @click="view = 'groups'"
      >
        告警组
      </Btn>
      <Btn
        size="sm"
        :variant="view === 'rules' ? 'primary' : 'default'"
        data-spec="alarms-view-rules"
        role="tab"
        :aria-selected="view === 'rules'"
        @click="view = 'rules'"
      >
        跟踪规则
      </Btn>
    </div>

    <div ref="listAnchor">
      <AlarmsList
        v-show="view === 'live'"
        v-model:group-id="groupId"
        v-model:dt-start="dtStart"
        v-model:dt-end="dtEnd"
        v-model:status-filter="statusFilter"
        :rows="rows"
        :loading="loading"
        :error="errorText"
        :groups="groupOptions"
        :group-error="groupError"
        @retry="reload"
        @history="onRowHistory"
        @edit="openEdit"
        @remove="askRemove"
      />
    </div>

    <AlarmsGroups
      v-if="view === 'groups'"
      @history="onGroupHistory"
    />

    <!-- 跟踪规则视图（CARD-MON-13）：懒挂载（切到才拉数），形态对齐 AlarmsGroups 区块卡 -->
    <AlarmsRules v-if="view === 'rules'" />

    <!-- 记录行编辑抽屉（CARD-MON-03：编辑=PUT /Monitors/AlarmRecord/{id}，本卡无新增入口） -->
    <AlarmRecordDrawer
      :open="drawerOpen"
      :initial="drawerInitial"
      :title="drawerTitle()"
      sub="/alarms"
      :groups="groupOptions"
      :busy="saveBusy"
      @close="closeDrawer"
      @submit="onDrawerSubmit"
    />

    <!-- 删除（GuardModal：标题含记录名、正文写后果；文案=旅程 C12 逐字） -->
    <GuardModal
      v-if="removeTarget"
      :label="`删除告警记录「${removeTarget.title}」？`"
      detail="删除后该条告警生命周期记录不可恢复"
      @cancel="removeTarget = null"
      @confirm="confirmRemove"
    />
  </div>
</template>
