<script setup lang="ts">
/**
 * AppConfigTable：应用工作台「配置」页签（原型 README §4.3 第 182-195 行）。
 * 纯 UI + 自带三态取数：props 只收 appId；deps.request 是测试缝（默认接 cube-vue request）。
 * 数据源：GET /Configs/AppConfig?appId= → GET /Configs/ConfigData?configId= 与 ConfigHistory、
 * GET /Registry/AppOnline?appId=；发布 POST /Configs/AppConfig/Publish?configId=，
 * 回滚 POST /Configs/ConfigHistory/Rollback?id=。
 *
 * F10 就地编辑（ConfigDataController 源码语义，2026-11-03 回读验证）：
 *  - 行内「编辑」开 ConfigItemDrawer：已发布行改 Key/Scope/Value 会被后端 400 拒绝 → Drawer 里只读，
 *    改动写 NewValue 草稿（Enable 翻转转 NewStatus）；未发布行（新增，Version==0）可直接改；
 *  - 「新增配置项」POST /Configs/ConfigData（OnInsert 自动分配 NewVersion，走待发布）；
 *  - 行内「删除」DELETE /Configs/ConfigData/{id}：未发布真删、已发布改标 [[Deleted]]（发布后生效）；
 *  - 「同步应用」POST /Configs/AppConfig/SyncApp（把注册表应用同步成 AppConfig，全局动作）。
 * 现实约束（后端没有对应入口，标待接入而不造数据）：放弃草稿（没有丢弃草稿的接口）、推送进度卡
 * （Publish 只返回通知的应用数）。
 */
import { computed, ref, watch } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import {
  createConfigItem,
  deleteConfigItem,
  fetchConfigApp,
  fetchConfigHistory,
  fetchConfigItems,
  fetchOnlineCount,
  filterPending,
  publishConfig,
  rollbackHistory,
  saveConfigItem,
  syncAppConfig,
} from './appConfig.logic';
import type { ConfigApp, ConfigHistoryItem, ConfigItem, ConfigItemForm } from './appConfig.logic.ts';
import ConfigItemDrawer from './ConfigItemDrawer.vue';
import { openGuard } from './ui.store.ts';
import { toast } from './useToast.ts';
import { useManualRefresh } from './useManualRefresh.ts';
import Btn from './Btn.vue';
import Card from './Card.vue';
import KIcon from './KIcon.vue';
import Tag from './Tag.vue';
import { H3, TD, TH } from './deployView.styles.ts';

const props = defineProps<{
  appId: number;
  /** 测试缝：注入 request；生产不传 */
  deps?: { request?: Req };
}>();

/** 取数入口：默认 cube-vue request，story/CT 注入桩 */
function req(): Req {
  return props.deps?.request ?? (request as Req);
}

const loading = ref(true);
const loadFailed = ref(false);
const config = ref<ConfigApp | null>(null);
const items = ref<ConfigItem[]>([]);
const history = ref<ConfigHistoryItem[]>([]);
const onlineCount = ref(0);
/** 正在进行的操作：'publish' | 'sync' | `rollback:<id>` | `item:<id>`；非空时相关按钮禁用 */
const busy = ref<string | null>(null);
const diffOpen = ref(false);

/** F10 配置项编辑 Drawer */
const itemDrawerOpen = ref(false);
/** null=新增；否则编辑（published 决定字段读写） */
const itemEditing = ref<(ConfigItemForm & { published: boolean }) | null>(null);
const itemEditId = ref(0);

const pending = computed(() => filterPending(items.value));
const hasDraft = computed(() => !!config.value && config.value.nextVersion > config.value.version);

const STATE_TAG: Record<ConfigItem['state'], { text: string; tone: 'ok' | 'warn' | 'err' }> = {
  published: { text: '已发布', tone: 'ok' },
  modified: { text: '已修改', tone: 'warn' },
  added: { text: '新增', tone: 'warn' },
  deleted: { text: '待删除', tone: 'err' },
};

async function load(): Promise<void> {
  loadFailed.value = false;
  const cfg = await fetchConfigApp(req(), props.appId);
  if (!cfg) {
    loadFailed.value = true;
    return;
  }
  config.value = cfg;
  const [its, his, online] = await Promise.all([
    fetchConfigItems(req(), cfg.id, cfg.version),
    fetchConfigHistory(req(), cfg.id),
    fetchOnlineCount(req(), props.appId),
  ]);
  items.value = its ?? [];
  history.value = his ?? [];
  onlineCount.value = online;
}

