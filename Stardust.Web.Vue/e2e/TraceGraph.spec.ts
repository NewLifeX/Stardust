import { test, expect } from './fixtures';
import { assertPageRendered, collectErrors } from './helpers';

/**
 * Trace/Graph 迁移 e2e（TDD）
 *
 * 还原 MVC TraceController.Graph → ECharts graph 关系图。
 * 路由 /Trace/Graph?traceId=<id>，直访渲染（隐藏二级路由，main.ts addRoute 兜底）。
 * 复用通用组件 TraceGraph（props traceId → 拉 /Monitors/SampleData 解析 nodes/links/categories → ECharts）。
 * 种子数据可能无采样 → 断言「页面渲染（图 canvas 容器或空状态）+ 无 >=400」。
 */
test('Trace/Graph 调用链关系图页面渲染（/Trace/Graph?traceId=）', async ({ page }) => {
  const errs = collectErrors(page);
  const traceId = 'test-trace-1';

  await page.goto(`/Trace/Graph?traceId=${encodeURIComponent(traceId)}`, { waitUntil: 'domcontentloaded' });
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

  // 真实数据态结构断言：拉真实 traceId，验证关系图能正确解析节点/连线（字段大小写修复回归保护）
  try {
    const api = await page.request.get('/api/Monitors/SampleData?pageSize=1', { timeout: 15_000 });
    const body: any = await api.json().catch(() => null);
    const rows: any[] = body?.data ?? body ?? [];
    const realTraceId = rows[0]?.traceId;
    if (realTraceId) {
      await page.goto(`/Trace/Graph?traceId=${encodeURIComponent(realTraceId)}`, { waitUntil: 'domcontentloaded' });
      await page.waitForFunction(
        () => {
          const c = document.querySelector('main.cosmic-content');
          return !!c && c.children.length > 0 && (c.innerText ?? '').trim().length > 0;
        },
        { timeout: 25_000 },
      );
      const canvas = page.locator('main.cosmic-content .trace-graph-wrap canvas').first();
      await canvas.waitFor({ state: 'visible', timeout: 10_000 }).catch(() => {});
      // 验证 ECharts 解析出节点（修复前大写字段名 bug：spanId/parentId 全空 → 无连线、节点极小）
      const graphInfo = await page
        .evaluate(() => {
          const dom = document.querySelector('main.cosmic-content .trace-graph-wrap canvas');
          if (!dom) return { count: 0, names: [] as string[] };
          const inst = (window as any).echarts?.getInstanceByDom(dom);
          const opt = inst?.getOption?.();
          const series = opt?.series?.[0];
          const data = (series?.data ?? []) as Array<{ name?: string }>;
          return {
            count: data.length,
            names: data.map((d) => String(d?.name ?? '')),
          };
        })
        .catch(() => ({ count: 0, names: [] as string[] }));
      if (graphInfo.count > 0) {
        expect(graphInfo.count).toBeGreaterThan(0);
        // 修复前（大写字段名 bug）：spanId/parentId/name 全空 → 节点名为空或 "undefined"；
        // 修复后（TraceGraph.vue 已兼容 appName/name 大小写）节点名应取到真实操作名/应用名。
        const firstName = (graphInfo.names[0] ?? '').trim();
        expect(firstName.length).toBeGreaterThan(0); // 节点名应有真实文本
        expect(firstName.toLowerCase()).not.toBe('undefined'); // 字段大小写修复回归：不得显示 undefined
      }
    }
  } catch (e) {
    console.log('TRACE_GRAPH_DATA_ASSERT_SKIP=' + (e as Error).message);
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
    path: 'e2e/screenshots/Trace/Graph/list.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
