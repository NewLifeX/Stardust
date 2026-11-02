import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('ThreeRows：F04 快照列——真实 CPU/内存、离线标签、无数据行保留「待接入」', async ({ page }) => {
  await openStory(page, 'AppInstances/ThreeRows');
  const rows = page.locator('[data-spec="app-instances-row"]');
  await expect(rows).toHaveCount(3);
  await expect(rows.first()).toContainText('node-a');
  // F04：快照值（12.3% / 150 MB），不再是「待接入」
  await expect(rows.nth(0).locator('[data-spec="app-instances-cpu"]')).toHaveText('12.3%');
  await expect(rows.nth(0).locator('[data-spec="app-instances-memory"]')).toHaveText('150 MB');
  // F04：online=false 的行（id 12）显示「离线」标签
  await expect(rows.nth(1)).toContainText('离线');
  await expect(rows.nth(1).locator('[data-spec="app-instances-cpu"]')).toHaveText('88.5%');
  // F04：快照无性能数据的行（id 13）保留「待接入」
  await expect(rows.nth(2).locator('[data-spec="app-instances-cpu"]')).toContainText('待接入');
  await expect(rows.nth(2).locator('[data-spec="app-instances-memory"]')).toContainText('待接入');
  // 页头计数：3 个实例（2 在线）
  await expect(page.locator('[data-spec="app-instances"]')).toContainText('3 个实例（2 在线）');
});

test('F03：勾选两行 → 批量条出现，执行命令后汇总完整展示（多行）', async ({ page }) => {
  await openStory(page, 'AppInstances/ThreeRows');
  const rows = page.locator('[data-spec="app-instances-row"]');
  await rows.nth(0).locator('[data-spec="app-instances-check"]').check();
  await rows.nth(1).locator('[data-spec="app-instances-check"]').check();
  const bar = page.locator('[data-spec="app-instances-batchbar"]');
  await expect(bar).toContainText('已选 2 个实例');
  // 空命令拦截在视线内
  await page.locator('[data-spec="app-instances-execute"]').click();
  await expect(page.locator('[data-spec="app-instances-command-error"]')).toContainText('请输入要下发的命令');
  // 输入命令执行 → 汇总面板多行完整展示（含失败详情）
  await page.locator('[data-spec="app-instances-command"]').fill('app/ping');
  await page.locator('[data-spec="app-instances-argument"]').fill('-v');
  await page.locator('[data-spec="app-instances-execute"]').click();
  const result = page.locator('[data-spec="app-instances-result"]');
  await expect(result).toContainText('下发2个，响应1个，超时1个');
  await expect(result).toContainText('node-a: pong');
  await expect(result).toContainText('node-b: (超时)');
});

test('F03：释放内存走 Guard 确认；取消选择清空批量条', async ({ page }) => {
  await openStory(page, 'AppInstances/ThreeRows');
  const rows = page.locator('[data-spec="app-instances-row"]');
  await rows.nth(0).locator('[data-spec="app-instances-check"]').check();
  // Guard 弹确认 → 确认后执行并显示汇总
  await page.locator('[data-spec="app-instances-freemem"]').click();
  await expect(page.locator('[data-spec="guard-modal"]')).toContainText('释放内存');
  await page.locator('[data-spec="guard-confirm"]').click();
  await expect(page.locator('[data-spec="app-instances-result"]')).toContainText('下发1个');
  // 取消选择 → 批量条收起
  await page.locator('[data-spec="app-instances-batch-clear"]').click();
  await expect(page.locator('[data-spec="app-instances-batchbar"]')).toHaveCount(0);
});

test('F03：危险命令（app/restart）弹 Guard，确认后才下发', async ({ page }) => {
  await openStory(page, 'AppInstances/ThreeRows');
  const rows = page.locator('[data-spec="app-instances-row"]');
  await rows.nth(0).locator('[data-spec="app-instances-check"]').check();
  await page.locator('[data-spec="app-instances-command"]').fill('app/restart');
  await page.locator('[data-spec="app-instances-execute"]').click();
  // 危险命令：先 Guard，不直接发请求
  await expect(page.locator('[data-spec="guard-modal"]')).toContainText('危险命令「app/restart」');
  await expect(page.locator('[data-spec="app-instances-result"]')).toHaveCount(0);
  await page.locator('[data-spec="guard-confirm"]').click();
  await expect(page.locator('[data-spec="app-instances-result"]')).toContainText('下发1个');
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
  // 行点击不含勾选框（勾选框 @click.stop）
  await row.locator('td').nth(1).click();
  const ev1 = await page.evaluate(() => (window as unknown as { __instEvents?: Array<[string, number]> }).__instEvents);
  expect(ev1).toEqual([['select', 11]]);

  // hover 浮出操作按钮后点击重启与终端
  await row.hover();
  await row.locator('[data-spec="app-instances-restart"]').click();
  await row.locator('[data-spec="app-instances-terminal"]').click();
  const ev2 = await page.evaluate(() => (window as unknown as { __instEvents?: Array<[string, number]> }).__instEvents);
  expect(ev2).toEqual([['select', 11], ['restart', 11], ['terminal', 11]]);
});
