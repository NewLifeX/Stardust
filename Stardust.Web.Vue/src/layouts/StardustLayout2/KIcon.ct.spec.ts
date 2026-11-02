import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('KIcon 默认尺寸', async ({ page }) => {
  await openStory(page, 'KIcon/Default');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('KIcon 尺寸 20', async ({ page }) => {
  await openStory(page, 'KIcon/Size20');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('KIcon 尺寸 12', async ({ page }) => {
  await openStory(page, 'KIcon/Size12');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

// 语义断言：svg 的 width/height 跟随 size props
test('KIcon 尺寸属性跟随 props', async ({ page }) => {
  await openStory(page, 'KIcon/Size20');
  const svg = page.locator('.gal-preview svg');
  await expect(svg).toHaveAttribute('width', '20');
  await expect(svg).toHaveAttribute('height', '20');
});
