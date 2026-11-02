import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('AppRuleTable 列表：规则/结果/优先级/状态/操作列齐全，按优先级序展示', async ({ page }) => {
  await openStory(page, 'AppRuleTable/Default');
  const row2 = page.locator('[data-spec="app-rule-row"][data-rule-id="2"]');
  await expect(row2).toContainText('IP=10.*,172.*;LocalIP=192.*');
  await expect(row2).toContainText('Scope=dev');
  await expect(row2).toContainText('9');
  await expect(row2.locator('[data-spec="app-rule-edit"]')).toBeEnabled();
  await expect(page.locator('[data-rule-id="1"]')).toContainText('停用');
  await expect(page.locator('[data-spec="app-rule-create"]')).toBeEnabled();
});

test('AppRuleTable 删除中：该行按钮写「删除中…」其余正常', async ({ page }) => {
  await openStory(page, 'AppRuleTable/Removing');
  await expect(page.locator('[data-rule-id="1"] [data-spec="app-rule-remove"]')).toHaveText('删除中…');
  await expect(page.locator('[data-rule-id="2"] [data-spec="app-rule-remove"]')).toHaveText('删除');
});

test('AppRuleTable 三态：空态引导 / 失败重试 / 加载中', async ({ page }) => {
  await openStory(page, 'AppRuleTable/Empty');
  await expect(page.locator('[data-spec="app-rule-empty"]')).toContainText('还没有应用规则');
  await openStory(page, 'AppRuleTable/Failed');
  await expect(page.locator('[data-spec="app-rule-failed"]')).toContainText('加载失败');
  await expect(page.locator('[data-spec="app-rule-retry"]')).toBeVisible();
  await openStory(page, 'AppRuleTable/Loading');
  await expect(page.locator('[data-spec="app-rule-loading"]')).toBeVisible();
});
