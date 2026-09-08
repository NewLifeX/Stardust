<script setup lang="ts">
/**
 * 星尘控制台 · 概览首页（Overview）
 * 视觉基准：顶部信息条 + 服务拓扑 + 实时 KPI + 集群健康 + 最新事件 + 快捷入口。
 * 样式纪律：颜色一律 --el-* 令牌；逻辑抽 overview.model.ts 纯函数。
 */
import { ref, computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { ElMessage } from 'element-plus';
import { request } from '@newlifex/cube-vue/core/utils/request';
import GlassCard from '@/layouts/StardustLayout/components/GlassCard.vue';
import KpiStat from '@/layouts/StardustLayout/components/KpiStat.vue';
import {
  orbitItems, mainOrbitItems, shortcuts, buildKpis, buildHealth, buildEvents,
  type KpiItem, type EventItem, type HealthSegment, type NodeRow, type AppRow, type OrbitItem,
} from './overview.model';

const router = useRouter();
const loading = ref(true);
const lastUpdate = ref<Date | null>(null);
const kpis = ref<KpiItem[]>([]);
const health = ref<HealthSegment[]>([]);
const events = ref<EventItem[]>([]);
const nodes = ref<NodeRow[]>([]);
const apps = ref<AppRow[]>([]);
const onlineApps = ref(0);
const serviceTotal = ref(0);

/** 首次加载且尚无数据时才显示骨架，刷新时保留旧数据 + 按钮转圈 */
const initialLoading = computed(() => loading.value && kpis.value.length === 0);

function go(item: OrbitItem) {
  router.push(item.route);
}

async function load() {
  loading.value = true;
  try {
    const [nr, ar, or, sr] = await Promise.all([
      request({ url: '/Nodes/Node', method: 'GET', params: { PageSize: 200 } }),
      request({ url: '/Registry/App', method: 'GET', params: { PageSize: 200 } }),
      request({ url: '/Registry/AppOnline', method: 'GET', params: { PageSize: 500 } }),
      request({ url: '/Registry/AppService', method: 'GET', params: { PageSize: 1 } }),
    ]);
    nodes.value = (nr?.data ?? []) as NodeRow[];
    apps.value = (ar?.data ?? []) as AppRow[];
    const online = new Set<string>((or?.data ?? []).map((r: any) => r.appName).filter(Boolean));
    onlineApps.value = online.size;
    serviceTotal.value = (sr as any)?.page?.totalCount ?? 0;
    const now = Date.now();
    kpis.value = buildKpis(nodes.value, apps.value, online, serviceTotal.value, now);
    health.value = buildHealth(nodes.value, now);
    events.value = buildEvents(nodes.value, apps.value, now);
    lastUpdate.value = new Date();
  } catch (e: any) {
    ElMessage.error(e?.message || '加载概览数据失败');
  } finally {
    loading.value = false;
  }
}

onMounted(load);

/** 轨道节点的实时数据徽标（把概览数字映射回拓扑节点，增强信息感） */
const orbitBadges = computed<Record<string, number | string>>(() => ({
  节点: nodes.value.length,
  应用: apps.value.length,
  服务: serviceTotal.value,
  监控: onlineApps.value,
  发布: apps.value.length,
  概览: '-',
  配置: '-',
  设置: '-',
}));

const healthTotal = computed(() => health.value.reduce((sum, s) => sum + s.value, 0));

function ringProps(segments: HealthSegment[], idx: number) {
  const radius = 44;
  const circumference = 2 * Math.PI * radius;
  const prev = segments.slice(0, idx).reduce((sum, s) => sum + s.value, 0);
  const value = segments[idx]?.value ?? 0;
  return {
    circumference,
    dasharray: `${(value / 100) * circumference} ${circumference}`,
    offset: -((prev / 100) * circumference),
  };
}
</script>

<template>
  <div class="flex h-[calc(100vh-64px-48px)] flex-col gap-4 overflow-hidden">
    <!-- 顶部信息条 -->
    <header class="flex shrink-0 items-center justify-between">
      <div>
        <h1 class="text-lg font-semibold text-[var(--el-text-color-primary)]">
          控制台概览
        </h1>
        <p class="mt-0.5 text-xs text-[var(--el-text-color-regular)]">
          分布式服务的运行态势总览 · 数据实时同步
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
          @click="load"
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

    <!-- 主体：左拓扑 + 右数据列 -->
    <div class="flex min-h-0 flex-1 gap-5 overflow-hidden">
      <!-- 左：服务拓扑 -->
      <GlassCard
        title="服务拓扑"
        variant="ghost"
        class="flex min-h-0 flex-1 flex-col overflow-hidden"
      >
        <p class="mb-2 text-xs text-[var(--el-text-color-regular)]">
          服务星座 · <span class="text-[var(--el-text-color-primary)]">点击模块节点进入对应管理页</span>
        </p>

        <div
          v-if="initialLoading"
          class="flex flex-1 flex-col gap-4 p-6"
        >
          <div class="mx-auto h-24 w-24 animate-pulse rounded-full bg-[var(--el-fill-color)]" />
          <div class="flex flex-wrap justify-center gap-4">
            <div
              v-for="i in 6"
              :key="i"
              class="h-14 w-14 animate-pulse rounded-full bg-[var(--el-fill-color)]"
            />
          </div>
        </div>

        <div
          v-else
          class="relative min-h-0 flex-1"
        >
          <!-- SVG 连线层 -->
          <svg
            class="pointer-events-none absolute inset-0 h-full w-full"
            viewBox="0 0 100 100"
            preserveAspectRatio="none"
          >
            <defs>
              <filter
                id="line-glow"
                x="-50%"
                y="-50%"
                width="200%"
                height="200%"
              >
                <feGaussianBlur
                  stdDeviation="0.6"
                  result="blur"
                />
                <feComposite
                  in="SourceGraphic"
                  in2="blur"
                  operator="over"
                />
              </filter>
              <linearGradient
                id="core-line"
                x1="50"
                y1="50"
                x2="100"
                y2="100"
                gradientUnits="userSpaceOnUse"
              >
                <stop
                  offset="0%"
                  :stop-color="'var(--el-color-primary)'"
                  stop-opacity="0.5"
                />
                <stop
                  offset="100%"
                  :stop-color="'var(--el-color-primary)'"
                  stop-opacity="0.05"
                />
              </linearGradient>
            </defs>
            <line
              v-for="(item, idx) in orbitItems"
              :key="`line-${idx}`"
              x1="50"
              y1="50"
              :x2="item.left"
              :y2="item.top"
              stroke="var(--el-border-color-light)"
              stroke-width="0.5"
              stroke-dasharray="2 1.5"
              opacity="0.6"
            />
            <line
              v-for="(item, idx) in mainOrbitItems"
              :key="`main-line-${idx}`"
              x1="50"
              y1="50"
              :x2="item.left"
              :y2="item.top"
              :stroke="item.color || 'var(--el-color-primary)'"
              stroke-width="0.7"
              opacity="0.5"
              filter="url(#line-glow)"
            />
          </svg>

          <!-- 星尘核心 -->
          <button
            type="button"
            class="core-pulse group absolute left-1/2 top-1/2 z-10 flex h-[92px] w-[92px] -translate-x-1/2 -translate-y-1/2 flex-col items-center justify-center rounded-full border-2 border-[var(--el-color-primary)] text-white transition-transform hover:scale-105"
            style="
              background: radial-gradient(circle, color-mix(in srgb, var(--el-color-primary) 78%, transparent), color-mix(in srgb, var(--el-color-primary) 20%, transparent));
              box-shadow: 0 0 28px color-mix(in srgb, var(--el-color-primary) 45%, transparent);
            "
            title="返回概览"
            @click="go({ route: '/stardust/overview' } as OrbitItem)"
          >
            <svg
              class="h-6 w-6"
              viewBox="0 0 24 24"
              fill="currentColor"
            >
              <path d="M12 2l1.8 6.2L20 10l-6.2 1.8L12 18l-1.8-6.2L4 10l6.2-1.8L12 2z" />
            </svg>
            <span class="text-[10px] font-medium">星尘核心</span>
            <span class="absolute inset-0 rounded-full ring-1 ring-[var(--el-color-primary-light-3)]/60 orbit-ring" />
          </button>

          <!-- 轨道节点 -->
          <button
            v-for="(item, idx) in orbitItems"
            :key="idx"
            type="button"
            class="orbit-node group absolute flex -translate-x-1/2 -translate-y-1/2 flex-col items-center justify-center rounded-full border text-center transition-all duration-300 hover:scale-110 hover:z-20"
            :class="[
              item.main ? 'h-[62px] w-[62px] border-[2px] z-[5]' : 'h-[50px] w-[50px] border z-[4]',
            ]"
            :style="{
              left: `${item.left}%`,
              top: `${item.top}%`,
              borderColor: item.color || 'var(--el-border-color)',
              background: item.main
                ? `radial-gradient(circle, color-mix(in srgb, ${item.color || 'var(--el-color-primary)'} 55%, transparent), color-mix(in srgb, var(--el-color-primary) 8%, transparent))`
                : 'color-mix(in srgb, var(--el-bg-color) 72%, transparent)',
              color: item.color || 'var(--el-text-color-primary)',
              boxShadow: item.main ? `0 0 20px color-mix(in srgb, ${item.color || 'var(--el-color-primary)'} 35%, transparent)` : 'none',
            }"
            :title="`${item.label} · ${item.sub}`"
            @click="go(item)"
          >
            <svg
              class="h-5 w-5"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="1.7"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <path :d="item.icon" />
            </svg>
            <span class="mt-0.5 text-[10px] font-medium leading-none">{{ item.label }}</span>
            <!-- 数据徽标 -->
            <span
              v-if="orbitBadges[item.label] !== '-'"
              class="absolute -right-1 -top-1 flex h-4 min-w-4 items-center justify-center rounded-full bg-[var(--el-color-primary)] px-1 text-[9px] font-bold text-white shadow"
            >
              {{ orbitBadges[item.label] }}
            </span>
          </button>
        </div>
      </GlassCard>

      <!-- 右：数据面板 -->
      <div
        data-spec="right-col"
        class="flex w-[340px] shrink-0 flex-col gap-4 overflow-hidden"
      >
        <!-- KPI 指标 -->
        <div class="grid grid-cols-2 gap-3">
          <KpiStat
            v-for="s in kpis"
            :key="s.label"
            :label="s.label"
            :value="s.value"
            :accent="s.accent"
            :icon="s.icon"
          />
        </div>

        <!-- 集群健康 -->
        <GlassCard
          title="集群健康"
          variant="ghost"
          class="shrink-0"
        >
          <div
            v-if="initialLoading"
            class="flex items-center gap-4 py-2"
          >
            <div class="h-[116px] w-[116px] animate-pulse rounded-full bg-[var(--el-fill-color)]" />
            <div class="flex-1 space-y-2">
              <div class="h-3 w-20 animate-pulse rounded bg-[var(--el-fill-color)]" />
              <div class="h-3 w-full animate-pulse rounded bg-[var(--el-fill-color)]" />
            </div>
          </div>
          <div
            v-else
            class="flex items-center gap-4 py-1"
          >
            <div class="relative h-[116px] w-[116px] shrink-0">
              <svg
                class="h-full w-full -rotate-90"
                viewBox="0 0 100 100"
              >
                <circle
                  cx="50"
                  cy="50"
                  r="44"
                  fill="none"
                  stroke="var(--el-fill-color-light)"
                  stroke-width="10"
                />
                <circle
                  v-for="(seg, idx) in health"
                  :key="seg.label"
                  cx="50"
                  cy="50"
                  r="44"
                  fill="none"
                  :stroke="seg.color"
                  stroke-width="10"
                  stroke-linecap="round"
                  :stroke-dasharray="ringProps(health, idx).dasharray"
                  :stroke-dashoffset="ringProps(health, idx).offset"
                />
              </svg>
              <div class="absolute inset-0 flex flex-col items-center justify-center">
                <span class="text-xl font-bold text-[var(--el-text-color-primary)]">{{ health[0]?.value ?? 0 }}%</span>
                <span class="text-[10px] text-[var(--el-text-color-regular)]">在线</span>
              </div>
            </div>

            <div class="flex min-w-0 flex-1 flex-col gap-2">
              <div class="flex items-center justify-between">
                <span class="text-xs text-[var(--el-text-color-regular)]">节点状态</span>
                <span class="text-xs text-[var(--el-text-color-regular)]">实时</span>
              </div>
              <div class="flex flex-col gap-1.5 text-xs">
                <div
                  v-for="seg in health"
                  :key="seg.label"
                  class="flex items-center justify-between"
                >
                  <div class="flex items-center gap-1.5">
                    <span
                      class="h-2.5 w-2.5 rounded-full"
                      :style="{ backgroundColor: seg.color }"
                    />
                    <span class="text-[var(--el-text-color-regular)]">{{ seg.label }}</span>
                  </div>
                  <span class="font-semibold text-[var(--el-text-color-primary)]">{{ seg.value }}%</span>
                </div>
              </div>
              <div
                v-if="healthTotal === 0"
                class="text-[11px] text-[var(--el-text-color-secondary)]"
              >
                暂无节点数据
              </div>
            </div>
          </div>
        </GlassCard>

        <!-- 最新事件 -->
        <GlassCard
          title="最新事件"
          variant="ghost"
          class="flex min-h-0 flex-1 flex-col overflow-hidden"
        >
          <template #default>
            <div class="flex items-center justify-between py-1">
              <span class="text-xs text-[var(--el-text-color-regular)]">{{ events.length }} 条</span>
              <span class="text-xs text-[var(--el-text-color-regular)]">实时</span>
            </div>
            <div class="mt-1 -mr-2 min-h-0 flex-1 overflow-y-auto pr-2">
              <div
                v-for="(ev, idx) in events"
                :key="idx"
                class="event-row group flex gap-3 rounded-lg border-b border-[var(--el-border-color-lighter)] py-2.5 pr-2 transition-colors last:border-b-0 hover:bg-[var(--el-fill-color-lighter)]/40"
              >
                <span
                  class="mt-1.5 h-7 w-1 shrink-0 rounded-full"
                  :style="{
                    backgroundColor: ev.status === 'online'
                      ? 'var(--el-color-success)'
                      : ev.status === 'warn'
                        ? 'var(--el-color-warning)'
                        : 'var(--el-color-danger)'
                  }"
                />
                <div class="flex min-w-0 flex-1 flex-col gap-0.5">
                  <div class="flex items-center justify-between gap-2">
                    <span class="truncate text-xs font-semibold text-[var(--el-text-color-primary)]">{{ ev.service }}</span>
                    <span class="shrink-0 text-[11px] text-[var(--el-text-color-regular)]">{{ ev.time }}</span>
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
                          : 'var(--el-color-success)'
                      }"
                    >
                      {{ ev.tag }}
                    </span>
                    <span class="truncate text-xs text-[var(--el-text-color-regular)]">{{ ev.detail }}</span>
                  </div>
                </div>
              </div>
              <div
                v-if="events.length === 0 && !initialLoading"
                class="flex h-24 items-center justify-center text-xs text-[var(--el-text-color-secondary)]"
              >
                暂无事件
              </div>
              <div
                v-else-if="initialLoading"
                class="space-y-3 py-2"
              >
                <div
                  v-for="i in 4"
                  :key="i"
                  class="flex gap-3"
                >
                  <div class="h-7 w-1 animate-pulse rounded-full bg-[var(--el-fill-color)]" />
                  <div class="flex-1 space-y-1.5">
                    <div class="h-3 w-1/2 animate-pulse rounded bg-[var(--el-fill-color)]" />
                    <div class="h-3 w-3/4 animate-pulse rounded bg-[var(--el-fill-color)]" />
                  </div>
                </div>
              </div>
            </div>
          </template>
        </GlassCard>

        <!-- 快捷入口 -->
        <div class="grid grid-cols-4 gap-2">
          <router-link
            v-for="sc in shortcuts"
            :key="sc.label"
            :to="sc.to"
            class="shortcut-card group flex flex-col items-center justify-center gap-1.5 rounded-xl border border-[var(--el-border-color)] bg-[var(--el-bg-color)]/70 px-2 py-3 text-[var(--el-text-color-regular)] transition-all hover:-translate-y-0.5 hover:border-[var(--el-color-primary)] hover:text-[var(--el-color-primary)] hover:shadow-[var(--el-box-shadow)]"
          >
            <div class="flex h-8 w-8 items-center justify-center rounded-lg transition-colors group-hover:bg-[var(--el-color-primary-light-9)]">
              <svg
                class="h-5 w-5"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="1.8"
                stroke-linecap="round"
                stroke-linejoin="round"
              >
                <path :d="sc.icon" />
              </svg>
            </div>
            <span class="text-[11px] font-semibold">{{ sc.label }}</span>
            <span class="text-[9px] opacity-70">{{ sc.sub }}</span>
          </router-link>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.orbit-node {
  backdrop-filter: blur(8px);
  -webkit-backdrop-filter: blur(8px);
}

.core-pulse {
  animation: core-breathe 3s ease-in-out infinite;
}

@keyframes core-breathe {
  0%, 100% {
    box-shadow: 0 0 24px color-mix(in srgb, var(--el-color-primary) 40%, transparent);
  }
  50% {
    box-shadow: 0 0 42px color-mix(in srgb, var(--el-color-primary) 65%, transparent);
  }
}

/* 核心外环缓慢旋转，增强科技感 */
.orbit-ring {
  animation: ring-spin 12s linear infinite;
}

@keyframes ring-spin {
  to {
    transform: rotate(360deg);
  }
}

.shortcut-card:hover svg {
  color: var(--el-color-primary);
}

.event-row:hover {
  background-color: color-mix(in srgb, var(--el-fill-color-lighter) 40%, transparent);
}
</style>
