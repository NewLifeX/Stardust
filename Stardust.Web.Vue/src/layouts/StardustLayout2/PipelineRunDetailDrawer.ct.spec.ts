import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

const ROWS = '[data-spec="pipeline-run-detail-rows"]';

test('PipelineRunDetailDrawer 进行中：轮询中徽标、全字段就绪、「查看全部步骤」入口', async ({ page }) => {
  await openStory(page, 'PipelineRunDetailDrawer/Active');
  await expect(page.locator('[data-spec="pipeline-run-detail-polling"]')).toContainText('（轮询中）');
  await expect(page.locator('[data-spec="pipeline-run-detail-steps"]')).toContainText('查看全部步骤');
  // 字段集（旅程 C26 全字段 + 空值占位）
  for (const label of ['所属流水线', '触发源', '分支', '提交 SHA', '提交信息', '提交人', '提交时间', '编译开始', '编译完成', '部署开始', '部署完成', '耗时（秒）', '产物版本', '编译节点', '追踪 TraceId', '备注与错误信息']) {
    await expect(page.locator(ROWS)).toContainText(label);
  }
  // 无 TraceUrl → 只显示 TraceId 文本，无链接
  await expect(page.locator(ROWS).locator('a')).toHaveCount(0);
  await expect(page.locator(ROWS)).toContainText('a1b2c3d4e5f60718');
  // 耗时 = 部署完成缺失 → 编译完成-编译开始 = 360 秒
  await expect(page.locator(ROWS)).toContainText('360');
});

test('PipelineRunDetailDrawer 终态：无轮询徽标；TraceUrl 渲染链接；耗时 480 秒', async ({ page }) => {
  await openStory(page, 'PipelineRunDetailDrawer/Done');
  await expect(page.locator('[data-spec="pipeline-run-detail-polling"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="pipeline-run-detail-rows"]').locator('a')).toHaveAttribute('href', '/trace/a1b2c3d4e5f60718');
  await expect(page.locator('[data-spec="pipeline-run-detail-rows"]')).toContainText('480');
});

test('PipelineRunDetailDrawer 失败备注可见；三态文案逐字', async ({ page }) => {
  await openStory(page, 'PipelineRunDetailDrawer/Failed');
  await expect(page.locator('[data-spec="pipeline-run-detail-rows"]')).toContainText('编译失败：MSB3073 退出码 1');

  await openStory(page, 'PipelineRunDetailDrawer/Loading');
  await expect(page.locator('[data-spec="pipeline-run-detail-loading"]')).toContainText('运行详情加载中');

  await openStory(page, 'PipelineRunDetailDrawer/Missing');
  await expect(page.locator('[data-spec="pipeline-run-detail-missing"]')).toHaveText('运行记录不存在或已被删除');

  await openStory(page, 'PipelineRunDetailDrawer/Error');
  await expect(page.locator('[data-spec="pipeline-run-detail-failed"]')).toContainText('运行详情加载失败');
  await expect(page.locator('[data-spec="pipeline-run-detail-retry"]')).toContainText('重试');
});
