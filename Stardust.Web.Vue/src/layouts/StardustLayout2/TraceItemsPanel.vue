<script setup lang="ts">
/**
 * TraceItemsPanel：工作台「监控中心」页签 ·「跟踪项」区块（CARD-MON-12，2026-11-09 定稿）。
 * 副题：应用埋点聚合口径。删除会级联清理每日统计。
 * 数据真实接口（TraceItemController.cs 现行 + 本卡新增 GetKinds）：
 *   GET    /Monitors/TraceItem?Id=&appId=&name=&kind=&enable=&dtStart=&dtEnd=&Q=（Search，dt 过滤更新时间）
 *   GET    /Monitors/TraceItem/GetKinds（类别词典，后端 Biz.cs:152 FieldCache）
 *   POST   /Monitors/TraceItem / PUT /Monitors/TraceItem/{id} / DELETE /Monitors/TraceItem/{id}
 *   GET    /Monitors/TraceItem/{Fix|FixDisplay|EnableSelect|DisableSelect}?keys=（批量四动作，逗号串）
 * 保存语义（Kind 补齐、Rules 无= 补 name= 前缀）由后端 Valid 承载（跟踪项.Biz.cs:45-47，裁定 2），
 * 前端直传不改写。列集隐藏 AlarmRobot 与创建类字段（后端 ListFields 同口径）。
 * 纯 UI：表单/确认/批量分支逻辑在 traceItem.logic.ts（可单测），组件只发事件与拉数。
 */
import { computed, ref, watch } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import Btn from './Btn.vue';
import Card from './Card.vue';
import TraceItemDrawer from './TraceItemDrawer.vue';
import { H3, TD, TH } from './deployView.styles.ts';
import { useManualRefresh } from './useManualRefresh.ts';
import { openGuard } from './ui.store.ts';
import { toast } from './useToast.ts';
import {
  deleteTraceItem,
  fetchTraceItemKinds,
  fetchTraceItems,
  runTraceItemAction,
  saveTraceItem,
  toTraceItemEditModel,
  validateTraceItemEdit,
  type TraceItemEditModel,
  type TraceItemKind,
  type TraceItemRow,
} from './traceItem.logic.ts';

const props = defineProps<{
  appId: number;
  appName?: string;
  /** 宿主「埋点数」可点下行的预设过滤（CARD-MON-12 裁定 3 裁量 2）：变化时覆盖应用过滤并重载 */
  externalAppId?: number | null;
  deps?: { request?: Req };
}>();

function req(): Req {
  return props.deps?.request ?? (request as Req);
}

const subtitle = '应用埋点聚合口径。删除会级联清理每日统计';

// ---------- 工具条条件 ----------
const appIdKw = ref<number | undefined>(undefined);
const kindKw = ref('');
const enableKw = ref<boolean | null>(null);
const nameKw = ref('');
const qKw = ref('');
const dtStartKw = ref('');
const dtEndKw = ref('');

// ---------- 类别词典（GetKinds，加载时拉一次，失败静默为空即无 kind 项） ----------
const kinds = ref<TraceItemKind[]>([]);
const kindsLoading = ref(false);

async function loadKinds(): Promise<void> {
  kindsLoading.value = true;
  try {
    kinds.value = await fetchTraceItemKinds(req());
  } catch {
    kinds.value = [];
  } finally {
    kindsLoading.value = false;
  }
}

// ---------- 状态 ----------
const rows = ref<TraceItemRow[]>([]);
const loading = ref(false);
const loadFailed = ref(false);
const failText = ref('');
let token = 0;

const REFRESH_MIN_SPIN_MS = 300;
const sleep = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms));

async function minSpin(started: number): Promise<void> {
  const used = Date.now() - started;
  const rest = Math.max(0, REFRESH_MIN_SPIN_MS - (Number.isFinite(used) && used > 0 ? used : 0));
  if (rest > 0) await sleep(rest);
}

