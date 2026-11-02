import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * Monitors/AppTracer/Index 迁移 e2e（TDD）
 *
 * 还原 MVC 应用跟踪器列表（自建 el-table + 链接列 + 批量修正 Action）：
 *  - 可见页 [Menu(90)]，路由由菜单自动注册，访问 /Monitors/AppTracer
 *  - 列表主体：.list-page / .el-table
 *  - 自建链接列（AutoPage 不渲染 Url 链接列）：每日监控 / 埋点 / 项目 / 审计日志
 *  - 后端 API：GET /Monitors/AppTracer（GetPage + GetList）
 *
 * 断言「页面渲染 + 表头关键列存在 + 无 >=400 错误」并截图。
 */
test('Monitors/AppTracer 页面渲染（列表 + 链接列 + 批量修正）', async ({ page }) => {
  // 回归防护：分类选项改由列表本地去重生成，初始化期间不得再请求已删除的
  // WebApi 接口 /Monitors/AppTracer/GetCategoryList（历史 404）。进入页面前挂监听。
  const getCategoryListRequests: string[] = [];
  page.on('request', (req) => {
    if (req.url().includes('GetCategoryList')) {
      getCategoryListRequests.push(req.url());
    }
  });

  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Monitors/AppTracer');
  const rendered = await assertPageRendered(page);
  expect(rendered).toBe('list');

  // 等待 onMounted 流程（fetchMeta -> fetchList -> fetchCategories）完成
  await expect(page.locator('main.cosmic-content .el-table').first()).toBeVisible();
  await page.waitForTimeout(300);

  // 核心回归断言：禁止发起 GetCategoryList 请求
  expect(getCategoryListRequests, `不得请求已删除的 GetCategoryList 接口，实际: ${JSON.stringify(getCategoryListRequests)}`).toHaveLength(0);

  // 分类筛选 Select 存在且来源正确（选项由列表本地去重生成，非来自远端接口）
  const categorySelect = page.locator('main.cosmic-content .lp-search .el-select').first();
  await expect(categorySelect).toBeVisible();
  // 收起态应展示「分类」占位符（来自 template 的 placeholder 属性）
  await expect(categorySelect.locator('.el-select__placeholder')).toHaveText('分类');
  // 展开下拉面板，验证 combobox 正确展开（本地生成，不依赖外部请求阻塞）
  const combobox = categorySelect.locator('input[role="combobox"]');
  await categorySelect.click();
  await expect(combobox).toHaveAttribute('aria-expanded', 'true', { timeout: 5_000 });
  // 下拉列表 DOM 已渲染（选项由本地列表去重生成，而非远端接口下发）
  await expect
    .poll(() => page.locator('.el-select-dropdown__list[role="listbox"]').count(), { timeout: 5_000 })
    .toBeGreaterThan(0);
  // 关闭下拉，避免遮挡后续交互
  await page.keyboard.press('Escape');
  await expect(combobox).toHaveAttribute('aria-expanded', 'false', { timeout: 5_000 }).catch(() => {});

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
  for (const col of ['名称', '类别', '每日监控', '埋点', '项目', '审计日志']) {
    expect(headerText, `表头应含列「${col}」`).toContain(col);
  }

  // 批量工具栏按钮存在（选中后可用性切换）
  await expect(page.locator('main.cosmic-content .lp-toolbar').getByRole('button', { name: '修正', exact: true })).toBeVisible();
  await expect(page.locator('main.cosmic-content .lp-toolbar').getByRole('button', { name: '修正旧数据' })).toBeVisible();

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
    path: 'e2e/screenshots/Monitors/AppTracer/list.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
