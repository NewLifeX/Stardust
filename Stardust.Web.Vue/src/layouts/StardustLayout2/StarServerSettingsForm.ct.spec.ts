import { expect, test, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

// ===== 视觉基线 =====
test('Loaded：完整表单视觉基线（分组+字段回显+密钥mask+只读）', async ({ page }) => {
  await openStory(page, 'StarServerSettingsForm/Loaded');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('Error：加载失败错误态不静默（视觉基线）', async ({ page }) => {
  await openStory(page, 'StarServerSettingsForm/Error');
  await expect(page.locator('[data-spec="star-server-form-error"]')).toContainText('平台设置加载失败：网络中断');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

// ===== 语义断言（lead 指定 specs：star-server-form / -secret-TokenSecret / -ro-McpActionSet） =====
test('根节点渲染，10 个分组按数组序（端口与注册首、只读展示末）', async ({ page }) => {
  await openStory(page, 'StarServerSettingsForm/Loaded');
  await expect(page.locator('[data-spec="star-server-form"]')).toBeVisible();
  const groups = page.locator('[data-spec^="star-server-form-group-"]');
  await expect(groups).toHaveCount(10);
  await expect(page.locator('[data-spec="star-server-form-group-端口与注册"]')).toBeVisible();
  await expect(page.locator('[data-spec="star-server-form-group-只读展示"]')).toBeVisible();
});

test('字段回显：Port 数值、TokenSecret 不回显原值（留空=保持不变）、McpActionSet 只读纯文本', async ({ page }) => {
  await openStory(page, 'StarServerSettingsForm/Loaded');
  const port = page.locator('[data-spec="star-server-form-field-Port"]');
  await expect(port).toHaveValue('6680');
  // 密钥 mask：不回显原值，placeholder 引导留空语义
  const secret = page.locator('[data-spec="star-server-form-field-TokenSecret"]');
  await expect(secret).toHaveValue('');
  await expect(secret).toHaveAttribute('placeholder', '已配置（留空保持不变）');
  // 只读：纯文本容器，无任何可编辑 input
  const ro = page.locator('[data-spec="star-server-form-field-McpActionSet"]');
  await expect(ro).toContainText('node/app/config/deploy/gateway/monitor/system');
  await expect(ro.locator('input')).toHaveCount(0);
  // 无改动时保存禁用（只读字段不产生改动）
  await expect(page.locator('[data-spec="star-server-form-save"]')).toBeDisabled();
  await expect(page.locator('[data-spec="star-server-form-no-change"]')).toContainText('暂无改动');
});

test('枚举控件：ReRegister 五个数值选项与后端枚举一致', async ({ page }) => {
  await openStory(page, 'StarServerSettingsForm/Loaded');
  const sel = page.locator('[data-spec="star-server-form-field-ReRegister"]');
  const options = sel.locator('option');
  await expect(options).toHaveCount(5);
  await expect(options.nth(0)).toHaveAttribute('value', '0');
  await expect(options.nth(4)).toHaveAttribute('value', '4');
  await expect(sel).toHaveValue('1'); // RAW ReRegister=1（开放）
});

// ===== 保存链路（全量覆盖语义：raw 原样回传 + patch 双 key + 密钥留空不进 patch） =====
test('保存链路：改动 Port 全量回存，密钥留空 = raw 原值保持不变', async ({ page }) => {
  await openStory(page, 'StarServerSettingsForm/Loaded');
  await page.locator('[data-spec="star-server-form-field-Port"]').fill('6601');
  await expect(page.locator('[data-spec="star-server-form-save"]')).toBeEnabled();
  await page.locator('[data-spec="star-server-form-save"]').click();
  // GuardModal 警示：全量覆盖 + 密钥留空语义 + 轮换后果
  await expect(page.locator('[data-spec="guard-modal"]')).toBeVisible();
  await expect(page.locator('[data-spec="guard-detail"]')).toContainText('密钥留空 = 保持不变');
  await page.locator('[data-spec="guard-confirm"]').click();
  // 成功后确认弹窗关闭
  await expect(page.locator('[data-spec="guard-modal"]')).toHaveCount(0);
  const calls = await page.evaluate(
    () => (globalThis as { __starServerCalls?: { method?: string; data?: Record<string, unknown> }[] }).__starServerCalls,
  );
  const put = (calls ?? []).find((c) => c.method === 'PUT');
  expect(put, 'PUT /Platform/StarServer 已发出').toBeTruthy();
  const body = put?.data ?? {};
  // 全量回存：未改字段取 raw 原值
  expect(body.SessionTimeout).toBe(7200);
  expect(body.WebUrl).toBe('http://127.0.0.1:6600');
  // 改动字段双 key（PascalCase + camelCase）
  expect(body.Port).toBe(6601);
  expect(body.port).toBe(6601);
  // 密钥留空 = patch 不含该键 → body = raw 原值，且无 camelCase 副本
  expect(body.TokenSecret).toBe('s3cret-token-value');
  expect(body.tokenSecret).toBeUndefined();
});

test('保存失败：PUT 抛错时错误条可见、不静默、抽屉不关', async ({ page }) => {
  await openStory(page, 'StarServerSettingsForm/SaveFail');
  await page.locator('[data-spec="star-server-form-field-Port"]').fill('6602');
  await page.locator('[data-spec="star-server-form-save"]').click();
  await page.locator('[data-spec="guard-confirm"]').click();
  await expect(page.locator('[data-spec="star-server-form-save-error"]')).toContainText('平台设置保存失败：权限不足');
  await expect(page.locator('[data-spec="star-server-form"]')).toBeVisible();
});
