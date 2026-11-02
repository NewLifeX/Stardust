import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('Mixed：四行，级别与状态 data 属性正确', async ({ page }) => {
  await openStory(page, 'AlarmsList/Mixed');
  const rows = page.locator('[data-spec="alarm-row"]');
  await expect(rows).toHaveCount(4);
  await expect(rows.nth(0)).toHaveAttribute('data-level', 'critical');
  await expect(rows.nth(0)).toHaveAttribute('data-status', 'active');
  await expect(rows.nth(3)).toHaveAttribute('data-status', 'resolved');
});

test('Mixed：统计条显示当前 Critical=1 / Warning=1', async ({ page }) => {
  await openStory(page, 'AlarmsList/Mixed');
  await expect(page.locator('[data-spec="alarms-stats"]')).toContainText('当前 1 Critical');
  await expect(page.locator('[data-spec="alarms-stats"]')).toContainText('当前 1 Warning');
});

test('Mixed：第一行显示 Critical 标签与未处理 Tag', async ({ page }) => {
  await openStory(page, 'AlarmsList/Mixed');
  const first = page.locator('[data-spec="alarm-row"]').first();
  await expect(first).toContainText('node-b 已下线');
  await expect(first).toContainText('未处理');
});

test('Empty：没有告警时显示空态文案，不渲染表格', async ({ page }) => {
  await openStory(page, 'AlarmsList/Empty');
  await expect(page.locator('[data-spec="alarms-empty"]')).toContainText('没有告警');
  await expect(page.locator('[data-spec="alarm-row"]')).toHaveCount(0);
});

test('Loading：spinner 加载中，不渲染空表', async ({ page }) => {
  await openStory(page, 'AlarmsList/Loading');
  await expect(page.locator('[data-spec="alarms-loading"]')).toContainText('告警加载中');
  await expect(page.locator('[data-spec="alarm-row"]')).toHaveCount(0);
});

test('Failed：请求失败显示可见错误与重试按钮，不当作空态', async ({ page }) => {
  await openStory(page, 'AlarmsList/Failed');
  await expect(page.locator('[data-spec="alarms-failed"]')).toContainText('告警加载失败');
  await expect(page.locator('[data-spec="alarms-retry"]')).toContainText('重试');
  await expect(page.locator('[data-spec="alarms-empty"]')).toHaveCount(0);
});

test('过滤：级别下拉选 Critical 后只剩 1 行', async ({ page }) => {
  await openStory(page, 'AlarmsList/Mixed');
  await page.locator('[data-spec="alarms-filter-level"]').selectOption('critical');
  await expect(page.locator('[data-spec="alarm-row"]')).toHaveCount(1);
  await expect(page.locator('[data-spec="alarm-row"]')).toContainText('node-b 已下线');
});

test('过滤：搜索框输入 orderservice 后只剩 1 行', async ({ page }) => {
  await openStory(page, 'AlarmsList/Mixed');
  await page.locator('[data-spec="alarms-filter-q"]').fill('orderservice');
  await expect(page.locator('[data-spec="alarm-row"]')).toHaveCount(1);
  await expect(page.locator('[data-spec="alarm-row"]')).toContainText('OrderService');
});

test('展开行：点标题行展开详情，显示 Content；再点收起', async ({ page }) => {
  await openStory(page, 'AlarmsList/Mixed');
  await expect(page.locator('[data-spec="alarm-detail"]')).toHaveCount(0);
  await page.locator('[data-spec="alarm-row"]').nth(1).click();
  await expect(page.locator('[data-spec="alarm-detail"]')).toHaveCount(1);
  await expect(page.locator('[data-spec="alarm-content"]')).toContainText('错误率 12%');
  await page.locator('[data-spec="alarm-row"]').nth(1).click();
  await expect(page.locator('[data-spec="alarm-detail"]')).toHaveCount(0);
});

test('Recorder：点重试按钮把 1 写进 window.__alarmRetry', async ({ page }) => {
  await openStory(page, 'AlarmsList/Recorder');
  await page.locator('[data-spec="alarms-retry"]').click();
  const picked = await page.evaluate(() => (window as unknown as { __alarmRetry?: number[] }).__alarmRetry);
  expect(picked).toEqual([1]);
});
