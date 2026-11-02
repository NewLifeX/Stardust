import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

const CHIP = '[data-spec="chip"]';

// 语义断言：高 26（原型 .x-chip height 26）
test('Chip 高 26', async ({ page }) => {
  await openStory(page, 'Chip/Off');
  await expect(page.locator(CHIP)).toHaveCSS('height', '26px');
});

// 语义断言：选中态通过 aria-pressed 暴露给读屏与测试
test('Chip 未选中 aria-pressed=false，选中 aria-pressed=true', async ({ page }) => {
  await openStory(page, 'Chip/Off');
  await expect(page.locator(CHIP)).toHaveAttribute('aria-pressed', 'false');
  await openStory(page, 'Chip/On');
  await expect(page.locator(CHIP)).toHaveAttribute('aria-pressed', 'true');
});

// 语义断言：选中态的文字色、描边色、底色都与未选中不同
test('Chip 选中态的文字、描边、底色都与未选中不同', async ({ page }) => {
  const styleOf = async (id: string) => {
    await openStory(page, id);
    return page.locator(CHIP).evaluate((el) => {
      const s = getComputedStyle(el);
      return { color: s.color, border: s.borderTopColor, bg: s.backgroundColor };
    });
  };
  const off = await styleOf('Chip/Off');
  const on = await styleOf('Chip/On');
  expect(on.color).not.toBe(off.color);
  expect(on.border).not.toBe(off.border);
  expect(on.bg).not.toBe(off.bg);
});

// 语义断言：未选中悬停后文字色变化（原型 .x-chip:hover color fg-1）
test('Chip 未选中悬停后文字色变化', async ({ page }) => {
  await openStory(page, 'Chip/Off');
  const chip = page.locator(CHIP);
  const before = await chip.evaluate((el) => getComputedStyle(el).color);
  await chip.hover();
  await page.waitForTimeout(250);
  const after = await chip.evaluate((el) => getComputedStyle(el).color);
  expect(after).not.toBe(before);
});
