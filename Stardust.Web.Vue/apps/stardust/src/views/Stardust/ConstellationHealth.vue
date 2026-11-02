<script setup lang="ts">
/**
 * 应用星座面板（设计稿 src/page-workspace.jsx · ConstellationHealth）
 *
 * 三视图（右上 segmented 切换）：
 *   星座 —— 健康度核心 + 应用卫星（椭圆轨道 + spoke + 图例）
 *   拓扑 —— 应用间有向依赖边（放射布局，带箭头）
 *   矩阵 —— 调用方 × 被调方 热力矩阵（TopologyMatrix）
 *
 * 坐标纪律：SVG 用 viewBox 0 0 100 100 + preserveAspectRatio=none，
 * 与 HTML 节点的 left/top 百分比共用同一坐标系，spoke 与节点严格对齐。
 * 颜色只用 --el-* 令牌，禁止硬编码。
 */
import { ref, computed } from 'vue'
import TopologyMatrix from './TopologyMatrix.vue'
import type { WsHealth, WsSatellite, WsMatrix, Topology } from './overview.model'

const props = defineProps<{
  health: WsHealth
  satellites: WsSatellite[]
  matrix: WsMatrix
  topology: Topology
}>()

defineEmits<{ 'select-app': [name: string] }>()

type ViewKey = 'constellation' | 'topology' | 'matrix'

const views: { key: ViewKey; label: string }[] = [
  { key: 'constellation', label: '星座' },
  { key: 'topology', label: '拓扑' },
  { key: 'matrix', label: '矩阵' },
]
const view = ref<ViewKey>('constellation')

const subText = computed(() => {
  if (view.value === 'constellation') {
    return '显示项目下 ' + props.satellites.length + ' 个关键应用的健康状况 · 点击进入应用列表'
  }
  if (view.value === 'topology') return '按调用依赖展开 · 有向边指向被调方'
  return '调用方 × 被调方 · 色块深浅对应 QPS'
})

const footer = computed(() => {
  if (view.value === 'constellation') return 'orbit-1 · ' + props.satellites.length + ' nodes'
  if (view.value === 'topology') {
    return props.topology.nodes.length + ' nodes · ' + (props.topology.edges ?? []).length + ' edges'
  }
  return props.matrix.edgeCount + ' 条调用边'
})

function dotColor(s: WsSatellite): string {
  if (s.status === 'alert') return 'var(--el-color-danger)'
  if (s.status === 'warn') return 'var(--el-color-warning)'
  if (s.status === 'offline') return 'var(--el-text-color-placeholder)'
  return 'var(--el-color-success)'
}

function spokeColor(s: WsSatellite): string {
  if (s.status === 'alert') return 'var(--el-color-danger)'
  if (s.status === 'warn') return 'var(--el-color-warning)'
  return 'var(--el-border-color)'
}

function nodeStyle(s: WsSatellite): Record<string, string> {
  return { left: 50 + s.dx + '%', top: 50 + s.dy + '%' }
}

function meta(s: WsSatellite): string {
  const qps = s.qps >= 1000 ? (s.qps / 1000).toFixed(1) + 'k' : s.qps.toFixed(1)
  return qps + ' QPS · ' + s.inst + ' 实例'
}

/** 拓扑视图：节点沿椭圆均布（百分比），边为三次贝塞尔 + 箭头 */
const topoNodes = computed(() => {
  const list = props.topology.nodes.slice(0, 10)
  const n = list.length
  return list.map((node, i) => {
    const angle = n > 0 ? (i / n) * Math.PI * 2 - Math.PI / 2 : 0
    return {
      node,
      x: Number((50 + Math.cos(angle) * 20).toFixed(2)),
      y: Number((50 + Math.sin(angle) * 26).toFixed(2)),
    }
  })
})

const topoEdges = computed(() => {
  const pos = new Map<string, { x: number; y: number }>()
  for (const t of topoNodes.value) {
    if (t.node.name) pos.set(t.node.name, { x: t.x, y: t.y })
  }
  const out: { key: string; d: string }[] = []
  for (const e of props.topology.edges ?? []) {
    const a = pos.get(e.from)
    const b = pos.get(e.to)
    if (!a || !b) continue
    const mx = ((a.x + b.x) / 2).toFixed(2)
    out.push({
      key: e.from + '->' + e.to,
      d: 'M ' + a.x + ' ' + a.y + ' C ' + mx + ' ' + a.y + ', ' + mx + ' ' + b.y + ', ' + b.x + ' ' + b.y,
    })
  }
  return out
})
</script>

