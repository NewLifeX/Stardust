<script setup lang="ts">
/**
 * NodesPage：控制台「节点」页（/nodes）容器。版面：页头 → 暗底星图 canvas → 过滤条 → 批量操作卡 → 节点表格。
 * 取数与归一全部在 nodesPage.logic（request 注入）；本组件只管 canvas 绘制、过滤、勾选、抽屉与上下文联动。
 * projectId 取自 stardustContext（非空时随请求 params 透传，全局拦截器也会注入），随项目切换重取。
 *
 * 现实约束（不造数字）：
 *  - 后端 NodeController 无「批量升级 Agent / 安装 .NET / 下发指令」端点，这些按钮一律 disabled
 *    并在 title 里说明原因；「接入节点向导」由外壳 A 键接入 Drawer 处理；
 *  - 终端按钮不再 toast：经 ui.store 的 requestTermNode 通道把节点 id 发给外壳，
 *    外壳（index.vue）watch 到后自动展开底部控制台的终端页签；
 *  - NodeStat 是每日全局聚合，没有单节点 CPU 时序 → 抽屉「概览」CPU 走势默认空态「CPU 走势待接入」。
 */
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import PageHead from './PageHead.vue';
import Btn from './Btn.vue';
import Card from './Card.vue';
import Tag from './Tag.vue';
import Dot from './Dot.vue';
import KIcon from './KIcon.vue';
import Chip from './Chip.vue';
import Drawer from './Drawer/Drawer.vue';
import { requestTermNode } from './ui.store.ts';
import { useContextReload } from './useContextReload.ts';
import { stardustContext } from '@/stores/stardustContext';
import { H3, TD, TH } from './deployView.styles.ts';
import {
  barLevel,
  drawerKv,
  fetchCpuHistory,
  fetchNodeApps,
  fetchNodes,
  filterConsole,
  layoutNodes,
  relTime,
  toneCounts,
  toggleSelect,
  type CpuPoint,
  type NodeAppRow,
  type NodeRowView,
  type SkyNode,
  type ToneFilter,
} from './nodesPage.logic.ts';

const props = defineProps<{
  /** 测试缝：注入 request；生产不传（gallery 也靠它触发首轮取数，见下方 immediate load） */
  deps?: { request?: Req };
}>();

function req(): Req {
  return props.deps?.request ?? (request as Req);
}

// ---------- 列表状态 ----------
const rows = ref<NodeRowView[]>([]);
const loading = ref(true);
const error = ref('');
const filter = ref<ToneFilter>('all');
const keyword = ref('');
const selected = ref<Set<number>>(new Set());

const counts = computed(() => toneCounts(rows.value));
const filtered = computed(() => filterConsole(rows.value, filter.value, keyword.value));
const skyNodes = computed<SkyNode[]>(() => layoutNodes(rows.value));
const sub = computed(
  () => `${counts.value.running} 台正常 · ${counts.value.high} 台高负载 · ${counts.value.offline} 台离线`,
);

const FILTERS: ReadonlyArray<{ key: ToneFilter; label: string }> = [
  { key: 'all', label: '全部' },
  { key: 'running', label: '正常' },
  { key: 'high', label: '高负载' },
  { key: 'offline', label: '离线' },
];
function filterCount(k: ToneFilter): number {
  if (k === 'all') return counts.value.total;
  if (k === 'running') return counts.value.running;
  if (k === 'high') return counts.value.high;
  return counts.value.offline;
}

async function loadNodes(): Promise<void> {
  loading.value = true;
  error.value = '';
  const r = await fetchNodes(req(), stardustContext.projectId);
  rows.value = r.rows;
  error.value = r.error;
  loading.value = false;
}

// 生产：上下文就绪/切换后取数。gallery 里 stardustContext.ready 默认 false，useContextReload 不会触发；
// 因此当显式注入 deps.request（story/CT）时立即首轮取数。
useContextReload(() => { void loadNodes(); }, stardustContext);
if (props.deps?.request) void loadNodes();

