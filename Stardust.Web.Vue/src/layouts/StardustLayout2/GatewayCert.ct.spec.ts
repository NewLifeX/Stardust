import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('Data：7 列表头（无 Id/有效期列）与行内容、工具条「添加证书」（CARD-GW-04）', async ({ page }) => {
  await openStory(page, 'GatewayCert/Data');
  const ths = page.locator('[data-spec="gateway-cert"] thead th');
  await expect(ths).toHaveCount(7);
  await expect(ths.nth(0)).toContainText('域名');
  await expect(ths.nth(1)).toContainText('启用');
  await expect(ths.nth(2)).toContainText('PFX文件');
  await expect(ths.nth(3)).toContainText('证书文件');
  await expect(ths.nth(4)).toContainText('私钥文件');
  await expect(ths.nth(5)).toContainText('PEM文件');
  await expect(ths.nth(6)).toContainText('操作');
  // 不应有有效期列（定稿裁定 4：NotAfter 收行内展开）
  await expect(page.locator('[data-spec="gateway-cert"] thead th', { hasText: '有效期至' })).toHaveCount(0);
  await expect(page.locator('[data-spec="gateway-cert-row"]')).toHaveCount(2);
  const first = page.locator('[data-spec="gateway-cert-row"]').first();
  await expect(first).toContainText('iot.example.com');
  await expect(first).toContainText('/data/certs/iot.example.com.pfx');
  await expect(first).toContainText('/data/certs/iot.example.com.crt');
  await expect(first).toContainText('/data/certs/iot.example.com.key');
  // 行操作：有效期展开 + 编辑/删除；工具条「添加证书」
  await expect(page.locator('[data-spec="gateway-cert-expand"]').first()).toContainText('有效期');
  await expect(page.locator('[data-spec="gateway-cert-edit"]').first()).toBeVisible();
  await expect(page.locator('[data-spec="gateway-cert-remove"]').first()).toBeVisible();
  await expect(page.locator('[data-spec="gateway-cert-create"]')).toContainText('添加证书');
});

test('Data：启用/停用 Tag、取不到的文件路径显示「—」、有效期缺值不造数', async ({ page }) => {
  await openStory(page, 'GatewayCert/Data');
  const first = page.locator('[data-spec="gateway-cert-row"]').first();
  await expect(first).toContainText('启用');
  const second = page.locator('[data-spec="gateway-cert-row"]').nth(1);
  await expect(second).toContainText('停用');
  await expect(second).toContainText('—');
  // 第二行有效期缺值（notAfterMs=0 降级）：行内不渲染日期（定稿裁定 4）
  await expect(second).not.toContainText('2026-');
});

test('Empty：空态「还没有网关证书」+「添加证书」引导按钮（J-GW-01 C16）', async ({ page }) => {
  await openStory(page, 'GatewayCert/Empty');
  await expect(page.locator('[data-spec="gateway-cert-empty"]')).toContainText('还没有网关证书');
  await expect(page.locator('[data-spec="gateway-cert-empty-create"]')).toContainText('添加证书');
});

test('Loading：加载中态「证书加载中…」', async ({ page }) => {
  await openStory(page, 'GatewayCert/Loading');
  await expect(page.locator('[data-spec="gateway-cert-loading"]')).toContainText('证书加载中…');
});

test('Failed：失败态「证书加载失败：{原因}」+ 重试（接口级归因拆分）', async ({ page }) => {
  await openStory(page, 'GatewayCert/Failed');
  await expect(page.locator('[data-spec="gateway-cert-failed"]')).toContainText('证书加载失败');
  await expect(page.locator('[data-spec="gateway-cert-failed"]')).toContainText('Request failed with status 500');
  await expect(page.locator('[data-spec="gateway-cert-retry"]')).toBeVisible();
});

test('Data：筛选条（启用开关、时间区间、注记「按有效期至（NotAfter）过滤」、关键字；无域名控件）', async ({ page }) => {
  await openStory(page, 'GatewayCert/Data');
  await expect(page.locator('[data-spec="gateway-cert-filter-enable"]')).toBeVisible();
  await expect(page.locator('[data-spec="gateway-cert-filter-dtstart"]')).toBeVisible();
  await expect(page.locator('[data-spec="gateway-cert-filter-dtend"]')).toBeVisible();
  await expect(page.locator('[data-spec="gateway-cert-filter-time-note"]')).toContainText('按有效期至（NotAfter）过滤');
  await expect(page.locator('[data-spec="gateway-cert-filter-q"]')).toBeVisible();
  // 不复刻后端未实现的「域名搜索」为假控件（定稿裁量 2）
  await expect(page.locator('[data-spec="gateway-cert-filter-domain"]')).toHaveCount(0);
});

