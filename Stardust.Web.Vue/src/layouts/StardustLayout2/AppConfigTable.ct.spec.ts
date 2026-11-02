import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('Pending：四行状态依次 已发布/已修改/新增/待删除，待发布浮条 3 项', async ({ page }) => {
  await openStory(page, 'AppConfigTable/Pending');
  const states = await page.locator('[data-spec="app-config-row"]').evaluateAll((els) => els.map((el) => el.getAttribute('data-state')));
  expect(states).toEqual(['published', 'modified', 'added', 'deleted']);
  await expect(page.locator('[data-spec="app-config-pub-count"]')).toHaveText('3 项待发布');
});

test('Pending：草稿值在生效值下方以警告色展示', async ({ page }) => {
  await openStory(page, 'AppConfigTable/Pending');
  await expect(page.locator('[data-spec="app-config-draft"]')).toHaveCount(3); // modified + added + deleted
  await expect(page.locator('[data-spec="app-config-draft"]').first()).toContainText('草稿：');
});

test('Pending：查看差异展开明细，三行旧值→新值', async ({ page }) => {
  await openStory(page, 'AppConfigTable/Pending');
  await expect(page.locator('[data-spec="app-config-diff-list"]')).toHaveCount(0);
  await page.locator('[data-spec="app-config-diff"]').click();
  await expect(page.locator('[data-spec="app-config-diff-list"] > div')).toHaveCount(3);
});

test('Pending：右栏两条历史，当前版本带「当前」Tag，非当前发布历史带回滚按钮', async ({ page }) => {
  await openStory(page, 'AppConfigTable/Pending');
  await expect(page.locator('[data-spec="app-config-history-row"]')).toHaveCount(2);
  const currentTags = page.locator('[data-spec="app-config-history-row"]', { hasText: '当前' });
  await expect(currentTags).toHaveCount(1);
  await expect(page.locator('[data-spec="app-config-rollback"]')).toHaveCount(1); // v2 是草稿版本，v1 是当前
});

test('Clean：没有草稿，不显示待发布浮条，所有行已发布', async ({ page }) => {
  await openStory(page, 'AppConfigTable/Clean');
  await expect(page.locator('[data-spec="app-config-pubbar"]')).toHaveCount(0);
  const states = await page.locator('[data-spec="app-config-row"]').evaluateAll((els) => els.map((el) => el.getAttribute('data-state')));
  expect(states).toEqual(['published']);
});

test('Empty：配置存在但没有配置项与历史', async ({ page }) => {
  await openStory(page, 'AppConfigTable/Empty');
  await expect(page.locator('[data-spec="app-config-empty"]')).toContainText('该应用还没有配置项');
  await expect(page.locator('[data-spec="app-config-history-empty"]')).toContainText('还没有发布历史');
});

test('Failed：请求抛错显示加载失败，不渲染空表', async ({ page }) => {
  await openStory(page, 'AppConfigTable/Failed');
  await expect(page.locator('[data-spec="app-config-failed"]')).toContainText('配置加载失败');
  await expect(page.locator('[data-spec="app-config-row"]')).toHaveCount(0);
});
