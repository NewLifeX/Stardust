// Toast 薄封装：调用 ElNotification，位置、时长、图标、左色边都对齐原型。
// 无生命周期；语气到图标与类名的映射在 toast.logic.ts，样式在 design-system/element-theme-override.css 的 .sd-toast。
// 对照原型 kit-app.jsx 的 toast(msg, tone) 与 kit-shell.jsx 的 Toasts。
//
// 已知偏差（均因 ElNotification 的实现，CSS 改不了）：
//  - 多条同时可见时堆叠间距固定 16px，原型是 8px（P-52）。
//  - 组件监听 document 的 keydown，按 Esc 会关掉可见的 toast，原型里 Esc 不影响 toast（P-53）。
import { h } from 'vue';
import { ElNotification } from 'element-plus';
import KIcon from './KIcon.vue';
import { TOAST_DURATION, TOAST_OFFSET, toastClass, toastIcon } from './toast.logic.ts';
import type { ToastTone } from './toast.logic.ts';

/** 弹一条 toast；msg 按纯文本渲染 */
export function toast(msg: string, tone?: ToastTone): void {
  ElNotification({
    // 不传 type：type 会带 Element Plus 自己的状态图标，原型的图标是 KIcon
    message: h('span', { class: 'sd-toast__body' }, [h(KIcon, { name: toastIcon(tone), size: 13 }), h('span', msg)]),
    position: 'bottom-left',
    offset: TOAST_OFFSET,
    duration: TOAST_DURATION,
    showClose: false,
    customClass: toastClass(tone),
  });
}

export function useToast() {
  return { toast };
}
