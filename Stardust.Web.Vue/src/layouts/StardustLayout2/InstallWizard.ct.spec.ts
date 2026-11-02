import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(500);
}

// 视觉截图：Drawer 是 fixed 定位，截整个视口
test('InstallWizard 打开态：安装命令 + 等待上线空态', async ({ page }) => {
  await openStory(page, 'InstallWizard/Basic');
  await expect(page).toHaveScreenshot();
});

// 语义断言
test('InstallWizard 显示安装命令说明文本', async ({ page }) => {
  await openStory(page, 'InstallWizard/Basic');
  const wizard = page.locator('[data-spec="drawer"]');
  await expect(wizard).toBeVisible();
  await expect(wizard).toContainText('接入向导');
  await expect(wizard).toContainText('复制安装命令');
  await expect(wizard).toContainText('等待节点上线');
});

test('InstallWizard 有复制按钮', async ({ page }) => {
  await openStory(page, 'InstallWizard/Basic');
  await expect(page.locator('[data-spec="wizard-copy"]')).toBeVisible();
});

test('InstallWizard 关闭态不可见', async ({ page }) => {
  await openStory(page, 'InstallWizard/Closed');
  await expect(page.locator('[data-spec="drawer"]')).toBeHidden();
});

test('InstallWizard 点 X 关闭', async ({ page }) => {
  await openStory(page, 'InstallWizard/Basic');
  await page.locator('[data-spec="drawer-close"]').click();
  await expect(page.locator('[data-spec="drawer"]')).toBeHidden();
});
