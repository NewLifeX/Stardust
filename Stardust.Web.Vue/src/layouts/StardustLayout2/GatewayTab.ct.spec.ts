import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('Data：路由名+域名副行，集群名解析，通配证书显示 TLS 域', async ({ page }) => {
  await openStory(page, 'GatewayTab/Data');
  await expect(page.locator('[data-spec="gateway-row"]')).toHaveCount(3);
  const first = page.locator('[data-spec="gateway-row"]').first();
  await expect(first).toContainText('iot-api');
  await expect(first).toContainText('DeviceService');
  await expect(first).toContainText('iot.example.com');
  // 无 ClusterId 解析到集群的行显示待接入；无证书的行 TLS 待接入
  await expect(page.locator('[data-spec="gateway-row"]').nth(2)).toContainText('待接入');
});

test('Empty：无路由显示空态', async ({ page }) => {
  await openStory(page, 'GatewayTab/Empty');
  await expect(page.locator('[data-spec="gateway-empty"]')).toContainText('还没有配置网关路由');
  await expect(page.locator('[data-spec="gateway-row"]')).toHaveCount(0);
});

test('Loading：spinner 加载中', async ({ page }) => {
  await openStory(page, 'GatewayTab/Loading');
  await expect(page.locator('[data-spec="gateway-loading"]')).toContainText('路由加载中');
});

test('Failed：请求失败显示错误与重试', async ({ page }) => {
  await openStory(page, 'GatewayTab/Failed');
  await expect(page.locator('[data-spec="gateway-failed"]')).toContainText('网关路由加载失败');
  await expect(page.locator('[data-spec="gateway-retry"]')).toBeVisible();
});

test('Data：工具条「新增路由」与行操作「编辑/删除」可见（CARD-GW-03）', async ({ page }) => {
  await openStory(page, 'GatewayTab/Data');
  await expect(page.locator('[data-spec="gateway-route-create"]')).toContainText('新增路由');
  await expect(page.locator('[data-spec="gateway-route-edit"]').first()).toBeVisible();
  await expect(page.locator('[data-spec="gateway-route-remove"]').first()).toBeVisible();
  // 行操作列在既有 5 列之后（colspan=6 承载空态行）
  const ths = page.locator('[data-spec="gateway-tab"] thead th');
  await expect(ths).toHaveCount(6);
  await expect(ths.nth(5)).toContainText('操作');
});

test('Data：筛选条常用项可见，「更多筛选」折叠展开显示低频项（J-GW-04 C21）', async ({ page }) => {
  await openStory(page, 'GatewayTab/Data');
  await expect(page.locator('[data-spec="gateway-route-filter-enable"]')).toBeVisible();
  await expect(page.locator('[data-spec="gateway-route-filter-cluster"]')).toBeVisible();
  await expect(page.locator('[data-spec="gateway-route-filter-domain"]')).toBeVisible();
  await expect(page.locator('[data-spec="gateway-route-filter-q"]')).toBeVisible();
  // 折叠区默认不显示
  await expect(page.locator('[data-spec="gateway-route-filter-more-open"]')).toHaveCount(0);
  await page.locator('[data-spec="gateway-route-filter-more"]').click();
  await expect(page.locator('[data-spec="gateway-route-filter-more-open"]')).toBeVisible();
  await expect(page.locator('[data-spec="gateway-route-filter-priority"]')).toBeVisible();
  await expect(page.locator('[data-spec="gateway-route-filter-stripprefix"]')).toBeVisible();
  await expect(page.locator('[data-spec="gateway-route-filter-dtstart"]')).toBeVisible();
  await expect(page.locator('[data-spec="gateway-route-filter-dtend"]')).toBeVisible();
  await expect(page.locator('[data-spec="gateway-route-filter-time-note"]')).toContainText('按更新时间过滤');
});

test('Loading/Failed：筛选条不显示', async ({ page }) => {
  await openStory(page, 'GatewayTab/Loading');
  await expect(page.locator('[data-spec="gateway-route-filter"]')).toHaveCount(0);
  await openStory(page, 'GatewayTab/Failed');
  await expect(page.locator('[data-spec="gateway-route-filter"]')).toHaveCount(0);
});

test('CreateDrawer：分组标题与必填标记、静态文件组默认置灰（CARD-GW-03）', async ({ page }) => {
  await openStory(page, 'GatewayTab/CreateDrawer');
  await expect(page.locator('[data-spec="gateway-route-drawer"]')).toContainText('新增路由');
  await expect(page.locator('[data-spec="gateway-route-drawer"]')).toContainText('基础');
  await expect(page.locator('[data-spec="gateway-route-drawer"]')).toContainText('匹配');
  await expect(page.locator('[data-spec="gateway-route-drawer"]')).toContainText('转发');
  await expect(page.locator('[data-spec="gateway-route-drawer"]')).toContainText('静态文件');
  await expect(page.locator('[data-spec="gateway-route-form-name"]')).toBeVisible();
  await expect(page.locator('[data-spec="gateway-route-form-staticroot"]')).toBeDisabled();
  // 静态组置灰（opacity-50 容器）
  await expect(page.locator('[data-spec="gateway-route-drawer"]')).toContainText('去除前缀 StripPrefix');
  await expect(page.locator('[data-spec="gateway-route-save"]')).toContainText('保存');
  await expect(page.locator('[data-spec="gateway-route-cancel"]')).toContainText('取消');
});

