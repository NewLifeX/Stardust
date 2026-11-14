import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('PipelineBoard 列出启用与停用，停用的不能触发', async ({ page }) => {
  await openStory(page, 'PipelineBoard/Default');
  const cards = page.locator('[data-spec="pipeline-card"]');
  await expect(cards).toHaveCount(2);
  await expect(cards.nth(0)).toContainText('测试-MindHub');
  await expect(cards.nth(0)).toContainText('DGCWIAX10-Win');
  await expect(cards.nth(0).locator('[data-spec="pipeline-trigger"]')).toBeEnabled();
  await expect(cards.nth(1).locator('[data-spec="pipeline-trigger"]')).toBeDisabled();
  await expect(page.locator('[data-spec="pipeline-board"]')).not.toContainText('token');
});

test('PipelineBoard 空列表与加载失败文案不同', async ({ page }) => {
  await openStory(page, 'PipelineBoard/Empty');
  await expect(page.locator('[data-spec="pipeline-board-empty"]')).toContainText('还没有流水线');
  await openStory(page, 'PipelineBoard/Failed');
  await expect(page.locator('[data-spec="pipeline-board-failed"]')).toContainText('加载失败');
});
