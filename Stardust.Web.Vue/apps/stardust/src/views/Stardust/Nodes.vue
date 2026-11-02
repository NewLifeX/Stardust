<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import { useRouter } from 'vue-router';
import { request } from '@newlifex/cube-vue/core/utils/request';
import { stardustContext } from '@/stores/stardustContext';
import StatusPill from '@/layouts/StardustLayout/components/StatusPill.vue';
import KpiStat from '@/layouts/StardustLayout/components/KpiStat.vue';
import FilterChips from '@/layouts/StardustLayout/components/FilterChips.vue';
import EmptyState from '@/layouts/StardustLayout/components/EmptyState.vue';
import {
  mapNodes, aggregateStats, filterNodes, clusterOptions, statusColor, overviewInfo,
  rings, historyRows, commandRows, perfRows, frameworkRows, appOnlineRows, appDeployRows,
  serviceRows, drawerTabs, bannerFor, pillStyleFor, commandPill, servicePill, lineSegs, buildOnlineIndex,
  type NodeView, type NodeRow, type AppOnlineRow, type PerfPoint,
} from './nodes.model';

const router = useRouter();
const list = ref<NodeView[]>([]); const filter = ref('全部'); const keyword = ref('');
/** P2-2: 集群/分类筛选 */
const clusterFilter = ref('全部');
const current = ref<NodeView | null>(null); const drawerVisible = ref(false); const activeTab = ref('overview');
const onlineRows = ref<AppOnlineRow[]>([]); const perfData = ref<PerfPoint[]>([]); const perfLoading = ref(false);
const stats = computed(() => aggregateStats(list.value));
const filtered = computed(() => filterNodes(list.value, filter.value, keyword.value, clusterFilter.value));
const clusters = computed(() => clusterOptions(list.value));
const detail = computed<NodeRow>(() => current.value?.raw ?? {});
const banner = computed(() => bannerFor(current.value?.status ?? 'offline'));
const hasPerf = computed(() => perfData.value.some((p) => p.cpu != null || p.memory != null));

function showDetail(row: NodeView) { current.value = row; drawerVisible.value = true; activeTab.value = 'overview'; loadPerf(row.name); }
/** P1-3: 跳转节点完整详情页 */
function goNodeDetail(id: number | undefined) {
  if (id == null) return;
  drawerVisible.value = false;
  void router.push(`/Nodes/Node/Detail?id=${id}`);
}
async function loadPerf(name: string) {
  if (!name) { perfData.value = []; return; }
  perfLoading.value = true;
  try {
    const r = await request({ url: '/Nodes/NodeData', method: 'GET', params: { Name: name } });
    perfData.value = perfRows(r?.data);
  } catch (e) { console.error('加载节点性能失败', e); perfData.value = []; }
  finally { perfLoading.value = false; }
}
async function load() {
  try {
    const [nr, or] = await Promise.all([
      request({ url: '/Nodes/Node', method: 'GET', params: { PageSize: 200 } }),
      request({ url: '/Registry/AppOnline', method: 'GET', params: { PageSize: 500 } }),
    ]);
    onlineRows.value = (or?.data ?? []) as AppOnlineRow[];
    const { onlineNames, counts } = buildOnlineIndex(onlineRows.value);
    list.value = mapNodes((nr?.data ?? []) as NodeRow[], onlineNames, counts, Date.now());
  } catch (e) { console.error('加载节点失败', e); }
}

onMounted(load);

/** 方案 §5.3：项目 / 环境切换后自动刷新当前页（清理失效的局部筛选：关闭详情抽屉、重置过滤条件） */
watch(
  () => stardustContext.revision,
  () => {
    drawerVisible.value = false;
    current.value = null;
    filter.value = '全部';
    keyword.value = '';
    clusterFilter.value = '全部';
    void load();
  },
);

/** A6：空态图标（Lucide 线性 path，stroke 1.5，内联 SVG） */
const ICON_SERVER = 'M4 4h16v6H4zM4 14h16v6H4z';

</script>