test('CreateDrawer：开启静态路由后 StaticRoot 解除置灰（默认置灰态解除）', async ({ page }) => {
  await openStory(page, 'GatewayTab/CreateDrawer');
  await page.locator('[data-spec="gateway-route-form-isstatic"] input').check();
  await expect(page.locator('[data-spec="gateway-route-form-staticroot"]')).toBeEnabled();
});

test('CreateDrawer：空名称保存显示内联校验「名称不能为空」（role=alert）', async ({ page }) => {
  await openStory(page, 'GatewayTab/CreateDrawer');
  await page.locator('[data-spec="gateway-route-save"]').click();
  await expect(page.locator('[data-spec="gateway-route-form-name-error"]')).toContainText('名称不能为空');
  await expect(page.locator('[data-spec="gateway-route-form-name-error"]')).toHaveAttribute('role', 'alert');
});

test('Data：集群区块挂载于路由区块之下，无数据时显示空态引导（CARD-GW-01 多区块壳）', async ({ page }) => {
  await openStory(page, 'GatewayTab/Data');
  // 路由区块形态不回归
  await expect(page.locator('[data-spec="gateway-row"]')).toHaveCount(3);
  // 集群区块挂载（story 未传 clusterRows → 空态引导）
  await expect(page.locator('[data-spec="gateway-cluster"]')).toBeVisible();
  await expect(page.locator('[data-spec="gateway-cluster-empty"]')).toContainText('还没有网关集群');
  await expect(page.locator('[data-spec="gateway-cluster-empty-create"]')).toContainText('创建集群');
});

test('Data：证书区块挂载于集群区块之下，行内容与工具条可见（CARD-GW-04 多区块壳）', async ({ page }) => {
  await openStory(page, 'GatewayTab/Data');
  // 路由区块形态不回归（上面用例同断言，双保险防插入顺序破坏）
  await expect(page.locator('[data-spec="gateway-row"]')).toHaveCount(3);
  // 证书区块挂载在文档流中位于集群区块之后
  const certBlock = page.locator('[data-spec="gateway-cert"]');
  await expect(certBlock).toBeVisible();
  const clusterBox = await page.locator('[data-spec="gateway-cluster"]').boundingBox();
  const certBox = await certBlock.boundingBox();
  expect(certBox && clusterBox && certBox.y > clusterBox.y).toBeTruthy();
  // 7 列 + 2 行数据（story 传入 certRows）
  const ths = certBlock.locator('thead th');
  await expect(ths).toHaveCount(7);
  await expect(ths.nth(0)).toContainText('域名');
  await expect(ths.nth(6)).toContainText('操作');
  await expect(certBlock.locator('[data-spec="gateway-cert-row"]')).toHaveCount(2);
  const first = certBlock.locator('[data-spec="gateway-cert-row"]').first();
  await expect(first).toContainText('iot.example.com');
  await expect(first).toContainText('启用');
  await expect(first).toContainText('/data/certs/iot.example.com.pfx');
  await expect(certBlock.locator('[data-spec="gateway-cert-create"]')).toContainText('添加证书');
});

test('Data：节点区块挂载于集群与证书之间，9 列 2 行与工具条「新增」可见（CARD-GW-02 多区块壳）', async ({ page }) => {
  await openStory(page, 'GatewayTab/Data');
  // 节点区块挂载在文档流中位于集群之后、证书之前（区块顺序：路由→集群→节点→证书末位）
  const nodeBlock = page.locator('[data-spec="gateway-node"]');
  await expect(nodeBlock).toBeVisible();
  const clusterBox = await page.locator('[data-spec="gateway-cluster"]').boundingBox();
  const nodeBox = await nodeBlock.boundingBox();
  const certBox = await page.locator('[data-spec="gateway-cert"]').boundingBox();
  expect(nodeBox && clusterBox && nodeBox.y > clusterBox.y).toBeTruthy();
  expect(certBox && nodeBox && certBox.y > nodeBox.y).toBeTruthy();
  // 9 列（健康状态列头逐字）+ 2 行数据（story 传入 nodeRows）
  const ths = nodeBlock.locator('thead th');
  await expect(ths).toHaveCount(9);
  await expect(ths.nth(3)).toContainText('健康状态');
  await expect(nodeBlock.locator('[data-spec="gateway-node-row"]')).toHaveCount(2);
  const first = nodeBlock.locator('[data-spec="gateway-node-row"]').first();
  await expect(first).toContainText('10.0.0.11:8080');
  await expect(first).toContainText('DeviceService');
  await expect(first).toContainText('运行');
  await expect(nodeBlock.locator('[data-spec="gateway-node-create"]')).toContainText('新增');
});
