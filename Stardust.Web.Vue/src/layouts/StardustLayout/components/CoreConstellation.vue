<script setup lang="ts">
/**
 * 服务星座 CoreConstellation —— 按设计交接包 CoreConstellation.jsx 实现
 * ---------------------------------------------------------------------------
 * 设计真源：design-handoff/ui_kits/console/CoreConstellation.jsx + README §2
 *   中心核心 132px：品牌紫环 + 内层发光 + Logo mark，3s 呼吸（corePulse）
 *   外围模块节点 92px：圆形玻璃卡片，自动均布在 42% × min(w,h) 半径圆周上
 *   节点内容：图标 + 中文标签 + 英文提示
 *   节点徽标：绝对定位 top:-4 right:-4，发光边
 *   spoke：中心到每个节点的 SVG 虚线；激活项转实线亮色
 *
 * README 实现建议：「用 ResizeObserver 计算容器尺寸，SVG 画环+连线，DOM 定位节点」
 * —— 本组件按此实现：px 精确定位，容器任意宽高比下节点都严格落在同一圆周。
 *
 * 样式纪律：颜色一律取自设计令牌，不出现字面色值。
 */
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import logoMark from '@/assets/brand/logo-mark.svg';

interface ConstellationNode {
  key: string
  label: string
  sub?: string
  icon: string
  path: string
  badge?: string | number
  online?: boolean
}

const props = withDefaults(defineProps<{
  nodes: ConstellationNode[]
  center?: { label: string; sub?: string }
}>(), {
  center: () => ({ label: '星尘核心', sub: 'Stardust' }),
})

const emit = defineEmits<{ (e: 'select', path: string): void }>()

const host = ref<HTMLElement | null>(null);
const size = ref({ w: 0, h: 0 });
let ro: ResizeObserver | null = null;

onMounted(() => {
  if (!host.value) return;
  const rect = host.value.getBoundingClientRect();
  size.value = { w: rect.width, h: rect.height };
  ro = new ResizeObserver((entries) => {
    const r = entries[0].contentRect;
    size.value = { w: r.width, h: r.height };
  });
  ro.observe(host.value);
});

onBeforeUnmount(() => {
  ro?.disconnect();
  ro = null;
});

/** 激活（hover / 键盘聚焦）节点 key —— spoke 变实线亮色 */
const activeKey = ref('');

/** 轨道半径 = 42% × min(宽, 高)（设计稿原式） */
const radius = computed(() => 0.42 * Math.min(size.value.w, size.value.h));
const cx = computed(() => size.value.w / 2);
const cy = computed(() => size.value.h / 2);

const patches = computed(() =>
  props.nodes.map((n, i) => {
    /* 从正上方起顺时针均布：-90° 起步 */
    const angle = -Math.PI / 2 + (2 * Math.PI * i) / Math.max(props.nodes.length, 1);
    return {
      n,
      x: cx.value + radius.value * Math.cos(angle),
      y: cy.value + radius.value * Math.sin(angle),
    };
  }),
);

function pick(n: ConstellationNode) {
  activeKey.value = '';
  emit('select', n.path);
}
</script>

<template>
  <div
    ref="host"
    class="cc-root"
    data-spec="core-constellation"
  >
    <!-- 连线层：轨道环 + 中心辐射 spoke -->
    <svg
      class="cc-lines"
      :viewBox="`0 0 ${size.w || 1} ${size.h || 1}`"
      preserveAspectRatio="none"
      aria-hidden="true"
    >
      <!-- 外圈虚线轨道 -->
      <circle
        :cx="cx"
        :cy="cy"
        :r="radius"
        fill="none"
        stroke="var(--border-1)"
        stroke-width="1"
        stroke-dasharray="3 5"
      />
      <circle
        :cx="cx"
        :cy="cy"
        :r="radius * 0.62"
        fill="none"
        stroke="var(--border-1)"
        stroke-width="1"
        stroke-dasharray="2 6"
        opacity="0.6"
      />
      <!-- spoke：虚线；激活项实线 + 品牌色 -->
      <line
        v-for="p in patches"
        :key="`spoke-${p.n.key}`"
        :x1="cx"
        :y1="cy"
        :x2="p.x"
        :y2="p.y"
        :stroke="activeKey === p.n.key ? 'var(--brand-500)' : 'var(--border-2)'"
        :stroke-width="activeKey === p.n.key ? 1.5 : 1"
        :stroke-dasharray="activeKey === p.n.key ? 'none' : '2 4'"
        :opacity="activeKey === p.n.key ? 1 : 0.75"
      />
    </svg>

    <!-- 中心核心 132px -->
    <button
      type="button"
      class="cc-core"
      data-spec="topo-core"
      :title="`${center.label}${center.sub ? ' · ' + center.sub : ''}`"
    >
      <span class="cc-core-inner">
        <img
          :src="logoMark"
          class="cc-core-mark"
          alt=""
          aria-hidden="true"
        >
        <span class="cc-core-label">{{ center.label }}</span>
        <span
          v-if="center.sub"
          class="cc-core-sub"
        >{{ center.sub }}</span>
      </span>
    </button>

    <!-- 外围模块节点 92px -->
    <button
      v-for="p in patches"
      :key="p.n.key"
      type="button"
      data-spec="module-node"
      class="cc-node"
      :class="{ 'is-active': activeKey === p.n.key }"
      :style="{ left: `${p.x}px`, top: `${p.y}px` }"
      :title="`${p.n.label}${p.n.sub ? ' · ' + p.n.sub : ''}`"
      @mouseenter="activeKey = p.n.key"
      @mouseleave="activeKey = ''"
      @focus="activeKey = p.n.key"
      @blur="activeKey = ''"
      @click="pick(p.n)"
    >
      <svg
        class="cc-node-icon"
        width="20"
        height="20"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="1.5"
        stroke-linecap="round"
        stroke-linejoin="round"
        aria-hidden="true"
      >
        <path :d="p.n.icon" />
      </svg>
      <span class="cc-node-label">{{ p.n.label }}</span>
      <span
        v-if="p.n.sub"
        class="cc-node-sub"
      >{{ p.n.sub }}</span>
      <span
        v-if="p.n.badge !== undefined && p.n.badge !== ''"
        class="cc-node-badge"
      >{{ p.n.badge }}</span>
    </button>
  </div>
