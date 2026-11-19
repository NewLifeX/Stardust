import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('Data：9 列表头（无 Id/元数据列）与行内容、工具条「新增」（CARD-GW-02）', async ({ page }) => {
  await openStory(page, 'GatewayNode/Data');
  const ths = page.locator('[data-spec="gateway-node"] thead th');
  await expect(ths).toHaveCount(9);
  await expect(ths.nth(0)).toContainText('名称');
  await expect(ths.nth(1)).toContainText('地址');
  await expect(ths.nth(2)).toContainText('所属集群');
  // C05 硬条款：健康状态列头逐字（非「健康」/「IsHealthy」）
  await expect(ths.nth(3)).toContainText('健康状态');
  await expect(ths.nth(4)).toContainText('权重');
  await expect(ths.nth(5)).toContainText('最大失败次数');
  await expect(ths.nth(6)).toContainText('失败超时');
  await expect(ths.nth(7)).toContainText('启用');
  await expect(ths.nth(8)).toContainText('操作');
  await expect(page.locator('[data-spec="gateway-node-row"]')).toHaveCount(2);
  const first = page.locator('[data-spec="gateway-node-row"]').first();
  await expect(first).toContainText('10.0.0.11:8080');
  await expect(first).toContainText('iot-cluster');
  await expect(first).toContainText('60s');
  // 行操作：编辑/删除；工具条「新增」（C03 逐字，非「新增节点」）
  await expect(page.locator('[data-spec="gateway-node-edit"]').first()).toBeVisible();
  await expect(page.locator('[data-spec="gateway-node-remove"]').first()).toBeVisible();
  await expect(page.locator('[data-spec="gateway-node-create"]')).toContainText('新增');
});

test('Data：健康三态 Tag 与启用列并存、name 缺省回退地址、集群缺省「—」', async ({ page }) => {
  await openStory(page, 'GatewayNode/Data');
  const first = page.locator('[data-spec="gateway-node-row"]').first();
  await expect(first).toContainText('运行');
  await expect(first).toContainText('启用');
  const second = page.locator('[data-spec="gateway-node-row"]').nth(1);
  // 停用 → 离线（健康三态，old-shell nodeStatus 同源）；启用列同时显示「停用」不合并
  await expect(second).toContainText('离线');
  await expect(second).toContainText('停用');
  // name 空回退地址；未归属集群显示「—」
  await expect(second).toContainText('10.0.0.12:8080');
  await expect(second).toContainText('—');
});

test('Empty：空态「还没有网关节点」+「创建节点」引导按钮（J-GW-01 C14）', async ({ page }) => {
  await openStory(page, 'GatewayNode/Empty');
  await expect(page.locator('[data-spec="gateway-node-empty"]')).toContainText('还没有网关节点');
  await expect(page.locator('[data-spec="gateway-node-empty-create"]')).toContainText('创建节点');
});

test('Loading：加载中态「节点加载中…」', async ({ page }) => {
  await openStory(page, 'GatewayNode/Loading');
  await expect(page.locator('[data-spec="gateway-node-loading"]')).toContainText('节点加载中…');
});

test('Failed：失败态「节点加载失败：{原因}」+ 重试（定稿裁定 5：节点独立三态）', async ({ page }) => {
  await openStory(page, 'GatewayNode/Failed');
  await expect(page.locator('[data-spec="gateway-node-failed"]')).toContainText('节点加载失败');
  await expect(page.locator('[data-spec="gateway-node-failed"]')).toContainText('Request failed with status 500');
  await expect(page.locator('[data-spec="gateway-node-retry"]')).toBeVisible();
});

test('Data：筛选条（集群/地址/健康/启用/时间区间注记「按更新时间过滤」/关键字；无 projectId 控件）', async ({ page }) => {
  await openStory(page, 'GatewayNode/Data');
  await expect(page.locator('[data-spec="gateway-node-filter-cluster"]')).toBeVisible();
  await expect(page.locator('[data-spec="gateway-node-filter-address"]')).toBeVisible();
  await expect(page.locator('[data-spec="gateway-node-filter-healthy"]')).toBeVisible();
  await expect(page.locator('[data-spec="gateway-node-filter-enable"]')).toBeVisible();
  await expect(page.locator('[data-spec="gateway-node-filter-dtstart"]')).toBeVisible();
  await expect(page.locator('[data-spec="gateway-node-filter-dtend"]')).toBeVisible();
  await expect(page.locator('[data-spec="gateway-node-filter-time-note"]')).toContainText('按更新时间过滤');
  await expect(page.locator('[data-spec="gateway-node-filter-q"]')).toBeVisible();
  // 节点请求永不带 projectId（GW-02 定稿裁定 1）：无项目控件
  await expect(page.locator('[data-spec="gateway-node-filter-project"]')).toHaveCount(0);
});

