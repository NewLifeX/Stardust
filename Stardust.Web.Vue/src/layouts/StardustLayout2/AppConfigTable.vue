<script setup lang="ts">
/**
 * AppConfigTable：应用工作台「配置」页签（原型 README §4.3 第 182-195 行）。
 * 纯 UI + 自带三态取数：props 只收 appId；deps.request 是测试缝（默认接 cube-vue request）。
 * 数据源：GET /Configs/AppConfig?appId= → GET /Configs/ConfigData?configId= 与 ConfigHistory、
 * GET /Registry/AppOnline?appId=；发布 POST /Configs/AppConfig/Publish?configId=，
 * 回滚 POST /Configs/ConfigHistory/Rollback?id=。
 *
 * 现实约束（后端没有对应入口，标待接入而不造数据）：
 *  - 就地编辑：草稿写入接口未在本层验证（旧配置中心同样把「保存草稿」标为待接入），Value 列只读展示；
 *  - 放弃草稿：后端没有丢弃草稿的接口，按钮禁用并注明；
 *  - 推送进度卡：Publish 只返回通知的应用数，没有逐实例确认进度。
 */
import { computed, ref, watch } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import {
  fetchConfigApp,
  fetchConfigHistory,
  fetchConfigItems,
  fetchOnlineCount,
  filterPending,
  publishConfig,
  rollbackHistory,
} from './appConfig.logic';
import type { ConfigApp, ConfigHistoryItem, ConfigItem } from './appConfig.logic.ts';
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
/** 正在进行的操作：'publish' 或 `rollback:<id>`；非空时相关按钮禁用 */
const busy = ref<string | null>(null);
const diffOpen = ref(false);

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
</script>

<template>
  <Card data-spec="app-config">
    <div class="flex flex-wrap items-center gap-x-3 gap-y-2">
      <h3 :class="H3">
        配置
        <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">
          {{ config ? `版本 v${config.version}${hasDraft ? `，草稿 v${config.nextVersion}` : ''}` : '—' }}
        </span>
        <Tag tone="default">
          后端暂不支持草稿编辑，发布即生效
        </Tag>
      </h3>
      <span class="flex-1" />
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
              <th :class="[TH, 'w-[34%]']">
                Key
              </th>
              <th :class="TH">
                Value
              </th>
              <th :class="[TH, 'w-[84px]']">
                状态
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
            </tr>
            <tr v-if="!items.length">
              <td
                colspan="3"
                class="p-5 text-[12.5px] text-[var(--el-text-color-secondary)]"
                data-spec="app-config-empty"
              >
                该应用还没有配置项
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
  </Card>
</template>
