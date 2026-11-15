import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

const ROW = '[data-spec="pipeline-steps-row"]';

test('PipelineStepDrawer 九字段表：四行、状态字符串原样不翻译、节点名兜底、起止时间', async ({ page }) => {
  await openStory(page, 'PipelineStepDrawer/Default');
  await expect(page.locator('[data-spec="drawer"]')).toContainText('共 4 步');
  await expect(page.locator(ROW)).toHaveCount(4);
  const first = page.locator(ROW).nth(0);
  await expect(first).toContainText('Build');
  await expect(first).toContainText('Success');
  // Build 步骤 NodeId=0 → 节点占位 —；Deploy 步骤节点名来自 nodes 列表
  await expect(first).toContainText('—');
  await expect(page.locator(ROW).nth(1)).toContainText('node-a');
  await expect(first).toContainText('2026-11-07 09:13');
  // Status 原样英文字符串（Running/Success/Failed/Skipped/Cancelled），不翻译
  await expect(page.locator(ROW).nth(2)).toContainText('Failed');
  await expect(page.locator(ROW).nth(3)).toContainText('Skipped');
  // 节点 9 不在节点列表 → 兜底「节点 9」
  await expect(page.locator(ROW).nth(3)).toContainText('节点 9');
  // Skipped 消息空 → 占位 —
  await expect(page.locator(ROW).nth(3)).toContainText('—');
  // 只读：表格区内无任何操作按钮；底部只有「关闭」
  await expect(page.locator('[data-spec="pipeline-steps-drawer"] button')).toHaveCount(0);
  await expect(page.locator('[data-spec="pipeline-steps-close"]')).toContainText('关闭');
});

test('PipelineStepDrawer 行展开：Message 全文（后端截断 2000 的展示位）', async ({ page }) => {
  await openStory(page, 'PipelineStepDrawer/Default');
  await expect(page.locator('[data-spec="pipeline-steps-expand"]')).toHaveCount(0);
  await page.locator(ROW).nth(2).click();
  const expanded = page.locator('[data-spec="pipeline-steps-expand"]');
  await expect(expanded).toHaveCount(1);
  await expect(expanded).toContainText('安装失败：404 包不存在');
  await expect(expanded).toContainText('RunId 42');
  await expect(expanded).toContainText('CommandId 5503');
  // 再点收起
  await page.locator(ROW).nth(2).click();
  await expect(page.locator('[data-spec="pipeline-steps-expand"]')).toHaveCount(0);
});

test('PipelineStepDrawer 无步骤：占位文案（非流水线发起的部署）', async ({ page }) => {
  await openStory(page, 'PipelineStepDrawer/Empty');
  await expect(page.locator('[data-spec="pipeline-steps-empty"]')).toContainText('这次运行没有步骤记录');
});
