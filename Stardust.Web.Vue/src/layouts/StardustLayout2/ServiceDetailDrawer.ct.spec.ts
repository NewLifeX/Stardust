import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('ServiceDetailDrawer 详情：标题/编辑表单回填 + 提供者/消费者列表', async ({ page }) => {
  await openStory(page, 'ServiceDetailDrawer/Default');
  await expect(page.locator('[data-spec="drawer"]')).toContainText('服务详情 · Star.Web');
  await expect(page.locator('[data-spec="service-form-displayname"]')).toHaveValue('星尘主站');
  await expect(page.locator('[data-spec="service-form-address"]')).toHaveValue('tcp://10.0.0.1:6680');
  await expect(page.locator('[data-spec="service-form-enable"]')).toBeChecked();
  await expect(page.locator('[data-spec="service-form-healthcheck"]')).toBeChecked();
  await expect(page.locator('[data-spec="service-form-singleton"]')).not.toBeChecked();
  await expect(page.locator('[data-spec="service-detail"]')).toContainText('提供者（2）');
  await expect(page.locator('[data-spec="service-provider-row"]')).toHaveCount(2);
  await expect(page.locator('[data-spec="service-detail"]')).toContainText('消费者（1）');
  await expect(page.locator('[data-spec="service-consumer-row"]')).toHaveCount(1);
  await expect(page.locator('[data-spec="service-form-submit"]')).toBeEnabled();
});

// 表单校验在视线内：清空显示名提交 → 错误提示
test('ServiceDetailDrawer 显示名必填：提交显示错误', async ({ page }) => {
  await openStory(page, 'ServiceDetailDrawer/Default');
  await page.locator('[data-spec="service-form-displayname"]').fill('');
  await page.locator('[data-spec="service-form-submit"]').click();
  await expect(page.locator('[data-spec="service-form-error"]')).toContainText('必填');
});

test('ServiceDetailDrawer 加载中/失败：互斥三态，失败给重试', async ({ page }) => {
  await openStory(page, 'ServiceDetailDrawer/Loading');
  await expect(page.locator('[data-spec="service-detail-loading"]')).toBeVisible();
  await openStory(page, 'ServiceDetailDrawer/Failed');
  await expect(page.locator('[data-spec="service-detail-failed"]')).toBeVisible();
  await expect(page.locator('[data-spec="service-detail-retry"]')).toBeVisible();
});

test('ServiceDetailDrawer 保存中：提交按钮禁用写「保存中…」', async ({ page }) => {
  await openStory(page, 'ServiceDetailDrawer/Busy');
  const submit = page.locator('[data-spec="service-form-submit"]');
  await expect(submit).toBeDisabled();
  await expect(submit).toHaveText('保存中…');
});
