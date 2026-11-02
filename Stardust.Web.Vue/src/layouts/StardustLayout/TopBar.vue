<script setup lang="ts">
/**
 * 星尘控制台 · 顶栏 TopBar —— 按设计交接包 StarTopBar.jsx 校准为 56px
 * ---------------------------------------------------------------------------
 * 设计真源：design-handoff/ui_kits/console/StarTopBar.jsx
 *   height 56 · padding 0 20 · 底 rgba(11,15,26,.55) + blur(12) · 下边框 border-1
 *   左：logo-mark.svg 26px +「星尘 (Stardust)」→ 竖分隔线 1×20
 *   中：集群选择器（前置状态光点 + chevron）→ 胶囊搜索（圆角 999 + ⌘K kbd）
 *   右：34×34 图标按钮（铃铛 + 红点）+ 34px 圆形头像
 *
 * 业务侧不变（方案 §6 红线）：
 *   - 项目 / 环境双选择器与 stardustContext 联动逻辑原样保留
 *   - ⌘K 全局搜索（GlobalSearch）与搜索胶囊入口保留
 *   - e2e 契约属性 data-spec="topbar|project-selector|project-trigger|
 *     env-selector|env-trigger|search-trigger" 全部保留
 *
 * 样式纪律：颜色一律取自设计令牌（--fg-*、--surface-*、--border-*、--brand-*），
 *          不出现字面色值。
 */
import { ref, onMounted } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import {
  stardustContext,
  ENVIRONMENTS,
  environmentToken,
  setStardustProject,
  setStardustEnvironment,
  markStardustContextReady,
} from '@/stores/stardustContext';
import GlobalSearch, { globalSearchState } from './components/GlobalSearch.vue';
import logoMark from '@/assets/brand/logo-mark.svg';
import { theme, toggleTheme } from '@/design-system/theme';

interface GalaxyProject {
  id?: number;
  name?: string;
}

// 静态兜底：后端不可用时仍保留可切换的项目上下文
const fallbackProjects: GalaxyProject[] = [
  { id: 0, name: '电商微服务平台' },
  { id: 0, name: '物流调度平台' },
  { id: 0, name: '风控计算平台' },
];
const projects = ref<GalaxyProject[]>([...fallbackProjects]);
const projectLoading = ref(false);

async function loadProjects() {
  projectLoading.value = true;
  try {
    // request 已自动拼接 /api 前缀并注入登录 token；Cube 响应体为 { code, data }
    const res = await request({
      url: '/Platform/GalaxyProject',
      method: 'GET',
      params: { PageSize: 200 },
    });
    const list: GalaxyProject[] = Array.isArray(res?.data) ? res.data : [];
    const valid = list.filter((p) => typeof p.name === 'string' && p.name.length > 0);
    if (valid.length > 0) {
      projects.value = valid;
      if (!valid.some((p) => p.name === stardustContext.projectName)) {
        selectProject(valid[0]);
      }
    }
  } catch (e) {
    console.warn('加载项目列表失败，使用静态兜底', e);
    projects.value = [...fallbackProjects];
  } finally {
    projectLoading.value = false;
    // 解析结束即置位（无论成功/失败/兜底）—— 页面首轮取数据此放行，避免空上下文白打一轮
    markStardustContextReady();
  }
}

function selectProject(p: GalaxyProject) {
  setStardustProject(p.id ?? null, p.name ?? '');
}

function openSearch() {
  globalSearchState.open = true;
}

onMounted(() => {
  if (stardustContext.projectName) {
    // 持久化上下文已在 store 初始化时载入，无需重复选择
  }
  void loadProjects();
});
</script>

