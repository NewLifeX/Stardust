import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('DeployEditDrawer 默认：初值回填，密码框为空且占位提示「留空=不修改」', async ({ page }) => {
  await openStory(page, 'DeployEditDrawer/Default');
  // 标题在 Drawer 头部（deploy-form 只是字段容器）
  await expect(page.locator('[data-spec="drawer"]')).toContainText('编辑部署集');
  await expect(page.locator('[data-spec="deploy-form-name"]')).toHaveValue('IoC容器发布集');
  await expect(page.locator('[data-spec="deploy-form-category"]')).toHaveValue('中间件');
  await expect(page.locator('[data-spec="deploy-form-packagename"]')).toHaveValue('ioc.zip');
  await expect(page.locator('[data-spec="deploy-form-enable"]')).toBeChecked();
  await expect(page.locator('[data-spec="deploy-form-autopublish"]')).toBeChecked();
  await expect(page.locator('[data-spec="deploy-form-multiversion"]')).not.toBeChecked();
  await expect(page.locator('[data-spec="deploy-form-repository"]')).toHaveValue('https://git.example.com/x/ioc.git');
  await expect(page.locator('[data-spec="deploy-form-repousername"]')).toHaveValue('deployer');
  // 密码语义：永不回显，占位写明留空=不修改
  const pwd = page.locator('[data-spec="deploy-form-repopassword"]');
  await expect(pwd).toHaveValue('');
  await expect(pwd).toHaveAttribute('placeholder', '留空=不修改');
  await expect(pwd).toHaveAttribute('type', 'password');
  await expect(page.locator('[data-spec="deploy-form-branch"]')).toHaveValue('main');
  await expect(page.locator('[data-spec="deploy-form-buildargs"]')).toHaveValue('-c Release');
  await expect(page.locator('[data-spec="deploy-form-packagefilters"]')).toHaveValue('*.dll;*.exe');
});

// 表单校验在视线内：清空名称提交 → 错误提示，不发请求
test('DeployEditDrawer 名称必填：清空后提交显示错误', async ({ page }) => {
  await openStory(page, 'DeployEditDrawer/Default');
  await page.locator('[data-spec="deploy-form-name"]').fill('');
  await page.locator('[data-spec="deploy-form-submit"]').click();
  await expect(page.locator('[data-spec="deploy-form-error"]')).toHaveText('部署集名称必填');
});

// AGENTS.md §11：保存请求进行中按钮禁用并写「保存中…」
test('DeployEditDrawer 保存中：提交按钮禁用写「保存中…」', async ({ page }) => {
  await openStory(page, 'DeployEditDrawer/Busy');
  const btn = page.locator('[data-spec="deploy-form-submit"]');
  await expect(btn).toBeDisabled();
  await expect(btn).toHaveText('保存中…');
  await expect(page.locator('[data-spec="deploy-form-cancel"]')).toBeEnabled();
});
