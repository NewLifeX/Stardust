import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('选中令牌带授权：视觉基线', async ({ page }) => {
  await openStory(page, 'McpTokenResourcePanel/WithToken');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('未选令牌：提示空态视觉基线', async ({ page }) => {
  await openStory(page, 'McpTokenResourcePanel/NoToken');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('无授权行：空态视觉基线', async ({ page }) => {
  await openStory(page, 'McpTokenResourcePanel/EmptyResources');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

// 语义断言
test('WithToken 渲染三条授权行', async ({ page }) => {
  await openStory(page, 'McpTokenResourcePanel/WithToken');
  await expect(page.locator('[data-spec="mcp-resource-row"]')).toHaveCount(3);
});

test('NoToken 显示选择提示，不渲染新增行', async ({ page }) => {
  await openStory(page, 'McpTokenResourcePanel/NoToken');
  await expect(page.locator('[data-spec="mcp-resource-empty-token"]')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-resource-add"]')).toHaveCount(0);
});

test('未选资源时添加按钮禁用', async ({ page }) => {
  await openStory(page, 'McpTokenResourcePanel/WithToken');
  await expect(page.locator('[data-spec="mcp-resource-add"]')).toBeDisabled();
  // 选一个资源后按钮可用
  await page.locator('[data-spec="mcp-resource-id"]').selectOption({ index: 1 });
  await expect(page.locator('[data-spec="mcp-resource-add"]')).toBeEnabled();
});

// ===== CARD-PLT-05 查询条 =====
test('查询条控件可见：查询生效空态不静默，返回全部回 props 数据', async ({ page }) => {
  await openStory(page, 'McpTokenResourcePanel/WithToken');
  await expect(page.locator('[data-spec="mcp-resource-query-type"]')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-resource-query-enable"]')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-resource-query-dt"]').first()).toBeVisible();
  await expect(page.locator('[data-spec="mcp-resource-query-q"]')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-resource-row"]')).toHaveCount(3);
  // 桩固定成功空数据 → 查询态空态必须可见（不静默）
  await page.locator('[data-spec="mcp-resource-query-apply"]').click();
  await expect(page.locator('[data-spec="mcp-resource-empty"]')).toContainText('当前查询条件下没有授权');
  await expect(page.locator('[data-spec="mcp-resource-query-reset"]')).toBeVisible();
  // 返回全部 → 清快照回 props 客户端过滤 3 行
  await page.locator('[data-spec="mcp-resource-query-reset"]').click();
  await expect(page.locator('[data-spec="mcp-resource-row"]')).toHaveCount(3);
  await expect(page.locator('[data-spec="mcp-resource-query-reset"]')).toHaveCount(0);
});

test('查询失败注入可见提示（__ctRequestImpl override，注册-清理对称）', async ({ page }) => {
  await openStory(page, 'McpTokenResourcePanel/WithToken');
  await page.evaluate(() => {
    (globalThis as { __ctRequestImpl?: unknown }).__ctRequestImpl = async () => { throw new Error('网络中断'); };
  });
  try {
    await page.locator('[data-spec="mcp-resource-query-apply"]').click();
    await expect(page.locator('[data-spec="mcp-resource-query-error"]')).toContainText('授权查询失败：网络中断');
  } finally {
    await page.evaluate(() => { delete (globalThis as { __ctRequestImpl?: unknown }).__ctRequestImpl; });
  }
});

// ===== CARD-PLT-05 ②裁定：Drawer 内导出按钮不做（BL-4 维持【缺】防假） =====
test('无导出按钮防假：无 export spec 且面板内无「导出」文本按钮（BL-4【缺】维持，基类端点能力记录在案）', async ({ page }) => {
  await openStory(page, 'McpTokenResourcePanel/WithToken');
  await expect(page.locator('[data-spec="mcp-resource-export"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="mcp-resource-panel"] button', { hasText: '导出' })).toHaveCount(0);
});