<template>
  <header
    data-spec="topbar"
    class="sd-topbar"
  >
    <!-- 左：品牌区（logo-mark + 星尘 Stardust） -->
    <div class="sd-brand">
      <img
        :src="logoMark"
        class="sd-brand-mark"
        alt=""
        aria-hidden="true"
      >
      <span class="sd-brand-name">
        星尘<span class="sd-brand-en">Stardust</span>
      </span>
    </div>

    <span class="sd-vdivider" />

    <!-- 全局上下文 —— 项目选择器 -->
    <el-dropdown
      trigger="click"
      class="shrink-0"
      data-spec="project-selector"
    >
      <button
        type="button"
        data-spec="project-trigger"
        class="sd-ctl"
      >
        <span class="truncate">{{ stardustContext.projectName || '选择项目' }}</span>
        <svg
          class="sd-ctl-chevron"
          width="14"
          height="14"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
          aria-hidden="true"
        >
          <path d="M6 9l6 6 6-6" />
        </svg>
      </button>
      <template #dropdown>
        <el-dropdown-menu>
          <el-dropdown-item
            v-for="p in projects"
            :key="p.name"
            :command="p"
            @click="selectProject(p)"
          >
            {{ p.name }}
          </el-dropdown-item>
        </el-dropdown-menu>
      </template>
    </el-dropdown>

    <!-- 全局上下文 —— 环境选择器 -->
    <el-dropdown
      trigger="click"
      class="shrink-0"
      data-spec="env-selector"
    >
      <button
        type="button"
        data-spec="env-trigger"
        class="sd-ctl"
      >
        <span class="flex items-center gap-2">
          <span
            class="sd-dot"
            :style="{ backgroundColor: environmentToken(stardustContext.environment) }"
          />
          {{ stardustContext.environmentName }}
        </span>
        <svg
          class="sd-ctl-chevron"
          width="14"
          height="14"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
          aria-hidden="true"
        >
          <path d="M6 9l6 6 6-6" />
        </svg>
      </button>
      <template #dropdown>
        <el-dropdown-menu>
          <el-dropdown-item @click="setStardustEnvironment(null)">
            <span class="flex items-center gap-2">全部环境</span>
          </el-dropdown-item>
          <el-dropdown-item
            v-for="e in ENVIRONMENTS"
            :key="e.key"
            @click="setStardustEnvironment(e.key)"
          >
            <span class="flex items-center gap-2">
              <span
                class="sd-dot"
                :style="{ backgroundColor: e.token }"
              />
              {{ e.label }}
            </span>
          </el-dropdown-item>
        </el-dropdown-menu>
      </template>
    </el-dropdown>

    <!-- 胶囊搜索入口（点击或 ⌘K / Ctrl+K 打开全局搜索） -->
    <button
      type="button"
      data-spec="search-trigger"
      class="sd-search"
      @click="openSearch"
    >
      <span class="flex min-w-0 items-center gap-2.5">
        <svg
          class="shrink-0"
          width="14"
          height="14"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="1.5"
          stroke-linecap="round"
          stroke-linejoin="round"
          aria-hidden="true"
        >
          <circle
            cx="11"
            cy="11"
            r="7"
          />
          <path d="M21 21l-4-4" />
        </svg>
        <span class="truncate">搜索节点、服务、应用…</span>
      </span>
      <kbd class="sd-kbd">⌘K</kbd>
    </button>

    <div class="flex-1" />

    <!-- 右：告警 / 通知 / 用户 -->
    <div class="flex shrink-0 items-center gap-3">
      <span
        class="sd-alert-pill"
        title="未处理告警"
      >
        <span class="sd-dot sd-dot-danger" />
        3 告警
      </span>

      <el-badge
        :value="3"
        class="flex"
      >
        <button
          type="button"
          class="sd-icon-btn"
          title="通知"
        >
          <svg
            width="16"
            height="16"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="1.5"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <path d="M18 8a6 6 0 1 0-12 0c0 7-3 9-3 9h18s-3-2-3-9" />
            <path d="M13.7 21a2 2 0 0 1-3.4 0" />
          </svg>
          <span class="sd-icon-dot" />
        </button>
      </el-badge>

      <button

        type="button"

        class="sd-icon-btn"

        data-spec="theme-toggle"

        :title="theme === 'dark' ? '切换浅色主题' : '切换深色主题'"

        @click="toggleTheme"
      >
        <svg

          v-if="theme === 'dark'"

          width="15"

          height="15"

          viewBox="0 0 24 24"

          fill="none"

          stroke="currentColor"

          stroke-width="1.5"

          stroke-linecap="round"

          stroke-linejoin="round"

          aria-hidden="true"
        >

          <path d="M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9z" />

        </svg>

        <svg

          v-else

          width="15"

          height="15"

          viewBox="0 0 24 24"

          fill="none"

          stroke="currentColor"

          stroke-width="1.5"

          stroke-linecap="round"

          stroke-linejoin="round"

          aria-hidden="true"
        >

          <circle
            cx="12"
            cy="12"
            r="4"
          />

          <path d="M12 2v2M12 20v2M2 12h2M20 12h2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" />

        </svg>
      </button>

      <div class="sd-avatar">
        笑
      </div>
    </div>

    <!-- 全局搜索面板 -->
    <GlobalSearch />
  </header>
