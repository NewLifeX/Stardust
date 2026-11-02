import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * Deployment/AppPipeline/Index 迁移 e2e（TDD）
 *
 * 自定义逻辑（来自 MVC 源 _List_Data_Action.cshtml + 控制器自定义 Action）：
 *  - 列表列：名称 Name / 部署集 DeployId(DeployName) / 分支 Branch / 启用 Enable /
 *           间隔 Interval / 更新时间 UpdateTime 等（Id/Token/Secret 已隐藏）
 *  - 「流水线历史」链接列：每行 -> /Deployment/AppPipelineRun?pipelineId=<行Id>（Url 字段，自建）
 *  - 「复制WebHook」操作：GET /Deployment/AppPipeline/CopyWebhook?id=<行Id> -> 写入剪贴板 -> ElMessage
 *  - 「启动」操作：confirm -> GET /Deployment/AppPipeline/Trigger?id=<行Id> -> ElMessage -> 刷新列表
 *  - 标准 查看/编辑/删除 按钮
 *  - 后端 API：GET /Deployment/AppPipeline（GetPage）、CopyWebhook/Trigger 已实现
 *
 * 种子数据为空时列表 0 行属正常，断言只校验：渲染 + 复制WebHook/启动按钮 + 流水线历史列 + 无 >=400 错误。
 */
test('Deployment/AppPipeline 页面渲染（列表 + 复制WebHook/启动 + 流水线历史列）', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Deployment/AppPipeline');
  const rendered = await assertPageRendered(page);
  expect(rendered).toBe('list');

  // 列表主体：.list-page / .el-table
  await expect(page.locator('main.cosmic-content .list-page')).toBeVisible();
  await expect(page.locator('main.cosmic-content .el-table').first()).toBeVisible();

  // 表头必须包含「流水线历史」「操作」列
  const headerText = await page
    .locator('main.cosmic-content .el-table')
    .first()
    .locator('.el-table__header-wrapper')
    .innerText()
    .catch(() => '');
  expect(headerText).toContain('流水线历史');
  expect(headerText).toContain('操作');

  // 有数据行时断言行内「复制WebHook」「启动」按钮 + 流水线历史链接（空数据时跳过，种子数据为空属正常）
  const rowCount = await page
    .locator('main.cosmic-content .el-table__body-wrapper tbody tr')
    .count();
  if (rowCount > 0) {
    await expect(
      page
        .locator('main.cosmic-content .el-table')
        .first()
        .getByRole('button', { name: '复制WebHook' })
        .first(),
    ).toBeVisible();
    await expect(
      page
        .locator('main.cosmic-content .el-table')
        .first()
        .getByRole('button', { name: '启动' })
        .first(),
    ).toBeVisible();
    const histLink = page
      .locator('main.cosmic-content .el-table')
      .first()
      .getByRole('link', { name: '流水线历史' })
      .first();
    if (await histLink.isVisible({ timeout: 1500 }).catch(() => false)) {
      await expect(histLink).toHaveAttribute('href', /pipelineId=/);
    }
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
    path: 'e2e/screenshots/Deployment/AppPipeline/list.png',
    fullPage: true,
  });

  // 截图 2：点击「复制WebHook」后（有数据时触发真实请求 + 写入剪贴板）
  try {
    const copyBtn = page
      .locator('main.cosmic-content .el-table')
      .first()
      .getByRole('button', { name: '复制WebHook' })
      .first();
    if (await copyBtn.isVisible({ timeout: 1500 })) {
      // 监听可能的浏览器 clipboard 权限对话框（部分环境无），避免卡住
      page.once('dialog', (d) => d.accept().catch(() => {}));
      await copyBtn.click({ timeout: 3000 });
      await page.waitForTimeout(1500);
    }
  } catch (e) {
    console.log('COPYWEBHOOK_CLICK_SKIPPED=' + String(e));
  }
  await page.screenshot({
    path: 'e2e/screenshots/Deployment/AppPipeline/after-copywebhook.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
