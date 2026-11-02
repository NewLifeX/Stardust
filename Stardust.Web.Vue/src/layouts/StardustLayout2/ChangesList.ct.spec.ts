import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('Mixed：四行，D/C 方块按 data-kind 区分，成功绿/失败红', async ({ page }) => {
  await openStory(page, 'ChangesList/Mixed');
  const rows = page.locator('[data-spec="changes-row"]');
  await expect(rows).toHaveCount(4);
  const kinds = await rows.evaluateAll((els) => els.map((el) => el.getAttribute('data-kind')));
  expect(kinds).toEqual(['D', 'D', 'C', 'D']);
  await expect(page.locator('[data-spec="changes-list"]')).toContainText('成功');
  await expect(page.locator('[data-spec="changes-list"]')).toContainText('失败');
});

test('Mixed：第一行是最新的流水线 D，显示应用名与摘要', async ({ page }) => {
  await openStory(page, 'ChangesList/Mixed');
  const first = page.locator('[data-spec="changes-row"]').first();
  await expect(first).toContainText('OrderService');
  await expect(first).toContainText('feat:');
  await expect(first).toHaveAttribute('data-path', '/app/pipeline/101');
});

test('Empty：没有记录时显示空态文案', async ({ page }) => {
  await openStory(page, 'ChangesList/Empty');
  await expect(page.locator('[data-spec="changes-empty"]')).toContainText('最近没有变更记录');
  await expect(page.locator('[data-spec="changes-row"]')).toHaveCount(0);
});

test('Loading：spinner 加载中，不渲染空表', async ({ page }) => {
  await openStory(page, 'ChangesList/Loading');
  await expect(page.locator('[data-spec="changes-loading"]')).toContainText('变更加载中');
  await expect(page.locator('[data-spec="changes-row"]')).toHaveCount(0);
});

test('Failed：请求失败显示可见错误，不当作空态', async ({ page }) => {
  await openStory(page, 'ChangesList/Failed');
  await expect(page.locator('[data-spec="changes-failed"]')).toContainText('变更加载失败');
  await expect(page.locator('[data-spec="changes-empty"]')).toHaveCount(0);
});

test('SelectRecorder：点行把跳转目标写进 window.__changesSelect', async ({ page }) => {
  await openStory(page, 'ChangesList/SelectRecorder');
  await page.locator('[data-spec="changes-row"]').nth(2).click();
  const picked = await page.evaluate(() => (window as unknown as { __changesSelect?: string[] }).__changesSelect);
  expect(picked).toEqual(['/app/config/202']);
});
