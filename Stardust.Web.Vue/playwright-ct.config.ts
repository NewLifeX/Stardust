import { defineConfig, devices } from '@playwright/test';

// 组件测试（CT）专用配置，与 e2e 的 playwright.config.ts 并存、互不干扰。
// 复用独立端口 5190 的 gallery（自带 Cube 虚拟模块桩与请求层桩），用系统 Chrome 渲染，
// toHaveScreenshot 做像素级回归。
// CT_FRESH=1 时强制换新端口 + 不复用既有 server（绕过本机可能存在的陈旧 vite 占用 5190 导致源码改不动的假象）
const fresh = process.env.CT_FRESH === '1';
const port = fresh ? 5193 : 5190;

export default defineConfig({
  testDir: './src/layouts',
  testMatch: '**/*.ct.spec.ts',
  outputDir: './test-results-ct',
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'ct-report' }]],
  use: {
    baseURL: `http://127.0.0.1:${port}`,
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'chromium-ct',
      use: {
        ...devices['Desktop Chrome'],
        // 复用本机已安装的 Chrome（避免下载 Playwright Chromium）
        channel: 'chrome',
        // 沙箱/CI 环境下 Chrome 自身 sandbox 会被拒，需显式关闭；仅当 CT_NO_SANDBOX=1 时启用，本地 Windows 正常运行不受影响
        ...(process.env.CT_NO_SANDBOX
          ? { launchOptions: { args: ['--no-sandbox', '--disable-setuid-sandbox', '--disable-dev-shm-usage'] } }
          : {}),
      },
    },
  ],
  webServer: {
    command: `node node_modules/vite/bin/vite.js --config ct/vite.config.ts --port ${port}`,
    url: `http://127.0.0.1:${port}/`,
    reuseExistingServer: fresh ? false : !process.env.CI,
    timeout: 180_000,
    // 本机若配置了 HTTP 代理，Playwright 探活 localhost 可能拿到 502 并一直等到超时
    env: {
      ...process.env,
      NO_PROXY: '127.0.0.1,localhost',
      no_proxy: '127.0.0.1,localhost',
    },
  },
});
