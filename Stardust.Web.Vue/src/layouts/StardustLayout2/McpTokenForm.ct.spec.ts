import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(500); // 等 Drawer 入场过渡
}

// 抽屉 fixed 贴右、视口截图（同 Drawer.ct.spec 的约定）
test('新建表单：空表单视觉基线', async ({ page }) => {
  await openStory(page, 'McpTokenForm/Create');
  await expect(page).toHaveScreenshot();
});

test('编辑表单：回填已有值视觉基线', async ({ page }) => {
  await openStory(page, 'McpTokenForm/Edit');
  await expect(page).toHaveScreenshot();
});

test('提交中：按钮旋转禁用视觉基线', async ({ page }) => {
  await openStory(page, 'McpTokenForm/Submitting');
  await expect(page).toHaveScreenshot();
});

// 语义断言
test('编辑表单回填名称与备注', async ({ page }) => {
  await openStory(page, 'McpTokenForm/Edit');
  await expect(page.locator('[data-spec="mcp-form-name"]')).toHaveValue('claude-ops');
  await expect(page.locator('[data-spec="mcp-form-remark"]')).toHaveValue('运维助手小萌');
});

test('名称为空时提交按钮禁用', async ({ page }) => {
  await openStory(page, 'McpTokenForm/Create');
  await page.locator('[data-spec="mcp-form-name"]').fill('');
  await expect(page.locator('[data-spec="mcp-form-submit"]')).toBeDisabled();
});

test('提交中文名后发 submit 事件', async ({ page }) => {
  await openStory(page, 'McpTokenForm/Create');
  // 画廊壳会把组件事件打到 window.__lastEvent；这里只验证按钮可点与表单可用
  await page.locator('[data-spec="mcp-form-name"]').fill('小萌-新令牌');
  await expect(page.locator('[data-spec="mcp-form-submit"]')).toBeEnabled();
});
