// StardustLayout2 顶栏「项目选择器」的纯逻辑：不依赖 Vue 生命周期，外部依赖全部由调用方传入，便于测试。
// 行为沿用旧 StardustLayout/TopBar.vue 的 loadProjects：取不到数据时回退到只有「全部项目」的列表；
// 无论成功、失败还是兜底，结束时都必须 markReady()，否则各页面首轮取数会被卡住或重复打请求。

export interface GalaxyProject {
  id?: number;
  name?: string;
}

/**
 * 「全部项目」：不带 id。选中后 setStardustProject 把 projectId 置 null，
 * 全局拦截器不再注入 projectId，后端按默认值 -1 返回全部项目。
 */
export const ALL_PROJECTS: GalaxyProject = { name: '全部项目' };

/** 后端不可用时只保留「全部项目」（原来三个演示项目 id 都是 0，实际等同于全部项目，却显示成三个名字） */
export const FALLBACK_PROJECTS: readonly GalaxyProject[] = [ALL_PROJECTS];

export interface ProjectSwitcherDeps {
  /** 拉取项目列表；Cube 响应体为 { code, data } 时由调用方取出 data 数组再返回 */
  fetchProjects: () => Promise<unknown>;
  /** 当前上下文里已选中的项目名 */
  currentName: () => string;
  /** 写回上下文（对应 setStardustProject） */
  select: (p: GalaxyProject) => void;
  /** 标记上下文已就绪（对应 markStardustContextReady） */
  markReady: () => void;
  warn?: (e: unknown) => void;
}

/** 只保留有名字的项目 */
export function pickValidProjects(list: unknown): GalaxyProject[] {
  if (!Array.isArray(list)) return [];
  return list.filter((p): p is GalaxyProject => typeof p?.name === 'string' && p.name.length > 0);
}

/**
 * 解析项目列表。取到真实项目时，「全部项目」排第一；
 * 当前选中项（含「全部项目」）不在列表里时，自动选第一个真实项目（首次进入也走这条，不会一上来就查全量）。
 * 返回要展示的列表。
 */
export async function resolveProjects(deps: ProjectSwitcherDeps): Promise<GalaxyProject[]> {
  let projects: GalaxyProject[] = [...FALLBACK_PROJECTS];
  try {
    const valid = pickValidProjects(await deps.fetchProjects());
    if (valid.length > 0) {
      projects = [ALL_PROJECTS, ...valid];
      if (!projects.some((p) => p.name === deps.currentName())) deps.select(valid[0]);
    }
  } catch (e) {
    deps.warn?.(e);
  } finally {
    deps.markReady();
  }
  return projects;
}

/** 原生 select 按项目名回传，这里按名字找回项目对象 */
export function findProjectByName(projects: readonly GalaxyProject[], name: string): GalaxyProject | undefined {
  return projects.find((p) => p.name === name);
}