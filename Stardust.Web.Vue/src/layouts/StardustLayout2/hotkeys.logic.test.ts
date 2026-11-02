// 键盘分发纯逻辑单测（Vitest）
// P-48 方案 A：弹窗与抽屉的 Esc 交给 Element Plus，本模块只管命令面板与底部控制台。
import { describe, it, expect } from 'vitest';
import { isTypingTarget, resolveHotkey, topEscapeTarget, type HotkeyState } from './hotkeys.logic';

const idle: HotkeyState = { typing: false, palette: false, dialog: false, drawer: false, dock: false };
const st = (p: Partial<HotkeyState>): HotkeyState => ({ ...idle, ...p });

describe('resolveHotkey · Cmd+K / Ctrl+K 打开命令面板', () => {
  it('Ctrl+K 打开面板', () => {
    expect(resolveHotkey({ key: 'k', ctrlKey: true }, idle)).toEqual({ type: 'palette' });
  });

  it('Cmd+K 打开面板', () => {
    expect(resolveHotkey({ key: 'k', metaKey: true }, idle)).toEqual({ type: 'palette' });
  });

  it('大写 K 同样生效', () => {
    expect(resolveHotkey({ key: 'K', ctrlKey: true }, idle)).toEqual({ type: 'palette' });
  });

  it('输入框内 Ctrl+K 仍生效', () => {
    expect(resolveHotkey({ key: 'k', ctrlKey: true }, st({ typing: true }))).toEqual({ type: 'palette' });
  });

  it('弹窗打开时 Ctrl+K 仍生效（与原型一致）', () => {
    expect(resolveHotkey({ key: 'k', ctrlKey: true }, st({ dialog: true }))).toEqual({ type: 'palette' });
  });

  it('单独 k 不处理', () => {
    expect(resolveHotkey({ key: 'k' }, idle)).toBeNull();
  });
});

describe('topEscapeTarget · Esc 只管命令面板与底部控制台（P-48）', () => {
  it('无浮层返回 null', () => {
    expect(topEscapeTarget(idle)).toBeNull();
  });

  it('面板最优先', () => {
    expect(topEscapeTarget(st({ palette: true, dialog: true, drawer: true, dock: true }))).toBe('palette');
  });

  it('只有底部控制台时关控制台', () => {
    expect(topEscapeTarget(st({ dock: true }))).toBe('dock');
  });

  it('dialog 打开时交给 Element Plus，不关控制台', () => {
    expect(topEscapeTarget(st({ dialog: true, dock: true }))).toBeNull();
  });

  it('drawer 打开时交给 Element Plus，不关控制台', () => {
    expect(topEscapeTarget(st({ drawer: true, dock: true }))).toBeNull();
  });

  it('dialog 与 drawer 同开也返回 null', () => {
    expect(topEscapeTarget(st({ dialog: true, drawer: true }))).toBeNull();
  });
});

describe('resolveHotkey · Esc 动作', () => {
  it('无浮层时 Esc 不处理', () => {
    expect(resolveHotkey({ key: 'Escape' }, idle)).toBeNull();
  });

  it('Esc 动作带目标：面板', () => {
    expect(resolveHotkey({ key: 'Escape' }, st({ palette: true, dock: true }))).toEqual({
      type: 'escape',
      target: 'palette',
    });
  });

  it('Esc 动作带目标：控制台', () => {
    expect(resolveHotkey({ key: 'Escape' }, st({ dock: true }))).toEqual({ type: 'escape', target: 'dock' });
  });

  it('抽屉打开时 Esc 不由本模块处理', () => {
    expect(resolveHotkey({ key: 'Escape' }, st({ drawer: true, dock: true }))).toBeNull();
  });

  it('输入框内 Esc 仍生效', () => {
    expect(resolveHotkey({ key: 'Escape' }, st({ typing: true, dock: true }))).toEqual({
      type: 'escape',
      target: 'dock',
    });
  });

  it('面板输入框内 Esc 关面板', () => {
    expect(resolveHotkey({ key: 'Escape' }, st({ typing: true, palette: true }))).toEqual({
      type: 'escape',
      target: 'palette',
    });
  });
});

