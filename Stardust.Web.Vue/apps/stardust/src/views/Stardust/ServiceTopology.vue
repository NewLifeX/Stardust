<script setup lang="ts">
/**
 * 服务拓扑 ServiceTopology —— 按设计交接包 page-services.jsx 的 SimpleTopology 实现
 * ---------------------------------------------------------------------------
 * 设计真源：design-handoff/src/page-services.jsx（SimpleTopology）+ mock.jsx（topologyNodes/Links）
 *   形态：工程化矩形拓扑（非星空），Panel 标题「调用依赖」
 *   连线：入口右边缘 → 目标左边缘，三次贝塞尔（C mx y1, mx y2, x2 y2），粗细按权重，末端箭头
 *   节点：rect rx=4 + 左上状态圆点(r=3) + 居中标签(11px/500)
 *   着色：alert→danger、warn→warning、data→cyan、edge→brand、其余→success
 *
 * 样式纪律：颜色零硬编码，全部取设计令牌。
 * 诚实标注：后端注册中心数据无法区分「数据节点(data)」，故实际只产出 edge / svc 两类；
 *           亦无按边 QPS，连线粗细改按「两个应用间共享的服务数」加权。
 */
import { computed } from 'vue'
import EmptyState from '@/layouts/StardustLayout/components/EmptyState.vue'
import type { TopoKind, TopoNode, Topology } from './service.model'

const props = defineProps<{ topology: Topology }>()

/** 状态/类别 → 描边色（对齐设计稿 colorFor） */
function toneOf(n: TopoNode): string {
  if (n.status === 'danger') return 'var(--danger-500)'
  if (n.status === 'warn') return 'var(--warning-500)'
  if (n.kind === 'data') return 'var(--cyan-500)'
  if (n.kind === 'edge') return 'var(--brand-500)'
  return 'var(--success-500)'
}

/** 类别 → 填充色（对齐设计稿 bgFor） */
function bgOf(n: TopoNode): string {
  if (n.kind === 'data') return 'var(--surface-2)'
  if (n.kind === 'edge') return 'color-mix(in oklab, var(--brand-500) 14%, var(--surface-2))'
  return 'var(--surface-2)'
}

/** 连线最大权重（用于归一化粗细） */
const maxWeight = computed(() => {
  const ws = props.topology.links.map((l) => l.weight)
  return ws.length ? Math.max(...ws) : 1
})

/** 节点 id → 节点（连线端点计算用） */
const byId = computed(() => {
  const map: Record<string, TopoNode> = {}
  for (const n of props.topology.nodes) map[n.id] = n
  return map
})

/** 连线几何：从 a 右边缘中点到 b 左边缘中点，中点做贝塞尔控制点 */
const paths = computed(() =>
  props.topology.links
    .map((l, i) => {
      const a = byId.value[l.a]
      const b = byId.value[l.b]
      if (!a || !b) return null
      const x1 = a.x + a.w
      const y1 = a.y + a.h / 2
      const x2 = b.x
      const y2 = b.y + b.h / 2
      const mx = (x1 + x2) / 2
      return {
        key: `${l.a}->${l.b}#${i}`,
        d: `M${x1} ${y1} C ${mx} ${y1}, ${mx} ${y2}, ${x2} ${y2}`,
        width: Number((0.6 + (l.weight / maxWeight.value) * 2.2).toFixed(2)),
      }
    })
    .filter((p): p is { key: string; d: string; width: number } => p !== null),
)

/** 节点状态计数 → 面板徽章（设计稿：N healthy / N degraded / N down） */
const badges = computed(() => {
  let ok = 0
  let warn = 0
  let danger = 0
  for (const n of props.topology.nodes) {
    if (n.status === 'danger') danger++
    else if (n.status === 'warn') warn++
    else ok++
  }
  const out: { tone: string; text: string }[] = []
  if (ok) out.push({ tone: 'ok', text: `${ok} 健康` })
  if (warn) out.push({ tone: 'warn', text: `${warn} 降级` })
  if (danger) out.push({ tone: 'danger', text: `${danger} 异常` })
  return out
})

const hasGraph = computed(() => props.topology.nodes.length > 0)

/** 节点类别 → 中文说明（图例用） */
const LEGEND: { kind: TopoKind; label: string }[] = [
  { kind: 'edge', label: '入口应用' },
  { kind: 'svc', label: '业务服务' },
  { kind: 'data', label: '数据节点' },
]
</script>

