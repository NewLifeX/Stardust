// 界面开关单测（Vitest）
// 对照原型 kit-app.jsx：palette 为 {placeholder, items} | null，⌘K 只会打开；
// toggleDock(open, tab) 的 open 缺省为翻转，tab 缺省为保持。
import { describe, it, expect, beforeEach } from 'vitest';
import * as store from './ui.store';
import {
  palette,
  paletteOpen,
  guard,
  guardOpen,
  dockOpen,
  dockTab,
  openPalette,
  closePalette,
  openGuard,
  cancelGuard,
  confirmGuard,
  openDock,
  closeDock,
  toggleDock,
  setDockTab,
  resetUiState,
  type PaletteConfig,
} from './ui.store';

const run = () => {};
const cfg = (placeholder: string): PaletteConfig => ({
  placeholder,
  groups: [{ title: '页面', list: [{ text: '跳转 · 此刻', hint: '首页', run }] }],
});

beforeEach(() => {
  resetUiState();
});

describe('ui.store · 初始状态', () => {
  it('面板初始为 null', () => {
    expect(palette.value).toBeNull();
  });

  it('paletteOpen 初始为 false', () => {
    expect(paletteOpen.value).toBe(false);
  });

  it('控制台初始关闭', () => {
    expect(dockOpen.value).toBe(false);
  });

  it('控制台初始页签为日志（原型 kit-app.jsx:39）', () => {
    expect(dockTab.value).toBe('log');
  });
});

describe('ui.store · 命令面板', () => {
  const a = cfg('搜索应用、节点、TraceId,或执行操作…');

  it('openPalette 后 paletteOpen 为 true', () => {
    openPalette(a);
    expect(paletteOpen.value).toBe(true);
  });

  it('取回的配置就是传入的对象（浅层持有）', () => {
    openPalette(a);
    expect(palette.value).toBe(a);
  });

  it('占位文案保持', () => {
    openPalette(a);
    expect(palette.value?.placeholder).toBe('搜索应用、节点、TraceId,或执行操作…');
  });

  it('run 函数未被代理，仍是同一个引用', () => {
    openPalette(a);
    expect(palette.value?.groups[0].list[0].run).toBe(run);
  });

  it('再次打开用新配置替换（动词键替换面板内容）', () => {
    const b = cfg('发布哪个应用？');
    openPalette(a);
    openPalette(b);
    expect(palette.value).toBe(b);
  });

  it('closePalette 后回到 null', () => {
    openPalette(a);
    closePalette();
    expect(palette.value).toBeNull();
    expect(paletteOpen.value).toBe(false);
  });

  it('重复关闭无副作用', () => {
    openPalette(a);
    closePalette();
    closePalette();
    expect(paletteOpen.value).toBe(false);
  });

  it('不导出 togglePalette（原型 ⌘K 没有关闭分支）', () => {
    expect('togglePalette' in store).toBe(false);
  });
});

describe('ui.store · 底部控制台', () => {
  it('无参 toggleDock 翻转为打开', () => {
    toggleDock();
    expect(dockOpen.value).toBe(true);
  });

  it('翻转不改页签', () => {
    toggleDock();
    expect(dockTab.value).toBe('log');
  });

  it('再翻转为关闭', () => {
    toggleDock();
    toggleDock();
    expect(dockOpen.value).toBe(false);
  });

  it("openDock('term') 打开并切到终端（对应 openTerm）", () => {
    openDock('term');
    expect(dockOpen.value).toBe(true);
    expect(dockTab.value).toBe('term');
  });

  it('openDock() 不传 tab 保持当前页签', () => {
    openDock('term');
    openDock();
    expect(dockOpen.value).toBe(true);
    expect(dockTab.value).toBe('term');
  });

  it("已开且在终端时 openDock('log') 切回日志并保持打开", () => {
    openDock('term');
    openDock('log');
    expect(dockOpen.value).toBe(true);
    expect(dockTab.value).toBe('log');
  });

  it('closeDock 只关闭，页签保持', () => {
    openDock('log');
    closeDock();
    expect(dockOpen.value).toBe(false);
    expect(dockTab.value).toBe('log');
  });

  it('重复关闭无副作用', () => {
    closeDock();
    closeDock();
    expect(dockOpen.value).toBe(false);
  });

  it('setDockTab 只换页签，不改开关', () => {
    setDockTab('term');
    expect(dockTab.value).toBe('term');
    expect(dockOpen.value).toBe(false);
  });

  it('打开状态下换页签保持打开', () => {
    openDock();
    setDockTab('log');
    expect(dockTab.value).toBe('log');
    expect(dockOpen.value).toBe(true);
  });
});

