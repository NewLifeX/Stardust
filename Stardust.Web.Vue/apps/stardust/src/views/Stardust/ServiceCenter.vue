<script setup lang="ts">
/**
 * 服务中心 ServiceCenter（第六阶段 · 方案 §10）
 *
 * 设计目标（§10.1）：有哪些服务 / 谁提供 / 谁消费 / 当前是否健康 / 谁调用谁。
 *  - 列表（§10.2）：服务名 / 提供者数 / 消费者数 / 状态（StatusPill 七态）
 *  - 详情（§10.3）：服务名 + 健康/实例数/权重/版本/地址 + 调用方列表 + 被调用方列表 + [查看服务拓扑]
 *
 * 数据：/Registry/AppService（提供者）+ /Registry/AppConsume（消费者/下游）+ /Registry/App（中文名 join）。
 * 上下文联动：watch stardustContext.revision → 重载；cubeAxios 拦截器自动注入 projectId/environment。
 * 纪律：所有 fetch 已在 model 层 try-catch 兜底空数组，本页不弹全局错误 ElMessage；节点禁止裸缩写。
 */
import { ref, computed, onMounted, watch } from 'vue';
import { useRouter } from 'vue-router';
import { request } from '@newlifex/cube-vue/core/utils/request';
import { stardustContext } from '@/stores/stardustContext';
import StatusPill from '@/layouts/StardustLayout/components/StatusPill.vue';
import KpiStat from '@/layouts/StardustLayout/components/KpiStat.vue';
import EmptyState from '@/layouts/StardustLayout/components/EmptyState.vue';
import ServiceTopology from './ServiceTopology.vue';
import * as m from './service.model';

const router = useRouter();

// ===== 列表状态 =====
const rows = ref<m.ServiceCenterRow[]>([]);
const loading = ref(false);
const keyword = ref('');
const statusFilter = ref<'all' | m.HealthStatus>('all');

// ===== Tab：服务注册 / 服务拓扑（设计稿 page-services.jsx）=====
type TabKey = 'registry' | 'topology';
const tab = ref<TabKey>('registry');
const TABS = computed(() => [
  { key: 'registry' as TabKey, label: '服务注册', count: rows.value.length as number | undefined },
  { key: 'topology' as TabKey, label: '服务拓扑', count: undefined as number | undefined },
]);
/** 调用拓扑（由服务聚合行推导） */
const topology = computed(() => m.buildTopology(rows.value));

const filteredRows = computed(() => m.filterServices(rows.value, keyword.value, statusFilter.value));
const kpi = computed(() => m.summarizeServices(rows.value));

async function reload() {
  loading.value = true;
  try {
    rows.value = await m.fetchServiceCenter(request);
  } catch {
    rows.value = [];
  } finally {
    loading.value = false;
  }
}

// ===== 详情 Drawer（§10.3）=====
const drawerVisible = ref(false);
const current = ref<m.ServiceCenterRow | null>(null);

function openDetail(row: m.ServiceCenterRow) {
  current.value = row;
  drawerVisible.value = true;
}

