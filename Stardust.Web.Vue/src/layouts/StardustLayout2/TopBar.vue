<script setup lang="ts">
// 顶栏（纯 UI）：数据与行为全部由父级传入，事件向上抛。结构对照原型 kit-shell.jsx 的 TopBar。
import logoMark from '@/assets/brand/logo-mark.svg';
import KIcon from './KIcon.vue';
import NavSegment from './NavSegment.vue';
import ProdGuardLine from './ProdGuardLine.vue';
import type { NavKey } from './nav.logic';
import type { GalaxyProject } from './useProjectSwitcher';

defineProps<{
  projects: GalaxyProject[];
  projectName: string;
  theme: 'dark' | 'light';
  initial: string;
  active: NavKey | null;
  nowCount?: number;
}>();
const emit = defineEmits<{
  go: [path: string];
  project: [name: string];
  'toggle-theme': [];
  search: [];
  logout: [];
}>();
</script>

<template>
  <header
    data-spec="topbar"
    class="relative z-20 flex h-topbar shrink-0 items-center gap-3 border-b border-[var(--el-border-color-light)] bg-[color-mix(in_oklab,var(--el-bg-color-page)_82%,transparent)] px-5 backdrop-blur-[12px]"
  >
    <ProdGuardLine />

    <button
      type="button"
      data-spec="brand"
      class="flex shrink-0 cursor-pointer items-center gap-[9px]"
      @click="emit('go', '/now')"
    >
      <img
        :src="logoMark"
        class="block h-6 w-6"
        alt="Stardust"
      >
      <b class="text-[15px] font-semibold tracking-[-0.01em] text-[var(--el-text-color-primary)]">星尘</b>
      <small class="font-mono text-[10px] tracking-[0.14em] text-[var(--el-text-color-secondary)]">STARDUST</small>
    </button>

    <div class="relative">
      <select
        data-spec="project-selector"
        aria-label="切换项目"
        :value="projectName"
        class="h-[30px] max-w-[208px] cursor-pointer appearance-none rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color)] pl-[10px] pr-[26px] text-[12.5px] text-[var(--el-text-color-regular)] hover:border-[var(--el-border-color)] hover:text-[var(--el-text-color-primary)] max-[1180px]:max-w-[150px]"
        @change="emit('project', ($event.target as HTMLSelectElement).value)"
      >
        <option
          v-for="p in projects"
          :key="p.name"
          :value="p.name"
        >
          {{ p.name }}
        </option>
      </select>
      <KIcon
        name="ChevronDown"
        :size="12"
        class="pointer-events-none absolute right-[9px] top-1/2 -translate-y-1/2 text-[var(--el-text-color-secondary)]"
      />
    </div>

    <NavSegment
      :active="active"
      :now-count="nowCount"
      @go="emit('go', $event)"
    />

    <span class="flex-1" />

    <span
      data-spec="env-tag"
      class="inline-flex h-[26px] items-center gap-[6px] whitespace-nowrap rounded-full border border-[color-mix(in_oklab,var(--el-color-danger)_35%,transparent)] bg-[color-mix(in_oklab,var(--el-color-danger)_10%,transparent)] px-[9px] font-mono text-[10.5px] uppercase tracking-[0.08em] text-[var(--el-color-danger)] max-[900px]:hidden"
    >
      <i class="h-[5px] w-[5px] rounded-full bg-current" />生产 · 仅生产
    </span>

    <button
      type="button"
      data-spec="search-trigger"
      class="flex h-[30px] min-w-[188px] cursor-pointer items-center gap-2 rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color)] px-[10px] text-[12.5px] text-[var(--el-text-color-secondary)] transition-colors duration-[120ms] hover:border-[var(--el-border-color)] hover:text-[var(--el-text-color-regular)] max-[1180px]:min-w-0"
      @click="emit('search')"
    >
      <KIcon
        name="Search"
        :size="13"
      />
      <span class="max-[1180px]:hidden">搜索 / 执行…</span>
      <span class="ml-auto rounded-sm border border-[var(--el-border-color-light)] bg-[var(--el-fill-color-light)] px-[5px] py-px font-mono text-[10.5px] text-[var(--el-text-color-secondary)] max-[1180px]:hidden">⌘K</span>
    </button>

    <button
      type="button"
      data-spec="theme-toggle"
      title="切换主题"
      class="grid h-[30px] w-[30px] cursor-pointer place-items-center rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color)] text-[var(--el-text-color-regular)] transition-colors duration-[120ms] hover:bg-[var(--el-bg-color-overlay)] hover:text-[var(--el-text-color-primary)]"
      @click="emit('toggle-theme')"
    >
      <KIcon
        :name="theme === 'dark' ? 'Sun' : 'Moon'"
        :size="15"
      />
    </button>

    <el-dropdown
      trigger="click"
      @command="emit('logout')"
    >
      <button
        type="button"
        data-spec="avatar"
        class="grid h-[28px] w-[28px] shrink-0 cursor-pointer place-items-center rounded-full bg-[var(--el-color-primary)] text-[12px] font-semibold text-[var(--el-color-white)]"
      >
        {{ initial }}
      </button>
      <template #dropdown>
        <el-dropdown-menu>
          <el-dropdown-item command="logout">
            退出登录
          </el-dropdown-item>
        </el-dropdown-menu>
      </template>
    </el-dropdown>
  </header>
</template>
