import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * Monitors/TraceMinuteStat/Index 一致性 e2e
 *
 * 对齐 MVC（TraceMinuteStat/_List_Data.cshtml + 控制器 Search）
 *  - 列表列（无 RingRate、无 Type）：统计时间/应用/操作名/总次数/错误数/错误率/
 *      平均耗时/最大耗时/最小耗时/数值/创建时间/更新时间 + 明细/追踪(独立链接列)
 *  - 双图表（调用次数轴 + 耗时轴）
 *  - 总计行（show-summary）
 *  - 行着色 ErrorRate 阈值
 *  - 搜索：appId + 时间范围(datetimerange)
 */
test('Monitors/TraceMinuteStat 页面渲染（双图表 + 列集合 + 链接列 + 总计行）', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Monitors/TraceMinuteStat');
  const rendered = await assertPageRendered(page);
  expect(rendered).toBe('list');

  await expect(page.locator('main.cosmic-content .list-page')).toBeVisible();
  const table = page.locator('main.cosmic-content .el-table').first();
  await expect(table).toBeVisible();

  const headerText = await table.locator('.el-table__header-wrapper').innerText().catch(() => '');
  for (const col of ['统计分钟', '应用', '跟踪项', '操作名', '总次数', '错误数', '错误率', '平均耗时', '最大耗时', '最小耗时', '数值', '明细', '追踪', '创建时间', '更新时间']) {
    expect(headerText, `表头应含列「${col}」`).toContain(col);
  }
  // 不应含 RingRate / Type（与 MVC 一致）
  expect(headerText).not.toContain('环比');

  // 链接列（MVC 明细/追踪，曾被缺失，必须有回归断言）
  expect(headerText, '表头应含独立链接列「明细」').toContain('明细');
  expect(headerText, '表头应含独立链接列「追踪」').toContain('追踪');

  // 跟踪项列（ItemId, DisplayName=跟踪项）：MVC 曾存在、Vue 重构时被误删的列，必须回归断言
  expect(headerText, '表头应含曾被误删的列「跟踪项」').toContain('跟踪项');

  const titles = await page.locator('main.cosmic-content .lp-card-title').allInnerTexts();
  expect(titles.join(' | ')).toContain('调用次数');
  expect(titles.join(' | ')).toContain('耗时');

  const hasCanvas = await page.locator('main.cosmic-content .lp-card canvas').first().isVisible().catch(() => false);
  const hasEmpty = await page.locator('main.cosmic-content .lp-card .el-empty').first().isVisible().catch(() => false);
  expect(hasCanvas || hasEmpty, '图表卡片应渲染 canvas 或空状态').toBeTruthy();

  const footerText = await page.locator('main.cosmic-content .el-table__footer-wrapper').innerText().catch(() => '');
  expect(footerText, '应渲染总计行').toContain('总计');

  const appIdVisible = await page.locator('main.cosmic-content .tms-search-input').first().isVisible().catch(() => false);
  expect(appIdVisible, '搜索栏应渲染 appId 应用输入').toBeTruthy();

  const captured = await errs;
  if (captured.errors.length) {
    console.log('NET_ERR=' + JSON.stringify(captured.errors));
    throw new Error('存在接口/资源 >=400 错误: ' + JSON.stringify(captured.errors));
  }
  if (captured.consoleErrors.length) {
    console.log('CONSOLE_ERR=' + JSON.stringify(captured.consoleErrors));
  }

  await page.screenshot({ path: 'e2e/screenshots/Monitors/TraceMinuteStat/list.png', fullPage: true });
  expect(true).toBe(true);
});
