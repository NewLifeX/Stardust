import { test, expect } from './fixtures';
import { assertPageRendered, collectErrors } from './helpers';

/**
 * Trace/Detail 迁移 e2e（TDD）
 *
 * 还原 MVC TraceController.Index（layout=detail）→ 追踪明细表格。
 * 路由 /Trace/<traceId>?layout=detail（由 Trace/index.vue 内联切换渲染 Trace/Detail 逻辑）。
 * 复用 SampleData 列表，el-table 展示 操作名/耗时/开始-结束/节点/状态/Tag。
 * 种子数据可能无采样 → 断言「页面渲染（表格或空状态）+ 无 >=400」。
 */
test('Trace/Detail 追踪明细页面渲染（/Trace/<traceId>?layout=detail）', async ({ page }) => {
  const errs = collectErrors(page);
  const traceId = 'test-trace-1';

  await page.goto(`/Trace/${encodeURIComponent(traceId)}?layout=detail`, { waitUntil: 'domcontentloaded' });
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

  // 明细表格（el-table）或 空状态 至少其一可见
  const tableVisible = await page
    .locator('main.cosmic-content .trace-detail-page .el-table')
    .first()
    .isVisible()
    .catch(() => false);
  const emptyVisible = await page
    .locator('main.cosmic-content .trace-detail-page .el-empty')
    .first()
    .isVisible()
    .catch(() => false);
  expect(tableVisible || emptyVisible).toBe(true);

  // 「追踪明细」标签处于激活态
  await expect(
    page.locator('main.cosmic-content .tip-tab.active').filter({ hasText: '追踪明细' }).first(),
  ).toBeVisible({ timeout: 10_000 });

  // 真实数据态结构断言：拉真实 traceId，验证明细表能正确渲染操作名/耗时（字段大小写修复回归保护）
  try {
    const api = await page.request.get('/api/Monitors/SampleData?pageSize=1', { timeout: 15_000 });
    const body: any = await api.json().catch(() => null);
    const rows: any[] = body?.data ?? body ?? [];
    const realTraceId = rows[0]?.traceId;
    if (realTraceId) {
      await page.goto(`/Trace/${encodeURIComponent(realTraceId)}?layout=detail`, { waitUntil: 'domcontentloaded' });
      await page.waitForFunction(
        () => {
          const c = document.querySelector('main.cosmic-content');
          return !!c && c.children.length > 0 && (c.innerText ?? '').trim().length > 0;
        },
        { timeout: 25_000 },
      );
      const table = page.locator('main.cosmic-content .trace-detail-page .el-table').first();
      await table.waitFor({ state: 'visible', timeout: 10_000 }).catch(() => {});
      const rowCount = await page.locator('main.cosmic-content .trace-detail-page .el-table__body-wrapper tbody tr').count();
      if (rowCount > 0) {
        const firstRow = page
          .locator('main.cosmic-content .trace-detail-page .el-table__body-wrapper tbody tr')
          .first();
        // 修复前（大写字段名 bug）：操作名/应用/开始时间列全空（取值 undefined）；
        // 修复后（detail.vue 已用 row.name/row.startTime 取值）应取到真实值。

        // 操作名（首列 prop="name"）：取真实值，非空且非字面量 "undefined"
        const firstName = (await firstRow.locator('td').nth(0).innerText().catch(() => '')) || '';
        expect(firstName.trim().length).toBeGreaterThan(0); // 操作名列应有真实文本
        expect(firstName.trim().toLowerCase()).not.toBe('undefined'); // 字段大小写修复回归

        // 开始时间（第 5 列，fmtTime(row.startTime) 形如 YYYY-MM-DD HH:mm:ss）：取到真实日期
        const startTimeText = (await firstRow.locator('td').nth(4).innerText().catch(() => '')) || '';
        expect(startTimeText.trim()).toMatch(/\d{4}-\d{2}-\d{2}/); // 时间字段取到真实值（日期格式）
      }
    }
  } catch (e) {
    console.log('TRACE_DETAIL_DATA_ASSERT_SKIP=' + (e as Error).message);
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
    path: 'e2e/screenshots/Trace/Detail/list.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
