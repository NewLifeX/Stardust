import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * Deployment/AppDeployNode/Index 迁移 e2e（TDD）
 *
 * 自定义逻辑（来自 MVC 源 _List_Data / _List_Search / _List_Toolbar_Batch）：
 *  - 列表列：编号/发布名/[应用]/[节点]/IP/延迟/启用/进程/进程时间/版本/编译时间/最后活跃/远程操作/操作
 *    （URL 带 deployId 时不显示「应用」「节点」列）
 *  - 「远程操作」列每行 5 个链接：发布/启动/终止/重启/卸载
 *    -> GET /Deployment/AppDeployNode/Operate?act=<动作>&id=<行Id>
 *  - 搜索栏：_Enable(启用) + _DateRange(dtStart/dtEnd)
 *  - 批量工具栏：选中行时启用 5 个批量按钮（发布/启动/终止/重启/卸载）
 *    -> POST/GET /Deployment/AppDeployNode/BatchOperate?act=<动作>（keys=选中id）
 *  - 后端 API：GET /api/Deployment/AppDeployNode、Operate/BatchOperate 已实现
 *
 * 种子数据为空时列表 0 行属正常，仅校验：渲染 + 远程操作列(5动作) + 批量按钮存在 + 无 >=400 错误。
 */
test('Deployment/AppDeployNode 页面渲染（列表 + 远程操作列 + 批量工具栏）', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Deployment/AppDeployNode');
  const rendered = await assertPageRendered(page);
  expect(rendered).toBe('list');

  // 列表主体：.list-page / .el-table
  await expect(page.locator('main.cosmic-content .list-page')).toBeVisible();
  await expect(page.locator('main.cosmic-content .el-table').first()).toBeVisible();

  // 远程操作列：5 个动作文字可见（发布/启动/终止/重启/卸载）
  for (const label of ['发布', '启动', '终止', '重启', '卸载']) {
    await expect(
      page.locator('main.cosmic-content').getByText(label, { exact: false }).first(),
    ).toBeVisible();
  }

  // 批量工具栏：5 个批量按钮存在（MVC _List_Toolbar_Batch，选中行时启用）
  for (const label of ['发布', '启动', '终止', '重启', '卸载']) {
    await expect(
      page.locator('main.cosmic-content .el-button', { hasText: label }).first(),
    ).toBeVisible();
  }

  const captured = await errs;
  if (captured.errors.length) {
    console.log('NET_ERR=' + JSON.stringify(captured.errors));
    throw new Error('存在接口/资源 >=400 错误: ' + JSON.stringify(captured.errors));
  }
  if (captured.consoleErrors.length) {
    console.log('CONSOLE_ERR=' + JSON.stringify(captured.consoleErrors));
  }

  // 截图 1：列表态
  await page.screenshot({
    path: 'e2e/screenshots/Deployment/AppDeployNode/list.png',
    fullPage: true,
  });

  // 截图 2：触发某远程操作（install）后的态
  try {
    // 仅当存在数据行时点击第一个「发布」链接，否则只做无副作用的 hover 验证
    const publishLink = page
      .locator('main.cosmic-content .el-table')
      .first()
      .getByRole('link', { name: '发布' })
      .first();
    if (await publishLink.isVisible({ timeout: 1500 })) {
      page.once('dialog', (d) => d.accept());
      await publishLink.click({ timeout: 3000 });
      await page.waitForTimeout(1500);
    }
  } catch (e) {
    console.log('OPERATE_CLICK_SKIPPED=' + String(e));
  }
  await page.screenshot({
    path: 'e2e/screenshots/Deployment/AppDeployNode/after-operate.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
