import { mkdirSync } from 'node:fs';
import { dirname } from 'node:path';
import { defineConfig, devices } from './e2e/fixtures';

const BASE_URL = process.env.PLAYWRIGHT_BASE_URL || 'http://localhost:5188';
const BACKEND_URL = process.env.BACKEND_URL || 'http://localhost:6681';
const STORAGE_STATE = 'e2e/.auth/user.json';

// 确保登录态存储目录存在
mkdirSync(dirname(STORAGE_STATE), { recursive: true });

export default defineConfig({
  testDir: './e2e',
  outputDir: './test-results',
  timeout: 60_000,
  expect: { timeout: 15_000 },
  fullyParallel: false,
  workers: 1,
  reporter: [
    ['list'],
    ['html', { open: 'never', outputFolder: 'e2e-report' }],
  ],
  use: {
    baseURL: BASE_URL,
    headless: true,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    // 登录态准备：调后端 /Auth/Login（直连）+ 注入 token，写入 storageState 供后续用例复用
    { name: 'setup', testMatch: /auth\.setup\.ts/ },
    {
      name: 'chromium',
      dependencies: ['setup'],
      use: {
        ...devices['Desktop Chrome'],
        storageState: STORAGE_STATE,
      },
    },
  ],
  // 两个 webServer：
  //   1) 后端（StarWebApi，6681）—— 已在运行则直接复用；未运行由脚本自动拉起
  //   2) 前端（Vite，5188）—— 前端**直连后端**，已无代理，故必须保证后端先就绪
  // 探活 URL 选 /api/Registry/App：未登录返回 401，Playwright 视为「已就绪」。
  webServer: [
    {
      command: 'node scripts/ensure-backend.mjs',
      url: `${BACKEND_URL}/api/Registry/App`,
      reuseExistingServer: true,
      timeout: 120_000,
      stdout: 'pipe',
    },
    {
      command: 'pnpm dev',
      url: BASE_URL,
      reuseExistingServer: true,
      timeout: 120_000,
    },
  ],
});