<template>
  <section
    class="sd-topo"
    data-spec="topo-panel"
  >
    <header class="sd-topo__head">
      <div class="sd-topo__titles">
        <div
          class="sd-topo__title"
          data-spec="topo-panel-title"
        >
          调用依赖
        </div>
        <div
          class="sd-topo__sub"
          data-spec="topo-panel-sub"
        >
          按调用服务数加权
        </div>
      </div>
      <span class="sd-topo__spacer" />
      <span
        v-for="b in badges"
        :key="b.tone"
        class="sd-topo__badge"
        :class="'is-' + b.tone"
        data-spec="topo-badge"
      >
        <span class="sd-topo__badge-dot" />
        {{ b.text }}
      </span>
    </header>

    <div class="sd-topo__body">
      <svg
        v-if="hasGraph"
        class="sd-topo__svg"
        data-spec="topo-svg"
        :viewBox="`0 0 ${topology.width} ${topology.height}`"
        width="100%"
        :height="topology.height"
        preserveAspectRatio="xMidYMid meet"
      >
        <defs>
          <marker
            id="sd-topo-arr"
            markerWidth="6"
            markerHeight="6"
            refX="5"
            refY="3"
            orient="auto"
          >
            <path
              class="sd-topo__arrow"
              d="M0,0 L6,3 L0,6 z"
            />
          </marker>
        </defs>

        <!-- 连线 -->
        <path
          v-for="p in paths"
          :key="p.key"
          class="sd-topo__link"
          data-spec="topo-link"
          :d="p.d"
          :stroke-width="p.width"
          fill="none"
          marker-end="url(#sd-topo-arr)"
        />

        <!-- 节点 -->
        <g
          v-for="n in topology.nodes"
          :key="n.id"
          data-spec="topo-node"
        >
          <rect
            :x="n.x"
            :y="n.y"
            :width="n.w"
            :height="n.h"
            rx="4"
            :style="{ fill: bgOf(n), stroke: toneOf(n) }"
            :stroke-width="n.status === 'ok' ? 1 : 1.5"
          />
          <circle
            :cx="n.x + 10"
            :cy="n.y + 10"
            r="3"
            :style="{ fill: toneOf(n) }"
          />
          <text
            class="sd-topo__label"
            :x="n.x + n.w / 2"
            :y="n.y + n.h / 2 + 4"
            text-anchor="middle"
          >{{ n.label }}</text>
        </g>
      </svg>

      <EmptyState
        v-else
        :icon="'M12 2v20M2 12h20'"
        title="暂无调用依赖"
        hint="服务注册并产生消费关系后，此处按调用关系绘制拓扑"
      />
    </div>

    <footer
      v-if="hasGraph"
      class="sd-topo__legend"
    >
      <span
        v-for="l in LEGEND"
        :key="l.kind"
        class="sd-topo__legend-item"
      >
        <span
          class="sd-topo__legend-swatch"
          :class="'is-' + l.kind"
        />
        {{ l.label }}
      </span>
      <span class="sd-topo__legend-sep" />
      <span class="sd-topo__legend-note">{{ topology.nodes.length }} 节点 · {{ topology.links.length }} 调用关系</span>
    </footer>
  </section>
</template>


<style scoped>
.sd-topo {
  display: flex;
  flex-direction: column;
  min-width: 0;
  border-radius: var(--radius-lg);
  border: 1px solid var(--border-1);
  background: color-mix(in srgb, var(--surface-1) 80%, transparent);
  box-shadow: var(--elev-1);
  overflow: hidden;
}
.sd-topo__head {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 12px 16px;
  border-bottom: 1px solid var(--border-1);
}
.sd-topo__titles {
  min-width: 0;
}
.sd-topo__title {
  font-size: 14px;
  font-weight: 500;
  color: var(--fg-1);
}
.sd-topo__sub {
  margin-top: 2px;
  font-size: 12px;
  color: var(--fg-3);
}
.sd-topo__spacer {
  flex: 1;
}
.sd-topo__badge {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 3px 9px;
  border-radius: var(--radius-pill);
  font-size: 11.5px;
  font-weight: 500;
  white-space: nowrap;
  border: 1px solid transparent;
}
.sd-topo__badge-dot {
  width: 6px;
  height: 6px;
  border-radius: var(--radius-pill);
}
.sd-topo__badge.is-ok {
  color: var(--success-500);
  background: color-mix(in srgb, var(--success-500) 15%, transparent);
  border-color: color-mix(in srgb, var(--success-500) 30%, transparent);
}
.sd-topo__badge.is-ok .sd-topo__badge-dot {
  background: var(--success-500);
}
.sd-topo__badge.is-warn {
  color: var(--warning-500);
  background: color-mix(in srgb, var(--warning-500) 15%, transparent);
  border-color: color-mix(in srgb, var(--warning-500) 30%, transparent);
}
.sd-topo__badge.is-warn .sd-topo__badge-dot {
  background: var(--warning-500);
}
.sd-topo__badge.is-danger {
  color: var(--danger-500);
  background: color-mix(in srgb, var(--danger-500) 15%, transparent);
  border-color: color-mix(in srgb, var(--danger-500) 30%, transparent);
}
.sd-topo__badge.is-danger .sd-topo__badge-dot {
  background: var(--danger-500);
}

/* 画布：设计稿 radial-gradient 底（数据从中心微亮向四周渐暗） */
.sd-topo__body {
  padding: 16px;
  background: radial-gradient(circle at 50% 40%, color-mix(in srgb, var(--surface-2) 70%, var(--bg)) 0%, var(--bg) 70%);
}
.sd-topo__svg {
  display: block;
}
.sd-topo__link {
  stroke: var(--border-2);
}
.sd-topo__arrow {
  fill: var(--border-strong, var(--border-2));
}
.sd-topo__label {
  font-family: var(--font-sans);
  font-size: 11px;
  font-weight: 500;
  fill: var(--fg-1);
}

/* 图例 */
.sd-topo__legend {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 12px;
  padding: 10px 16px;
  border-top: 1px solid var(--border-1);
  font-size: 11px;
  color: var(--fg-3);
}
.sd-topo__legend-item {
  display: inline-flex;
  align-items: center;
  gap: 5px;
}
.sd-topo__legend-swatch {
  width: 10px;
  height: 10px;
  border-radius: 2px;
  border: 1px solid;
}
.sd-topo__legend-swatch.is-edge {
  border-color: var(--brand-500);
  background: color-mix(in oklab, var(--brand-500) 14%, var(--surface-2));
}
.sd-topo__legend-swatch.is-svc {
  border-color: var(--success-500);
  background: var(--surface-2);
}
.sd-topo__legend-swatch.is-data {
  border-color: var(--cyan-500);
  background: var(--surface-2);
}
.sd-topo__legend-sep {
  flex: 1;
}
.sd-topo__legend-note {
  font-family: var(--font-mono);
  font-variant-numeric: tabular-nums;
}
</style>
