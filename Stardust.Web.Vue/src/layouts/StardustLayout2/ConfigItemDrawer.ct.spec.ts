import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('ConfigItemDrawer 已发布：Key/Scope/Value 只读，NewValue 草稿回填，提示条可见', async ({ page }) => {
  await openStory(page, 'ConfigItemDrawer/Published');
  await expect(page.locator('[data-spec="drawer"]')).toContainText('编辑配置项 · Star.Server');
  await expect(page.locator('[data-spec="config-item-draft-hint"]')).toContainText('已发布');
  await expect(page.locator('[data-spec="config-item-key"]')).toHaveValue('Star.Server');
  await expect(page.locator('[data-spec="config-item-key"]')).toBeDisabled();
  await expect(page.locator('[data-spec="config-item-scope"]')).toBeDisabled();
  // 生效值只读展示（disabled input），草稿值可编辑
  await expect(page.locator('[data-spec="config-item-value"]')).toBeDisabled();
  await expect(page.locator('[data-spec="config-item-value"]')).toHaveValue('tcp://127.0.0.1:6680');
  await expect(page.locator('[data-spec="config-item-newvalue"]')).toHaveValue('tcp://10.0.0.8:6680');
  await expect(page.locator('[data-spec="config-item-newvalue"]')).toBeEnabled();
  await expect(page.locator('[data-spec="config-item-enable"]')).toBeChecked();
});

test('ConfigItemDrawer 未发布：Key/Scope/Value 全部可改，无草稿提示', async ({ page }) => {
  await openStory(page, 'ConfigItemDrawer/Unpublished');
  await expect(page.locator('[data-spec="config-item-draft-hint"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="config-item-key"]')).toBeEnabled();
  await expect(page.locator('[data-spec="config-item-key"]')).toHaveValue('LocalIP');
  await expect(page.locator('[data-spec="config-item-scope"]')).toHaveValue('dev');
  await expect(page.locator('[data-spec="config-item-value"]')).toBeEnabled();
  // 未发布行没有 NewValue 草稿框
  await expect(page.locator('[data-spec="config-item-newvalue"]')).toHaveCount(0);
});

test('ConfigItemDrawer 新增：Key 必填校验在视线内', async ({ page }) => {
  await openStory(page, 'ConfigItemDrawer/Create');
  await expect(page.locator('[data-spec="config-item-key"]')).toBeEnabled();
  await page.locator('[data-spec="config-item-submit"]').click();
  await expect(page.locator('[data-spec="config-item-error"]')).toContainText('必填');
  // 填上 Key 后错误消失（由父容器接 submit，这里只验校验通过即无 error）
  await page.locator('[data-spec="config-item-key"]').fill('NewKey');
  await page.locator('[data-spec="config-item-submit"]').click();
  await expect(page.locator('[data-spec="config-item-error"]')).toHaveCount(0);
});

test('ConfigItemDrawer 保存中：提交按钮禁用写「保存中…」', async ({ page }) => {
  await openStory(page, 'ConfigItemDrawer/Busy');
  const submit = page.locator('[data-spec="config-item-submit"]');
  await expect(submit).toBeDisabled();
  await expect(submit).toHaveText('保存中…');
});