// ---------- 勾选 ----------
function onToggleSelect(id: number): void {
  selected.value = toggleSelect(selected.value, id);
}
function onClearSelection(): void {
  selected.value = new Set();
}
const allFilteredSelected = computed(
  () => filtered.value.length > 0 && filtered.value.every((r) => selected.value.has(r.id)),
);
function onToggleAll(): void {
  const next = new Set(selected.value);
  if (allFilteredSelected.value) {
    for (const r of filtered.value) next.delete(r.id);
  } else {
    for (const r of filtered.value) next.add(r.id);
  }
  selected.value = next;
}

// ---------- 抽屉 ----------
const drawerId = ref<number | null>(null);
const drawerTab = ref<'overview' | 'apps'>('overview');
const appRows = ref<NodeAppRow[]>([]);
const appsLoading = ref(false);
const appsError = ref('');
const cpuPts = ref<CpuPoint[]>([]);
const cpuLoading = ref(false);

const drawerRow = computed(() => rows.value.find((r) => r.id === drawerId.value) ?? null);
const drawerKvPairs = computed(() => (drawerRow.value ? drawerKv(drawerRow.value) : []));

async function loadDrawerApps(nodeId: number): Promise<void> {
  appsLoading.value = true;
  appsError.value = '';
  const r = await fetchNodeApps(req(), nodeId, stardustContext.projectId);
  appRows.value = r.rows;
  appsError.value = r.error;
  appsLoading.value = false;
}
async function loadCpu(nodeId: number): Promise<void> {
  cpuLoading.value = true;
  const r = await fetchCpuHistory(req(), nodeId, stardustContext.projectId);
  cpuPts.value = r.points;
  cpuLoading.value = false;
}
function openNode(id: number): void {
  drawerId.value = id;
  drawerTab.value = 'overview';
  appRows.value = [];
  appsError.value = '';
  cpuPts.value = [];
  void loadDrawerApps(id);
  void loadCpu(id);
}
function closeDrawer(): void {
  drawerId.value = null;
}

/** 终端：经模块级通道把节点 id 发给外壳，外壳自动展开终端页签（见 ui.store.ts TermNodeRequest 注释） */
function openTerm(id: number): void {
  requestTermNode(id);
}

// ---------- 表格小工具 ----------
function barColor(pct: number | null): string {
  const lvl = barLevel(pct);
  if (!lvl) return 'transparent';
  if (lvl === 'ok') return cssVar('--el-color-success');
  if (lvl === 'warn') return cssVar('--el-color-warning');
  return cssVar('--el-color-danger');
}
function pctText(pct: number | null): string {
  return pct === null ? '—' : `${pct.toFixed(0)}%`;
}

// ---------- 星图 canvas ----------
const canvasRef = ref<HTMLCanvasElement | null>(null);
const mousePt = ref<{ x: number; y: number } | null>(null);
const hovNode = ref<SkyNode | null>(null);

function cssVar(name: string): string {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
}

