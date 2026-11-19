import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('PipelineWebhookCard 默认：GetFields 指引、请求说明、结果语义五类、每条流水线复制按钮', async ({ page }) => {
  await openStory(page, 'PipelineWebhookCard/Default');
  await expect(page.locator('[data-spec="pipeline-webhook-card"] h3')).toContainText('Webhook 接入');
  await expect(page.locator('[data-spec="pipeline-webhook-copy"]')).toHaveCount(2);
  // 接入指引来自 GetFields：token/secret 两条说明
  const fields = page.locator('[data-spec="pipeline-webhook-fields"]');
  const card = page.locator('[data-spec="pipeline-webhook-card"]');
  await expect(fields).toContainText('Token');
  await expect(fields).toContainText('?token=');
  await expect(fields).toContainText('Secret');
  // Secret 值不展示注记（裁定 #6）：只有说明，无查看/设置入口
  await expect(card).toContainText('不做查看/设置');
  // 请求说明：POST + 原始 JSON + X-Hub-Signature-256 + 异步 5s
  await expect(card).toContainText('POST /Deployment/Pipeline/Webhook?token=');
  await expect(card).toContainText('原始 JSON');
  await expect(card).toContainText('X-Hub-Signature-256');
  await expect(card).toContainText('每 5 秒轮询');
  // 结果语义五类 + reason 逐字
  const results = page.locator('[data-spec="pipeline-webhook-results"] > div');
  await expect(results).toHaveCount(5);
  await expect(results.nth(0)).toContainText('accepted');
  await expect(results.nth(1)).toContainText('pipeline disabled');
  await expect(results.nth(1)).toContainText('branch mismatch');
  await expect(results.nth(2)).toContainText('duplicate');
  await expect(results.nth(3)).toContainText('invalid signature');
  await expect(results.nth(4)).toContainText('missing token');
});

test('PipelineWebhookCard 降级：GetFields 失败显示静态说明 + 降级注记', async ({ page }) => {
  await openStory(page, 'PipelineWebhookCard/Fallback');
  await expect(page.locator('[data-spec="pipeline-webhook-fields-fallback"]')).toContainText('已降级');
  await expect(page.locator('[data-spec="pipeline-webhook-fields"]')).toContainText('HMAC-SHA256');
});

test('PipelineWebhookCard busy 与空态', async ({ page }) => {
  await openStory(page, 'PipelineWebhookCard/Copying');
  const copy = page.locator('[data-spec="pipeline-webhook-copy"]').nth(0);
  await expect(copy).toBeDisabled();
  await expect(copy).toContainText('复制中…');
  await expect(copy.locator('.animate-spin')).toHaveCount(1);
  // 另一条流水线的复制按钮只是禁用（同一时刻单操作），非旋转
  await expect(page.locator('[data-spec="pipeline-webhook-copy"]').nth(1)).toBeDisabled();

  await openStory(page, 'PipelineWebhookCard/Empty');
  await expect(page.locator('[data-spec="pipeline-webhook-card"]')).toContainText('还没有流水线');
  await expect(page.locator('[data-spec="pipeline-webhook-copy"]')).toHaveCount(0);
});
