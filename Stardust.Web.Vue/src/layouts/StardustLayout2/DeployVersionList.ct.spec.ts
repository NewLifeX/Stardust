import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

const ROW = '[data-spec="ship-version-row"]';
const APPLY = '[data-spec="ship-apply"]';
const NOTICE = '[data-spec="ship-version-notice"]';
const TOGGLE = '[data-spec="ship-versions-toggle"]';

test('DeployVersionList 默认：三种行状态与按钮文案各不相同，没有提示条', async ({ page }) => {
  await openStory(page, 'DeployVersionList/Default');
  await expect(page.locator(ROW)).toHaveCount(3);
  await expect(page.locator('[data-spec="ship-versions"]')).toContainText('共 3 个');
  await expect(page.locator(NOTICE)).toHaveCount(0);
  await expect(page.locator(TOGGLE)).toHaveCount(0);

  // F05：上传新版本入口在视线内，隐藏的 file input 存在但不可见
  const upload = page.locator('[data-spec="ship-upload"]');
  await expect(upload).toBeEnabled();
  await expect(upload).toContainText('上传新版本');
  await expect(page.locator('[data-spec="ship-upload-input"]')).toBeHidden();

  // 当前生效：按钮禁用，文案「当前版本」
  const current = page.locator(ROW).nth(0).locator(APPLY);
  await expect(current).toBeDisabled();
  await expect(current).toHaveText('当前版本');
  await expect(page.locator(ROW).nth(0)).toContainText('当前生效');

  // 可应用：按钮可点，文案「应用此版本」
  const ok = page.locator(ROW).nth(1).locator(APPLY);
  await expect(ok).toBeEnabled();
  await expect(ok).toHaveText('应用此版本');
  await expect(page.locator(ROW).nth(1)).toContainText('可应用');

  // 不可用：按钮禁用，原因写在标签和 title 里
  const off = page.locator(ROW).nth(2).locator(APPLY);
  await expect(off).toBeDisabled();
  await expect(page.locator(ROW).nth(2)).toContainText('不可用 · 没有文件地址');
  await expect(off).toHaveAttribute('title', '没有文件地址');
});

// AGENTS.md §11：上传进行中必须禁用、旋转、文案变化，且应用按钮同时禁用
test('DeployVersionList 上传中：上传按钮禁用旋转写「上传中…」，应用按钮同步禁用', async ({ page }) => {
  await openStory(page, 'DeployVersionList/Uploading');
  const upload = page.locator('[data-spec="ship-upload"]');
  await expect(upload).toBeDisabled();
  await expect(upload).toContainText('上传中…');
  await expect(upload.locator('.animate-spin')).toHaveCount(1);
  await expect(page.locator(ROW).nth(1).locator(APPLY)).toBeDisabled();
});

test('DeployVersionList 超过 8 个版本：默认 8 行，可展开与收起', async ({ page }) => {
  await openStory(page, 'DeployVersionList/Many');
  await expect(page.locator(ROW)).toHaveCount(8);
  const toggle = page.locator(TOGGLE);
  await expect(toggle).toHaveText('显示全部 12 个版本');
  await toggle.click();
  await expect(page.locator(ROW)).toHaveCount(12);
  await expect(toggle).toHaveText('收起');
  await toggle.click();
  await expect(page.locator(ROW)).toHaveCount(8);
});

test('DeployVersionList 没有版本与加载失败的提示不同', async ({ page }) => {
  await openStory(page, 'DeployVersionList/Empty');
  await expect(page.locator('[data-spec="ship-versions-empty"]')).toContainText('该部署集还没有版本');
  await expect(page.locator('[data-spec="ship-versions-failed"]')).toHaveCount(0);

  await openStory(page, 'DeployVersionList/Failed');
  await expect(page.locator('[data-spec="ship-versions-failed"]')).toContainText('版本加载失败');
  await expect(page.locator('[data-spec="ship-versions-empty"]')).toHaveCount(0);
});

// 反馈：操作进行中，该行写「切换中…」，其余按钮也不能点，避免并发下发
test('DeployVersionList 应用中：该行显示「切换中…」，所有按钮禁用', async ({ page }) => {
  await openStory(page, 'DeployVersionList/Busy');
  await expect(page.locator(ROW).nth(1).locator(APPLY)).toHaveText('切换中…');
  const n = await page.locator(APPLY).count();
  for (let i = 0; i < n; i++) {
    await expect(page.locator(APPLY).nth(i)).toBeDisabled();
  }
});

// 语义断言：提示条两种语气颜色不同（warn 用警告色）
test('DeployVersionList 提示条：info 与 warn 文字色不同，文案原样显示', async ({ page }) => {
  const colorOf = async (id: string) => {
    await openStory(page, id);
    return page.locator(NOTICE).evaluate((el) => getComputedStyle(el).color);
  };
  const info = await colorOf('DeployVersionList/NoticeInfo');
  await expect(page.locator(NOTICE)).toContainText('部署集未启用');
  const warn = await colorOf('DeployVersionList/NoticeWarn');
  await expect(page.locator(NOTICE)).toContainText('下发 install');
  expect(warn).not.toBe(info);
});
