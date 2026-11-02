import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  // 桩请求 + 300ms 最小旋转后状态才落定
  await page.waitForTimeout(500);
}

test('Busy：Hero 显示「2 件事需要你」，eyebrow 在线数与实例数就位', async ({ page }) => {
  await openStory(page, 'NowPage/Busy');
  await expect(page.locator('[data-spec="now-page"]')).toContainText('件事需要你');
  await expect(page.locator('[data-spec="now-hero"]')).toContainText('节点 3/4 在线');
  await expect(page.locator('[data-spec="now-hero"]')).toContainText('0 个应用实例');
});

test('Busy：舰队四格三色，标题带节点名与 CPU', async ({ page }) => {
  await openStory(page, 'NowPage/Busy');
  const cells = page.locator('[data-spec="now-fleet-cell"]');
  await expect(cells).toHaveCount(4);
  const statuses = await cells.evaluateAll((els) => els.map((el) => el.getAttribute('data-status')));
  expect(statuses).toEqual(['ok', 'high', 'ok', 'off']);
  await expect(cells.first()).toHaveAttribute('title', 'node-a · CPU 23%');
});

test('Busy：两条告警卡片，首条默认展开证据块与三个处置按钮', async ({ page }) => {
  await openStory(page, 'NowPage/Busy');
  const cards = page.locator('[data-spec="now-incident"]');
  await expect(cards).toHaveCount(2);
  await expect(cards.first()).toHaveAttribute('data-severity', 'warn');
  await expect(cards.first()).toHaveAttribute('data-open', 'true');
  await expect(page.locator('[data-spec="now-incident-content"]')).toContainText('错误率 12%');
  await expect(page.locator('[data-spec="now-fix-rollback"]')).toContainText('回滚');
  await expect(page.locator('[data-spec="now-fix-snooze"]')).toContainText('静默 1 小时');
});

test('Busy：点第二条告警头，第二条展开、首条收起', async ({ page }) => {
  await openStory(page, 'NowPage/Busy');
  await page.locator('[data-spec="now-incident-head"]').nth(1).click();
  const cards = page.locator('[data-spec="now-incident"]');
  await expect(cards.nth(0)).toHaveAttribute('data-open', 'false');
  await expect(cards.nth(1)).toHaveAttribute('data-open', 'true');
  await expect(cards.nth(1)).toContainText('node-b 已下线');
});

test('Busy：右栏应用 3 行、最近变更 2 行（D/C 方块）', async ({ page }) => {
  await openStory(page, 'NowPage/Busy');
  await expect(page.locator('[data-spec="now-app"]')).toHaveCount(3);
  const changes = page.locator('[data-spec="now-change"]');
  await expect(changes).toHaveCount(2);
  await expect(changes.first()).toHaveAttribute('data-path', '/app/history/101');
  await expect(changes.nth(1)).toHaveAttribute('data-path', '/app/config/202');
});

test('Clear：无告警时 Hero「✓ 一切正常」，左栏空态「全部处理完毕」', async ({ page }) => {
  await openStory(page, 'NowPage/Clear');
  await expect(page.locator('[data-spec="now-hero"]')).toContainText('一切正常');
  await expect(page.locator('[data-spec="now-incidents-empty"]')).toContainText('全部处理完毕');
  await expect(page.locator('[data-spec="now-incident"]')).toHaveCount(0);
});

test('Empty：无节点/应用/变更时各段空态文案可见', async ({ page }) => {
  await openStory(page, 'NowPage/Empty');
  await expect(page.locator('[data-spec="now-fleet-empty"]')).toContainText('还没有接入节点');
  await expect(page.locator('[data-spec="now-apps-empty"]')).toContainText('还没有接入应用');
  await expect(page.locator('[data-spec="now-changes-empty"]')).toContainText('最近没有变更记录');
});

test('Failed：四段错误横幅全部可见，不渲染空态', async ({ page }) => {
  await openStory(page, 'NowPage/Failed');
  await expect(page.locator('[data-spec="now-fleet-failed"]')).toContainText('重试');
  await expect(page.locator('[data-spec="now-incidents-failed"]')).toContainText('重试');
  await expect(page.locator('[data-spec="now-apps-failed"]')).toContainText('重试');
  await expect(page.locator('[data-spec="now-changes-failed"]')).toContainText('重试');
  await expect(page.locator('[data-spec="now-fleet-empty"]')).toHaveCount(0);
});

test('SelectRecorder：点节点格 / 应用行 / 变更行发出对应 go 路径', async ({ page }) => {
  await openStory(page, 'NowPage/SelectRecorder');
  await page.locator('[data-spec="now-fleet-cell"]').first().click();
  await page.locator('[data-spec="now-app"]').first().click();
  await page.locator('[data-spec="now-change"]').first().click();
  const picked = await page.evaluate(() => (window as unknown as { __nowGo?: string[] }).__nowGo);
  expect(picked).toEqual(['/nodes', '/app/overview/0', '/app/history/101']);
});

test('SelectRecorder：告警区标题旁「查看全部 →」发出 /alarms', async ({ page }) => {
  await openStory(page, 'NowPage/SelectRecorder');
  await page.locator('[data-spec="now-alarms-more"]').click();
  const picked = await page.evaluate(() => (window as unknown as { __nowGo?: string[] }).__nowGo);
  expect(picked).toEqual(['/alarms']);
});
