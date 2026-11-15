import { test, expect, type Page } from '@playwright/test';

// 桩语义注记：本文件按 URL/type 分支返回数据，是**前端契约侧写**；
// 真实后端 TraceHourStat/TraceMinuteStat 不支持 type 过滤（差异已记录于 Doc/用户旅程/监控调用链/TraceDayStat.md §9.1，待仲裁）。
const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('引导空态：未选埋点显示引导文案，埋点下拉含桩数据三项', async ({ page }) => {
  await openStory(page, 'TraceStatCard/WithData');
  await expect(page.locator('[data-spec="trace-stat-empty"]')).toContainText('请选择埋点');
  const item = page.locator('[data-spec="trace-stat-item"]');
  await expect(item.locator('option')).toHaveCount(3);
  await expect(item.locator('option').nth(1)).toContainText('StarServer/Home');
  // 未查询时无图表无表格
  await expect(page.locator('[data-spec="trace-stat-chart"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="trace-stat-row"]')).toHaveCount(0);
});

test('选埋点加载：单图+三行+错误率百分比口径（C13）+环比倍率口径+总计行手算', async ({ page }) => {
  await openStory(page, 'TraceStatCard/WithData');
  await page.locator('[data-spec="trace-stat-item"]').selectOption({ label: 'StarServer/Home（接口）' });
  await expect(page.locator('[data-spec="trace-stat-row"]')).toHaveCount(3);
  // 260px 单图
  await expect(page.locator('.e-chart canvas')).toHaveCount(1);
  // 错误率：后端小数 0.02/0.0045/0.0011 → 显示 2%/0.45%/0.11%（fmtErrorRate ×100 两位舍入无尾零）
  const rows = page.locator('[data-spec="trace-stat-row"]');
  await expect(rows.nth(0)).toContainText('2%');
  await expect(rows.nth(1)).toContainText('0.45%');
  await expect(rows.nth(2)).toContainText('0.11%');
  // 环比：倍率 1.2→+20%、0.75→-25%、0→—（fmtRingRate）
  await expect(rows.nth(0)).toContainText('+20%');
  await expect(rows.nth(1)).toContainText('-25%');
  await expect(rows.nth(2)).toContainText('—');
  // 总计行手算：Σtotal=1200+300+88=1588；Σerrors=27；rate=27/1588→1.7%；耗时均值 round((15+9+22)/3)=15；max=120；min=1
  // DOM 映射（label 列 colspan=3）：nth(0)=label nth(1)=total nth(2)=环比(—) nth(3)=errors nth(4)=错误率 nth(5)=cost nth(6)=max nth(7)=min
  const tds = page.locator('[data-spec="trace-stat-totalrow"] td');
  await expect(tds.nth(0)).toContainText('总计（已加载 3 行）');
  await expect(tds.nth(1)).toHaveText('1588');
  await expect(tds.nth(2)).toHaveText('—');
  await expect(tds.nth(3)).toHaveText('27');
  await expect(tds.nth(4)).toHaveText('1.7%');
  await expect(tds.nth(5)).toHaveText('15');
  await expect(tds.nth(6)).toHaveText('120');
  await expect(tds.nth(7)).toHaveText('1');
  // 错误率行底色：0.02≥0.01 danger、0.0045≥0.001 warning
  await expect(rows.nth(0)).toHaveClass(/bg-\[var\(--el-color-danger-light-9\)\]/);
  await expect(rows.nth(1)).toHaveClass(/bg-\[var\(--el-color-warning-light-9\)\]/);
  await expect(rows.nth(2)).not.toHaveClass(/bg-\[var\(--el-color-danger-light-9\)\]/);
});

test('粒度三态切换：点「每小时」后按钮选中态生效', async ({ page }) => {
  await openStory(page, 'TraceStatCard/WithData');
  await page.locator('[data-spec="trace-stat-item"]').selectOption({ label: 'StarServer/Home（接口）' });
  const hour = page.locator('[data-spec="trace-stat-gran-hour"]');
  await expect(page.locator('[data-spec="trace-stat-gran-day"]')).toHaveAttribute('aria-pressed', 'true');
  await hour.click();
  await expect(hour).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('[data-spec="trace-stat-gran-day"]')).toHaveAttribute('aria-pressed', 'false');
});

test('C17 列链 drill：种类标签+单日区间+天数下拉置自定义禁用（CARD-MON-07）', async ({ page }) => {
  await openStory(page, 'TraceStatCard/Drill');
  // drill prop（seq immediate）直接进入查询态：种类标签可清除
  await expect(page.locator('[data-spec="trace-stat-type-tag"]')).toContainText('接口');
  await expect(page.locator('[data-spec="trace-stat-type-clear"]')).toBeVisible();
  await expect(page.locator('[data-spec="trace-stat-range-start"]')).toHaveValue('2026-11-03');
  await expect(page.locator('[data-spec="trace-stat-range-end"]')).toHaveValue('2026-11-03');
  await expect(page.locator('[data-spec="trace-stat-days"]')).toBeDisabled();
  // 查询态不显示引导空态
  await expect(page.locator('[data-spec="trace-stat-empty"]')).toHaveCount(0);
  // 主体描述含种类与日期
  await expect(page.locator('[data-spec="trace-stat"]')).toContainText('接口 · 2026-11-03 ~ 2026-11-03');
});

test('失败态：选埋点后统计加载失败显示失败文案与重试（统计桩 throw，埋点桩成功）', async ({ page }) => {
  await openStory(page, 'TraceStatCard/Failed');
  // 埋点下拉桩成功一项；选埋点触发统计加载（throw）→ 失败态，不伪装成空数据
  await page.locator('[data-spec="trace-stat-item"]').selectOption({ label: 'StarServer/Home（接口）' });
  await expect(page.locator('[data-spec="trace-stat-failed"]')).toContainText('埋点统计加载失败');
  await expect(page.locator('[data-spec="trace-stat-failed"]')).toContainText('stub 500');
  await expect(page.locator('[data-spec="trace-stat-retryload"]')).toBeVisible();
});
