// 全局热键接线（无生命周期、无 DOM 依赖：window 监听由 index.vue 挂载与卸载）。
// 判断全在 hotkeys.logic.ts 的 resolveHotkey；这里只把浮层状态喂进去，再把动作分发给注入的处理函数。
// 生产确认、底部控制台、动词键的处理函数是可选的：对应功能未接入时不传，该动作按「未处理」返回，不吞按键。
import { resolveHotkey, type HotkeyState, type Verb } from './hotkeys.logic.ts';

/** KeyboardEvent 的最小子集，便于单测里直接构造 */
export interface HotkeyEventLike {
  key: string;
  metaKey?: boolean;
  ctrlKey?: boolean;
  altKey?: boolean;
  /** 输入法组词期间不处理，避免拼音选词时的按键被当成热键 */
  isComposing?: boolean;
  preventDefault: () => void;
}

export interface GlobalHotkeyDeps {
  /** 焦点是否在输入类元素上（通常是 isTypingTarget(document.activeElement)） */
  isTyping: () => boolean;
  paletteOpen: () => boolean;
  /** 接入向导 Drawer 是否打开；未接入前不传，视为 false */
  wizardOpen?: () => boolean;
  /** 生产确认（GuardModal）是否打开；未接入前不传，视为 false */
  guardOpen?: () => boolean;
  /** 派生自 useModal 的 dialog 类弹窗（接入向导）；未接入前不传，视为 false */
  dialogOpen?: () => boolean;
  /** 派生自 useModal 的 drawer 类弹窗；未接入前不传，视为 false */
  drawerOpen?: () => boolean;
  /** 底部控制台是否展开；未接入前不传，视为 false */
  dockOpen?: () => boolean;
  /** ⌘K：打开基础面板（已打开时由 ui.store 用新配置替换，与原型一致） */
  openPalette: () => void;
  closePalette: () => void;
  /** Esc 关接入向导 */
  closeWizard?: () => void;
  /** Esc 关生产确认（等同点取消，不执行回调） */
  cancelGuard?: () => void;
  closeDock?: () => void;
  toggleDock?: () => void;
  /** P/C/L/N/A 动词键 */
  onVerb?: (verb: Verb) => void;
}

export function useGlobalHotkeys(deps: GlobalHotkeyDeps) {
  function state(): HotkeyState {
    return {
      typing: deps.isTyping(),
      palette: deps.paletteOpen(),
      wizard: deps.wizardOpen?.() ?? false,
      guard: deps.guardOpen?.() ?? false,
      dialog: deps.dialogOpen?.() ?? false,
      drawer: deps.drawerOpen?.() ?? false,
      dock: deps.dockOpen?.() ?? false,
    };
  }

  /**
   * 处理一次 keydown。返回 true 表示已处理并已 preventDefault；
   * 返回 false 表示不处理（含缺少对应处理函数），调用方什么都不用做。
   */
  function onKeydown(e: HotkeyEventLike): boolean {
    if (e.isComposing) return false;
    const action = resolveHotkey(e, state());
    if (!action) return false;

    switch (action.type) {
      case 'palette':
        deps.openPalette();
        break;
      case 'escape':
        if (action.target === 'palette') deps.closePalette();
        else if (action.target === 'wizard') {
          if (!deps.closeWizard) return false;
          deps.closeWizard();
        } else if (action.target === 'guard') {
          if (!deps.cancelGuard) return false;
          deps.cancelGuard();
        } else if (deps.closeDock) deps.closeDock();
        else return false;
        break;
      case 'dock-toggle':
        if (!deps.toggleDock) return false;
        deps.toggleDock();
        break;
      case 'verb':
        if (!deps.onVerb) return false;
        deps.onVerb(action.verb);
        break;
    }

    // Ctrl+K 默认会聚焦浏览器地址栏，命中时必须阻止
    e.preventDefault();
    return true;
  }

  return { onKeydown };
}
