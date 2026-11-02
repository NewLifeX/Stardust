import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * Monitors/Trace/Index 迁移 e2e（TDD）
 *
 * 还原 MVC 全链路追踪查询首页（独立 Index.cshtml）：
 *  - 标题「全链路追踪」
 *  - 搜索输入框（placeholder「搜索TraceId（支持逗号分隔的多个TraceId）」）
 *  - 查询按钮（el-button）
 *  - 点击查询/回车 → router.push 跳转详情页路由（/Trace/<traceId>，详情页待迁移）
 *
 * 纯前端交互页，不调后端列表接口；核心：搜索框输入 + 跳转。
 * 种子数据为空不影响本页（无列表）。断言只校验：渲染 + 跳转发生（URL 变化含 traceId）。
 */
test('Monitors/Trace 查询首页渲染 + 输入 TraceId 查询跳转', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Monitors/Trace');
  const rendered = await assertPageRendered(page);
  // 自定义视图（非标准 CRUD 列表）
  expect(rendered, 'Monitors/Trace 应为自定义视图').toBe('custom');

  // 标题「全链路追踪」
  await expect(page.getByRole('heading', { name: '全链路追踪' }).first()).toBeVisible();

  // 搜索输入框（placeholder 匹配）
  const input = page.locator('main.cosmic-content input').first();
  await expect(input).toBeVisible();
  await expect(input).toHaveAttribute(
    'placeholder',
    '搜索TraceId（支持逗号分隔的多个TraceId）',
  );

  // 查询按钮存在
  const queryBtn = page.getByRole('button', { name: '查询' }).first();
  await expect(queryBtn).toBeVisible();

  // 输入一个测试 TraceId
  const traceId = 'abc123';
  await input.fill(traceId);

  // 记录跳转前 URL
  const beforeUrl = page.url();

  // 点击查询 → 跳转详情路由
  await queryBtn.click();

  // 断言 URL 发生跳转（包含 traceId 或进入详情路由）
  await page.waitForFunction(
    ({ tid, prev }) => {
      const u = location.href;
      return u !== prev && u.toLowerCase().includes(tid.toLowerCase());
    },
    { tid: traceId, prev: beforeUrl },
    { timeout: 10_000 },
  );
  const afterUrl = page.url();
  expect(afterUrl, '查询后应跳转到含 traceId 的详情路由').toContain(traceId);

  const captured = await errs;
  if (captured.errors.length) {
    console.log('NET_ERR=' + JSON.stringify(captured.errors));
    throw new Error('存在接口/资源 >=400 错误: ' + JSON.stringify(captured.errors));
  }
  if (captured.consoleErrors.length) {
    console.log('CONSOLE_ERR=' + JSON.stringify(captured.consoleErrors));
  }

  // 截图：搜索页态
  await page.goto(`/Monitors/Trace`, { waitUntil: 'domcontentloaded' });
  await gotoLeafPage(page, 'Monitors/Trace');
  await page.screenshot({
    path: 'e2e/screenshots/Monitors/Trace/list.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