<template>
  <div class="flex h-full flex-col gap-5">
    <!-- 页头 -->
    <div class="flex items-start justify-between">
      <div class="flex flex-col gap-1">
        <h1 class="text-xl font-semibold text-[var(--el-text-color-primary)]">
          节点管理
        </h1>
        <p class="text-xs text-[var(--el-text-color-regular)]">
          集群节点状态 · 点击节点查看详情（含本节点提供的服务）
        </p>
      </div>
      <el-input
        v-model="keyword"
        placeholder="搜索节点 IP / 名称"
        clearable
        size="small"
        class="w-56"
      />
    </div>

    <!-- KPI（盒装卡片，对照效果图） -->
    <div class="grid grid-cols-4 gap-4">
      <KpiStat
        label="节点总数"
        :value="stats.total"
        accent="primary"
      />
      <KpiStat
        label="运行"
        :value="stats.running"
        accent="success"
      />
      <KpiStat
        label="异常"
        :value="stats.alarm"
        accent="warning"
      />
      <KpiStat
        label="离线"
        :value="stats.offline"
        accent="danger"
      />
    </div>

    <!-- 状态筛选 chips + 集群分类筛选（§9.2） -->
    <div class="flex items-center justify-between gap-4">
      <div class="flex items-center gap-2">
        <FilterChips
          v-model="filter"
          :options="[{ value: '全部', label: '全部', count: stats.total }, { value: '运行', label: '运行', count: stats.running }, { value: '异常', label: '异常', count: stats.alarm }, { value: '离线', label: '离线', count: stats.offline }]"
        />
      </div>
      <!-- P2-2: 集群/分类下拉筛选 -->
      <el-select
        v-model="clusterFilter"
        size="small"
        class="w-40"
        placeholder="集群分类"
      >
        <el-option
          v-for="c in clusters"
          :key="c"
          :label="c"
          :value="c"
        />
      </el-select>
    </div>

    <!-- 节点表格（§22.3 表格优先：节点/IP/状态/CPU/内存/应用数；点击行打开详情抽屉） -->
    <div class="flex flex-col gap-3">
      <el-table
        :data="filtered"
        data-spec="ntable"
        class="sd-node-table"
        @row-click="(row: NodeView) => showDetail(row)"
      >
        <el-table-column
          label="节点"
          min-width="220"
        >
          <template #default="{ row }">
            <div class="flex items-center gap-2.5">
              <span
                class="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg text-[13px] font-semibold"
                style="background: color-mix(in srgb, var(--el-color-primary) 15%, transparent); color: var(--el-color-primary)"
              >
                {{ String(row.name || 'N').charAt(0) }}
              </span>
              <div class="min-w-0">
                <div class="truncate text-[14px] font-semibold text-[var(--el-text-color-primary)]">
                  {{ row.name }}
                </div>
                <div class="truncate text-xs text-[var(--el-text-color-placeholder)]">
                  {{ row.cluster }}
                </div>
              </div>
            </div>
          </template>
        </el-table-column>
        <el-table-column
          label="IP"
          min-width="130"
        >
          <template #default="{ row }">
            <span class="font-mono text-xs text-[var(--el-text-color-regular)]">{{ row.ip }}</span>
          </template>
        </el-table-column>
        <el-table-column
          label="状态"
          width="96"
        >
          <template #default="{ row }">
            <StatusPill :status="row.status" />
          </template>
        </el-table-column>
        <el-table-column
          label="CPU"
          width="90"
          align="right"
        >
          <template #default="{ row }">
            <span
              class="font-medium tabular-nums"
              :style="{ color: statusColor(row.status) }"
            >{{ row.cpu != null ? row.cpu + '%' : '—' }}</span>
          </template>
        </el-table-column>
        <el-table-column
          label="内存"
          width="90"
          align="right"
        >
          <template #default="{ row }">
            <span
              class="font-medium tabular-nums"
              :style="{ color: statusColor(row.status) }"
            >{{ row.memory != null ? row.memory + '%' : '—' }}</span>
          </template>
        </el-table-column>
        <el-table-column
          label="应用数"
          width="90"
          align="right"
        >
          <template #default="{ row }">
            <span class="tabular-nums text-[var(--el-text-color-primary)]">{{ row.services }}</span>
          </template>
        </el-table-column>
        <template #empty>
          <EmptyState
            :icon="ICON_SERVER"
            title="当前项目暂无节点"
            hint="可切换项目，或部署 StarAgent 后自动接入"
          />
        </template>
      </el-table>
    </div>
    <!-- 节点详情抽屉 -->
    <el-drawer
      v-model="drawerVisible"
      :title="current?.name || '节点详情'"
      size="420px"
      data-spec="drawer"
      class="stardust-drawer"
      destroy-on-close
    >
      <template #header>
        <div class="flex min-w-0 flex-1 items-center justify-between gap-3">
          <div class="flex min-w-0 flex-col">
            <span class="truncate text-lg font-bold text-[var(--el-text-color-primary)]">
              {{ current?.name || '节点详情' }}
            </span>
            <span
              v-if="current?.cluster"
              class="truncate text-xs text-[var(--el-text-color-secondary)]"
            >
              {{ current.cluster }}
            </span>
          </div>
          <div class="flex shrink-0 items-center gap-2">
            <button
              class="rounded-lg border border-[var(--el-border-color)] px-2.5 py-1.5 text-xs text-[var(--el-text-color-regular)] transition-colors hover:border-[var(--el-color-primary)] hover:text-[var(--el-color-primary)]"
              @click="goNodeDetail(current?.id)"
            >
              查看完整详情 →
            </button>
            <button
              class="sd-close"
              aria-label="关闭"
              @click="drawerVisible = false"
            >
              ×
            </button>
          </div>
        </div>
      </template>
      <div class="flex flex-col gap-4 p-5">
        <!-- ① 状态横幅七态（规格 03 §5：12% 底 / 30% 边 / 同色字；替掉 StatusPill 行） -->
        <div
          v-if="current"
          class="flex items-center justify-between rounded-lg px-3.5 py-2.5 text-xs"
          :style="banner.style"
        >
          <span class="font-medium">{{ banner.text }}</span>
          <span class="opacity-80">{{ current?.ip || '-' }}</span>
        </div>

        <!-- ⑤ 自定义胶囊 Tab（规格 03 §5：padding 6×11 圆角 8；激活 blue20%+白字；容器 gap4 border-b pb8 mb14） -->
        <div class="mb-3.5 flex flex-wrap gap-1 border-b border-[var(--el-border-color-light)] pb-2">
          <button
            v-for="t in drawerTabs"
            :key="t.name"
            class="rounded-lg px-[11px] py-1.5 text-xs transition"
            :class="activeTab === t.name
              ? 'bg-[color-mix(in_srgb,var(--el-color-primary)_20%,transparent)] text-white'
              : 'text-[var(--el-text-color-secondary)] hover:bg-[var(--el-bg-color-overlay)]'"
            @click="activeTab = t.name"
          >
            {{ t.label }}
          </button>
        </div>

        <!-- 概览 -->
        <div
          v-if="activeTab === 'overview'"
          class="flex flex-col gap-4"
        >
          <div class="grid grid-cols-2 gap-3">
            <div
              v-for="it in overviewInfo(detail)"
              :key="it.k"
              class="flex flex-col gap-1"
            >
              <span class="text-xs text-[var(--el-text-color-placeholder)]">{{ it.k }}</span>
              <span class="text-sm font-medium text-[var(--el-text-color-primary)]">{{ it.v }}</span>
            </div>
          </div>
          <!-- ② 概览 Tab 补 ring 三环（规格 03 §5：72 圆 conic-gradient，值 15/700，CPU 绿/内存 蓝/磁盘 青） -->
          <div class="flex items-center justify-around gap-3 py-2">
            <div
              v-for="r in rings(detail)"
              :key="r.label"
              class="flex flex-col items-center gap-2"
            >
              <div
                class="flex h-[72px] w-[72px] items-center justify-center rounded-full"
                :style="{
                  background:
                    r.value != null
                      ? `conic-gradient(${r.color} ${r.value}%, var(--el-fill-color-light) 0)`
                      : 'var(--el-fill-color-light)',
                }"
              >
                <div class="flex h-[54px] w-[54px] items-center justify-center rounded-full bg-[var(--el-bg-color)] text-[15px] font-bold tabular-nums text-[var(--el-text-color-primary)]">
                  {{ r.value != null ? r.value + '%' : '—' }}
                </div>
              </div>
              <span class="text-xs text-[var(--el-text-color-regular)]">{{ r.label }}</span>
            </div>
          </div>
        </div>
        <div
          v-else-if="activeTab === 'online'"
          class="flex flex-col gap-3"
        >
          <h3 class="text-sm font-semibold text-[var(--el-text-color-primary)]">
            实时资源 · 近 1 分钟
          </h3>
          <div class="flex items-center justify-around gap-3 py-2">
            <div
              v-for="r in rings(detail)"
              :key="r.label"
              class="flex flex-col items-center gap-2"
            >
              <div
                class="flex h-[72px] w-[72px] items-center justify-center rounded-full"
                :style="{
                  background:
                    r.value != null
                      ? `conic-gradient(${r.color} ${r.value}%, var(--el-fill-color-light) 0)`
                      : 'var(--el-fill-color-light)',
                }"
              >
                <div class="flex h-[54px] w-[54px] items-center justify-center rounded-full bg-[var(--el-bg-color)] text-[15px] font-bold tabular-nums text-[var(--el-text-color-primary)]">
                  {{ r.value != null ? r.value + '%' : '—' }}
                </div>
              </div>
              <span class="text-xs text-[var(--el-text-color-regular)]">{{ r.label }}</span>
            </div>
          </div>
        </div>
        <div
          v-else-if="activeTab === 'history'"
          class="flex flex-col gap-3"
        >
          <div
            v-if="historyRows(detail).length"
            class="flex flex-col gap-3"
          >
            <div
              v-for="(ev, i) in historyRows(detail)"
              :key="i"
              class="flex items-center gap-3"
            >
              <span
                class="h-2 w-2 rounded-full"
                :class="ev.status === 'login' ? 'bg-[var(--el-color-primary)]' : 'bg-[var(--el-color-success)]'"
              />
              <span class="flex-1 text-sm text-[var(--el-text-color-primary)]">{{ ev.text }}</span>
              <span class="text-xs tabular-nums text-[var(--el-text-color-placeholder)]">{{ ev.time }}</span>
            </div>
          </div>
          <div
            v-else
            class="py-10 text-center text-sm text-[var(--el-text-color-placeholder)]"
          >
            暂无历史事件
          </div>
        </div>
        <div
          v-else-if="activeTab === 'commands'"
          class="flex flex-col gap-2"
        >
          <!-- ④ svc 行：白 4% 底 + role 药丸（规格 03 §5：药丸 10px padding 2×7 圆角 8，绿/蓝 15% 底；替掉圆点） -->
          <div
            v-for="c in commandRows()"
            :key="c.name"
            class="flex items-center gap-2.5 rounded-lg border border-[var(--el-border-color-light)] bg-[color-mix(in_srgb,var(--el-text-color-primary)_4%,transparent)] px-3 py-2.5"
          >
            <span
              class="rounded-lg px-[7px] py-0.5 text-[10px] font-medium"
              :style="pillStyleFor(commandPill[c.role].color)"
            >{{ commandPill[c.role].label }}</span>
            <span class="text-[13px] font-semibold text-[var(--el-text-color-primary)]">{{ c.name }}</span>
          </div>
        </div>
        <div
          v-else-if="activeTab === 'perf'"
          class="flex flex-col gap-3"
        >
          <!-- ③ 性能 Tab：/Nodes/NodeData（params Name=node.name）→ perfRows → 双折线 svg（绿 CPU/蓝 内存），空则空态 -->
          <div
            v-if="perfLoading"
            class="py-10 text-center text-sm text-[var(--el-text-color-placeholder)]"
          >
            加载中…
          </div>
          <template v-else>
            <div
              v-if="hasPerf"
              class="flex flex-col gap-3"
            >
              <div class="flex items-center gap-4 text-xs text-[var(--el-text-color-regular)]">
                <span class="flex items-center gap-1.5"><span class="h-2 w-2 rounded-full bg-[var(--el-color-success)]" />CPU</span>
                <span class="flex items-center gap-1.5"><span class="h-2 w-2 rounded-full bg-[var(--el-color-primary)]" />内存</span>
              </div>
              <svg
                viewBox="0 0 320 110"
                class="h-[110px] w-full"
                preserveAspectRatio="none"
              >
                <polyline
                  v-for="(s, i) in lineSegs(perfData, 'cpu')"
                  :key="'cpu-' + i"
                  :points="s"
                  fill="none"
                  stroke="var(--el-color-success)"
                  stroke-width="1.5"
                  stroke-linejoin="round"
                  stroke-linecap="round"
                />
                <polyline
                  v-for="(s, i) in lineSegs(perfData, 'memory')"
                  :key="'mem-' + i"
                  :points="s"
                  fill="none"
                  stroke="var(--el-color-primary)"
                  stroke-width="1.5"
                  stroke-linejoin="round"
                  stroke-linecap="round"
                />
              </svg>
            </div>
            <div
              v-else
              class="py-10 text-center text-sm text-[var(--el-text-color-placeholder)]"
            >
              暂无性能数据
            </div>
          </template>
        </div>
        <div
          v-else-if="activeTab === 'framework'"
          class="flex flex-col gap-2"
        >
          <!-- ④ svc 行：白 4% 底（规格 03 §5） -->
          <div
            v-for="f in frameworkRows()"
            :key="f.name"
            class="flex items-center justify-between rounded-lg border border-[var(--el-border-color-light)] bg-[color-mix(in_srgb,var(--el-text-color-primary)_4%,transparent)] px-3 py-2.5"
          >
            <span class="text-sm text-[var(--el-text-color-primary)]">{{ f.name }}</span>
            <span class="text-xs font-medium tabular-nums text-[var(--el-color-primary)]">{{ f.match }}% 匹配</span>
          </div>
        </div>
        <div
          v-else-if="activeTab === 'appOnline'"
          class="flex flex-col gap-2"
        >
          <!-- ④ svc 行：白 4% 底（规格 03 §5） -->
          <div
            v-if="appOnlineRows(onlineRows, current?.name ?? '').length"
            class="flex flex-col gap-2"
          >
            <div
              v-for="(a, i) in appOnlineRows(onlineRows, current?.name ?? '')"
              :key="i"
              class="flex items-center justify-between rounded-lg border border-[var(--el-border-color-light)] bg-[color-mix(in_srgb,var(--el-text-color-primary)_4%,transparent)] px-3 py-2.5"
            >
              <div class="flex flex-col gap-0.5">
                <span class="text-sm text-[var(--el-text-color-primary)]">{{ a.appName || a.name || '-' }}</span>
                <span class="text-xs text-[var(--el-text-color-placeholder)]">{{ a.iP || '-' }}</span>
              </div>
              <span class="text-xs tabular-nums text-[var(--el-text-color-regular)]">{{ a.version || '-' }}</span>
            </div>
          </div>
          <div
            v-else
            class="py-10 text-center text-sm text-[var(--el-text-color-placeholder)]"
          >
            暂无在线应用
          </div>
        </div>
        <div
          v-else-if="activeTab === 'appDeploy'"
          class="flex flex-col gap-2"
        >
          <!-- ④ svc 行：白 4% 底（规格 03 §5） -->
          <div
            v-if="appDeployRows().length"
            class="flex flex-col gap-2"
          >
            <div
              v-for="(d, i) in appDeployRows()"
              :key="i"
              class="flex items-center justify-between rounded-lg border border-[var(--el-border-color-light)] bg-[color-mix(in_srgb,var(--el-text-color-primary)_4%,transparent)] px-3 py-2.5"
            >
              <span class="text-sm text-[var(--el-text-color-primary)]">{{ d.appName }}</span>
              <span class="text-xs tabular-nums text-[var(--el-text-color-regular)]">{{ d.version }} · {{ d.time }}</span>
            </div>
          </div>
          <div
            v-else
            class="py-10 text-center text-sm text-[var(--el-text-color-placeholder)]"
          >
            暂无发布记录
          </div>
        </div>
        <div
          v-else-if="activeTab === 'services'"
          class="flex flex-col gap-2"
        >
          <!-- ④ svc 行：白 4% 底 + role 药丸（规格 03 §5：提供绿/消费蓝，15% 底；替掉圆点） -->
          <div
            v-if="serviceRows(detail.services).length"
            class="flex flex-col gap-2"
          >
            <div
              v-for="(s, i) in serviceRows(detail.services)"
              :key="i"
              class="flex items-center gap-2.5 rounded-lg border border-[var(--el-border-color-light)] bg-[color-mix(in_srgb,var(--el-text-color-primary)_4%,transparent)] px-3 py-2.5"
            >
              <span
                class="rounded-lg px-[7px] py-0.5 text-[10px] font-medium"
                :style="pillStyleFor(servicePill[s.role].color)"
              >{{ servicePill[s.role].label }}</span>
              <span class="flex-1 text-[13px] font-semibold text-[var(--el-text-color-primary)]">{{ s.name }}</span>
              <span
                v-if="s.meta"
                class="text-xs text-[var(--el-text-color-placeholder)]"
              >{{ s.meta }}</span>
            </div>
          </div>
          <div
            v-else
            class="py-10 text-center text-sm text-[var(--el-text-color-placeholder)]"
          >
            暂无服务
          </div>
        </div>
      </div>
    </el-drawer>
  </div>
</template>

<style scoped>
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
  transition:
    background-color 0.2s,
    color 0.2s;
}
.sd-close:hover {
  background: var(--el-fill-color-light);
  color: var(--el-text-color-primary);
}
</style>
