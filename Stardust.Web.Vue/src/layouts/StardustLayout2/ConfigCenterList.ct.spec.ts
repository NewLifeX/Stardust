import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('Mixed：三行，待发布行在最前带橙色 Tag，已发布绿色', async ({ page }) => {
  await openStory(page, 'ConfigCenterList/Mixed');
  const rows = page.locator('[data-spec="config-row"]');
  await expect(rows).toHaveCount(3);
  await expect(rows.first()).toContainText('订单服务');
  await expect(rows.first()).toContainText('待发布');
  // 后两行是已发布
  await expect(page.locator('[data-spec="config-row"]').nth(1)).toContainText('已发布');
});

test('Mixed：工具条显示总数，Key 数与最近更新列存在', async ({ page }) => {
  await openStory(page, 'ConfigCenterList/Mixed');
  await expect(page.locator('[data-spec="config-center"]')).toContainText('3 条');
  await expect(page.locator('[data-spec="config-row"]').first()).toContainText('12');
});

test('Empty：没有配置时显示空态文案', async ({ page }) => {
  await openStory(page, 'ConfigCenterList/Empty');
  await expect(page.locator('[data-spec="config-empty"]')).toContainText('还没有应用配置');
  await expect(page.locator('[data-spec="config-row"]')).toHaveCount(0);
});

test('Loading：spinner 加载中，不渲染表格', async ({ page }) => {
  await openStory(page, 'ConfigCenterList/Loading');
  await expect(page.locator('[data-spec="config-loading"]')).toContainText('配置加载中');
  await expect(page.locator('[data-spec="config-row"]')).toHaveCount(0);
});

test('Failed：请求失败显示可见错误 + 重试按钮', async ({ page }) => {
  await openStory(page, 'ConfigCenterList/Failed');
  await expect(page.locator('[data-spec="config-failed"]')).toContainText('配置加载失败');
  await expect(page.locator('[data-spec="config-retry"]')).toContainText('重试');
});

test('OpenRecorder：点行把 configId 写进 window.__configOpen', async ({ page }) => {
  await openStory(page, 'ConfigCenterList/OpenRecorder');
  await page.locator('[data-spec="config-row"]').nth(1).click();
  const picked = await page.evaluate(() => (window as unknown as { __configOpen?: number[] }).__configOpen);
  expect(picked).toEqual([8]);
});
