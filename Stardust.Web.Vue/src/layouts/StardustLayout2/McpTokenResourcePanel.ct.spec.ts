import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('选中令牌带授权：视觉基线', async ({ page }) => {
  await openStory(page, 'McpTokenResourcePanel/WithToken');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('未选令牌：提示空态视觉基线', async ({ page }) => {
  await openStory(page, 'McpTokenResourcePanel/NoToken');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('无授权行：空态视觉基线', async ({ page }) => {
  await openStory(page, 'McpTokenResourcePanel/EmptyResources');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

// 语义断言
test('WithToken 渲染三条授权行', async ({ page }) => {
  await openStory(page, 'McpTokenResourcePanel/WithToken');
  await expect(page.locator('[data-spec="mcp-resource-row"]')).toHaveCount(3);
});

test('NoToken 显示选择提示，不渲染新增行', async ({ page }) => {
  await openStory(page, 'McpTokenResourcePanel/NoToken');
  await expect(page.locator('[data-spec="mcp-resource-empty-token"]')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-resource-add"]')).toHaveCount(0);
});

test('未选资源时添加按钮禁用', async ({ page }) => {
  await openStory(page, 'McpTokenResourcePanel/WithToken');
  await expect(page.locator('[data-spec="mcp-resource-add"]')).toBeDisabled();
  // 选一个资源后按钮可用
  await page.locator('[data-spec="mcp-resource-id"]').selectOption({ index: 1 });
  await expect(page.locator('[data-spec="mcp-resource-add"]')).toBeEnabled();
});
