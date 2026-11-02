import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('F14 单链路：瀑布两片段（失败片段红条）+ 诊断框，无多值标注', async ({ page }) => {
  await openStory(page, 'TraceDetail/Single');
  const unit = page.locator('[data-spec="trace-unit"]');
  await expect(unit).toHaveCount(1);
  await expect(unit).toContainText('GET /order');
  await expect(unit.locator('[data-spec="trace-waterfall"] .absolute')).toHaveCount(2);
  await expect(unit).toContainText('1 个失败');
  await expect(unit).toContainText('最耗时片段「GET /order」500ms');
  await expect(page.locator('[data-spec="trace-multi-note"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="trace-unit-id"]')).toContainText('405');
});

test('F14 多值：两条链路并列 + 部分失败提示 + 诚实标注', async ({ page }) => {
  await openStory(page, 'TraceDetail/Multi');
  await expect(page.locator('[data-spec="trace-unit"]')).toHaveCount(2);
  await expect(page.locator('[data-spec="trace-multi-note"]')).toContainText('多值并列展示');
  await expect(page.locator('[data-spec="trace-detail-error"]')).toContainText('部分链路片段拉取失败');
  // 失败的那条给出失败态，成功的照常渲染（不互相吞掉）
  await expect(page.locator('[data-spec="trace-unit-failed"]')).toContainText('该链路片段拉取失败');
  await expect(page.locator('[data-spec="trace-unit"]').first()).toContainText('GET /order');
});

test('F14 输入框多值查询（无路由环境本地查询）', async ({ page }) => {
  await openStory(page, 'TraceDetail/Single');
  await page.locator('[data-spec="trace-input"]').fill('405, 369299');
  await page.locator('[data-spec="trace-search-btn"]').click();
  await expect(page.locator('[data-spec="trace-unit"]')).toHaveCount(2);
  await expect(page.locator('[data-spec="trace-multi-note"]')).toContainText('2 条链路各自独立时间轴');
  await expect(page.locator('[data-spec="trace-back"]')).toBeVisible();
});

test('F14-1 多值逐条请求：/trace/a,b 语义=两次单 id 请求（不整串直传）', async ({ page }) => {
  await openStory(page, 'TraceDetail/Multi');
  await expect(page.locator('[data-spec="trace-unit"]')).toHaveCount(2);
  const calls = await page.evaluate(() => (window as unknown as { __traceCalls?: string[] }).__traceCalls ?? []);
  expect(calls).toEqual(['405', 'bad']);
});
