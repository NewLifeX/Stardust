<script setup lang="ts">
/**
 * 应用列表（/apps），对照原型 page-apps.jsx 的 PageApps：页头 + 分类 chip + 应用行卡片。
 * 数据复用 appsModel.fetchAppList（应用、在线实例、服务数、分钟统计四个接口并行）。
 * 取数失败与「没有应用」要分开显示；失败时给重试按钮。
 *
 * F12 应用管理：
 *  - 「新建应用」AppFormDrawer（POST /Registry/App 返回 data.id；Secret 列表隐藏、表单可见）；
 *  - 行首勾选（AppRow selectable）→ 批量启用/禁用（PUT /Registry/App/{id} 只提交 Enable，逐个）；
 *  - 批量删除 DELETE /Registry/App/{id}（Guard 确认，不可恢复）；
 *  - F12-1：行悬浮操作条「编辑」按钮 → AppFormDrawer 编辑态（PUT /Registry/App/{id}，Secret 留空=不修改）。
 * 取数随 stardustContext（项目/环境）失效重取（useContextReload）。
 */
import { computed, ref } from 'vue';
import { useRouter } from 'vue-router';
import { request } from '@newlifex/cube-vue/core/utils/request';
import { fetchAppList, type AppAggRow } from '../Stardust/appsModel';
import AppRow from './AppRow.vue';
import AppFormDrawer from '@/layouts/StardustLayout2/AppFormDrawer.vue';
import Btn from '@/layouts/StardustLayout2/Btn.vue';
import Card from '@/layouts/StardustLayout2/Card.vue';
import Chip from '@/layouts/StardustLayout2/Chip.vue';
import KIcon from '@/layouts/StardustLayout2/KIcon.vue';
import PageHead from '@/layouts/StardustLayout2/PageHead.vue';
import {
  ALL_CATEGORY,
  createApp,
  deleteApp,
  fetchAppBuildStatus,
  filterByCategory,
  listCategories,
  saveApp,
  setAppEnable,
} from '@/layouts/StardustLayout2/apps.logic';
import type { AppForm } from '@/layouts/StardustLayout2/apps.logic';
import { appPath, type AppTab } from '@/layouts/StardustLayout2/palette.items';
import { openGuard } from '@/layouts/StardustLayout2/ui.store';
import { toast } from '@/layouts/StardustLayout2/useToast';
import { stardustContext } from '@/stores/stardustContext';
import { useContextReload } from '@/layouts/StardustLayout2/useContextReload';

const router = useRouter();

const rows = ref<AppAggRow[]>([]);
const state = ref<'loading' | 'ok' | 'failed'>('loading');
const errorText = ref('');
const category = ref(ALL_CATEGORY);

const categories = computed(() => listCategories(rows.value));
const visible = computed(() => filterByCategory(rows.value, category.value));

// ---------- F12 应用 CRUD ----------

/** 勾选的应用 id（勾选即显示批量条） */
const selectedIds = ref<Set<number>>(new Set());
const batchBusy = ref(false);
const formOpen = ref(false);
/** null=新建 */
const formEditing = ref<AppForm | null>(null);
const formEditId = ref(0);
const formBusy = ref(false);

const selectedCount = computed(() => selectedIds.value.size);

function toggleSelect(id: number): void {
  const next = new Set(selectedIds.value);
  if (next.has(id)) next.delete(id);
  else next.add(id);
  selectedIds.value = next;
}

function clearSelection(): void {
  selectedIds.value = new Set();
}

const nameById = computed(() => {
  const m = new Map<number, string>();
  for (const r of rows.value) if (r.id !== undefined) m.set(r.id, r.displayName || r.name || `#${r.id}`);
  return m;
});

