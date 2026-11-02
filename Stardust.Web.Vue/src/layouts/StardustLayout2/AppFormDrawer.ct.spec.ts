import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('AppFormDrawer 新建：Name 可填，Secret 占位「留空由服务端自动生成」', async ({ page }) => {
  await openStory(page, 'AppFormDrawer/Create');
  await expect(page.locator('[data-spec="drawer"]')).toContainText('新建应用');
  const name = page.locator('[data-spec="app-form-name"]');
  await expect(name).toBeEnabled();
  await expect(name).toHaveAttribute('placeholder', '小写字母/数字/点，如 star.web');
  await expect(page.locator('[data-spec="app-form-secret"]')).toHaveAttribute('placeholder', '留空由服务端自动生成');
  await expect(page.locator('[data-spec="app-form-period"]')).toHaveValue('60');
  await expect(page.locator('[data-spec="app-form-enable"]')).toBeChecked();
});

test('AppFormDrawer 编辑：Name 只读，Secret 不回显且占位「留空=不修改」', async ({ page }) => {
  await openStory(page, 'AppFormDrawer/Edit');
  await expect(page.locator('[data-spec="app-form-name"]')).toBeDisabled();
  await expect(page.locator('[data-spec="app-form-name"]')).toHaveValue('star.web');
  await expect(page.locator('[data-spec="app-form-displayname"]')).toHaveValue('星尘主站');
  await expect(page.locator('[data-spec="app-form-category"]')).toHaveValue('IoT 平台');
  const secret = page.locator('[data-spec="app-form-secret"]');
  await expect(secret).toHaveValue('');
  await expect(secret).toHaveAttribute('placeholder', '留空=不修改');
});

// 表单校验在视线内：清空名称提交 → 错误提示
test('AppFormDrawer 名称必填：提交显示错误', async ({ page }) => {
  await openStory(page, 'AppFormDrawer/Create');
  await page.locator('[data-spec="app-form-name"]').fill('');
  await page.locator('[data-spec="app-form-submit"]').click();
  await expect(page.locator('[data-spec="app-form-error"]')).toContainText('必填');
});

test('AppFormDrawer 保存中：提交按钮禁用', async ({ page }) => {
  await openStory(page, 'AppFormDrawer/Busy');
  const submit = page.locator('[data-spec="app-form-submit"]');
  await expect(submit).toBeDisabled();
  await expect(submit).toHaveText('保存中…');
});
