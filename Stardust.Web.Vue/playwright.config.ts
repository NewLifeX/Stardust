import { mkdirSync } from 'node:fs';
import { dirname } from 'node:path';
import { defineConfig, devices } from './e2e/fixtures';

const BASE_URL = process.env.PLAYWRIGHT_BASE_URL || 'http://localhost:5188';
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
    // 登录态准备：真实 UI 以 admin/admin 登录，写入 storageState 供后续用例复用
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
  // 自动拉起前端开发服务器（Vite 5188，经代理连真实后端 6680）；
  // 若 5188 已在运行则不复用，确保每次启动都用最新依赖（api-core 修复需重优化）。
  // vite.config 中已设 optimizeDeps.force=true，启动时自动重优化依赖。
  webServer: {
    command: 'pnpm dev',
    url: BASE_URL,
    reuseExistingServer: true,
    timeout: 120_000,
  },
});
