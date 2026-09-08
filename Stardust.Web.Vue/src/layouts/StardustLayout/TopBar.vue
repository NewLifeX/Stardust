<script setup lang="ts">
/**
 * 星尘控制台 · 顶栏 TopBar
 * 样式纪律（cube-vue 技能红线）：颜色一律 --el-* 令牌；EP 组件由模板自动解析，禁止显式 import 组件。
 */
import { ref, onMounted } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';

interface GalaxyProject {
  id?: number;
  name?: string;
}

// 静态兜底：后端不可用时仍保留可切换的项目上下文
const fallbackProjects = ['电商微服务平台', '物流调度平台', '风控计算平台'];
const projects = ref<string[]>([...fallbackProjects]);
const currentProject = ref('电商微服务平台');
const projectLoading = ref(false);

async function loadProjects() {
  projectLoading.value = true;
  try {
    // request 已自动拼接 /api 前缀并注入登录 token；
    // Cube 响应体为 { code, data: [...] }，data 为实体数组（camelCase 字段）
    const res = await request({
      url: '/Platform/GalaxyProject',
      method: 'GET',
      params: { PageSize: 200 },
    });
    const list: GalaxyProject[] = Array.isArray(res?.data) ? res.data : [];
    const names = list
      .map((p) => p.name)
      .filter((n): n is string => typeof n === 'string' && n.length > 0);
    if (names.length > 0) {
      projects.value = names;
      // 若当前选中项不在新列表内，默认选中第一个
      if (!projects.value.includes(currentProject.value)) {
        currentProject.value = names[0];
      }
    }
  } catch (e) {
    console.warn('加载项目列表失败，使用静态兜底', e);
    projects.value = [...fallbackProjects];
  } finally {
    projectLoading.value = false;
  }
}

function selectProject(name: string) {
  currentProject.value = name;
  // 项目上下文变更持久化，便于后续各页面按项目过滤
  localStorage.setItem('stardust-current-project', name);
}

onMounted(() => {
  const cached = localStorage.getItem('stardust-current-project');
  if (cached) currentProject.value = cached;
  void loadProjects();
});
</script>

<template>
  <header
    data-spec="topbar"
    class="flex h-16 items-center gap-5 border-b border-[var(--el-border-color)] px-6 backdrop-blur"
    style="background-color: color-mix(in srgb, var(--el-bg-color) 55%, transparent)"
  >
    <!-- 左：品牌区 -->
    <div class="flex shrink-0 items-center gap-2.5 font-bold text-lg">
      <span
        class="flex h-[30px] w-[30px] items-center justify-center rounded-[9px] text-white"
        style="
          background: linear-gradient(135deg, var(--el-color-primary), var(--el-color-primary-dark-2));
          box-shadow: 0 0 18px color-mix(in srgb, var(--el-color-primary) 45%, transparent);
        "
      >
        <!-- 内联 SVG 四角星 -->
        <svg
          width="16"
          height="16"
          viewBox="0 0 24 24"
          fill="currentColor"
          aria-hidden="true"
        >
          <path d="M12 2l1.8 6.2L20 10l-6.2 1.8L12 18l-1.8-6.2L4 10l6.2-1.8L12 2z" />
        </svg>
      </span>
      <span
        class="bg-gradient-to-r from-[var(--el-color-primary)] to-[var(--el-color-info)] bg-clip-text text-transparent"
      >
        星尘 Stardust
      </span>
    </div>

    <!-- 中：项目选择器 -->
    <el-dropdown
      trigger="click"
      class="shrink-0"
      data-spec="project-selector"
    >
      <button
        type="button"
        data-spec="project-trigger"
        class="flex h-[36px] min-w-[200px] items-center justify-between gap-1.5 rounded-md border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-3 text-[13px] text-[var(--el-text-color-regular)] transition-colors hover:text-[var(--el-text-color-primary)]"
      >
        <span>{{ currentProject }}</span>
        <svg
          width="10"
          height="10"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2.4"
          aria-hidden="true"
        >
          <path d="M6 9l6 6 6-6" />
        </svg>
      </button>
      <template #dropdown>
        <el-dropdown-menu>
          <el-dropdown-item
            v-for="p in projects"
            :key="p"
            :command="p"
            @click="selectProject(p)"
          >
            {{ p }}
          </el-dropdown-item>
        </el-dropdown-menu>
      </template>
    </el-dropdown>

    <!-- 搜索胶囊（flex:1, max 420） -->
    <el-input
      placeholder="搜索节点、应用、配置…"
      class="search-capsule max-w-[420px] flex-1"
    >
      <template #prefix>
        <svg
          width="16"
          height="16"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="1.6"
          aria-hidden="true"
        >
          <circle
            cx="11"
            cy="11"
            r="7"
          />
          <path d="M21 21l-4-4" />
        </svg>
      </template>
    </el-input>

    <div class="flex-1" />

    <!-- 右：通知 + 头像 -->
    <div class="flex shrink-0 items-center gap-3">
      <!-- 通知铃铛 -->
      <el-badge
        :value="3"
        class="flex"
      >
        <button
          type="button"
          class="flex h-[38px] w-[38px] items-center justify-center rounded-full border border-[var(--el-border-color)] bg-[var(--el-bg-color)] text-[var(--el-text-color-primary)] transition-colors hover:text-[var(--el-color-info)]"
          title="通知"
        >
          <svg
            width="18"
            height="18"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="1.6"
            aria-hidden="true"
          >
            <path d="M18 8a6 6 0 1 0-12 0c0 7-3 9-3 9h18s-3-2-3-9" />
            <path d="M13.7 21a2 2 0 0 1-3.4 0" />
          </svg>
        </button>
      </el-badge>

      <!-- 头像 -->
      <div
        class="flex h-[38px] w-[38px] items-center justify-center rounded-full text-sm font-semibold text-white"
        style="background: linear-gradient(135deg, var(--el-color-primary), var(--el-color-primary-dark-2))"
      >
        笑
      </div>
    </div>
  </header>
</template>

<style scoped>
/* 搜索胶囊：玻璃拟态 · 半透明白底 + 细描边 + 圆角 20px + 13px 输入
   背景 white 5% · 边 line(rgba 255,255,255,.08) · 聚焦品牌蓝 */
.search-capsule :deep(.el-input__wrapper) {
  background-color: rgba(255, 255, 255, 0.05);
  box-shadow: 0 0 0 1px rgba(255, 255, 255, 0.08) inset;
  border-radius: 20px;
  height: 36px;
  padding: 0 14px;
  transition: box-shadow 0.2s ease;
}
/* 聚焦：描边转品牌蓝，去除 EP 默认外环 */
.search-capsule :deep(.el-input__wrapper.is-focus) {
  box-shadow: 0 0 0 1px var(--el-color-primary) inset;
}
.search-capsule :deep(.el-input__inner) {
  color: var(--el-text-color-primary);
  font-size: 13px;
}
.search-capsule :deep(.el-input__inner)::placeholder {
  color: var(--el-text-color-placeholder);
}
.search-capsule :deep(.el-input__prefix) {
  color: var(--el-text-color-secondary);
  margin-right: 6px;
}
</style>
