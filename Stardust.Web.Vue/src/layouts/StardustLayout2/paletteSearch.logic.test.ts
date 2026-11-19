// openSearchPalette 单测（Vitest）：先开基础面板、数据到达原位替换、失败可见提示、
// 面板已关/已切走时迟到数据不弹面板不顶面板（ui.store 的 openPalette 语义是「替换/打开」）。
import { describe, it, expect, beforeEach } from 'vitest';
import { openSearchPalette, initSearchRuntime, searchRuntime, SEARCH_DATA_FAILED_MESSAGE } from './paletteSearch.logic';
import { buildBasePalette } from './palette.items';
import { openPalette, closePalette, palette, paletteOpen, resetUiState } from './ui.store';
import type { PaletteData, PaletteDataPart } from './paletteData.logic';
import type { PaletteRuntime } from './paletteRuntime.logic';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

function runtimeOf(impl: Partial<Pick<PaletteRuntime, 'ensure'>>): PaletteRuntime {
  return { peek: () => null, ensure: async () => ({} as PaletteData), invalidate: () => {}, ...impl };
}

function data(apps: number, nodes: number, failed: PaletteDataPart[] = []): PaletteData {
  return {
    apps: Array.from({ length: apps }, (_, i) => ({ id: i + 1, name: `app-${i + 1}`, online: true })),
    nodes: Array.from({ length: nodes }, (_, i) => ({ id: `n${i + 1}`, name: `node-${i + 1}`, online: true })),
    failed,
  };
}

beforeEach(() => {
  resetUiState();
});

describe('openSearchPalette · 打开', () => {
  it('立即打开基础面板（不等网络）：占位符 + traceJump 接线', () => {
    openSearchPalette({ go: () => {}, goTrace: () => {} }, runtimeOf({ ensure: async () => data(0, 0) }));
    expect(paletteOpen.value).toBe(true);
    expect(palette.value?.placeholder).toBe(buildBasePalette({ go: () => {} }).placeholder);
    expect(palette.value?.traceJump).toBeTruthy();
    expect(palette.value?.groups.some((g) => g.title === '页面')).toBe(true);
  });

  it('没传 goTrace：配置不带 traceJump', () => {
    openSearchPalette({ go: () => {} }, runtimeOf({ ensure: async () => data(0, 0) }));
    expect(palette.value?.traceJump).toBeUndefined();
  });

  it('没有运行时（外壳未初始化）：只开基础面板，不报错', () => {
    openSearchPalette({ go: () => {}, goTrace: () => {} }, null);
    expect(paletteOpen.value).toBe(true);
    expect(palette.value?.groups.some((g) => g.title === '页面')).toBe(true);
  });
});

describe('openSearchPalette · 数据到达后替换', () => {
  it('ensure 成功：面板替换为带应用/节点的配置（节点组需 openNode）', async () => {
    openSearchPalette(
      { go: () => {}, goTrace: () => {}, openNode: () => {} },
      runtimeOf({ ensure: async () => data(2, 3) }),
    );
    // 宏任务排空：ensure 的 then/catch 链跑完
    await new Promise((r) => setTimeout(r, 0));
    expect(palette.value?.groups.some((g) => g.title === '应用')).toBe(true);
    expect(palette.value?.groups.some((g) => g.title === '节点')).toBe(true);
    expect(palette.value?.groups.find((g) => g.title === '应用')?.list).toHaveLength(2);
  });

  it('面板已被用户关闭：迟到数据不重新弹面板', async () => {
    openSearchPalette({ go: () => {}, goTrace: () => {} }, runtimeOf({ ensure: async () => data(2, 3) }));
    closePalette();
    await new Promise((r) => setTimeout(r, 0));
    expect(paletteOpen.value).toBe(false);
  });

  it('用户已切到别的面板（如动词键）：迟到数据不顶掉现面板', async () => {
    openSearchPalette({ go: () => {}, goTrace: () => {} }, runtimeOf({ ensure: async () => data(2, 3) }));
    const verbPalette = { placeholder: '发布哪个应用？', groups: [] };
    openPalette(verbPalette);
    await new Promise((r) => setTimeout(r, 0));
    expect(palette.value).toBe(verbPalette);
  });

  it('替换后的新配置仍带 traceJump（追踪能力不丢）', async () => {
    openSearchPalette({ go: () => {}, goTrace: () => {} }, runtimeOf({ ensure: async () => data(1, 1) }));
    await new Promise((r) => setTimeout(r, 0));
    expect(palette.value?.traceJump?.('aaa111')?.title).toBe('链路追踪');
  });
});

describe('openSearchPalette · 失败可见提示（AGENTS §11）', () => {
  it('ensure 失败且面板仍开着：调用 warn 提示，面板保留基础形态', async () => {
    const warns: string[] = [];
    openSearchPalette(
      { go: () => {}, goTrace: () => {}, warn: (m) => warns.push(m) },
      runtimeOf({ ensure: async () => Promise.reject(new Error('network')) }),
    );
    await new Promise((r) => setTimeout(r, 0));
    await Promise.resolve();
    expect(warns).toEqual([SEARCH_DATA_FAILED_MESSAGE]);
    expect(paletteOpen.value).toBe(true);
  });

  it('ensure 失败但面板已关：不提示（用户已放弃搜索）', async () => {
    const warns: string[] = [];
    openSearchPalette(
      { go: () => {}, goTrace: () => {}, warn: (m) => warns.push(m) },
      runtimeOf({ ensure: async () => Promise.reject(new Error('network')) }),
    );
    closePalette();
    await new Promise((r) => setTimeout(r, 0));
    await Promise.resolve();
    expect(warns).toEqual([]);
  });

  it('没传 warn：静默降级，不抛错', async () => {
    openSearchPalette({ go: () => {} }, runtimeOf({ ensure: async () => Promise.reject(new Error('x')) }));
    await new Promise((r) => setTimeout(r, 0));
    expect(paletteOpen.value).toBe(true);
  });
});

describe('共享运行时单例（外壳与页面同一份缓存）', () => {
  it('initSearchRuntime 幂等：重复初始化返回同一实例，searchRuntime 可取到，invalidate 不抛', () => {
    const req = (() => Promise.resolve({ data: { data: { list: [] } } })) as unknown as Req;
    const a = initSearchRuntime(req);
    const b = initSearchRuntime(req);
    expect(b).toBe(a);
    expect(searchRuntime()).toBe(a);
    a.invalidate();
  });
});