async function load(force = false): Promise<void> {
  const mine = ++token;
  const started = Date.now();
  if (!force) loading.value = true;
  loadFailed.value = false;
  try {
    const r = await fetchTraceItems(req(), {
      appId: (appIdKw.value && appIdKw.value > 0 ? appIdKw.value : props.appId) || undefined,
      kind: kindKw.value || undefined,
      enable: enableKw.value,
      name: nameKw.value || undefined,
      Q: qKw.value || undefined,
      dtStart: dtStartKw.value || undefined,
      dtEnd: dtEndKw.value || undefined,
      pageSize: 100,
    });
    if (mine !== token) return;
    rows.value = r;
  } catch (e) {
    if (mine !== token) return;
    loadFailed.value = true;
    failText.value = e instanceof Error ? e.message : String(e);
    rows.value = [];
  }
  if (mine !== token) return;
  await minSpin(started);
  if (mine !== token) return;
  loading.value = false;
}

function query(): void {
  void load();
}

const { refreshing, refresh } = useManualRefresh({
  load: async () => {
    await Promise.all([load(true), loadKinds()]);
  },
  outcome: () => ({
    loadError: loadFailed.value ? `跟踪项加载失败：${failText.value}` : '',
    versionsFailed: false,
    nodesFailed: false,
    historyFailed: false,
  }),
  notify: (ok, text) => toast(text, ok ? 'ok' : 'err'),
});

// ---------- 选择与批量（声明必须先于 immediate watch：TDZ） ----------
const selected = ref<string[]>([]);

// 依赖序（SKILL §4 反模式「immediate watch 反序」）：kindKw 不依赖；load 依赖 props.appId。先 kinds 后 load。
watch(
  () => props.appId,
  () => {
    appIdKw.value = undefined;
    kindKw.value = '';
    enableKw.value = null;
    nameKw.value = '';
    qKw.value = '';
    dtStartKw.value = '';
    dtEndKw.value = '';
    selected.value = [];
    void loadKinds();
    void load();
  },
  { immediate: true },
);

// 宿主「埋点数」可点（裁定 3 裁量 2）：预设应用过滤 → 覆盖 appIdKw 并重查
watch(
  () => props.externalAppId,
  (v) => {
    if (v === undefined || v === null || v <= 0) return;
    appIdKw.value = v;
    selected.value = [];
    void load(true);
  },
);

// ---------- 选择与批量：交互函数（selected 声明见上） ----------

function allSelected(): boolean {
  return rows.value.length > 0 && selected.value.length === rows.value.length;
}

function toggleAll(e: Event): void {
  const on = (e.target as HTMLInputElement).checked;
  selected.value = on ? rows.value.map((r) => r.id) : [];
}

function toggleRow(id: string, e: Event): void {
  const on = (e.target as HTMLInputElement).checked;
  if (on && !selected.value.includes(id)) selected.value = [...selected.value, id];
  if (!on) selected.value = selected.value.filter((x) => x !== id);
}

const batchAction = ref('');

async function runBatch(action: 'EnableSelect' | 'DisableSelect' | 'Fix' | 'FixDisplay'): Promise<void> {
  if (selected.value.length === 0 || batchAction.value) return;
  batchAction.value = action;
  try {
    const msg = await runTraceItemAction(req(), action, selected.value);
    toast(msg || '成功！', 'ok');
    await load(true);
  } catch (e) {
    toast(`${action} 失败：${e instanceof Error ? e.message : String(e)}`, 'err');
  } finally {
    batchAction.value = '';
  }
}

const batchBusy = computed(() => batchAction.value !== '');

// ---------- 抽屉（新增/编辑） ----------
const drawerOpen = ref(false);
const drawerTitle = ref('新增跟踪项');
const drawerSub = ref('');
const drawerInitial = ref<TraceItemEditModel | null>(null);
const editingId = ref<string | undefined>(undefined);
const saving = ref(false);
const formError = ref('');