describe('ui.store · 复位与相互独立', () => {
  const a = cfg('搜索应用、节点、TraceId,或执行操作…');

  it('resetUiState 全部复位', () => {
    openPalette(a);
    openDock('term');
    resetUiState();
    expect(palette.value).toBeNull();
    expect(dockOpen.value).toBe(false);
    expect(dockTab.value).toBe('log');
  });

  it('开面板不影响控制台', () => {
    openPalette(a);
    expect(dockOpen.value).toBe(false);
  });

  it('关面板不影响控制台', () => {
    openPalette(a);
    toggleDock();
    closePalette();
    expect(dockOpen.value).toBe(true);
  });
});

describe('ui.store · 生产确认（guard）', () => {
  it('初始为 null，guardOpen 为 false', () => {
    expect(guard.value).toBeNull();
    expect(guardOpen.value).toBe(false);
  });

  it('openGuard 后 guardOpen 为 true，label 与 fn 原样保持', () => {
    const fn = () => {};
    openGuard('发布 bj-app-01 的配置（3 项变更）', fn);
    expect(guardOpen.value).toBe(true);
    expect(guard.value?.label).toBe('发布 bj-app-01 的配置（3 项变更）');
    expect(guard.value?.fn).toBe(fn);
  });

  it('再次 openGuard 用新请求替换（原型 doGuard 就是 setGuard）', () => {
    openGuard('第一个', () => {});
    const second = () => {};
    openGuard('第二个', second);
    expect(guard.value?.label).toBe('第二个');
    expect(guard.value?.fn).toBe(second);
  });

  it('cancelGuard 关闭弹窗，不执行回调', () => {
    const log: string[] = [];
    openGuard('回滚', () => {
      log.push('run');
    });
    cancelGuard();
    expect(guardOpen.value).toBe(false);
    expect(guard.value).toBeNull();
    expect(log).toHaveLength(0);
  });

  it('confirmGuard 执行回调并关闭弹窗', () => {
    const log: string[] = [];
    openGuard('发布', () => {
      log.push('run');
    });
    confirmGuard();
    expect(log.join()).toBe('run');
    expect(guardOpen.value).toBe(false);
  });

  it('confirmGuard 先关弹窗再执行回调（回调执行时 guardOpen 已是 false）', () => {
    let openInside: boolean | null = null;
    openGuard('发布', () => {
      openInside = guardOpen.value;
    });
    confirmGuard();
    expect(openInside).toBe(false);
  });

  it('回调里再打开新的确认，新确认保持打开，不被误关', () => {
    openGuard('第一步', () => {
      openGuard('第二步', () => {});
    });
    confirmGuard();
    expect(guardOpen.value).toBe(true);
    expect(guard.value?.label).toBe('第二步');
  });

  it('异步回调不阻塞关闭：confirmGuard 返回时弹窗已关', () => {
    openGuard('推送', async () => {
      await Promise.resolve();
    });
    confirmGuard();
    expect(guardOpen.value).toBe(false);
  });

  it('未打开时 confirmGuard 无副作用', () => {
    expect(() => confirmGuard()).not.toThrow();
    expect(guardOpen.value).toBe(false);
  });

  it('未打开时 cancelGuard 无副作用', () => {
    expect(() => cancelGuard()).not.toThrow();
    expect(guardOpen.value).toBe(false);
  });

  it('resetUiState 清掉 guard', () => {
    openGuard('发布', () => {});
    resetUiState();
    expect(guard.value).toBeNull();
    expect(guardOpen.value).toBe(false);
  });

  it('开确认不影响面板与控制台，关确认也不影响', () => {
    openPalette(cfg('搜索…'));
    toggleDock();
    openGuard('发布', () => {});
    expect(paletteOpen.value).toBe(true);
    expect(dockOpen.value).toBe(true);
    cancelGuard();
    expect(paletteOpen.value).toBe(true);
    expect(dockOpen.value).toBe(true);
  });
});
