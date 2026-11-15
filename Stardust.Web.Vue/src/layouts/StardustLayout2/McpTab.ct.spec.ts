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
// 注意：服务器区块是「高级」折叠区（serversSectionOpen 默认 false，PLT-2 起），用例先点开再断言。
test('服务器区块默认折叠：开关与提示可见，无表格行', async ({ page }) => {
  await openStory(page, 'McpTab/Data');
  await expect(page.locator('[data-spec="mcp-server-section-toggle"]')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-server-section-toggle"]')).toContainText('2 个 · 高级');
  await expect(page.getByText('登记外部 MCP 端点时使用')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-server-row"]')).toHaveCount(0);
});

test('服务器表：渲染 2 行与操作按钮', async ({ page }) => {
  await openStory(page, 'McpTab/Data');
  await page.locator('[data-spec="mcp-server-section-toggle"]').click();
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
  await page.locator('[data-spec="mcp-server-section-toggle"]').click();
  await page.locator('[data-spec="mcp-server-create"]').click();
  await expect(page.locator('[data-spec="mcp-server-form-name"]')).toBeVisible();
});

// ===== 服务器卡头查询条与导出组（CARD-PLT-03） =====
test('服务器卡头查询条：enable 真下拉+date 双框+关键字手输+apply；reset 查询后才浮现', async ({ page }) => {
  await openStory(page, 'McpTab/Data');
  await page.locator('[data-spec="mcp-server-section-toggle"]').click();
  // C11 防假（PLT-03）：enable=select 真下拉（3 选项）、Q=手输入框（非假下拉）、dt=原生 date 双框
  const enableSel = page.locator('select[data-spec="mcp-server-query-enable"]');
  await expect(enableSel).toHaveCount(1);
  await expect(enableSel.locator('option')).toHaveCount(3);
  await expect(page.locator('[data-spec="mcp-server-query-dt"] input[type="date"]')).toHaveCount(2);
  await expect(page.locator('input[data-spec="mcp-server-query-q"]')).toHaveCount(1);
  await expect(page.locator('[data-spec="mcp-server-query-apply"]')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-server-query-reset"]')).toHaveCount(0);
});

test('工具数列 0 快照显示「—」占位，有快照显示数字（-7 如实核对）', async ({ page }) => {
  await openStory(page, 'McpTab/Data');
  await page.locator('[data-spec="mcp-server-section-toggle"]').click();
  const fsRow = page.locator('[data-spec="mcp-server-row"]', { hasText: 'filesystem' });
  await expect(fsRow).toHaveCount(1);
  // 列序：服务器/URL/归属项目/状态/工具数——tools:0 → 工具数列「—」；projectId:0 → 归属项目列同「—」
  await expect(fsRow.locator('td').nth(4)).toHaveText('—');
  await expect(fsRow.getByText('—')).toHaveCount(2);
  const coreRow = page.locator('[data-spec="mcp-server-row"]', { hasText: 'stardust-core' });
  await expect(coreRow.locator('td').nth(4)).toHaveText('18');
  await expect(coreRow.getByText('—')).toHaveCount(0);
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
test('令牌编辑 Drawer：标题编辑态且无 Token 字段与资源勾选区（-12 预选缺防假）', async ({ page }) => {
  await openStory(page, 'McpTab/Data');
  await page.locator('[data-spec="mcp-token-edit"]').first().click();
  // 标题为「编辑」而非「新增」，证明打开的是真编辑 Drawer
  await expect(page.locator('h2', { hasText: '编辑 MCP 令牌' })).toBeVisible();
  // C11 防假（CARD-PLT-04 -12）：编辑表单无 Token 明文字段、无资源勾选区（存量授权在资源白名单 Drawer 查看，-10/-11【弃】维持）
  await expect(page.locator('[data-spec="mcp-form-token"]')).toHaveCount(0);
  // 资源勾选区=授权面板/行，仅在资源 Drawer 打开时渲染（mcp-resource-drawer 为 Drawer 常驻外层壳，不在断言面）
  await expect(page.locator('[data-spec="mcp-resource-panel"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="mcp-resource-row"]')).toHaveCount(0);
});

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

// ===== 审计历史检索（PLT-02：动作名/关键字条件 + 导出按钮组） =====
test('审计历史检索：动作名与关键字输入、导出按钮组可见', async ({ page }) => {
  await openStory(page, 'McpTab/Data');
  await page.locator('[data-spec="mcp-audit-history"]').click();
  await expect(page.locator('[data-spec="mcp-audit-tool"]')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-audit-action-name"]')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-audit-keyword"]')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-audit-export-excel"]')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-audit-export-json"]')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-audit-query"]')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-audit-back"]')).toBeVisible();
  // C11 防假（PLT-02）：工具/动作名/关键字仍是手输入框，不得假借下拉（后端无枚举端点，PLT-02 -7 差距记档不做）
  await expect(page.locator('input[data-spec="mcp-audit-tool"]')).toHaveCount(1);
  await expect(page.locator('input[data-spec="mcp-audit-action-name"]')).toHaveCount(1);
  await expect(page.locator('input[data-spec="mcp-audit-keyword"]')).toHaveCount(1);
});

// ===== 服务器卡头查询条（PLT-03：enable/dt/Q 条件 + 导出按钮组） =====
// CT 宿主请求层桩（ct/mocks/request.ts）固定 resolve 空数据且不发网络（page.route 拦不到）——
// 点击查询即「服务端返回空列表」路径：计数 0 + 空态文案 + 返回全部；失败文案路径由 mesh.logic 单测覆盖（桩下 CT 不可达）。
test('服务器查询条：条件控件与导出按钮组可见，查询生效空态不静默', async ({ page }) => {
  await openStory(page, 'McpTab/Data');
  await page.locator('[data-spec="mcp-server-section-toggle"]').click();
  await expect(page.locator('[data-spec="mcp-server-query-enable"]')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-server-query-dt"]')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-server-query-q"]')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-server-query-apply"]')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-server-export-excel"]')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-server-export-json"]')).toBeVisible();
  // 查询（桩返回空数据）：计数变 0 + 空态文案（不静默）+ 返回全部出现
  await page.locator('[data-spec="mcp-server-query-apply"]').click();
  await expect(page.getByRole('heading', { name: /远端 MCP 服务器 0 个/ })).toBeVisible();
  await expect(page.getByText('无匹配的服务器。可调整查询条件后重试')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-server-query-reset"]')).toBeVisible();
  // 返回全部：清条件回落 story props 列表（2 行）
  await page.locator('[data-spec="mcp-server-query-reset"]').click();
  await expect(page.getByRole('heading', { name: /远端 MCP 服务器 2 个/ })).toBeVisible();
});

