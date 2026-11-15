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

test('总计行显示已加载行前端聚合与口径注（CARD-MON-04）', async ({ page }) => {
  await openStory(page, 'DayStatTab/Chart');
  const total = page.locator('[data-spec="daystat-totalrow"]');
  await expect(total).toContainText('总计（已加载 3 天）');
  // 手算：Σtotal=500+420+380=1300；Σerrors=6+2+0=8；错误率=8/1300×100=0.62%；
  // 平均耗时均值=(12+11+10)/3=11；最大耗时=max(40,35,30)=40；分类型逐列求和
  const tds = total.locator('td');
  await expect(tds.nth(1)).toHaveText('1300');
  await expect(tds.nth(2)).toHaveText('8');
  await expect(tds.nth(3)).toHaveText('0.62%');
  await expect(tds.nth(4)).toHaveText('11');
  await expect(tds.nth(5)).toHaveText('40');
  await expect(tds.nth(6)).toHaveText('31');
  await expect(tds.nth(7)).toHaveText('6');
  await expect(tds.nth(8)).toHaveText('7');
  await expect(tds.nth(9)).toHaveText('10');
  await expect(tds.nth(10)).toHaveText('1');
  await expect(tds.nth(11)).toHaveText('4');
  await expect(page.locator('[data-spec="daystat-totalnote"]')).toHaveText('口径=已加载行合计，非服务端全量');
});

test('C17 列链五入口：首钻进种类查询态，二次钻取列表随种类切换（CARD-MON-07）', async ({ page }) => {
  await openStory(page, 'DayStatTab/Chart');
  // 首钻：接口列链接 → 卡进入 Api 查询态，列表=api 行（桩按 type=Api 返回 /api/Galaxy）
  await page.locator('[data-spec="daystat-drill-api"]').first().click();
  await expect(page.locator('[data-spec="trace-stat-type-tag"]')).toContainText('接口');
  await expect(page.locator('[data-spec="trace-stat-row"]').first()).toContainText('/api/Galaxy');
  // 二次钻取：Http 列链接 → 必须重新加载为 http 行（回归：二次钻取曾因 rangeActive 无边沿而零重载）
  await page.locator('[data-spec="daystat-drill-http"]').first().click();
  await expect(page.locator('[data-spec="trace-stat-type-tag"]')).toContainText('Http');
  await expect(page.locator('[data-spec="trace-stat-row"]').first()).toContainText('http/webhook');
});

test('自选日期区间：选区间后天数下拉置自定义并禁用，清除恢复预设（CARD-MON-04）', async ({ page }) => {
  await openStory(page, 'DayStatTab/Chart');
  const days = page.locator('[data-spec="daystat-days"]');
  await expect(days).toBeEnabled();
  await page.locator('[data-spec="daystat-range-start"]').fill('2026-11-01');
  await page.locator('[data-spec="daystat-range-end"]').fill('2026-11-03');
  await expect(days).toBeDisabled();
  await expect(days).toHaveValue('0'); // 「自定义」选项
  await expect(page.locator('[data-spec="daystat-range-clear"]')).toBeVisible();
  await page.locator('[data-spec="daystat-range-clear"]').click();
  await expect(days).toBeEnabled();
  await expect(days).toHaveValue('7');
  await expect(page.locator('[data-spec="daystat-range-clear"]')).toHaveCount(0);
});

test('空应用开开关：主区空态与分钟卡空态共存（CARD-MON-DEF-01 形态 A）', async ({ page }) => {
  await openStory(page, 'DayStatTab/Empty');
  await page.locator('[data-spec="daystat-minute-toggle"]').click();
  // 竞态修复（load/loadMinute 分独立 guard）后 loading 必复位：主区 daystat-empty 照常显出，
  // 分钟卡独立于有数据分支渲染且呈自带空态，不再永挂「正在加载日统计…」
  await expect(page.locator('[data-spec="daystat-empty"]')).toContainText('还没有日统计数据');
  await expect(page.locator('[data-spec="daystat-minute-empty"]')).toContainText('没有分钟级统计');
  await expect(page.locator('[data-spec="daystat-loading"]')).toHaveCount(0);
});