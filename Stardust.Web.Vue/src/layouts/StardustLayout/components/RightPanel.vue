<script setup lang="ts">
/**
 * 右侧全局实时数据面板 RightPanel —— 按设计交接包 RightPanel.jsx 实现
 * ---------------------------------------------------------------------------
 * 设计真源：design-handoff/ui_kits/console/RightPanel.jsx（152 行）
 *   宽 320 · flexShrink 0 · 竖向 flex · gap 12 · padding「20px 20px 20px 0」
 *   四段：
 *     1) KPI 迷你卡组 2×2 —— 值 26px mono/500 + 标签 11.5px；底为 tone 渐变 + tone 描边
 *     2) 集群健康 —— 标题 + 「实时」亮点；6px 三段条；下方 11px 图例
 *     3) 最近事件 —— 可滚动列表；6px 发光圆点 + 12.5px 正文 + 10.5px 元信息（时间 · 类型）
 *     4) 快捷入口 —— 4 列网格按钮
 *   README：「右侧始终保留全局实时数据面板」，故挂载在布局层、全控制台常驻。
 *
 * 样式纪律：颜色零硬编码，全部由令牌经 color-mix 派生（与设计稿 oklch(.../0.10) 等价）。
 */
import { computed, onMounted, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { request } from '@newlifex/cube-vue/core/utils/request';
import { stardustContext } from '@/stores/stardustContext';
import {
  fetchRightPanel,
  type PanelEvent,
  type PanelKpi,
  type PanelTone,
  type RightPanelData,
} from '../rightPanel.model';

const router = useRouter();

const data = ref<RightPanelData>({
  kpis: [],
  health: { online: 0, warn: 0, offline: 0 },
  events: [],
  shortcuts: [],
});
const loading = ref(true);

async function reload(): Promise<void> {
  loading.value = true;
  try {
    data.value = await fetchRightPanel(request);
  } catch (e) {
    console.error('[RightPanel] 取数失败', e);
  } finally {
    loading.value = false;
  }
}

onMounted(reload);
watch(
  () => stardustContext.revision,
  () => void reload(),
);

/** tone → 令牌派生的前景/底色/描边（对齐设计稿 toneStyles） */
function toneVars(tone: PanelTone): Record<string, string> {
  const map: Record<PanelTone, string> = {
    brand: 'var(--brand-500)',
    cyan: 'var(--cyan-500)',
    warning: 'var(--warning-500)',
    success: 'var(--success-500)',
  };
  const base = map[tone];
  return {
    '--rp-fg': tone === 'brand' ? 'var(--brand-300)' : base,
    '--rp-bg': `color-mix(in oklab, ${base} 10%, transparent)`,
    '--rp-bd': `color-mix(in oklab, ${base} 28%, transparent)`,
  };
}

/** 事件 kind → 圆点色（对齐设计稿 eventColors） */
function dotVars(kind: PanelEvent['kind']): Record<string, string> {
  const map: Record<PanelEvent['kind'], string> = {
    ok: 'var(--success-500)',
    warn: 'var(--warning-500)',
    danger: 'var(--danger-500)',
    info: 'var(--cyan-500)',
  };
  return { '--rp-dot': map[kind] };
}

/** 健康条三段宽度（合计 100%） */
const healthSegs = computed(() => [
  { key: 'online', pct: data.value.health.online, color: 'var(--success-500)' },
  { key: 'warn', pct: data.value.health.warn, color: 'var(--warning-500)' },
  { key: 'offline', pct: data.value.health.offline, color: 'var(--danger-500)' },
]);

const hasHealth = computed(() => data.value.health.online + data.value.health.warn + data.value.health.offline > 0);

function go(path: string): void {
  void router.push(path);
}
</script>

<template>
  <aside
    class="rp"
    data-spec="right-panel"
  >
    <!-- 1) KPI 迷你卡组 2×2 -->
    <div
      class="rp__kpis"
      data-spec="rp-kpis"
    >
      <div
        v-for="k in data.kpis"
        :key="k.label"
        class="rp__kpi"
        data-spec="rp-kpi"
        :style="toneVars(k.tone)"
      >
        <div
          class="rp__kpi-value"
          data-spec="rp-kpi-value"
        >
          {{ k.value }}
        </div>
        <div
          class="rp__kpi-label"
          data-spec="rp-kpi-label"
        >
          {{ k.label }}
        </div>
      </div>
      <div
        v-if="!data.kpis.length"
        class="rp__kpi rp__kpi--empty"
        data-spec="rp-kpi"
      >
        <div class="rp__kpi-value">
          —
        </div>
        <div class="rp__kpi-label">
          暂无数据
        </div>
      </div>
    </div>

    <!-- 2) 集群健康 -->
    <section
      class="rp__card"
      data-spec="rp-health"
    >
      <header class="rp__card-head">
        <span class="rp__card-title">集群健康</span>
        <span class="rp__live">
          <span class="rp__live-dot" />
          实时
        </span>
      </header>
      <div
        class="rp__bar"
        data-spec="rp-health-bar"
      >
        <div
          v-for="s in healthSegs"
          :key="s.key"
          class="rp__bar-seg"
          :style="{ width: s.pct + '%', background: s.color }"
        />
      </div>
      <div
        class="rp__legend"
        data-spec="rp-health-legend"
      >
        <span class="rp__legend-item">
          <span
            class="rp__legend-dot"
            :style="{ background: 'var(--success-500)' }"
          />在线 {{ data.health.online }}%
        </span>
        <span class="rp__legend-item">
          <span
            class="rp__legend-dot"
            :style="{ background: 'var(--warning-500)' }"
          />告警 {{ data.health.warn }}%
        </span>
        <span class="rp__legend-item">
          <span
            class="rp__legend-dot"
            :style="{ background: 'var(--danger-500)' }"
          />离线 {{ data.health.offline }}%
        </span>
      </div>
      <p
        v-if="!hasHealth"
        class="rp__hint"
      >
        暂无节点心跳数据
      </p>
    </section>

    <!-- 3) 最近事件 -->
    <section
      class="rp__card rp__events"
      data-spec="rp-events"
    >
      <header class="rp__card-head rp__card-head--line">
        <span class="rp__card-title">最近事件</span>
        <span class="rp__spacer" />
        <span
          class="rp__count"
          data-spec="rp-event-count"
        >{{ data.events.length }} 条</span>
      </header>
      <div class="rp__events-body">
        <div
          v-for="(e, i) in data.events"
          :key="i"
          class="rp__event"
          data-spec="rp-event"
          :style="dotVars(e.kind)"
        >
          <span
            class="rp__event-dot"
            data-spec="rp-event-dot"
          />
          <div class="rp__event-main">
            <div
              class="rp__event-msg"
              data-spec="rp-event-msg"
            >
              {{ e.msg }}
            </div>
            <div class="rp__event-meta">
              <span>{{ e.t }}</span>
              <span class="rp__event-sep">·</span>
              <span>{{ e.tag }}</span>
            </div>
          </div>
        </div>
        <p
          v-if="!loading && !data.events.length"
          class="rp__hint rp__hint--pad"
        >
          暂无事件
        </p>
      </div>
    </section>

    <!-- 4) 快捷入口 -->
    <nav
      class="rp__shortcuts"
      data-spec="rp-shortcuts"
    >
      <button
        v-for="s in data.shortcuts"
        :key="s.path"
        type="button"
        class="rp__shortcut"
        data-spec="rp-shortcut"
        :title="s.label"
        @click="go(s.path)"
      >
        {{ s.label }}
      </button>
    </nav>
  </aside>
