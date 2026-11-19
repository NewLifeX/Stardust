import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('Data：9 列表头与行内容（名称/项目/负载均衡中文/健康检查主副行/会话保持/启用/在线节点/更新时间/操作）', async ({ page }) => {
  await openStory(page, 'GatewayCluster/Data');
  const ths = page.locator('[data-spec="gateway-cluster"] thead th');
  await expect(ths).toHaveCount(9);
  await expect(ths.nth(0)).toContainText('名称');
  await expect(ths.nth(6)).toContainText('在线节点');
  await expect(ths.nth(8)).toContainText('操作');
  await expect(page.locator('[data-spec="gateway-cluster-row"]')).toHaveCount(2);
  const first = page.locator('[data-spec="gateway-cluster-row"]').first();
  await expect(first).toContainText('DeviceService');
  await expect(first).toContainText('演示项目');
  await expect(first).toContainText('轮询（RoundRobin）');
  await expect(first).toContainText('10s / 3000ms');
  await expect(first).toContainText('/health');
  await expect(first).toContainText('2026-11-07 10:30');
  // 行操作
  await expect(page.locator('[data-spec="gateway-cluster-edit"]').first()).toBeVisible();
  await expect(page.locator('[data-spec="gateway-cluster-remove"]').first()).toBeVisible();
  // 工具条
  await expect(page.locator('[data-spec="gateway-cluster-create"]')).toContainText('新增集群');
});

test('Data：ActiveNodes 取不到显示「—」，会话保持 Tag 开/关各一行（定稿裁定 3 不造数）', async ({ page }) => {
  await openStory(page, 'GatewayCluster/Data');
  const second = page.locator('[data-spec="gateway-cluster-row"]').nth(1);
  await expect(second).toContainText('—');
  await expect(second).toContainText('最少连接（LeastConnection）');
  // story：row2 sessionSticky=true 显「开启」，row1 显「关闭」
  await expect(second).toContainText('开启');
  await expect(page.locator('[data-spec="gateway-cluster-row"]').first()).toContainText('关闭');
});

test('Empty：空态「还没有网关集群」+「创建集群」引导按钮（J-GW-02 C18）', async ({ page }) => {
  await openStory(page, 'GatewayCluster/Empty');
  await expect(page.locator('[data-spec="gateway-cluster-empty"]')).toContainText('还没有网关集群');
  await expect(page.locator('[data-spec="gateway-cluster-empty-create"]')).toContainText('创建集群');
});

test('Loading：加载中态', async ({ page }) => {
  await openStory(page, 'GatewayCluster/Loading');
  await expect(page.locator('[data-spec="gateway-cluster-loading"]')).toContainText('集群加载中…');
});

test('Failed：失败态「集群加载失败：{原因}」+ 重试（J-GW-02 C13 自身错误）', async ({ page }) => {
  await openStory(page, 'GatewayCluster/Failed');
  await expect(page.locator('[data-spec="gateway-cluster-failed"]')).toContainText('集群加载失败');
  await expect(page.locator('[data-spec="gateway-cluster-retry"]')).toBeVisible();
});

test('Data：筛选条（启用/会话保持开关、时间区间注记「按更新时间过滤」、关键字）', async ({ page }) => {
  await openStory(page, 'GatewayCluster/Data');
  await expect(page.locator('[data-spec="gateway-cluster-filter-enable"]')).toBeVisible();
  await expect(page.locator('[data-spec="gateway-cluster-filter-session"]')).toBeVisible();
  await expect(page.locator('[data-spec="gateway-cluster-filter-dtstart"]')).toBeVisible();
  await expect(page.locator('[data-spec="gateway-cluster-filter-dtend"]')).toBeVisible();
  await expect(page.locator('[data-spec="gateway-cluster-filter-time-note"]')).toContainText('按更新时间过滤');
  await expect(page.locator('[data-spec="gateway-cluster-filter-q"]')).toBeVisible();
});

test('CreateDrawer：分组与默认值（LoadBalance 轮询、间隔 10、超时 3000、启用勾选）', async ({ page }) => {
  await openStory(page, 'GatewayCluster/CreateDrawer');
  const drawer = page.locator('[data-spec="gateway-cluster-drawer"]');
  await expect(drawer).toContainText('新增集群');
  await expect(drawer).toContainText('基础');
  await expect(drawer).toContainText('健康检查');
  await expect(drawer).toContainText('会话保持');
  await expect(drawer).toContainText('备注');
  await expect(page.locator('[data-spec="gateway-cluster-form-name"]')).toBeVisible();
  await expect(page.locator('[data-spec="gateway-cluster-form-healthpath"]')).toHaveAttribute('placeholder', '/health');
  await expect(page.locator('[data-spec="gateway-cluster-form-healthinterval"]')).toHaveValue('10');
  await expect(page.locator('[data-spec="gateway-cluster-form-healthtimeout"]')).toHaveValue('3000');
  await expect(page.locator('[data-spec="gateway-cluster-form-unhealthy"]')).toHaveValue('3');
  await expect(page.locator('[data-spec="gateway-cluster-form-healthy"]')).toHaveValue('2');
  await expect(page.locator('[data-spec="gateway-cluster-form-enable"] input')).toBeChecked();
  // 会话保持标识默认隐藏（开关关闭）
  await expect(page.locator('[data-spec="gateway-cluster-form-sessionstickyname"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="gateway-cluster-save"]')).toContainText('保存');
  await expect(page.locator('[data-spec="gateway-cluster-cancel"]')).toContainText('取消');
});

test('CreateDrawer：空名称保存显示内联校验「名称不能为空」（role=alert）；开启会话保持显示标识输入', async ({ page }) => {
  await openStory(page, 'GatewayCluster/CreateDrawer');
  await page.locator('[data-spec="gateway-cluster-save"]').click();
  await expect(page.locator('[data-spec="gateway-cluster-form-name-error"]')).toContainText('名称不能为空');
  await expect(page.locator('[data-spec="gateway-cluster-form-name-error"]')).toHaveAttribute('role', 'alert');
  await page.locator('[data-spec="gateway-cluster-form-sessionsticky"] input').check();
  await expect(page.locator('[data-spec="gateway-cluster-form-sessionstickyname"]')).toBeVisible();
});

test('EditDrawer：标题「编辑集群」+ 变更字段副注', async ({ page }) => {
  await openStory(page, 'GatewayCluster/EditDrawer');
  await expect(page.locator('[data-spec="gateway-cluster-drawer"]')).toContainText('编辑集群');
  await expect(page.locator('[data-spec="gateway-cluster-drawer"]')).toContainText('编辑后仅提交变更字段，其余保持不变');
  await expect(page.locator('[data-spec="gateway-cluster-form-sessionstickyname"]')).toBeVisible();
});
