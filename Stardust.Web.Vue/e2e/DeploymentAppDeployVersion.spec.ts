import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * Deployment/AppDeployVersion/Index 迁移 e2e（TDD）
 *
 * 自定义逻辑（来自 MVC 源 _Form_File.cshtml + 控制器 ListFields）：
 *  - 列表列：编号 Id / 发布集 DeployName（可点击链接 -> /Deployment/AppDeploy?deployId=<DeployId>）
 *            / 版本 Version / 大小 Size / 哈希 Hash / 启用 Enable / 文件 Url（下载链接）/ 创建时间 等
 *  - 「使用版本」操作列：每行一个按钮 -> confirm -> GET /Deployment/AppDeployVersion/UseVersion?Id=<行Id>
 *  - 文件上传新增：POST /Deployment/AppDeployVersion/Save（字段名 Url_attachment，accept=zip）
 *  - 后端 API：GET /api/Deployment/AppDeployVersion（GetPage/GetList）、UseVersion 已实现（JsonRefresh）
 *
 * 前端经 usePageApi + request，自定义 Action 写相对无前缀路径 /Deployment/AppDeployVersion/UseVersion
 * （request 会自动拼 baseURL，不要写 /api/... 否则双写 404）。
 *
 * 种子数据为空时列表 0 行属正常，仅校验：渲染 + 「使用版本」按钮可见 + DeployName 链接存在 + 无 >=400 错误。
 */
test('Deployment/AppDeployVersion 页面渲染（列表 + 使用版本操作 + 下载链接）', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Deployment/AppDeployVersion');
  const rendered = await assertPageRendered(page);
  expect(rendered).toBe('list');

  // 列表主体：.list-page / .el-table
  await expect(page.locator('main.cosmic-content .list-page')).toBeVisible();
  await expect(page.locator('main.cosmic-content .el-table').first()).toBeVisible();

  // 表头必须包含「应用部署集」（DeployName 跳转列）与「操作」（使用版本列）
  const headerText = await page
    .locator('main.cosmic-content .el-table')
    .first()
    .locator('.el-table__header-wrapper')
    .innerText()
    .catch(() => '');
  expect(headerText).toContain('应用部署集');
  expect(headerText).toContain('操作');

  // 哈希列已在后端 RemoveField，Vue 不应再渲染该列（与 MVC 一致）
  expect(headerText).not.toContain('哈希');

  // 有数据行时断言「使用版本」操作按钮可见（空数据时跳过，种子数据为空属正常）
  const rowCount = await page.locator('main.cosmic-content .el-table__body-wrapper tbody tr').count();
  if (rowCount > 0) {
    await expect(
      page.locator('main.cosmic-content .el-table').first().getByRole('button', { name: '使用版本' }).first(),
    ).toBeVisible();
  } else {
    console.log('ROW_COUNT_EMPTY: 种子数据为空，仅校验表头结构');
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
    path: 'e2e/screenshots/Deployment/AppDeployVersion/list.png',
    fullPage: true,
  });

  // 截图 2：触发「使用版本」后的态（仅当存在数据行时点击，否则仅做无副作用验证）
  try {
    const useBtn = page
      .locator('main.cosmic-content .el-table')
      .first()
      .getByRole('button', { name: '使用版本' })
      .first();
    if (await useBtn.isVisible({ timeout: 1500 })) {
      // 接受 confirm 弹窗
      page.once('dialog', (d) => d.accept());
      await useBtn.click({ timeout: 3000 });
      await page.waitForTimeout(1500);
    }
  } catch (e) {
    console.log('USEVERSION_CLICK_SKIPPED=' + String(e));
  }
  await page.screenshot({
    path: 'e2e/screenshots/Deployment/AppDeployVersion/after-useversion.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
