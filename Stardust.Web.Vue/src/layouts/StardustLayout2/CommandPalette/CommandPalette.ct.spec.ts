import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('CommandPalette 三个分组共 5 项', async ({ page }) => {
  await openStory(page, 'CommandPalette/Groups');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('CommandPalette 单分组单项', async ({ page }) => {
  await openStory(page, 'CommandPalette/Single');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('CommandPalette 空分组', async ({ page }) => {
  await openStory(page, 'CommandPalette/Empty');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

// 语义断言：输入框占位符取自 config.placeholder，首项默认选中
test('CommandPalette 占位符取自 config', async ({ page }) => {
  await openStory(page, 'CommandPalette/Groups');
  await expect(page.locator('[data-spec="palette-input"]')).toHaveAttribute(
    'placeholder',
    '搜索应用、节点、TraceId,或执行操作…',
  );
});

test('CommandPalette 首项默认选中', async ({ page }) => {
  await openStory(page, 'CommandPalette/Groups');
  const selected = page.locator('.gal-preview [aria-selected="true"]');
  await expect(selected).toHaveCount(1);
  await expect(selected).toContainText('跳转 · 此刻');
});

test('CommandPalette 输入过滤后仅留命中项', async ({ page }) => {
  await openStory(page, 'CommandPalette/Groups');
  await page.locator('[data-spec="palette-input"]').fill('OrderService');
  await page.waitForTimeout(200);
  const items = page.locator('.gal-preview [role="option"]');
  await expect(items).toHaveCount(1);
  await expect(items.first()).toContainText('OrderService');
});

// ---------- 追踪组（2026-11-16 输入2）：输入像 TraceId 时追加「链路追踪」组，回车跳 /trace ----------

test('CommandPalette TraceId 输入追加链路追踪组', async ({ page }) => {
  await openStory(page, 'CommandPalette/Trace');
  await page.locator('[data-spec="palette-input"]').fill('0abc20d417947901750510000dcefca2');
  await page.waitForTimeout(200);
  const items = page.locator('.gal-preview [role="option"]');
  // 页面组被 id 串过滤空，只剩追踪一项
  await expect(items).toHaveCount(1);
  await expect(items.first()).toContainText('追踪 · 0abc20d41794790175051000…');
  await expect(items.first()).toContainText('打开链路详情');
});

test('CommandPalette TraceId 回车执行追踪跳转（goTrace 捕获完整 id）', async ({ page }) => {
  await openStory(page, 'CommandPalette/Trace');
  const input = page.locator('[data-spec="palette-input"]');
  await input.fill('0abc20d417947901750510000dcefca2');
  await input.press('Enter');
  const trace = await page.evaluate(() => (window as unknown as { __paletteTrace?: string }).__paletteTrace);
  expect(trace).toBe('0abc20d417947901750510000dcefca2');
});

test('CommandPalette 中文与点分 IP 不追加追踪组', async ({ page }) => {
  await openStory(page, 'CommandPalette/Trace');
  const input = page.locator('[data-spec="palette-input"]');
  await input.fill('星尘');
  await page.waitForTimeout(200);
  await expect(page.locator('.gal-preview')).toContainText('没有结果');
  await input.fill('192.168.1.1');
  await page.waitForTimeout(200);
  await expect(page.locator('.gal-preview')).toContainText('没有结果');
});