describe('resolveHotkey · 动词键 P/C/L/N/A', () => {
  it.each(['P', 'C', 'L', 'N', 'A'])('%s 触发动词', (v) => {
    expect(resolveHotkey({ key: v }, idle)).toEqual({ type: 'verb', verb: v });
  });

  it('小写 p 归一为 P', () => {
    expect(resolveHotkey({ key: 'p' }, idle)).toEqual({ type: 'verb', verb: 'P' });
  });

  it('输入中不触发', () => {
    expect(resolveHotkey({ key: 'p' }, st({ typing: true }))).toBeNull();
  });

  it('Alt+P 不触发', () => {
    expect(resolveHotkey({ key: 'p', altKey: true }, idle)).toBeNull();
  });

  it('Ctrl+P 不触发', () => {
    expect(resolveHotkey({ key: 'p', ctrlKey: true }, idle)).toBeNull();
  });

  it('Cmd+P 不触发', () => {
    expect(resolveHotkey({ key: 'p', metaKey: true }, idle)).toBeNull();
  });

  it('面板打开时不触发', () => {
    expect(resolveHotkey({ key: 'p' }, st({ palette: true }))).toBeNull();
  });

  it('dialog 打开时不触发（确认框、向导）', () => {
    expect(resolveHotkey({ key: 'p' }, st({ dialog: true }))).toBeNull();
  });

  it('抽屉打开时仍可用', () => {
    expect(resolveHotkey({ key: 'p' }, st({ drawer: true }))?.type).toBe('verb');
  });

  it('控制台打开时仍可用', () => {
    expect(resolveHotkey({ key: 'p' }, st({ dock: true }))?.type).toBe('verb');
  });

  it('非动词键不处理', () => {
    expect(resolveHotkey({ key: 'x' }, idle)).toBeNull();
  });

  it('Enter 不被误判（多字符 key）', () => {
    expect(resolveHotkey({ key: 'Enter' }, idle)).toBeNull();
  });

  it('Alt 键本身不被误判', () => {
    expect(resolveHotkey({ key: 'Alt' }, idle)).toBeNull();
  });
});

describe('resolveHotkey · 反引号切换控制台', () => {
  it('反引号切换控制台', () => {
    expect(resolveHotkey({ key: '`' }, idle)).toEqual({ type: 'dock-toggle' });
  });

  it('输入中不触发', () => {
    expect(resolveHotkey({ key: '`' }, st({ typing: true }))).toBeNull();
  });

  it('面板打开时不触发', () => {
    expect(resolveHotkey({ key: '`' }, st({ palette: true }))).toBeNull();
  });

  it('dialog 打开时不触发', () => {
    expect(resolveHotkey({ key: '`' }, st({ dialog: true }))).toBeNull();
  });

  it('抽屉打开时仍可切换控制台', () => {
    expect(resolveHotkey({ key: '`' }, st({ drawer: true }))).toEqual({ type: 'dock-toggle' });
  });

  it('带修饰键不触发', () => {
    expect(resolveHotkey({ key: '`', ctrlKey: true }, idle)).toBeNull();
  });
});

describe('resolveHotkey · 生产确认（guard）', () => {
  it('topEscapeTarget：护栏优先于控制台', () => {
    expect(topEscapeTarget(st({ guard: true, dock: true }))).toBe('guard');
  });

  it('topEscapeTarget：面板优先于护栏', () => {
    expect(topEscapeTarget(st({ palette: true, guard: true }))).toBe('palette');
  });

  it('topEscapeTarget：护栏优先于 dialog 与 drawer', () => {
    expect(topEscapeTarget(st({ guard: true, dialog: true, drawer: true }))).toBe('guard');
  });

  it('guard 缺省按 false 处理：只有 dialog 时仍返回 null', () => {
    expect(topEscapeTarget(st({ dialog: true }))).toBeNull();
  });

  it('Esc 动作带目标：护栏', () => {
    expect(resolveHotkey({ key: 'Escape' }, st({ guard: true, dock: true }))).toEqual({
      type: 'escape',
      target: 'guard',
    });
  });

  it('输入框内 Esc 仍关护栏', () => {
    expect(resolveHotkey({ key: 'Escape' }, st({ guard: true, typing: true }))).toEqual({
      type: 'escape',
      target: 'guard',
    });
  });

  it('护栏打开时反引号不触发', () => {
    expect(resolveHotkey({ key: '`' }, st({ guard: true }))).toBeNull();
  });

  it.each(['P', 'C', 'L', 'N', 'A'])('护栏打开时动词键 %s 不触发', (v) => {
    expect(resolveHotkey({ key: v }, st({ guard: true }))).toBeNull();
  });

  it('护栏打开时 Ctrl+K 仍打开面板（与原型一致）', () => {
    expect(resolveHotkey({ key: 'k', ctrlKey: true }, st({ guard: true }))).toEqual({ type: 'palette' });
  });
});

