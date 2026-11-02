// 全局热键接线的单测（Vitest）
import { describe, it, expect } from 'vitest';
import { useGlobalHotkeys, type GlobalHotkeyDeps, type HotkeyEventLike } from './useGlobalHotkeys';

const BACKTICK = '\x60';

interface Flags {
  typing?: boolean;
  palette?: boolean;
  guard?: boolean;
  dialog?: boolean;
  drawer?: boolean;
  dock?: boolean;
}

/** wire：是否传入控制台与动词键的处理函数（模拟这两块页面是否已接入） */
function setup(flags: Flags = {}, wire = false) {
  const log: string[] = [];
  const deps: GlobalHotkeyDeps = {
    isTyping: () => !!flags.typing,
    paletteOpen: () => !!flags.palette,
    guardOpen: () => !!flags.guard,
    dialogOpen: () => !!flags.dialog,
    drawerOpen: () => !!flags.drawer,
    dockOpen: () => !!flags.dock,
    openPalette: () => log.push('open'),
    closePalette: () => log.push('close'),
  };
  if (wire) {
    deps.closeDock = () => log.push('closeDock');
    deps.toggleDock = () => log.push('toggleDock');
    deps.onVerb = (v) => log.push(`verb:${v}`);
    deps.cancelGuard = () => log.push('cancelGuard');
  }
  return { log, hk: useGlobalHotkeys(deps) };
}

/** 构造按键事件，prevented() 报告是否调用过 preventDefault */
function press(init: Partial<HotkeyEventLike> & { key: string }) {
  let prevented = false;
  const e: HotkeyEventLike = { ...init, preventDefault: () => { prevented = true; } };
  return { e, prevented: () => prevented };
}

function fire(hk: ReturnType<typeof useGlobalHotkeys>, init: Partial<HotkeyEventLike> & { key: string }) {
  const p = press(init);
  const handled = hk.onKeydown(p.e);
  return { handled, prevented: p.prevented() };
}

describe('useGlobalHotkeys · ⌘K / Ctrl+K', () => {
  it('Ctrl+K 打开面板并阻止默认行为（地址栏聚焦）', () => {
    const { log, hk } = setup();
    const r = fire(hk, { key: 'k', ctrlKey: true });
    expect(r.handled).toBe(true);
    expect(r.prevented).toBe(true);
    expect(log.join()).toBe('open');
  });

  it('Cmd+K（大写 K）同样生效', () => {
    const { log, hk } = setup();
    const r = fire(hk, { key: 'K', metaKey: true });
    expect(r.handled).toBe(true);
    expect(r.prevented).toBe(true);
    expect(log.join()).toBe('open');
  });

  it('输入框内也生效', () => {
    const { log, hk } = setup({ typing: true });
    const r = fire(hk, { key: 'k', ctrlKey: true });
    expect(r.handled).toBe(true);
    expect(log.join()).toBe('open');
  });

  it('面板已打开时再按仍调用 openPalette（由 ui.store 替换配置）', () => {
    const { log, hk } = setup({ palette: true });
    fire(hk, { key: 'k', ctrlKey: true });
    expect(log.join()).toBe('open');
  });

  it('弹窗打开时也生效（与原型一致）', () => {
    const { log, hk } = setup({ dialog: true });
    fire(hk, { key: 'k', ctrlKey: true });
    expect(log.join()).toBe('open');
  });

  it('单独的 k 不处理', () => {
    const { log, hk } = setup();
    const r = fire(hk, { key: 'k' });
    expect(r.handled).toBe(false);
    expect(r.prevented).toBe(false);
    expect(log).toHaveLength(0);
  });
});

