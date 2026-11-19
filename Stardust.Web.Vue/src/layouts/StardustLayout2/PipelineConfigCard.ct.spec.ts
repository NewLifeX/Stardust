import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('PipelineConfigCard 默认：标题、7 列、行操作文案；禁用行「启动」置灰并说明原因', async ({ page }) => {
  await openStory(page, 'PipelineConfigCard/Default');
  await expect(page.locator('[data-spec="pipeline-config"] h3')).toContainText('流水线配置');
  await expect(page.locator('[data-spec="pipeline-config-create"]')).toContainText('创建流水线');
  await expect(page.locator('[data-spec="pipeline-config-row"]')).toHaveCount(2);
  const first = page.locator('[data-spec="pipeline-config-row"]').nth(0);
  await expect(first).toContainText('build-main');
  await expect(first).toContainText('release/*');
  await expect(first.locator('[data-spec="pipeline-config-copywebhook"]')).toContainText('复制 WebHook');
  await expect(first.locator('[data-spec="pipeline-config-trigger"]')).toBeEnabled();
  await expect(first.locator('[data-spec="pipeline-config-trigger"]')).toContainText('启动');
  await expect(first.locator('[data-spec="pipeline-config-edit"]')).toContainText('编辑');
  await expect(first.locator('[data-spec="pipeline-config-remove"]')).toContainText('删除');
  // 敏感列不出现（盘点隐藏清单：Id/Token/Secret/DeployNodeIds/TraceId）
  const head = page.locator('[data-spec="pipeline-config"] thead');
  for (const banned of ['Id', 'Token', 'Secret', 'TraceId']) {
    await expect(head).not.toContainText(banned);
  }
  // 禁用行：启动置灰，title 说明原因
  const second = page.locator('[data-spec="pipeline-config-row"]').nth(1);
  await expect(second.locator('[data-spec="pipeline-config-trigger"]')).toBeDisabled();
  await expect(second.locator('[data-spec="pipeline-config-trigger"]')).toHaveAttribute('title', '流水线未启用，先编辑打开「启用」');
});

// C21（裁定 #9）：部署集存在但无流水线 → 空态「还没有流水线」+「创建流水线」引导
test('PipelineConfigCard 空态：还没有流水线 + 创建流水线引导按钮', async ({ page }) => {
  await openStory(page, 'PipelineConfigCard/Empty');
  await expect(page.locator('[data-spec="pipeline-config-empty"]')).toContainText('还没有流水线');
  await expect(page.locator('[data-spec="pipeline-config-empty-create"]')).toContainText('创建流水线');
  await expect(page.locator('[data-spec="pipeline-config-empty-create"]')).toBeEnabled();
});

test('PipelineConfigCard 加载失败：失败行 + 重试', async ({ page }) => {
  await openStory(page, 'PipelineConfigCard/Failed');
  await expect(page.locator('[data-spec="pipeline-config-failed"]')).toContainText('流水线配置加载失败');
  await expect(page.locator('[data-spec="pipeline-config-retry"]')).toContainText('重试');
});

// AGENTS.md §11：行操作进行中按钮旋转、禁用、文案变化
test('PipelineConfigCard busy：复制/启动/删除进行中的旋转与禁用', async ({ page }) => {
  await openStory(page, 'PipelineConfigCard/Busy');
  const copy = page.locator('[data-spec="pipeline-config-copywebhook"]').nth(0);
  await expect(copy).toBeDisabled();
  await expect(copy).toContainText('复制中…');
  await expect(copy.locator('.animate-spin')).toHaveCount(1);

  await openStory(page, 'PipelineConfigCard/Triggering');
  const trigger = page.locator('[data-spec="pipeline-config-trigger"]').nth(0);
  await expect(trigger).toBeDisabled();
  await expect(trigger).toContainText('触发中…');
  await expect(trigger.locator('.animate-spin')).toHaveCount(1);

  await openStory(page, 'PipelineConfigCard/Removing');
  const remove = page.locator('[data-spec="pipeline-config-remove"]').nth(0);
  await expect(remove).toBeDisabled();
  await expect(remove).toContainText('删除中…');
});