</template>

<style scoped>
.cc-root {
  position: relative;
  width: 100%;
  height: 100%;
  min-height: 320px;
}

.cc-lines {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  pointer-events: none;
}

/* ---- 中心核心 ---- */
.cc-core {
  position: absolute;
  left: 50%;
  top: 50%;
  width: 132px;
  height: 132px;
  transform: translate(-50%, -50%);
  border-radius: var(--radius-full);
  border: 1px solid color-mix(in srgb, var(--brand-500) 55%, transparent);
  background: radial-gradient(
    circle,
    color-mix(in srgb, var(--brand-500) 42%, transparent),
    color-mix(in srgb, var(--brand-500) 8%, transparent) 70%
  );
  box-shadow: 0 0 32px color-mix(in srgb, var(--brand-500) 35%, transparent), var(--elev-2);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: default;
  animation: ccCorePulse 3s var(--ease-in-out) infinite;
  z-index: 3;
}
.cc-core-inner {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
}
.cc-core-mark {
  width: 34px;
  height: 34px;
}
.cc-core-label {
  font-size: 13px;
  font-weight: 600;
  color: var(--fg-1);
  letter-spacing: -0.01em;
}
.cc-core-sub {
  font-family: var(--font-mono);
  font-size: 9px;
  letter-spacing: 0.04em;
  color: var(--fg-3);
  text-transform: uppercase;
}

/* 呼吸：scale 1 → 1.04 → 1（设计稿 corePulse 3s） */
@keyframes ccCorePulse {
  0%,
  100% { transform: translate(-50%, -50%) scale(1); }
  50% { transform: translate(-50%, -50%) scale(1.04); }
}

/* ---- 外围模块节点 ---- */
.cc-node {
  position: absolute;
  width: 92px;
  height: 92px;
  transform: translate(-50%, -50%);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 2px;
  border-radius: var(--radius-full);
  border: 1px solid var(--border-2);
  background: color-mix(in srgb, var(--surface-1) 82%, transparent);
  backdrop-filter: blur(8px);
  color: var(--fg-2);
  cursor: pointer;
  transition: transform var(--dur-short) var(--ease-out), border-color var(--dur-short) var(--ease-out),
    box-shadow var(--dur-short) var(--ease-out), color var(--dur-short) var(--ease-out);
  z-index: 2;
}
.cc-node:hover,
.cc-node.is-active {
  transform: translate(-50%, -50%) scale(1.06);
  border-color: color-mix(in srgb, var(--brand-500) 60%, transparent);
  box-shadow: 0 0 20px color-mix(in srgb, var(--brand-500) 35%, transparent);
  color: var(--fg-1);
}
.cc-node-icon {
  color: var(--brand-300);
}
.cc-node-label {
  font-size: 12px;
  font-weight: 600;
  line-height: 1.2;
}
.cc-node-sub {
  font-family: var(--font-mono);
  font-size: 9px;
  line-height: 1.2;
  letter-spacing: 0.04em;
  color: var(--fg-3);
}

/* 徽标：右上角外挂 + 发光边 */
.cc-node-badge {
  position: absolute;
  top: -4px;
  right: -4px;
  min-width: 20px;
  height: 18px;
  padding: 0 5px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: var(--radius-pill);
  background: var(--brand-500);
  color: #ffffff;
  font-family: var(--font-mono);
  font-size: 10px;
  font-weight: 600;
  line-height: 1;
  box-shadow: 0 0 10px color-mix(in srgb, var(--brand-500) 65%, transparent);
}

/* 无障碍：减少动态偏好下关闭呼吸 */
@media (prefers-reduced-motion: reduce) {
  .cc-core {
    animation: none;
  }
  .cc-node {
    transition: none;
  }
}
</style>
