import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

const ITEM = '[data-spec="attention-item"]';
const DOT = '[data-spec="dot"]';
const DETAIL = '[data-spec="attention-detail"]';
const PENDING = '[data-spec="attention-pending"]';
const GO = '[data-spec="attention-go"]';

const tonesOf = (page: Page) => page.locator(ITEM).evaluateAll((els) => els.map((el) => el.querySelector('[data-spec="dot"]')?.getAttribute('data-tone')));

test('AttentionList 混合：固定四项次序，每行一个跳转按钮，没有待接入标签', async ({ page }) => {
  await openStory(page, 'AttentionList/Mixed');
  const keys = await page.locator(ITEM).evaluateAll((els) => els.map((el) => el.getAttribute('data-key')));
  expect(keys).toEqual(['versions', 'pipeline', 'history', 'instances']);
  await expect(page.locator(GO)).toHaveText(['去节点', '看流水线', '看部署历史', '看实例']);
  await expect(page.locator(PENDING)).toHaveCount(0);
});

// 语义断言：warn 用黄点和警告色详情，正常项用次要文字色，两者必须看得出区别
test('AttentionList 混合：警告项的圆点语气与详情颜色和正常项不同', async ({ page }) => {
  await openStory(page, 'AttentionList/Mixed');
  await expect(page.locator(ITEM).nth(0).locator(DOT)).toHaveAttribute('data-tone', 'warn');
  await expect(page.locator(ITEM).nth(1).locator(DOT)).toHaveAttribute('data-tone', 'ok');
  const colorOf = (n: number) => page.locator(ITEM).nth(n).locator(DETAIL).evaluate((el) => getComputedStyle(el).color);
  const warn = await colorOf(0);
  const normal = await colorOf(1);
  expect(warn).not.toBe(normal);
});

// 取不到数据不当作正常，也不写成没有记录：空心圈加「待接入」标签
test('AttentionList 全部待接入：四个空心圈加四个待接入标签', async ({ page }) => {
  await openStory(page, 'AttentionList/AllPending');
  expect(await tonesOf(page)).toEqual(['off', 'off', 'off', 'off']);
  await expect(page.locator(PENDING)).toHaveCount(4);
  await expect(page.locator(PENDING).first()).toHaveText('待接入');
  await expect(page.locator(DETAIL).first()).toContainText('没有部署集');
});

test('AttentionList 全部正常：圆点语气依次 ok、ok、ok、default，只有最后一行没有下边线', async ({ page }) => {
  await openStory(page, 'AttentionList/AllOk');
  expect(await tonesOf(page)).toEqual(['ok', 'ok', 'ok', 'default']);
  const borderOf = (n: number) => page.locator(ITEM).nth(n).evaluate((el) => getComputedStyle(el).borderBottomWidth);
  expect(await borderOf(0)).toBe('1px');
  expect(await borderOf(3)).toBe('0px');
});

// 点击跳转：GoRecorder 这个 story 的 onGo 把收到的页签追加进 window.__attentionGo，每点一个按钮就读一次，断言发出的是对应页签
test('AttentionList 点击跳转：四个按钮依次发出 nodes、pipeline、history、instances', async ({ page }) => {
  await openStory(page, 'AttentionList/GoRecorder');
  await expect(page.locator(GO)).toHaveCount(4);
  const expected = ['nodes', 'pipeline', 'history', 'instances'];
  for (let i = 0; i < expected.length; i++) {
    await page.locator(GO).nth(i).click();
    const got = await page.evaluate(() => (window as unknown as { __attentionGo?: string[] }).__attentionGo ?? []);
    expect(got).toEqual(expected.slice(0, i + 1));
  }
});
