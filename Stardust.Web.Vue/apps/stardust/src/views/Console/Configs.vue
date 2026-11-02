<script setup lang="ts">
/**
 * 跨应用配置中心页（/configs）。
 * 上：ConfigCenterList——数据由 configCenter.logic.fetchAllConfigs 编排：拉全部 AppConfig +
 * 应用列表建 appId→名映射 + 一枪 GET /Configs/ConfigData（不传 configId 即全量）按 configId
 * 分组数 Key 数（UsedKeys 是客户端上报字段，本地常全 0，真实键数以 ConfigData 行数为准）。
 * 点行打开 ConfigDetailDrawer；「前往编辑」跳对应应用的配置页签 /app/config/<appId>。
 *
 * 下（F11）：AppRuleTable——应用规则是全局规则表（无 AppId 列），CRUD 走 /Configs/AppRule
 * （POST 新增 / PUT /{id} 部分更新 / DELETE /{id}），Drawer 表单在 AppRuleDrawer。
 *
 * 下（F15）：AppQuoteTable——应用依赖（AppQuote：AppId 依赖 TargetAppId），CRUD 走 /Configs/AppQuote；
 * 行内应用名由 fetchAppList 建 id→名映射注入；Drawer 下拉同源。
 * 取数随 stardustContext（项目/环境）失效重取（useContextReload）。
 */
import { ref } from 'vue';
import { useRouter } from 'vue-router';
import { request } from '@newlifex/cube-vue/core/utils/request';
import Btn from '@/layouts/StardustLayout2/Btn.vue';
import KIcon from '@/layouts/StardustLayout2/KIcon.vue';
import PageHead from '@/layouts/StardustLayout2/PageHead.vue';
import ConfigCenterList from '@/layouts/StardustLayout2/ConfigCenterList.vue';
import ConfigDetailDrawer from '@/layouts/StardustLayout2/ConfigDetailDrawer.vue';
import AppRuleTable from '@/layouts/StardustLayout2/AppRuleTable.vue';
import AppRuleDrawer from '@/layouts/StardustLayout2/AppRuleDrawer.vue';
import AppQuoteTable from '@/layouts/StardustLayout2/AppQuoteTable.vue';
import AppQuoteDrawer from '@/layouts/StardustLayout2/AppQuoteDrawer.vue';
import { fetchAppList } from '../Stardust/appsModel';
import {
  createAppQuote,
  createAppRule,
  deleteAppQuote,
  deleteAppRule,
  fetchAllConfigs,
  fetchAppQuotes,
  fetchAppRules,
  fetchConfigDetail,
  saveAppQuote,
  saveAppRule,
} from '@/layouts/StardustLayout2/configCenter.logic';
import type {
  AppQuoteForm,
  AppQuoteRow,
  AppRuleForm,
  AppRuleRow,
  ConfigCenterRow,
  ConfigDetail,
} from '@/layouts/StardustLayout2/configCenter.logic';
import { openGuard } from '@/layouts/StardustLayout2/ui.store';
import { toast } from '@/layouts/StardustLayout2/useToast';
import { stardustContext } from '@/stores/stardustContext';
import { useContextReload } from '@/layouts/StardustLayout2/useContextReload';

const router = useRouter();

const rows = ref<ConfigCenterRow[]>([]);
const loading = ref(false);
const errorText = ref('');

const drawerOpen = ref(false);
const detail = ref<ConfigDetail | null>(null);
const detailLoading = ref(false);
const detailError = ref('');

// ---------- F11 应用规则 ----------

const rules = ref<AppRuleRow[]>([]);
const rulesLoading = ref(false);
const rulesError = ref('');
/** 删除中的规则 id（行内按钮写「删除中…」） */
const ruleRemovingId = ref(0);
const ruleDrawerOpen = ref(false);
/** null=新增 */
const ruleEditing = ref<AppRuleForm | null>(null);
const ruleEditId = ref(0);
const ruleBusy = ref(false);

async function loadRules(isStale: () => boolean): Promise<void> {
  rulesLoading.value = true;
  rulesError.value = '';
  try {
    const r = await fetchAppRules(request);
    if (isStale()) return;
    if (r === null) rulesError.value = '加载失败';
    else rules.value = r;
  } finally {
    if (!isStale()) rulesLoading.value = false;
  }
}

