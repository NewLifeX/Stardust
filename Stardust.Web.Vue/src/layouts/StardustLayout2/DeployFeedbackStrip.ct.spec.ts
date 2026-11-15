import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

const FEEDBACK = '[data-spec="ship-feedback"]';
const ITEM = '[data-spec="ship-feedback-item"]';
const ACTION = '[data-spec="ship-feedback-action"]';
const BUSY = '[data-spec="ship-busy"]';

// 视觉基线：默认态（成功带「查看运行」直达 + 失败 + 普通）与进行中态
test('DeployFeedbackStrip 默认态截图', async ({ page }) => {
  await openStory(page, 'DeployFeedbackStrip/Default');
  await expect(page.locator(FEEDBACK)).toHaveScreenshot();
});

test('DeployFeedbackStrip 进行中态截图', async ({ page }) => {
  await openStory(page, 'DeployFeedbackStrip/Busy');
  await expect(page.locator(FEEDBACK)).toHaveScreenshot();
});

// 语义断言：三条反馈各就各位；只有带 action 的成功条目有「查看运行」按钮
test('DeployFeedbackStrip 默认：查看运行按钮只出现在触发成功的条目上', async ({ page }) => {
  await openStory(page, 'DeployFeedbackStrip/Default');
  await expect(page.locator(ITEM)).toHaveCount(3);
  await expect(page.locator(BUSY)).toHaveCount(0);

  const first = page.locator(ITEM).nth(0);
  await expect(first).toContainText('流水线已触发，运行编号[42]');
  const action = first.locator(ACTION);
  await expect(action).toBeEnabled();
  await expect(action).toHaveText('查看运行');
  await expect(action).toHaveAttribute('title', '打开运行 #42 的详情抽屉（状态、步骤、Trace）');

  await expect(page.locator(ITEM).nth(1)).toContainText('触发流水线 build-main 失败');
  await expect(page.locator(ITEM).nth(1).locator(ACTION)).toHaveCount(0);
  await expect(page.locator(ITEM).nth(2)).toContainText('部署集已保存');
  await expect(page.locator(ITEM).nth(2).locator(ACTION)).toHaveCount(0);
});

// 语义断言：进行中只有 busy 条，没有反馈条目；空闲时整条不渲染
test('DeployFeedbackStrip 进行中只显示 busy 条；空闲不渲染', async ({ page }) => {
  await openStory(page, 'DeployFeedbackStrip/Busy');
  await expect(page.locator(BUSY)).toHaveText('正在触发流水线 build-main…');
  await expect(page.locator(ITEM)).toHaveCount(0);
  await expect(page.locator(ACTION)).toHaveCount(0);

  await openStory(page, 'DeployFeedbackStrip/Empty');
  await expect(page.locator(FEEDBACK)).toHaveCount(0);
});
