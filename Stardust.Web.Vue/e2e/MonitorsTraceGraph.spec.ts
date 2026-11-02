import { test, expect } from './fixtures';
import { assertPageRendered, collectErrors } from './helpers';

/**
 * Monitors/Trace/Graph 迁移 e2e（TDD）
 *
 * 与 Trace/Graph 内容一致（复用同一个通用图解析组件 TraceGraph）。
 * 路由 /Monitors/Trace/Graph?traceId=<id>，直访渲染（隐藏三级路由，main.ts addRoute 兜底）。
 * 断言「页面渲染（图 canvas 容器或空状态）+ 无 >=400」。
 */
test('Monitors/Trace/Graph 调用链关系图页面渲染（/Monitors/Trace/Graph?traceId=）', async ({ page }) => {
  const errs = collectErrors(page);
  const traceId = 'test-trace-1';

  await page.goto(`/Monitors/Trace/Graph?traceId=${encodeURIComponent(traceId)}`, { waitUntil: 'domcontentloaded' });
  await page.waitForFunction(
    () => {
      const c = document.querySelector('main.cosmic-content');
      if (!c) return false;
      return c.children.length > 0 && (c.innerText ?? '').trim().length > 0;
    },
    { timeout: 25_000 },
  );

  const rendered = await assertPageRendered(page);
  expect(['custom']).toContain(rendered);

  // 关系图容器存在；等待数据态（图 canvas 或 空状态）出现
  await expect(
    page.locator('main.cosmic-content .trace-graph-wrap').first(),
  ).toBeVisible({ timeout: 10_000 });
  const dataState = page
    .locator('main.cosmic-content .trace-graph-wrap canvas, main.cosmic-content .trace-graph-wrap .el-empty')
    .first();
  await dataState.waitFor({ state: 'visible', timeout: 10_000 }).catch(() => {});
  const canvasVisible = await page
    .locator('main.cosmic-content .trace-graph-wrap canvas')
    .first()
    .isVisible()
    .catch(() => false);
  const emptyVisible = await page
    .locator('main.cosmic-content .trace-graph-wrap .el-empty')
    .first()
    .isVisible()
    .catch(() => false);
  expect(canvasVisible || emptyVisible).toBe(true);

  await expect(
    page.getByRole('heading', { name: /调用链关系图/ }).first(),
  ).toBeVisible({ timeout: 10_000 });

  const captured = await errs;
  if (captured.errors.length) {
    console.log('NET_ERR=' + JSON.stringify(captured.errors));
    throw new Error('存在接口/资源 >=400 错误: ' + JSON.stringify(captured.errors));
  }
  if (captured.consoleErrors.length) {
    console.log('CONSOLE_ERR=' + JSON.stringify(captured.consoleErrors));
  }

  await page.screenshot({
    path: 'e2e/screenshots/Monitors/Trace/Graph/list.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
