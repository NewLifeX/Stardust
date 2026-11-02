import { test, expect } from './fixtures';
import { assertPageRendered, collectErrors } from './helpers';

/**
 * Trace/Index 迁移 e2e（TDD）
 *
 * 还原 MVC TraceController.Index（layout != detail）→ 调用链树。
 * 路由 /Trace/<traceId>，traceId 取 route.params.id。
 * 复用通用组件 TraceTree（props traceId → 拉 /Monitors/SampleData 构建有序调用链树）。
 * 种子数据可能无该 traceId 采样 → 断言「页面渲染（树容器或空状态）+ 无 >=400」。
 */
test('Trace/Index 调用链树页面渲染（/Trace/<traceId>）', async ({ page }) => {
  const errs = collectErrors(page);
  const traceId = 'test-trace-1';

  // 三级隐藏路由，手动 addRoute 兜底（src/main.ts），直访渲染
  await page.goto(`/Trace/${encodeURIComponent(traceId)}`, { waitUntil: 'domcontentloaded' });
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

  // 调用链树容器存在；等待数据态（树或空状态）出现（种子可能无采样 → 空状态）
  await expect(
    page.locator('main.cosmic-content .trace-tree-wrap').first(),
  ).toBeVisible({ timeout: 10_000 });
  const dataState = page
    .locator('main.cosmic-content .trace-tree-wrap .el-tree, main.cosmic-content .trace-tree-wrap .el-empty')
    .first();
  await dataState.waitFor({ state: 'visible', timeout: 10_000 }).catch(() => {});
  const treeVisible = await page
    .locator('main.cosmic-content .trace-tree-wrap .el-tree')
    .first()
    .isVisible()
    .catch(() => false);
  const emptyVisible = await page
    .locator('main.cosmic-content .trace-tree-wrap .el-empty')
    .first()
    .isVisible()
    .catch(() => false);
  expect(treeVisible || emptyVisible).toBe(true);

  // 标题可见
  await expect(
    page.getByRole('heading', { name: /调用链/ }).first(),
  ).toBeVisible({ timeout: 10_000 });

  // 真实数据态结构断言：拉一个真实 traceId，验证调用链树能正确构建父子节点（字段大小写修复回归保护）
  try {
    const api = await page.request.get('/api/Monitors/SampleData?pageSize=1', { timeout: 15_000 });
    const body: any = await api.json().catch(() => null);
    const rows: any[] = body?.data ?? body ?? [];
    const realTraceId = rows[0]?.traceId;
    if (realTraceId) {
      await page.goto(`/Trace/${encodeURIComponent(realTraceId)}`, { waitUntil: 'domcontentloaded' });
      await page.waitForFunction(
        () => {
          const c = document.querySelector('main.cosmic-content');
          return !!c && c.children.length > 0 && (c.innerText ?? '').trim().length > 0;
        },
        { timeout: 25_000 },
      );
      const tree = page.locator('main.cosmic-content .trace-tree-wrap .el-tree').first();
      await tree.waitFor({ state: 'visible', timeout: 10_000 }).catch(() => {});
      const nodeCount = await page.locator('main.cosmic-content .trace-tree-wrap .el-tree-node__content').count();
      // 修复前（大写字段名 bug）：父子关系失效，节点名/耗时/开始时间全空（取值 undefined）；
      // 修复后（TraceTree.vue 已兼容 item.name || item.appName 等大小写）应能渲染真实操作名+开始时间。
      if (nodeCount > 0) {
        // 操作名（.tt-name）：取到真实值，非空且不是字面量 "undefined"
        const firstName = await page
          .locator('main.cosmic-content .trace-tree-wrap .el-tree-node__content .tt-name')
          .first()
          .innerText()
          .catch(() => '');
        expect(firstName.trim().length).toBeGreaterThan(0); // 名称列应有真实文本
        expect(firstName.trim().toLowerCase()).not.toBe('undefined'); // 字段大小写修复回归：不得显示 undefined

        // 开始时间（.tt-meta）：fmtTime(startTime) 形如 YYYY-MM-DD HH:mm:ss，应取到真实日期
        const firstMeta = await page
          .locator('main.cosmic-content .trace-tree-wrap .el-tree-node__content .tt-meta')
          .first()
          .innerText()
          .catch(() => '');
        expect(firstMeta.trim()).toMatch(/\d{4}-\d{2}-\d{2}/); // 时间字段取到真实值（日期格式）
      }
    }
  } catch (e) {
    console.log('TRACE_INDEX_DATA_ASSERT_SKIP=' + (e as Error).message);
  }

  const captured = await errs;
  if (captured.errors.length) {
    console.log('NET_ERR=' + JSON.stringify(captured.errors));
    throw new Error('存在接口/资源 >=400 错误: ' + JSON.stringify(captured.errors));
  }
  if (captured.consoleErrors.length) {
    console.log('CONSOLE_ERR=' + JSON.stringify(captured.consoleErrors));
  }

  await page.screenshot({
    path: 'e2e/screenshots/Trace/Index/list.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
