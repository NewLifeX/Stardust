import { test, expect } from './fixtures';

// 本地闭环 E2E 实际数据（硬编码，与本地 SQLite 一致）
const PROJECT = 'E2E-Project-63545b3f';
const APP = 'TestHttp';
const NODE = 'F4683130';

async function gotoList(page: any, path: string) {
  await page.goto(`/${path.replace(/^\/+/, '')}`, { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('.el-table__body-wrapper', { timeout: 30_000 });
}

test('E2E-V1 项目列表显示核对（本地 SQLite 数据）', async ({ page }) => {
  await gotoList(page, 'Platform/GalaxyProject');
  await expect(page.locator('.el-table__body-wrapper tbody tr', { hasText: PROJECT }).first()).toBeVisible({ timeout: 20_000 });
  console.log(`E2E_VERIFY_PROJECT=${PROJECT}`);
});

test('E2E-V2 应用列表显示核对（TestHttp）', async ({ page }) => {
  await gotoList(page, 'Registry/App');
  await expect(page.locator('.el-table__body-wrapper tbody tr', { hasText: APP }).first()).toBeVisible({ timeout: 20_000 });
  console.log(`E2E_VERIFY_APP=${APP}`);
});

test('E2E-V3 部署集列表显示核对（TestHttp）', async ({ page }) => {
  await gotoList(page, 'Deployment/AppDeploy');
  await expect(page.locator('.el-table__body-wrapper tbody tr', { hasText: APP }).first()).toBeVisible({ timeout: 20_000 });
  console.log(`E2E_VERIFY_DEPLOY=${APP}`);
});

test('E2E-V4 节点列表显示核对（F4683130/DGCWIAX10 在线）', async ({ page }) => {
  await gotoList(page, 'Nodes/Node');
  await expect(page.locator('.el-table__body-wrapper tbody tr', { hasText: NODE }).first()).toBeVisible({ timeout: 20_000 });
  console.log(`E2E_VERIFY_NODE=${NODE}`);
});

test('E2E-V5 应用节点显示核对（TestHttp 已部署到节点）', async ({ page }) => {
  await gotoList(page, 'Deployment/AppDeployNode');
  await expect(page.locator('.el-table__body-wrapper tbody tr', { hasText: APP }).first()).toBeVisible({ timeout: 20_000 });
  console.log(`E2E_VERIFY_ADN=${APP}`);
});

test('E2E-V6 部署的应用 HTTP 可访问（19090）', async ({ page }) => {
  const resp = await page.request.get('http://127.0.0.1:19090', { timeout: 10_000 });
  expect(resp.status()).toBe(200);
  const body = await resp.json();
  expect(body.app).toBe('TestHttp');
  console.log(`E2E_VERIFY_RUN=${JSON.stringify(body)}`);
});
