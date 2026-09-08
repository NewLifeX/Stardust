/* =============================================================================
 * 星尘 Stardust · Tailwind 主题配置 (布局用 Tailwind，组件用 Element Plus)
 * -----------------------------------------------------------------------------
 * 作用: 把设计令牌 (tokens.css 的 --sd-*) 注入 Tailwind 主题，使布局工具类
 *       (bg-surface / text-primary / p-4 / rounded-base ...) 直接来自令牌。
 * 注意: 颜色一律引用 var(--sd-*)，**不写死 hex**；如需透明度工具类 (/50 等)
 *       请把对应令牌补成 RGB 通道形式（见文末注释）。
 *
 * 引入: 在 tailwind.config 的 content 中扫描 .vue 文件即可。
 * ============================================================================= */

/** @type {import('tailwindcss').Config} */
export default {
  darkMode: ['class', 'dark'],       // 配合根节点 class="dark"
  content: [
    './index.html',
    './src/**/*.{vue,ts,js}',
    './apps/**/*.{vue,ts,js}',
  ],
  theme: {
    extend: {
      /* ---- 颜色: 全部指向 --sd-*（单一来源）---- */
      colors: {
        brand:   'var(--sd-color-brand)',
        'brand-2':'var(--sd-color-brand-2)',
        accent:  'var(--sd-color-accent)',
        online:  'var(--sd-color-online)',
        alert:   'var(--sd-color-alert)',
        offline: 'var(--sd-color-offline)',
        // 表面层级
        'bg-page':     'var(--sd-bg-page)',
        'bg-surface':  'var(--sd-bg-surface)',
        'bg-elevated': 'var(--sd-bg-elevated)',
        'bg-input':    'var(--sd-bg-input)',
        // 文本
        'text-primary':   'var(--sd-text-primary)',
        'text-regular':   'var(--sd-text-regular)',
        'text-secondary': 'var(--sd-text-secondary)',
        'text-placeholder':'var(--sd-text-placeholder)',
        'text-disabled':  'var(--sd-text-disabled)',
        // 边框
        'border-base':    'var(--sd-border)',
        'border-light':   'var(--sd-border-light)',
        'border-lighter': 'var(--sd-border-lighter)',
      },

      /* ---- 字体 ---- */
      fontFamily: {
        sans: ['Inter', 'PingFang SC', 'Microsoft YaHei', 'sans-serif'],
        mono: ['JetBrains Mono', 'ui-monospace', 'monospace'],
      },
      fontSize: {
        xs:  'var(--sd-font-xs)',
        sm:  'var(--sd-font-sm)',
        base:'var(--sd-font-base)',
        md:  'var(--sd-font-md)',
        lg:  'var(--sd-font-lg)',
        xl:  'var(--sd-font-xl)',
        '2xl':'var(--sd-font-2xl)',
        '3xl':'var(--sd-font-3xl)',
      },

      /* ---- 圆角 ---- */
      borderRadius: {
        sm:   'var(--sd-radius-sm)',
        base: 'var(--sd-radius-base)',
        md:   'var(--sd-radius-md)',
        lg:   'var(--sd-radius-lg)',
        round:'var(--sd-radius-round)',
        full: 'var(--sd-radius-full)',
      },

      /* ---- 阴影 ---- */
      boxShadow: {
        1: 'var(--sd-shadow-1)',
        2: 'var(--sd-shadow-2)',
        3: 'var(--sd-shadow-3)',
      },

      /* ---- 尺寸常量 ---- */
      height: { topbar: 'var(--sd-topbar-h)' },
      width:  { sidebar: 'var(--sd-sidebar-w)' },
      maxWidth: { content: 'var(--sd-content-max)' },
    },
  },
  plugins: [],
}

/* -----------------------------------------------------------------------------
 * 如需透明度工具类（如 bg-brand/50），把令牌补成 RGB 通道，例如 tokens.css 中加：
 *   --sd-color-brand-rgb: 102 115 255;
 * 然后本文件改为：
 *   brand: 'rgb(var(--sd-color-brand-rgb) / <alpha-value>)',
 * 其余颜色同理。当前默认 hex 引用已覆盖绝大多数布局场景。
 * --------------------------------------------------------------------------- */