describe('useGlobalHotkeys · Esc', () => {
  it('面板打开：关闭面板', () => {
    const { log, hk } = setup({ palette: true }, true);
    const r = fire(hk, { key: 'Escape' });
    expect(r.handled).toBe(true);
    expect(r.prevented).toBe(true);
    expect(log.join()).toBe('close');
  });

  it('连按两次（全局监听与面板监听各一次）不报错，closePalette 被调用两次', () => {
    const { log, hk } = setup({ palette: true }, true);
    fire(hk, { key: 'Escape' });
    const r = fire(hk, { key: 'Escape' });
    expect(r.handled).toBe(true);
    expect(log.join()).toBe('close,close');
  });

  it('输入框内 Esc 同样关闭面板', () => {
    const { log, hk } = setup({ palette: true, typing: true }, true);
    fire(hk, { key: 'Escape' });
    expect(log.join()).toBe('close');
  });

  it('面板优先于弹窗与控制台', () => {
    const { log, hk } = setup({ palette: true, dialog: true, dock: true }, true);
    fire(hk, { key: 'Escape' });
    expect(log.join()).toBe('close');
  });

  it('只有控制台展开：关闭控制台', () => {
    const { log, hk } = setup({ dock: true }, true);
    const r = fire(hk, { key: 'Escape' });
    expect(r.handled).toBe(true);
    expect(r.prevented).toBe(true);
    expect(log.join()).toBe('closeDock');
  });

  it('控制台展开但没传 closeDock：不处理、不阻止', () => {
    const { log, hk } = setup({ dock: true }, false);
    const r = fire(hk, { key: 'Escape' });
    expect(r.handled).toBe(false);
    expect(r.prevented).toBe(false);
    expect(log).toHaveLength(0);
  });

  it('弹窗打开：Esc 交给 Element Plus，不关控制台', () => {
    const { log, hk } = setup({ dock: true, dialog: true }, true);
    const r = fire(hk, { key: 'Escape' });
    expect(r.handled).toBe(false);
    expect(r.prevented).toBe(false);
    expect(log).toHaveLength(0);
  });

  it('抽屉打开：同样不关控制台', () => {
    const { log, hk } = setup({ dock: true, drawer: true }, true);
    const r = fire(hk, { key: 'Escape' });
    expect(r.handled).toBe(false);
    expect(log).toHaveLength(0);
  });

  it('没有任何浮层：不处理', () => {
    const { log, hk } = setup({}, true);
    const r = fire(hk, { key: 'Escape' });
    expect(r.handled).toBe(false);
    expect(r.prevented).toBe(false);
    expect(log).toHaveLength(0);
  });
});

describe('useGlobalHotkeys · 生产确认（guard）', () => {
  it('Esc 调用 cancelGuard 并阻止默认行为', () => {
    const { log, hk } = setup({ guard: true }, true);
    const r = fire(hk, { key: 'Escape' });
    expect(r.handled).toBe(true);
    expect(r.prevented).toBe(true);
    expect(log.join()).toBe('cancelGuard');
  });

  it('没传 cancelGuard：不处理、不阻止（不吞按键）', () => {
    const { log, hk } = setup({ guard: true }, false);
    const r = fire(hk, { key: 'Escape' });
    expect(r.handled).toBe(false);
    expect(r.prevented).toBe(false);
    expect(log).toHaveLength(0);
  });

  it('护栏优先于控制台：一次 Esc 只关护栏', () => {
    const { log, hk } = setup({ guard: true, dock: true }, true);
    fire(hk, { key: 'Escape' });
    expect(log.join()).toBe('cancelGuard');
  });

  it('护栏优先于抽屉：一次 Esc 只关护栏', () => {
    const { log, hk } = setup({ guard: true, drawer: true }, true);
    fire(hk, { key: 'Escape' });
    expect(log.join()).toBe('cancelGuard');
  });

  it('面板优先于护栏', () => {
    const { log, hk } = setup({ palette: true, guard: true }, true);
    fire(hk, { key: 'Escape' });
    expect(log.join()).toBe('close');
  });

  it('护栏打开时反引号不触发', () => {
    const { log, hk } = setup({ guard: true }, true);
    expect(fire(hk, { key: BACKTICK }).handled).toBe(false);
    expect(log).toHaveLength(0);
  });

  it('护栏打开时动词键不触发', () => {
    const { log, hk } = setup({ guard: true }, true);
    expect(fire(hk, { key: 'p' }).handled).toBe(false);
    expect(log).toHaveLength(0);
  });

  it('护栏打开时 Ctrl+K 仍打开面板', () => {
    const { log, hk } = setup({ guard: true }, true);
    fire(hk, { key: 'k', ctrlKey: true });
    expect(log.join()).toBe('open');
  });
});

describe('useGlobalHotkeys · 输入法组词', () => {
  it('组词期间 Esc 不处理', () => {
    const { hk } = setup({ palette: true }, true);
    expect(fire(hk, { key: 'Escape', isComposing: true }).handled).toBe(false);
  });

  it('组词期间 Ctrl+K 不处理', () => {
    const { hk } = setup({ palette: true }, true);
    expect(fire(hk, { key: 'k', ctrlKey: true, isComposing: true }).handled).toBe(false);
  });

  it('组词期间字母键不处理', () => {
    const { hk } = setup({ palette: true }, true);
    expect(fire(hk, { key: 'p', isComposing: true }).handled).toBe(false);
  });

  it('组词期间没有任何处理函数被调用', () => {
    const { log, hk } = setup({ palette: true }, true);
    fire(hk, { key: 'Escape', isComposing: true });
    fire(hk, { key: 'k', ctrlKey: true, isComposing: true });
    fire(hk, { key: 'p', isComposing: true });
    expect(log).toHaveLength(0);
  });
});

