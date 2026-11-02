import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('Data：三行服务，在线满条/停用空条，负载均衡列固定待接入', async ({ page }) => {
  await openStory(page, 'RegistryTab/Data');
  await expect(page.locator('[data-spec="registry-row"]')).toHaveCount(3);
  // 在线那行显示「在线」，停用那行显示「停用」
  await expect(page.locator('[data-spec="registry-row"]').first()).toContainText('订单服务');
  await expect(page.locator('[data-spec="registry-row"]').nth(2)).toContainText('停用');
  // 负载均衡列每行都是「待接入」
  const pending = page.locator('[data-spec="registry-row"] >> text=待接入');
  await expect(pending).toHaveCount(3);
});

test('Empty：无服务显示空态，不渲染行', async ({ page }) => {
  await openStory(page, 'RegistryTab/Empty');
  await expect(page.locator('[data-spec="registry-empty"]')).toContainText('还没有注册任何服务');
  await expect(page.locator('[data-spec="registry-row"]')).toHaveCount(0);
});

test('Loading：spinner 加载中，不渲染空表', async ({ page }) => {
  await openStory(page, 'RegistryTab/Loading');
  await expect(page.locator('[data-spec="registry-loading"]')).toContainText('服务加载中');
  await expect(page.locator('[data-spec="registry-row"]')).toHaveCount(0);
});

test('Failed：请求失败显示可见错误与重试按钮，不当作空态', async ({ page }) => {
  await openStory(page, 'RegistryTab/Failed');
  await expect(page.locator('[data-spec="registry-failed"]')).toContainText('注册中心加载失败');
  await expect(page.locator('[data-spec="registry-retry"]')).toBeVisible();
});
