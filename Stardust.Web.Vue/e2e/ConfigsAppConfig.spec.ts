import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors, getRowCount } from './helpers';

/**
 * Configs/AppConfig/Index 迁移 e2e（TDD）
 *
 * 自定义逻辑：对齐 MVC 表头，管理配置 / 发布 / 历史 / 预览 / 审计日志 各自为独立列。
 *  - 工具栏含「同步应用」(SyncApp) 自定义 Action
 *  - Publish 为行级 AJAX action（Version < NextVersion 时禁用）
 *  - 后端 API：GET /api/Configs/AppConfig、POST /api/Configs/AppConfig/Publish、POST /api/Configs/AppConfig/SyncApp
 *
 * 种子数据为空时列表 0 行属正常，仅校验：渲染 + 独立链接列/按钮存在 + 无 >=400 错误。
 */
test('Configs/AppConfig 页面渲染（列表 + MVC 独立链接列）', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Configs/AppConfig');
  const rendered = await assertPageRendered(page);
  expect(rendered).toBe('list');

  // 标准列表 + 独立链接列必须存在
  await expect(page.locator('main.cosmic-content .list-page')).toBeVisible();
  await expect(page.locator('main.cosmic-content .el-table').first()).toBeVisible();

  // 对齐 MVC 的独立列：管理配置 / 发布 / 历史 / 预览 / 审计日志
  for (const label of ['管理配置', '发布', '历史', '预览', '审计日志']) {
    await expect(page.locator('main.cosmic-content').getByText(label, { exact: false }).first()).toBeVisible();
  }

  // 工具栏：同步应用（SyncApp）
  await expect(page.locator('main.cosmic-content').getByText('同步应用', { exact: false }).first()).toBeVisible();

  // 链接列（还原 MVC：管理配置 / 历史 / 预览 / 审计日志 为 el-link=<a>，项目等带 url 字段亦为链接）。
  // 断言表头含这些列；有数据行时断言首行内存在可点击链接 <a>。
  const linkColumns = ['管理配置', '历史', '预览', '审计日志'];
  for (const label of linkColumns) {
    await expect(
      page.locator('main.cosmic-content .el-table__header').getByText(label, { exact: true }).first(),
    ).toBeVisible();
  }

  const rowCount = await getRowCount(page);
  if (rowCount > 0) {
    const firstRow = page.locator('main.cosmic-content .el-table__body .el-table__row').first();
    // 首行内至少存在 4 个链接（四个链接列渲染的 el-link=<a>）
    const linkCount = await firstRow.locator('a').count();
    expect(linkCount).toBeGreaterThanOrEqual(4);
  } else {
    console.log('APPCONFIG_NO_ROWS=种子数据为空，跳过链接列单元格断言，仅保留表头/无400校验');
  }

  const captured = await errs;
  if (captured.errors.length) {
    console.log('NET_ERR=' + JSON.stringify(captured.errors));
    throw new Error('存在接口/资源 >=400 错误: ' + JSON.stringify(captured.errors));
  }
  if (captured.consoleErrors.length) {
    console.log('CONSOLE_ERR=' + JSON.stringify(captured.consoleErrors));
  }
  expect(true).toBe(true);
});
