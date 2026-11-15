import { defineConfig, devices } from '@playwright/test';

// 组件测试（CT）专用配置，与 e2e 的 playwright.config.ts 并存、互不干扰。
// 复用独立端口 5190 的 gallery（自带 Cube 虚拟模块桩与请求层桩），用系统 Chrome 渲染，
// toHaveScreenshot 做像素级回归。
// CT_FRESH=1 时强制换新端口 + 不复用既有 server（绕过本机可能存在的陈旧 vite 占用 5190 导致源码改不动的假象）
// CT_BASE_URL（如 http://127.0.0.1:5195/）优先：多人并行门禁的独立端口分配（lead 广播规则），未设置时行为不变
const fresh = process.env.CT_FRESH === '1';
const baseEnv = process.env.CT_BASE_URL;
const port = baseEnv ? Number(new URL(baseEnv).port) : fresh ? 5193 : 5190;
const baseURL = baseEnv ?? `http://127.0.0.1:${port}`;

export default defineConfig({
  // testDir 从 './src/layouts' 放宽为 './'（CARD-MON-13 首例 apps 侧 CT：apps/stardust/src/views/Console/
  // AlarmsRules.ct.spec.ts）；testMatch 仍锁 *.ct.spec.ts，node_modules 由 Playwright 默认忽略，
  // 对既有 src/layouts 全量用例集零影响。
  testDir: './',
  testMatch: '**/*.ct.spec.ts',
  // 第二波后用例 24→447：vite 按需冷编译在高并发下随机超 30s 默认超时（openStory 的 waitForSelector
  // 轮转超时，失败集每轮漂移、重跑即过=负载漂移而非产品缺陷）。用例级 120s + 动作 60s + workers=4
  // 把首载编译等待纳入预算；真实挂死仍会在预算内失败。
  timeout: 120_000,
  workers: 4,
  outputDir: './test-results-ct',
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'ct-report' }]],
  use: {
    baseURL,
    trace: 'on-first-retry',
    actionTimeout: 60_000,
    navigationTimeout: 60_000,
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
    url: `${baseURL}/`,
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