</template>

<style scoped>
/* 宽 320 · 竖向 flex · 设计稿 padding「20px 20px 20px 0」 */
.rp {
  width: 320px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 20px 20px 20px 0;
  min-height: 0;
  max-height: calc(100vh - 56px);
  position: sticky;
  top: 56px;
}

/* ---- 1) KPI 迷你卡 ---- */
.rp__kpis {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 10px;
}
.rp__kpi {
  padding: 14px 16px;
  border-radius: var(--radius-xl);
  background: linear-gradient(135deg, var(--rp-bg, transparent), var(--surface-1) 80%);
  border: 1px solid var(--rp-bd, var(--border-1));
  box-shadow: inset 0 1px 0 color-mix(in srgb, var(--fg-1) 5%, transparent);
  min-width: 0;
}
.rp__kpi--empty {
  --rp-bg: transparent;
  --rp-bd: var(--border-1);
}
.rp__kpi-value {
  font-family: var(--font-mono);
  font-size: 26px;
  font-weight: 500;
  line-height: 1;
  letter-spacing: -0.02em;
  font-variant-numeric: tabular-nums;
  color: var(--rp-fg, var(--fg-1));
}
.rp__kpi-label {
  margin-top: 6px;
  font-size: 11.5px;
  letter-spacing: 0.02em;
  color: var(--fg-3);
}

