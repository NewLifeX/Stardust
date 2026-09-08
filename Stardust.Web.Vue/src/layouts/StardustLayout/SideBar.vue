<script setup lang="ts">
/**
 * 星尘控制台 · 侧栏导航 SideBar（design-spec 01 §3 红标：72 宽 / nav 48×48 圆角 14 / 图标 20 / 标签 10）
 * 激活态：useRoute() 判断 route.path 是否以对应前缀开头。
 * 样式纪律（cube-vue 技能红线）：颜色一律 --el-* 令牌（品牌渐变用 --el-color-primary 派生），禁止自定义 CSS 变量与硬编码色值。
 */
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'

const route = useRoute()
const router = useRouter()

interface NavItem {
  key: string
  label: string
  path: string
}

const items: NavItem[] = [
  { key: 'overview', label: '概览', path: '/stardust/overview' },
  { key: 'nodes', label: '节点', path: '/stardust/nodes' },
  { key: 'apps', label: '应用', path: '/stardust/apps' },
  { key: 'settings', label: '设置', path: '/stardust/settings' },
]

/** 激活判断：对实际有独立路由的入口做前缀匹配 */
const activeKey = computed(() => {
  const path = route.path
  const exactFirst = items.find((it) => it.path === path)
  if (exactFirst) return exactFirst.key
  const prefixHit = items.find((it) => path.startsWith(it.path))
  return prefixHit ? prefixHit.key : ''
})

function go(path: string) {
  router.push(path)
}
</script>

<template>
  <aside
    data-spec="sidebar"
    class="flex w-[56px] shrink-0 flex-col items-center gap-2 border-r border-[var(--el-border-color-light)] bg-[var(--el-bg-color)] pt-4 backdrop-blur"
  >
    <nav class="flex w-full flex-col items-center gap-2">
      <button
        v-for="it in items"
        :key="it.key"
        type="button"
        data-spec="nav"
        class="flex h-10 w-10 flex-col items-center justify-center gap-[2px] rounded-[12px] border border-transparent text-[var(--el-text-color-regular)] transition-colors hover:bg-[var(--el-bg-color-overlay)] hover:text-[var(--el-text-color-primary)]"
        :class="activeKey === it.key ? 'nav-active' : ''"
        :title="it.label"
        @click="go(it.path)"
      >
        <!-- 概览：房屋 -->
        <svg
          v-if="it.key === 'overview'"
          class="h-5 w-5"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="1.6"
        >
          <path d="M3 10.5L12 3l9 7.5" />
          <path d="M5 9.5V21h5v-6h4v6h5V9.5" />
        </svg>
        <!-- 节点：服务器方块 -->
        <svg
          v-else-if="it.key === 'nodes'"
          class="h-5 w-5"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="1.6"
        >
          <rect
            x="3"
            y="4"
            width="18"
            height="7"
            rx="2"
          />
          <rect
            x="3"
            y="14"
            width="18"
            height="6"
            rx="2"
          />
          <circle
            cx="7"
            cy="7.5"
            r="1"
          />
          <circle
            cx="7"
            cy="17"
            r="1"
          />
        </svg>
        <!-- 应用：方块堆叠 -->
        <svg
          v-else-if="it.key === 'apps'"
          class="h-5 w-5"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="1.6"
        >
          <path d="M12 3l8 4.5v9L12 21l-8-4.5v-9z" />
          <path d="M12 12l8-4.5M12 12v9M12 12L4 7.5" />
        </svg>
        <!-- 设置：齿轮 -->
        <svg
          v-else
          class="h-5 w-5"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="1.6"
        >
          <circle
            cx="12"
            cy="12"
            r="3"
          />
          <path d="M19 12a7 7 0 0 0-.1-1l2-1.5-2-3.4-2.3 1a7 7 0 0 0-1.7-1l-.3-2.5h-4l-.3 2.5a7 7 0 0 0-1.7 1l-2.3-1-2 3.4 2 1.5a7 7 0 0 0 0 2l-2 1.5 2 3.4 2.3-1a7 7 0 0 0 1.7 1l.3 2.5h4l.3-2.5a7 7 0 0 0 1.7-1l2.3 1 2-3.4-2-1.5c.1-.3.1-.7.1-1z" />
        </svg>

        <span
          data-spec="nav-label"
          class="text-[10px] font-semibold tracking-wide"
        >{{ it.label }}</span>
      </button>
    </nav>
  </aside>
</template>

<style scoped>
/* 激活态（对照 preview_*.png）：半透明品牌底 + 描边光环，图标保持品牌紫、标签白色；全部 --el-* 派生，禁止硬编码色值 */
.nav-active {
  background: color-mix(in srgb, var(--el-color-primary) 22%, transparent);
  border-color: var(--el-color-primary);
  box-shadow: 0 0 12px color-mix(in srgb, var(--el-color-primary) 35%, transparent);
  color: var(--el-text-color-primary);
}
.nav-active svg {
  color: var(--el-color-primary-light-3);
}
</style>
