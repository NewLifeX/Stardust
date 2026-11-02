<script setup lang="ts">
/**
 * 星尘控制台 · 概览首页（Overview）
 * 交互排版对照 Doc/preview_home.png「运行概览」：
 *   顶部统计卡片排（系统状态 / 运行实例 / QPS / 错误率 / P99 / 未处理告警）
 *   + 中部 2×2（需要关注 / 应用运行态势 / 服务拓扑 / 最近事件）
 *   + 底部整体流量趋势(QPS) 折线图。
 * 样式纪律：颜色一律 --el-* / --sd-* 令牌；ECharts 为 canvas，经 getComputedStyle
 *   读取设计令牌（src/design-system/tokens.css 的 --sd-*）再传入，不硬编码十六进制。
 * 逻辑抽 overview.model.ts 纯函数；数据全部来自真实接口。
 */
import { ref, computed, onMounted, watch } from 'vue';
import { useRouter } from 'vue-router';
import { ElMessage } from 'element-plus';
import { request } from '@newlifex/cube-vue/core/utils/request';
import { stardustContext } from '@/stores/stardustContext';
import GlassCard from '@/layouts/StardustLayout/components/GlassCard.vue';
import KpiStat from '@/layouts/StardustLayout/components/KpiStat.vue';
import EChart from '@/components/EChart.vue';
import CoreConstellation from '@/layouts/StardustLayout/components/CoreConstellation.vue';
import {
  isNodeOnline,
  buildEvents,
  buildMinuteBuckets,
  buildSystemKpis,
  buildAttention,
  buildAppHealth,
  buildTrafficSeries,
  buildTopology,
  appMetrics,
  type KpiItem,
  type EventItem,
  type MinuteStatRow,
  type AlarmRow,
  type AttentionItem,
  type AppHealthRow,
  type TrafficPoint,
  type Topology,
  type TopoNode,
} from './overview.model';

const router = useRouter();
const loading = ref(true);
const lastUpdate = ref<Date | null>(null);

const nodes = ref<Record<string, unknown>[]>([]);
const apps = ref<Record<string, unknown>[]>([]);
/** 应用 → 在线实例数（拓扑 Inspector 用） */
const instCount = ref<Map<string, number>>(new Map());
const minuteRows = ref<MinuteStatRow[]>([]);

const kpis = ref<KpiItem[]>([]);
const attention = ref<AttentionItem[]>([]);
const appHealth = ref<AppHealthRow[]>([]);
const topology = ref<Topology>({
  center: { label: '星尘核心', sub: 'Stardust 控制台', online: true, route: '/stardust/overview' },
  nodes: [],
  edges: [],
});
const events = ref<EventItem[]>([]);
const traffic = ref<TrafficPoint[]>([]);

/** 拓扑选中节点（Inspector） */
const selectedTopo = ref<TopoNode | null>(null);
const topoMetrics = computed(() =>
  selectedTopo.value ? appMetrics(minuteRows.value, selectedTopo.value.name) : { qps: 0, errorRate: 0, p99: 0 },
);

const initialLoading = computed(() => loading.value && kpis.value.length === 0);

/**
 * 服务星座 8 个模块节点（设计稿 Overview：中心核心 + 8 卫星）
 * 与侧栏九域对齐：工作台本身是中心核心，其余 8 域作卫星环绕。
 * 徽标取真实计数（应用数 / 节点数 / 未处理告警），无数据来源的域不显示徽标。
 */
