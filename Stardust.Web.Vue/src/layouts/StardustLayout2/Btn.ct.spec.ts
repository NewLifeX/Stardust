import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

const BTN = '[data-spec="btn"]';

// 视觉用例：每个变体一张基线图
const VISUAL: Array<[string, string]> = [
  ['Btn 默认变体', 'Btn/Default'],
  ['Btn primary', 'Btn/Primary'],
  ['Btn danger', 'Btn/Danger'],
  ['Btn ghost', 'Btn/Ghost'],
  ['Btn sm 尺寸', 'Btn/Small'],
  ['Btn 禁用', 'Btn/Disabled'],
  ['Btn 带图标', 'Btn/WithIcon'],
];

for (const [name, id] of VISUAL) {
  test(name, async ({ page }) => {
    await openStory(page, id);
    await expect(page.locator('.gal-preview')).toHaveScreenshot();
  });
}

// 语义断言：尺寸（原型 .x-btn 高 32，.x-btn.sm 高 26）
test('Btn md 高 32，sm 高 26', async ({ page }) => {
  await openStory(page, 'Btn/Default');
  await expect(page.locator(BTN)).toHaveCSS('height', '32px');

  await openStory(page, 'Btn/Small');
  await expect(page.locator(BTN)).toHaveCSS('height', '26px');
});

// 语义断言：图标在文字前，与文字间距 6
test('Btn 带图标时 svg 在文字前，间距 6', async ({ page }) => {
  await openStory(page, 'Btn/WithIcon');
  const btn = page.locator(BTN);
  await expect(btn.locator('> svg')).toHaveCount(1);
  await expect(btn).toHaveCSS('column-gap', '6px');
  const firstTag = await btn.evaluate((el) => el.firstElementChild?.tagName.toLowerCase());
  expect(firstTag).toBe('svg');
});

test('Btn 没传图标时没有 svg', async ({ page }) => {
  await openStory(page, 'Btn/Default');
  await expect(page.locator(BTN).locator('> svg')).toHaveCount(0);
});

// 语义断言：禁用沿用原型，透明度 0.4 且不响应指针
test('Btn 禁用：透明度 0.4，不响应指针', async ({ page }) => {
  await openStory(page, 'Btn/Disabled');
  const btn = page.locator(BTN);
  await expect(btn).toBeDisabled();
  await expect(btn).toHaveCSS('opacity', '0.4');
  await expect(btn).toHaveCSS('pointer-events', 'none');
});

// 语义断言：primary 悬停后背景变化（只验证「变了」，不验证具体色值，色值偏差见 TODO2.md）
test('Btn primary 悬停后背景色变化', async ({ page }) => {
  await openStory(page, 'Btn/Primary');
  const btn = page.locator(BTN);
  const before = await btn.evaluate((el) => getComputedStyle(el).backgroundColor);
  await btn.hover();
  // 过渡 120ms，等够再取
  await page.waitForTimeout(250);
  const after = await btn.evaluate((el) => getComputedStyle(el).backgroundColor);
  expect(after).not.toBe(before);
});
