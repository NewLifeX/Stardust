<script setup lang="ts">
/**
 * 星图背景组件 StarfieldBg
 * 仅负责铺底：背景图 + 暗色遮罩 + 可选 twinkle 光点呼吸层。
 * 样式纪律：颜色一律 --el-* 令牌，禁止硬编码色值。
 */
import bg from '../../../assets/visual/2_2.png';
</script>

<template>
  <!-- 根容器：铺满父级，裁切溢出 -->
  <div class="absolute inset-0 overflow-hidden">
    <!-- 背景图：绝对定位铺满，星云主体，不可交互 -->
    <img
      :src="bg"
      alt=""
      class="pointer-events-none absolute inset-0 h-full w-full object-cover opacity-90"
      aria-hidden="true"
    >
    <!-- 暗色遮罩：压低亮度保证文字可读，同时保留星云层次 -->
    <div
      class="pointer-events-none absolute inset-0 bg-[var(--el-bg-color-page)]/35"
      aria-hidden="true"
    />
    <!-- 轻微 twinkle：径向星尘蓝光点缓慢呼吸 -->
    <div
      class="starfield-twinkle pointer-events-none absolute inset-0"
      aria-hidden="true"
    />
  </div>
</template>

<style scoped>
.starfield-twinkle {
  background: radial-gradient(
    circle at 30% 35%,
    var(--el-color-primary) 0%,
    transparent 18%
  ),
  radial-gradient(
    circle at 72% 60%,
    var(--el-color-primary) 0%,
    transparent 14%
  ),
  radial-gradient(
    circle at 55% 80%,
    var(--el-color-primary) 0%,
    transparent 12%
  );
  background-repeat: no-repeat;
  opacity: 0.18;
  /* 注意：此处曾用 7s 无限透明度动画做「呼吸」。但它位于所有玻璃卡片
   * (backdrop-filter / backdrop-blur) 的背后，每帧都触发 backdrop-filter
   * 重新合成，在 Chrome 下表现为整页持续闪烁（节点/应用页卡片多更明显）。
   * 改为静态层，消除无限重合成；星云层次由背景图 2_2.png 提供。
   */
}
</style>