<template>
  <div
    class="sd-cn"
    data-spec="ws-constellation"
  >
    <div class="sd-cn-header">
      <div class="sd-cn-head-l">
        <div
          class="sd-cn-live"
          data-spec="ws-cn-live"
        >
          <span class="sd-cn-live-dot" />实时健康 · Live
        </div>
        <div
          class="sd-cn-sub"
          data-spec="ws-cn-sub"
        >
          {{ subText }}
        </div>
      </div>
      <div class="sd-cn-switch">
        <button
          v-for="v in views"
          :key="v.key"
          type="button"
          class="sd-cn-tab"
          :class="{ 'is-active': view === v.key }"
          :data-spec="'ws-cn-tab-' + v.key"
          @click="view = v.key"
        >
          {{ v.label }}
        </button>
      </div>
    </div>

    <div
      v-if="view === 'constellation'"
      class="sd-cn-stage"
    >
      <svg
        class="sd-cn-svg"
        viewBox="0 0 100 100"
        preserveAspectRatio="none"
        aria-hidden="true"
      >
        <defs>
          <radialGradient id="sd-cn-glow">
            <stop
              offset="0"
              stop-color="var(--el-color-primary)"
              stop-opacity="0.4"
            />
            <stop
              offset="45%"
              stop-color="var(--el-color-primary)"
              stop-opacity="0.1"
            />
            <stop
              offset="100%"
              stop-color="var(--el-color-primary)"
              stop-opacity="0"
            />
          </radialGradient>
        </defs>
        <ellipse
          cx="50"
          cy="50"
          rx="26"
          ry="32"
          fill="url(#sd-cn-glow)"
        />
        <ellipse
          data-spec="ws-cn-orbit"
          cx="50"
          cy="50"
          rx="20"
          ry="26"
          fill="none"
          stroke="var(--el-border-color)"
          stroke-dasharray="2 6"
          vector-effect="non-scaling-stroke"
        />
        <ellipse
          data-spec="ws-cn-orbit"
          cx="50"
          cy="50"
          rx="11"
          ry="14"
          fill="none"
          stroke="var(--el-border-color-lighter)"
          vector-effect="non-scaling-stroke"
        />
        <line
          v-for="(s, i) in satellites"
          :key="'sp-' + i"
          data-spec="ws-cn-spoke"
          x1="50"
          y1="50"
          :x2="50 + s.dx"
          :y2="50 + s.dy"
          :stroke="spokeColor(s)"
          :stroke-width="s.status === 'alert' ? 1.2 : 0.8"
          :stroke-dasharray="s.status === 'online' ? '3 6' : ''"
          vector-effect="non-scaling-stroke"
        />
      </svg>

      <div
        class="sd-cn-core"
        data-spec="ws-cn-core"
      >
        <div
          class="sd-cn-core-value"
          data-spec="ws-cn-core-value"
        >
          {{ health.score }}<span class="sd-cn-core-unit">/100</span>
        </div>
        <div
          class="sd-cn-core-label"
          data-spec="ws-cn-core-label"
        >
          健康度
        </div>
      </div>

      <button
        v-for="(s, i) in satellites"
        :key="s.name + '-' + i"
        type="button"
        class="sd-cn-node"
        :class="'is-' + s.status"
        data-spec="ws-cn-node"
        :style="nodeStyle(s)"
        :title="s.label"
        @click="$emit('select-app', s.name)"
      >
        <span class="ws-cn-node-title"><span
          class="sd-cn-node-dot"
          :style="{ background: dotColor(s) }"
        />{{ s.label }}</span>
        <span class="ws-cn-node-meta">{{ meta(s) }}</span>
      </button>
    </div>

    <div
      v-else-if="view === 'topology'"
      class="sd-cn-stage"
      data-spec="ws-topo"
    >
      <svg
        class="sd-cn-svg"
        viewBox="0 0 100 100"
        preserveAspectRatio="none"
        aria-hidden="true"
      >
        <defs>
          <marker
            id="sd-cn-arrow"
            viewBox="0 0 10 10"
            refX="9"
            refY="5"
            markerWidth="6"
            markerHeight="6"
            orient="auto-start-reverse"
          >
            <path
              d="M 0 0 L 10 5 L 0 10 z"
              fill="var(--el-color-primary)"
            />
          </marker>
        </defs>
        <path
          v-for="e in topoEdges"
          :key="e.key"
          :d="e.d"
          fill="none"
          stroke="var(--el-color-primary)"
          stroke-opacity="0.5"
          stroke-width="1"
          marker-end="url(#sd-cn-arrow)"
          vector-effect="non-scaling-stroke"
        />
      </svg>
      <div
        v-for="(t, i) in topoNodes"
        :key="'tn-' + i"
        class="sd-cn-node is-online"
        :style="{ left: t.x + '%', top: t.y + '%' }"
      >
        <span class="ws-cn-node-title"><span
          class="sd-cn-node-dot"
          :style="{ background: t.node.online ? 'var(--el-color-success)' : 'var(--el-color-danger)' }"
        />{{ t.node.label }}</span>
        <span class="ws-cn-node-meta">{{ t.node.sub }}</span>
      </div>
    </div>

    <div
      v-else
      class="sd-cn-body"
    >
      <TopologyMatrix :matrix="matrix" />
    </div>

    <div
      class="sd-cn-legend"
      data-spec="ws-cn-legend"
    >
      <span><i class="sd-cn-lg is-online" />正常</span>
      <span><i class="sd-cn-lg is-warn" />告警</span>
      <span><i class="sd-cn-lg is-alert" />异常</span>
      <span class="sd-cn-spacer" />
      <span
        class="sd-cn-foot"
        data-spec="ws-cn-footer"
      >{{ footer }}</span>
    </div>
  </div>
