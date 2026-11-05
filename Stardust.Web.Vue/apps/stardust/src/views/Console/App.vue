<script setup lang="ts">
/**
 * 应用工作台（/app/:tab/:id），对照原型 page-apps.jsx 的 PageApp：面包屑、头像、标题、Tag、两个快捷按钮。
 * 页签：原型五个 + 注册侧五个（应用历史/命令/性能/注册/消费）+ 监控（监控中心/监控大屏）+ 部署四个。
 * 页签切换只改路由的 :tab，不换页面，页头与数据保持不动（「上下文贯穿」）。
 * 取数：复用 appsModel.fetchAppList，按路由 :id 找应用；不用 fetchAppDetail（一次 5 个请求，多数是各页签才要的）。
 * 部署面板 ShipPanel：版本、节点、部署历史、流水线四个页签各渲染一块，由 tabSection 决定；概览页签传 overview（版本条加需关注事项，按钮跳页签）；调用链、日志、配置、实例接口与原型对照确认后在后续停点实现，先显示「待接入」。
 * 原型页头下方的上下游依赖与 app-id 行是演示数据，后端没有，不画。
 * 五种视图：id 非法、加载中、加载失败（可重试）、找不到应用、正常，都有明确文字，不留空白。
 */
import { computed, ref } from 'vue';
import { RouterLink, useRoute, useRouter } from 'vue-router';
import { request } from '@newlifex/cube-vue/core/utils/request';
import { fetchAppList, statusLabel, type AppAggRow } from '../Stardust/appsModel';
import ShipPanel from './ShipPanel.vue';
import AppTabs from '@/layouts/StardustLayout2/AppTabs.vue';
import TraceList from '@/layouts/StardustLayout2/TraceList.vue';
import AppLog from '@/layouts/StardustLayout2/AppLog.vue';
import AppConfigTable from '@/layouts/StardustLayout2/AppConfigTable.vue';
import AppInstances from '@/layouts/StardustLayout2/AppInstances.vue';
import AppObserve from '@/layouts/StardustLayout2/AppObserve.vue';
import AppTracerTab from '@/layouts/StardustLayout2/AppTracerTab.vue';
import DayStatTab from '@/layouts/StardustLayout2/DayStatTab.vue';
import Btn from '@/layouts/StardustLayout2/Btn.vue';
import Card from '@/layouts/StardustLayout2/Card.vue';
import KIcon from '@/layouts/StardustLayout2/KIcon.vue';
import Tag from '@/layouts/StardustLayout2/Tag.vue';
import { avatarInitial } from '@/layouts/StardustLayout2/avatar.logic';
import { avatarTone, categoryOf } from '@/layouts/StardustLayout2/apps.logic';
import { appPath, parseAppId, type AppTab } from '@/layouts/StardustLayout2/palette.items';
import { findAppById, resolveWorkbenchView, statusTone } from '@/layouts/StardustLayout2/appWorkbench.logic';
import { WORKBENCH_TABS, resolveTab, tabLabel, tabSection } from '@/layouts/StardustLayout2/workbench.logic';
import { observeQueryToRoute, toObserveQuery, type ObserveKind, type ObserveQuery } from '@/layouts/StardustLayout2/appObserve.logic';
import { stardustContext } from '@/stores/stardustContext';
import { useContextReload } from '@/layouts/StardustLayout2/useContextReload';

const route = useRoute();
const router = useRouter();

const rows = ref<AppAggRow[]>([]);
const load = ref<'loading' | 'ok' | 'failed'>('loading');
const errorText = ref('');

const tab = computed(() => resolveTab(route.params.tab));
// 当前页签对应的部署内容块；null 表示不是部署页签，显示「待接入」
const section = computed(() => tabSection(tab.value));
const id = computed(() => parseAppId(route.params.id));
const row = computed(() => findAppById(rows.value, id.value));
const view = computed(() => resolveWorkbenchView({ id: id.value, load: load.value, found: row.value !== null }));

const name = computed(() => row.value?.name ?? '');
const title = computed(() => row.value?.displayName || name.value);
const items = computed(() => WORKBENCH_TABS.map((t) => ({ key: t.key, label: tabLabel(t.key, row.value?.instances) })));
const OBSERVE: readonly ObserveKind[] = ['appHistory', 'commands', 'meter', 'services', 'consume'];
const observeKind = computed(() => (OBSERVE.includes(tab.value as ObserveKind) ? (tab.value as ObserveKind) : null));
const observeQuery = computed(() => toObserveQuery(route.query as Record<string, unknown>));

async function loadApps(isStale: () => boolean): Promise<void> {
  load.value = 'loading';
  errorText.value = '';
  try {
    const list = (await fetchAppList(request)).rows;
    if (isStale()) return;
    rows.value = list;
    load.value = 'ok';
  } catch (e) {
    if (isStale()) return;
    errorText.value = e instanceof Error ? e.message : String(e);
    load.value = 'failed';
  }
}

function go(key: AppTab, query: Record<string, string> = {}): void {
  if (id.value !== null) void router.push({ path: appPath(id.value, key), query });
}

function onObserveQuery(q: ObserveQuery): void {
  if (observeKind.value) go(observeKind.value, observeQueryToRoute(q));
}

function onInstanceDrill(payload: { tab: 'appHistory' | 'meter'; client: string }): void {
  if (payload.tab === 'meter') go('meter', payload.client ? { clientId: payload.client } : {});
  else go('appHistory', payload.client ? { client: payload.client } : {});
}

