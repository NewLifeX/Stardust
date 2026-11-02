import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

const DOT = '[data-spec="dot"]';

// 语义断言：尺寸（原型 .x-dot 7×7 圆）
test('Dot 宽高 7，各语气一致', async ({ page }) => {
  for (const id of ['Dot/Default', 'Dot/Ok', 'Dot/Err', 'Dot/Warn', 'Dot/Off']) {
    await openStory(page, id);
    await expect(page.locator(DOT), id).toHaveCSS('width', '7px');
    await expect(page.locator(DOT), id).toHaveCSS('height', '7px');
  }
});

// 语义断言：ok、err 带光晕，default、warn 没有
test('Dot ok 与 err 有光晕，default 与 warn 没有', async ({ page }) => {
  const shadowOf = async (id: string) => {
    await openStory(page, id);
    return page.locator(DOT).evaluate((el) => getComputedStyle(el).boxShadow);
  };
  expect(await shadowOf('Dot/Ok')).not.toBe('none');
  expect(await shadowOf('Dot/Err')).not.toBe('none');
  expect(await shadowOf('Dot/Default')).toBe('none');
  expect(await shadowOf('Dot/Warn')).toBe('none');
});

// 语义断言：off 是空心圈，1.5px 描边 + 透明底
test('Dot off 是空心圈：描边 1.5px、背景透明', async ({ page }) => {
  await openStory(page, 'Dot/Off');
  const dot = page.locator(DOT);
  // 样式声明是 1.5px（与原型一致），但 Chromium 在 1 倍像素比下把边框宽度取整为 1px，所以断言计算值 1px 与实线
  await expect(dot).toHaveCSS('border-top-width', '1px');
  await expect(dot).toHaveCSS('border-top-style', 'solid');
  await expect(dot).toHaveCSS('background-color', 'rgba(0, 0, 0, 0)');
});

// 语义断言：不同语气的底色互不相同（ok 绿、err 红、warn 黄、default 灰）
test('Dot 各语气底色互不相同', async ({ page }) => {
  const colors: string[] = [];
  for (const id of ['Dot/Default', 'Dot/Ok', 'Dot/Err', 'Dot/Warn']) {
    await openStory(page, id);
    colors.push(await page.locator(DOT).evaluate((el) => getComputedStyle(el).backgroundColor));
  }
  expect(new Set(colors).size).toBe(4);
});
