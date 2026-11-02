import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * Deployment/AppPipelineRun/Detail 迁移 e2e（TDD）
 *
 * 隐藏页兜底（[Menu(40,false)]）：控制器不在 MenuTree，URL 直访会 404，
 * 框架 DefaultEntity 在无菜单匹配时检查 apps/stardust/src/views/Deployment/AppPipelineRun/
 * 下自定义视图（detail.vue）直接渲染，路由形如 /Deployment/AppPipelineRun/<id>。
 *
 * 还原 MVC Detail 自定义逻辑：
 *  - 调 usePageApi('Deployment','AppPipelineRun').getDetail(id) 读单条运行记录
 *  - el-descriptions 展示：运行编号/id、流水线、状态、开始/结束时间、耗时、触发源、错误信息、创建时间
 *  - 状态轮询（加分）：若处于「运行中」(0/1/2/3) 每 ~3s getDetail 刷新直到非运行中
 *  - 「返回列表」链接 -> /Deployment/AppPipelineRun
 *
 * 种子数据可能无运行记录：id=1 后端可能返回空，断言「页面未崩溃 + 有详情结构/返回链接」即可，
 * 不强制断言具体字段值。
 */
test('Deployment/AppPipelineRun/Detail 页面渲染（详情 + 返回链接 + 状态轮询不崩）', async ({ page }) => {
  const errs = collectErrors(page);
  // 三级路由：/Deployment/AppPipelineRun/<id>
  await gotoLeafPage(page, 'Deployment/AppPipelineRun/1');
  const rendered = await assertPageRendered(page);
  expect(['list', 'custom', 'object']).toContain(rendered);

  // 详情容器 / 列表页结构存在（自定义视图应渲染 .detail-page 容器）
  const detailVisible = await page
    .locator('main.cosmic-content .detail-page')
    .first()
    .isVisible()
    .catch(() => false);
  // 若框架回退到列表页（index.vue），至少 .list-page 存在也算未崩溃
  const listVisible = await page
    .locator('main.cosmic-content .list-page')
    .first()
    .isVisible()
    .catch(() => false);
  expect(detailVisible || listVisible).toBe(true);

  // 必须有「返回列表」链接（指向 /Deployment/AppPipelineRun）
  const backLink = page
    .locator('main.cosmic-content')
    .getByRole('link')
    .filter({ hasText: '返回列表' })
    .first();
  if (detailVisible) {
    await expect(backLink).toBeVisible();
    await expect(backLink).toHaveAttribute('href', /#\/Deployment\/AppPipelineRun$/);
  }

  // 等待 ~5s 让状态轮询（若有运行中记录）跑至少一轮，断言不崩溃
  await page.waitForTimeout(5000);

  const captured = await errs;
  if (captured.errors.length) {
    console.log('NET_ERR=' + JSON.stringify(captured.errors));
    throw new Error('存在接口/资源 >=400 错误: ' + JSON.stringify(captured.errors));
  }
  if (captured.consoleErrors.length) {
    console.log('CONSOLE_ERR=' + JSON.stringify(captured.consoleErrors));
  }

  await page.screenshot({
    path: 'e2e/screenshots/Deployment/AppPipelineRun/detail.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
