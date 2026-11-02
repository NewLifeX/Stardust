import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

const ROW = '[data-spec="ship-node-row"]';
const ACTS = ['install', 'start', 'restart', 'stop', 'uninstall'];

test('DeployNodes 默认：三行节点，信息、状态、版本标签各自正确', async ({ page }) => {
  await openStory(page, 'DeployNodes/Default');
  await expect(page.locator(ROW)).toHaveCount(3);

  const a = page.locator(ROW).nth(0);
  await expect(a).toContainText('node-a');
  await expect(a).toContainText('10.0.0.1');
  await expect(a).toContainText('Standard');
  await expect(a).toContainText('v1.4.2');
  await expect(a).toContainText('启用');
  await expect(a).toContainText('与部署集一致');

  const b = page.locator(ROW).nth(1);
  await expect(b).toContainText('与部署集不一致');

  // 已停用的节点：没有 IP 显示「—」，没有版本标签
  const c = page.locator(ROW).nth(2);
  await expect(c).toContainText('已停用');
  await expect(c).toContainText('—');
  await expect(c).not.toContainText('部署集');
});

test('DeployNodes 每行五个操作按钮，文案固定，空闲时都可点', async ({ page }) => {
  await openStory(page, 'DeployNodes/Default');
  const row = page.locator(ROW).nth(0);
  for (const act of ACTS) {
    await expect(row.locator(`[data-spec="ship-op-${act}"]`)).toBeEnabled();
  }
  await expect(row.locator('[data-spec="ship-op-install"]')).toHaveText('安装');
  await expect(row.locator('[data-spec="ship-op-start"]')).toHaveText('启动');
  await expect(row.locator('[data-spec="ship-op-restart"]')).toHaveText('重启');
  await expect(row.locator('[data-spec="ship-op-stop"]')).toHaveText('停止');
  await expect(row.locator('[data-spec="ship-op-uninstall"]')).toHaveText('卸载');
});

// 反馈：下发中该按钮写「下发中…」，其余按钮也禁用，避免对多个节点并发下发
test('DeployNodes 下发中：对应按钮写「下发中…」，所有操作按钮禁用', async ({ page }) => {
  await openStory(page, 'DeployNodes/Busy');
  await expect(page.locator(ROW).nth(1).locator('[data-spec="ship-op-restart"]')).toHaveText('下发中…');
  // 同一行的其他按钮、其他行的同名按钮都不受影响文案，但都禁用
  await expect(page.locator(ROW).nth(0).locator('[data-spec="ship-op-restart"]')).toHaveText('重启');
  for (let r = 0; r < 3; r++) {
    for (const act of ACTS) {
      await expect(page.locator(ROW).nth(r).locator(`[data-spec="ship-op-${act}"]`)).toBeDisabled();
    }
  }
});

test('DeployNodes 没有节点与加载失败的提示不同', async ({ page }) => {
  await openStory(page, 'DeployNodes/Empty');
  await expect(page.locator('[data-spec="ship-nodes-empty"]')).toContainText('该部署集下暂无节点');
  await expect(page.locator('[data-spec="ship-nodes-failed"]')).toHaveCount(0);

  await openStory(page, 'DeployNodes/Failed');
  await expect(page.locator('[data-spec="ship-nodes-failed"]')).toContainText('节点加载失败');
  await expect(page.locator('[data-spec="ship-nodes-empty"]')).toHaveCount(0);
});

// 语义断言：危险操作（停止、卸载）与普通操作的样式要有区别；一致与不一致标签颜色不同
test('DeployNodes 危险按钮与普通按钮样式不同；一致与不一致标签颜色不同', async ({ page }) => {
  await openStory(page, 'DeployNodes/Default');
  const row = page.locator(ROW).nth(0);
  const look = (act: string) =>
    row.locator(`[data-spec="ship-op-${act}"]`).evaluate((el) => {
      const s = getComputedStyle(el);
      return `${s.color}|${s.borderColor}|${s.backgroundColor}`;
    });
  const normal = await look('start');
  expect(await look('stop')).not.toBe(normal);
  expect(await look('uninstall')).not.toBe(normal);
  expect(await look('restart')).toBe(normal);

  const tagColor = (n: number) =>
    page.locator(ROW).nth(n).locator('[data-spec="tag"]', { hasText: '部署集' }).evaluate((el) => getComputedStyle(el).color);
  expect(await tagColor(0)).not.toBe(await tagColor(1));
});
