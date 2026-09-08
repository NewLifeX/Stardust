<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import GlassCard from '@/layouts/StardustLayout/components/GlassCard.vue';
import StatusPill from '@/layouts/StardustLayout/components/StatusPill.vue';
import KpiStat from '@/layouts/StardustLayout/components/KpiStat.vue';
import {
  mapNodes, aggregateStats, filterNodes, statusBorder, statusLabel, statusColor, overviewInfo,
  rings, historyRows, commandRows, perfRows, frameworkRows, appOnlineRows, appDeployRows,
  serviceRows, drawerTabs, bannerFor, pillStyleFor, commandPill, servicePill, lineSegs, buildOnlineIndex,
  type NodeView, type NodeRow, type AppOnlineRow, type PerfPoint,
} from './nodes.model';

const list = ref<NodeView[]>([]); const filter = ref('全部'); const keyword = ref('');
const current = ref<NodeView | null>(null); const drawerVisible = ref(false); const activeTab = ref('overview');
const onlineRows = ref<AppOnlineRow[]>([]); const perfData = ref<PerfPoint[]>([]); const perfLoading = ref(false);
const stats = computed(() => aggregateStats(list.value));
const filtered = computed(() => filterNodes(list.value, filter.value, keyword.value));
const detail = computed<NodeRow>(() => current.value?.raw ?? {});
const banner = computed(() => bannerFor(current.value?.status ?? 'offline'));
const hasPerf = computed(() => perfData.value.some((p) => p.cpu != null || p.memory != null));

function showDetail(row: NodeView) { current.value = row; drawerVisible.value = true; activeTab.value = 'overview'; loadPerf(row.name); }
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
        label="在线"
        :value="stats.online"
        accent="success"
      />
      <KpiStat
        label="告警"
        :value="stats.alarm"
        accent="warning"
      />
      <KpiStat
        label="离线"
        :value="stats.offline"
        accent="danger"
      />
    </div>

    <!-- 过滤 -->
    <div class="flex items-center gap-2">
      <button
        v-for="f in ['全部', '在线', '告警', '离线']"
        :key="f"
        class="rounded-[18px] px-3.5 py-[7px] text-[13px] transition"
        data-spec="chip"
        :class="filter === f ? 'bg-[var(--el-color-primary)] text-white' : 'text-[var(--el-text-color-regular)] hover:bg-[var(--el-bg-color-overlay)]'"
        @click="filter = f"
      >
        {{ f }} {{ f === '全部' ? stats.total : f === '在线' ? stats.online : f === '告警' ? stats.alarm : stats.offline }}
      </button>
    </div>

    <!-- 卡片网格 -->
    <div class="grid grid-cols-3 gap-4">
      <GlassCard
        v-for="row in filtered"
        :key="row.id"
        class="relative cursor-pointer border-l-4 p-4 transition hover:-translate-y-0.5 hover:bg-[var(--el-bg-color-overlay)]/60"
        :class="statusBorder[row.status]"
        @click="showDetail(row)"
      >
        <div class="flex items-start justify-between">
          <div>
            <div
              data-spec="ncard-name"
              class="text-[15px] font-semibold text-[var(--el-text-color-primary)]"
            >
              {{ row.name }}
            </div>
            <div class="mt-0.5 text-xs text-[var(--el-text-color-placeholder)]">
              {{ row.ip }}
            </div>
            <div class="mt-1 text-[10px] text-[var(--el-text-color-placeholder)]">
              {{ row.cluster }}
            </div>
          </div>
          <StatusPill :status="statusLabel[row.status]" />
        </div>
        <div class="mt-4 flex items-center gap-6 text-xs">
          <div>
            <div class="text-[10px] text-[var(--el-text-color-placeholder)]">
              CPU
            </div>
            <div
              class="font-medium tabular-nums"
              :style="{ color: statusColor(row.status) }"
            >
              {{ row.cpu != null ? row.cpu + '%' : '—' }}
            </div>
          </div>
          <div>
            <div class="text-[10px] text-[var(--el-text-color-placeholder)]">
              内存
            </div>
            <div
              class="font-medium tabular-nums"
              :style="{ color: statusColor(row.status) }"
            >
              {{ row.memory != null ? row.memory + '%' : '—' }}
            </div>
          </div>
          <div>
            <div class="text-[10px] text-[var(--el-text-color-placeholder)]">
              服务
            </div>
            <div class="font-medium tabular-nums text-[var(--el-text-color-primary)]">
              {{ row.services }}
            </div>
          </div>
        </div>
      </GlassCard>
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
      <div class="flex flex-col gap-4 p-5">
        <!-- ① 状态横幅三态（规格 03 §5：12% 底 / 30% 边 / 同色字；替掉 StatusPill 行） -->
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
