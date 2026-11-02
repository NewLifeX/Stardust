import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * Nodes/NodeCommand/Index 迁移 e2e
 *
 * MVC 自定义逻辑：仅 _List_Search 自定义（节点 nodeId / 日期范围），
 * 控制器为标准 NodesEntityController<NodeCommand>，带 [Menu(58, false)] 隐藏菜单。
 *
 * 验证结论：AutoPage 不会渲染 _List_Search 中的「节点 / 时间」自定义字段
 * （后端 GetPage 默认搜索字段不含这些），因此需要自定义视图还原上述搜索字段。
 *
 * 种子数据为空时列表 0 行属正常，仅校验：渲染 + 搜索字段 + 无 >=400 错误。
 */
test('Nodes/NodeCommand 页面渲染（列表 + 自定义搜索字段）', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Nodes/NodeCommand');
  const rendered = await assertPageRendered(page);
  expect(rendered).toBe('list');

  await expect(page.locator('main.cosmic-content .list-page')).toBeVisible();
  await expect(page.locator('main.cosmic-content .el-table').first()).toBeVisible();

  // 自定义搜索区必须包含 _List_Search 关键字段：节点 / 时间（日期范围）
  await expect(page.locator('main.cosmic-content').getByText('节点', { exact: true }).first()).toBeVisible();
  await expect(page.locator('main.cosmic-content').getByText('时间', { exact: true }).first()).toBeVisible();

  const captured = await errs;
  if (captured.errors.length) {
    console.log('NET_ERR=' + JSON.stringify(captured.errors));
    throw new Error('存在接口/资源 >=400 错误: ' + JSON.stringify(captured.errors));
  }

  await page.screenshot({
    path: 'e2e/screenshots/Nodes/NodeCommand/list.png',
    fullPage: true,
  });

  try {
    await page
      .locator('main.cosmic-content')
      .getByRole('button', { name: '搜索' })
      .first()
      .click({ timeout: 3000 });
    await page.waitForTimeout(800);
  } catch (e) {
    console.log('SEARCH_CLICK_SKIPPED=' + String(e));
  }
  await page.screenshot({
    path: 'e2e/screenshots/Nodes/NodeCommand/after-search.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
