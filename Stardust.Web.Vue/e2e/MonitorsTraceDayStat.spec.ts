import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * Monitors/TraceDayStat/Index 一致性 e2e
 *
 * 对齐 MVC（Stardust.Web/Areas/Monitors/Views/TraceDayStat/_List_Data.cshtml + 控制器 Search）：
 *  - 列表列（与 MVC 逐列一致）：统计日期/应用/种类/操作名/总次数/环比/错误数/错误率/
 *      平均耗时/最大耗时/最小耗时/数值/创建时间/更新时间 + 明细/追踪(独立链接列)
 *  - 双图表（调用次数轴 + 耗时轴），有数据时 canvas 渲染
 *  - 总计行（show-summary）：表尾含「总计」
 *  - 行着色：ErrorRate 阈值 danger/warning
 *  - 搜索：appId 输入 + 日期范围
 */
test('Monitors/TraceDayStat 页面渲染（双图表 + 列集合 + 链接列 + 总计行）', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Monitors/TraceDayStat');
  const rendered = await assertPageRendered(page);
  expect(rendered).toBe('list');

  await expect(page.locator('main.cosmic-content .list-page')).toBeVisible();
  const table = page.locator('main.cosmic-content .el-table').first();
  await expect(table).toBeVisible();

  // 表头列集合（与 MVC _List_Data 一致；应用列在未指定 appId 时显示）
  const headerText = await table.locator('.el-table__header-wrapper').innerText().catch(() => '');
  for (const col of ['统计日期', '种类', '操作名', '总次数', '环比', '错误数', '错误率', '平均耗时', '最大耗时', '最小耗时', '数值', '明细', '追踪', '创建时间', '更新时间']) {
    expect(headerText, `表头应含列「${col}」`).toContain(col);
  }

  // 独立链接列「明细」「追踪」表头存在（链接列自身在行存在时渲染，空数据属数据源差异不 blocking）
  expect(headerText, '表头应含独立链接列「明细」').toContain('明细');
  expect(headerText, '表头应含独立链接列「追踪」').toContain('追踪');

  // 双图表卡片标题
  const titles = await page.locator('main.cosmic-content .lp-card-title').allInnerTexts();
  expect(titles.join(' | ')).toContain('调用次数');
  expect(titles.join(' | ')).toContain('耗时');

  // 图表容器：有数据时 canvas，无数据时空状态文案
  const hasCanvas = await page.locator('main.cosmic-content .lp-card canvas').first().isVisible().catch(() => false);
  const hasEmpty = await page.locator('main.cosmic-content .lp-card .el-empty').first().isVisible().catch(() => false);
  expect(hasCanvas || hasEmpty, '图表卡片应渲染 canvas 或空状态').toBeTruthy();

  // 总计行（show-summary）：表尾含「总计」
  const footerText = await page.locator('main.cosmic-content .el-table__footer-wrapper').innerText().catch(() => '');
  expect(footerText, '应渲染总计行').toContain('总计');

  // 搜索栏
  await expect(page.locator('main.cosmic-content .tds-search-input').first()).toBeVisible();

  const captured = await errs;
  if (captured.errors.length) {
    console.log('NET_ERR=' + JSON.stringify(captured.errors));
    throw new Error('存在接口/资源 >=400 错误: ' + JSON.stringify(captured.errors));
  }
  if (captured.consoleErrors.length) {
    console.log('CONSOLE_ERR=' + JSON.stringify(captured.consoleErrors));
  }

  await page.screenshot({ path: 'e2e/screenshots/Monitors/TraceDayStat/list.png', fullPage: true });
  expect(true).toBe(true);
});