</template>

<style scoped>
.sd-cn {
  position: relative;
  border: 1px solid var(--el-border-color-light);
  border-radius: 16px;
  overflow: hidden;
  background: color-mix(in srgb, var(--el-bg-color) 70%, transparent);
}
.sd-cn-header {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 14px 18px;
  border-bottom: 1px solid var(--el-border-color-light);
}
.sd-cn-head-l {
  min-width: 0;
}
.sd-cn-live {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  font-weight: 500;
  color: var(--el-text-color-primary);
}
.sd-cn-live-dot {
  width: 6px;
  height: 6px;
  border-radius: 999px;
  background: var(--el-color-success);
  box-shadow: 0 0 6px var(--el-color-success);
}
.sd-cn-sub {
  margin-top: 4px;
  font-size: 11px;
  color: var(--el-text-color-secondary);
}
.sd-cn-switch {
  margin-left: auto;
  display: flex;
  gap: 2px;
  padding: 3px;
  border-radius: 8px;
  border: 1px solid var(--el-border-color-light);
  background: color-mix(in srgb, var(--el-bg-color-overlay) 70%, transparent);
}
.sd-cn-tab {
  padding: 5px 12px;
  border-radius: 6px;
  font-size: 12.5px;
  color: var(--el-text-color-regular);
  transition: background-color 120ms cubic-bezier(0.16, 1, 0.3, 1), color 120ms;
}
.sd-cn-tab.is-active {
  background: var(--el-color-primary);
  color: var(--el-color-white);
}
.sd-cn-stage {
  position: relative;
  height: 520px;
}
.sd-cn-svg {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
}
.sd-cn-core {
  position: absolute;
  left: 50%;
  top: 50%;
  transform: translate(-50%, -50%);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  width: 132px;
  height: 132px;
  border-radius: 999px;
  border: 1px solid var(--el-color-primary);
  background: color-mix(in srgb, var(--el-color-primary) 18%, transparent);
}
.sd-cn-core-value {
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
  font-size: 28px;
  font-weight: 700;
  color: var(--el-text-color-primary);
  font-variant-numeric: tabular-nums;
}
.sd-cn-core-unit {
  font-size: 12px;
  font-weight: 400;
  color: var(--el-text-color-secondary);
}
.sd-cn-core-label {
  margin-top: 2px;
  font-size: 11px;
  color: var(--el-text-color-secondary);
}
.sd-cn-node {
  position: absolute;
  transform: translate(-50%, -50%);
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
  padding: 6px 10px;
  border-radius: 10px;
  border: 1px solid var(--el-border-color-light);
  background: color-mix(in srgb, var(--el-bg-color-overlay) 82%, transparent);
  transition: border-color 200ms cubic-bezier(0.16, 1, 0.3, 1), transform 200ms;
}
.sd-cn-node:hover {
  border-color: var(--el-color-primary);
  transform: translate(-50%, -50%) scale(1.06);
}
.sd-cn-node.is-alert {
  border-color: color-mix(in srgb, var(--el-color-danger) 45%, transparent);
}
.sd-cn-node.is-warn {
  border-color: color-mix(in srgb, var(--el-color-warning) 45%, transparent);
}
.ws-cn-node-title {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  font-weight: 500;
  color: var(--el-text-color-primary);
  white-space: nowrap;
}
.sd-cn-node-dot {
  width: 6px;
  height: 6px;
  border-radius: 999px;
  flex-shrink: 0;
}
.ws-cn-node-meta {
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
  font-size: 10.5px;
  color: var(--el-text-color-secondary);
  white-space: nowrap;
}
.sd-cn-body {
  padding: 16px;
}
.sd-cn-legend {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 10px 18px;
  border-top: 1px solid var(--el-border-color-light);
  font-size: 11.5px;
  color: var(--el-text-color-regular);
}
.sd-cn-lg {
  display: inline-block;
  width: 6px;
  height: 6px;
  border-radius: 999px;
  margin-right: 6px;
}
.sd-cn-lg.is-online {
  background: var(--el-color-success);
}
.sd-cn-lg.is-warn {
  background: var(--el-color-warning);
}
.sd-cn-lg.is-alert {
  background: var(--el-color-danger);
}
.sd-cn-spacer {
  flex: 1;
}
.sd-cn-foot {
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
  font-size: 10px;
  color: var(--el-text-color-placeholder);
}
</style>
