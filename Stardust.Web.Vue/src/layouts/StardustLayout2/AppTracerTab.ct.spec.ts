import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('监控中心列出跟踪器，含状态与操作列', async ({ page }) => {
  await openStory(page, 'AppTracerTab/List');
  await expect(page.locator('[data-spec="tracer-row"]')).toHaveCount(2);
  await expect(page.locator('[data-spec="tracer-row"]').first()).toContainText('ioc');
  await expect(page.locator('[data-spec="tracer-row"]').first()).toContainText('跟踪所有项');
  await expect(page.locator('[data-spec="tracer-row"]').nth(1)).toContainText('停用');
  await expect(page.locator('[data-spec="tracer-row"]').first()).toContainText('编辑');
  await expect(page.locator('[data-spec="tracer-row"]').first()).toContainText('删除');
});

test('批量修正未勾选时禁用，勾选后可用', async ({ page }) => {
  await openStory(page, 'AppTracerTab/List');
  const fix = page.locator('[data-spec="tracer-fix"]');
  await expect(fix).toBeDisabled();
  await page.locator('[data-spec="tracer-row"]').first().locator('[data-spec="tracer-select"]').check();
  await expect(fix).toBeEnabled();
});

test('编辑打开 Drawer 表单并带初值，取消可关闭', async ({ page }) => {
  await openStory(page, 'AppTracerTab/List');
  await page.locator('[data-spec="tracer-edit"]').first().click();
  await expect(page.locator('[data-spec="tracer-form"]')).toBeVisible();
  await expect(page.locator('[data-spec="tracer-form-displayname"]')).toHaveValue('IoC容器');
  await expect(page.locator('[data-spec="tracer-form-mode"]')).toHaveValue('0');
  await page.locator('[data-spec="tracer-form-cancel"]').click();
  await expect(page.locator('[data-spec="tracer-form"]')).toHaveCount(0);
});

test('没有跟踪器时显示空态引导', async ({ page }) => {
  await openStory(page, 'AppTracerTab/Empty');
  await expect(page.locator('[data-spec="tracer-empty"]')).toContainText('还没有跟踪器');
  await expect(page.locator('[data-spec="tracer-empty"]')).toContainText('自动创建');
  await expect(page.locator('[data-spec="tracer-row"]')).toHaveCount(0);
});

test('加载失败显示失败文案与重试', async ({ page }) => {
  await openStory(page, 'AppTracerTab/Failed');
  await expect(page.locator('[data-spec="tracer-failed"]')).toContainText('监控中心加载失败');
  await expect(page.locator('[data-spec="tracer-failed"]')).toContainText('stub 500');
  await expect(page.locator('[data-spec="tracer-retry"]')).toBeVisible();
});
