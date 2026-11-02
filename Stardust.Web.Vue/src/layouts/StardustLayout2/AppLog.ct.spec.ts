import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

const ROW = '[data-spec="log-row"]';
const LEVEL = '[data-spec="log-row"] span:nth-child(2)';

test('AppLog 有日志：五个级别 chip、四行日志、暂停实时按钮', async ({ page }) => {
  await openStory(page, 'AppLog/WithLogs');
  await expect(page.locator('[data-spec="log-chip"]')).toHaveText(['全部', 'Error', 'Warn', 'Info', 'Debug']);
  await expect(page.locator(ROW)).toHaveCount(4);
  await expect(page.locator('[data-spec="log-pause"]')).toContainText('暂停实时');
});

test('AppLog 级别颜色：Info 与 Error 的级别列颜色不同', async ({ page }) => {
  await openStory(page, 'AppLog/WithLogs');
  const colors = await page.locator(LEVEL).evaluateAll((els) => els.map((el) => getComputedStyle(el).color));
  expect(colors[2]).not.toBe(colors[0]);
  expect(colors[1]).not.toBe(colors[2]);
});

test('AppLog 级别过滤：点 Error chip 只剩 Error 行，再点全部恢复四行', async ({ page }) => {
  await openStory(page, 'AppLog/WithLogs');
  await page.locator('[data-spec="log-chip"]', { hasText: 'Error' }).click();
  await expect(page.locator(ROW)).toHaveCount(1);
  await expect(page.locator(LEVEL)).toHaveText(['error']);
  await page.locator('[data-spec="log-chip"]', { hasText: '全部' }).click();
  await expect(page.locator(ROW)).toHaveCount(4);
});

test('AppLog 暂停/恢复：按钮文案切换', async ({ page }) => {
  await openStory(page, 'AppLog/WithLogs');
  await page.locator('[data-spec="log-pause"]').click();
  await expect(page.locator('[data-spec="log-pause"]')).toContainText('恢复实时');
  await page.locator('[data-spec="log-pause"]').click();
  await expect(page.locator('[data-spec="log-pause"]')).toContainText('暂停实时');
});

test('AppLog 空态：今天没有日志记录', async ({ page }) => {
  await openStory(page, 'AppLog/Empty');
  await expect(page.locator('[data-spec="log-empty"]')).toContainText('该应用今天没有日志记录');
});
