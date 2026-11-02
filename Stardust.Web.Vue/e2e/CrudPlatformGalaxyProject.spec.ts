import { test, expect, type Page } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * 星尘前端「创建项目」正规 e2e（TDD 闭环）
 *
 * 业务背景：
 *  - Stardust.Web.Vue 的 Platform/GalaxyProject 列表页（自定义视图 views/Platform/GalaxyProject/index.vue）
 *    顶部有「新建项目」按钮，弹出表单对话框并提交 `POST /api/Platform/GalaxyProject/Add`。
 *  - 后端 GalaxyProjectController 是 MVCEntityController<GalaxyProject>，标准 CRUD。
 *
 * 测试约束：
 *  - 真实对接后端 http://localhost:6681（SQLite），绝不 mock。
 *  - 新建项目名使用 `E2E_` + 时间戳 唯一名，验证后在列表内真实触发删除清理。
 *
 * 运行：
 *  cd X/Stardust/Stardust.Web.Vue
 *  $env:PLAYWRIGHT_BROWSERS_PATH=0
 *  pnpm exec playwright test e2e/CrudPlatformGalaxyProject.spec.ts --project=chromium --reporter=line
 */

/** 在 GalaxyProject 列表页触发新建对话框并填写表单。返回是否成功打开对话框。 */
async function openCreateDialog(page: Page, name: string): Promise<boolean> {
  // 优先点击自定义视图的「新建项目」按钮；兜底 AutoPage 的「新建」
  const createBtn = page
    .getByRole('button', { name: '新建项目' })
    .or(page.getByRole('button', { name: '新建' }))
    .first();
  if (!(await createBtn.isVisible({ timeout: 3000 }).catch(() => false))) return false;
  await createBtn.click();

  // 等待对话框出现（自定义对话框 + 表单）；或 AutoPage 对象表单
  const dialogOrForm = page.locator('.el-dialog').first().or(
    page.locator('main.cosmic-content .object-page .form-content').first(),
  );
  await dialogOrForm.waitFor({ state: 'visible', timeout: 8000 }).catch(() => {});

  // 填写名称（对话框内第一个输入框 / AutoPage 表单第一个输入框）
  const nameInput = page.locator('.el-dialog input').first().or(
    page.locator('main.cosmic-content .object-page form input').first(),
  );
  await nameInput.waitFor({ state: 'visible', timeout: 5000 }).catch(() => {});
  const editable = await nameInput.isEditable({ timeout: 2000 }).catch(() => false);
  if (!editable) return false;
  await nameInput.fill(name);
  return true;
}

/** 提交对话框/表单（自定义「保存」按钮 或 AutoPage「保存」）。 */
async function submitForm(page: Page): Promise<void> {
  const saveBtn = page
    .getByRole('button', { name: /保存|提交|确定/ })
    .first();
  await saveBtn.waitFor({ state: 'visible', timeout: 5000 }).catch(() => {});
  await saveBtn.click();
}

/** 在列表内定位含指定文本的实践按钮并点击（用于删除清理）。 */
async function deleteRowByName(page: Page, name: string): Promise<void> {
  const row = page
    .locator('main.cosmic-content .el-table__body-wrapper tbody tr')
    .filter({ hasText: name })
    .first();
  if (await row.count() === 0) return;
  // 列表行内删除按钮（AutoPage 默认删除在行末操作列）
  const del = row.getByRole('button', { name: /删除|Delete/ }).first();
  if (await del.isVisible({ timeout: 2000 }).catch(() => false)) {
    await del.click();
    // 确认弹窗
    const confirm = page.getByRole('button', { name: /确定|确认|是/ }).first();
    await confirm.waitFor({ state: 'visible', timeout: 3000 }).catch(() => {});
    await confirm.click().catch(() => {});
    await page.waitForTimeout(800);
  }
}

test('Platform/GalaxyProject 新建项目（UI 闭环：新建→列表出现→清理）', async ({ page }) => {
  const errs = collectErrors(page);
  const projectName = `E2E_${Date.now()}`;

  await gotoLeafPage(page, 'Platform/GalaxyProject');
  expect(await assertPageRendered(page)).toBe('list');
  await expect(page.locator('main.cosmic-content .el-table').first()).toBeVisible();

  const rowsBefore = await page
    .locator('main.cosmic-content .el-table__body-wrapper tbody tr')
    .count();
  console.log('ROWS_BEFORE=' + rowsBefore);

  // 打开新建对话框并填写
  const opened = await openCreateDialog(page, projectName);
  expect(opened, '应打开新建项目对话框/表单').toBe(true);

  await submitForm(page);
  await page.waitForTimeout(1200);

  // 表单/对话框关闭后回到列表，等待新行出现（最多 8s）
  let found = 0;
  for (let i = 0; i < 16; i++) {
    found = await page
      .locator('main.cosmic-content .el-table__body-wrapper tbody tr')
      .filter({ hasText: projectName })
      .count();
    if (found > 0) break;
    await page.waitForTimeout(500);
  }
  console.log('NEW_PROJECT_FOUND=' + found);
  expect(found > 0, '新建项目应出现在列表中').toBeTruthy();

  // 断言新建后的行数增加
  const rowsAfter = await page
    .locator('main.cosmic-content .el-table__body-wrapper tbody tr')
    .count();
  console.log('ROWS_AFTER=' + rowsAfter);
  expect(rowsAfter).toBeGreaterThan(rowsBefore);

  // 清理：删除刚才创建的项目，避免污染数据
  await deleteRowByName(page, projectName);
  await page.waitForTimeout(1000);
  const remains = await page
    .locator('main.cosmic-content .el-table__body-wrapper tbody tr')
    .filter({ hasText: projectName })
    .count();
  console.log('AFTER_CLEANUP=' + remains);
  expect(remains === 0, '测试创建的项目应被清理').toBeTruthy();

  // 无 >=400 接口错误
  const captured = await errs;
  if (captured.errors.length) {
    console.log('NET_ERR=' + JSON.stringify(captured.errors));
    throw new Error('存在接口/资源 >=400 错误: ' + JSON.stringify(captured.errors));
  }

  await page.screenshot({
    path: 'e2e/screenshots/Crud/Platform/GalaxyProject/create.png',
    fullPage: true,
  });
});

// 同时保留基础渲染用例（与既有 PlatformGalaxyProject.spec.ts 互补：本用例验证新建闭环）
test('Platform/GalaxyProject 列表渲染 + 新建按钮可见', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Platform/GalaxyProject');
  expect(await assertPageRendered(page)).toBe('list');

  await expect(page.locator('main.cosmic-content .el-table').first()).toBeVisible();

  // 应有「新建项目」入口（自定义视图顶部按钮）
  const createBtn = page.getByRole('button', { name: '新建项目' }).first();
  expect(await createBtn.isVisible({ timeout: 3000 }).catch(() => false)).toBe(true);

  const captured = await errs;
  if (captured.errors.length) {
    console.log('NET_ERR=' + JSON.stringify(captured.errors));
    throw new Error('存在接口/资源 >=400 错误: ' + JSON.stringify(captured.errors));
  }
});