// ===== 令牌卡头查询条（PLT-04：token/enable/dt/Q 服务端过滤 + 客户端项目过滤叠加） =====
// CT 宿主请求层桩（ct/mocks/request.ts）默认 resolve 空数据且不发网络（page.route 拦不到）——
// 查询即「服务端返回空列表」路径：计数 0 + 空态文案 + 返回全部。
// 失败路径用 window.__ctRequestImpl 注入（lead 2026-11-09 批准：新用例可用，注册-清理对称，存量基线不回溯）。
test('令牌查询条：条件控件可见，查询生效空态不静默，失败注入可见提示', async ({ page }) => {
  await openStory(page, 'McpTab/Data');
  await expect(page.locator('[data-spec="mcp-token-query-token"]')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-token-query-enable"]')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-token-query-dt"]')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-token-query-q"]')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-token-query-apply"]')).toBeVisible();
  // 查询（桩返回空数据）：计数变 0 + 查询态空态文案（不静默）+ 返回全部出现
  await page.locator('[data-spec="mcp-token-query-apply"]').click();
  await expect(page.getByRole('heading', { name: /MCP 令牌 0 枚/ })).toBeVisible();
  await expect(page.getByText('当前查询条件下没有令牌')).toBeVisible();
  await expect(page.locator('[data-spec="mcp-token-query-reset"]')).toBeVisible();
  // 返回全部：清条件回落 story props 列表（3 行）
  await page.locator('[data-spec="mcp-token-query-reset"]').click();
  await expect(page.getByRole('heading', { name: /MCP 令牌 3 枚/ })).toBeVisible();
  // 失败路径（override 注入）：注册→查询→错误文案可见→收尾拔除（注册-清理对称，防跨用例污染）
  await page.evaluate(() => {
    (window as unknown as { __ctRequestImpl: unknown }).__ctRequestImpl = async () => {
      throw new Error('网络中断');
    };
  });
  try {
    await page.locator('[data-spec="mcp-token-query-apply"]').click();
    await expect(page.locator('[data-spec="mcp-token-query-error"]')).toContainText('令牌查询失败：网络中断');
  } finally {
    await page.evaluate(() => {
      delete (window as unknown as { __ctRequestImpl?: unknown }).__ctRequestImpl;
    });
  }
});
