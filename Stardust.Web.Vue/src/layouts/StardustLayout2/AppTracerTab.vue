<script setup lang="ts">
/**
 * AppTracerTab：应用工作台「监控中心」页签（F01）。
 * 列表 + 编辑 Drawer + 删除 + 批量修正 Fix / FixOldData；无跟踪器时空态引导。
 * 数据真实接口：GET /api/Monitors/AppTracer?appId=（后端在 appId 有效但无跟踪器时会自动补建）。
 * 行为真源见 appTracer.logic.ts 头注释（AppTracerController.cs：SelectKeys=GetRequest("Keys") 逗号串）。
 * 交互反馈按 AGENTS.md §11：刷新/保存/修正/删除按钮一律 loading + 禁用 + 结果 toast，失败写明对象与步骤。
 */
import { computed, ref, watch } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import Btn from './Btn.vue';
import Card from './Card.vue';
import Drawer from './Drawer/Drawer.vue';
import KIcon from './KIcon.vue';
import Tag from './Tag.vue';
import { openGuard } from './ui.store';
import { toast } from './useToast.ts';
import { useManualRefresh } from './useManualRefresh.ts';
import { H3, TD, TH } from './deployView.styles.ts';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import {
  TRACE_MODES,
  deleteTracer,
  fetchTracers,
  fixTracers,
  saveTracer,
  toTracerEditModel,
  traceModeLabel,
  tracerEmptyState,
  validateTracerEdit,
  type TracerEditModel,
  type TracerRow,
} from './appTracer.logic.ts';

const props = defineProps<{
  appId: number;
  appName?: string;
  deps?: { request?: Req };
}>();

function req(): Req {
  return props.deps?.request ?? (request as Req);
}

const rows = ref<TracerRow[]>([]);
const loading = ref(true);
const loadFailed = ref(false);
const failText = ref('');
let token = 0;

const selected = ref<number[]>([]);
const allSelected = computed(() => rows.value.length > 0 && selected.value.length === rows.value.length);
const someSelected = computed(() => selected.value.length > 0 && selected.value.length < rows.value.length);

function toggleAll(): void {
  selected.value = allSelected.value ? [] : rows.value.map((r) => r.id);
}

function toggleRow(id: number): void {
  const i = selected.value.indexOf(id);
  if (i >= 0) selected.value.splice(i, 1);
  else selected.value.push(id);
}

async function load(force = false): Promise<void> {
  const mine = ++token;
  if (!force) loading.value = true;
  loadFailed.value = false;
  try {
    const list = await fetchTracers(req(), props.appId);
    if (mine !== token) return;
    rows.value = list;
    const alive = new Set(list.map((r) => r.id));
    selected.value = selected.value.filter((id) => alive.has(id));
  } catch (e) {
    if (mine !== token) return;
    loadFailed.value = true;
    failText.value = e instanceof Error ? e.message : String(e);
    rows.value = [];
  } finally {
    if (mine === token) loading.value = false;
  }
}

const { refreshing, refresh } = useManualRefresh({
  load: async () => {
    await load(true);
  },
  outcome: () => ({
    loadError: loadFailed.value ? `监控中心加载失败：${failText.value}` : '',
    versionsFailed: false,
    nodesFailed: false,
    historyFailed: false,
  }),
  notify: (ok, text) => toast(text, ok ? 'ok' : 'err'),
});

watch(
  () => props.appId,
  () => {
    selected.value = [];
    void load();
  },
  { immediate: true },
);

// ---------- 批量修正 ----------
const fixing = ref<'' | 'fix' | 'fixold'>('');

async function runFix(kind: 'fix' | 'fixold'): Promise<void> {
  const ids = [...selected.value];
  if (ids.length === 0 || fixing.value !== '') return;
  const label = kind === 'fix' ? '修正' : '修正旧数据';
  openGuard(`对选中的 ${ids.length} 个跟踪器执行${label}（Action 重新对齐）。确认？`, async () => {
    fixing.value = kind;
    try {
      const msg = await fixTracers(req(), ids, kind === 'fixold');
      toast(`${label}完成：${msg || '成功'}`, 'ok');
      await load(true);
    } catch (e) {
      toast(`${label}失败（${ids.length} 个跟踪器）：${e instanceof Error ? e.message : String(e)}`, 'err');
    } finally {
      fixing.value = '';
    }
  });
}

