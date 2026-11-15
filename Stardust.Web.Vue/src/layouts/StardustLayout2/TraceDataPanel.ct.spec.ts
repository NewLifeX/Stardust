import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

// 日期控件「默认今天」是跨天时敏源：把两日期输入框钉在基线生成日（2026-11-10），与基线帧一致且不再随日漂移。
// fill 会留下焦点态（focus 样式入帧致视觉 diff），fill 完 blur 归零。
async function pinDates(page: Page, value = '2026-11-10') {
  await page.fill('[data-spec="trace-data-dt-start"]', value);
  await page.fill('[data-spec="trace-data-dt-end"]', value);
  await page.evaluate(() => (document.activeElement as HTMLElement | null)?.blur());
}

// 手算基准（story 桩 4 行，映射口径与 traceData.logic.test.ts 一致）：
//   GET /api/user   total=15+11=26 errors=0+1=1
//   POST /api/order total=3+4=7    errors=0+2=2
//   ΣTotal=33、ΣErrors=3、ΣTotalCost=3000+378+900+4200=8478、avgCost=round(8478/33)=257
//   本页 4 行 → 双图隐藏（itemId 未填，条件二不满足）；StartTime=0 → 「—」（时区无关）

test('表格数据态：4 行 + 总计行手算 + 双图隐藏 + 开始时间时区无关占位', async ({ page }) => {
  await openStory(page, 'TraceDataPanel/WithData');
  await expect(page.locator('[data-spec="trace-data-panel"]')).toBeVisible();
  await expect(page.locator('[data-spec="trace-data-subtitle"]')).toContainText('IoC容器·每日聚合');
  const rows = page.locator('[data-spec="trace-data-row"]');
  await expect(rows).toHaveCount(4);
  await expect(rows.nth(0)).toContainText('GET /api/user');
  await expect(rows.nth(0)).toContainText('—');
  // 错误数 >0 红色、=0 默认
  await expect(rows.nth(1)).toContainText('1');
  // 总计行手算：label+Σ总耗时（colspan=3 一格）
  const tds = page.locator('[data-spec="trace-data-total"] td');
  await expect(tds.nth(0)).toContainText('本页合计（已加载 4 行）');
  await expect(tds.nth(0)).toContainText('8478 ms');
  await expect(tds.nth(1)).toHaveText('33');
  await expect(tds.nth(2)).toHaveText('3');
  await expect(tds.nth(3)).toHaveText('257 ms');
  // 常规表格态（无 itemId）不显示双图
  await expect(page.locator('[data-spec="trace-data-charts"]')).toHaveCount(0);
  await pinDates(page);
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('空态：提示「暂无追踪数据记录」，无表格行无双图', async ({ page }) => {
  await openStory(page, 'TraceDataPanel/Empty');
  await expect(page.locator('[data-spec="trace-data-empty"]')).toContainText('暂无追踪数据记录');
  await expect(page.locator('[data-spec="trace-data-row"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="trace-data-charts"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="trace-data-failed"]')).toHaveCount(0);
  await pinDates(page);
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('加载中：空态分支内联 loading 旋转，提示「追踪数据加载中…」', async ({ page }) => {
  await openStory(page, 'TraceDataPanel/Loading');
  await expect(page.locator('[data-spec="trace-data-empty"]')).toContainText('追踪数据加载中…');
  await expect(page.locator('[data-spec="trace-data-loading"]')).toBeVisible();
  await expect(page.locator('[data-spec="trace-data-loading"].animate-spin')).toBeVisible();
  await pinDates(page);
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('失败态：失败文案透传桩错误原文 + 重试按钮可见，不伪装成空数据', async ({ page }) => {
  await openStory(page, 'TraceDataPanel/Failed');
  await expect(page.locator('[data-spec="trace-data-failed"]')).toContainText('追踪数据加载失败');
  await expect(page.locator('[data-spec="trace-data-failed"]')).toContainText('示例数据源未');
  await expect(page.locator('[data-spec="trace-data-retry"]')).toBeVisible();
  await expect(page.locator('[data-spec="trace-data-empty"]')).toHaveCount(0);
  await pinDates(page);
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('单行态：行数不足（>1 不满足）双图仍隐藏', async ({ page }) => {
  await openStory(page, 'TraceDataPanel/SingleRow');
  await expect(page.locator('[data-spec="trace-data-row"]')).toHaveCount(1);
  await expect(page.locator('[data-spec="trace-data-charts"]')).toHaveCount(0);
  // 单行也出总计行（label 显示已加载 1 行）
  await expect(page.locator('[data-spec="trace-data-total"]')).toContainText('已加载 1 行');
  await pinDates(page);
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});
