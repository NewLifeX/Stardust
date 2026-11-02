<script setup lang="ts">
/**
 * 网关中心 GatewayCenter（第六阶段 · 方案 §16）
 *
 * 信息架构（§16.1）：网关 → 集群 / 路由 / 节点 三 Tab。
 *  - 集群 Tab（§16.2）：集群选择 + 节点表格（节点 / 状态 / QPS / 健康）
 *  - 路由 Tab（§16.3）：搜索 + 表格 + 详情 Drawer（路由路径 + 目标列表 + 负载策略/健康检查/TLS/WebSocket）
 *  - 节点 Tab：全部节点表格（节点名 / IP / 状态 / QPS / 健康 / 所属集群）
 *
 * 数据：/Gateway/GatewayCluster|GatewayRoute|GatewayNode。
 * QPS 实体无实时字段 → 列显示「—」（待接入）；上下文联动 watch stardustContext.revision。
 * 纪律：model 层已 try-catch 兜底空数组，本页不弹全局错误 ElMessage；节点禁止裸缩写。
 */
import { ref, computed, onMounted, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { request } from '@newlifex/cube-vue/core/utils/request';
import { stardustContext } from '@/stores/stardustContext';
import StatusPill from '@/layouts/StardustLayout/components/StatusPill.vue';
import EmptyState from '@/layouts/StardustLayout/components/EmptyState.vue';
import * as m from './gateway.model';

// ===== A20 设计稿对齐：Tab 计数（设计稿 Tabs items 带 count）=====
function tabCount(k: string): number {
  if (k === 'route') return routes.value.length;
  if (k === 'node') return nodes.value.length;
  return clusters.value.length;
}

// 集群卡片：节点数（真实数据派生）
function clusterNodeCount(clusterId: number): number {
  return nodes.value.filter((n) => n.clusterId === clusterId).length;
}

const route = useRoute();
const router = useRouter();

// ===== Tab 切换（与可观测中心一致）=====
type TabKey = 'cluster' | 'route' | 'node';
const tabs: { key: TabKey; label: string }[] = [
  { key: 'cluster', label: '集群' },
  { key: 'route', label: '路由' },
  { key: 'node', label: '节点' },
];
const tab = ref<TabKey>(((route.query.tab as TabKey) || 'cluster') as TabKey);

function switchTab(k: TabKey) {
  tab.value = k;
  void router.replace({ query: { ...route.query, tab: k } });
}
watch(
  () => route.query.tab as TabKey | undefined,
  (k) => {
    if (k && k !== tab.value) tab.value = k;
  },
);

// ===== 数据 =====
const clusters = ref<m.ClusterRow[]>([]);
const nodes = ref<m.NodeRow[]>([]);
const routes = ref<m.RouteRow[]>([]);
const loading = ref(false);

const clusterMap = computed(() => new Map(clusters.value.map((c) => [c.id, c])));

async function reload() {
  loading.value = true;
  try {
    const [c, n, r] = await Promise.all([
      m.fetchClusters(request),
      m.fetchNodes(request),
      m.fetchRoutes(request),
    ]);
    clusters.value = c;
    nodes.value = n;
    routes.value = r;
  } catch {
    // model 已兜底，这里仅防御
  } finally {
    loading.value = false;
  }
}

// ===== 集群 Tab =====
const selectedClusterId = ref<number>(0);
const activeCluster = computed(() => clusters.value.find((c) => c.id === selectedClusterId.value) ?? clusters.value[0]);
const clusterNodes = computed(() => nodes.value.filter((n) => n.clusterId === (activeCluster.value?.id ?? -1)));

// ===== 路由 Tab =====
const routeKeyword = ref('');
const routeStatusFilter = ref<'all' | 'running' | 'offline'>('all');
const routeMethodFilter = ref<'all' | 'GET' | 'POST'>('all');
// 方法筛选：真实字段 methods（如 'GET,POST'），大小写无关包含匹配
const routesByMethod = computed(() =>
  routeMethodFilter.value === 'all'
    ? filteredRoutes.value
    : filteredRoutes.value.filter((r) => String(r.methods ?? '').toUpperCase().indexOf(String(routeMethodFilter.value)) >= 0),
);
const filteredRoutes = computed(() => {
  const kw = routeKeyword.value.trim().toLowerCase();
  return routes.value.filter((r) => {
    if (routeStatusFilter.value !== 'all' && m.routeStatus(r) !== routeStatusFilter.value) return false;
    if (!kw) return true;
    return (
      r.path.toLowerCase().includes(kw) ||
      r.name.toLowerCase().includes(kw) ||
      r.clusterName.toLowerCase().includes(kw)
    );
  });
});

// 路由详情 Drawer
const routeDrawerVisible = ref(false);
const currentRoute = ref<m.RouteRow | null>(null);
const currentRouteCluster = computed(() => (currentRoute.value ? clusterMap.value.get(currentRoute.value.clusterId) : undefined));
const currentRouteNodes = computed(() =>
  currentRoute.value ? nodes.value.filter((n) => n.clusterId === currentRoute.value!.clusterId) : [],
);

function openRoute(row: m.RouteRow) {
  currentRoute.value = row;
  routeDrawerVisible.value = true;
}

// ===== 生命周期 + 上下文联动 =====
onMounted(reload);
watch(
  () => stardustContext.revision,
  () => void reload(),
);

/** A6：空态图标（Lucide 线性 path，stroke 1.5） */
const ICON_EMPTY = 'M3 7h18M3 12h18M3 17h10';

</script>

<template>
  <div class="flex h-full flex-col gap-5">
    <!-- 页头 -->
    <div class="flex flex-col gap-1">
      <h1 class="text-xl font-semibold text-[var(--el-text-color-primary)]">
        网关中心
      </h1>
      <p class="text-xs text-[var(--el-text-color-regular)]">
        集群 · 路由 · 节点 — 流量入口的完整视图
      </p>
    </div>

    <!-- 页头动作（设计稿 PageHeader actions） -->
    <div
      class="flex items-center justify-end"
      data-spec="gw-header-actions"
    >
      <button
        class="gw-btn primary"
        type="button"
        data-spec="gw-new-route"
      >
        <svg
          width="12"
          height="12"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="1.5"
          stroke-linecap="round"
          stroke-linejoin="round"
        >
          <path d="M12 5v14M5 12h14" />
        </svg>
        新增路由
      </button>
    </div>

    <!-- Tab 栏 -->
    <div class="sd-tabs">
      <button
        v-for="t in tabs"
        :key="t.key"
        class="sd-tab"
        :class="{ 'sd-tab-active': tab === t.key }"
        @click="switchTab(t.key)"
      >
        {{ t.label }} <span
          class="gw-tab-count"
          data-spec="gw-tab-count"
        >{{ tabCount(t.key) }}</span>
      </button>
    </div>

    <!-- ===== 集群 Tab（§16.2）===== -->
    <div
      v-if="tab === 'cluster'"
      class="flex flex-col gap-4"
    >
      <!-- 集群卡片（设计稿 three-col：状态 + region tag + 名称 + N 节点 · QPS + 详情/路由） -->
      <div
        class="gw-cluster-cards"
        data-spec="gw-cluster-cards"
      >
        <section
          v-for="c in clusters"
          :key="c.id"
          class="gw-card"
          data-spec="gw-cluster-card"
        >
          <div class="gw-card-top">
            <StatusPill :status="c.enable ? 'running' : 'offline'" />
            <span
              class="gw-tag brand"
              data-spec="gw-cluster-region"
            >{{ c.projectName || '—' }}</span>
          </div>
          <div
            class="gw-card-name"
            data-spec="gw-cluster-name"
          >
            {{ c.name }}
          </div>
          <div
            class="gw-card-sub"
            data-spec="gw-cluster-sub"
          >
            {{ clusterNodeCount(c.id) }} 网关节点 · 负载策略 {{ m.loadBalanceLabel(c.loadBalance) }} · QPS 待接入
          </div>
          <div class="gw-card-actions">
            <button
              class="gw-btn sm"
              type="button"
            >
              详情
            </button>
            <span class="gw-spacer" />
            <button
              class="gw-btn sm ghost"
              type="button"
            >
              路由
            </button>
          </div>
        </section>
        <div
          v-if="!clusters.length"
          class="gw-empty"
          data-spec="gw-cluster-empty"
        >
          暂无网关集群数据
        </div>
      </div>
    </div>

    <!-- ===== 路由 Tab（§16.3）===== -->
    <div
      v-else-if="tab === 'route'"
      class="flex flex-col gap-4"
    >
      <!-- 搜索条 -->
      <div class="flex flex-wrap items-center gap-3">
        <el-input
          v-model="routeKeyword"
          placeholder="搜索路由路径 / 名称 / 目标集群"
          clearable
          size="small"
          class="w-60"
        />
        <div class="flex items-center gap-1">
          <button
            class="rounded-[18px] px-3.5 py-[7px] text-[13px] transition"
            :class="routeStatusFilter === 'all' ? 'bg-[var(--el-color-primary)] text-white' : 'text-[var(--el-text-color-regular)] hover:bg-[var(--el-bg-color-overlay)]'"
            @click="routeStatusFilter = 'all'"
          >
            全部
          </button>
          <button
            class="rounded-[18px] px-3.5 py-[7px] text-[13px] transition"
            :class="routeStatusFilter === 'running' ? 'bg-[var(--el-color-primary)] text-white' : 'text-[var(--el-text-color-regular)] hover:bg-[var(--el-bg-color-overlay)]'"
            @click="routeStatusFilter = 'running'"
          >
            已启用
          </button>
          <button
            class="rounded-[18px] px-3.5 py-[7px] text-[13px] transition"
            :class="routeStatusFilter === 'offline' ? 'bg-[var(--el-color-primary)] text-white' : 'text-[var(--el-text-color-regular)] hover:bg-[var(--el-bg-color-overlay)]'"
            @click="routeStatusFilter = 'offline'"
          >
            已停用
          </button>
        </div>
      </div>

      <!-- 方法筛选（设计稿 filter-bar method select） -->
      <div
        class="flex flex-wrap items-center gap-2"
        data-spec="gw-routes-bar"
      >
        <el-select
          v-model="routeMethodFilter"
          size="small"
          class="w-32"
          data-spec="gw-method-select"
        >
          <el-option
            label="method: 全部"
            value="all"
          />
          <el-option
            label="GET"
            value="GET"
          />
          <el-option
            label="POST"
            value="POST"
          />
        </el-select>
      </div>

      <!-- 路由表格 -->
      <el-table
        v-loading="loading"
        :data="routesByMethod"
        class="sd-table gw-routes-table"
        @row-click="openRoute"
      >
        <el-table-column
          label="路由路径"
          min-width="200"
        >
          <template #default="{ row }">
            <span class="font-mono text-[var(--el-text-color-primary)]">{{ (row as m.RouteRow).path || (row as m.RouteRow).name }}</span>
          </template>
        </el-table-column>
        <el-table-column
          label="目标服务"
          min-width="160"
        >
          <template #default="{ row }">
            <span class="text-[var(--el-text-color-regular)]">{{ (row as m.RouteRow).clusterName || '—' }}</span>
          </template>
        </el-table-column>
        <el-table-column
          label="域名"
          min-width="150"
        >
          <template #default="{ row }">
            <span class="text-xs text-[var(--el-text-color-regular)]">{{ (row as m.RouteRow).domain || '—' }}</span>
          </template>
        </el-table-column>
        <el-table-column
          label="状态"
          width="90"
        >
          <template #default="{ row }">
            <StatusPill :status="m.routeStatus(row as m.RouteRow)" />
          </template>
        </el-table-column>
        <el-table-column
          label="操作"
          width="80"
          fixed="right"
        >
          <template #default="{ row }">
            <el-link
              type="primary"
              @click.stop="openRoute(row as m.RouteRow)"
            >
              详情
            </el-link>
          </template>
        </el-table-column>
        <el-table-column
          label="方法"
          width="96"
        >
          <template #default="{ row }">
            <span
              class="gw-tag cyan"
              data-spec="gw-route-method"
            >{{ (row as m.RouteRow).methods || '全部' }}</span>
          </template>
        </el-table-column>
        <el-table-column
          label="QPS"
          width="96"
          align="right"
        >
          <template #default>
            <span
              class="gw-dim gw-mono"
              data-spec="gw-route-qps"
            >—</span>
          </template>
        </el-table-column>
        <el-table-column
          label="权重"
          width="80"
          align="right"
        >
          <template #default>
            <span
              class="gw-dim gw-mono"
              data-spec="gw-route-weight"
            >—</span>
          </template>
        </el-table-column>
        <el-table-column
          label="插件"
          width="150"
        >
          <template #default>
            <span
              class="gw-dim"
              data-spec="gw-route-plugin"
            >待接入</span>
          </template>
        </el-table-column>
        <template #empty>
          <EmptyState
            :icon="ICON_EMPTY"
            title="暂无网关路由"
          />
        </template>
      </el-table>
    </div>

    <!-- ===== 节点 Tab ===== -->
    <div
      v-else
      class="flex flex-col gap-4"
    >
      <el-table
        v-loading="loading"
        :data="nodes"
        class="sd-table gw-nodes-table"
      >
        <el-table-column
          label="节点名"
          min-width="150"
        >
          <template #default="{ row }">
            <span class="text-[var(--el-text-color-primary)]">{{ (row as m.NodeRow).name || (row as m.NodeRow).address }}</span>
          </template>
        </el-table-column>
        <el-table-column
          label="IP / 地址"
          min-width="200"
        >
          <template #default="{ row }">
            <span class="font-mono text-xs text-[var(--el-text-color-regular)]">{{ (row as m.NodeRow).address }}</span>
          </template>
        </el-table-column>
        <el-table-column
          label="所属集群"
          min-width="140"
        >
          <template #default="{ row }">
            <span class="text-[var(--el-text-color-regular)]">{{ (row as m.NodeRow).clusterName || '—' }}</span>
          </template>
        </el-table-column>
        <el-table-column
          label="状态"
          width="90"
        >
          <template #default="{ row }">
            <StatusPill :status="m.nodeStatus(row as m.NodeRow)" />
          </template>
        </el-table-column>
        <el-table-column
          label="QPS"
          width="90"
          align="right"
        >
          <template #default>
            <span class="text-xs text-[var(--el-text-color-placeholder)]">—</span>
          </template>
        </el-table-column>
        <el-table-column
          label="健康"
          width="90"
        >
          <template #default="{ row }">
            <span class="text-[var(--el-text-color-regular)]">{{ (row as m.NodeRow).isHealthy ? '健康' : '异常' }}</span>
          </template>
        </el-table-column>
        <el-table-column
          label="连接"
          width="96"
          align="right"
        >
          <template #default>
            <span
              class="gw-dim gw-mono"
              data-spec="gw-node-conn"
            >—</span>
          </template>
        </el-table-column>
        <el-table-column
          label="CPU"
          width="80"
          align="right"
        >
          <template #default>
            <span
              class="gw-dim gw-mono"
              data-spec="gw-node-cpu"
            >—</span>
          </template>
        </el-table-column>
        <el-table-column
          label="内存"
          width="80"
          align="right"
        >
          <template #default>
            <span
              class="gw-dim gw-mono"
              data-spec="gw-node-mem"
            >—</span>
          </template>
        </el-table-column>
        <template #empty>
          <EmptyState
            :icon="ICON_EMPTY"
            title="暂无网关节点"
          />
        </template>
      </el-table>
    </div>

    <!-- ===== 路由详情 Drawer（§16.3）===== -->
    <el-drawer
      v-model="routeDrawerVisible"
      size="640px"
      destroy-on-close
      class="stardust-drawer"
    >
      <template #header>
        <div class="flex min-w-0 flex-1 items-center justify-between gap-3">
          <div class="flex min-w-0 flex-col gap-1">
            <span class="truncate text-lg font-bold text-[var(--el-text-color-primary)]">
              {{ currentRoute?.path || currentRoute?.name || '路由详情' }}
            </span>
            <span class="text-xs text-[var(--el-text-color-secondary)]">{{ currentRoute?.domain || '全部域名' }}</span>
          </div>
          <button
            class="sd-close"
            aria-label="关闭"
            @click="routeDrawerVisible = false"
          >
            ×
          </button>
        </div>
      </template>

      <div
        v-if="currentRoute"
        class="flex flex-col gap-5"
      >
        <!-- 路由基本信息 -->
        <div>
          <div class="sd-section-title">
            路由配置
          </div>
          <div class="sd-overview-grid">
            <div class="sd-overview-item">
              <span class="sd-overview-label">路由路径</span>
              <span class="sd-overview-value font-mono">{{ currentRoute.path || '—' }}</span>
            </div>
            <div class="sd-overview-item">
              <span class="sd-overview-label">域名匹配</span>
              <span class="sd-overview-value">{{ currentRoute.domain || '全部' }}</span>
            </div>
            <div class="sd-overview-item">
              <span class="sd-overview-label">HTTP 方法</span>
              <span class="sd-overview-value">{{ currentRoute.methods || '全部' }}</span>
            </div>
            <div class="sd-overview-item">
              <span class="sd-overview-label">优先级</span>
              <span class="sd-overview-value">{{ currentRoute.priority }}</span>
            </div>
            <div class="sd-overview-item">
              <span class="sd-overview-label">负载策略</span>
              <span class="sd-overview-value">{{ m.loadBalanceLabel(currentRouteCluster?.loadBalance ?? '') }}</span>
            </div>
            <div class="sd-overview-item">
              <span class="sd-overview-label">WebSocket</span>
              <span class="sd-overview-value">{{ m.yn(currentRoute.webSocket) }}</span>
            </div>
            <div class="sd-overview-item">
              <span class="sd-overview-label">健康检查</span>
              <span class="sd-overview-value">{{ currentRouteCluster?.healthPath || '—' }}</span>
            </div>
            <div class="sd-overview-item">
              <span class="sd-overview-label">TLS 终端</span>
              <span class="sd-overview-value text-[var(--el-text-color-secondary)]">—（待接入）</span>
            </div>
          </div>
        </div>

        <!-- 目标列表（后端实例） -->
        <div>
          <div class="sd-section-title">
            目标列表（后端实例：{{ currentRouteNodes.length }}）
          </div>
          <div
            v-if="currentRouteNodes.length"
            class="sd-ref-list"
          >
            <div
              v-for="n in currentRouteNodes"
              :key="n.id"
              class="sd-ref-row"
            >
              <span class="text-[var(--el-text-color-primary)]">{{ n.name || n.address }}</span>
              <span class="flex items-center gap-2">
                <span class="font-mono text-xs text-[var(--el-text-color-secondary)]">{{ n.address }}</span>
                <StatusPill :status="m.nodeStatus(n)" />
              </span>
            </div>
          </div>
          <EmptyState
            :icon="ICON_EMPTY"
            title="该路由暂无后端节点"
          />
        </div>
      </div>
      <EmptyState
        :icon="ICON_EMPTY"
        title="暂无路由详情"
      />
    </el-drawer>
  </div>
</template>

<style scoped>
.sd-tabs {
  position: relative;
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  border-bottom: 1px solid var(--el-border-color-light);
}
.sd-tab {
  padding: 8px 14px;
  font-size: 13px;
  line-height: 1.4;
  color: var(--el-text-color-regular);
  transition: color 0.2s;
}
.sd-tab:hover {
  color: var(--el-text-color-primary);
}
.sd-tab-active {
  color: var(--el-color-primary);
  font-weight: 600;
}
.sd-tab-active::after {
  content: '';
  position: absolute;
  right: 14px;
  bottom: -1px;
  left: 14px;
  height: 2px;
  border-radius: 2px 2px 0 0;
  background: var(--el-color-primary);
}

.sd-table {
  --el-table-border-color: var(--el-border-color-lighter);
  --el-table-header-bg-color: color-mix(in srgb, var(--el-bg-color-overlay) 60%, transparent);
  --el-table-tr-bg-color: color-mix(in srgb, var(--el-bg-color) 55%, transparent);
  --el-table-row-hover-bg-color: color-mix(in srgb, var(--el-color-primary) 10%, transparent);
  --el-table-bg-color: transparent;
  border-radius: 12px;
  overflow: hidden;
}
.sd-table :deep(.el-table__inner-wrapper::before) {
  display: none;
}
.sd-table :deep(th.el-table__cell) {
  background: color-mix(in srgb, var(--el-bg-color-overlay) 60%, transparent);
  color: var(--el-text-color-secondary);
  font-weight: 600;
}
.sd-table :deep(td.el-table__cell) {
  border-bottom-color: var(--el-border-color-lighter);
  cursor: pointer;
}

.sd-cluster-summary {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 12px;
  padding: 12px 14px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 12px;
  background: color-mix(in srgb, var(--el-bg-color) 70%, transparent);
}

.stardust-drawer :deep(.el-drawer__header) {
  margin-bottom: 0;
  padding: 16px 20px;
  border-bottom: 1px solid var(--el-border-color-light);
}
.stardust-drawer :deep(.el-drawer__body) {
  padding: 20px;
  background: color-mix(in srgb, var(--el-bg-color) 92%, transparent);
}
.stardust-drawer :deep(.el-drawer__close-btn) {
  display: none;
}
.sd-close {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border-radius: 50%;
  font-size: 18px;
  line-height: 1;
  color: var(--el-text-color-regular);
  transition: background-color 0.2s, color 0.2s;
}
.sd-close:hover {
  background: var(--el-fill-color-light);
  color: var(--el-text-color-primary);
}

.sd-section-title {
  margin-bottom: 8px;
  font-size: 13px;
  font-weight: 600;
  color: var(--el-text-color-regular);
}
.sd-overview-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 14px 12px;
}
.sd-overview-item {
  display: flex;
  flex-direction: column;
  gap: 5px;
}
.sd-overview-label {
  font-size: 11px;
  color: var(--el-text-color-secondary);
}
.sd-overview-value {
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.sd-ref-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.sd-ref-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 8px 12px;
  border-radius: 8px;
  background: color-mix(in srgb, var(--el-text-color-primary) 4%, transparent);
  font-size: 13px;
}
/* A20 网关屏（设计稿 page-gateway.jsx） */
.gw-tab-count {
  margin-left: 5px;
  padding: 1px 6px;
  border-radius: 999px;
  font-size: 10px;
  font-variant-numeric: tabular-nums;
  color: var(--el-text-color-secondary);
  background: var(--el-fill-color-light);
}
.gw-cluster-cards {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 16px;
}
.gw-card {
  padding: 16px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 12px;
  background: var(--el-bg-color-overlay);
}
.gw-card-top {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}
.gw-card-name {
  font-size: 16px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin-bottom: 4px;
}
.gw-card-sub {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  margin-bottom: 12px;
}
.gw-card-actions {
  display: flex;
  align-items: center;
  gap: 6px;
}
.gw-spacer { flex: 1; }
.gw-tag {
  display: inline-block;
  padding: 1px 7px;
  border-radius: 4px;
  font-size: 10px;
  border: 1px solid var(--el-border-color);
  color: var(--el-text-color-regular);
}
.gw-tag.cyan {
  color: var(--el-color-info);
  border-color: var(--el-color-info-light-5);
  background: var(--el-color-info-light-9);
  font-family: 'JetBrains Mono', ui-monospace, monospace;
}
.gw-tag.brand {
  color: var(--el-color-primary);
  border-color: var(--el-color-primary-light-5);
  background: var(--el-color-primary-light-9);
}
.gw-btn {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  height: 28px;
  padding: 0 12px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  background: var(--el-bg-color);
  color: var(--el-text-color-regular);
  font-size: 12px;
  cursor: pointer;
  transition: all 0.2s cubic-bezier(0.16, 1, 0.3, 1);
}
.gw-btn:hover {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
}
.gw-btn.primary {
  border-color: var(--el-color-primary);
  background: var(--el-color-primary);
  color: var(--el-color-white);
}
.gw-btn.sm { height: 24px; padding: 0 10px; font-size: 11px; }
.gw-btn.ghost {
  border-color: transparent;
  color: var(--el-text-color-secondary);
}
.gw-dim { color: var(--el-text-color-secondary); }
.gw-mono { font-family: 'JetBrains Mono', ui-monospace, monospace; }
.gw-empty {
  grid-column: 1 / -1;
  padding: 32px;
  text-align: center;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  border: 1px dashed var(--el-border-color-lighter);
  border-radius: 12px;
}

</style>
