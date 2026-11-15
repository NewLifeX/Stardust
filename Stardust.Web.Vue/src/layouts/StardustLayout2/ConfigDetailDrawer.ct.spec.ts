import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

/** 点头部「预览」钮打开弹层（两个 Drawer 都有关闭钮，取最后出现的那个） */
async function openPreview(page: Page) {
  await page.locator('[data-spec="config-preview-link"]').click();
  await page.locator('[data-spec="config-preview-body"], [data-spec="config-preview-empty"], [data-spec="config-preview-failed"]').first().waitFor();
}

test('Preview：弹层显示 /config/getall 实拉键值，secret 脱敏，关闭无副作用', async ({ page }) => {
  await openStory(page, 'ConfigDetailDrawer/Preview');
  await openPreview(page);
  // 元信息：生效版本 + 键数
  await expect(page.locator('[data-spec="config-preview-meta"]')).toContainText('v1');
  await expect(page.locator('[data-spec="config-preview-meta"]')).toContainText('3 个键');
  // 键值按 Key 排序，内容与替身一致
  const rows = page.locator('[data-spec="config-preview-row"]');
  await expect(rows).toHaveCount(3);
  await expect(rows.nth(0)).toHaveAttribute('data-key', 'App.Trace.Enable');
  await expect(rows.nth(1)).toHaveAttribute('data-key', 'Log.Level');
  await expect(rows.nth(2)).toHaveAttribute('data-key', 'Timeout.Http');
  await expect(rows.nth(2)).toContainText('3000');
  // sub 端点脱敏：显示 appId 参数与 secret=***，不出现明文密钥
  await expect(page.locator('.gal-preview')).toContainText('GET /config/getall?appId=订单服务&secret=***');
  // 关闭预览弹层：主抽屉仍开着（关闭无副作用）
  await page.locator('[data-spec="drawer-close"]').last().click();
  await expect(page.locator('[data-spec="config-preview-body"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="config-detail-body"]')).toBeVisible();
});

test('PreviewEmpty：拉到结果但没有键时显示空态', async ({ page }) => {
  await openStory(page, 'ConfigDetailDrawer/PreviewEmpty');
  await openPreview(page);
  await expect(page.locator('[data-spec="config-preview-empty"]')).toContainText('该应用还没有可拉取的配置');
});

test('PreviewFailed：取数失败显示可见失败 + 重试按钮', async ({ page }) => {
  await openStory(page, 'ConfigDetailDrawer/PreviewFailed');
  await openPreview(page);
  await expect(page.locator('[data-spec="config-preview-failed"]')).toContainText('预览加载失败');
  await expect(page.locator('[data-spec="config-preview-retry"]')).toContainText('重试');
});
