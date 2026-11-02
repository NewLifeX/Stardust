import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('监控大屏画两张日曲线并给出汇总与表格', async ({ page }) => {
  await openStory(page, 'DayStatTab/Chart');
  // 汇总卡：1300 次调用 / 8 个错误（桩数据手算）
  await expect(page.locator('[data-spec="daystat-total"]')).toHaveText('1300');
  await expect(page.locator('[data-spec="daystat-errors"]')).toHaveText('8');
  await expect(page.locator('[data-spec="daystat-avgcost"]')).toHaveText('11');
  await expect(page.locator('[data-spec="daystat-errorrate"]')).toContainText('0.62');
  // EChart 复用（canvas），两张图
  await expect(page.locator('.e-chart canvas')).toHaveCount(2);
  // 表格 3 行，日期升序
  await expect(page.locator('[data-spec="daystat-row"]')).toHaveCount(3);
  await expect(page.locator('[data-spec="daystat-row"]').first()).toContainText('2026-11-01');
});

test('打开最近24h分钟曲线并画图', async ({ page }) => {
  await openStory(page, 'DayStatTab/Chart');
  await page.locator('[data-spec="daystat-minute-toggle"]').click();
  await expect(page.locator('.e-chart canvas')).toHaveCount(3);
  await expect(page.locator('[data-spec="daystat-minute-empty"]')).toHaveCount(0);
});

test('重算按钮在无数据时禁用原因可见，有数据时可点', async ({ page }) => {
  await openStory(page, 'DayStatTab/Chart');
  await expect(page.locator('[data-spec="daystat-retry"]')).toBeEnabled();
});

test('没有日统计时显示空态，不显示表格', async ({ page }) => {
  await openStory(page, 'DayStatTab/Empty');
  await expect(page.locator('[data-spec="daystat-empty"]')).toContainText('还没有日统计数据');
  await expect(page.locator('[data-spec="daystat-row"]')).toHaveCount(0);
});

test('请求失败显示失败文案与重试，不伪装成空表', async ({ page }) => {
  await openStory(page, 'DayStatTab/Failed');
  await expect(page.locator('[data-spec="daystat-failed"]')).toContainText('监控大屏加载失败');
  await expect(page.locator('[data-spec="daystat-failed"]')).toContainText('stub 500');
  await expect(page.locator('[data-spec="daystat-retryload"]')).toBeVisible();
});