// 上下文（项目、环境）就绪后首轮取数，之后切换项目或环境自动重取
const { reload } = useContextReload(loadApps, stardustContext);
</script>

<template>
  <div data-spec="page-app">
    <p
      v-if="view === 'invalid'"
      data-spec="app-invalid"
      class="text-[13px] text-[var(--el-color-danger)]"
    >
      应用 Id「{{ route.params.id }}」不是有效的正整数，请从
      <RouterLink
        to="/apps"
        class="text-[var(--el-color-primary)]"
      >
        应用列表
      </RouterLink>
      进入。
    </p>

    <p
      v-else-if="view === 'loading'"
      data-spec="app-loading"
      class="text-[13px] text-[var(--el-text-color-secondary)]"
    >
      应用加载中…
    </p>

    <div
      v-else-if="view === 'failed'"
      data-spec="app-failed"
      class="flex items-center gap-[10px] text-[13px] text-[var(--el-color-danger)]"
    >
      <span>应用加载失败：{{ errorText }}</span>
      <Btn
        size="sm"
        data-spec="app-retry"
        @click="reload"
      >
        重试
      </Btn>
    </div>

    <p
      v-else-if="view === 'notfound'"
      data-spec="app-notfound"
      class="text-[13px] text-[var(--el-text-color-secondary)]"
    >
      没有找到 Id 为 {{ id }} 的应用，它可能已被删除。返回
      <RouterLink
        to="/apps"
        class="text-[var(--el-color-primary)]"
      >
        应用列表
      </RouterLink>
      。
    </p>

    <template v-else-if="row">
      <div class="mb-[10px] flex items-center gap-[6px] font-mono text-[11.5px] text-[var(--el-text-color-secondary)]">
        <RouterLink
          to="/apps"
          data-spec="app-crumb"
          class="text-[var(--el-text-color-secondary)] hover:text-[var(--el-text-color-primary)]"
        >
          应用
        </RouterLink>
        <span>/</span>
        <b class="font-medium text-[var(--el-text-color-primary)]">{{ title }}</b>
      </div>

      <div class="flex flex-wrap items-end gap-[14px]">
        <span
          data-spec="app-avatar"
          class="grid h-[40px] w-[40px] flex-none place-items-center rounded-[8px] text-[17px] font-semibold text-[var(--el-color-white)]"
          :style="{ background: avatarTone(name) }"
        >
          {{ avatarInitial(name, '?') }}
        </span>
        <div class="min-w-0">
          <h1
            data-spec="app-title"
            class="m-0 text-[24px] font-semibold tracking-[-0.02em] text-[var(--el-text-color-primary)]"
          >
            {{ title }}
          </h1>
          <div class="mt-[5px] flex flex-wrap gap-[6px]">
            <Tag>{{ categoryOf(row) }}</Tag>
            <Tag>{{ row.version || '无版本' }}</Tag>
            <Tag
              data-spec="app-status"
              :tone="statusTone(row.status)"
            >
              {{ statusLabel[row.status] }}
            </Tag>
          </div>
        </div>
        <span class="flex-1" />
        <Btn
          data-spec="app-go-config"
          @click="go('config')"
        >
          <template #icon>
            <KIcon
              name="Sliders"
              :size="13"
            />
          </template>
          改配置
        </Btn>
        <Btn
          variant="primary"
          data-spec="app-go-versions"
          @click="go('versions')"
        >
          <template #icon>
            <KIcon
              name="Rocket"
              :size="13"
            />
          </template>
          发布
        </Btn>
      </div>

      <AppTabs
        :items="items"
        :active="tab"
        @select="go"
      />

      <ShipPanel
        v-if="(tab === 'overview' || section !== null) && row.id !== undefined"
        :app-id="Number(row.id)"
        :app-name="name"
        :section="section ?? undefined"
        :overview="tab === 'overview'"
        :instances="row.instances"
        @go="go"
      />
      <TraceList
        v-else-if="tab === 'trace' && row.id !== undefined"
        :app-id="Number(row.id)"
        @open-config="go('config')"
        @locate-instance="go('instances')"
      />
      <AppLog
        v-else-if="tab === 'logs' && row.id !== undefined"
        :app-id="Number(row.id)"
      />
      <template
        v-else-if="tab === 'config' && row.id !== undefined"
      >
        <AppConfigTable
          :app-id="Number(row.id)"
        />
      </template>
      <AppInstances
        v-else-if="tab === 'instances' && row.id !== undefined"
        :app-id="Number(row.id)"
        @drill="onInstanceDrill"
      />
      <AppObserve
        v-else-if="observeKind && row.id !== undefined"
        :app-id="Number(row.id)"
        :kind="observeKind"
        :query="observeQuery"
        @query="onObserveQuery"
      />
      <AppTracerTab
        v-else-if="tab === 'tracer' && row.id !== undefined"
        :app-id="Number(row.id)"
        :app-name="name"
      />
      <DayStatTab
        v-else-if="tab === 'daystat' && row.id !== undefined"
        :app-id="Number(row.id)"
        :app-name="name"
      />
      <Card
        v-else
        data-spec="app-pending"
      >
        <p class="m-0 text-[13px] text-[var(--el-text-color-secondary)]">
          「{{ tabLabel(tab) }}」页签待接入：接口与原型对照确认后，在后续停点实现。
        </p>
      </Card>
    </template>
  </div>
</template>
