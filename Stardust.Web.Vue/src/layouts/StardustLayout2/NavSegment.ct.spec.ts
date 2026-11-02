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

test('NavSegment 未选中任何项', async ({ page }) => {
  await openStory(page, 'NavSegment/None');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('NavSegment 选中「此刻」', async ({ page }) => {
  await openStory(page, 'NavSegment/ActiveNow');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('NavSegment 选中「应用」', async ({ page }) => {
  await openStory(page, 'NavSegment/ActiveApps');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('NavSegment 选中「治理」', async ({ page }) => {
  await openStory(page, 'NavSegment/ActiveMesh');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('NavSegment 显示计数徽标', async ({ page }) => {
  await openStory(page, 'NavSegment/ActiveNowWithCount');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('NavSegment 计数为 0 时不显示徽标', async ({ page }) => {
  await openStory(page, 'NavSegment/ActiveNowZeroCount');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

// 交互态：高亮项带 aria-current="page"，点击抛 go 事件（这里只断言 DOM 语义变化）
test('NavSegment 高亮项带 aria-current=page', async ({ page }) => {
  await openStory(page, 'NavSegment/ActiveNow');
  await expect(page.locator('[data-spec="nav-now"]')).toHaveAttribute('aria-current', 'page');
  await expect(page.locator('[data-spec="nav-apps"]')).not.toHaveAttribute('aria-current', 'page');
});
