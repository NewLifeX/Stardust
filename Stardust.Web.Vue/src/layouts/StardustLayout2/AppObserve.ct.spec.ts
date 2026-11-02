import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('应用历史列出操作与成功失败', async ({ page }) => {
  await openStory(page, 'AppObserve/History');
  await expect(page.locator('[data-spec="observe-row"]')).toHaveCount(2);
  await expect(page.locator('[data-spec="observe-row"]').first()).toContainText('登录');
  await expect(page.locator('[data-spec="observe-row"]').first()).toContainText('成功');
  await expect(page.locator('[data-spec="observe-row"]').nth(1)).toContainText('失败');
});

test('唯一实例时画出性能曲线', async ({ page }) => {
  await openStory(page, 'AppObserve/Meter');
  await expect(page.locator('[data-spec="meter-chart"]')).toContainText('10.0.0.1@11');
  await expect(page.locator('[data-spec="meter-chart"] polyline')).toHaveCount(2);
  await expect(page.locator('[data-spec="observe-row"]').first()).toContainText('1.0 MB');
});

test('没有记录时显示空态，不显示表格', async ({ page }) => {
  await openStory(page, 'AppObserve/Empty');
  await expect(page.locator('[data-spec="observe-empty"]')).toContainText('该应用还没有命令记录');
  await expect(page.locator('[data-spec="observe-row"]')).toHaveCount(0);
});

test('请求失败时显示失败，不伪装成空表', async ({ page }) => {
  await openStory(page, 'AppObserve/Failed');
  await expect(page.locator('[data-spec="observe-failed"]')).toContainText('注册加载失败');
});

test('F14 深链：历史 TraceId 是到 /trace/:id 的链接，无值仍是 —', async ({ page }) => {
  await openStory(page, 'AppObserve/History');
  const links = page.locator('[data-spec="observe-trace-link"]');
  await expect(links).toHaveCount(1);
  await expect(links.first()).toHaveAttribute('href', '/trace/abc');
  await expect(links.first()).toHaveText('abc');
});