// ---------- 编辑 Drawer ----------
const drawerOpen = ref(false);
const editing = ref<TracerRow | null>(null);
const form = ref<TracerEditModel | null>(null);
const formError = ref('');
const submitting = ref(false);

function openEdit(r: TracerRow): void {
  editing.value = r;
  form.value = toTracerEditModel(r);
  formError.value = '';
  drawerOpen.value = true;
}

function closeDrawer(): void {
  drawerOpen.value = false;
  editing.value = null;
  form.value = null;
  formError.value = '';
}

async function submitEdit(): Promise<void> {
  if (!form.value || !editing.value || submitting.value) return;
  const err = validateTracerEdit(form.value);
  if (err) {
    formError.value = err;
    return;
  }
  formError.value = '';
  submitting.value = true;
  try {
    await saveTracer(req(), editing.value.id, form.value);
    toast(`跟踪器「${editing.value.name}」已保存`, 'ok');
    closeDrawer();
    await load(true);
  } catch (e) {
    formError.value = `保存失败：${e instanceof Error ? e.message : String(e)}`;
  } finally {
    submitting.value = false;
  }
}

// ---------- 删除 ----------
const deleting = ref<number | null>(null);

function askDelete(r: TracerRow): void {
  if (deleting.value !== null) return;
  openGuard(`删除跟踪器「${r.name}」（应用 #${r.appId}）。删除后该应用的监控参数恢复默认，确认？`, async () => {
    deleting.value = r.id;
    try {
      await deleteTracer(req(), r.id);
      toast(`跟踪器「${r.name}」已删除`, 'ok');
      await load(true);
    } catch (e) {
      toast(`删除「${r.name}」失败：${e instanceof Error ? e.message : String(e)}`, 'err');
    } finally {
      deleting.value = null;
    }
  });
}

const emptyText = computed(() => tracerEmptyState(rows.value, props.appId, props.appName));

const inputCls =
  'h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]';
</script>

