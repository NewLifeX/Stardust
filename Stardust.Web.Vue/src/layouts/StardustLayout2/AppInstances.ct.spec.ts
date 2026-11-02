import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('ThreeRows：三行实例，节点名+mono IP，CPU/内存列都是待接入', async ({ page }) => {
  await openStory(page, 'AppInstances/ThreeRows');
  await expect(page.locator('[data-spec="app-instances-row"]')).toHaveCount(3);
  await expect(page.locator('[data-spec="app-instances-row"]').first()).toContainText('node-a');
  await expect(page.locator('[data-spec="app-instances-row"]').first()).toContainText('10.0.0.1');
  // CPU/内存两列各三个待接入 Tag
  await expect(page.locator('[data-spec="app-instances-row"]').locator('[data-spec="tag"]')).toHaveCount(6);
});

test('Empty：整行显示暂无在线实例', async ({ page }) => {
  await openStory(page, 'AppInstances/Empty');
  await expect(page.locator('[data-spec="app-instances-empty"]')).toContainText('暂无在线实例');
  await expect(page.locator('[data-spec="app-instances-row"]')).toHaveCount(0);
});

test('Failed：请求抛错显示加载失败', async ({ page }) => {
  await openStory(page, 'AppInstances/Failed');
  await expect(page.locator('[data-spec="app-instances-failed"]')).toContainText('实例加载失败');
});

test('EmitRecorder：行点击发 select，hover 浮出的重启/终端按钮发对应事件', async ({ page }) => {
  await openStory(page, 'AppInstances/EmitRecorder');
  await page.evaluate(() => {
    (window as unknown as { __instEvents?: Array<[string, number]> }).__instEvents = [];
  });
  const row = page.locator('[data-spec="app-instances-row"]').nth(0);
  await row.click();
  const ev1 = await page.evaluate(() => (window as unknown as { __instEvents?: Array<[string, number]> }).__instEvents);
  expect(ev1).toEqual([['select', 11]]);

  // hover 浮出操作按钮后点击重启与终端
  await row.hover();
  await row.locator('[data-spec="app-instances-restart"]').click();
  await row.locator('[data-spec="app-instances-terminal"]').click();
  const ev2 = await page.evaluate(() => (window as unknown as { __instEvents?: Array<[string, number]> }).__instEvents);
  expect(ev2).toEqual([['select', 11], ['restart', 11], ['terminal', 11]]);
});
