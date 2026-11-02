<script setup lang="ts">
/**
 * 星云背景 StarfieldBg —— 按设计交接包 StarField.jsx 重写
 * ---------------------------------------------------------------------------
 * 设计真源：design-handoff/ui_kits/console/StarField.jsx
 * 分层（自下而上）：
 *   1. 深空底        var(--bg) #0B0F1A
 *   2. 星云四层      径向渐变（紫 285 / 青 210 / 品红 305 / 蓝 240）
 *   3. 颗粒噪声      SVG feTurbulence + mix-blend-mode: overlay
 *   4. 120 颗星点    cx/cy 百分比定位，大星用径向渐变发光
 *   5. 两团 wisp     大尺度柔光椭圆
 *
 * 与设计稿的一处**刻意偏离**（有据可依）：
 *   设计稿给星点挂了 `starTwinkle 4s` 无限动画。但本层位于所有玻璃卡片
 *   （backdrop-filter: blur）之后，逐帧动画会强制 backdrop-filter 每帧重新
 *   合成，在 Chrome 下表现为整页持续闪烁——本仓库此前已在旧 StarfieldBg.vue
 *   记录过该问题（7s 呼吸动画 → 改静态层）。故此处星点取静态，闪烁感由
 *   星点透明度分层（0.15~0.74）与四层星云提供。
 *
 * 样式纪律：颜色一律取自设计令牌（tokens.stardust.css / --bg、--gradient），
 *          不出现字面色值（SVG 星点为纯白/渐变白，属图形资源例外）。
 */
withDefaults(defineProps<{
  /** 星云强度倍率（1 = 设计稿原值），低配环境可传 0.6 降载 */
  intensity?: number
  /** 星点数量，默认 120（与设计稿一致） */
  starCount?: number
}>(), {
  intensity: 1,
  starCount: 120,
})

/* 确定性伪随机：同一种子每次渲染星图一致，避免刷新跳变（设计稿同为确定性生成） */
const SEED = 0.42
function hash(i: number, salt: number): number {
  const s = Math.sin(i * salt + SEED * 78.233) * 43758.5453
  return s - Math.floor(s)
}

const stars = Array.from({ length: 120 }, (_, i) => ({
  x: hash(i, 12.9898) * 100,
  y: hash(i, 4.1234) * 100,
  /* 设计稿：每 7 颗一颗 2px 发光大星，每 3 颗一颗 1.4px 中星，其余 1px */
  r: i % 7 === 0 ? 2 : i % 3 === 0 ? 1.4 : 1,
  /* 设计稿：opacity = 0.15 + ((i * 37) % 60) / 100 → 0.15 ~ 0.74 */
  o: 0.15 + ((i * 37) % 60) / 100,
}))
</script>