async function load(isStale: () => boolean): Promise<void> {
  loading.value = true;
  errorText.value = '';
  try {
    const r = await fetchAllConfigs(request, stardustContext.projectId);
    if (isStale()) return;
    rows.value = r.rows;
    errorText.value = r.error;
  } catch (e) {
    if (isStale()) return;
    errorText.value = e instanceof Error ? e.message : '配置中心加载失败';
  } finally {
    if (!isStale()) loading.value = false;
  }
  // 规则块独立三态，失败不拖垮配置列表
  void loadRules(isStale);
  // 依赖块独立三态，失败不拖垮配置列表
  void loadQuotes(isStale);
}

async function onOpen(configId: number): Promise<void> {
  const row = rows.value.find((r) => r.configId === configId);
  if (!row) return;
  drawerOpen.value = true;
  detail.value = null;
  detailError.value = '';
  detailLoading.value = true;
  try {
    const d = await fetchConfigDetail(request, configId, row.appId, row.appName);
    detail.value = d;
    detailError.value = d ? '' : '配置详情加载失败';
  } finally {
    detailLoading.value = false;
  }
}

function onGotoAppConfig(appId: number): void {
  drawerOpen.value = false;
  void router.push(`/app/config/${appId}`);
}

// F11 规则操作：新增/编辑/删除，全部 Guard/Toast 反馈（§11）

function askCreateRule(): void {
  if (ruleBusy.value) return;
  ruleEditId.value = 0;
  ruleEditing.value = { rule: '', result: '', priority: 0, enable: true, remark: '' };
  ruleDrawerOpen.value = true;
}

function askEditRule(row: AppRuleRow): void {
  if (ruleBusy.value) return;
  ruleEditId.value = row.id;
  ruleEditing.value = { rule: row.rule, result: row.result, priority: row.priority, enable: row.enable, remark: row.remark };
  ruleDrawerOpen.value = true;
}

async function onRuleSubmit(form: AppRuleForm): Promise<void> {
  if (ruleBusy.value) return;
  ruleBusy.value = true;
  try {
    const r = ruleEditId.value > 0 ? await saveAppRule(request, ruleEditId.value, form) : await createAppRule(request, form);
    toast(r.message, r.ok ? 'ok' : 'err');
    if (r.ok) {
      ruleDrawerOpen.value = false;
      await loadRules(() => false);
    }
  } finally {
    ruleBusy.value = false;
  }
}

function askDeleteRule(row: AppRuleRow): void {
  if (ruleBusy.value) return;
  openGuard(`确认删除应用规则「${row.rule}」？删除后按该规则划分的作用域立即失效。`, async () => {
    ruleRemovingId.value = row.id;
    try {
      const r = await deleteAppRule(request, row.id);
      toast(r.message, r.ok ? 'ok' : 'err');
      if (r.ok) await loadRules(() => false);
    } finally {
      ruleRemovingId.value = 0;
    }
  });
}

// ---------- F15 应用依赖（AppQuote） ----------

const quotes = ref<AppQuoteRow[]>([]);
const quotesLoading = ref(false);
const quotesError = ref('');
const quoteRemovingId = ref(0);
const quoteDrawerOpen = ref(false);
/** null=新增 */
const quoteEditing = ref<AppQuoteForm | null>(null);
const quoteEditId = ref(0);
const quoteBusy = ref(false);
/** 应用下拉选项（依赖方/被依赖方同源） */
const quoteApps = ref<{ id: number; label: string }[]>([]);

async function loadQuotes(isStale: () => boolean): Promise<void> {
  quotesLoading.value = true;
  quotesError.value = '';
  try {
    const [list, appRes] = await Promise.all([
      fetchAppQuotes(request),
      fetchAppList(request),
    ]);
    if (isStale()) return;
    if (list === null) {
      quotesError.value = '加载失败';
      quotes.value = [];
    } else {
      const names = new Map<number, string>();
      for (const a of appRes.rows) names.set(Number(a.id), String(a.displayName || a.name || `#${a.id}`));
      const nameOf = (id: number) => names.get(id) ?? `#${id}`;
      // 列表已带映射（fetchAppQuotes 默认 nameOf 空），这里按应用名重算一遍
      quotes.value = list.map((q) => ({ ...q, appName: nameOf(q.appId), targetName: nameOf(q.targetAppId) }));
      quoteApps.value = appRes.rows.map((a) => ({ id: Number(a.id), label: String(a.displayName || a.name || `#${a.id}`) }));
    }
  } finally {
    if (!isStale()) quotesLoading.value = false;
  }
}