/** 逐个 PUT /{id}（部分更新只提交 Enable）；全过才算成功，失败列出对象 */
async function batchSetEnable(enable: boolean): Promise<void> {
  if (batchBusy.value || selectedCount.value === 0) return;
  batchBusy.value = true;
  try {
    const ids = [...selectedIds.value];
    let okCount = 0;
    const failed: string[] = [];
    for (const id of ids) {
      const r = await setAppEnable(request, id, enable);
      if (r.ok) okCount += 1;
      else failed.push(`${nameById.value.get(id) ?? `#${id}`}`);
    }
    if (failed.length) toast(`已${enable ? '启用' : '停用'} ${okCount} 个，失败：${failed.join('、')}`, 'err');
    else toast(`已${enable ? '启用' : '停用'} ${okCount} 个应用`, 'ok');
    clearSelection();
    await load(() => false);
  } finally {
    batchBusy.value = false;
  }
}

function askBatchDelete(): void {
  if (batchBusy.value || selectedCount.value === 0) return;
  const names = [...selectedIds.value].map((id) => nameById.value.get(id) ?? `#${id}`);
  openGuard(`确认删除 ${names.length} 个应用（${names.join('、')}）？删除后接入端再用同名注册会被当成新应用，不可恢复。`, async () => {
    batchBusy.value = true;
    try {
      const ids = [...selectedIds.value];
      let okCount = 0;
      const failed: string[] = [];
      for (const id of ids) {
        const r = await deleteApp(request, id);
        if (r.ok) okCount += 1;
        else failed.push(`${nameById.value.get(id) ?? `#${id}`}`);
      }
      if (failed.length) toast(`已删除 ${okCount} 个，失败：${failed.join('、')}`, 'err');
      else toast(`已删除 ${okCount} 个应用`, 'ok');
      clearSelection();
      await load(() => false);
    } finally {
      batchBusy.value = false;
    }
  });
}

function askCreateApp(): void {
  if (formBusy.value) return;
  formEditId.value = 0;
  formEditing.value = null;
  formOpen.value = true;
}

function askEditApp(r: AppAggRow): void {
  if (formBusy.value || r.id === undefined) return;
  formEditId.value = r.id;
  formEditing.value = {
    name: r.name ?? '',
    displayName: r.displayName ?? '',
    category: r.category ?? '',
    enable: r.enable !== false,
    period: 60,
    secret: '',
    remark: '',
  };
  formOpen.value = true;
}

async function onFormSubmit(form: AppForm): Promise<void> {
  if (formBusy.value) return;
  formBusy.value = true;
  try {
    const r = formEditId.value > 0
      ? await saveApp(request, formEditId.value, form)
      : await createApp(request, form, stardustContext.projectId);
    toast(r.message, r.ok ? 'ok' : 'err');
    if (r.ok) {
      formOpen.value = false;
      await load(() => false);
    }
  } finally {
    formBusy.value = false;
  }
}

async function load(isStale: () => boolean): Promise<void> {
  state.value = 'loading';
  errorText.value = '';
  try {
    const list = (await fetchAppList(request)).rows;
    if (isStale()) return;
    // 批量取构建状态（全局聚合，非逐应用 N+1）；失败不阻塞列表
    const buildMap = await fetchAppBuildStatus(request);
    if (isStale()) return;
    rows.value = list.map((r) => ({ ...r, buildStatus: buildMap.get(r.id ?? 0) ?? null }));
    if (!categories.value.includes(category.value)) category.value = ALL_CATEGORY;
    state.value = 'ok';
  } catch (e) {
    if (isStale()) return;
    errorText.value = e instanceof Error ? e.message : String(e);
    state.value = 'failed';
  }
}

function openApp(id: number | undefined, tab: AppTab): void {
  if (id !== undefined) void router.push(appPath(id, tab));
}

// 上下文（项目、环境）就绪后首轮取数，之后切换项目或环境自动重取
const { reload } = useContextReload(load, stardustContext);
</script>

<template>
  <div data-spec="page-apps">
    <PageHead
      :eyebrow="`应用 · ${rows.length}`"
      title="应用"
      sub="每个应用是一个工作台：性能、链路、日志、配置、实例、发布都在同一处。"
    >
      <template #actions>
        <Btn
          variant="primary"
          data-spec="apps-create"
          @click="askCreateApp"
        >
          <template #icon>
            <KIcon
              name="Plus"
              :size="13"
            />
          </template>
          新建应用
        </Btn>
      </template>
    </PageHead>

    <p
      v-if="state === 'loading'"
      data-spec="apps-loading"
      class="text-[13px] text-[var(--el-text-color-secondary)]"
    >
      应用列表加载中…
    </p>

    <div
      v-else-if="state === 'failed'"
      data-spec="apps-failed"
      class="flex items-center gap-[10px] text-[13px] text-[var(--el-color-danger)]"
    >
      <span>应用列表加载失败：{{ errorText }}</span>
      <Btn
        size="sm"
        data-spec="apps-retry"
        @click="reload"
      >
        重试
      </Btn>
    </div>

    <p
      v-else-if="rows.length === 0"
      data-spec="apps-empty"
      class="text-[13px] text-[var(--el-text-color-secondary)]"
    >
      还没有接入应用，点右上角「新建应用」创建。
    </p>

    <template v-else>
      <div class="mb-[14px] flex flex-wrap items-center gap-[8px]">
        <Chip
          v-for="c in categories"
          :key="c"
          :on="c === category"
          data-spec="apps-chip"
          @click="category = c"
        >
          {{ c }}
        </Chip>
        <span class="flex-1" />
        <span class="font-mono text-[11px] text-[var(--el-text-color-secondary)]">
          点击任意行进入工作台 · 快捷键 P / C / L
        </span>
      </div>

      <!-- F12 批量操作条（勾选即显示） -->
      <div
        v-if="selectedCount > 0"
        data-spec="apps-batchbar"
        class="mb-[10px] flex flex-wrap items-center gap-[10px] rounded-[10px] border border-[color-mix(in_oklab,var(--el-color-primary)_35%,transparent)] bg-[var(--el-bg-color-overlay)] px-[14px] py-[10px]"
      >
        <span class="text-[12.5px] text-[var(--el-text-color-primary)]">
          已选 {{ selectedCount }} 个应用
        </span>
        <span class="flex-1" />
        <Btn
          size="sm"
          :disabled="batchBusy"
          :aria-busy="batchBusy"
          data-spec="apps-batch-enable"
          @click="batchSetEnable(true)"
        >
          批量启用
        </Btn>
        <Btn
          size="sm"
          :disabled="batchBusy"
          data-spec="apps-batch-disable"
          @click="batchSetEnable(false)"
        >
          批量停用
        </Btn>
        <Btn
          size="sm"
          variant="ghost"
          :disabled="batchBusy"
          class="!text-[var(--el-color-danger)]"
          data-spec="apps-batch-delete"
          @click="askBatchDelete"
        >
          删除
        </Btn>
        <Btn
          size="sm"
          variant="ghost"
          :disabled="batchBusy"
          data-spec="apps-batch-clear"
          @click="clearSelection"
        >
          取消选择
        </Btn>
      </div>

      <Card padding="none">
        <AppRow
          v-for="r in visible"
          :key="r.id ?? r.name"
          :row="r"
          selectable
          :selected="r.id !== undefined && selectedIds.has(r.id)"
          @open="(tab) => openApp(r.id, tab)"
          @toggle="toggleSelect"
          @edit="askEditApp"
        />
      </Card>
    </template>

    <!-- F12 新建/编辑应用 Drawer（Secret 表单可见、列表隐藏） -->
    <AppFormDrawer
      :open="formOpen"
      :initial="formEditing"
      :title="formEditId > 0 ? `编辑应用 · ${formEditing?.name || ''}` : '新建应用'"
      :sub="formEditId > 0 ? '应用名不可修改；密钥留空=保持原值' : '创建后可到工作台配置发布与实例'"
      :busy="formBusy"
      @close="formOpen = false"
      @submit="onFormSubmit"
    />
  </div>
</template>
