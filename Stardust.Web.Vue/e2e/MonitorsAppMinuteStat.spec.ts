import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * Monitors/AppMinuteStat/Index 一致性 e2e
 *
 * 对齐 MVC（AppMinuteStat/_List_Data.cshtml + 控制器 Search）
 *  - 列表列：编号/统计时间/应用/总次数/错误数/错误率/平均耗时/最大耗时/最小耗时/创建时间/更新时间
 *  - 双图表（调用次数轴 + 耗时轴）
 *  - 总计行（show-summary）—— MVC 有、Vue 此前缺失，本次补齐
 *  - 行着色 ErrorRate 阈值
 *  - 搜索：appId + 时间范围(datetimerange)
 */
test('Monitors/AppMinuteStat 页面渲染（双图表 + 列集合 + 总计行）', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Monitors/AppMinuteStat');
  const rendered = await assertPageRendered(page);
  expect(rendered).toBe('list');

  await expect(page.locator('main.cosmic-content .list-page')).toBeVisible();
  const table = page.locator('main.cosmic-content .el-table').first();
  await expect(table).toBeVisible();

  const headerText = await table.locator('.el-table__header-wrapper').innerText().catch(() => '');
  for (const col of ['编号', '统计分钟', '应用', '总次数', '错误数', '错误率', '平均耗时', '最大耗时', '最小耗时', '创建时间', '更新时间']) {
    expect(headerText, `表头应含列「${col}」`).toContain(col);
  }

  // 链接列（MVC 明细/追踪等价）：本页以「总次数」单元格内链接实现（无独立链接列名），
  // 故不硬断言不存在的列名，仅保留下方总计行断言作为核心回归点。

  // 双图表卡片标题
  const titles = await page.locator('main.cosmic-content .lp-card-title').allInnerTexts();
  expect(titles.join(' | ')).toContain('调用次数');
  expect(titles.join(' | ')).toContain('耗时');

  const hasCanvas = await page.locator('main.cosmic-content .lp-card canvas').first().isVisible().catch(() => false);
  const hasEmpty = await page.locator('main.cosmic-content .lp-card .el-empty').first().isVisible().catch(() => false);
  expect(hasCanvas || hasEmpty, '图表卡片应渲染 canvas 或空状态').toBeTruthy();

  // 总计行（本次修复核心）
  const footerText = await page.locator('main.cosmic-content .el-table__footer-wrapper').innerText().catch(() => '');
  expect(footerText, '应渲染总计行（MVC 有、Vue 此前缺失）').toContain('总计');

  const appIdVisible = await page.locator('main.cosmic-content .ams-search-input').first().isVisible().catch(() => false);
  expect(appIdVisible, '搜索栏应渲染 appId 应用输入').toBeTruthy();

  const captured = await errs;
  if (captured.errors.length) {
    console.log('NET_ERR=' + JSON.stringify(captured.errors));
    throw new Error('存在接口/资源 >=400 错误: ' + JSON.stringify(captured.errors));
  }
  if (captured.consoleErrors.length) {
    console.log('CONSOLE_ERR=' + JSON.stringify(captured.consoleErrors));
  }

  await page.screenshot({ path: 'e2e/screenshots/Monitors/AppMinuteStat/list.png', fullPage: true });
  expect(true).toBe(true);
});