function askCreateQuote(): void {
  if (quoteBusy.value) return;
  quoteEditId.value = 0;
  quoteEditing.value = { appId: 0, targetAppId: 0, kind: 0, enable: true };
  quoteDrawerOpen.value = true;
}

function askEditQuote(row: AppQuoteRow): void {
  if (quoteBusy.value) return;
  quoteEditId.value = row.id;
  quoteEditing.value = { appId: row.appId, targetAppId: row.targetAppId, kind: row.kind, enable: row.enable };
  quoteDrawerOpen.value = true;
}

async function onQuoteSubmit(form: AppQuoteForm): Promise<void> {
  if (quoteBusy.value) return;
  quoteBusy.value = true;
  try {
    const r = quoteEditId.value > 0 ? await saveAppQuote(request, quoteEditId.value, form) : await createAppQuote(request, form);
    toast(r.message, r.ok ? 'ok' : 'err');
    if (r.ok) {
      quoteDrawerOpen.value = false;
      await loadQuotes(() => false);
    }
  } finally {
    quoteBusy.value = false;
  }
}

function askDeleteQuote(row: AppQuoteRow): void {
  if (quoteBusy.value) return;
  openGuard(`确认删除依赖「${row.appName} → ${row.targetName}」？删除后应用侧按依赖划分的可见性立即恢复为无依赖。`, async () => {
    quoteRemovingId.value = row.id;
    try {
      const r = await deleteAppQuote(request, row.id);
      toast(r.message, r.ok ? 'ok' : 'err');
      if (r.ok) await loadQuotes(() => false);
    } finally {
      quoteRemovingId.value = 0;
    }
  });
}

const { reload } = useContextReload(load, stardustContext);
</script>

<template>
  <div data-spec="page-configs">
    <PageHead
      eyebrow="配置 · 跨应用"
      title="配置中心"
      sub="汇总所有应用的配置项：搜索、按应用过滤，点行看完整键值、待发布差异与版本历史。就地编辑请到对应应用的配置页签。"
    >
      <template #actions>
        <Btn
          size="sm"
          :disabled="loading"
          data-spec="configs-refresh"
          @click="reload"
        >
          <template #icon>
            <KIcon
              name="Refresh"
              :size="13"
              :class="loading ? 'animate-spin' : ''"
            />
          </template>
          {{ loading ? '刷新中…' : '刷新' }}
        </Btn>
      </template>
    </PageHead>

    <ConfigCenterList
      :rows="rows"
      :loading="loading"
      :error="errorText"
      @open="onOpen"
      @retry="reload"
    />

    <AppRuleTable
      :rows="rules"
      :loading="rulesLoading"
      :error="rulesError"
      :removing-id="ruleRemovingId"
      :busy="ruleBusy"
      @retry="loadRules(() => false)"
      @create="askCreateRule"
      @edit="askEditRule"
      @remove="askDeleteRule"
    />

    <AppQuoteTable
      :rows="quotes"
      :loading="quotesLoading"
      :error="quotesError"
      :removing-id="quoteRemovingId"
      :busy="quoteBusy"
      @retry="loadQuotes(() => false)"
      @create="askCreateQuote"
      @edit="askEditQuote"
      @remove="askDeleteQuote"
    />

    <ConfigDetailDrawer
      :open="drawerOpen"
      :detail="detail"
      :loading="detailLoading"
      :error="detailError"
      @close="drawerOpen = false"
      @goto-app-config="onGotoAppConfig"
    />

    <AppRuleDrawer
      :open="ruleDrawerOpen"
      :initial="ruleEditing"
      :title="ruleEditId > 0 ? '编辑应用规则' : '新增应用规则'"
      :sub="ruleEditId > 0 ? '规则分号分段，逗号间隔多项匹配值' : '命中后按 Result 给应用划分作用域（如 Scope=dev）'"
      :busy="ruleBusy"
      @close="ruleDrawerOpen = false"
      @submit="onRuleSubmit"
    />

    <AppQuoteDrawer
      :open="quoteDrawerOpen"
      :apps="quoteApps"
      :initial="quoteEditing"
      :title="quoteEditId > 0 ? '编辑应用依赖' : '新增应用依赖'"
      :sub="quoteEditId > 0 ? '调整依赖关系后立即生效' : '声明「依赖方 → 被依赖方」的引用关系'"
      :busy="quoteBusy"
      @close="quoteDrawerOpen = false"
      @submit="onQuoteSubmit"
    />
  </div>
</template>