function openCreate(): void {
  editingId.value = undefined;
  drawerTitle.value = '新增跟踪项';
  drawerSub.value = props.appName ? `${props.appName} · #${props.appId}` : `#${props.appId}`;
  drawerInitial.value = {
    appId: props.appId,
    name: '',
    kind: '',
    displayName: '',
    enable: true,
    cloned: false,
    timeout: 0,
    rules: '',
  };
  formError.value = '';
  drawerOpen.value = true;
}

function openEdit(r: TraceItemRow): void {
  editingId.value = r.id;
  drawerTitle.value = `编辑跟踪项 · ${r.name}`;
  drawerSub.value = props.appName ? `${props.appName} · #${r.appId}` : `#${r.appId}`;
  drawerInitial.value = toTraceItemEditModel(r, props.appId);
  formError.value = '';
  drawerOpen.value = true;
}

async function onDrawerSubmit(m: TraceItemEditModel): Promise<void> {
  const err = validateTraceItemEdit(m);
  if (err) {
    formError.value = err;
    return;
  }
  formError.value = '';
  if (saving.value) return;
  saving.value = true;
  try {
    await saveTraceItem(req(), m, editingId.value);
    drawerOpen.value = false;
    toast('保存成功', 'ok');
    await load(true);
  } catch (e) {
    formError.value = `保存失败：${e instanceof Error ? e.message : String(e)}`;
  } finally {
    saving.value = false;
  }
}

// ---------- 删除（GuardModal 两段式） ----------
const deleting = ref('');

function onDelete(r: TraceItemRow): void {
  if (deleting.value) return;
  openGuard(
    '删除跟踪项',
    async () => {
      deleting.value = r.id;
      try {
        await deleteTraceItem(req(), r.id);
        toast(`已删除 ${r.name} 并级联清理每日统计`, 'ok');
        await load(true);
      } catch (e) {
        toast(`删除失败：${e instanceof Error ? e.message : String(e)}`, 'err');
      } finally {
        deleting.value = '';
      }
    },
    `将删除 ${r.name} 并级联清理其每日统计（TraceDayStat）`,
  );
}

const inputCls =
  'h-[28px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px] text-[12px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]';

function enableText(v: boolean | null): string {
  if (v === null) return '—';
  return v ? '启用' : '停用';
}
</script>

