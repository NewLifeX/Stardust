import { describe, expect, it, vi } from 'vitest';
import {
  ALL_PROJECTS,
  FALLBACK_PROJECTS,
  findProjectByName,
  pickValidProjects,
  resolveProjects,
  type ProjectSwitcherDeps,
} from './useProjectSwitcher.ts';

function makeDeps(over: Partial<ProjectSwitcherDeps> = {}): ProjectSwitcherDeps {
  return {
    fetchProjects: async () => [],
    currentName: () => '',
    select: vi.fn(),
    markReady: vi.fn(),
    ...over,
  };
}

describe('pickValidProjects', () => {
  it('不是数组时返回空数组', () => {
    expect(pickValidProjects(undefined)).toEqual([]);
    expect(pickValidProjects({ name: 'A' })).toEqual([]);
  });

  it('丢弃没有名字或名字为空的项目', () => {
    const list = [{ id: 1, name: 'A' }, { id: 2 }, { id: 3, name: '' }, null];
    expect(pickValidProjects(list)).toEqual([{ id: 1, name: 'A' }]);
  });
});

describe('FALLBACK_PROJECTS', () => {
  it('只有「全部项目」一项，且没有 id', () => {
    expect(FALLBACK_PROJECTS).toEqual([ALL_PROJECTS]);
    expect(ALL_PROJECTS.name).toBe('全部项目');
    expect(ALL_PROJECTS.id).toBeUndefined();
  });
});

describe('resolveProjects', () => {
  it('取到项目：「全部项目」排第一，真实项目在后，并标记就绪', async () => {
    const markReady = vi.fn();
    const deps = makeDeps({
      fetchProjects: async () => [{ id: 1, name: 'A' }, { id: 2, name: 'B' }],
      currentName: () => 'A',
      markReady,
    });
    const r = await resolveProjects(deps);
    expect(r.map((p) => p.name)).toEqual(['全部项目', 'A', 'B']);
    expect(markReady).toHaveBeenCalledTimes(1);
  });

  it('首次进入（还没选过）：默认选第一个真实项目，不选「全部项目」', async () => {
    const select = vi.fn();
    const deps = makeDeps({
      fetchProjects: async () => [{ id: 1, name: 'A' }, { id: 2, name: 'B' }],
      currentName: () => '',
      select,
    });
    await resolveProjects(deps);
    expect(select).toHaveBeenCalledTimes(1);
    expect(select).toHaveBeenCalledWith({ id: 1, name: 'A' });
  });

  it('当前选中的项目仍在列表里：不改选择', async () => {
    const select = vi.fn();
    const deps = makeDeps({
      fetchProjects: async () => [{ id: 1, name: 'A' }, { id: 2, name: 'B' }],
      currentName: () => 'B',
      select,
    });
    await resolveProjects(deps);
    expect(select).not.toHaveBeenCalled();
  });

  it('当前选中的是「全部项目」：刷新后不会被改回第一个项目', async () => {
    const select = vi.fn();
    const deps = makeDeps({
      fetchProjects: async () => [{ id: 1, name: 'A' }],
      currentName: () => '全部项目',
      select,
    });
    await resolveProjects(deps);
    expect(select).not.toHaveBeenCalled();
  });

  it('当前选中的项目已不存在：改选第一个真实项目', async () => {
    const select = vi.fn();
    const deps = makeDeps({
      fetchProjects: async () => [{ id: 1, name: 'A' }],
      currentName: () => '已被删除的项目',
      select,
    });
    await resolveProjects(deps);
    expect(select).toHaveBeenCalledWith({ id: 1, name: 'A' });
  });

  it('拉取失败：回退到兜底列表，调用 warn，仍然标记就绪，不改选择', async () => {
    const select = vi.fn();
    const markReady = vi.fn();
    const warn = vi.fn();
    const deps = makeDeps({
      fetchProjects: async () => {
        throw new Error('boom');
      },
      select,
      markReady,
      warn,
    });
    const r = await resolveProjects(deps);
    expect(r).toEqual([...FALLBACK_PROJECTS]);
    expect(warn).toHaveBeenCalledTimes(1);
    expect(markReady).toHaveBeenCalledTimes(1);
    expect(select).not.toHaveBeenCalled();
  });

  it('后端没有项目（空数组或非数组）：只有「全部项目」，仍然标记就绪', async () => {
    const markReady = vi.fn();
    const select = vi.fn();
    const empty = await resolveProjects(makeDeps({ fetchProjects: async () => [], markReady, select }));
    const bad = await resolveProjects(makeDeps({ fetchProjects: async () => undefined, markReady, select }));
    expect(empty).toEqual([ALL_PROJECTS]);
    expect(bad).toEqual([ALL_PROJECTS]);
    expect(markReady).toHaveBeenCalledTimes(2);
    expect(select).not.toHaveBeenCalled();
  });
});

describe('findProjectByName', () => {
  it('能按名字找到「全部项目」和真实项目', () => {
    const list = [ALL_PROJECTS, { id: 1, name: 'A' }];
    expect(findProjectByName(list, '全部项目')).toBe(ALL_PROJECTS);
    expect(findProjectByName(list, 'A')).toEqual({ id: 1, name: 'A' });
  });

  it('找不到返回 undefined', () => {
    expect(findProjectByName([ALL_PROJECTS], '不存在')).toBeUndefined();
  });
});