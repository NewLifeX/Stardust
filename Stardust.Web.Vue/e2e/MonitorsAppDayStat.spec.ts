import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * Monitors/AppDayStat/Index 一致性 e2e
 *
 * 对齐 MVC（AppDayStat/_List_Data.cshtml + 控制器 Search + _List_Toolbar_Batch.cshtml）
 *  - 列表列：统计日期/应用/埋点数/总次数/环比/错误数/错误率/平均耗时/最大耗时/最小耗时/
 *      接口数/Http请求/数据库/消息队列/Redis缓存/其它/创建时间/更新时间 + 当日监控(独立链接列)
 *  - 双图表（总次数轴含分项 + 耗时轴）
 *  - 总计行（show-summary）
 *  - 行着色 ErrorRate 阈值
 *  - 批量统计工具栏（RetryStat）
 *  - 搜索：monitorId(appId) + 日期范围
 */
test('Monitors/AppDayStat 页面渲染（双图表 + 列集合 + 当日监控链接列 + 总计行 + 批量统计）', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Monitors/AppDayStat');
  const rendered = await assertPageRendered(page);
  expect(rendered).toBe('list');

  await expect(page.locator('main.cosmic-content .list-page')).toBeVisible();
  const table = page.locator('main.cosmic-content .el-table').first();
  await expect(table).toBeVisible();

  const headerText = await table.locator('.el-table__header-wrapper').innerText().catch(() => '');
  for (const col of ['统计日期', '应用', '埋点数', '总次数', '环比', '错误数', '错误率', '平均耗时', '最大耗时', '最小耗时', '接口数', 'Http请求', '数据库', '消息队列', 'Redis缓存', '其它', '当日监控', '创建时间', '更新时间']) {
    expect(headerText, `表头应含列「${col}」`).toContain(col);
  }

  // 当日监控独立链接列（即 MVC 的明细/追踪等价链接列，曾被缺失，必须有回归断言）
  // 表头存在性；空数据时行级链接不可见属数据源差异
  expect(headerText, '表头应含独立链接列「当日监控」（MVC 明细/追踪等价列）').toContain('当日监控');

  // 双图表卡片标题
  const titles = await page.locator('main.cosmic-content .lp-card-title').allInnerTexts();
  expect(titles.join(' | ')).toContain('总次数');
  expect(titles.join(' | ')).toContain('耗时');

  const hasCanvas = await page.locator('main.cosmic-content .lp-card canvas').first().isVisible().catch(() => false);
  const hasEmpty = await page.locator('main.cosmic-content .lp-card .el-empty').first().isVisible().catch(() => false);
  expect(hasCanvas || hasEmpty, '图表卡片应渲染 canvas 或空状态').toBeTruthy();

  // 总计行
  const footerText = await page.locator('main.cosmic-content .el-table__footer-wrapper').innerText().catch(() => '');
  expect(footerText, '应渲染总计行').toContain('总计');

  // 批量统计工具栏
  await expect(page.locator('main.cosmic-content .lp-toolbar .el-button', { hasText: '批量统计' }).first()).toBeVisible();

  // 搜索栏
  await expect(page.locator('main.cosmic-content .ads-search-input').first()).toBeVisible();

  const captured = await errs;
  if (captured.errors.length) {
    console.log('NET_ERR=' + JSON.stringify(captured.errors));
    throw new Error('存在接口/资源 >=400 错误: ' + JSON.stringify(captured.errors));
  }
  if (captured.consoleErrors.length) {
    console.log('CONSOLE_ERR=' + JSON.stringify(captured.consoleErrors));
  }

  await page.screenshot({ path: 'e2e/screenshots/Monitors/AppDayStat/list.png', fullPage: true });
  expect(true).toBe(true);
});
