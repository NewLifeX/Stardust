// StardustLayout2 外壳的接线层：把路由、主题、用户、项目上下文接到纯逻辑上，给 index.vue 用。
// 判断逻辑都在 nav.logic.ts / useProjectSwitcher.ts / avatar.logic.ts，这里只负责组装。
import { computed, onMounted, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { request } from '@newlifex/cube-vue/core/utils/request';
import { useUserStore } from '@newlifex/cube-vue/core/stores/user';
import { stardustContext, setStardustProject, markStardustContextReady } from '@/stores/stardustContext';
import { theme, toggleTheme } from '@/design-system/theme';
import { avatarInitial } from './avatar.logic';
import { resolveActiveNav } from './nav.logic';
import { FALLBACK_PROJECTS, findProjectByName, resolveProjects, type GalaxyProject } from './useProjectSwitcher';

export function useConsoleShell() {
  const route = useRoute();
  const router = useRouter();
  const userStore = useUserStore();
  const projects = ref<GalaxyProject[]>([...FALLBACK_PROJECTS]);

  const active = computed(() => resolveActiveNav(route.path));
  const projectName = computed(() => stardustContext.projectName);
  const initial = computed(() => avatarInitial(userStore.userInfo?.displayName || userStore.userInfo?.name));

  const selectProject = (p: GalaxyProject) => setStardustProject(p.id ?? null, p.name ?? '');

  onMounted(async () => {
    projects.value = await resolveProjects({
      fetchProjects: async () => (await request({ url: '/Platform/GalaxyProject', method: 'GET', params: { PageSize: 200 } }))?.data,
      currentName: () => stardustContext.projectName,
      select: selectProject,
      markReady: markStardustContextReady,
      warn: (e) => console.warn('加载项目列表失败，使用静态兜底', e),
    });
  });

  return {
    projects,
    projectName,
    active,
    initial,
    theme,
    toggleTheme,
    go: (path: string) => void router.push(path),
    pickProject: (name: string) => {
      const p = findProjectByName(projects.value, name);
      if (p) selectProject(p);
    },
    logout: () => void userStore.logout(),
  };
}
