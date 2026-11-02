import { expect, test, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(400);
}

test('Mixed：五节点行渲染，名称+mono IP，chip 计数正确', async ({ page }) => {
  await openStory(page, 'NodesPage/Mixed');
  await expect(page.locator('[data-spec="nodes-row"]')).toHaveCount(5);
  await expect(page.locator('[data-spec="nodes-row"]').nth(0)).toContainText('node-a');
  await expect(page.locator('[data-spec="nodes-row"]').nth(0)).toContainText('10.0.0.1');
  // chip 计数：全部5 / 正常2 / 高负载2 / 离线1
  const chips = page.locator('[data-spec="nodes-filter-chip"]');
  await expect(chips.nth(0)).toContainText('全部 5');
  await expect(chips.nth(1)).toContainText('正常 2');
  await expect(chips.nth(2)).toContainText('高负载 2');
  await expect(chips.nth(3)).toContainText('离线 1');
  // 离线行 CPU/内存列显示 —
  await expect(page.locator('[data-spec="nodes-row"][data-tone="offline"]')).toContainText('node-c');
});

test('过滤：chip 切离线只留 1 行，搜索框按名称过滤', async ({ page }) => {
  await openStory(page, 'NodesPage/Mixed');
  await page.locator('[data-spec="nodes-filter-chip"][data-key="offline"]').click();
  await expect(page.locator('[data-spec="nodes-row"]')).toHaveCount(1);
  await expect(page.locator('[data-spec="nodes-row"]')).toContainText('node-c');

  // 切回全部，再按 IP 搜索
  await page.locator('[data-spec="nodes-filter-chip"][data-key="all"]').click();
  await page.locator('[data-spec="nodes-search"]').fill('10.0.0.2');
  await expect(page.locator('[data-spec="nodes-row"]')).toHaveCount(1);
  await expect(page.locator('[data-spec="nodes-row"]')).toContainText('node-b');
});

test('勾选：勾一行出现批量卡「已选 1 台」，取消后消失', async ({ page }) => {
  await openStory(page, 'NodesPage/Mixed');
  await expect(page.locator('[data-spec="nodes-batch"]')).toHaveCount(0);
  await page.locator('[data-spec="nodes-row"]').nth(0).locator('[data-spec="nodes-row-check"]').check();
  await expect(page.locator('[data-spec="nodes-batch"]')).toBeVisible();
  await expect(page.locator('[data-spec="nodes-batch"]')).toContainText('已选 1 台');
  // 批量按钮都是 disabled 且带「后端暂未提供批量操作接口」提示
  await expect(page.locator('[data-spec="nodes-batch-upgrade"]')).toBeDisabled();
  await expect(page.locator('[data-spec="nodes-batch-upgrade"]')).toHaveAttribute('title', '后端暂未提供批量操作接口');
  // 取消清空
  await page.locator('[data-spec="nodes-batch-clear"]').click();
  await expect(page.locator('[data-spec="nodes-batch"]')).toHaveCount(0);
});

test('点行打开抽屉：概览 tab 显示 KV 与 CPU 空态，切到应用 tab 显示应用列表', async ({ page }) => {
  await openStory(page, 'NodesPage/Mixed');
  await page.locator('[data-spec="nodes-row"]').nth(0).click();
  await expect(page.locator('[data-spec="drawer"]')).toBeVisible();
  await expect(page.locator('[data-spec="drawer"]')).toContainText('node-a');
  // 概览：CPU 空态 + KV 行（IP/机房/Agent 版本等）
  await expect(page.locator('[data-spec="nodes-drawer-cpu-empty"]')).toContainText('CPU 走势待接入');
  await expect(page.locator('[data-spec="drawer"]')).toContainText('Agent 版本');
  await expect(page.locator('[data-spec="drawer"]')).toContainText('10.0.0.1');

  // 切到「应用进程」tab：node-a 有 2 个应用
  await page.locator('[data-spec="nodes-drawer-tab"][data-key="apps"]').click();
  await expect(page.locator('[data-spec="nodes-drawer-app"]')).toHaveCount(2);
  await expect(page.locator('[data-spec="nodes-drawer-app"]').nth(0)).toContainText('order-api');
});

test('终端按钮点击不发 toast（走 requestTermNode 通道），drawer 操作按钮 disabled', async ({ page }) => {
  await openStory(page, 'NodesPage/Mixed');
  // 表格行内终端按钮：现在走 ui.store.requestTermNode，不再 toast
  await page.locator('[data-spec="nodes-row-terminal"]').nth(0).click();
  await expect(page.locator('.el-notification, .sd-toast')).toHaveCount(0);

  // 打开抽屉，抽屉内升级/安装按钮 disabled
  await page.locator('[data-spec="nodes-row"]').nth(0).click();
  await expect(page.locator('[data-spec="nodes-drawer-upgrade"]')).toBeDisabled();
  await expect(page.locator('[data-spec="nodes-drawer-dotnet"]')).toBeDisabled();
  await expect(page.locator('[data-spec="nodes-drawer-terminal"]')).toBeVisible();
});

test('Empty：空态文案，表格「没有匹配的节点」', async ({ page }) => {
  await openStory(page, 'NodesPage/Empty');
  await expect(page.locator('[data-spec="nodes-sky-empty"]')).toContainText('还没有节点');
  await expect(page.locator('[data-spec="nodes-empty"]')).toContainText('没有匹配的节点');
});

test('Failed：可见错误条 + 重试按钮', async ({ page }) => {
  await openStory(page, 'NodesPage/Failed');
  await expect(page.locator('[data-spec="nodes-error"]')).toContainText('节点加载失败');
  await expect(page.locator('[data-spec="nodes-retry"]')).toBeVisible();
});
