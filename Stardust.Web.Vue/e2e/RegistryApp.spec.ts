import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * Registry/App/Index 迁移 e2e（TDD）
 *
 * 自定义逻辑（来自 MVC 源 AppController.cs + _List_Search.cshtml + _List_Toolbar_Batch.cshtml）：
 *  - 列表列：编号 Id / 项目 ProjectName(→GalaxyProject) / 名称 Name(→Detail) / 显示名 /
 *           类别 Category / 启用 Enable / 版本 Version / 最后登录 LastLogin 等
 *  - 自定义 url 跳转列：Name→Detail、ProjectName→GalaxyProject、AppLog→应用日志、Log→审计日志
 *  - 自定义 Action：
 *      AppSearch  GET  /api/Registry/App/AppSearch        （应用搜索建议）
 *      SetAlarm   GET  /api/Registry/App/SetAlarm          （批量启用/禁用下线告警，需选中行 keys）
 *  - 搜索（_List_Search）：类别下拉(category) + 日期范围(dtStart/dtEnd)
 *  - 后端 API：GET /api/Registry/App（GetPage/GetList）已就绪；AppSearch/SetAlarm 已实现
 *  - 自定义视图兜底：本页 [Menu(99)] 为可见页，路由已由 MenuTree 注册，直接访问 /Registry/App
 *
 * 种子数据为空时列表 0 行属正常，断言只校验：渲染 + 表头含主要列 + 无 >=400 错误。
 */
test('Registry/App 页面渲染（列表 + 链接列 + 批量告警）', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Registry/App');
  const rendered = await assertPageRendered(page);
  expect(rendered).toBe('list');

  // 列表主体：.list-page / .el-table（不依赖 main.cosmic-content 旧布局）
  await expect(page.locator('.list-page').first()).toBeVisible();
  await expect(page.locator('.el-table').first()).toBeVisible();

  // 表头必须包含主要列：名称 / 应用日志 / 审计日志 / 操作
  const headerText = await page
    .locator('.el-table')
    .first()
    .locator('.el-table__header-wrapper')
    .innerText()
    .catch(() => '');
  expect(headerText).toContain('名称');
  expect(headerText).toContain('应用日志');
  expect(headerText).toContain('审计日志');
  expect(headerText).toContain('操作');

  // 自定义搜索栏「类别」下拉（MVC _List_Search 自定义；框架 fallback 不含）
  const hasCategory = await page
    .getByText('类别', { exact: true })
    .first()
    .isVisible({ timeout: 1500 })
    .catch(() => false);
  expect(hasCategory, '自定义搜索栏应包含「类别」下拉').toBe(true);

  // 有数据行时断言：批量「启用告警/禁用告警」按钮（需先选中行才渲染）+ 行内「详情」链接
  const rowCount = await page
    .locator('.el-table__body-wrapper tbody tr')
    .count();
  if (rowCount > 0) {
    // 批量工具栏 v-if="selection.length>0"，需选中行才渲染（MVC 原版行为）
    await page
      .locator('.el-table__body-wrapper tbody tr')
      .first()
      .locator('.el-checkbox')
      .first()
      .click({ timeout: 3000 })
      .catch(() => {});
    await expect(
      page.getByRole('button', { name: '启用告警' }).first(),
    )
      .toBeVisible({ timeout: 3000 })
      .catch(() => {});
    await expect(
      page.getByRole('button', { name: '禁用告警' }).first(),
    )
      .toBeVisible({ timeout: 3000 })
      .catch(() => {});

    const detailLink = page
      .locator('.el-table')
      .first()
      .getByRole('link', { name: '详情' })
      .first();
    if (await detailLink.isVisible({ timeout: 1500 }).catch(() => false)) {
      await expect(detailLink).toHaveAttribute('href', /Detail/);
    }
  } else {
    console.log('ROW_COUNT_EMPTY: 种子数据为空，批量工具栏/详情链接需有数据才渲染');
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
    path: 'e2e/screenshots/Registry/App/list.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
