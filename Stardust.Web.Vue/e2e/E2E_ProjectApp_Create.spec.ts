import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

// E2E 部署主流程（前端浏览器操作 + 后端数据核对）
// 步骤1-3：新建项目、创建测试应用，并核对页面与后端数据一致
// 项目名/应用名后缀从环境变量读取，保证跨步骤一致
const SUFFIX = process.env.E2E_SUFFIX || 'test';
const PROJECT = `E2E-Project-${SUFFIX}`;
const APP = `E2E-App-${SUFFIX}`;

test('E2E-1 新建项目（浏览器）并核对后端数据', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Platform/GalaxyProject');
  expect(await assertPageRendered(page)).toBe('list');

  // 点「新建」按钮
  await page.getByRole('button', { name: '新建' }).first().click();
  await page.waitForSelector('.object-page .form-content', { timeout: 15_000 });

  // 填名称
  await page.locator('.object-page input').first().fill(PROJECT);
  // 启用开关（默认可能已开，确保开启）
  const enableSwitch = page.locator('.object-page .el-switch').first();
  if (await enableSwitch.count()) {
    const cls = await enableSwitch.getAttribute('class');
    if (cls && !cls.includes('is-checked')) await enableSwitch.click();
  }

  // 保存
  await page.getByRole('button', { name: '保存' }).first().click();

  // 回到列表，等待新建行出现
  await gotoLeafPage(page, 'Platform/GalaxyProject');
  await expect(
    page.locator('.el-table__body-wrapper tbody tr', { hasText: PROJECT }).first(),
  ).toBeVisible({ timeout: 20_000 });

  const captured = await errs;
  if (captured.errors.length) throw new Error('接口>=400: ' + JSON.stringify(captured.errors));
  console.log(`E2E_PROJECT_CREATED=${PROJECT}`);
});

test('E2E-2 创建测试应用（浏览器）并核对后端数据', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Registry/App');
  expect(await assertPageRendered(page)).toBe('list');

  await page.getByRole('button', { name: '新建' }).first().click();
  await page.waitForSelector('.object-page .form-content', { timeout: 15_000 });

  // 名称
  await page.locator('.object-page input').first().fill(APP);
  // 项目下拉选择 E2E-Project-xxx
  const projectSelect = page.locator('.object-page .el-select').first();
  await projectSelect.click();
  await page.getByText(PROJECT, { exact: false }).last().click();

  const enableSwitch = page.locator('.object-page .el-switch').first();
  if (await enableSwitch.count()) {
    const cls = await enableSwitch.getAttribute('class');
    if (cls && !cls.includes('is-checked')) await enableSwitch.click();
  }

  await page.getByRole('button', { name: '保存' }).first().click();

  await gotoLeafPage(page, 'Registry/App');
  await expect(
    page.locator('.el-table__body-wrapper tbody tr', { hasText: APP }).first(),
  ).toBeVisible({ timeout: 20_000 });

  const captured = await errs;
  if (captured.errors.length) throw new Error('接口>=400: ' + JSON.stringify(captured.errors));
  console.log(`E2E_APP_CREATED=${APP}`);
});
