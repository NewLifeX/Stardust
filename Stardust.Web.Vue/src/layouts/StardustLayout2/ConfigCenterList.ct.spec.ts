import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('Mixed：四行（含全局 Common），待发布行在最前带橙色 Tag，已发布绿色', async ({ page }) => {
  await openStory(page, 'ConfigCenterList/Mixed');
  const rows = page.locator('[data-spec="config-row"]');
  await expect(rows).toHaveCount(4);
  await expect(rows.first()).toContainText('订单服务');
  await expect(rows.first()).toContainText('待发布');
  // 后三行是已发布
  await expect(page.locator('[data-spec="config-row"]').nth(1)).toContainText('已发布');
});

test('Mixed：工具条显示总数，Key 数与最近更新列存在', async ({ page }) => {
  await openStory(page, 'ConfigCenterList/Mixed');
  await expect(page.locator('[data-spec="config-center"]')).toContainText('4 条');
  await expect(page.locator('[data-spec="config-row"]').first()).toContainText('12');
});

test('Empty：没有配置时显示空态文案', async ({ page }) => {
  await openStory(page, 'ConfigCenterList/Empty');
  await expect(page.locator('[data-spec="config-empty"]')).toContainText('还没有应用配置');
  await expect(page.locator('[data-spec="config-row"]')).toHaveCount(0);
});

test('Loading：spinner 加载中，不渲染表格', async ({ page }) => {
  await openStory(page, 'ConfigCenterList/Loading');
  await expect(page.locator('[data-spec="config-loading"]')).toContainText('配置加载中');
  await expect(page.locator('[data-spec="config-row"]')).toHaveCount(0);
});

test('Failed：请求失败显示可见错误 + 重试按钮', async ({ page }) => {
  await openStory(page, 'ConfigCenterList/Failed');
  await expect(page.locator('[data-spec="config-failed"]')).toContainText('配置加载失败');
  await expect(page.locator('[data-spec="config-retry"]')).toContainText('重试');
});

test('OpenRecorder：点行把 configId 写进 window.__configOpen', async ({ page }) => {
  await openStory(page, 'ConfigCenterList/OpenRecorder');
  await page.locator('[data-spec="config-row"]').nth(1).click();
  const picked = await page.evaluate(() => (window as unknown as { __configOpen?: number[] }).__configOpen);
  expect(picked).toEqual([8]);
});

test('Filters：类别下拉首项「全部类别」，选「物联网」只留匹配行，清除恢复全量', async ({ page }) => {
  await openStory(page, 'ConfigCenterList/Filters');
  // 首项「全部类别」+ 选项来自 categories（公共/物联网）
  const cat = page.locator('[data-spec="config-category-filter"]');
  await expect(cat.locator('option')).toHaveCount(3);
  await expect(cat.locator('option').first()).toHaveText('全部类别');
  // 全量四行
  await expect(page.locator('[data-spec="config-row"]')).toHaveCount(4);
  // 选「物联网」→ 只剩 IotWeb 行
  await cat.selectOption({ label: '物联网' });
  const rows = page.locator('[data-spec="config-row"]');
  await expect(rows).toHaveCount(1);
  await expect(rows.first()).toContainText('IotWeb');
  // 清除筛选 → 恢复全量
  await page.locator('[data-spec="config-filter-clear"]').click();
  await expect(page.locator('[data-spec="config-row"]')).toHaveCount(4);
});

test('Filters：应用下拉不含全局配置（AppId=0 不进选项），选应用+关键字双条件过滤（DEF-1）', async ({ page }) => {
  await openStory(page, 'ConfigCenterList/Filters');
  // 应用下拉 = 首项「全部应用」+ 3 个真实应用；AppId=0 的 Common 不进下拉
  const app = page.locator('[data-spec="config-app-filter"]');
  await expect(app.locator('option')).toHaveCount(4);
  await expect(app.locator('option').first()).toHaveText('全部应用');
  const appTexts = await app.locator('option').allTextContents();
  expect(appTexts).not.toContain('Common');
  // 双条件：关键字「订单」+ 应用「IotWeb」→ 交集为空
  await page.locator('[data-spec="config-search"]').fill('订单');
  await expect(page.locator('[data-spec="config-row"]')).toHaveCount(1);
  await app.selectOption({ label: 'IotWeb' });
  await expect(page.locator('[data-spec="config-row"]')).toHaveCount(0);
  // 清除筛选 → 恢复全量
  await page.locator('[data-spec="config-filter-clear"]').click();
  await expect(page.locator('[data-spec="config-row"]')).toHaveCount(4);
});

test('Filters：日期区间筛「最近更新」，区间外行被滤掉，清除恢复全量', async ({ page }) => {
  await openStory(page, 'ConfigCenterList/Filters');
  // 2026-10-27 ~ 2026-10-28：命中订单服务(10-28)与 IotWeb(10-27)，滤掉支付网关(10-20)
  await page.locator('[data-spec="config-date-start"]').fill('2026-10-27');
  await page.locator('[data-spec="config-date-end"]').fill('2026-10-28');
  const rows = page.locator('[data-spec="config-row"]');
  await expect(rows).toHaveCount(2);
  await expect(rows.first()).toContainText('订单服务');
  // 改为 10-28 单日 → 只剩订单服务
  await page.locator('[data-spec="config-date-start"]').fill('2026-10-28');
  await expect(rows).toHaveCount(1);
  await expect(rows.first()).toContainText('订单服务');
  // 清除筛选 → 恢复全量四行
  await page.locator('[data-spec="config-filter-clear"]').click();
  await expect(page.locator('[data-spec="config-row"]')).toHaveCount(4);
});