describe('useGlobalHotkeys · 反引号', () => {
  it('没传 toggleDock：不处理、不阻止（不吞按键）', () => {
    const { log, hk } = setup({}, false);
    const r = fire(hk, { key: BACKTICK });
    expect(r.handled).toBe(false);
    expect(r.prevented).toBe(false);
    expect(log).toHaveLength(0);
  });

  it('传了 toggleDock：切换并阻止默认行为', () => {
    const { log, hk } = setup({}, true);
    const r = fire(hk, { key: BACKTICK });
    expect(r.handled).toBe(true);
    expect(r.prevented).toBe(true);
    expect(log.join()).toBe('toggleDock');
  });

  it('输入中不触发', () => {
    const { log, hk } = setup({ typing: true }, true);
    expect(fire(hk, { key: BACKTICK }).handled).toBe(false);
    expect(log).toHaveLength(0);
  });

  it('面板打开时不触发', () => {
    const { log, hk } = setup({ palette: true }, true);
    expect(fire(hk, { key: BACKTICK }).handled).toBe(false);
    expect(log).toHaveLength(0);
  });

  it('弹窗打开时不触发', () => {
    const { log, hk } = setup({ dialog: true }, true);
    expect(fire(hk, { key: BACKTICK }).handled).toBe(false);
    expect(log).toHaveLength(0);
  });

  it('抽屉打开时仍可用', () => {
    const { log, hk } = setup({ drawer: true }, true);
    fire(hk, { key: BACKTICK });
    expect(log.join()).toBe('toggleDock');
  });

  it('带 Alt 不触发', () => {
    const { log, hk } = setup({}, true);
    expect(fire(hk, { key: BACKTICK, altKey: true }).handled).toBe(false);
    expect(log).toHaveLength(0);
  });
});

describe('useGlobalHotkeys · 动词键 P/C/L/N/A', () => {
  it('没传 onVerb：不处理、不阻止', () => {
    const { log, hk } = setup({}, false);
    const r = fire(hk, { key: 'p' });
    expect(r.handled).toBe(false);
    expect(r.prevented).toBe(false);
    expect(log).toHaveLength(0);
  });

  it('小写 p 归一为 P，并阻止默认行为', () => {
    const { log, hk } = setup({}, true);
    const r = fire(hk, { key: 'p' });
    expect(r.handled).toBe(true);
    expect(r.prevented).toBe(true);
    expect(log.join()).toBe('verb:P');
  });

  it('五个动词键都能分发', () => {
    const { log, hk } = setup({}, true);
    fire(hk, { key: 'p' });
    fire(hk, { key: 'C' });
    fire(hk, { key: 'l' });
    fire(hk, { key: 'N' });
    fire(hk, { key: 'a' });
    expect(log.join()).toBe('verb:P,verb:C,verb:L,verb:N,verb:A');
  });

  it('输入中不触发', () => {
    const { log, hk } = setup({ typing: true }, true);
    expect(fire(hk, { key: 'p' }).handled).toBe(false);
    expect(log).toHaveLength(0);
  });

  it('面板打开时不触发', () => {
    const { log, hk } = setup({ palette: true }, true);
    expect(fire(hk, { key: 'p' }).handled).toBe(false);
    expect(log).toHaveLength(0);
  });

  it('弹窗打开时不触发', () => {
    const { log, hk } = setup({ dialog: true }, true);
    expect(fire(hk, { key: 'p' }).handled).toBe(false);
    expect(log).toHaveLength(0);
  });

  it('抽屉与控制台打开时仍可用', () => {
    const { log, hk } = setup({ drawer: true, dock: true }, true);
    fire(hk, { key: 'p' });
    expect(log.join()).toBe('verb:P');
  });

  it('带修饰键与普通键都不触发', () => {
    const { log, hk } = setup({}, true);
    expect(fire(hk, { key: 'p', metaKey: true }).handled).toBe(false);
    expect(fire(hk, { key: 'p', ctrlKey: true }).handled).toBe(false);
    expect(fire(hk, { key: 'p', altKey: true }).handled).toBe(false);
    expect(fire(hk, { key: 'x' }).handled).toBe(false);
    expect(fire(hk, { key: 'Enter' }).handled).toBe(false);
    expect(log).toHaveLength(0);
  });
});

describe('useGlobalHotkeys · 未传状态函数时按 false 处理', () => {
  it('没传 dialogOpen、drawerOpen、dockOpen 也能正常工作', () => {
    const log: string[] = [];
    const hk = useGlobalHotkeys({
      isTyping: () => false,
      paletteOpen: () => false,
      openPalette: () => log.push('open'),
      closePalette: () => log.push('close'),
      toggleDock: () => log.push('toggleDock'),
    });
    fire(hk, { key: BACKTICK });
    expect(log.join()).toBe('toggleDock');
  });
});