</template>

<style scoped>
/* 顶栏：56px 玻璃条（设计稿 StarTopBar） */
.sd-topbar {
  display: flex;
  align-items: center;
  gap: 16px;
  height: var(--sd-topbar-h);
  padding: 0 20px;
  border-bottom: 1px solid var(--border-1);
  background: color-mix(in srgb, var(--bg) 55%, transparent);
  backdrop-filter: blur(12px);
  flex-shrink: 0;
}

/* ---- 品牌 ---- */
.sd-brand {
  display: flex;
  align-items: center;
  gap: 10px;
  white-space: nowrap;
  flex-shrink: 0;
}
.sd-brand-mark {
  width: 26px;
  height: 26px;
}
.sd-brand-name {
  font-size: 15px;
  font-weight: 600;
  letter-spacing: -0.005em;
  color: var(--fg-1);
}
.sd-brand-en {
  margin-left: 6px;
  font-weight: 500;
  color: var(--fg-3);
}

.sd-vdivider {
  width: 1px;
  height: 20px;
  background: var(--border-1);
  flex-shrink: 0;
}

/* ---- 上下文控件（项目 / 环境共用） ---- */
.sd-ctl {
  display: flex;
  align-items: center;
  gap: 8px;
  max-width: 220px;
  height: 32px;
  padding: 0 12px;
  border: 1px solid var(--border-1);
  border-radius: var(--radius-md);
  background: var(--surface-2);
  color: var(--fg-1);
  font-size: 13px;
  font-weight: 500;
  white-space: nowrap;
  cursor: pointer;
  transition: border-color var(--dur-micro) var(--ease-out);
}
.sd-ctl:hover {
  border-color: var(--brand-500);
}
.sd-ctl-chevron {
  flex-shrink: 0;
  color: var(--fg-3);
}

/* 状态光点 */
.sd-dot {
  width: 6px;
  height: 6px;
  border-radius: var(--radius-pill);
  background: var(--success-500);
  box-shadow: 0 0 6px var(--success-500);
  flex-shrink: 0;
}
.sd-dot-danger {
  background: var(--danger-500);
  box-shadow: 0 0 6px var(--danger-500);
}

/* ---- 胶囊搜索 ---- */
.sd-search {
  display: flex;
  flex: 1;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  max-width: 480px;
  height: 32px;
  padding: 0 14px;
  border: 1px solid var(--border-1);
  border-radius: var(--radius-pill);
  background: var(--surface-1);
  color: var(--fg-3);
  font-size: 13px;
  cursor: pointer;
  transition: border-color var(--dur-micro) var(--ease-out), color var(--dur-micro) var(--ease-out);
}
.sd-search:hover {
  border-color: var(--brand-500);
  color: var(--fg-2);
}
.sd-kbd {
  flex-shrink: 0;
  padding: 1px 6px;
  border: 1px solid var(--border-1);
  border-radius: var(--radius-xs);
  color: var(--fg-4);
  font-family: var(--font-mono);
  font-size: 10.5px;
  line-height: 1.5;
}

/* ---- 右侧 ---- */
.sd-alert-pill {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 3px 10px;
  border: 1px solid var(--border-1);
  border-radius: var(--radius-pill);
  background: var(--surface-1);
  color: var(--fg-2);
  font-size: 11.5px;
  font-weight: 500;
  white-space: nowrap;
}

.sd-icon-btn {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 34px;
  height: 34px;
  border: 1px solid var(--border-1);
  border-radius: var(--radius-md);
  background: transparent;
  color: var(--fg-2);
  cursor: pointer;
  transition: color var(--dur-micro) var(--ease-out), border-color var(--dur-micro) var(--ease-out);
}
.sd-icon-btn:hover {
  color: var(--fg-1);
  border-color: var(--border-2);
}
.sd-icon-dot {
  position: absolute;
  top: 7px;
  right: 7px;
  width: 6px;
  height: 6px;
  border-radius: var(--radius-pill);
  background: var(--danger-500);
  box-shadow: 0 0 6px var(--danger-500);
}

.sd-avatar {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 34px;
  height: 34px;
  border: 1px solid var(--border-1);
  border-radius: var(--radius-full);
  background: linear-gradient(135deg, var(--brand-300), var(--cyan-500));
  color: #ffffff;
  font-size: 13px;
  font-weight: 600;
}
</style>
