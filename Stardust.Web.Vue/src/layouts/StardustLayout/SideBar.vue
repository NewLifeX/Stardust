<script setup lang="ts">
/**
 * 星尘控制台 · 侧栏 SideBar —— 按设计交接包 MiniSidebar.jsx 重写为 68px 图标轨
 * ---------------------------------------------------------------------------
 * 设计真源：design-handoff/ui_kits/console/MiniSidebar.jsx
 *   width 68 · 背景 bg/0.72 + blur(12) · 右边框 border-1
 *   项：宽 52 / 内边距 8px 0 / 圆角 10 / 图标 18 / 标签 10px(0.06em)/500
 *   激活：底 brand/0.18 + 边 brand/0.32 + 文字 brand-300 + 左侧 2px 发光竖条
 *   悬浮：底 surface-2 + 文字 fg-1
 *   徽标：>=14px 药丸 / font-mono 9px / 底 brand-500 / 绝对定位 top-4 right-8
 *
 * 业务侧不变（方案 §6 红线）：
 *   - 九域一级导航与 `?tab=` 深层链接完全保留
 *   - 平台管理 / 可观测 / 网关的 hover 二级菜单保留
 *   - e2e 契约属性 data-spec="sidebar|nav|nav-label|nav-sub" 全部保留
 *   - 激活判定逻辑（前缀匹配）原样保留
 *
 * 样式纪律：颜色一律取自设计令牌，不出现字面色值。
 */
