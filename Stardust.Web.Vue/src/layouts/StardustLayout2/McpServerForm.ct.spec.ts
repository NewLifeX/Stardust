import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(500); // 等 Drawer 入场过渡
}

// 视觉基线
test('新建服务器：空表单视觉基线', async ({ page }) => {
  await openStory(page, 'McpServerForm/Create');
  await expect(page).toHaveScreenshot();
});

test('编辑服务器：回填已有值视觉基线', async ({ page }) => {
  await openStory(page, 'McpServerForm/Edit');
  await expect(page).toHaveScreenshot();
});

test('服务器表单提交中视觉基线', async ({ page }) => {
  await openStory(page, 'McpServerForm/Submitting');
  await expect(page).toHaveScreenshot();
});

// 语义断言
test('编辑表单回填名称/地址/备注', async ({ page }) => {
  await openStory(page, 'McpServerForm/Edit');
  await expect(page.locator('[data-spec="mcp-server-form-name"]')).toHaveValue('stardust-core');
  await expect(page.locator('[data-spec="mcp-server-form-url"]')).toHaveValue('http://localhost:6680/api/mcp');
  await expect(page.locator('[data-spec="mcp-server-form-remark"]')).toHaveValue('平台内置端点');
});

test('名称为空时提交按钮禁用', async ({ page }) => {
  await openStory(page, 'McpServerForm/Create');
  await page.locator('[data-spec="mcp-server-form-name"]').fill('');
  await expect(page.locator('[data-spec="mcp-server-form-submit"]')).toBeDisabled();
});

test('填齐名称与地址后提交按钮可用', async ({ page }) => {
  await openStory(page, 'McpServerForm/Create');
  await page.locator('[data-spec="mcp-server-form-name"]').fill('filesystem');
  await page.locator('[data-spec="mcp-server-form-url"]').fill('http://node-a:6680/api/mcp');
  await expect(page.locator('[data-spec="mcp-server-form-submit"]')).toBeEnabled();
});
