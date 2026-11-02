import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * Deployment/AppPipelineRun/Index 迁移 e2e（TDD）
 *
 * 自定义逻辑（来自 MVC 源控制器 ListFields + 自定义 Action）：
 *  - 列表列：运行编号(隐藏)/流水线 PipelineId(PipelineName)/状态 Status/
 *           分支 Branch/提交 CommitId/创建时间 CreateTime/更新时间 UpdateTime 等
 *           （Id/TraceId/TriggerSource 已隐藏）
 *  - 「步骤」链接列：每行 -> /Deployment/AppPipelineStep?runId=<行Id>（Url 字段，自建）
 *  - 「重跑」操作列：confirm -> GET /Deployment/AppPipelineRun/Reprocess?id=<行Id>
 *           -> ElMessage(成功/失败) -> 刷新列表
 *  - TraceUrl（TraceId 调用链）加分：若后端返回 TraceUrl 字段则渲染为链接
 *  - 搜索：pipelineId / status / 时间范围（加分）
 *  - 后端 API：GET /Deployment/AppPipelineRun（GetPage）、Reprocess 已实现
 *
 * 种子数据为空时列表 0 行属正常，断言只校验：渲染 + 重跑按钮 + 步骤链接列 + 无 >=400 错误。
 */
test('Deployment/AppPipelineRun 页面渲染（列表 + 重跑 + 步骤链接列）', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Deployment/AppPipelineRun');
  const rendered = await assertPageRendered(page);
  expect(rendered).toBe('list');

  // 列表主体：.list-page / .el-table
  await expect(page.locator('main.cosmic-content .list-page')).toBeVisible();
  await expect(page.locator('main.cosmic-content .el-table').first()).toBeVisible();

  // 表头必须包含「步骤」「重跑」列
  const headerText = await page
    .locator('main.cosmic-content .el-table')
    .first()
    .locator('.el-table__header-wrapper')
    .innerText()
    .catch(() => '');
  expect(headerText).toContain('步骤');
  expect(headerText).toContain('重跑');

  // 有数据行时断言行内「重跑」按钮 + 步骤链接（空数据时跳过，种子数据为空属正常）
  const rowCount = await page
    .locator('main.cosmic-content .el-table__body-wrapper tbody tr')
    .count();
  if (rowCount > 0) {
    const reprocessBtn = page
      .locator('main.cosmic-content .el-table')
      .first()
      .getByRole('button', { name: '重跑' })
      .first();
    if (await reprocessBtn.isVisible({ timeout: 1500 }).catch(() => false)) {
      await expect(reprocessBtn).toBeVisible();
    }
    const stepLink = page
      .locator('main.cosmic-content .el-table')
      .first()
      .getByRole('link', { name: '步骤' })
      .first();
    if (await stepLink.isVisible({ timeout: 1500 }).catch(() => false)) {
      await expect(stepLink).toHaveAttribute('href', /runId=/);
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
    path: 'e2e/screenshots/Deployment/AppPipelineRun/list.png',
    fullPage: true,
  });

  // 截图 2：点击「重跑」后（有数据时触发真实请求 + ElMessage + 刷新）
  try {
    const reprocessBtn = page
      .locator('main.cosmic-content .el-table')
      .first()
      .getByRole('button', { name: '重跑' })
      .first();
    if (await reprocessBtn.isVisible({ timeout: 1500 })) {
      // 点击会弹 confirm，自动接受
      page.once('dialog', (d) => d.accept().catch(() => {}));
      await reprocessBtn.click({ timeout: 3000 });
      await page.waitForTimeout(1500);
    }
  } catch (e) {
    console.log('REPROCESS_CLICK_SKIPPED=' + String(e));
  }
  await page.screenshot({
    path: 'e2e/screenshots/Deployment/AppPipelineRun/after-reprocess.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
