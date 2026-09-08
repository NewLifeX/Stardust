<script setup lang="ts">
/**
 * 星尘控制台布局 StardustLayout（完整外壳）
 *
 * 布局结构（竖向）：
 *   1. 背景层 StarfieldBg（absolute inset-0，置于最底）
 *   2. 顶栏 TopBar（高 64px，顶部 sticky，z-20）
 *   3. 主体 flex：左侧 SideBar（固定 72px）+ 右侧主内容区 <main>
 *
 * 样式纪律（cube-vue 技能红线）：颜色一律 --el-* 令牌，布局尺寸用 Tailwind，禁止自定义 CSS 变量与硬编码色值。
 */
import StarfieldBg from './starfield/StarfieldBg.vue';
import TopBar from './TopBar.vue';
import SideBar from './SideBar.vue';
</script>

<template>
  <!-- 根容器：相对定位铺底，星图背景置于其内；页面底透明以露出星空层 -->
  <div class="sd-layout relative min-h-screen text-[var(--el-text-color-primary)]">
    <!-- 1. 背景层：绝对铺满，置于最底 -->
    <StarfieldBg />

    <!-- 顶栏 + 主体整体浮于背景之上 -->
    <div class="relative z-10 flex min-h-screen flex-col">
      <!-- 2. 顶栏：固定顶部，高 64px，z-20 -->
      <div class="sticky top-0 z-20">
        <TopBar />
      </div>

      <!-- 3. 主体：顶栏之下，左侧栏 + 右侧内容 -->
      <div class="flex flex-1">
        <!-- 左侧栏：固定宽 72px，不收缩 -->
        <SideBar />

        <!-- 右侧主内容区：页面经框架 RootLayout 以默认插槽注入（不可用 router-view） -->
        <main class="min-h-0 flex-1 p-6">
          <div class="mx-auto max-w-[1600px]">
            <slot />
          </div>
        </main>
      </div>
    </div>
  </div>
</template>

<style scoped>
/* 星尘控制台：页面背景透明，露出星空层 StarfieldBg；
   并把卡片 / 侧栏 / 表格等表面令牌改为派生自设计令牌的半透明玻璃态，
   使星空透出（颜色一律 color-mix + --sd-* 派生，不硬编码色值）。 */
.sd-layout {
  --el-bg-color-page: transparent; /* 页面底透明，星空层成为可视背景 */
  --el-bg-color: color-mix(in srgb, var(--sd-bg-surface) 72%, transparent);
  --el-bg-color-overlay: color-mix(in srgb, var(--sd-bg-elevated) 82%, transparent);
  --el-fill-color: color-mix(in srgb, var(--sd-bg-elevated) 72%, transparent);
  --el-fill-color-lighter: color-mix(in srgb, var(--sd-bg-surface) 72%, transparent);
}
</style>
