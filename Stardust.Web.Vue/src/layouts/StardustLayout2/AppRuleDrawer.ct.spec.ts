import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('AppRuleDrawer 新增：空表单 + 占位提示', async ({ page }) => {
  await openStory(page, 'AppRuleDrawer/Create');
  await expect(page.locator('[data-spec="drawer"]')).toContainText('新增应用规则');
  await expect(page.locator('[data-spec="app-rule-rule"]')).toHaveValue('');
  await expect(page.locator('[data-spec="app-rule-rule"]')).toHaveAttribute('placeholder', 'IP=10.*,172.*;LocalIP=192.*');
  await expect(page.locator('[data-spec="app-rule-enable"]')).toBeChecked();
  await expect(page.locator('[data-spec="app-rule-priority"]')).toHaveValue('0');
});

test('AppRuleDrawer 编辑：初值回填', async ({ page }) => {
  await openStory(page, 'AppRuleDrawer/Edit');
  await expect(page.locator('[data-spec="app-rule-rule"]')).toHaveValue('IP=10.*,172.*;LocalIP=192.*');
  await expect(page.locator('[data-spec="app-rule-result"]')).toHaveValue('Scope=dev');
  await expect(page.locator('[data-spec="app-rule-priority"]')).toHaveValue('5');
  await expect(page.locator('[data-spec="app-rule-remark"]')).toHaveValue('本机与内网段走 dev 作用域');
});

// 表单校验在视线内：清空 Result 提交 → 错误提示
test('AppRuleDrawer 命中结果必填：提交显示错误', async ({ page }) => {
  await openStory(page, 'AppRuleDrawer/Edit');
  await page.locator('[data-spec="app-rule-result"]').fill('');
  await page.locator('[data-spec="app-rule-submit"]').click();
  await expect(page.locator('[data-spec="app-rule-error"]')).toContainText('命中结果必填');
});

test('AppRuleDrawer 保存中：提交按钮禁用', async ({ page }) => {
  await openStory(page, 'AppRuleDrawer/Busy');
  const submit = page.locator('[data-spec="app-rule-submit"]');
  await expect(submit).toBeDisabled();
  await expect(submit).toHaveText('保存中…');
});
