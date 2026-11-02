import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

const ROW = '[data-spec="app-row"]';

test('AppRow 渲染应用名与副标题', async ({ page }) => {
  await openStory(page, 'AppRow/Normal');
  await expect(page.locator(ROW)).toContainText('订单服务');
  await expect(page.locator(ROW)).toContainText('交易 · v1.8.0 · 3 实例');
});

test('构建中显示蓝色「构建中」小标', async ({ page }) => {
  await openStory(page, 'AppRow/Building');
  const tag = page.locator('[data-spec="app-row-build-tag"]');
  await expect(tag).toHaveText('构建中');
  await expect(tag).toHaveCSS('color', /rgb|oklch/);
});

test('构建失败显示红色「失败」小标', async ({ page }) => {
  await openStory(page, 'AppRow/Failed');
  await expect(page.locator('[data-spec="app-row-build-tag"]')).toHaveText('失败');
});

test('有趋势数据时渲染 sparkline SVG', async ({ page }) => {
  await openStory(page, 'AppRow/Trend');
  const svg = page.locator('[data-spec="app-row-trend"]');
  await expect(svg).toHaveAttribute('width', '60');
  await expect(svg).toHaveAttribute('height', '20');
  await expect(svg.locator('path')).toHaveAttribute('d', /^M/);
});

test('无构建小标时不渲染 tag 元素', async ({ page }) => {
  await openStory(page, 'AppRow/Normal');
  await expect(page.locator('[data-spec="app-row-build-tag"]')).toHaveCount(0);
});

test('无趋势数据时不渲染 sparkline', async ({ page }) => {
  await openStory(page, 'AppRow/Empty');
  await expect(page.locator('[data-spec="app-row-trend"]')).toHaveCount(0);
});
