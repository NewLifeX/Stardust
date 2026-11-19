import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('Diff：三键逐行 old→new，变更 Tag 与 warning 色', async ({ page }) => {
  await openStory(page, 'ConfigHistoryDiffDrawer/Diff');
  const rows = page.locator('[data-spec="config-history-diff-row"]');
  await expect(rows).toHaveCount(3);
  const kinds = await rows.evaluateAll((els) => els.map((el) => el.textContent ?? ''));
  expect(kinds[0]).toContain('Enable');
  expect(kinds[0]).toContain('变更');
  expect(kinds[0]).toContain('true');
  expect(kinds[0]).toContain('false');
  await expect(page.locator('[data-spec="config-history-diff-row"]', { hasText: 'NextVersion' })).toContainText('2');
  await expect(page.locator('[data-spec="config-history-diff"]')).not.toContainText('首次发布');
});

test('DiffFirst：首次发布提示条，全量字段按新增归类', async ({ page }) => {
  await openStory(page, 'ConfigHistoryDiffDrawer/DiffFirst');
  await expect(page.locator('[data-spec="config-history-diff-first"]')).toContainText('首次发布');
  await expect(page.locator('[data-spec="config-history-diff-row"]')).toHaveCount(2);
  await expect(
    page.locator('[data-spec="config-history-diff-row"]', { hasText: 'Version' }),
  ).toContainText('新增');
});

test('DiffClean：changedKeys 空显示无差异空态，不渲染行', async ({ page }) => {
  await openStory(page, 'ConfigHistoryDiffDrawer/DiffClean');
  await expect(page.locator('[data-spec="config-history-diff-empty"]')).toContainText('无差异');
  await expect(page.locator('[data-spec="config-history-diff-row"]')).toHaveCount(0);
});

test('DiffFail：请求抛错显示对比加载失败', async ({ page }) => {
  await openStory(page, 'ConfigHistoryDiffDrawer/DiffFail');
  await expect(page.locator('[data-spec="config-history-diff-fail"]')).toContainText('对比加载失败');
  await expect(page.locator('[data-spec="config-history-diff-row"]')).toHaveCount(0);
});
