import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

async function events(page: Page): Promise<string[]> {
  return page.evaluate(() => (window as unknown as { __appQuoteEvents?: string[] }).__appQuoteEvents ?? []);
}

test('F15 应用依赖表：两行 + 启用/停用 Tag + 行操作 emit', async ({ page }) => {
  await openStory(page, 'AppQuoteTable/Rows');
  const rows = page.locator('[data-spec="app-quote-row"]');
  await expect(rows).toHaveCount(2);
  await expect(rows.first()).toContainText('OrderService');
  await expect(rows.first()).toContainText('StarWeb');
  await expect(rows.first()).toContainText('启用');
  await expect(rows.nth(1)).toContainText('停用');
  await rows.first().locator('[data-spec="app-quote-edit"]').click();
  await rows.first().locator('[data-spec="app-quote-remove"]').click();
  expect(await events(page)).toEqual(['edit:2', 'remove:2']);
});

test('F15 空态与失败态', async ({ page }) => {
  await openStory(page, 'AppQuoteTable/Empty');
  await expect(page.locator('[data-spec="app-quote-empty"]')).toContainText('新增依赖');
  await openStory(page, 'AppQuoteTable/Failed');
  await expect(page.locator('[data-spec="app-quote-failed"]')).toContainText('加载失败');
  await expect(page.locator('[data-spec="app-quote-retry"]')).toBeVisible();
});

test('F15 删除中：行按钮禁用写「删除中…」', async ({ page }) => {
  await openStory(page, 'AppQuoteTable/Removing');
  const btn = page.locator('[data-spec="app-quote-row"]').first().locator('[data-spec="app-quote-remove"]');
  await expect(btn).toBeDisabled();
  await expect(btn).toHaveText('删除中…');
});

test('F15 依赖抽屉：必填校验 + 选两个应用提交 emit 表单', async ({ page }) => {
  await openStory(page, 'AppQuoteDrawer/Create');
  await page.locator('[data-spec="app-quote-submit"]').click();
  await expect(page.locator('[data-spec="app-quote-error"]')).toContainText('依赖方');
  await page.locator('[data-spec="app-quote-app"]').selectOption({ label: 'OrderService' });
  await page.locator('[data-spec="app-quote-target"]').selectOption({ label: 'StarWeb' });
  await page.locator('[data-spec="app-quote-kind"]').fill('2');
  await page.locator('[data-spec="app-quote-submit"]').click();
  expect(await events(page)).toEqual(['submit:7>9@2:true']);
  // 编辑回填
  await openStory(page, 'AppQuoteDrawer/Edit');
  await expect(page.locator('[data-spec="app-quote-app"]')).toHaveValue('7');
  await expect(page.locator('[data-spec="app-quote-target"]')).toHaveValue('9');
  await expect(page.locator('[data-spec="app-quote-kind"]')).toHaveValue('1');
  await expect(page.locator('[data-spec="app-quote-enable"]')).toBeChecked();
});