test('CreateDrawer：分组与默认值（启用勾选、自动续期不勾、提前 30 天；文件输入带 title）', async ({ page }) => {
  await openStory(page, 'GatewayCert/CreateDrawer');
  const drawer = page.locator('[data-spec="gateway-cert-drawer"]');
  await expect(drawer).toContainText('新增证书');
  await expect(drawer).toContainText('基础');
  await expect(drawer).toContainText('证书文件');
  await expect(drawer).toContainText('证书信息');
  await expect(drawer).toContainText('备注');
  await expect(page.locator('[data-spec="gateway-cert-form-enable"] input')).toBeChecked();
  await expect(page.locator('[data-spec="gateway-cert-form-autorenew"] input')).not.toBeChecked();
  await expect(page.locator('[data-spec="gateway-cert-form-renewdays"]')).toHaveValue('30');
  // 文件路径输入 title 统一（WebApi 无上传端点，纯路径输入，卡片回填 §6.2）
  await expect(page.locator('[data-spec="gateway-cert-form-pfxfile"]')).toHaveAttribute('title', '填写服务器上的证书文件路径');
  // PfxPassword 为 password 型（硬条款②）；新建态无「编辑不回显」说明
  await expect(page.locator('[data-spec="gateway-cert-form-pfxpassword"]')).toHaveAttribute('type', 'password');
  await expect(page.locator('[data-spec="gateway-cert-form-pfxpassword-note"]')).toHaveCount(0);
  // 证书信息组默认折叠
  await expect(page.locator('[data-spec="gateway-cert-form-certinfo"]')).toBeHidden();
  await expect(page.locator('[data-spec="gateway-cert-save"]')).toContainText('保存');
  await expect(page.locator('[data-spec="gateway-cert-cancel"]')).toContainText('取消');
});

test('CreateDrawer：空域名保存显示内联校验「域名不能为空」（role=alert）；展开证书信息组', async ({ page }) => {
  await openStory(page, 'GatewayCert/CreateDrawer');
  await page.locator('[data-spec="gateway-cert-save"]').click();
  await expect(page.locator('[data-spec="gateway-cert-form-domain-error"]')).toContainText('域名不能为空');
  await expect(page.locator('[data-spec="gateway-cert-form-domain-error"]')).toHaveAttribute('role', 'alert');
  await page.locator('[data-spec="gateway-cert-form-certinfo-toggle"]').click();
  await expect(page.locator('[data-spec="gateway-cert-form-issuer"]')).toBeVisible();
  await expect(page.locator('[data-spec="gateway-cert-form-notafter"]')).toBeVisible();
});

test('EditDrawer：标题「编辑证书」+ PFX 密码不回显（空值 + password 型 + 不回显说明）', async ({ page }) => {
  await openStory(page, 'GatewayCert/EditDrawer');
  await expect(page.locator('[data-spec="gateway-cert-drawer"]')).toContainText('编辑证书');
  await expect(page.locator('[data-spec="gateway-cert-drawer"]')).toContainText('编辑后仅变更字段，其余保持不变');
  // 硬条款③：编辑不回显——输入恒空；type=password（硬条款②）；说明可见
  await expect(page.locator('[data-spec="gateway-cert-form-pfxpassword"]')).toHaveValue('');
  await expect(page.locator('[data-spec="gateway-cert-form-pfxpassword"]')).toHaveAttribute('type', 'password');
  await expect(page.locator('[data-spec="gateway-cert-form-pfxpassword-note"]')).toContainText('PFX 密码编辑不回显，留空表示不修改');
  // 回填的证书信息在折叠组里（展开后可见）
  await page.locator('[data-spec="gateway-cert-form-certinfo-toggle"]').click();
  await expect(page.locator('[data-spec="gateway-cert-form-domain"]')).toHaveValue('iot.example.com');
  await expect(page.locator('[data-spec="gateway-cert-form-notafter"]')).toHaveValue('2026-12-31');
  await expect(page.locator('[data-spec="gateway-cert-form-issuer"]')).toHaveValue('CN=Example CA');
});

test('Expanded：行内展开显示有效期至/生效/颁发者/指纹（懒拉 Detail 承载，定稿裁定 4）', async ({ page }) => {
  await openStory(page, 'GatewayCert/Expanded');
  // 展开按钮处于 aria-expanded 状态
  const btn = page.locator('[data-spec="gateway-cert-expand"]').first();
  await expect(btn).toHaveAttribute('aria-expanded', 'true');
  // 详情区（独立 data-spec，避免与按钮同名 strict 冲突）
  const detail = page.locator('[data-spec="gateway-cert-expand-detail"]');
  await expect(detail).toBeVisible();
  await expect(detail).toContainText('有效期至');
  await expect(detail).toContainText('2026-12-31');
  await expect(detail).toContainText('生效');
  await expect(detail).toContainText('CN=Example CA');
  await expect(detail).toContainText('AB12CD34EF56');
});
