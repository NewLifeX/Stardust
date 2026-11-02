import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('TraceList 有数据：左列表两行，首行选中（surface 底+左侧品牌条），右卡渲染名称/成功 Tag/耗时 Tag/TraceId/瀑布条', async ({ page }) => {
  await openStory(page, 'TraceList/WithData');
  await expect(page.locator('[data-spec="trace-row"]')).toHaveCount(2);

  const first = page.locator('[data-spec="trace-row"]').nth(0);
  await expect(first).toHaveClass(/bg-\[var\(--el-fill-color-light\)\]/);
  await expect(first).toHaveClass(/shadow-\[inset_2px_0_0_var\(--el-color-primary\)\]/);

  // 右卡：h3 名称 + 成功 Tag + 耗时 Tag
  await expect(page.locator('[data-spec="trace-detail"] h3')).toContainText('POST /api/order');
  await expect(page.locator('[data-spec="trace-detail"] h3')).toContainText('成功');
  await expect(page.locator('[data-spec="trace-detail"] h3')).toContainText('420ms');
  await expect(page.locator('[data-spec="trace-detail-id"]')).toContainText('TraceId ac15452e01234567');

  // 瀑布：两个片段条，诊断框有内容
  await expect(page.locator('[data-spec="trace-waterfall"] > div')).toHaveCount(2);
  await expect(page.locator('[data-spec="trace-diag"]')).toContainText('最耗时片段');
});

test('TraceList 切换选中：点第二行后右卡变失败 Tag 与失败 TraceId', async ({ page }) => {
  await openStory(page, 'TraceList/WithData');
  await page.locator('[data-spec="trace-row"]').nth(1).click();
  await page.waitForTimeout(500);
  await expect(page.locator('[data-spec="trace-row"]').nth(0)).not.toHaveClass(/shadow-\[inset/);
  await expect(page.locator('[data-spec="trace-row"]').nth(1)).toHaveClass(/shadow-\[inset/);
  await expect(page.locator('[data-spec="trace-detail"] h3')).toContainText('GET /api/pay');
  await expect(page.locator('[data-spec="trace-detail"] h3')).toContainText('失败');
  await expect(page.locator('[data-spec="trace-detail-id"]')).toContainText('ac15452e89abcdef0');
});

test('TraceList 空态：今天没有采样记录时显示空态文案', async ({ page }) => {
  await openStory(page, 'TraceList/Empty');
  await expect(page.locator('[data-spec="trace-empty"]')).toContainText('该应用今天没有采样追踪记录');
});

test('TraceList 失败态：列表请求失败显示错误文案', async ({ page }) => {
  await openStory(page, 'TraceList/Failed');
  await expect(page.locator('[data-spec="trace-error"]')).toContainText('调用链加载失败');
});

test('TraceList 底部按钮：查看相关配置 / 定位实例 只发事件，依次追加进 window.__traceEvents', async ({ page }) => {
  await openStory(page, 'TraceList/EventRecorder');
  await page.waitForSelector('[data-spec="trace-open-config"]', { state: 'visible' });
  await page.locator('[data-spec="trace-open-config"]').click();
  await page.waitForFunction(() => Array.isArray((window as unknown as { __traceEvents?: string[] }).__traceEvents) && (window as unknown as { __traceEvents: string[] }).__traceEvents.includes('openConfig'));
  await page.locator('[data-spec="trace-locate"]').click();
  await page.waitForFunction(() => Array.isArray((window as unknown as { __traceEvents?: string[] }).__traceEvents) && (window as unknown as { __traceEvents: string[] }).__traceEvents.includes('locateInstance'));
  const picked = await page.evaluate(() => (window as unknown as { __traceEvents?: string[] }).__traceEvents ?? []);
  expect(picked).toEqual(['openConfig', 'locateInstance']);
});
