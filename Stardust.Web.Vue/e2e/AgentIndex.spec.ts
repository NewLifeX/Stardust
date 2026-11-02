import { test, expect } from './fixtures';
import { gotoLeafPage, collectErrors } from './helpers';

/**
 * Agent/Index 迁移 e2e（TDD）
 *
 * 后端 AgentController.cs（根 Area，Stardust.Web/Controllers）+ Agent/Index.cshtml
 * 是纯展示页：StarAgent 介绍、下载链接、安装命令、本机探测状态。
 * [AllowAnonymous]，但本 e2e 统一走登录态访问 /Agent。
 * 前端该页为静态展示页（catch-all 兜底渲染自定义视图），
 * 路由 /Agent；核心是不 404、真实渲染、无 >=400 错误、标题/卡片可见。
 */
test('Agent/Index 页面渲染（StarAgent 下载安装静态页）', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Agent');

  // 主区域必须渲染内容（自定义视图：标题 + 卡片）
  await expect(page.locator('main.cosmic-content').first()).toBeVisible();
  // 标题「星尘代理 StarAgent」
  await expect(
    page.getByRole('heading', { name: /星尘代理|StarAgent/ }).first(),
  ).toBeVisible({ timeout: 10_000 });
  // 至少有下载/安装说明卡片可见
  await expect(page.locator('.el-card').first()).toBeVisible({ timeout: 10_000 });

  const captured = await errs;
  if (captured.errors.length) {
    console.log('NET_ERR=' + JSON.stringify(captured.errors));
    throw new Error('存在接口/资源 >=400 错误: ' + JSON.stringify(captured.errors));
  }
  if (captured.consoleErrors.length) {
    console.log('CONSOLE_ERR=' + JSON.stringify(captured.consoleErrors));
  }

  // 截图：列表态
  await page.screenshot({
    path: 'e2e/screenshots/Agent/Index/list.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
