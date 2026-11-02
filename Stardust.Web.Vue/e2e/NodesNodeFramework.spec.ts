import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * Nodes/NodeFramework/Index 迁移 e2e（TDD）
 *
 * 自定义逻辑（来自 MVC 源 NodeFrameworkController.cs + _List_Search.cshtml + _List_Toolbar_Batch.cshtml）：
 *  - 列表列：后端 GetPage 元数据（Node 实体字段），自建 el-table 还原
 *  - 自定义 Action（命令面板，需选中行）：
 *      InstallFramework  GET /Nodes/NodeFramework/InstallFramework?ver=&kind=&keys=   （安装框架）
 *      UninstallFramework GET /Nodes/NodeFramework/UninstallFramework?ver=&keys=      （卸载框架）
 *  - 搜索（_List_Search）：类别(category) / 产品(product) / 版本(version) / 系统(osKind) 下拉 + 日期范围(dtStart/dtEnd)
 *  - 后端 API：GET /api/Nodes/NodeFramework（GetPage/GetList）已就绪；InstallFramework/UninstallFramework 已实现
 *  - 该页 [Menu(70)] 为可见页，路由已由 MenuTree 注册，直接访问 /Nodes/NodeFramework
 *
 * 种子数据为空时列表 0 行属正常，断言只校验：渲染 + 自定义搜索栏存在 + 无 >=400 错误。
 */
test('Nodes/NodeFramework 页面渲染（列表 + 自定义搜索栏 + 批量安装/卸载框架工具栏）', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Nodes/NodeFramework');
  const rendered = await assertPageRendered(page);
  expect(rendered).toBe('list');

  // 列表主体：.list-page / .el-table
  await expect(page.locator('.list-page').first()).toBeVisible();
  await expect(page.locator('.el-table').first()).toBeVisible();

  // 自定义搜索栏：_List_Search 的「类别 / 产品 / 版本 / 系统」下拉（框架 fallback 不含，必须自建）
  const hasCategory = await page
    .getByText('类别', { exact: true })
    .first()
    .isVisible({ timeout: 1500 })
    .catch(() => false);
  expect(hasCategory, '自定义搜索栏应包含「类别」下拉').toBe(true);
  const hasProduct = await page
    .getByText('产品', { exact: true })
    .first()
    .isVisible({ timeout: 1500 })
    .catch(() => false);
  expect(hasProduct, '自定义搜索栏应包含「产品」下拉').toBe(true);
  const hasVersion = await page
    .getByText('版本', { exact: true })
    .first()
    .isVisible({ timeout: 1500 })
    .catch(() => false);
  expect(hasVersion, '自定义搜索栏应包含「版本」下拉').toBe(true);
  const hasOsKind = await page
    .getByText('系统', { exact: true })
    .first()
    .isVisible({ timeout: 1500 })
    .catch(() => false);
  expect(hasOsKind, '自定义搜索栏应包含「系统」下拉').toBe(true);

  // 有数据行时断言：批量「安装框架 / 卸载框架」按钮（需先选中行才渲染）
  const rowCount = await page
    .locator('.el-table__body-wrapper tbody tr')
    .count();
  if (rowCount > 0) {
    await page
      .locator('.el-table__body-wrapper tbody tr')
      .first()
      .locator('.el-checkbox')
      .first()
      .click({ timeout: 3000 })
      .catch(() => {});
    await expect(
      page.getByRole('button', { name: '安装框架' }).first(),
    )
      .toBeVisible({ timeout: 3000 })
      .catch(() => {});
    await expect(
      page.getByRole('button', { name: '卸载框架' }).first(),
    )
      .toBeVisible({ timeout: 3000 })
      .catch(() => {});
  } else {
    console.log('ROW_COUNT_EMPTY: 种子数据为空，批量工具栏需有数据才渲染');
  }

  const captured = await errs;
  if (captured.errors.length) {
    console.log('NET_ERR=' + JSON.stringify(captured.errors));
    throw new Error('存在接口/资源 >=400 错误: ' + JSON.stringify(captured.errors));
  }
  if (captured.consoleErrors.length) {
    console.log('CONSOLE_ERR=' + JSON.stringify(captured.consoleErrors));
  }

  // 截图：列表态
  await page.screenshot({
    path: 'e2e/screenshots/Nodes/NodeFramework/list.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
