// Toast 纯逻辑：语气 → 图标名 / 样式类，以及时长与偏移。无 Vue、无 Element Plus 依赖，可直接单测。
// 对照原型 kit-app.jsx 的 toast(msg, tone) 与 kit-shell.jsx 的 Toasts。
// 样式写在 design-system/element-theme-override.css 的 .sd-toast，这里只负责给出类名。
import type { KIconName } from './icons.ts';

export type ToastTone = 'ok' | 'err' | 'warn';

/** 原型 3400ms 后自动移除（kit-app.jsx:75） */
export const TOAST_DURATION = 3400;
/** 原型第一条距底边 20px；Element Plus 的 notify 会在 offset 上再加 16，所以传 4 */
export const TOAST_OFFSET = 4;

/** ok 用对勾，err 用警示，其余（含 warn 与无语气）用 Activity，与原型 Toasts 一致 */
export function toastIcon(tone?: ToastTone): KIconName {
  if (tone === 'ok') return 'Check';
  if (tone === 'err') return 'Alert';
  return 'Activity';
}

/** 基础类 sd-toast；有语气时再加 sd-toast--ok / --err / --warn（左侧 2px 色边） */
export function toastClass(tone?: ToastTone): string {
  return tone ? `sd-toast sd-toast--${tone}` : 'sd-toast';
}
