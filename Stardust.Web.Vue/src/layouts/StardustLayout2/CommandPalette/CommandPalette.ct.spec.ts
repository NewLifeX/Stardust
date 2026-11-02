import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('CommandPalette 三个分组共 5 项', async ({ page }) => {
  await openStory(page, 'CommandPalette/Groups');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('CommandPalette 单分组单项', async ({ page }) => {
  await openStory(page, 'CommandPalette/Single');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('CommandPalette 空分组', async ({ page }) => {
  await openStory(page, 'CommandPalette/Empty');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

// 语义断言：输入框占位符取自 config.placeholder，首项默认选中
test('CommandPalette 占位符取自 config', async ({ page }) => {
  await openStory(page, 'CommandPalette/Groups');
  await expect(page.locator('[data-spec="palette-input"]')).toHaveAttribute(
    'placeholder',
    '搜索应用、节点、TraceId,或执行操作…',
  );
});

test('CommandPalette 首项默认选中', async ({ page }) => {
  await openStory(page, 'CommandPalette/Groups');
  const selected = page.locator('.gal-preview [aria-selected="true"]');
  await expect(selected).toHaveCount(1);
  await expect(selected).toContainText('跳转 · 此刻');
});

test('CommandPalette 输入过滤后仅留命中项', async ({ page }) => {
  await openStory(page, 'CommandPalette/Groups');
  await page.locator('[data-spec="palette-input"]').fill('OrderService');
  await page.waitForTimeout(200);
  const items = page.locator('.gal-preview [role="option"]');
  await expect(items).toHaveCount(1);
  await expect(items.first()).toContainText('OrderService');
});