const constellationNodes = computed(() => {
  const alarmKpi = kpis.value.find((k) => k.label === '未处理告警');
  return [
    { key: 'apps', label: '应用', sub: 'Apps', path: '/stardust/apps', badge: apps.value.length || undefined, icon: 'M12 3l8 4.5v9L12 21l-8-4.5v-9z M12 12l8-4.5M12 12v9M12 12L4 7.5' },
    { key: 'nodes', label: '节点', sub: 'Nodes', path: '/stardust/nodes', badge: nodes.value.length || undefined, icon: 'M3 4h18v7H3z M3 14h18v6H3z M7 7.5h.01M7 17h.01' },
    { key: 'services', label: '服务', sub: 'Services', path: '/stardust/service', icon: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10' },
    { key: 'configs', label: '配置', sub: 'Config', path: '/stardust/config', icon: 'M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z M15 12a3 3 0 11-6 0 3 3 0 016 0z' },
    { key: 'releases', label: '发布', sub: 'Release', path: '/stardust/release', icon: 'M15.59 14.37a6 6 0 01-5.84 7.18 6 6 0 01-5.84-7.18 6 6 0 0112 0zm0 0H3.75m0 0l-.08-.83a6 6 0 0111.89-3.18M3.75 14.37l-.08.83M20 2v6m0 0l3-3m-3 3l-3-3' },
    { key: 'observability', label: '可观测', sub: 'Observe', path: '/stardust/observability', badge: alarmKpi?.value || undefined, icon: 'M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7z M12 15a3 3 0 100-6 3 3 0 000 6z' },
    { key: 'gateway', label: '网关', sub: 'Gateway', path: '/stardust/gateway', icon: 'M13 10V3L4 14h7v7l9-11h-7z' },
    { key: 'platform', label: '平台', sub: 'Platform', path: '/Platform/GalaxyProject', icon: 'M12 2l8 4v6c0 5-3.5 8-8 10-4.5-2-8-5-8-10V6z M9 12l2 2 4-4' },
  ];
});

/** 读取设计令牌 CSS 变量（ECharts canvas 不识别 var()，须桥接为实际色值） */
function cssVar(name: string, fallback = ''): string {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim() || fallback;
}

function go(route: string) {
  router.push(route);
}

/** 拓扑外围节点圆周位置：中心 (50,50)，半径随节点数调整 */
const topoNodes = computed(() => {
  const count = topology.value.nodes.length;
  const radius = count > 6 ? 30 : 36;
  return topology.value.nodes.map((node, i) => {
    const angle = -90 + (360 / count) * i;
    const rad = (angle * Math.PI) / 180;
    return { node, left: 50 + radius * Math.cos(rad), top: 50 + radius * Math.sin(rad) };
  });
});

/** 应用名 → 圆周坐标（供应用间有向依赖边定位连线端点） */
const topoPosByName = computed(() => {
  const m = new Map<string, { left: number; top: number }>();
  for (const { node, left, top } of topoNodes.value) {
    if (node.name) m.set(node.name, { left, top });
  }
  return m;
});

/** 可绘制的应用间有向边：仅两端都在圆周节点集合中时输出坐标 */
const topoEdges = computed(() =>
  (topology.value.edges ?? [])
    .map((e) => ({ from: topoPosByName.value.get(e.from), to: topoPosByName.value.get(e.to), raw: e }))
    .filter((x) => x.from && x.to)
    .map((x) => ({ x1: x.from!.left, y1: x.from!.top, x2: x.to!.left, y2: x.to!.top, key: `${x.raw.from}->${x.raw.to}` })),
);

/** [查看 Trace]：跳调用链页并按应用名筛选 */
function traceRoute(node: TopoNode): string {
  return node.name ? `/Monitors/Trace?appName=${encodeURIComponent(node.name)}` : '/Monitors/Trace';
}

async function reloadAll() {
  loading.value = true;
  try {
    // 上下文参数（projectId/environment）由全局请求拦截器自动注入，此处无需手动拼接
    const [nr, ar, or, mr, alr, crv, srv] = await Promise.all([
      request({ url: '/Nodes/Node', method: 'GET', params: { PageSize: 200 } }),
      request({ url: '/Registry/App', method: 'GET', params: { PageSize: 200 } }),
      request({ url: '/Registry/AppOnline', method: 'GET', params: { PageSize: 500 } }),
      request({ url: '/Monitors/AppMinuteStat', method: 'GET', params: { PageSize: 500 } }),
      request({ url: '/Monitors/AlarmRecord', method: 'GET', params: { PageSize: 100, status: 1 } }),
      request({ url: '/Registry/AppConsume', method: 'GET', params: { PageSize: 500 } }),
      request({ url: '/Registry/AppService', method: 'GET', params: { PageSize: 500 } }),
    ]);
    nodes.value = (nr?.data ?? []) as Record<string, unknown>[];
    apps.value = (ar?.data ?? []) as Record<string, unknown>[];
    const online = new Set<string>(
      (or?.data ?? []).map((r: any) => r.appName).filter(Boolean),
    );
    const count = new Map<string, number>();
    for (const r of or?.data ?? []) {
      if (r.appName) count.set(r.appName, (count.get(r.appName) ?? 0) + 1);
    }
    instCount.value = count;
    const minuteRowsArr = (mr?.data ?? []) as MinuteStatRow[];
    minuteRows.value = minuteRowsArr;
    const alarmRows = (alr?.data ?? []) as AlarmRow[];
    const buckets = buildMinuteBuckets(minuteRowsArr);
    const now = Date.now();
    const onlineNodes = nodes.value.filter((n) => isNodeOnline(n as any, now)).length;
    const sys = buildSystemKpis(nodes.value as any, onlineNodes, buckets, alarmRows.length);
    kpis.value = [sys.status, sys.instances, sys.qps, sys.errorRate, sys.p99, sys.alarms];
    attention.value = buildAttention(alarmRows, nodes.value as any, apps.value as any, online, now);
    appHealth.value = buildAppHealth(apps.value as any, online, alarmRows);
    const consumes = (crv?.data ?? []) as Record<string, unknown>[];
    const services = (srv?.data ?? []) as Record<string, unknown>[];
    topology.value = buildTopology(apps.value as any, online, consumes, services);
    events.value = buildEvents(nodes.value as any, apps.value as any, now);
    traffic.value = buildTrafficSeries(buckets);
    lastUpdate.value = new Date();
  } catch (e: any) {
    ElMessage.error(e?.message || '加载概览数据失败');
  } finally {
    loading.value = false;
  }
}

onMounted(reloadAll);

/** 方案 §5.3：项目 / 环境切换后自动刷新当前页（保留页面位置，不重置筛选） */
watch(
  () => stardustContext.revision,
  () => {
    selectedTopo.value = null;
    void reloadAll();
  },
);

/** 整体流量趋势折线图（颜色来自设计令牌 --sd-*） */
const trafficOption = computed<Record<string, unknown>>(() => {
  const brand = cssVar('--sd-color-brand', '#7C6BF5');
  const border = cssVar('--sd-border', 'rgba(255,255,255,0.14)');
  const split = cssVar('--sd-border-lighter', 'rgba(255,255,255,0.08)');
  const textSecondary = cssVar('--sd-text-secondary', 'oklch(0.58 0.014 240)');
  return {
    color: [brand],
    tooltip: { trigger: 'axis', valueFormatter: (v: unknown) => `${v} 次/秒` },
    grid: { left: 44, right: 16, top: 20, bottom: 24 },
    xAxis: {
      type: 'category',
      boundaryGap: false,
      data: traffic.value.map((t) => t.time),
      axisLine: { lineStyle: { color: border } },
      axisLabel: { color: textSecondary, fontSize: 10 },
    },
    yAxis: {
      type: 'value',
      name: 'QPS',
      nameTextStyle: { color: textSecondary, fontSize: 10 },
      axisLine: { show: false },
      splitLine: { lineStyle: { color: split } },
      axisLabel: { color: textSecondary, fontSize: 10 },
    },
    series: [
      {
        name: 'QPS',
        type: 'line',
        smooth: true,
        showSymbol: false,
        data: traffic.value.map((t) => t.qps),
        lineStyle: { color: brand, width: 2 },
        areaStyle: {
          color: {
            type: 'linear',
            x: 0,
            y: 0,
            x2: 0,
            y2: 1,
            colorStops: [
              { offset: 0, color: `${brand}47` },
              { offset: 1, color: `${brand}05` },
            ],
          },
        },
      },
    ],
  };
});

function statusMeta(status: AppHealthRow['status']): { color: string; text: string } {
  switch (status) {
    case 'alarm':
      return { color: 'var(--el-color-warning)', text: '告警' };
    case 'offline':
      return { color: 'var(--el-color-danger)', text: '离线' };
    default:
      return { color: 'var(--el-color-success)', text: '在线' };
  }
}

function attentionColor(level: AttentionItem['level']): string {
  switch (level) {
    case 'warning':
      return 'var(--el-color-warning)';
    case 'info':
      return 'var(--el-color-primary)';
    default:
      return 'var(--el-color-danger)';
  }
}

/** 点击拓扑节点 → Inspector（同节点再次点击取消选中） */
function selectTopo(node: TopoNode) {
  selectedTopo.value = selectedTopo.value === node ? null : node;
}

function topoInstances(node: TopoNode): number {
  return node.name ? instCount.value.get(node.name) ?? 0 : 0;
}

function topoVersion(node: TopoNode): string {
  const hit = apps.value.find((a) => a.name === node.name);
  return String(hit?.version ?? '') || '-';
}

function topoStatusText(node: TopoNode): string {
  if (!node.online) return '离线';
  const err = topoMetrics.value.errorRate;
  return err >= 0.01 ? '告警' : '在线';
}

function topoStatusColor(node: TopoNode): string {
  if (!node.online) return 'var(--el-color-danger)';
  const err = topoMetrics.value.errorRate;
  return err >= 0.01 ? 'var(--el-color-warning)' : 'var(--el-color-success)';
}
</script>

<template>
  <div class="flex h-[calc(100vh-var(--sd-topbar-h)-48px)] flex-col gap-4 overflow-y-auto pr-1">
    <!-- 顶部信息条 -->
    <header class="flex shrink-0 items-center justify-between">
      <div>
        <h1 class="text-lg font-semibold text-[var(--el-text-color-primary)]">
          工作台
        </h1>
        <p class="mt-0.5 text-xs text-[var(--el-text-color-regular)]">
          运行态势 + 问题入口 + 快速定位 · 以应用为核心、以可观测为主线的工程控制台
        </p>
      </div>
      <div class="flex items-center gap-3">
        <span class="text-xs text-[var(--el-text-color-secondary)]">
          <template v-if="lastUpdate">更新于 {{ lastUpdate.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit', second: '2-digit' }) }}</template>
          <template v-else>尚未加载</template>
        </span>
        <button
          type="button"
          class="flex h-8 items-center gap-1.5 rounded-lg border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-3 text-xs text-[var(--el-text-color-regular)] transition-colors hover:border-[var(--el-color-primary)] hover:text-[var(--el-color-primary)] disabled:opacity-60"
          :disabled="loading"
          @click="reloadAll"
        >
          <svg
            class="h-3.5 w-3.5"
            :class="{ 'animate-spin': loading }"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
          >
            <path d="M21 12a9 9 0 1 1-2.64-6.36" />
            <path d="M21 3v6h-6" />
          </svg>
          刷新
        </button>
      </div>
    </header>

    <!-- 服务星座（设计稿 Overview：132px 中心核心 + 8 个 92px 模块卫星 + 虚线 spoke） -->
    <section
      class="h-[560px] shrink-0"
      data-spec="constellation-hero"
    >
      <GlassCard
        variant="ghost"
        class="h-full"
      >
        <CoreConstellation
          :nodes="constellationNodes"
          :center="{ label: '星尘核心', sub: 'Stardust' }"
          @select="go"
        />
      </GlassCard>
    </section>

    <!-- 顶部统计卡片排：系统状态 / 运行实例 / QPS / 错误率 / P99 / 未处理告警 -->
    <section class="grid shrink-0 grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
      <template v-if="initialLoading">
        <div
          v-for="i in 6"
          :key="i"
          class="h-[86px] animate-pulse rounded-xl bg-[var(--el-fill-color)]"
        />
      </template>
      <KpiStat
        v-for="s in kpis"
        v-else
        :key="s.label"
        :label="s.label"
        :value="s.value"
        :unit="s.unit"
        :accent="s.accent"
        :icon="s.icon"
      />
    </section>

    <!-- 中部第一行：需要关注 | 应用运行态势 | 服务拓扑 -->
    <section class="grid min-h-0 grid-cols-1 gap-4 xl:grid-cols-12">
      <!-- 需要关注 -->
      <GlassCard
        title="需要关注"
        class="xl:col-span-3"
      >
        <div
          v-if="initialLoading"
          class="space-y-3 py-1"
        >
          <div
            v-for="i in 3"
            :key="i"
            class="h-9 animate-pulse rounded-lg bg-[var(--el-fill-color)]"
          />
        </div>
        <div
          v-else-if="attention.length === 0"
          class="flex h-32 items-center justify-center text-xs text-[var(--el-text-color-secondary)]"
        >
          暂无需要关注的事项
        </div>
        <ul
          v-else
          class="flex flex-col gap-2"
        >
          <li
            v-for="item in attention"
            :key="item.key"
            class="flex cursor-pointer items-center gap-2.5 rounded-lg border border-[var(--el-border-color-lighter)] px-2.5 py-2 transition-colors hover:border-[var(--el-color-primary)]/40 hover:bg-[var(--el-fill-color-lighter)]/60"
            @click="go(item.targetRoute)"
          >
            <span
              class="h-7 w-1 shrink-0 rounded-full"
              :style="{ backgroundColor: attentionColor(item.level) }"
            />
            <div class="min-w-0 flex-1">
              <div class="truncate text-xs font-semibold text-[var(--el-text-color-primary)]">
                {{ item.title }}
              </div>
              <div class="truncate text-[11px] text-[var(--el-text-color-regular)]">
                {{ item.desc }}
              </div>
            </div>
            <span class="shrink-0 text-[10px] text-[var(--el-text-color-secondary)]">{{ item.time }}</span>
            <span
              class="shrink-0 rounded border border-[var(--el-border-color)] px-1.5 py-0.5 text-[10px] text-[var(--el-text-color-secondary)] transition-colors hover:border-[var(--el-color-primary)] hover:text-[var(--el-color-primary)]"
            >{{ item.actionLabel }}</span>
          </li>
        </ul>
      </GlassCard>

      <!-- 应用运行态势 -->
      <GlassCard
        title="应用运行态势"
        class="xl:col-span-3"
      >
        <div
          v-if="initialLoading"
          class="space-y-2 py-1"
        >
          <div
            v-for="i in 4"
            :key="i"
            class="h-8 animate-pulse rounded-lg bg-[var(--el-fill-color)]"
          />
        </div>
        <div
          v-else-if="appHealth.length === 0"
          class="flex h-32 items-center justify-center text-xs text-[var(--el-text-color-secondary)]"
        >
          暂无应用数据
        </div>
        <ul
          v-else
          class="flex flex-col gap-1.5"
        >
          <li
            v-for="row in appHealth.slice(0, 8)"
            :key="row.name"
            class="flex items-center justify-between gap-2 rounded-lg px-2 py-1.5 transition-colors hover:bg-[var(--el-fill-color-lighter)]/40"
          >
            <div class="flex min-w-0 items-center gap-2">
              <span
                class="h-2 w-2 shrink-0 rounded-full"
                :style="{ backgroundColor: statusMeta(row.status).color }"
              />
              <span class="truncate text-xs text-[var(--el-text-color-primary)]">{{ row.displayName || row.name }}</span>
              <span
                v-if="row.displayName && row.displayName !== row.name"
                class="truncate text-[10px] text-[var(--el-text-color-secondary)]"
              >{{ row.name }}</span>
            </div>
            <div class="flex shrink-0 items-center gap-2">
              <span
                v-if="row.version"
                class="rounded bg-[var(--el-fill-color-light)] px-1.5 py-0.5 text-[10px] text-[var(--el-text-color-regular)]"
              >v{{ row.version }}</span>
              <span
                class="w-8 text-right text-[10px] font-medium"
                :style="{ color: statusMeta(row.status).color }"
              >{{ statusMeta(row.status).text }}</span>
            </div>
          </li>
        </ul>
      </GlassCard>

      <!-- 服务拓扑 -->
      <GlassCard
        title="服务拓扑"
        class="min-h-[280px] xl:col-span-6"
      >
        <p class="mb-1 text-xs text-[var(--el-text-color-regular)]">
          应用依赖关系 · <span class="text-[var(--el-text-color-primary)]">点击节点查看运行详情（Inspector）</span>
        </p>
        <p
          v-if="!initialLoading && topoEdges.length === 0"
          class="mb-1 text-[11px] text-[var(--el-text-color-placeholder)]"
        >
          服务依赖关系待接入
        </p>
        <div
          v-if="initialLoading"
          class="flex h-[220px] items-center justify-center"
        >
          <div class="h-24 w-24 animate-pulse rounded-full bg-[var(--el-fill-color)]" />
        </div>
        <div
          v-else
          class="relative h-[220px]"
        >
          <!-- SVG 连线层：中心辐射虚线 + 应用间有向依赖边（带箭头） -->
          <svg
            class="pointer-events-none absolute inset-0 h-full w-full"
            viewBox="0 0 100 100"
            preserveAspectRatio="none"
          >
            <defs>
              <marker
                id="sd-topo-arrow"
                viewBox="0 0 10 10"
                refX="8"
                refY="5"
                markerWidth="5"
                markerHeight="5"
                orient="auto-start-reverse"
              >
                <path
                  d="M 0 0 L 10 5 L 0 10 z"
                  fill="var(--el-color-primary)"
                />
              </marker>
            </defs>
            <line
              v-for="({ left, top }, idx) in topoNodes"
              :key="`line-${idx}`"
              x1="50"
              y1="50"
              :x2="left"
              :y2="top"
              stroke="var(--el-border-color-light)"
              stroke-width="0.4"
              stroke-dasharray="2 1.5"
              opacity="0.7"
            />
            <line
              v-for="e in topoEdges"
              :key="`edge-${e.key}`"
              :x1="e.x1"
              :y1="e.y1"
              :x2="e.x2"
              :y2="e.y2"
              stroke="var(--el-color-primary)"
              stroke-width="0.5"
              stroke-opacity="0.55"
              marker-end="url(#sd-topo-arrow)"
            />
          </svg>

          <!-- 中心节点 -->
          <button
            type="button"
            class="absolute left-1/2 top-1/2 z-10 flex -translate-x-1/2 -translate-y-1/2 flex-col items-center justify-center rounded-full border-2 border-[var(--el-color-primary)] px-5 py-3 text-white transition-transform hover:scale-105"
            style="
              background: radial-gradient(circle, color-mix(in srgb, var(--el-color-primary) 78%, transparent), color-mix(in srgb, var(--el-color-primary) 20%, transparent));
              box-shadow: 0 0 24px color-mix(in srgb, var(--el-color-primary) 40%, transparent);
            "
            :title="`${topology.center.label}${topology.center.sub ? ' · ' + topology.center.sub : ''}`"
            @click="go(topology.center.route)"
          >
            <span class="text-xs font-semibold">{{ topology.center.label }}</span>
            <span
              v-if="topology.center.sub"
              class="text-[9px] opacity-80"
            >{{ topology.center.sub }}</span>
          </button>

          <!-- 外围应用节点 -->
          <button
            v-for="({ node, left, top }, idx) in topoNodes"
            :key="`node-${idx}`"
            type="button"
            data-spec="topo-node"
            class="absolute z-[5] -translate-x-1/2 -translate-y-1/2 rounded-full border px-2 py-1 text-center transition-all duration-300 hover:z-20 hover:scale-110"
            :class="node.online
              ? 'border-[var(--el-color-primary)]/50 bg-[var(--el-bg-color)]/70'
              : 'border-[var(--el-color-danger)]/50 bg-[var(--el-bg-color)]/70'"
            :style="{ left: `${left}%`, top: `${top}%` }"
            :title="`${node.label}${node.sub ? ' · ' + node.sub : ''}${node.online ? ' · 在线' : ' · 离线'}`"
            @click="selectTopo(node)"
          >
            <span class="block max-w-[120px] truncate text-[10px] font-medium leading-tight text-[var(--el-text-color-primary)]">{{ node.label }}</span>
            <span
              v-if="node.sub"
              class="block max-w-[120px] truncate text-[8px] leading-tight text-[var(--el-text-color-secondary)]"
            >{{ node.sub }}</span>
            <span
              class="absolute -right-1 -top-1 h-2.5 w-2.5 rounded-full border border-[var(--el-bg-color)]"
              :style="{ backgroundColor: node.online ? 'var(--el-color-success)' : 'var(--el-color-danger)' }"
            />
          </button>
        </div>

        <!-- 拓扑 Inspector：点击节点后显示运行详情与快捷跳转 -->
        <div
          v-if="selectedTopo"
          data-spec="topo-inspector"
          class="mt-3 flex flex-wrap items-center gap-x-5 gap-y-2 rounded-xl border border-[var(--el-border-color)] px-4 py-3"
          style="background: color-mix(in srgb, var(--el-bg-color-overlay) 72%, transparent)"
        >
          <div class="flex min-w-0 items-center gap-2.5">
            <span
              class="h-2.5 w-2.5 shrink-0 rounded-full"
              :style="{ backgroundColor: topoStatusColor(selectedTopo) }"
            />
            <div class="min-w-0">
              <div class="truncate text-[13px] font-semibold text-[var(--el-text-color-primary)]">
                {{ selectedTopo.label }}
              </div>
              <div
                v-if="selectedTopo.sub"
                class="truncate text-[10px] text-[var(--el-text-color-secondary)]"
              >
                {{ selectedTopo.sub }}
              </div>
            </div>
          </div>
          <div class="flex shrink-0 items-center gap-4 text-xs">
            <span class="flex items-center gap-1 text-[var(--el-text-color-regular)]">
              状态 <b :style="{ color: topoStatusColor(selectedTopo) }">{{ topoStatusText(selectedTopo) }}</b>
            </span>
            <span class="flex items-center gap-1 text-[var(--el-text-color-regular)]">
              实例 <b class="tabular-nums text-[var(--el-text-color-primary)]">{{ topoInstances(selectedTopo) }}</b>
            </span>
            <span class="flex items-center gap-1 text-[var(--el-text-color-regular)]">
              QPS <b class="tabular-nums text-[var(--el-text-color-primary)]">{{ topoMetrics.qps }}</b>
            </span>
            <span class="flex items-center gap-1 text-[var(--el-text-color-regular)]">
              错误率
              <b
                class="tabular-nums"
                :style="{ color: topoMetrics.errorRate >= 0.01 ? 'var(--el-color-danger)' : 'var(--el-color-success)' }"
              >{{ (topoMetrics.errorRate * 100).toFixed(2) }}%</b>
            </span>
            <span class="flex items-center gap-1 text-[var(--el-text-color-regular)]">
              P99 <b class="tabular-nums text-[var(--el-text-color-primary)]">{{ topoMetrics.p99 }}<span class="text-[10px]">ms</span></b>
            </span>
            <span class="flex items-center gap-1 text-[var(--el-text-color-regular)]">
              版本 <b class="tabular-nums text-[var(--el-text-color-primary)]">{{ topoVersion(selectedTopo) }}</b>
            </span>
          </div>
          <div class="ml-auto flex shrink-0 items-center gap-2">
            <button
              type="button"
              class="rounded-lg border border-[var(--el-border-color)] px-3 py-1.5 text-xs text-[var(--el-text-color-regular)] transition-colors hover:border-[var(--el-color-primary)] hover:text-[var(--el-color-primary)]"
              @click="go(selectedTopo.route)"
            >
              查看应用
            </button>
            <button
              type="button"
              class="rounded-lg border border-[var(--el-border-color)] px-3 py-1.5 text-xs text-[var(--el-text-color-regular)] transition-colors hover:border-[var(--el-color-primary)] hover:text-[var(--el-color-primary)]"
              @click="go(selectedTopo.name ? `/Registry/AppOnline?appName=${encodeURIComponent(selectedTopo.name)}` : '/Registry/AppOnline')"
            >
              查看实例
            </button>
            <button
              type="button"
              class="rounded-lg border border-[var(--el-border-color)] px-3 py-1.5 text-xs text-[var(--el-text-color-regular)] transition-colors hover:border-[var(--el-color-primary)] hover:text-[var(--el-color-primary)]"
              @click="go(traceRoute(selectedTopo))"
            >
              查看 Trace
            </button>
            <button
              v-if="selectedTopo.id != null"
              type="button"
              class="rounded-lg bg-[var(--el-color-primary)] px-3 py-1.5 text-xs text-white transition hover:opacity-90"
              @click="go(`/Registry/App/${selectedTopo.id}`)"
            >
              查看完整详情
            </button>
            <button
              type="button"
              class="flex h-7 w-7 items-center justify-center rounded-lg text-[var(--el-text-color-secondary)] transition-colors hover:bg-[var(--el-fill-color)] hover:text-[var(--el-text-color-primary)]"
              aria-label="关闭"
              @click="selectedTopo = null"
            >
              ×
            </button>
          </div>
        </div>
      </GlassCard>
    </section>

    <!-- 中部第二行：最近事件 | 整体流量趋势(QPS) -->
    <section class="grid min-h-0 grid-cols-1 gap-4 xl:grid-cols-12">
      <!-- 最近事件 -->
      <GlassCard
        title="最近事件"
        class="xl:col-span-4"
      >
        <template #default>
          <div
            v-if="initialLoading"
            class="space-y-3 py-1"
          >
            <div
              v-for="i in 4"
              :key="i"
              class="h-8 animate-pulse rounded-lg bg-[var(--el-fill-color)]"
            />
          </div>
          <div
            v-else-if="events.length === 0"
            class="flex h-28 items-center justify-center text-xs text-[var(--el-text-color-secondary)]"
          >
            暂无事件
          </div>
          <ul
            v-else
            class="flex flex-col"
          >
            <li
              v-for="(ev, idx) in events"
              :key="idx"
              class="flex gap-3 rounded-lg border-b border-[var(--el-border-color-lighter)] py-2 transition-colors last:border-b-0 hover:bg-[var(--el-fill-color-lighter)]/40"
            >
              <span
                class="mt-1.5 h-7 w-1 shrink-0 rounded-full"
                :style="{
                  backgroundColor: ev.status === 'online'
                    ? 'var(--el-color-success)'
                    : ev.status === 'warn'
                      ? 'var(--el-color-warning)'
                      : 'var(--el-color-danger)',
                }"
              />
              <div class="min-w-0 flex-1">
                <div class="flex items-center justify-between gap-2">
                  <span class="truncate text-xs font-semibold text-[var(--el-text-color-primary)]">{{ ev.service }}</span>
                  <span class="shrink-0 text-[10px] text-[var(--el-text-color-regular)]">{{ ev.time }}</span>
                </div>
                <div class="flex items-center gap-2">
                  <span
                    class="rounded px-1 py-0.5 text-[10px] font-medium"
                    :style="{
                      backgroundColor: ev.tag === '节点'
                        ? 'var(--el-color-primary-light-9)'
                        : 'var(--el-color-success-light-9)',
                      color: ev.tag === '节点'
                        ? 'var(--el-color-primary)'
                        : 'var(--el-color-success)',
                    }"
                  >
                    {{ ev.tag }}
                  </span>
                  <span class="truncate text-xs text-[var(--el-text-color-regular)]">{{ ev.detail }}</span>
                </div>
              </div>
            </li>
          </ul>
        </template>
      </GlassCard>

      <!-- 整体流量趋势(QPS) -->
      <GlassCard
        title="整体流量趋势（QPS）"
        class="xl:col-span-8"
      >
        <div
          v-if="initialLoading"
          class="flex h-[220px] items-center justify-center"
        >
          <div class="h-40 w-full animate-pulse rounded-lg bg-[var(--el-fill-color)]" />
        </div>
        <div
          v-else-if="traffic.length === 0"
          class="flex h-[220px] items-center justify-center text-xs text-[var(--el-text-color-secondary)]"
        >
          暂无流量统计数据
        </div>
        <EChart
          v-else
          :option="trafficOption"
          height="220px"
        />
      </GlassCard>
    </section>
  </div>
</template>