const { refreshing, refresh } = useManualRefresh({
  load: async () => {
    loading.value = true;
    try {
      await load();
    } finally {
      loading.value = false;
    }
  },
  outcome: () => ({
    loadError: loadFailed.value ? '配置加载失败' : '',
    versionsFailed: false,
    nodesFailed: false,
    historyFailed: false,
  }),
  notify: (ok, text) => toast(text, ok ? 'ok' : 'err'),
});

watch(
  () => props.appId,
  async () => {
    loading.value = true;
    diffOpen.value = false;
    try {
      await load();
    } finally {
      loading.value = false;
    }
  },
  { immediate: true },
);

function onPublish(): void {
  const cfg = config.value;
  if (!cfg || busy.value) return;
  openGuard(
    `确认发布配置 v${cfg.nextVersion}？将热更新 ${onlineCount.value} 个在线实例`,
    async () => {
      busy.value = 'publish';
      try {
        const r = await publishConfig(req(), cfg.id);
        toast(r.message, r.ok ? 'ok' : 'err');
        if (r.ok) await refresh();
      } finally {
        busy.value = null;
      }
    },
  );
}

function onRollback(h: ConfigHistoryItem): void {
  const cfg = config.value;
  if (!cfg || busy.value) return;
  openGuard(`确认回滚到版本 v${h.version}？将撤销当前未发布的草稿`, async () => {
    busy.value = `rollback:${h.id}`;
    try {
      const r = await rollbackHistory(req(), h.id);
      toast(r.message, r.ok ? 'ok' : 'err');
      if (r.ok) await refresh();
    } finally {
      busy.value = null;
    }
  });
}

// ---------- F10 配置项就地编辑 ----------

/** 新增配置项：POST 建 Key/Scope/Value，走待发布草稿 */
function askCreateItem(): void {
  if (busy.value) return;
  itemEditId.value = 0;
  itemEditing.value = { key: '', scope: '', value: '', newValue: '', enable: true, remark: '', published: false };
  itemDrawerOpen.value = true;
}

/** 编辑配置项：已发布行 Key/Scope/Value 只读（后端拒绝），改动写 NewValue 草稿 */
function askEditItem(it: ConfigItem): void {
  if (busy.value || it.state === 'deleted') return;
  itemEditId.value = it.id;
  itemEditing.value = {
    key: it.key,
    scope: it.scope,
    value: it.value,
    newValue: it.newValue,
    enable: it.enable,
    remark: '',
    published: it.state !== 'added',
  };
  itemDrawerOpen.value = true;
}

async function onItemSubmit(form: ConfigItemForm): Promise<void> {
  if (busy.value) return;
  busy.value = 'item';
  try {
    const r = itemEditId.value > 0
      ? await saveConfigItem(
          req(),
          { id: itemEditId.value, published: itemEditing.value?.published ?? false, enable: form.enable },
          form,
        )
      : await createConfigItem(req(), config.value?.id ?? 0, form);
    toast(r.message, r.ok ? 'ok' : 'err');
    if (r.ok) {
      itemDrawerOpen.value = false;
      await refresh();
    }
  } finally {
    busy.value = null;
  }
}

/** 删除配置项：未发布真删；已发布后端改标待删除（发布后生效） */
function askDeleteItem(it: ConfigItem): void {
  if (busy.value || it.state === 'deleted') return;
  const isPub = it.state !== 'added';
  openGuard(
    isPub
      ? `确认删除配置项「${it.key}」？已发布的键不会被立即移除，会先标记待删除，发布后生效。`
      : `确认删除配置项「${it.key}」？尚未发布，将直接移除。`,
    async () => {
      busy.value = `item:${it.id}`;
      try {
        const r = await deleteConfigItem(req(), it.id);
        toast(r.message, r.ok ? 'ok' : 'err');
        if (r.ok) await refresh();
      } finally {
        busy.value = null;
      }
    },
  );
}

/** 同步应用：把注册表应用同步成 AppConfig（全局动作，返回「成功同步N个应用！」） */
async function onSync(): Promise<void> {
  if (busy.value) return;
  busy.value = 'sync';
  try {
    const r = await syncAppConfig(req());
    toast(r.message, r.ok ? 'ok' : 'err');
    if (r.ok) await refresh();
  } finally {
    busy.value = null;
  }
}
</script>