/* ---- 通用卡片 ---- */
.rp__card {
  padding: 14px 16px;
  border-radius: var(--radius-xl);
  background: var(--surface-1);
  border: 1px solid var(--border-1);
  min-width: 0;
}
.rp__card-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}
.rp__card-head--line {
  padding: 12px 16px;
  border-bottom: 1px solid var(--border-1);
}
.rp__card-title {
  font-size: 12.5px;
  font-weight: 500;
  color: var(--fg-1);
}
.rp__spacer {
  flex: 1;
}
.rp__live {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  font-size: 10.5px;
  color: var(--fg-3);
}
.rp__live-dot {
  width: 5px;
  height: 5px;
  border-radius: var(--radius-pill);
  background: var(--success-500);
  box-shadow: 0 0 4px var(--success-500);
}

/* ---- 2) 健康条 ---- */
.rp__bar {
  margin-top: 10px;
  height: 6px;
  border-radius: var(--radius-pill);
  background: var(--surface-3);
  overflow: hidden;
  display: flex;
}
.rp__bar-seg {
  height: 100%;
  transition: width var(--dur-medium) var(--ease-out);
}
.rp__legend {
  margin-top: 8px;
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
  font-size: 11px;
  color: var(--fg-2);
}
.rp__legend-item {
  display: inline-flex;
  align-items: center;
  gap: 5px;
}
.rp__legend-dot {
  width: 5px;
  height: 5px;
  border-radius: var(--radius-pill);
}

/* ---- 3) 事件流 ---- */
.rp__events {
  padding: 0;
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
}
.rp__count {
  font-family: var(--font-mono);
  font-size: 11px;
  color: var(--fg-3);
  white-space: nowrap;
}
.rp__events-body {
  overflow-y: auto;
  flex: 1;
  min-height: 0;
}
.rp__event {
  padding: 11px 16px;
  display: grid;
  grid-template-columns: 6px 1fr;
  gap: 10px;
  align-items: start;
  border-bottom: 1px solid var(--border-1);
}
.rp__event:last-child {
  border-bottom: none;
}
.rp__event-dot {
  width: 6px;
  height: 6px;
  margin-top: 6px;
  border-radius: var(--radius-pill);
  background: var(--rp-dot, var(--fg-3));
  box-shadow: 0 0 6px var(--rp-dot, transparent);
}
.rp__event-main {
  min-width: 0;
}
.rp__event-msg {
  font-size: 12.5px;
  line-height: 1.4;
  color: var(--fg-1);
}
.rp__event-meta {
  margin-top: 3px;
  display: flex;
  gap: 6px;
  align-items: center;
  font-size: 10.5px;
  color: var(--fg-3);
  white-space: nowrap;
}
.rp__event-sep {
  color: var(--fg-4);
}
.rp__hint {
  margin: 8px 0 0;
  font-size: 11px;
  color: var(--fg-4);
}
.rp__hint--pad {
  padding: 12px 16px;
  margin: 0;
}

/* ---- 4) 快捷入口 ---- */
.rp__shortcuts {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 8px;
}
.rp__shortcut {
  padding: 9px 6px;
  border-radius: var(--radius-md);
  background: var(--surface-1);
  border: 1px solid var(--border-1);
  color: var(--fg-2);
  font-family: var(--font-sans);
  font-size: 11.5px;
  font-weight: 500;
  white-space: nowrap;
  cursor: pointer;
  transition: background var(--dur-micro) var(--ease-out), color var(--dur-micro) var(--ease-out),
    border-color var(--dur-micro) var(--ease-out);
}
.rp__shortcut:hover {
  background: var(--surface-2);
  color: var(--fg-1);
  border-color: var(--border-2);
}
</style>