<template>
  <!-- 铺满父级、裁剪溢出、不拦截交互 -->
  <div
    class="sd-starfield pointer-events-none absolute inset-0 overflow-hidden"
    aria-hidden="true"
  >
    <!-- 1 + 2：深空底 + 星云四层 -->
    <div
      class="sd-nebula absolute inset-0"
      :style="{ opacity: intensity }"
    />

    <!-- 3：颗粒噪声（打破渐变的塑料感） -->
    <div class="sd-grain absolute inset-0" />

    <!-- 4：星点（确定性分布，静态） -->
    <svg
      class="sd-stars absolute inset-0"
      width="100%"
      height="100%"
    >
      <defs>
        <radialGradient id="sdStarGlow">
          <stop
            offset="0%"
            stop-color="#ffffff"
            stop-opacity="0.9"
          />
          <stop
            offset="60%"
            stop-color="#ffffff"
            stop-opacity="0.3"
          />
          <stop
            offset="100%"
            stop-color="#ffffff"
            stop-opacity="0"
          />
        </radialGradient>
      </defs>
      <circle
        v-for="(s, i) in stars.slice(0, starCount)"
        :key="i"
        :cx="`${s.x}%`"
        :cy="`${s.y}%`"
        :r="s.r"
        :fill="s.r >= 2 ? 'url(#sdStarGlow)' : '#ffffff'"
        :opacity="s.o"
      />
    </svg>

    <!-- 5：两团 wisp 柔光 -->
    <svg
      class="sd-wisp absolute inset-0"
      width="100%"
      height="100%"
      viewBox="0 0 1000 700"
      preserveAspectRatio="xMidYMid slice"
    >
      <defs>
        <radialGradient
          id="sdWisp1"
          cx="0.5"
          cy="0.5"
          r="0.5"
        >
          <stop
            offset="0"
            style="stop-color: var(--sd-wisp-a)"
            stop-opacity="0.35"
          />
          <stop
            offset="1"
            style="stop-color: var(--sd-wisp-a)"
            stop-opacity="0"
          />
        </radialGradient>
        <radialGradient
          id="sdWisp2"
          cx="0.5"
          cy="0.5"
          r="0.5"
        >
          <stop
            offset="0"
            style="stop-color: var(--sd-wisp-b)"
            stop-opacity="0.3"
          />
          <stop
            offset="1"
            style="stop-color: var(--sd-wisp-b)"
            stop-opacity="0"
          />
        </radialGradient>
      </defs>
      <ellipse
        cx="500"
        cy="350"
        rx="380"
        ry="220"
        fill="url(#sdWisp1)"
      />
      <ellipse
        cx="620"
        cy="420"
        rx="280"
        ry="180"
        fill="url(#sdWisp2)"
      />
    </svg>
  </div>
</template>

<style scoped>
.sd-starfield {
  /* 深空底：设计稿 --bg #0B0F1A（本层是页面可视背景的唯一来源，
     因 StardustLayout 把 --el-bg-color-page 置为 transparent 以露出星空） */
  background: var(--bg);
  z-index: 0;
}

/* 星云四层：取值与设计稿 StarField.jsx 一致（强度倍率由外层 opacity 统一缩放） */
.sd-nebula {
  background:
    radial-gradient(ellipse 70% 50% at 30% 25%, var(--sd-nebula-1), transparent 60%),
    radial-gradient(ellipse 60% 45% at 75% 55%, var(--sd-nebula-2), transparent 60%),
    radial-gradient(ellipse 55% 40% at 20% 80%, var(--sd-nebula-3), transparent 55%),
    radial-gradient(ellipse 40% 30% at 85% 15%, var(--sd-nebula-4), transparent 60%);
}

/* 颗粒噪声：SVG feTurbulence，叠加模式 overlay */
.sd-grain {
  opacity: 0.4;
  mix-blend-mode: overlay;
  background-image: url("data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' width='120' height='120'><filter id='n'><feTurbulence baseFrequency='0.9' numOctaves='2' seed='7'/><feColorMatrix values='0 0 0 0 0.6  0 0 0 0 0.6  0 0 0 0 0.8  0 0 0 0.4 0'/></filter><rect width='100%25' height='100%25' filter='url(%23n)'/></svg>");
}

.sd-wisp {
  opacity: 0.5;
}

/* 无障碍：减少动态偏好下不额外引入动效（本层本就静态，此处为后续扩展兜底） */
@media (prefers-reduced-motion: reduce) {
  .sd-starfield,
  .sd-nebula,
  .sd-wisp {
    animation: none !important;
    transition: none !important;
  }
}

/* 星点层强度由令牌控制（浅色主题 0.15，设计稿 stars 压暗） */
.sd-stars {
  opacity: var(--sd-star-opacity);
}

/* 浅色主题：wisp 柔光与噪点一并压暗，避免亮底上的浑浊感 */
[data-theme='light'] .sd-wisp {
  opacity: 0.12;
}
[data-theme='light'] .sd-grain {
  opacity: 0.2;
}
</style>
