import js from '@eslint/js';
import tseslint from 'typescript-eslint';
import pluginVue from 'eslint-plugin-vue';
import vueParser from 'vue-eslint-parser';

// 内联常见浏览器/Node 全局变量（避免额外依赖 globals 包，规避安装环境限制）
const browserGlobals = {
  window: 'readonly', document: 'readonly', navigator: 'readonly', location: 'readonly',
  localStorage: 'readonly', sessionStorage: 'readonly', console: 'readonly',
  setTimeout: 'readonly', clearTimeout: 'readonly', setInterval: 'readonly', clearInterval: 'readonly',
  requestAnimationFrame: 'readonly', cancelAnimationFrame: 'readonly',
  alert: 'readonly', confirm: 'readonly', prompt: 'readonly',
  HTMLElement: 'readonly', Element: 'readonly', Node: 'readonly', Event: 'readonly',
  CustomEvent: 'readonly', MouseEvent: 'readonly', KeyboardEvent: 'readonly',
  File: 'readonly', Blob: 'readonly', FormData: 'readonly', URL: 'readonly', URLSearchParams: 'readonly', WebSocket: 'readonly', XMLHttpRequest: 'readonly',
  HTMLDivElement: 'readonly', HTMLCanvasElement: 'readonly', HTMLInputElement: 'readonly', SVGSVGElement: 'readonly',
  fetch: 'readonly', history: 'readonly', screen: 'readonly',
} as const;
const nodeGlobals = {
  process: 'readonly', __dirname: 'readonly', __filename: 'readonly',
  module: 'readonly', require: 'readonly', exports: 'readonly', Buffer: 'readonly',
} as const;

export default tseslint.config(
  {
    ignores: [
      'dist',
      'node_modules',
      'Bin',
      'bin',
      '**/*.config.ts',
      'e2e-report',
      'e2e/**/.cache',
      // 调试/临时残留脚本（非项目源码，跳过 lint）
      '**/_*.mjs',
      '**/_*.cjs',
      '**/_*.js',
      '**/_*.ts',
      '**/_*.spec.ts',
      '**/_dump*',
      // 废弃/一次性产物（非源码）
      'stardust_layout.js',
      'main_resolved.js',
      'spec-check.cjs',
    ],
  },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  ...pluginVue.configs['flat/recommended'],
  {
    files: ['**/*.vue'],
    languageOptions: {
      parser: vueParser,
      parserOptions: {
        parser: tseslint.parser,
        ecmaVersion: 'latest',
        sourceType: 'module',
      },
    },
  },
  {
    files: ['**/*.{ts,tsx,vue}'],
    languageOptions: {
      globals: {
        ...browserGlobals,
        ...nodeGlobals,
      },
    },
    rules: {
      '@typescript-eslint/no-explicit-any': 'off',
      '@typescript-eslint/no-unused-vars': ['warn', { argsIgnorePattern: '^_', varsIgnorePattern: '^_' }],
      'vue/multi-word-component-names': 'off',
      'vue/no-v-html': 'off',
    },
  },
);