<template>
  <Card data-spec="app-config">
    <div class="flex flex-wrap items-center gap-x-3 gap-y-2">
      <h3 :class="H3">
        配置
        <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">
          {{ config ? `版本 v${config.version}${hasDraft ? `，草稿 v${config.nextVersion}` : ''}` : '—' }}
        </span>
      </h3>
      <span class="flex-1" />
      <Btn
        size="sm"
        variant="default"
        :disabled="!!busy || refreshing"
        :aria-busy="busy === 'sync'"
        data-spec="app-config-sync"
        title="把注册表应用同步成应用配置（全局动作）"
        @click="onSync"
      >
        {{ busy === 'sync' ? '同步中…' : '同步应用' }}
      </Btn>
      <Btn
        size="sm"
        variant="default"
        :disabled="!!busy || refreshing"
        data-spec="app-config-create"
        @click="askCreateItem"
      >
        <template #icon>
          <KIcon
            name="Plus"
            :size="12"
          />
        </template>
        新增配置项
      </Btn>
      <Btn
        size="sm"
        :disabled="!!busy || refreshing"
        :aria-busy="refreshing"
        data-spec="app-config-refresh"
        @click="refresh"
      >
        <template #icon>
          <span :class="refreshing ? 'inline-flex animate-spin' : 'inline-flex'">
            <KIcon
              name="Refresh"
              :size="12"
            />
          </span>
        </template>
        {{ refreshing ? '刷新中…' : '刷新' }}
      </Btn>
    </div>

    <div
      v-if="loading"
      class="py-10 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
      data-spec="app-config-loading"
    >
      配置加载中…
    </div>
    <div
      v-else-if="loadFailed"
      class="py-10 text-center text-[12.5px] text-[var(--el-color-danger)]"
      data-spec="app-config-failed"
    >
      配置加载失败，请点「刷新」重试
    </div>

    <div
      v-else
      class="mt-3 flex items-start gap-5"
    >
      <!-- 左：键值表 -->
      <div class="min-w-0 flex-1">
        <table class="w-full border-collapse">
          <thead>
            <tr>
              <th :class="[TH, 'w-[30%]']">
                Key
              </th>
              <th :class="TH">
                Value
              </th>
              <th :class="[TH, 'w-[80px]']">
                状态
              </th>
              <th :class="[TH, 'w-[120px] text-right']">
                操作
              </th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="it in items"
              :key="it.id"
              data-spec="app-config-row"
              :data-state="it.state"
              class="group"
              :class="it.pending ? 'bg-[color-mix(in_oklab,var(--el-color-warning)_7%,transparent)]' : ''"
            >
              <td :class="[TD, 'font-mono text-[12px] text-[var(--el-text-color-primary)]']">
                {{ it.key }}
              </td>
              <td :class="TD">
                <div class="font-mono text-[12px] text-[var(--el-text-color-primary)]">
                  {{ it.value || (it.state === 'deleted' ? '—' : '（空）') }}
                </div>
                <div
                  v-if="it.pending && it.newValue !== it.value"
                  class="mt-[2px] font-mono text-[11.5px] text-[var(--el-color-warning)]"
                  data-spec="app-config-draft"
                >
                  草稿：{{ it.newValue || '（空）' }}
                </div>
              </td>
              <td :class="TD">
                <Tag :tone="STATE_TAG[it.state].tone">
                  {{ STATE_TAG[it.state].text }}
                </Tag>
              </td>
              <td :class="[TD, 'text-right whitespace-nowrap']">
                <Btn
                  size="sm"
                  variant="ghost"
                  :disabled="!!busy || it.state === 'deleted'"
                  :title="it.state === 'deleted' ? '待删除的键不可编辑' : (it.state !== 'added' ? '已发布：名称/作用域/生效值只读，改动写入草稿' : '未发布：可直接修改')"
                  data-spec="app-config-edit"
                  @click="askEditItem(it)"
                >
                  编辑
                </Btn>
                <Btn
                  size="sm"
                  variant="ghost"
                  :disabled="!!busy || it.state === 'deleted'"
                  :aria-busy="busy === `item:${it.id}`"
                  class="!text-[var(--el-color-danger)]"
                  :title="it.state !== 'added' ? '已发布：先标记待删除，发布后生效' : '未发布：直接移除'"
                  data-spec="app-config-remove"
                  @click="askDeleteItem(it)"
                >
                  {{ busy === `item:${it.id}` ? '删除中…' : '删除' }}
                </Btn>
              </td>
            </tr>
            <tr v-if="!items.length">
              <td
                colspan="4"
                class="p-5 text-[12.5px] text-[var(--el-text-color-secondary)]"
                data-spec="app-config-empty"
              >
                该应用还没有配置项，点「新增配置项」创建
              </td>
            </tr>
          </tbody>
        </table>

        <!-- 待发布浮条 -->
        <div
          v-if="hasDraft"
          class="mt-3 flex flex-wrap items-center gap-x-3 gap-y-2 rounded-[10px] border border-[color-mix(in_oklab,var(--el-color-primary)_35%,transparent)] bg-[var(--el-bg-color-overlay)] px-[14px] py-3"
          data-spec="app-config-pubbar"
        >
          <div>
            <div
              class="text-[13px] font-semibold text-[var(--el-text-color-primary)]"
              data-spec="app-config-pub-count"
            >
              {{ pending.length }} 项待发布
            </div>
            <div class="mt-[2px] text-[11px] text-[var(--el-text-color-secondary)]">
              将热更新 {{ onlineCount }} 个在线实例（生产）
            </div>
          </div>
          <span class="flex-1" />
          <Btn
            size="sm"
            variant="ghost"
            disabled
            title="待接入：后端没有放弃草稿的接口"
            data-spec="app-config-discard"
          >
            放弃
          </Btn>
          <Btn
            size="sm"
            variant="default"
            data-spec="app-config-diff"
            @click="diffOpen = !diffOpen"
          >
            {{ diffOpen ? '收起差异' : '查看差异' }}
          </Btn>
          <Btn
            size="sm"
            variant="primary"
            :disabled="!!busy"
            :aria-busy="busy === 'publish'"
            data-spec="app-config-publish"
            @click="onPublish"
          >
            {{ busy === 'publish' ? '发布中…' : '发布到生产' }}
          </Btn>
        </div>

        <!-- 差异明细（前端按草稿行算出，不调接口） -->
        <div
          v-if="diffOpen && pending.length"
          class="mt-2 rounded-[8px] border border-[var(--el-border-color-light)] px-[14px] py-2"
          data-spec="app-config-diff-list"
        >
          <div
            v-for="it in pending"
            :key="it.id"
            class="flex gap-3 py-[3px] font-mono text-[11.5px]"
          >
            <span class="w-[34%] shrink-0 text-[var(--el-text-color-secondary)]">
              {{ it.key }}
            </span>
            <span class="min-w-0 flex-1 truncate text-[var(--el-text-color-secondary)]">
              {{ it.value || '（空）' }} →
            </span>
            <span class="min-w-0 flex-1 truncate text-[var(--el-color-warning)]">
              {{ it.newValue || '（空）' }}
            </span>
          </div>
        </div>
      </div>

      <!-- 右栏：版本历史 -->
      <aside class="w-[210px] shrink-0 border-l border-[var(--el-border-color-light)] pl-4">
        <div class="text-[12px] font-semibold text-[var(--el-text-color-primary)]">
          版本历史
        </div>
        <div
          v-for="h in history"
          :key="h.id"
          data-spec="app-config-history-row"
          class="flex items-center gap-2 border-b border-[var(--el-border-color-light)] py-[8px] last:border-b-0"
        >
          <Tag tone="brand">
            v{{ h.version }}
          </Tag>
          <span class="text-[11px] text-[var(--el-text-color-secondary)]">{{ h.time }}</span>
          <Tag
            v-if="config && h.version === config.version"
            tone="ok"
          >
            当前
          </Tag>
          <Btn
            v-else-if="h.action === 'Publish'"
            size="sm"
            variant="ghost"
            :disabled="!!busy"
            class="ml-auto"
            data-spec="app-config-rollback"
            @click="onRollback(h)"
          >
            {{ busy === `rollback:${h.id}` ? '回滚中…' : '回滚' }}
          </Btn>
        </div>
        <div
          v-if="!history.length"
          class="py-4 text-[12px] text-[var(--el-text-color-secondary)]"
          data-spec="app-config-history-empty"
        >
          还没有发布历史
        </div>
        <div class="mt-3 font-mono text-[10.5px] leading-[1.6] text-[var(--el-text-color-secondary)]">
          标记的值已脱敏，仅授权者可见明文。
        </div>
      </aside>
    </div>

    <!-- F10 配置项新增/编辑 Drawer（应用配置尚未加载时不可打开） -->
    <ConfigItemDrawer
      :open="itemDrawerOpen && !!config"
      :initial="itemEditing"
      :title="itemEditId > 0 ? `编辑配置项 · ${itemEditing?.key || ''}` : '新增配置项'"
      :sub="`应用配置：${config?.name || '—'}`"
      :busy="busy === 'item'"
      @close="itemDrawerOpen = false"
      @submit="onItemSubmit"
    />
  </Card>
</template>
