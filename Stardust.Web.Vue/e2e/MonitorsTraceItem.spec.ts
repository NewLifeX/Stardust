import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * Monitors/TraceItem/Index 迁移 e2e（TDD）
 *
 * 还原 MVC 跟踪项（埋点）列表（自建 el-table + 链接列 + 批量修正 Action）：
 *  - 隐藏页 [Menu(65, false)]，靠框架 DefaultEntity catch-all + 自定义视图兜底，访问 /Monitors/TraceItem
 *  - 列表主体：.list-page / .el-table
 *  - 自建链接列（AutoPage 不渲染 Url 链接列）：每日监控 / 审计日志
 *  - 后端 API：GET /Monitors/TraceItem（GetPage + GetList）
 *
 * 断言「页面渲染 + 表头关键列存在 + 无 >=400 错误」并截图。
 */
test('Monitors/TraceItem 页面渲染（列表 + 链接列 + 批量修正）', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Monitors/TraceItem');
  const rendered = await assertPageRendered(page);
  expect(rendered).toBe('list');

  // 列表主体
  await expect(page.locator('main.cosmic-content .list-page')).toBeVisible();
  await expect(page.locator('main.cosmic-content .el-table').first()).toBeVisible();

  // 表头必须包含关键列名（后端字段 + 自建链接列）
  const headerText = await page
    .locator('main.cosmic-content .el-table')
    .first()
    .locator('.el-table__header-wrapper')
    .innerText()
    .catch(() => '');
  for (const col of ['操作名', '种类', '每日监控', '审计日志']) {
    expect(headerText, `表头应含列「${col}」`).toContain(col);
  }

  // 批量工具栏按钮存在（选中后可用性切换）
  await expect(page.locator('main.cosmic-content .lp-toolbar').getByRole('button', { name: '修正', exact: true })).toBeVisible();
  await expect(page.locator('main.cosmic-content .lp-toolbar').getByRole('button', { name: '修正显示名' })).toBeVisible();

  // 勾选首页第一行（验证选择交互，便于后续批量 Action）
  const firstRowCheckbox = page
    .locator('main.cosmic-content .el-table__body-wrapper tbody tr')
    .first()
    .locator('.el-checkbox')
    .first();
  if (await firstRowCheckbox.isVisible().catch(() => false)) {
    await firstRowCheckbox.click().catch(() => {});
  }

  const captured = await errs;
  if (captured.errors.length) {
    console.log('NET_ERR=' + JSON.stringify(captured.errors));
    throw new Error('存在接口/资源 >=400 错误: ' + JSON.stringify(captured.errors));
  }
  if (captured.consoleErrors.length) {
    console.log('CONSOLE_ERR=' + JSON.stringify(captured.consoleErrors));
  }

  await page.screenshot({
    path: 'e2e/screenshots/Monitors/TraceItem/list.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
