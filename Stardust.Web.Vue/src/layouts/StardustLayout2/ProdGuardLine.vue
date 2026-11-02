<template>
  <!-- 生产环境护栏：顶栏底边下的 2px 红色渐变线，不占布局。环境固定为生产，所以常驻。 -->
  <span
    data-spec="prod-guard-line"
    aria-hidden="true"
    class="pointer-events-none absolute inset-x-0 -bottom-px h-[2px] opacity-[0.55] bg-[linear-gradient(90deg,transparent,var(--el-color-danger)_18%,var(--el-color-danger)_82%,transparent)]"
  />
</template>