<template>
  <Card
    data-spec="trace-item-panel"
    class="mt-3"
  >
    <div :class="H3">
      <h3 class="m-0">
        跟踪项
      </h3>
      <span class="flex-1" />
      <Btn
        variant="default"
        size="sm"
        data-spec="trace-item-refresh"
        :disabled="refreshing || loading"
        @click="refresh"
      >
        <template #icon>
          <span
            v-if="refreshing"
            class="inline-flex animate-spin"
          >
            <span class="h-[12px] w-[12px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
          </span>
        </template>
        刷新
      </Btn>
      <Btn
        variant="primary"
        size="sm"
        data-spec="trace-item-create"
        @click="openCreate"
      >
        新增
      </Btn>
    </div>
    <p
      data-spec="trace-item-subtitle"
      class="mb-[10px] mt-0 text-[11.5px] text-[var(--el-text-color-secondary)]"
    >
      {{ subtitle }}
    </p>

    <div class="mb-[10px] flex flex-wrap items-center gap-2 text-[12px] text-[var(--el-text-color-secondary)]">
      <label>应用</label>
      <input
        v-model.number="appIdKw"
        data-spec="trace-item-appid"
        type="number"
        min="0"
        :placeholder="`#${appId}`"
        :class="inputCls"
        class="w-[88px]"
      >
      <label>种类</label>
      <select
        v-model="kindKw"
        data-spec="trace-item-kind"
        :class="inputCls"
      >
        <option value="">
          全部
        </option>
        <option
          v-for="k in kinds"
          :key="k.value"
          :value="k.value"
        >
          {{ k.label }}
        </option>
      </select>
      <label>启用</label>
      <select
        v-model="enableKw"
        data-spec="trace-item-enable"
        :class="inputCls"
        class="w-[86px]"
      >
        <option :value="null">
          全部
        </option>
        <option :value="true">
          启用
        </option>
        <option :value="false">
          停用
        </option>
      </select>
      <label>更新时间</label>
      <input
        v-model="dtStartKw"
        data-spec="trace-item-dt-start"
        type="date"
        :class="inputCls"
      >
      <label>止</label>
      <input
        v-model="dtEndKw"
        data-spec="trace-item-dt-end"
        type="date"
        :class="inputCls"
      >
      <label>操作名</label>
      <input
        v-model="nameKw"
        data-spec="trace-item-name"
        type="text"
        placeholder="精确操作名"
        :class="inputCls"
      >
      <label>关键字</label>
      <input
        v-model="qKw"
        data-spec="trace-item-q"
        type="text"
        placeholder="Q"
        :class="inputCls"
      >
      <Btn
        variant="primary"
        size="sm"
        data-spec="trace-item-query"
        :disabled="loading"
        @click="query"
      >
        <template #icon>
          <span
            v-if="loading"
            class="inline-flex animate-spin"
          >
            <span class="h-[12px] w-[12px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-white)]" />
          </span>
        </template>
        查询
      </Btn>
    </div>

    <div
      v-if="loadFailed"
      data-spec="trace-item-failed"
      class="py-6 text-[12.5px] text-[var(--el-color-danger)]"
    >
      跟踪项加载失败：{{ failText }}
      <Btn
        variant="default"
        size="sm"
        class="ml-2"
        data-spec="trace-item-retry"
        @click="load(true)"
      >
        重试
      </Btn>
    </div>

    <div
      v-else-if="rows.length === 0"
      data-spec="trace-item-empty"
      class="py-6 text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      <span
        v-if="loading"
        data-spec="trace-item-loading"
        class="mr-2 inline-flex animate-spin align-[-2px]"
      >
        <span class="h-[12px] w-[12px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
      </span>
      {{ loading ? '跟踪项加载中…' : '暂无跟踪项记录' }}
    </div>

    <template v-else>
      <div class="overflow-x-auto">
        <table class="w-full border-collapse">
          <thead>
            <tr>
              <th :class="TH">
                <input
                  type="checkbox"
                  data-spec="trace-item-sel-all"
                  :checked="allSelected()"
                  @change="toggleAll"
                >
              </th>
              <th :class="TH">
                编号
              </th>
              <th :class="TH">
                应用
              </th>
              <th :class="TH">
                种类
              </th>
              <th :class="TH">
                操作名
              </th>
              <th :class="TH">
                显示名
              </th>
              <th :class="TH">
                启用
              </th>
              <th :class="TH">
                规则
              </th>
              <th :class="TH">
                克隆
              </th>
              <th :class="TH">
                超时
              </th>
              <th :class="TH">
                天数
              </th>
              <th :class="TH">
                总次数
              </th>
              <th :class="TH">
                错误数
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
            <tr
              v-for="r in rows"
              :key="r.id"
              class="group"
              data-spec="trace-item-row"
            >
              <td :class="TD">
                <input
                  type="checkbox"
                  :data-spec="`trace-item-sel-${r.id}`"
                  :checked="selected.includes(r.id)"
                  @change="toggleRow(r.id, $event)"
                >
              </td>
              <td :class="TD">
                {{ r.id }}
              </td>
              <td :class="TD">
                {{ r.appId }}
              </td>
              <td :class="TD">
                {{ r.kind || '—' }}
              </td>
              <td
                :class="TD"
                class="font-mono"
              >
                {{ r.name }}
              </td>
              <td :class="TD">
                {{ r.displayName || '—' }}
              </td>
              <td :class="TD">
                <span
                  data-spec="trace-item-enable-tag"
                  :class="r.enable ? 'text-[var(--el-color-success)]' : 'text-[var(--el-text-color-secondary)]'"
                >{{ enableText(r.enable) }}</span>
              </td>
              <td
                :class="TD"
                class="max-w-[180px] truncate font-mono"
                :title="r.rules"
              >
                {{ r.rules || '—' }}
              </td>
              <td :class="TD">
                {{ r.cloned ? '是' : '否' }}
              </td>
              <td :class="TD">
                {{ r.timeout }}
              </td>
              <td :class="TD">
                {{ r.days }}
              </td>
              <td :class="TD">
                {{ r.total }}
              </td>
              <td :class="TD">
                <span :class="r.errors > 0 ? 'text-[var(--el-color-danger)]' : ''">{{ r.errors }}</span>
              </td>
              <td :class="TD">
                {{ r.updateUser || '—' }}
              </td>
              <td
                :class="TD"
                class="whitespace-nowrap"
              >
                {{ r.updateTime || '—' }}
              </td>
              <td :class="TD">
                <div class="flex gap-1">
                  <Btn
                    variant="default"
                    size="sm"
                    :data-spec="`trace-item-edit-${r.id}`"
                    @click="openEdit(r)"
                  >
                    编辑
                  </Btn>
                  <Btn
                    variant="default"
                    size="sm"
                    :data-spec="`trace-item-delete-${r.id}`"
                    :disabled="deleting === r.id"
                    :title="`删除 ${r.name} 并级联清理每日统计`"
                    @click="onDelete(r)"
                  >
                    {{ deleting === r.id ? '删除中…' : '删除' }}
                  </Btn>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div
        data-spec="trace-item-batch"
        class="mt-[8px] flex flex-wrap items-center gap-2 text-[12px] text-[var(--el-text-color-secondary)]"
      >
        <span data-spec="trace-item-batch-count">选中 {{ selected.length }} 项：</span>
        <Btn
          variant="default"
          size="sm"
          data-spec="trace-item-batch-enable"
          :disabled="batchBusy || selected.length === 0"
          @click="runBatch('EnableSelect')"
        >
          <template #icon>
            <span
              v-if="batchAction === 'EnableSelect'"
              class="inline-flex animate-spin"
            >
              <span class="h-[12px] w-[12px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
            </span>
          </template>
          启用
        </Btn>
        <Btn
          variant="default"
          size="sm"
          data-spec="trace-item-batch-disable"
          :disabled="batchBusy || selected.length === 0"
          @click="runBatch('DisableSelect')"
        >
          <template #icon>
            <span
              v-if="batchAction === 'DisableSelect'"
              class="inline-flex animate-spin"
            >
              <span class="h-[12px] w-[12px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
            </span>
          </template>
          停用
        </Btn>
        <Btn
          variant="default"
          size="sm"
          data-spec="trace-item-batch-fix"
          :disabled="batchBusy || selected.length === 0"
          @click="runBatch('Fix')"
        >
          <template #icon>
            <span
              v-if="batchAction === 'Fix'"
              class="inline-flex animate-spin"
            >
              <span class="h-[12px] w-[12px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
            </span>
          </template>
          修正统计
        </Btn>
        <Btn
          variant="default"
          size="sm"
          data-spec="trace-item-batch-fixdisplay"
          :disabled="batchBusy || selected.length === 0"
          @click="runBatch('FixDisplay')"
        >
          <template #icon>
            <span
              v-if="batchAction === 'FixDisplay'"
              class="inline-flex animate-spin"
            >
              <span class="h-[12px] w-[12px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
            </span>
          </template>
          修正显示名
        </Btn>
      </div>
    </template>

    <TraceItemDrawer
      :open="drawerOpen"
      :title="drawerTitle"
      :sub="drawerSub"
      :initial="drawerInitial"
      :kinds="kinds"
      :kinds-loading="kindsLoading"
      :busy="saving"
      :error="formError"
      @close="drawerOpen = false"
      @submit="onDrawerSubmit"
    />
  </Card>
</template>