function draw(): void {
  const cv = canvasRef.value;
  if (!cv) return;
  const ctx = cv.getContext('2d');
  if (!ctx) return;
  const dpr = window.devicePixelRatio || 1;
  const W = cv.clientWidth;
  const H = cv.clientHeight;
  if (W <= 0 || H <= 0) return;
  cv.width = Math.floor(W * dpr);
  cv.height = Math.floor(H * dpr);
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  ctx.clearRect(0, 0, W, H);

  const ok = cssVar('--el-color-success');
  const warn = cssVar('--el-color-warning');
  const mut = cssVar('--el-text-color-secondary');
  const line = cssVar('--el-border-color-light');
  const ink = cssVar('--el-text-color-primary');

  // 背景星尘（确定性派生，不随机，CT 截图稳定）
  ctx.fillStyle = line;
  for (let i = 0; i < 60; i += 1) {
    const x = (((i * 137) % 100) / 100) * W;
    const y = (((i * 89) % 100) / 100) * H;
    ctx.globalAlpha = 0.3;
    ctx.beginPath();
    ctx.arc(x, y, 0.6 + ((i * 7) % 10) * 0.12, 0, 6.283);
    ctx.fill();
  }
  ctx.globalAlpha = 1;

  // 按集群分组连线 + 集群标签
  const byCluster = new Map<string, SkyNode[]>();
  for (const s of skyNodes.value) {
    const row = rows.value.find((r) => r.id === s.id);
    const c = row?.cluster || '默认集群';
    const arr = byCluster.get(c);
    if (arr) arr.push(s);
    else byCluster.set(c, [s]);
  }
  ctx.strokeStyle = line;
  ctx.lineWidth = 1;
  for (const [cluster, nodes] of byCluster) {
    if (nodes.length > 1) {
      ctx.beginPath();
      nodes.forEach((s, i) => {
        const x = s.x * W;
        const y = s.y * H;
        if (i === 0) ctx.moveTo(x, y);
        else ctx.lineTo(x, y);
      });
      ctx.stroke();
    }
    ctx.fillStyle = mut;
    ctx.font = '500 10px "JetBrains Mono", monospace';
    ctx.fillText(cluster.slice(0, 6).toUpperCase(), Math.max(4, nodes[0].x * W - 14), H - 8);
  }

  // 命中检测
  let hovered: SkyNode | null = null;
  const m = mousePt.value;
  if (m) {
    for (const s of skyNodes.value) {
      const x = s.x * W;
      const y = s.y * H;
      if (Math.hypot(m.x - x, m.y - y) < 15) {
        hovered = s;
        break;
      }
    }
  }

  for (const s of skyNodes.value) {
    const x = s.x * W;
    const y = s.y * H;
    const col = s.tone === 'offline' ? mut : s.tone === 'high' ? warn : ok;
    const R = 3 + (s.cpu ?? 30) / 26;

    ctx.beginPath();
    ctx.arc(x, y, R, 0, 6.283);
    if (s.tone === 'offline') {
      ctx.strokeStyle = mut;
      ctx.lineWidth = 1.2;
      ctx.setLineDash([3, 3]);
      ctx.stroke();
      ctx.setLineDash([]);
    } else {
      ctx.fillStyle = col;
      ctx.shadowColor = col;
      ctx.shadowBlur = s.tone === 'high' ? 12 : 6;
      ctx.fill();
      ctx.shadowBlur = 0;
    }

    if (hovered?.id === s.id) {
      ctx.strokeStyle = ink;
      ctx.lineWidth = 1.2;
      ctx.beginPath();
      ctx.arc(x, y, R + 6, 0, 6.283);
      ctx.stroke();
    }
  }

  hovNode.value = hovered;
  cv.style.cursor = hovered ? 'pointer' : 'default';
}

function onCanvasMove(e: MouseEvent): void {
  const cv = canvasRef.value;
  if (!cv) return;
  const rect = cv.getBoundingClientRect();
  mousePt.value = { x: e.clientX - rect.left, y: e.clientY - rect.top };
  draw();
}
function onCanvasLeave(): void {
  mousePt.value = null;
  hovNode.value = null;
  draw();
}
function onCanvasClick(): void {
  if (hovNode.value) openNode(hovNode.value.id);
}
function onResize(): void {
  draw();
}

onMounted(() => {
  draw();
  window.addEventListener('resize', onResize);
});
onBeforeUnmount(() => {
  window.removeEventListener('resize', onResize);
});
watch(skyNodes, () => draw());
</script>

