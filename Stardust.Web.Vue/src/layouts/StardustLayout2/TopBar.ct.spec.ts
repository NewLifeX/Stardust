import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

/**
 * 挂载指定 story 并截图：
 *  - 深链 ?story=<id> → main.ts 在加载时挂载该故事（不显示侧栏，基线干净）。
 *  - 等待 .gal-preview 出现后截该容器（而非整页），保证基线只含组件本身。
 */
async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  // 等动画与字体稳定，避免偶发的模糊差异导致基线抖动
  await page.waitForTimeout(300);
}

test('TopBar 暗色主题 + 选中「此刻」', async ({ page }) => {
  await openStory(page, 'TopBar/DarkActiveNow');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('TopBar 暗色主题 + 选中「应用」', async ({ page }) => {
  await openStory(page, 'TopBar/DarkActiveApps');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('TopBar 亮色主题 + 选中「节点」', async ({ page }) => {
  await openStory(page, 'TopBar/LightActiveNodes');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('TopBar 带计数徽标', async ({ page }) => {
  await openStory(page, 'TopBar/DarkWithNowCount');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('TopBar 单项目形态', async ({ page }) => {
  await openStory(page, 'TopBar/SingleProject');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

// 语义断言：环境徽标常驻、搜索框带 ⌘K 提示、头像首字取自 initial
test('TopBar 常驻生产环境徽标与 ⌘K 提示', async ({ page }) => {
  await openStory(page, 'TopBar/DarkActiveNow');
  await expect(page.locator('[data-spec="env-tag"]')).toContainText('生产');
  await expect(page.locator('[data-spec="search-trigger"]')).toContainText('⌘K');
  await expect(page.locator('[data-spec="avatar"]')).toHaveText('笑');
});

test('TopBar 项目下拉列出全部项目', async ({ page }) => {
  await openStory(page, 'TopBar/DarkActiveNow');
  const options = page.locator('[data-spec="project-selector"] option');
  await expect(options).toHaveCount(2);
  await expect(options.nth(0)).toHaveText('stardust-prod');
  await expect(options.nth(1)).toHaveText('stardust-staging');
});
