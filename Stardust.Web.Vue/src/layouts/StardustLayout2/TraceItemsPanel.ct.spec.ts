import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

// 手算基准（story 桩 3 行，映射口径与 traceItem.logic.test.ts 一致，Id Int32 → string）：
//   #11 Http  GET /api/user     enable=true  total=1064  errors=2
//   #12 Sql   User.FindAll      enable=false total=385364 errors=1497（错误数红）
//   #13 Redis redis://cache/get enable=null  total=9932  errors=0

test('表格数据态：3 行 + 批量条 + 类别下拉词典 + 启用 Tag 三态', async ({ page }) => {
  await openStory(page, 'TraceItemsPanel/WithData');
  await expect(page.locator('[data-spec="trace-item-panel"]')).toBeVisible();
  await expect(page.locator('[data-spec="trace-item-subtitle"]')).toContainText('应用埋点聚合口径');
  await expect(page.locator('[data-spec="trace-item-subtitle"]')).toContainText('删除会级联清理每日统计');
  const rows = page.locator('[data-spec="trace-item-row"]');
  await expect(rows).toHaveCount(3);
  await expect(rows.nth(0)).toContainText('GET /api/user');
  await expect(rows.nth(0)).toContainText('Http远程请求'.slice(0, 4));
  // 批量条初始 0 项
  await expect(page.locator('[data-spec="trace-item-batch-count"]')).toContainText('选中 0 项');
  // 批量按钮在 0 项时禁用
  await expect(page.locator('[data-spec="trace-item-batch-enable"]')).toBeDisabled();
  await expect(page.locator('[data-spec="trace-item-batch-fix"]')).toBeDisabled();
  // 类别下拉含词典项（GetKinds 桩）
  await expect(page.locator('[data-spec="trace-item-kind"] option').nth(1)).toHaveText('Http远程请求');
  // 勾选一行 → 批量计数与启用状态联动
  await page.locator('[data-spec="trace-item-sel-11"]').check();
  await expect(page.locator('[data-spec="trace-item-batch-count"]')).toContainText('选中 1 项');
  await expect(page.locator('[data-spec="trace-item-batch-enable"]')).toBeEnabled();
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('空态：提示「暂无跟踪项记录」', async ({ page }) => {
  await openStory(page, 'TraceItemsPanel/Empty');
  await expect(page.locator('[data-spec="trace-item-empty"]')).toContainText('暂无跟踪项记录');
  await expect(page.locator('[data-spec="trace-item-row"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="trace-item-failed"]')).toHaveCount(0);
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('加载中：空态分支内联 loading 旋转，提示「跟踪项加载中…」', async ({ page }) => {
  await openStory(page, 'TraceItemsPanel/Loading');
  await expect(page.locator('[data-spec="trace-item-empty"]')).toContainText('跟踪项加载中…');
  await expect(page.locator('[data-spec="trace-item-loading"].animate-spin')).toBeVisible();
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('失败态：失败文案透传桩错误原文 + 重试按钮可见，不伪装成空数据', async ({ page }) => {
  await openStory(page, 'TraceItemsPanel/Failed');
  await expect(page.locator('[data-spec="trace-item-failed"]')).toContainText('跟踪项加载失败');
  await expect(page.locator('[data-spec="trace-item-failed"]')).toContainText('示例数据源未');
  await expect(page.locator('[data-spec="trace-item-retry"]')).toBeVisible();
  await expect(page.locator('[data-spec="trace-item-empty"]')).toHaveCount(0);
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('应用过滤输入框 placeholder 渲染插值（CARD-MON-LIGHT-01）', async ({ page }) => {
  await openStory(page, 'TraceItemsPanel/WithData');
  // story appId=32 → 期望渲染 `#32`（修复前绑定串内 {{ appId }} 为字面文本，placeholder 外露 `#{{ appId }}`）
  await expect(page.locator('[data-spec="trace-item-appid"]')).toHaveAttribute('placeholder', '#32');
});