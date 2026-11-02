import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

const REFRESH = '[data-spec="ship-refresh"]';
const STATS = '[data-spec="ship-current-stats"]';
const DIFF = '[data-spec="ship-diff-count"]';
const SYNC = '[data-spec="ship-sync-app"]';
const EDIT = '[data-spec="ship-deploy-edit"]';

test('DeployVersionBar 默认：显示版本与统计，刷新按钮可点、不在旋转', async ({ page }) => {
  await openStory(page, 'DeployVersionBar/Default');
  await expect(page.locator('[data-spec="ship-current-version"]')).toHaveText('v1.4.2');
  await expect(page.locator(STATS)).toContainText('4 个启用节点');
  const btn = page.locator(REFRESH);
  await expect(btn).toBeEnabled();
  await expect(btn).toContainText('刷新');
  await expect(btn).toHaveAttribute('aria-busy', 'false');
  await expect(btn.locator('.animate-spin')).toHaveCount(0);
  // F06：编辑发布集与同步应用入口在视线内
  await expect(page.locator(EDIT)).toBeEnabled();
  await expect(page.locator(SYNC)).toBeEnabled();
  await expect(page.locator(SYNC)).toContainText('同步应用');
});

// AGENTS.md §11：同步应用请求进行中必须禁用、旋转、文案变化
test('DeployVersionBar 同步中：同步按钮禁用旋转写「同步中…」，编辑与刷新同时禁用', async ({ page }) => {
  await openStory(page, 'DeployVersionBar/Syncing');
  const btn = page.locator(SYNC);
  await expect(btn).toBeDisabled();
  await expect(btn).toContainText('同步中…');
  await expect(btn.locator('.animate-spin')).toHaveCount(1);
  await expect(page.locator(EDIT)).toBeDisabled();
  await expect(page.locator(REFRESH)).toBeDisabled();
});

// AGENTS.md §11：刷新中必须禁用、旋转、文案变化
test('DeployVersionBar 刷新中：按钮禁用，aria-busy，图标旋转，文案「刷新中…」', async ({ page }) => {
  await openStory(page, 'DeployVersionBar/Refreshing');
  const btn = page.locator(REFRESH);
  await expect(btn).toBeDisabled();
  await expect(btn).toHaveAttribute('aria-busy', 'true');
  await expect(btn).toContainText('刷新中…');
  const spinner = btn.locator('.animate-spin');
  await expect(spinner).toHaveCount(1);
  const animationName = await spinner.evaluate((el) => getComputedStyle(el).animationName);
  expect(animationName).not.toBe('none');
});

test('DeployVersionBar 有操作在进行：刷新按钮禁用，但文案仍是「刷新」且不旋转', async ({ page }) => {
  await openStory(page, 'DeployVersionBar/Busy');
  const btn = page.locator(REFRESH);
  await expect(btn).toBeDisabled();
  await expect(btn).toHaveAttribute('aria-busy', 'false');
  await expect(btn).not.toContainText('刷新中');
  await expect(btn.locator('.animate-spin')).toHaveCount(0);
});

test('DeployVersionBar 节点加载失败与没有启用节点的文案不同', async ({ page }) => {
  await openStory(page, 'DeployVersionBar/NodesFailed');
  await expect(page.locator(STATS)).toContainText('节点加载失败');
  await openStory(page, 'DeployVersionBar/NoNodes');
  await expect(page.locator(STATS)).toContainText('没有启用的节点');
});

test('DeployVersionBar 有不一致节点时显示数量；一致时不显示', async ({ page }) => {
  await openStory(page, 'DeployVersionBar/Diff');
  await expect(page.locator(DIFF)).toHaveText('1');
  await expect(page.locator(STATS)).toContainText('个不一致');
  await openStory(page, 'DeployVersionBar/Default');
  await expect(page.locator(DIFF)).toHaveCount(0);
});

// 语义断言：开多版本后不一致不算异常，数字不用警告色，并多一个「多版本」标签
test('DeployVersionBar 多版本下不一致的数字颜色与非多版本不同', async ({ page }) => {
  const colorOf = async (id: string) => {
    await openStory(page, id);
    return page.locator(DIFF).evaluate((el) => getComputedStyle(el).color);
  };
  const warn = await colorOf('DeployVersionBar/Diff');
  const multi = await colorOf('DeployVersionBar/DiffMulti');
  expect(multi).not.toBe(warn);
  await expect(page.locator('[data-spec="tag"]', { hasText: '多版本' })).toHaveCount(1);
});
