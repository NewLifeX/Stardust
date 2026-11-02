import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('Data：路由名+域名副行，集群名解析，通配证书显示 TLS 域', async ({ page }) => {
  await openStory(page, 'GatewayTab/Data');
  await expect(page.locator('[data-spec="gateway-row"]')).toHaveCount(3);
  const first = page.locator('[data-spec="gateway-row"]').first();
  await expect(first).toContainText('iot-api');
  await expect(first).toContainText('DeviceService');
  await expect(first).toContainText('iot.example.com');
  // 无 ClusterId 解析到集群的行显示待接入；无证书的行 TLS 待接入
  await expect(page.locator('[data-spec="gateway-row"]').nth(2)).toContainText('待接入');
});

test('Empty：无路由显示空态', async ({ page }) => {
  await openStory(page, 'GatewayTab/Empty');
  await expect(page.locator('[data-spec="gateway-empty"]')).toContainText('还没有配置网关路由');
  await expect(page.locator('[data-spec="gateway-row"]')).toHaveCount(0);
});

test('Loading：spinner 加载中', async ({ page }) => {
  await openStory(page, 'GatewayTab/Loading');
  await expect(page.locator('[data-spec="gateway-loading"]')).toContainText('路由加载中');
});

test('Failed：请求失败显示错误与重试', async ({ page }) => {
  await openStory(page, 'GatewayTab/Failed');
  await expect(page.locator('[data-spec="gateway-failed"]')).toContainText('网关路由加载失败');
  await expect(page.locator('[data-spec="gateway-retry"]')).toBeVisible();
});
