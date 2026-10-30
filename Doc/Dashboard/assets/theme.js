/**
 * 星尘看板设计稿 · 主题（跟随系统 / 浅色 / 深色）
 * -----------------------------------------------------------------------------
 * 默认 auto：跟随操作系统 prefers-color-scheme。
 * 选择结果写入 localStorage['sd-theme']，并在 <html data-theme> 上落地，
 * 页面与组件只依赖 --sd-* 变量，无需感知主题。
 *
 * ECharts 画在 canvas 上、无法读取 CSS 变量，因此切换主题后重载页面重建图表，
 * 并保留滚动位置（sessionStorage）。StarWeb 实现时应改为响应式重绘图表。
 */
(function () {
  'use strict';

  const KEY = 'sd-theme';
  const MODES = ['auto', 'light', 'dark'];
  const LABELS = { auto: '跟随系统', light: '浅色', dark: '深色' };
  const mq = window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null;

  const ICONS = {
    auto:
      '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">' +
      '<rect x="3" y="4" width="18" height="12" rx="2"/><path d="M8 20h8M12 16v4"/></svg>',
    light:
      '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">' +
      '<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/></svg>',
    dark:
      '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">' +
      '<path d="M21 12.8A8.5 8.5 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z"/></svg>'
  };

  function stored() {
    try {
      const v = localStorage.getItem(KEY);
      return MODES.indexOf(v) >= 0 ? v : 'auto';
    } catch (e) {
      return 'auto';
    }
  }

  function resolved(mode) {
    if (mode === 'auto') return mq && mq.matches ? 'dark' : 'light';
    return mode;
  }

  function apply(mode) {
    const m = mode || stored();
    document.documentElement.dataset.themeMode = m;
    document.documentElement.dataset.theme = resolved(m);
  }

  function render() {
    document.querySelectorAll('[data-theme-switch]').forEach(function (el) {
      const current = stored();
      el.classList.add('theme-switch');
      el.setAttribute('role', 'group');
      el.setAttribute('aria-label', '主题模式');
      el.innerHTML = MODES.map(function (m) {
        return '<button type="button" data-theme-mode="' + m + '"' +
          ' class="' + (current === m ? 'is-active' : '') + '"' +
          ' title="' + LABELS[m] + '" aria-label="' + LABELS[m] + '"' +
          ' aria-pressed="' + (current === m) + '">' + ICONS[m] + '</button>';
      }).join('');
      el.onclick = function (e) {
        const btn = e.target.closest('button[data-theme-mode]');
        if (btn) set(btn.dataset.themeMode);
      };
    });
  }

  function scroller() {
    return document.querySelector('.sd-scroll, .landing, .wb');
  }

  function preserveReload() {
    try {
      const el = scroller();
      if (el) sessionStorage.setItem('sd-theme-scroll', String(el.scrollTop));
    } catch (e) { /* 忽略隐私模式限制 */ }
    location.reload();
  }

  function restoreScroll() {
    try {
      const value = sessionStorage.getItem('sd-theme-scroll');
      if (value === null) return;
      sessionStorage.removeItem('sd-theme-scroll');
      const el = scroller();
      if (el) el.scrollTop = Number(value) || 0;
    } catch (e) { /* 忽略 */ }
  }

  function set(mode) {
    if (MODES.indexOf(mode) < 0) return;
    if (mode === stored()) {
      apply(mode);
      render();
      return;
    }
    try {
      localStorage.setItem(KEY, mode);
    } catch (e) { /* 忽略 */ }
    apply(mode);
    render();
    document.dispatchEvent(new CustomEvent('sd:themechange', {
      detail: { mode: mode, theme: resolved(mode) }
    }));
    preserveReload();
  }

  if (mq) {
    const onSystemChange = function () {
      if (stored() !== 'auto') return;
      apply('auto');
      preserveReload();
    };
    if (mq.addEventListener) mq.addEventListener('change', onSystemChange);
    else if (mq.addListener) mq.addListener(onSystemChange);
  }

  apply();
  document.addEventListener('DOMContentLoaded', function () {
    render();
    requestAnimationFrame(restoreScroll);
  });

  window.SDTheme = {
    modes: MODES.slice(),
    mode: stored,
    resolved: function () { return resolved(stored()); },
    set: set,
    apply: apply
  };
})();
