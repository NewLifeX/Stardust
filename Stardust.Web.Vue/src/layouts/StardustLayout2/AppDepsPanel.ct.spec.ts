import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('F15 上下游依赖：上游显示被依赖名（映射应用名），下游显示依赖方', async ({ page }) => {
  await openStory(page, 'AppDepsPanel/Both');
  const up = page.locator('[data-spec="app-deps-up-row"]');
  const down = page.locator('[data-spec="app-deps-down-row"]');
  await expect(up).toHaveCount(1);
  await expect(up).toContainText('StarWeb');
  await expect(up).toContainText('启用');
  await expect(down).toHaveCount(1);
  // 下游 MindHub 是 DisplayName；StarWeb 无 DisplayName 退化 Name
  await expect(down).toContainText('智能工单');
  await expect(down).toContainText('停用');
  await expect(page.locator('[data-spec="app-deps-manage"]')).toBeVisible();
});

test('F15 无依赖：两侧空态', async ({ page }) => {
  await openStory(page, 'AppDepsPanel/Empty');
  await expect(page.locator('[data-spec="app-deps-up-empty"]')).toContainText('没有声明上游依赖');
  await expect(page.locator('[data-spec="app-deps-down-empty"]')).toContainText('还没有应用依赖本应用');
});