import { computed, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';

const route = useRoute();
const router = useRouter();

interface NavItem {
  key: string;
  label: string;
  path: string;
  icon: string;
  /** 激活前缀集合：命中其一即高亮（默认 path 本身） */
  prefixes?: string[];
  /** 右上角徽标（暂未接线，留待 A6 接真实计数） */
  badge?: string;
  /** hover 二级菜单 */
  children?: { label: string; path: string }[];
}

const items: NavItem[] = [
  { key: 'workbench', label: '工作台', path: '/stardust/overview', icon: 'M3 10.5L12 3l9 7.5 M5 9.5V21h5v-6h4v6h5V9.5' },
  { key: 'apps', label: '应用', path: '/stardust/apps', icon: 'M12 3l8 4.5v9L12 21l-8-4.5v-9z M12 12l8-4.5M12 12v9M12 12L4 7.5', prefixes: ['/stardust/apps', '/Registry/App/'] },
  { key: 'nodes', label: '节点', path: '/stardust/nodes', icon: 'M3 4h18v7H3z M3 14h18v6H3z M7 7.5h.01M7 17h.01', prefixes: ['/stardust/nodes', '/Nodes'] },
  { key: 'services', label: '服务', path: '/stardust/service', icon: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10', prefixes: ['/stardust/service', '/Registry/AppService', '/Registry/Service'] },
  { key: 'configs', label: '配置', path: '/stardust/config', icon: 'M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z M15 12a3 3 0 11-6 0 3 3 0 016 0z', prefixes: ['/stardust/config', '/Configs'] },
  { key: 'releases', label: '发布', path: '/stardust/release', icon: 'M15.59 14.37a6 6 0 01-5.84 7.18 6 6 0 01-5.84-7.18 6 6 0 0112 0zm0 0H3.75m0 0l-.08-.83a6 6 0 0111.89-3.18M3.75 14.37l-.08.83M20 2v6m0 0l3-3m-3 3l-3-3', prefixes: ['/stardust/release', '/Deployment'] },
  { key: 'observability', label: '可观测', path: '/stardust/observability', icon: 'M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7z M12 15a3 3 0 100-6 3 3 0 000 6z', prefixes: ['/stardust/observability', '/Monitors', '/Trace'], children: [
    { label: '指标', path: '/stardust/observability?tab=metrics' },
    { label: 'Trace', path: '/stardust/observability?tab=trace' },
    { label: '日志', path: '/stardust/observability?tab=logs' },
    { label: '告警', path: '/stardust/observability?tab=alerts' },
  ] },
  { key: 'gateway', label: '网关', path: '/stardust/gateway', icon: 'M13 10V3L4 14h7v7l9-11h-7z', prefixes: ['/stardust/gateway', '/Gateway'], children: [
    { label: '集群', path: '/stardust/gateway?tab=cluster' },
    { label: '路由', path: '/stardust/gateway?tab=route' },
    { label: '节点', path: '/stardust/gateway?tab=node' },
  ] },
  { key: 'platform', label: '平台', path: '/stardust/admin', icon: 'M12 2l8 4v6c0 5-3.5 8-8 10-4.5-2-8-5-8-10V6z M9 12l2 2 4-4', prefixes: ['/Platform', '/stardust/settings', '/stardust/users', '/stardust/roles', '/Admin/User', '/Admin/Role'], children: [
    { label: '项目', path: '/stardust/admin?tab=projects' },
    { label: '用户', path: '/stardust/admin?tab=users' },
    { label: '角色', path: '/stardust/admin?tab=roles' },
    { label: 'Token', path: '/stardust/admin?tab=tokens' },
    { label: '系统设置', path: '/stardust/settings' },
  ] },
];

/** 激活判断：对有独立路由的入口做前缀匹配（逻辑与原实现一致） */
const activeKey = computed(() => {
  const path = route.path;
  const hit = items.find((it) =>
    path === it.path || (it.prefixes ?? [it.path]).some((p) => path.startsWith(p)),
  );
  return hit ? hit.key : '';
});

const hoverKey = ref('');

function go(item: NavItem) {
  if (item.children && item.children.length > 0) return; // 有二级菜单的项由子项承担跳转
  void router.push(item.path);
}

function goChild(path: string) {
  hoverKey.value = '';
  void router.push(path);
}

function isActive(it: NavItem): boolean {
  return activeKey.value === it.key;
}
</script>

<template>
  <aside
    data-spec="sidebar"
    class="sd-rail"
  >
    <nav class="sd-rail-nav">
      <template
        v-for="it in items"
        :key="it.key"
      >
        <!-- 分隔线：平台管理之前（分组语义） -->
        <div
          v-if="it.key === 'platform'"
          class="sd-rail-sep"
        />

        <div
          class="relative"
          @mouseenter="hoverKey = it.key"
          @mouseleave="hoverKey = ''"
        >
          <button
            type="button"
            data-spec="nav"
            class="sd-rail-item"
            :class="{ 'is-active': isActive(it) }"
            :title="it.label"
            @click="go(it)"
          >
            <!-- 激活左侧竖条（设计稿：left -8 / 上下 10 / 宽 2 / 发光） -->
            <span
              v-if="isActive(it)"
              class="sd-rail-bar"
            />
            <span class="sd-rail-icon">
              <svg
                width="18"
                height="18"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="1.5"
                stroke-linecap="round"
                stroke-linejoin="round"
                aria-hidden="true"
              >
                <path :d="it.icon" />
              </svg>
              <span
                v-if="it.badge"
                class="sd-rail-badge"
              >{{ it.badge }}</span>
            </span>
            <span
              data-spec="nav-label"
              class="sd-rail-label"
            >{{ it.label }}</span>
          </button>

          <!-- hover 二级菜单（平台管理 / 可观测 / 网关） -->
          <Transition name="sd-pop">
            <div
              v-if="hoverKey === it.key && it.children && it.children.length > 0"
              class="sd-rail-flyout"
            >
              <button
                v-for="c in it.children"
                :key="c.path"
                type="button"
                data-spec="nav-sub"
                class="sd-rail-flyout-item"
                @click="goChild(c.path)"
              >
                {{ c.label }}
              </button>
            </div>
          </Transition>
        </div>
      </template>
    </nav>
  </aside>
</template>

<style scoped>
/* 轨体：68px / 玻璃底 / 贴顶常驻（对齐设计稿 MiniSidebar 的 sticky 68px） */
.sd-rail {
  position: sticky;
  top: var(--sd-topbar-h);
  height: calc(100vh - var(--sd-topbar-h));
  width: var(--sd-sidebar-w);
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  border-right: 1px solid var(--border-1);
  background: color-mix(in srgb, var(--bg) 72%, transparent);
  backdrop-filter: blur(12px);
  z-index: 10;
}

.sd-rail-nav {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  padding: 14px 0;
  overflow-y: auto;
  overflow-x: visible;
}

.sd-rail-sep {
  width: 28px;
  height: 1px;
  margin: 6px 0;
  background: var(--border-1);
}

/* 项：52 宽 / 图标在上文字在下 / 圆角 10 */
.sd-rail-item {
  position: relative;
  width: 52px;
  padding: 8px 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  border: 1px solid transparent;
  border-radius: var(--radius-lg);
  background: transparent;
  color: var(--fg-3);
  cursor: pointer;
  transition: all var(--dur-micro) var(--ease-out);
}
.sd-rail-item:hover {
  background: var(--surface-2);
  color: var(--fg-1);
}
.sd-rail-item.is-active {
  background: color-mix(in srgb, var(--brand-500) 18%, transparent);
  border-color: color-mix(in srgb, var(--brand-500) 32%, transparent);
  color: var(--brand-300);
}

/* 激活竖条：绝对定位，落在轨左内缘 */
.sd-rail-bar {
  position: absolute;
  left: -8px;
  top: 10px;
  bottom: 10px;
  width: 2px;
  border-radius: 2px;
  background: var(--brand-500);
  box-shadow: 0 0 8px var(--brand-500);
}

.sd-rail-icon {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
}

/* 徽标：右上角外挂 */
.sd-rail-badge {
  position: absolute;
  top: -4px;
  right: -8px;
  min-width: 14px;
  height: 14px;
  padding: 0 3px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: var(--radius-pill);
  background: var(--brand-500);
  color: #ffffff;
  font-family: var(--font-mono);
  font-size: 9px;
  font-weight: 600;
  line-height: 1;
}

/* 标签：10px / 0.06em */
.sd-rail-label {
  font-size: 10px;
  font-weight: 500;
  letter-spacing: 0.06em;
  line-height: 1.2;
  white-space: nowrap;
}

/* 二级菜单：贴轨右侧弹出 */
.sd-rail-flyout {
  position: absolute;
  left: 100%;
  top: 0;
  margin-left: 4px;
  width: 132px;
  padding: 6px;
  display: flex;
  flex-direction: column;
  gap: 2px;
  border: 1px solid var(--border-2);
  border-radius: var(--radius-xl);
  background: color-mix(in srgb, var(--surface-2) 96%, transparent);
  box-shadow: var(--elev-2);
  z-index: 50;
}
.sd-rail-flyout-item {
  padding: 8px 12px;
  text-align: left;
  font-size: 12px;
  border-radius: var(--radius-md);
  color: var(--fg-2);
  background: transparent;
  cursor: pointer;
  transition: background var(--dur-micro) var(--ease-out), color var(--dur-micro) var(--ease-out);
}
.sd-rail-flyout-item:hover {
  background: var(--surface-3);
  color: var(--fg-1);
}

/* 弹出过渡（设计稿：短位移淡入） */
.sd-pop-enter-active,
.sd-pop-leave-active {
  transition: opacity var(--dur-micro) var(--ease-out), transform var(--dur-micro) var(--ease-out);
}
.sd-pop-enter-from,
.sd-pop-leave-to {
  opacity: 0;
  transform: translateX(-4px);
}
</style>