<template>
  <Card data-spec="tracer-tab">
    <div class="mb-[10px] flex flex-wrap items-center gap-2">
      <h3 :class="H3">
        监控中心 · 应用跟踪器
      </h3>
      <span class="flex-1" />
      <Btn
        variant="default"
        size="sm"
        data-spec="tracer-refresh"
        :disabled="refreshing || loading"
        @click="refresh"
      >
        <template #icon>
          <span :class="{ 'animate-spin': refreshing }">
            <KIcon
              name="Refresh"
              :size="12"
            />
          </span>
        </template>
        刷新
      </Btn>
      <Btn
        variant="primary"
        size="sm"
        data-spec="tracer-fix"
        :disabled="selected.length === 0 || fixing !== ''"
        :title="selected.length === 0 ? '先勾选要修正的跟踪器' : `对选中的 ${selected.length} 个跟踪器执行修正`"
        @click="runFix('fix')"
      >
        <template #icon>
          <span
            v-if="fixing === 'fix'"
            class="inline-flex animate-spin"
          >
            <span class="h-[12px] w-[12px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-white)]" />
          </span>
        </template>
        批量修正
      </Btn>
      <Btn
        variant="default"
        size="sm"
        data-spec="tracer-fixold"
        :disabled="selected.length === 0 || fixing !== ''"
        :title="selected.length === 0 ? '先勾选要修正的跟踪器' : '对选中跟踪器重算历史数据'"
        @click="runFix('fixold')"
      >
        <template #icon>
          <span
            v-if="fixing === 'fixold'"
            class="inline-flex animate-spin"
          >
            <span class="h-[12px] w-[12px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-white)]" />
          </span>
        </template>
        修正旧数据
      </Btn>
    </div>
    <p
      v-if="selected.length === 0"
      data-spec="tracer-hint"
      class="mb-2 text-[12px] text-[var(--el-text-color-secondary)]"
    >
      勾选跟踪器后可执行「批量修正 / 修正旧数据」。
    </p>

    <div
      v-if="loadFailed"
      data-spec="tracer-failed"
      class="py-6 text-[12.5px] text-[var(--el-color-danger)]"
    >
      监控中心加载失败：{{ failText }}
      <Btn
        variant="default"
        size="sm"
        class="ml-2"
        data-spec="tracer-retry"
        @click="load(true)"
      >
        重试
      </Btn>
    </div>

    <div
      v-else-if="rows.length === 0"
      data-spec="tracer-empty"
      class="py-6 text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      <span
        v-if="loading"
        class="mr-2 inline-flex animate-spin align-[-2px]"
      >
        <span class="h-[12px] w-[12px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
      </span>
      {{ loading ? '正在加载跟踪器…' : emptyText }}
    </div>

    <div
      v-else
      class="overflow-x-auto"
    >
      <table class="w-full border-collapse">
        <thead>
          <tr>
            <th :class="TH">
              <input
                type="checkbox"
                data-spec="tracer-select"
                :checked="allSelected"
                :indeterminate.prop="someSelected"
                aria-label="全选跟踪器"
                @change="toggleAll"
              >
            </th>
            <th :class="TH">
              名称
            </th>
            <th :class="TH">
              显示名
            </th>
            <th :class="TH">
              状态
            </th>
            <th :class="TH">
              跟踪模式
            </th>
            <th :class="TH">
              采样周期(s)
            </th>
            <th :class="TH">
              跟踪项
            </th>
            <th :class="TH">
              累计次数
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
            data-spec="tracer-row"
            :data-tracer-id="r.id"
          >
            <td :class="TD">
              <input
                type="checkbox"
                data-spec="tracer-select"
                :checked="selected.includes(r.id)"
                :aria-label="`选择跟踪器 ${r.name}`"
                @change="toggleRow(r.id)"
              >
            </td>
            <td :class="TD">
              {{ r.name }}
            </td>
            <td :class="TD">
              {{ r.displayName || '—' }}
            </td>
            <td :class="TD">
              <Tag :tone="r.enable === false ? 'err' : r.enable ? 'ok' : 'default'">
                {{ r.enable === false ? '停用' : r.enable ? '启用' : '—' }}
              </Tag>
            </td>
            <td :class="TD">
              {{ traceModeLabel(r.mode) }}
            </td>
            <td
              :class="TD"
              class="tabular-nums"
            >
              {{ r.period }}
            </td>
            <td
              :class="TD"
              class="tabular-nums"
            >
              {{ r.itemCount }}
            </td>
            <td
              :class="TD"
              class="tabular-nums"
            >
              {{ r.total }}
            </td>
            <td :class="TD">
              {{ r.updateTime || '—' }}
            </td>
            <td :class="TD">
              <button
                type="button"
                data-spec="tracer-edit"
                class="mr-2 text-[12px] text-[var(--el-color-primary)] hover:underline"
                @click="openEdit(r)"
              >
                编辑
              </button>
              <button
                type="button"
                data-spec="tracer-delete"
                class="text-[12px] text-[var(--el-color-danger)] hover:underline disabled:pointer-events-none disabled:opacity-40"
                :disabled="deleting !== null"
                @click="askDelete(r)"
              >
                <span
                  v-if="deleting === r.id"
                  class="mr-1 inline-flex animate-spin align-[-2px]"
                >
                  <span class="h-[10px] w-[10px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-white)]" />
                </span>
                {{ deleting === r.id ? '删除中…' : '删除' }}
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <Drawer
      :open="drawerOpen"
      :title="editing ? `编辑跟踪器 · ${editing.name}` : '编辑跟踪器'"
      :sub="editing ? `应用 #${editing.appId} · 编号 ${editing.id}` : ''"
      @close="closeDrawer"
    >
      <div
        v-if="form"
        class="flex flex-col gap-[12px]"
        data-spec="tracer-form"
      >
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">显示名</span>
          <input
            v-model="form.displayName"
            :class="inputCls"
            data-spec="tracer-form-displayname"
          >
        </label>
        <div class="flex items-center gap-[16px]">
          <label class="flex items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]">
            <input
              v-model="form.enable"
              type="checkbox"
              data-spec="tracer-form-enable"
            >
            启用
          </label>
          <label class="flex items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]">
            <input
              v-model="form.enableMeter"
              type="checkbox"
              data-spec="tracer-form-meter"
            >
            性能收集
          </label>
        </div>
        <div class="grid grid-cols-2 gap-[10px]">
          <label class="flex flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">跟踪模式</span>
            <select
              v-model.number="form.mode"
              :class="inputCls"
              data-spec="tracer-form-mode"
            >
              <option
                v-for="m in TRACE_MODES"
                :key="m.value"
                :value="m.value"
              >
                {{ m.label }}
              </option>
            </select>
          </label>
          <label class="flex flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">采样周期（秒）</span>
            <input
              v-model.number="form.period"
              type="number"
              min="0"
              :class="inputCls"
              data-spec="tracer-form-period"
            >
          </label>
          <label class="flex flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">正常数（最大正常采样）</span>
            <input
              v-model.number="form.maxSamples"
              type="number"
              min="0"
              :class="inputCls"
              data-spec="tracer-form-maxsamples"
            >
          </label>
          <label class="flex flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">异常数</span>
            <input
              v-model.number="form.maxErrors"
              type="number"
              min="0"
              :class="inputCls"
              data-spec="tracer-form-maxerrors"
            >
          </label>
          <label class="flex flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">超时时间（毫秒）</span>
            <input
              v-model.number="form.timeout"
              type="number"
              min="0"
              :class="inputCls"
              data-spec="tracer-form-timeout"
            >
          </label>
          <label class="flex flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">告警阈值（错误数，0 不启用）</span>
            <input
              v-model.number="form.alarmThreshold"
              type="number"
              min="0"
              :class="inputCls"
              data-spec="tracer-form-alarmthreshold"
            >
          </label>
          <label class="flex flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">告警错误率（0~1，0 不启用）</span>
            <input
              v-model.number="form.alarmErrorRate"
              type="number"
              min="0"
              step="0.01"
              :class="inputCls"
              data-spec="tracer-form-alarmrate"
            >
          </label>
          <label class="flex flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">钩子地址（WebHook）</span>
            <input
              v-model="form.webHook"
              :class="inputCls"
              data-spec="tracer-form-webhook"
            >
          </label>
        </div>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">白名单（支持 * 模糊，如 /Cube/*）</span>
          <input
            v-model="form.whiteList"
            :class="inputCls"
            data-spec="tracer-form-whitelist"
          >
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">排除项（支持 * 模糊）</span>
          <input
            v-model="form.excludes"
            :class="inputCls"
            data-spec="tracer-form-excludes"
          >
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">Vip客户端（高频大样本，逗号分割）</span>
          <input
            v-model="form.vipClients"
            :class="inputCls"
            data-spec="tracer-form-vipclients"
          >
        </label>

        <p
          v-if="formError"
          data-spec="tracer-form-error"
          class="m-0 text-[12px] text-[var(--el-color-danger)]"
        >
          {{ formError }}
        </p>
      </div>

      <template #actions>
        <Btn
          variant="ghost"
          data-spec="tracer-form-cancel"
          @click="closeDrawer"
        >
          取消
        </Btn>
        <Btn
          variant="primary"
          :disabled="submitting"
          data-spec="tracer-form-submit"
          @click="submitEdit"
        >
          <template
            v-if="submitting"
            #icon
          >
            <span class="inline-flex animate-spin">
              <span class="h-[12px] w-[12px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-white)]" />
            </span>
          </template>
          {{ submitting ? '保存中…' : '保存修改' }}
        </Btn>
      </template>
    </Drawer>
  </Card>
</template>