<template>
  <div data-spec="nodes-page">
    <PageHead
      eyebrow="节点 · 每台装着 StarAgent 的机器是一颗星，应用是它身边的尘"
      title="星图"
      :sub="sub"
    >
      <template #actions>
        <Btn
          variant="primary"
          size="sm"
          disabled
          data-spec="nodes-connect"
        >
          <template #icon>
            <KIcon
              name="Plus"
              :size="13"
            />
          </template>
          接入节点 · 待接入
        </Btn>
      </template>
    </PageHead>

    <!-- 星图 canvas -->
    <Card
      padding="none"
      data-spec="nodes-sky"
      class="mb-4"
    >
      <div
        class="relative"
        data-spec="nodes-sky-wrap"
      >
        <canvas
          ref="canvasRef"
          class="block h-[280px] w-full"
          data-spec="nodes-sky-canvas"
          @mousemove="onCanvasMove"
          @mouseleave="onCanvasLeave"
          @click="onCanvasClick"
        />
        <!-- hover tooltip -->
        <div
          v-if="hovNode"
          data-spec="nodes-sky-tip"
          class="pointer-events-none absolute z-10 min-w-[140px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] py-[8px] text-[11.5px] text-[var(--el-text-color-primary)] shadow-[var(--el-box-shadow)]"
          :style="{ left: `${Math.min(92, hovNode.x * 100)}%`, top: `${Math.max(2, hovNode.y * 100)}%`, transform: 'translate(14px, -40px)' }"
        >
          <div class="font-medium">
            {{ hovNode.name }}
            <span class="text-[var(--el-text-color-secondary)]">· CPU {{ hovNode.cpu === null ? '—' : `${hovNode.cpu}%` }} · {{ hovNode.services }} 个应用</span>
          </div>
          <div class="mt-[2px] font-mono text-[10.5px] text-[var(--el-text-color-secondary)]">
            {{ hovNode.ip }}
          </div>
        </div>
        <!-- 空态 -->
        <div
          v-if="!loading && !error && rows.length === 0"
          data-spec="nodes-sky-empty"
          class="absolute inset-0 grid place-items-center text-[12.5px] text-[var(--el-text-color-secondary)]"
        >
          还没有节点
        </div>
      </div>
    </Card>

    <!-- 过滤条 -->
    <div
      class="mb-4 flex flex-wrap items-center gap-[6px]"
      data-spec="nodes-filter"
    >
      <Chip
        v-for="f in FILTERS"
        :key="f.key"
        :on="filter === f.key"
        data-spec="nodes-filter-chip"
        :data-key="f.key"
        @click="filter = f.key"
      >
        {{ f.label }} {{ filterCount(f.key) }}
      </Chip>
      <span class="flex-1" />
      <div class="relative">
        <span class="pointer-events-none absolute left-[9px] top-1/2 -translate-y-1/2 text-[var(--el-text-color-placeholder)]">
          <KIcon
            name="Search"
            :size="13"
          />
        </span>
        <input
          v-model="keyword"
          type="text"
          placeholder="按名称 / IP 过滤"
          data-spec="nodes-search"
          class="h-[28px] w-[220px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] pl-[28px] pr-[10px] text-[12px] text-[var(--el-text-color-primary)] outline-none placeholder:text-[var(--el-text-color-placeholder)] focus:border-[var(--el-color-primary)]"
        >
      </div>
    </div>

    <!-- 失败 -->
    <div
      v-if="error"
      data-spec="nodes-error"
      class="mb-4 rounded-[8px] border border-[color-mix(in_oklab,var(--el-color-danger)_35%,transparent)] bg-[color-mix(in_oklab,var(--el-color-danger)_8%,transparent)] px-[14px] py-[10px] text-[12.5px] text-[var(--el-color-danger)]"
    >
      节点加载失败：{{ error }}
      <button
        type="button"
        data-spec="nodes-retry"
        class="ml-[8px] cursor-pointer underline"
        @click="loadNodes"
      >
        重试
      </button>
    </div>

    <!-- 批量操作卡 -->
    <Card
      v-if="selected.size > 0"
      padding="pad"
      data-spec="nodes-batch"
      class="mb-4"
    >
      <div class="flex flex-wrap items-center gap-[8px]">
        <b class="text-[12.5px] text-[var(--el-text-color-primary)]">已选 {{ selected.size }} 台</b>
        <Btn
          size="sm"
          disabled
          title="后端暂未提供批量操作接口"
          data-spec="nodes-batch-upgrade"
        >
          升级 Agent
        </Btn>
        <Btn
          size="sm"
          disabled
          title="后端暂未提供批量操作接口"
          data-spec="nodes-batch-dotnet"
        >
          安装 .NET
        </Btn>
        <Btn
          size="sm"
          disabled
          title="后端暂未提供批量操作接口"
          data-spec="nodes-batch-cmd"
        >
          下发指令
        </Btn>
        <span class="flex-1" />
        <Btn
          size="sm"
          variant="ghost"
          data-spec="nodes-batch-clear"
          @click="onClearSelection"
        >
          取消
        </Btn>
      </div>
    </Card>

    <!-- 节点表格 -->
    <Card
      padding="none"
      data-spec="nodes-table-card"
    >
      <div
        v-if="loading"
        data-spec="nodes-loading"
        class="py-12 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
      >
        <span class="mr-[6px] inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)] align-[-3px]" />
        节点加载中…
      </div>
      <table
        v-else
        class="w-full border-collapse"
        data-spec="nodes-table"
      >
        <thead>
          <tr>
            <th :class="[TH, 'w-[34px]']">
              <input
                type="checkbox"
                data-spec="nodes-check-all"
                :checked="allFilteredSelected"
                @change="onToggleAll"
              >
            </th>
            <th :class="TH">
              节点
            </th>
            <th :class="TH">
              系统
            </th>
            <th :class="[TH, 'text-right']">
              CPU
            </th>
            <th :class="[TH, 'text-right']">
              内存
            </th>
            <th :class="[TH, 'text-right']">
              应用
            </th>
            <th :class="TH">
              心跳
            </th>
            <th :class="[TH, 'w-[90px]']">
              终端
            </th>
          </tr>
        </thead>
        <tbody class="group">
          <tr
            v-for="r in filtered"
            :key="r.id"
            data-spec="nodes-row"
            :data-id="r.id"
            :data-tone="r.tone"
            class="cursor-pointer hover:bg-[var(--el-bg-color-overlay)]"
            @click="openNode(r.id)"
          >
            <td
              :class="TD"
              @click.stop
            >
              <input
                type="checkbox"
                data-spec="nodes-row-check"
                :checked="selected.has(r.id)"
                @change="onToggleSelect(r.id)"
              >
            </td>
            <td :class="TD">
              <div class="flex items-center gap-[8px]">
                <Dot :tone="r.tone === 'high' ? 'warn' : r.tone === 'offline' ? 'off' : 'ok'" />
                <div class="min-w-0">
                  <div class="text-[12.5px] font-medium text-[var(--el-text-color-primary)]">
                    {{ r.name }}
                  </div>
                  <div class="font-mono text-[11px] text-[var(--el-text-color-secondary)]">
                    {{ r.ip }}
                  </div>
                </div>
              </div>
            </td>
            <td :class="TD">
              <div class="text-[11.5px] text-[var(--el-text-color-secondary)]">
                {{ r.os || '—' }}
              </div>
              <div class="mt-[3px] flex items-center gap-[4px]">
                <Tag tone="default">
                  {{ r.agentVer || '—' }}
                </Tag>
              </div>
            </td>
            <td :class="[TD, 'text-right']">
              <span
                v-if="r.tone !== 'offline' && r.cpu !== null"
                class="inline-flex items-center justify-end gap-[6px]"
              >
                <span class="h-[6px] w-[60px] overflow-hidden rounded-full bg-[var(--el-fill-color)]">
                  <span
                    class="block h-full rounded-full"
                    :style="{ width: `${Math.min(100, r.cpu)}%`, backgroundColor: barColor(r.cpu) }"
                  />
                </span>
                <span class="font-mono text-[11.5px] tabular-nums text-[var(--el-text-color-primary)]">{{ pctText(r.cpu) }}</span>
              </span>
              <span
                v-else
                class="font-mono text-[11.5px] text-[var(--el-text-color-secondary)]"
              >
                —
              </span>
            </td>
            <td :class="[TD, 'text-right']">
              <span
                v-if="r.tone !== 'offline' && r.mem !== null"
                class="inline-flex items-center justify-end gap-[6px]"
              >
                <span class="h-[6px] w-[60px] overflow-hidden rounded-full bg-[var(--el-fill-color)]">
                  <span
                    class="block h-full rounded-full"
                    :style="{ width: `${Math.min(100, r.mem)}%`, backgroundColor: barColor(r.mem) }"
                  />
                </span>
                <span class="font-mono text-[11.5px] tabular-nums text-[var(--el-text-color-primary)]">{{ pctText(r.mem) }}</span>
              </span>
              <span
                v-else
                class="font-mono text-[11.5px] text-[var(--el-text-color-secondary)]"
              >
                —
              </span>
            </td>
            <td :class="[TD, 'text-right']">
              <span class="font-mono text-[11.5px] tabular-nums text-[var(--el-text-color-secondary)]">{{ r.services }}</span>
            </td>
            <td :class="TD">
              <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">{{ relTime(r.lastActive, Date.now()) }}</span>
            </td>
            <td :class="TD">
              <Btn
                size="sm"
                variant="ghost"
                data-spec="nodes-row-terminal"
                @click.stop="openTerm(r.id)"
              >
                终端
              </Btn>
            </td>
          </tr>
          <tr v-if="!filtered.length">
            <td
              colspan="8"
              class="px-[14px] py-[24px] text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
              data-spec="nodes-empty"
            >
              {{ loading ? '节点加载中…' : '没有匹配的节点' }}
            </td>
          </tr>
        </tbody>
      </table>
    </Card>

    <!-- 节点抽屉 -->
    <Drawer
      :open="drawerRow !== null"
      :title="drawerRow?.name ?? ''"
      :sub="drawerRow ? `${drawerRow.ip} · ${drawerRow.cluster}` : ''"
      @close="closeDrawer"
    >
      <template #actions>
        <Btn
          variant="primary"
          size="sm"
          data-spec="nodes-drawer-terminal"
          @click="drawerRow && openTerm(drawerRow.id)"
        >
          <template #icon>
            <KIcon
              name="Terminal"
              :size="12"
            />
          </template>
          打开终端
        </Btn>
        <Btn
          size="sm"
          disabled
          title="后端暂未提供批量操作接口"
          data-spec="nodes-drawer-upgrade"
        >
          升级 Agent
        </Btn>
        <Btn
          size="sm"
          disabled
          title="后端暂未提供批量操作接口"
          data-spec="nodes-drawer-dotnet"
        >
          安装 .NET
        </Btn>
      </template>

      <!-- tab 切换 -->
      <div
        class="mb-3 flex gap-[2px] border-b border-[var(--el-border-color-light)]"
        data-spec="nodes-drawer-tabs"
      >
        <button
          type="button"
          data-spec="nodes-drawer-tab"
          data-key="overview"
          :class="[
            '-mb-px h-[34px] border-b-2 border-solid px-[13px] text-[13px] font-medium transition-colors',
            drawerTab === 'overview'
              ? 'border-b-[var(--el-color-primary)] text-[var(--el-color-primary-light-3)]'
              : 'border-b-transparent text-[var(--el-text-color-secondary)] hover:text-[var(--el-text-color-primary)]',
          ]"
          @click="drawerTab = 'overview'"
        >
          概览
        </button>
        <button
          type="button"
          data-spec="nodes-drawer-tab"
          data-key="apps"
          :class="[
            '-mb-px h-[34px] border-b-2 border-solid px-[13px] text-[13px] font-medium transition-colors',
            drawerTab === 'apps'
              ? 'border-b-[var(--el-color-primary)] text-[var(--el-color-primary-light-3)]'
              : 'border-b-transparent text-[var(--el-text-color-secondary)] hover:text-[var(--el-text-color-primary)]',
          ]"
          @click="drawerTab = 'apps'"
        >
          应用进程 {{ appRows.length }}
        </button>
      </div>

      <!-- 概览 tab -->
      <div v-if="drawerTab === 'overview'">
        <Card
          padding="pad"
          class="mb-3"
          data-spec="nodes-drawer-cpu"
        >
          <h3 :class="H3">
            CPU %
          </h3>
          <div
            v-if="cpuLoading"
            class="py-6 text-center text-[12px] text-[var(--el-text-color-secondary)]"
          >
            <span class="mr-[6px] inline-block h-[13px] w-[13px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)] align-[-3px]" />
            走势加载中…
          </div>
          <div
            v-else-if="cpuPts.length > 0"
            class="py-2 text-[12px] text-[var(--el-text-color-secondary)]"
          >
            {{ cpuPts.length }} 个采样点（示意）
          </div>
          <div
            v-else
            data-spec="nodes-drawer-cpu-empty"
            class="py-6 text-center text-[12px] text-[var(--el-text-color-secondary)]"
          >
            CPU 走势待接入
          </div>
        </Card>

        <table class="w-full border-collapse">
          <tbody>
            <tr
              v-for="kv in drawerKvPairs"
              :key="kv.k"
              class="border-b border-[var(--el-border-color-light)] last:border-b-0"
            >
              <td class="w-[130px] px-[2px] py-[8px] text-[12px] text-[var(--el-text-color-secondary)]">
                {{ kv.k }}
              </td>
              <td class="px-[2px] py-[8px] font-mono text-[12px] text-[var(--el-text-color-primary)]">
                {{ kv.v }}
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- 应用进程 tab -->
      <div v-else>
        <div
          v-if="appsLoading"
          data-spec="nodes-drawer-apps-loading"
          class="py-8 text-center text-[12px] text-[var(--el-text-color-secondary)]"
        >
          <span class="mr-[6px] inline-block h-[13px] w-[13px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)] align-[-3px]" />
          应用加载中…
        </div>
        <div
          v-else-if="appsError"
          data-spec="nodes-drawer-apps-error"
          class="py-4 text-[12px] text-[var(--el-color-danger)]"
        >
          应用列表加载失败：{{ appsError }}
        </div>
        <div
          v-else-if="appRows.length === 0"
          data-spec="nodes-drawer-apps-empty"
          class="py-8 text-center text-[12px] text-[var(--el-text-color-secondary)]"
        >
          该节点没有应用
        </div>
        <ul
          v-else
          class="m-0 list-none p-0"
        >
          <li
            v-for="a in appRows"
            :key="a.id"
            data-spec="nodes-drawer-app"
            class="flex items-center gap-[10px] border-b border-[var(--el-border-color-light)] px-[2px] py-[10px] last:border-b-0"
          >
            <span class="grid h-[28px] w-[28px] shrink-0 place-items-center rounded-[6px] bg-[color-mix(in_oklab,var(--el-color-primary)_12%,transparent)] font-mono text-[12px] text-[var(--el-color-primary-light-3)]">
              {{ a.name.slice(0, 1).toUpperCase() }}
            </span>
            <div class="min-w-0 flex-1">
              <div class="text-[12.5px] font-medium text-[var(--el-text-color-primary)]">
                {{ a.name }}
              </div>
              <div class="font-mono text-[10.5px] text-[var(--el-text-color-secondary)]">
                {{ a.process || '—' }}{{ a.pid !== null ? ` · PID ${a.pid}` : '' }}{{ a.ip ? ` · ${a.ip}` : '' }}
              </div>
            </div>
            <span class="font-mono text-[10.5px] text-[var(--el-text-color-secondary)]">{{ a.version || '—' }}</span>
          </li>
        </ul>
      </div>
    </Drawer>
  </div>
</template>