describe('resolveHotkey · 接入向导（wizard）', () => {
  it('topEscapeTarget：向导优先于护栏', () => {
    expect(topEscapeTarget(st({ wizard: true, guard: true, dock: true }))).toBe('wizard');
  });

  it('topEscapeTarget：面板优先于向导', () => {
    expect(topEscapeTarget(st({ palette: true, wizard: true }))).toBe('palette');
  });

  it('topEscapeTarget：向导优先于 dialog 与 drawer', () => {
    expect(topEscapeTarget(st({ wizard: true, dialog: true, drawer: true }))).toBe('wizard');
  });

  it('topEscapeTarget：向导优先于控制台', () => {
    expect(topEscapeTarget(st({ wizard: true, dock: true }))).toBe('wizard');
  });

  it('wizard 缺省按 false 处理：只有 guard 时返回 guard', () => {
    expect(topEscapeTarget(st({ guard: true }))).toBe('guard');
  });

  it('Esc 动作带目标：向导', () => {
    expect(resolveHotkey({ key: 'Escape' }, st({ wizard: true, dock: true }))).toEqual({
      type: 'escape',
      target: 'wizard',
    });
  });

  it('输入框内 Esc 仍关向导', () => {
    expect(resolveHotkey({ key: 'Escape' }, st({ wizard: true, typing: true }))).toEqual({
      type: 'escape',
      target: 'wizard',
    });
  });

  it('向导打开时反引号不触发', () => {
    expect(resolveHotkey({ key: '`' }, st({ wizard: true }))).toBeNull();
  });

  it.each(['P', 'C', 'L', 'N', 'A'])('向导打开时动词键 %s 不触发', (v) => {
    expect(resolveHotkey({ key: v }, st({ wizard: true }))).toBeNull();
  });

  it('向导打开时 Ctrl+K 仍打开面板', () => {
    expect(resolveHotkey({ key: 'k', ctrlKey: true }, st({ wizard: true }))).toEqual({ type: 'palette' });
  });
});

describe('isTypingTarget · 判定焦点是否在输入类元素', () => {
  // 显式标注元素类型：it.each 的元组各行类型会被推断成联合，
  // 未标注时 TS 会把两个 it.each 的回调签名判为不兼容（TS2345）。
  const typingCases: Array<[{ tagName?: string; isContentEditable?: boolean }, string]> = [
    [{ tagName: 'INPUT' }, 'INPUT'],
    [{ tagName: 'select' }, 'select 不区分大小写'],
    [{ tagName: 'TEXTAREA' }, 'TEXTAREA'],
    [{ tagName: 'DIV', isContentEditable: true }, 'contenteditable'],
  ];

  it.each(typingCases)('应识别为输入目标：%j', (el) => {
    expect(isTypingTarget(el)).toBe(true);
  });

  const nonTypingCases: Array<[{ tagName?: string; isContentEditable?: boolean }, string]> = [
    [{ tagName: 'DIV' }, 'DIV'],
    [{ tagName: 'BUTTON' }, 'BUTTON'],
  ];

  it.each(nonTypingCases)('不应识别为输入目标：%j', (el) => {
    expect(isTypingTarget(el)).toBe(false);
  });

  it('null 不算', () => {
    expect(isTypingTarget(null)).toBe(false);
  });

  it('undefined 不算', () => {
    expect(isTypingTarget(undefined)).toBe(false);
  });
});
