import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(500);
}

// ===== 视觉基线 =====
test('McpTab 数据态整页视觉基线（指引+服务器+令牌+授权+审计）', async ({ page }) => {
  await openStory(page, 'McpTab/Data');
  await expect(page).toHaveScreenshot();
});

test('McpTab 空态视觉基线', async ({ page }) => {
  await openStory(page, 'McpTab/Empty');
  await expect(page).toHaveScreenshot();
});

test('McpTab 加载态视觉基线', async ({ page }) => {
  await openStory(page, 'McpTab/Loading');
  await expect(page).toHaveScreenshot();
});

test('McpTab 失败态视觉基线', async ({ page }) => {
  await openStory(page, 'McpTab/Failed');
  await expect(page).toHaveScreenshot();
});

// ===== 连接指引（G-B） =====
test('连接指引卡：端点与 Bearer 可见', async ({ page }) => {
  await openStory(page, 'McpTab/Data');
  await expect(page.locator('[data-spec="mcp-guide-card"]')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-guide-endpoint"]')).toContainText('/mcp');
  await expect(page.locator('[data-spec="mcp-guide-card"]')).toContainText('Authorization: Bearer');
  await expect(page.locator('[data-spec="mcp-guide-sample"]')).toContainText('"mcpServers"');
});

// ===== 服务器管理（G-A） =====
test('服务器表：渲染 2 行与操作按钮', async ({ page }) => {
  await openStory(page, 'McpTab/Data');
  await expect(page.locator('[data-spec="mcp-server-row"]')).toHaveCount(2);
  await expect(page.locator('[data-spec="mcp-server-create"]')).toBeVisible();
  const first = page.locator('[data-spec="mcp-server-row"]').first();
  await expect(first).toContainText('stardust-core');
  await expect(first).toContainText('默认项目');
  await expect(first.locator('[data-spec="mcp-server-toggle"]')).toBeVisible();
  await expect(first.locator('[data-spec="mcp-server-edit"]')).toBeVisible();
  await expect(first.locator('[data-spec="mcp-server-delete"]')).toBeVisible();
});

test('服务器 Drawer：点新增打开表单', async ({ page }) => {
  await openStory(page, 'McpTab/Data');
  await page.locator('[data-spec="mcp-server-create"]').click();
  await expect(page.locator('[data-spec="mcp-server-form-name"]')).toBeVisible();
});

// ===== 令牌项目过滤（G-D） =====
test('令牌项目过滤：未绑定项目只剩 1 行', async ({ page }) => {
  await openStory(page, 'McpTab/Data');
  await expect(page.locator('[data-spec="mcp-token-row"]')).toHaveCount(3);
  await page.locator('[data-spec="mcp-token-project-filter"]').selectOption({ label: '未绑定项目' });
  await expect(page.locator('[data-spec="mcp-token-row"]')).toHaveCount(1);
  await page.locator('[data-spec="mcp-token-project-filter"]').selectOption({ label: '默认项目' });
  await expect(page.locator('[data-spec="mcp-token-row"]')).toHaveCount(2);
});

// ===== 审计展开（G2 字段 Before/After） =====
test('资源授权：行内按钮打开 Drawer', async ({ page }) => {
  await openStory(page, 'McpTab/Data');
  await page.locator('[data-spec="mcp-token-resources"]').first().click();
  await expect(page.getByRole('dialog', { name: /资源白名单/ })).toBeVisible();
  await expect(page.locator('[data-spec="mcp-resource-add"]')).toBeVisible();
});

test('审计行展开：Arguments 与 Before/After 快照可见', async ({ page }) => {
  await openStory(page, 'McpTab/Data');
  await page.locator('[data-spec="mcp-audit-row"]').first().click();
  await expect(page.locator('[data-spec="mcp-audit-args"]')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-audit-before"]')).toContainText('running');
  await expect(page.locator('[data-spec="mcp-audit-after"]')).toContainText('restarting');
  await expect(page.getByText('风险级别：L2 高危')).toBeVisible();
});
