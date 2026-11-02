// toast.logic.ts 的单测（Vitest）
import { describe, it, expect } from 'vitest';
import { TOAST_DURATION, TOAST_OFFSET, toastClass, toastIcon } from './toast.logic';
import type { ToastTone } from './toast.logic';

describe('toast 时长与偏移', () => {
  // 原型 3400ms；第一条距底边 20px = offset 4 + Element Plus 内置的 16
  it('时长 3400ms', () => {
    expect(TOAST_DURATION).toBe(3400);
  });

  it('偏移 4（加 16 后落在 20px）', () => {
    expect(TOAST_OFFSET).toBe(4);
  });
});

describe('toastIcon · 图标选择', () => {
  it('ok 用 Check', () => {
    expect(toastIcon('ok')).toBe('Check');
  });

  it('err 用 Alert', () => {
    expect(toastIcon('err')).toBe('Alert');
  });

  it('warn 用 Activity（原型 warn 没有专属图标）', () => {
    expect(toastIcon('warn')).toBe('Activity');
  });

  it('无语气用 Activity', () => {
    expect(toastIcon()).toBe('Activity');
  });

  it('undefined 用 Activity', () => {
    expect(toastIcon(undefined)).toBe('Activity');
  });

  it('未知语气退到 Activity', () => {
    expect(toastIcon('x' as ToastTone)).toBe('Activity');
  });
});

describe('toastClass · 样式类', () => {
  it('ok 类', () => {
    expect(toastClass('ok')).toBe('sd-toast sd-toast--ok');
  });

  it('err 类', () => {
    expect(toastClass('err')).toBe('sd-toast sd-toast--err');
  });

  it('warn 类', () => {
    expect(toastClass('warn')).toBe('sd-toast sd-toast--warn');
  });

  it('无语气只有基础类', () => {
    expect(toastClass()).toBe('sd-toast');
  });

  it('undefined 只有基础类', () => {
    expect(toastClass(undefined)).toBe('sd-toast');
  });

  it('四种情形的类名互不相同（左色边靠它区分）', () => {
    const classes = new Set<string>([toastClass('ok'), toastClass('err'), toastClass('warn'), toastClass()]);
    expect(classes.size).toBe(4);
  });
});
