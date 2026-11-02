import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

const ROW = '[data-spec="ship-history-row"]';
const DETAIL = '[data-spec="ship-history-detail"]';
const FILTER = '[data-spec="ship-history-node"]';

test('DeployHistory 默认：三条记录，时间、节点、动作、下发状态、备注各自正确', async ({ page }) => {
  await openStory(page, 'DeployHistory/Default');
  await expect(page.locator(ROW)).toHaveCount(3);
  await expect(page.locator(DETAIL)).toHaveCount(0);

  const a = page.locator(ROW).nth(0);
  await expect(a).toContainText('10-28 10:00:00');
  await expect(a).toContainText('node-a');
  await expect(a).toContainText('安装');
  await expect(a).toContainText('已下发');
  await expect(a).toContainText('安装 v1.4.2');

  const b = page.locator(ROW).nth(1);
  await expect(b).toContainText('重启');
  await expect(b).toContainText('下发失败');
  await expect(b).toContainText('连接超时');
});

test('DeployHistory 点行展开详情，再点收起；详情含备注、动作、TraceId、来源', async ({ page }) => {
  await openStory(page, 'DeployHistory/Default');
  await page.locator(ROW).nth(1).click();
  await expect(page.locator(DETAIL)).toHaveCount(1);
  await expect(page.locator(DETAIL)).toContainText('连接超时');
  await expect(page.locator(DETAIL)).toContainText('动作 restart');
  await expect(page.locator(DETAIL)).toContainText('TraceId trace-102');
  await expect(page.locator(DETAIL)).toContainText('来源 192.168.1.8');

  // 再点同一行收起
  await page.locator(ROW).nth(1).click();
  await expect(page.locator(DETAIL)).toHaveCount(0);

  // 没有备注、没有来源的记录：详情写占位文案
  await page.locator(ROW).nth(2).click();
  await expect(page.locator(DETAIL)).toContainText('流水线发布 #7');
  await expect(page.locator(DETAIL)).toContainText('来源 —');
});

test('DeployHistory 按节点筛选：只留该节点；无节点的流水线记录筛选时消失；选回全部恢复', async ({ page }) => {
  await openStory(page, 'DeployHistory/Default');
  const filter = page.locator(FILTER);
  await expect(filter.locator('option')).toHaveCount(3);
  await filter.selectOption('1');
  await expect(page.locator(ROW)).toHaveCount(1);
  await expect(page.locator(ROW).nth(0)).toContainText('node-a');
  await filter.selectOption('2');
  await expect(page.locator(ROW)).toHaveCount(1);
  await expect(page.locator(ROW).nth(0)).toContainText('node-b');
  await filter.selectOption('0');
  await expect(page.locator(ROW)).toHaveCount(3);
});

test('DeployHistory 暂无记录、加载失败、筛选无结果的提示各不相同', async ({ page }) => {
  await openStory(page, 'DeployHistory/Empty');
  await expect(page.locator('[data-spec="ship-history-empty"]')).toContainText('暂无部署历史');
  await expect(page.locator('[data-spec="ship-history-failed"]')).toHaveCount(0);

  await openStory(page, 'DeployHistory/Failed');
  await expect(page.locator('[data-spec="ship-history-failed"]')).toContainText('部署历史加载失败');
  await expect(page.locator('[data-spec="ship-history-empty"]')).toHaveCount(0);

  await openStory(page, 'DeployHistory/OnlyNodeA');
  await page.locator(FILTER).selectOption('2');
  await expect(page.locator(ROW)).toHaveCount(0);
  await expect(page.locator('[data-spec="ship-history-empty"]')).toContainText('最近这一页里没有该节点的记录');
});

// 语义断言：「已下发」与「下发失败」标签颜色不同
test('DeployHistory 已下发与下发失败的标签颜色不同', async ({ page }) => {
  await openStory(page, 'DeployHistory/Default');
  const colorOf = (text: string) =>
    page.locator('[data-spec="tag"]', { hasText: text }).first().evaluate((el) => getComputedStyle(el).color);
  expect(await colorOf('下发失败')).not.toBe(await colorOf('已下发'));
});
