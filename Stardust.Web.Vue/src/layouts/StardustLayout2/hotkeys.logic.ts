// 键盘分发（纯逻辑，无 DOM 依赖，便于测试）。对照原型 kit-app.jsx 的 onKey。
// 组件层只负责：把 KeyboardEvent 与当前浮层状态喂进来，按返回的动作执行。
//
// Esc 的层级按原型：命令面板、接入向导、生产确认（guard）、其他 dialog/抽屉（交给 Element Plus）、底部控制台。
// guard 由本模块处理（GuardModal 自写遮罩，不用 ElDialog）；接入向导由 ui.store 管理，本模块处理其 Esc；
// 其他 dialog / drawer 由调用方从 useModal 的 modals 派生。

export type Verb = 'P' | 'C' | 'L' | 'N' | 'A';

/** 由本模块负责关闭的 Esc 目标（向导之后的 dialog 与 drawer 交给 Element Plus） */
export type EscapeTarget = 'palette' | 'wizard' | 'guard' | 'dock';

export interface KeyLike {
  key: string;
  metaKey?: boolean;
  ctrlKey?: boolean;
  altKey?: boolean;
}

export interface HotkeyState {
  /** 焦点是否在输入类元素上 */
  typing: boolean;
  /** 命令面板是否打开（ui.store） */
  palette: boolean;
  /** 接入向导 Drawer 是否打开（ui.store）；可选，未接入时视为 false */
  wizard?: boolean;
  /** 生产确认（GuardModal）是否打开（ui.store）；可选，未接入时视为 false */
  guard?: boolean;
  /** 是否有 dialog 类弹窗打开：接入向导（派生自 useModal） */
  dialog: boolean;
  /** 是否有 drawer 类弹窗打开：节点抽屉、应用抽屉（派生自 useModal） */
  drawer: boolean;
  /** 底部控制台是否展开（ui.store） */
  dock: boolean;
}

export type HotkeyAction =
  | { type: 'palette' }
  | { type: 'escape'; target: EscapeTarget }
  | { type: 'dock-toggle' }
  | { type: 'verb'; verb: Verb };

const VERBS = 'PCLNA';

/** 原型：/INPUT|SELECT|TEXTAREA/.test(activeElement.tagName)；这里额外认 contenteditable */
export function isTypingTarget(el: { tagName?: string; isContentEditable?: boolean } | null | undefined): boolean {
  if (!el) return false;
  if (el.isContentEditable) return true;
  return /^(INPUT|SELECT|TEXTAREA)$/i.test(el.tagName ?? '');
}

/**
 * 当前应由本模块处理 Esc 的目标，没有则返回 null。
 * 命令面板最优先，其次是接入向导，再其次是生产确认；其后若有其他 dialog 或抽屉，
 * Esc 归 Element Plus，这里不再关底部控制台，否则一次 Esc 会同时关掉弹窗和控制台。
 */
export function topEscapeTarget(s: HotkeyState): EscapeTarget | null {
  if (s.palette) return 'palette';
  if (s.wizard) return 'wizard';
  if (s.guard) return 'guard';
  if (s.dialog || s.drawer) return null;
  if (s.dock) return 'dock';
  return null;
}

/**
 * 返回需要执行的动作；返回 null 表示该按键不处理（调用方不要 preventDefault）。
 * 规则：
 * 1. Cmd+K / Ctrl+K 任何时候都打开命令面板（含输入框内）；
 * 2. Esc 只处理命令面板、生产确认与底部控制台，输入框内同样生效；
 * 3. 输入中、带修饰键、命令面板、生产确认或 dialog 弹窗打开时，不触发反引号和动词键；
 * 4. 抽屉与底部控制台打开时，动词键仍可用（与原型一致）。
 */
export function resolveHotkey(e: KeyLike, s: HotkeyState): HotkeyAction | null {
  const mod = !!(e.metaKey || e.ctrlKey);

  if (mod && e.key.toLowerCase() === 'k') return { type: 'palette' };

  if (e.key === 'Escape') {
    const target = topEscapeTarget(s);
    return target ? { type: 'escape', target } : null;
  }

  if (s.typing || mod || e.altKey) return null;
  if (s.palette || s.wizard || s.guard || s.dialog) return null;

  if (e.key === '`') return { type: 'dock-toggle' };

  const upper = e.key.toUpperCase();
  if (upper.length === 1 && VERBS.includes(upper)) return { type: 'verb', verb: upper as Verb };

  return null;
}
