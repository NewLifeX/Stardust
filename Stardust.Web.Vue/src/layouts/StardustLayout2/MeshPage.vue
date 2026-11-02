<script setup lang="ts">
/**
 * MeshPage：治理页（/mesh）智能容器。四个 tab：注册中心 / 网关 / AI·MCP / 平台设置。
 * 取数与归一全部在 mesh.logic（request 注入）；本组件只管 tab 切换、四 tab 独立加载与上下文联动。
 * projectId 取自 stardustContext（非空时随请求 params 透传，全局拦截器也会注入）；
 * 单个 tab 请求失败只让该 tab 显示错误+重试，不拖垮整页。展示细节见各 *Tab.vue（纯 UI）。
 */
import { computed, onMounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { request } from '@newlifex/cube-vue/core/utils/request';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import PageHead from './PageHead.vue';
import Btn from './Btn.vue';
import KIcon from './KIcon.vue';
import RegistryTab from './RegistryTab.vue';
import GatewayTab from './GatewayTab.vue';
import McpTab from './McpTab.vue';
import PlatformTab from './PlatformTab.vue';
import { fetchGateway, fetchMcpServers, fetchPlatform, fetchRegistry } from './mesh.logic.ts';
import type { GatewayRouteRow, McpServerRow, MemberRow, PlatformTile, ProjectInfo, RegistryRow } from './mesh.logic.ts';
import { fetchMcpManage, type McpManageData } from './mcpToken.logic.ts';
import { stardustContext } from '@/stores/stardustContext';
import { useContextReload } from './useContextReload.ts';

type MeshTab = 'registry' | 'gateway' | 'mcp' | 'platform';

const TABS: ReadonlyArray<{ key: MeshTab; label: string }> = [
  { key: 'registry', label: '注册中心' },
  { key: 'gateway', label: '网关' },
  { key: 'mcp', label: 'AI / MCP' },
  { key: 'platform', label: '平台设置' },
];

const MESH_TAB_KEY = 'stardust-mesh-tab';
const route = useRoute();
const router = useRouter();

function readInitialTab(): MeshTab {
  const q = String(route.query.tab ?? '');
  if (q === 'mcp' || q === 'registry' || q === 'gateway' || q === 'platform') return q;
  const saved = localStorage.getItem(MESH_TAB_KEY);
  if (saved === 'mcp' || saved === 'registry' || saved === 'gateway' || saved === 'platform') return saved;
  return 'mcp';
}

const active = ref<MeshTab>(readInitialTab());

onMounted(() => {
  if (route.query.tab !== active.value) {
    void router.replace({ query: { ...route.query, tab: active.value } });
  }
});

watch(active, (tab) => {
  localStorage.setItem(MESH_TAB_KEY, tab);
  if (route.query.tab !== tab) {
    void router.replace({ query: { ...route.query, tab } });
  }
});

interface State<T> {
  rows: T[];
  loading: boolean;
  error: string;
}
function initState<T>(): State<T> {
  return { rows: [], loading: true, error: '' };
}

const registry = ref<State<RegistryRow>>(initState());
const gateway = ref<State<GatewayRouteRow>>(initState());
const mcp = ref<{ data: McpManageData; servers: McpServerRow[]; serversError: string; loading: boolean; error: string }>({
  data: { tokens: [], resources: [], audits: [], projects: [], users: [], nodes: [], apps: [], error: '' },
  servers: [], serversError: '', loading: true, error: '',
});
const platform = ref<{ rows: PlatformTile[]; members: MemberRow[]; projectInfo: ProjectInfo | null; loading: boolean; error: string }>({
  rows: [], members: [], projectInfo: null, loading: true, error: '',
});

const anyLoading = computed(
  () => registry.value.loading || gateway.value.loading || mcp.value.loading || platform.value.loading,
);

const req = request as Req;

async function loadRegistry(): Promise<void> {
  registry.value.loading = true;
  const r = await fetchRegistry(req, stardustContext.projectId);
  registry.value.rows = r.rows;
  registry.value.error = r.error;
  registry.value.loading = false;
}
async function loadGateway(): Promise<void> {
  gateway.value.loading = true;
  const r = await fetchGateway(req, stardustContext.projectId);
  gateway.value.rows = r.rows;
  gateway.value.error = r.error;
  gateway.value.loading = false;
}
async function loadMcp(): Promise<void> {
  mcp.value.loading = true;
  const [mgd, srv] = await Promise.all([
    fetchMcpManage(req, stardustContext.projectId),
    fetchMcpServers(req, stardustContext.projectId),
  ]);
  mcp.value.data = mgd;
  mcp.value.error = mgd.error;
  mcp.value.servers = srv.rows;
  mcp.value.serversError = srv.error;
  mcp.value.loading = false;
}
async function loadPlatform(): Promise<void> {
  platform.value.loading = true;
  const r = await fetchPlatform(req, stardustContext.projectId);
  platform.value.rows = r.tiles;
  platform.value.members = r.members;
  platform.value.projectInfo = r.projectInfo;
  platform.value.error = r.error;
  platform.value.loading = false;
}

/** 上下文就绪/切换后并行重取四个 tab（各自独立，一个失败不影响其它） */
async function loadAll(): Promise<void> {
  await Promise.allSettled([loadRegistry(), loadGateway(), loadMcp(), loadPlatform()]);
}

// 上下文（项目/环境）就绪后首轮取数，之后切换自动重取
const { reload } = useContextReload(() => { void loadAll(); }, stardustContext);
</script>

<template>
  <div data-spec="mesh-page">
    <PageHead
      eyebrow="治理 · 低频但重要，一次配置长期受益"
      title="治理"
      sub="注册中心、网关、MCP 与平台设置都在这里。日常你不需要打开它。"
    >
      <template #actions>
        <Btn
          size="sm"
          :disabled="anyLoading"
          data-spec="mesh-refresh"
          @click="reload"
        >
          <template #icon>
            <span :class="anyLoading ? 'inline-flex animate-spin' : 'inline-flex'">
              <KIcon
                name="Refresh"
                :size="13"
              />
            </span>
          </template>
          {{ anyLoading ? '刷新中…' : '刷新' }}
        </Btn>
      </template>
    </PageHead>

    <div
      role="tablist"
      data-spec="mesh-tabs"
      class="mb-5 mt-4 flex flex-wrap gap-[2px] border-b border-[var(--el-border-color-light)]"
    >
      <button
        v-for="t in TABS"
        :key="t.key"
        type="button"
        role="tab"
        data-spec="mesh-tab"
        :data-key="t.key"
        :aria-selected="t.key === active"
        :class="[
          '-mb-px inline-flex h-[34px] cursor-pointer items-center rounded-t-[6px] border-0 border-b-2 border-solid px-[13px] text-[13px] font-medium outline-none transition-colors focus-visible:bg-[var(--el-fill-color-light)]',
          t.key === active
            ? 'border-b-[var(--el-color-primary)] bg-[color-mix(in_oklab,var(--el-color-primary)_10%,transparent)] text-[var(--el-color-primary-light-3)]'
            : 'border-b-transparent bg-transparent text-[var(--el-text-color-secondary)] hover:bg-[var(--el-fill-color-light)] hover:text-[var(--el-text-color-primary)]',
        ]"
        @click="active = t.key"
      >
        {{ t.label }}
      </button>
    </div>

    <RegistryTab
      v-show="active === 'registry'"
      :rows="registry.rows"
      :loading="registry.loading"
      :error="registry.error"
      @retry="loadRegistry"
    />
    <GatewayTab
      v-show="active === 'gateway'"
      :rows="gateway.rows"
      :loading="gateway.loading"
      :error="gateway.error"
      @retry="loadGateway"
    />
    <McpTab
      v-show="active === 'mcp'"
      :tokens="mcp.data.tokens"
      :resources="mcp.data.resources"
      :audits="mcp.data.audits"
      :projects="mcp.data.projects"
      :users="mcp.data.users"
      :nodes="mcp.data.nodes"
      :apps="mcp.data.apps"
      :servers="mcp.servers"
      :servers-error="mcp.serversError"
      :loading="mcp.loading"
      :error="mcp.error"
      @retry="loadMcp"
      @changed="loadMcp"
    />
    <PlatformTab
      v-show="active === 'platform'"
      :tiles="platform.rows"
      :members="platform.members"
      :project-info="platform.projectInfo"
      :project-id="stardustContext.projectId"
      :loading="platform.loading"
      :error="platform.error"
      @retry="loadPlatform"
    />
  </div>
</template>