function goTopology() {
  // 切到本页「服务拓扑」Tab（设计稿：拓扑为服务页内的一个视图）
  drawerVisible.value = false;
  tab.value = 'topology';
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
        服务中心
      </h1>
      <p class="text-xs text-[var(--el-text-color-regular)]">
        服务注册 · 发现 · 拓扑 — 谁提供谁消费，一目了然
      </p>
    </div>

    <!-- Tab：服务注册 / 服务拓扑 -->
    <div
      class="sd-tabs"
      data-spec="svc-tabs"
    >
      <button
        v-for="t in TABS"
        :key="t.key"
        type="button"
        class="sd-tab"
        :class="{ 'is-active': tab === t.key }"
        data-spec="svc-tab"
        @click="tab = t.key"
      >
        {{ t.label }}
        <span
          v-if="t.count !== undefined"
          class="sd-tab__count"
        >{{ t.count }}</span>
      </button>
    </div>

    <template v-if="tab === 'registry'">
      <!-- KPI 统计行 -->
      <div class="grid grid-cols-4 gap-4">
        <KpiStat
          label="服务总数"
          :value="kpi.total"
          accent="primary"
        />
        <KpiStat
          label="健康服务"
          :value="kpi.running"
          accent="success"
        />
        <KpiStat
          label="异常服务"
          :value="kpi.error"
          accent="danger"
        />
        <KpiStat
          label="离线服务"
          :value="kpi.offline"
          accent="warning"
        />
      </div>

      <!-- 搜索条：服务名/应用搜索 + 状态筛选 chips -->
      <div class="flex flex-wrap items-center gap-3">
        <el-input
          v-model="keyword"
          placeholder="搜索服务名 / 提供方应用"
          clearable
          size="small"
          class="w-64"
        />
        <div class="flex items-center gap-1">
          <button
            v-for="c in m.serviceStatusChips"
            :key="c.key"
            class="rounded-[18px] px-3.5 py-[7px] text-[13px] transition"
            :class="statusFilter === c.key ? 'bg-[var(--el-color-primary)] text-white' : 'text-[var(--el-text-color-regular)] hover:bg-[var(--el-bg-color-overlay)]'"
            @click="statusFilter = c.key"
          >
            {{ c.label }}
          </button>
        </div>
        <el-button
          size="small"
          :loading="loading"
          @click="reload"
        >
          刷新
        </el-button>
      </div>

      <!-- 服务表格（§10.2） -->
      <el-table
        v-loading="loading"
        :data="filteredRows"
        class="sd-table"
        @row-click="openDetail"
      >
        <el-table-column
          label="服务名"
          min-width="220"
        >
          <template #default="{ row }">
            <span class="font-medium text-[var(--el-text-color-primary)]">{{ m.serviceLabel((row as m.ServiceCenterRow).serviceName) }}</span>
          </template>
        </el-table-column>
        <el-table-column
          label="提供者"
          width="120"
          align="right"
        >
          <template #default="{ row }">
            <span class="tabular-nums text-[var(--el-text-color-regular)]">{{ (row as m.ServiceCenterRow).providerCount }} 个应用</span>
          </template>
        </el-table-column>
        <el-table-column
          label="消费者"
          width="120"
          align="right"
        >
          <template #default="{ row }">
            <span class="tabular-nums text-[var(--el-text-color-regular)]">{{ (row as m.ServiceCenterRow).consumerCount }} 个应用</span>
          </template>
        </el-table-column>
        <el-table-column
          label="实例数"
          width="90"
          align="right"
        >
          <template #default="{ row }">
            <span class="tabular-nums text-[var(--el-text-color-regular)]">{{ (row as m.ServiceCenterRow).instanceCount }}</span>
          </template>
        </el-table-column>
        <el-table-column
          label="状态"
          width="100"
        >
          <template #default="{ row }">
            <StatusPill :status="(row as m.ServiceCenterRow).status" />
          </template>
        </el-table-column>
        <el-table-column
          label="操作"
          width="90"
          fixed="right"
        >
          <template #default="{ row }">
            <el-link
              type="primary"
              @click.stop="openDetail(row as m.ServiceCenterRow)"
            >
              详情
            </el-link>
          </template>
        </el-table-column>
        <template #empty>
          <EmptyState
            :icon="ICON_EMPTY"
            title="暂无服务注册数据"
          />
        </template>
      </el-table>
    </template>

    <!-- 服务拓扑视图 -->
    <ServiceTopology
      v-else
      :topology="topology"
    />

    <!-- ===== 服务详情 Drawer（§10.3）===== -->
    <el-drawer
      v-model="drawerVisible"
      size="640px"
      destroy-on-close
      class="stardust-drawer"
    >
      <template #header>
        <div class="flex min-w-0 flex-1 items-center justify-between gap-3">
          <div class="flex min-w-0 flex-col gap-1">
            <span class="truncate text-lg font-bold text-[var(--el-text-color-primary)]">
              {{ current ? m.serviceLabel(current.serviceName) : '服务详情' }}
            </span>
            <span class="flex items-center gap-2">
              <StatusPill
                v-if="current"
                :status="current.status"
              />
              <span class="text-xs text-[var(--el-text-color-secondary)]">{{ current?.version ? `版本 ${current.version}` : '版本 —' }}</span>
            </span>
          </div>
          <button
            class="sd-close"
            aria-label="关闭"
            @click="drawerVisible = false"
          >
            ×
          </button>
        </div>
      </template>

      <div
        v-if="current"
        class="flex flex-col gap-5"
      >
        <!-- 基本信息区：键值对（有数据展示，无数据显示 —） -->
        <div>
          <div class="sd-section-title">
            基本信息
          </div>
          <div class="sd-overview-grid">
            <div class="sd-overview-item">
              <span class="sd-overview-label">健康状态</span>
              <StatusPill :status="current.status" />
            </div>
            <div class="sd-overview-item">
              <span class="sd-overview-label">实例数</span>
              <span class="sd-overview-value">{{ current.instanceCount }}</span>
            </div>
            <div class="sd-overview-item">
              <span class="sd-overview-label">提供者</span>
              <span class="sd-overview-value">{{ current.providerCount }} 个应用</span>
            </div>
            <div class="sd-overview-item">
              <span class="sd-overview-label">版本</span>
              <span class="sd-overview-value">{{ current.version || '—' }}</span>
            </div>
            <div class="sd-overview-item">
              <span class="sd-overview-label">对外地址</span>
              <span class="sd-overview-value break-all text-xs">{{ current.addresses.join('、') || '—' }}</span>
            </div>
          </div>
        </div>

        <!-- 提供者列表 -->
        <div>
          <div class="sd-section-title">
            提供者（谁提供）
          </div>
          <div
            v-if="current.providers.length"
            class="sd-ref-list"
          >
            <div
              v-for="p in current.providers"
              :key="p.appId"
              class="sd-ref-row"
            >
              <span class="text-[var(--el-text-color-primary)]">{{ p.label }}</span>
              <span class="font-mono text-xs text-[var(--el-text-color-secondary)]">{{ p.address || '—' }}</span>
            </div>
          </div>
          <EmptyState
            :icon="ICON_EMPTY"
            title="暂无提供者"
          />
        </div>

        <!-- 调用方列表（谁调用了本服务） -->
        <div>
          <div class="sd-section-title">
            调用方（谁消费本服务）
          </div>
          <div
            v-if="current.callers.length"
            class="sd-ref-list"
          >
            <div
              v-for="c in current.callers"
              :key="c.appId"
              class="sd-ref-row"
            >
              <span class="text-[var(--el-text-color-primary)]">{{ c.label }}</span>
              <span class="text-xs text-[var(--el-text-color-secondary)]">{{ c.address || '消费方应用' }}</span>
            </div>
          </div>
          <EmptyState
            :icon="ICON_EMPTY"
            title="暂无调用方"
          />
        </div>

        <!-- 被调用方列表（本服务又依赖哪些下游） -->
        <div>
          <div class="sd-section-title">
            被调用方（本服务依赖的下游）
          </div>
          <div
            v-if="current.callees.length"
            class="sd-ref-list"
          >
            <div
              v-for="(c, i) in current.callees"
              :key="i"
              class="sd-ref-row"
            >
              <span class="text-[var(--el-text-color-primary)]">{{ c.label }}</span>
            </div>
          </div>
          <EmptyState
            :icon="ICON_EMPTY"
            title="暂无下游依赖"
          />
        </div>

        <!-- 底部操作：查看服务拓扑 -->
        <div class="flex items-center gap-2 pt-1">
          <el-button
            type="primary"
            size="small"
            @click="goTopology"
          >
            查看服务拓扑
          </el-button>
          <el-button
            size="small"
            @click="drawerVisible = false"
          >
            关闭
          </el-button>
        </div>
      </div>
      <EmptyState
        :icon="ICON_EMPTY"
        title="暂无服务详情"
      />
    </el-drawer>
  </div>
</template>

<style scoped>
/* 表格玻璃态（与可观测中心一致） */
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

/* Drawer */
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

/* 区块标题 */
.sd-section-title {
  margin-bottom: 8px;
  font-size: 13px;
  font-weight: 600;
  color: var(--el-text-color-regular);
}

/* 概览网格 */
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

/* 引用列表（提供者/调用方/被调用方） */
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

/* Tab 条（对齐设计稿 Tabs） */
.sd-tabs {
  display: flex;
  align-items: center;
  gap: 4px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}
.sd-tab {
  position: relative;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 9px 14px;
  border: none;
  background: none;
  color: var(--el-text-color-secondary);
  font-size: 13px;
  font-weight: 500;
  white-space: nowrap;
  cursor: pointer;
  transition: color 0.12s cubic-bezier(0.16, 1, 0.3, 1);
}
.sd-tab:hover {
  color: var(--el-text-color-primary);
}
.sd-tab.is-active {
  color: var(--el-text-color-primary);
}
.sd-tab.is-active::after {
  content: '';
  position: absolute;
  left: 10px;
  right: 10px;
  bottom: -1px;
  height: 2px;
  border-radius: 999px;
  background: var(--el-color-primary);
}
.sd-tab__count {
  padding: 1px 6px;
  border-radius: 999px;
  background: var(--el-fill-color);
  color: var(--el-text-color-regular);
  font-family: var(--font-mono, ui-monospace, monospace);
  font-size: 11px;
  font-variant-numeric: tabular-nums;
}
</style>