test('CreateDrawer：分组与默认值（权重1/最大失败3/超时60秒、启用+健康勾选、IsHealthy 旁注 C15）', async ({ page }) => {
  await openStory(page, 'GatewayNode/CreateDrawer');
  const drawer = page.locator('[data-spec="gateway-node-drawer"]');
  await expect(drawer).toContainText('新增节点');
  await expect(drawer).toContainText('基础');
  await expect(drawer).toContainText('转发与失败参数');
  await expect(drawer).toContainText('备注');
  await expect(page.locator('[data-spec="gateway-node-form-weight"]')).toHaveValue('1');
  await expect(page.locator('[data-spec="gateway-node-form-maxfails"]')).toHaveValue('3');
  await expect(page.locator('[data-spec="gateway-node-form-failtimeout"]')).toHaveValue('60');
  await expect(page.locator('[data-spec="gateway-node-form-enable"] input')).toBeChecked();
  await expect(page.locator('[data-spec="gateway-node-form-ishealthy"] input')).toBeChecked();
  await expect(page.locator('[data-spec="gateway-node-form-ishealthy-note"]')).toContainText('运行时状态，网关探测会回写，一般无需手改');
  await expect(page.locator('[data-spec="gateway-node-save"]')).toContainText('保存');
  await expect(page.locator('[data-spec="gateway-node-cancel"]')).toContainText('取消');
});

test('CreateDrawer：无集群就地提示「请先创建网关集群」并禁用保存（§3.2 分支空态）', async ({ page }) => {
  await openStory(page, 'GatewayNode/CreateDrawerNoCluster');
  await expect(page.locator('[data-spec="gateway-node-form-nocluster"]')).toContainText('请先创建网关集群');
  await expect(page.locator('[data-spec="gateway-node-save"]')).toBeDisabled();
});

test('CreateDrawer：空地址保存显示内联校验「地址不能为空」（role=alert）+ 未选集群提示', async ({ page }) => {
  await openStory(page, 'GatewayNode/CreateDrawer');
  await page.locator('[data-spec="gateway-node-save"]').click();
  await expect(page.locator('[data-spec="gateway-node-form-address-error"]')).toContainText('地址不能为空');
  await expect(page.locator('[data-spec="gateway-node-form-address-error"]')).toHaveAttribute('role', 'alert');
  await expect(page.locator('[data-spec="gateway-node-form-cluster-error"]')).toContainText('请选择所属集群');
});

test('EditDrawer：标题「编辑节点」+ 回填值 + IsHealthy 旁注可见（运行时状态如实承载）', async ({ page }) => {
  await openStory(page, 'GatewayNode/EditDrawer');
  const drawer = page.locator('[data-spec="gateway-node-drawer"]');
  await expect(drawer).toContainText('编辑节点');
  await expect(drawer).toContainText('编辑后仅提交变更字段，其余保持不变');
  await expect(page.locator('[data-spec="gateway-node-form-cluster"]')).toHaveValue('2');
  await expect(page.locator('[data-spec="gateway-node-form-name"]')).toHaveValue('api-node');
  await expect(page.locator('[data-spec="gateway-node-form-address"]')).toHaveValue('10.0.0.12:8080');
  await expect(page.locator('[data-spec="gateway-node-form-weight"]')).toHaveValue('2');
  await expect(page.locator('[data-spec="gateway-node-form-maxfails"]')).toHaveValue('5');
  await expect(page.locator('[data-spec="gateway-node-form-failtimeout"]')).toHaveValue('30');
  await expect(page.locator('[data-spec="gateway-node-form-enable"] input')).not.toBeChecked();
  await expect(page.locator('[data-spec="gateway-node-form-ishealthy"] input')).not.toBeChecked();
  await expect(page.locator('[data-spec="gateway-node-form-ishealthy-note"]')).toContainText('运行时状态，网关探测会回写，一般无需手改');
});
