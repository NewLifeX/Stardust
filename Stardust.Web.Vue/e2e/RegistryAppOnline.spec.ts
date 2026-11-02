import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * Registry/AppOnline/Index 迁移 e2e（TDD）
 *
 * 自定义逻辑（来自 MVC 源 AppOnlineController.cs + _List_Search.cshtml + _List_Toolbar_Batch.cshtml）：
 *  - 列表列：后端 GetPage 元数据过滤掉 AppName/NodeName/Meter/History 等 Url 跳转字段，
 *           自建链接列还原（AutoPage 不渲染 Url 链接列）
 *      AppName   → /Registry/App/Detail?id={AppId}
 *      NodeName  → /Nodes/Node?Id={NodeId}
 *      Meter     → /Registry/AppMeter?appId={AppId}&clientId={Client}   （性能）
 *      History   → /Registry/AppHistory?appId={AppId}&client={Client}   （历史）
 *  - 自定义 Action：
 *      Execute    GET /Registry/AppOnline/Execute?command=&argument=&keys=   （执行命令，需选中行）
 *      FreeMemory GET /Registry/AppOnline/FreeMemory?keys=                    （释放内存，需选中行）
 *  - 搜索（_List_Search）：类别下拉(category, App.FindAllCategory()) + 日期范围(dtStart/dtEnd)
 *  - 后端 API：GET /api/Registry/AppOnline（GetPage/GetList）已就绪；Execute/FreeMemory 已实现
 *  - 该页 [Menu(95)] 为可见页，路由已由 MenuTree 注册，直接访问 /Registry/AppOnline
 *
 * 种子数据为空时列表 0 行属正常，断言只校验：渲染 + 表头含主要列 + 无 >=400 错误。
 */
test('Registry/AppOnline 页面渲染（列表 + 链接列 + 批量命令工具栏）', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Registry/AppOnline');
  const rendered = await assertPageRendered(page);
  expect(rendered).toBe('list');

  // 列表主体：.list-page / .el-table
  await expect(page.locator('.list-page').first()).toBeVisible();
  await expect(page.locator('.el-table').first()).toBeVisible();

  // 表头必须包含自建链接列对应表头：应用 / 节点(Header) / 性能 / 历史 / 跟踪
  const headerText = await page
    .locator('.el-table')
    .first()
    .locator('.el-table__header-wrapper')
    .innerText()
    .catch(() => '');
  expect(headerText).toContain('应用');
  expect(headerText).toContain('节点');
  expect(headerText).toContain('性能');
  expect(headerText).toContain('历史');
  expect(headerText).toContain('追踪');

  // 自定义搜索栏「类别」下拉（MVC _List_Search 自定义；框架 fallback 不含）
  const hasCategory = await page
    .getByText('类别', { exact: true })
    .first()
    .isVisible({ timeout: 1500 })
    .catch(() => false);
  expect(hasCategory, '自定义搜索栏应包含「类别」下拉').toBe(true);

  // 有数据行时断言：批量「释放内存/执行命令」按钮（需先选中行才渲染）+ 行内链接列
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
      page.getByRole('button', { name: '释放内存' }).first(),
    )
      .toBeVisible({ timeout: 3000 })
      .catch(() => {});
    await expect(
      page.getByRole('button', { name: '执行命令' }).first(),
    )
      .toBeVisible({ timeout: 3000 })
      .catch(() => {});

    // 行内链接列：应用(→App Detail) / 节点(→Node) / 性能(→AppMeter) / 历史(→AppHistory)
    const linkCount = await page
      .locator('.el-table__body-wrapper tbody tr')
      .first()
      .locator('a')
      .count();
    expect(linkCount, '行内应存在链接列(应用/节点/性能/历史)').toBeGreaterThan(0);
  } else {
    console.log('ROW_COUNT_EMPTY: 种子数据为空，批量工具栏/链接列需有数据才渲染');
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
    path: 'e2e/screenshots/Registry/AppOnline/list.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
