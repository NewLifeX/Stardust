import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('TraceItemDrawer 编辑回填：名称/种类(词典)/显示名/启用/克隆/超时/规则', async ({ page }) => {
  await openStory(page, 'TraceItemDrawer/Edit');
  await expect(page.locator('[data-spec="trace-item-form-name"]')).toHaveValue('GET /api/user');
  // Kind select 选中值 = 'Http'（词典 option 文案为展示名）
  await expect(page.locator('[data-spec="trace-item-form-kind"]')).toHaveValue('Http');
  await expect(page.locator('[data-spec="trace-item-form-display-name"]')).toHaveValue('用户接口');
  await expect(page.locator('[data-spec="trace-item-form-enable"]')).toBeChecked();
  await expect(page.locator('[data-spec="trace-item-form-cloned"]')).not.toBeChecked();
  await expect(page.locator('[data-spec="trace-item-form-timeout"]')).toHaveValue('3');
  await expect(page.locator('[data-spec="trace-item-form-rules"]')).toHaveValue('name=*/api/user*');
  // Rules placeholder 逐字（卡 §3.2 定稿）
  await expect(page.locator('[data-spec="trace-item-form-rules"]')).toHaveAttribute('placeholder', 'name=*/check*,*/ping*;clientId=10.10.*');
  await expect(page.locator('[data-spec="trace-item-form-submit"]')).toContainText('保存');
  await expect(page.locator('[data-spec="trace-item-form-cancel"]')).toContainText('取消');
});

test('TraceItemDrawer 校验：名称空提交 → 「操作名不能为空」；超时负数 → 「超时时间不能为负数」', async ({ page }) => {
  await openStory(page, 'TraceItemDrawer/Create');
  await expect(page.locator('[data-spec="trace-item-form-name"]')).toHaveValue('');
  await page.locator('[data-spec="trace-item-form-submit"]').click();
  await expect(page.locator('[data-spec="trace-item-form-error"]')).toHaveText('操作名不能为空');
  // 填名 + 负超时 → 下一校验
  await page.locator('[data-spec="trace-item-form-name"]').fill('GET /api/user');
  await page.locator('[data-spec="trace-item-form-timeout"]').fill('-1');
  await page.locator('[data-spec="trace-item-form-submit"]').click();
  await expect(page.locator('[data-spec="trace-item-form-error"]')).toHaveText('超时时间不能为负数');
  // 修为 0 → 错误消失（0=不判超时 合法）
  await page.locator('[data-spec="trace-item-form-timeout"]').fill('0');
  await page.locator('[data-spec="trace-item-form-submit"]').click();
  await expect(page.locator('[data-spec="trace-item-form-error"]')).toHaveCount(0);
});

test('TraceItemDrawer 保存中：提交按钮禁用「保存中…」；类别加载中提示（AGENTS.md §11）', async ({ page }) => {
  await openStory(page, 'TraceItemDrawer/Busy');
  const submit = page.locator('[data-spec="trace-item-form-submit"]');
  await expect(submit).toBeDisabled();
  await expect(submit).toContainText('保存中…');
  await expect(page.locator('[data-spec="trace-item-form-kinds-loading"]')).toContainText('类别加载中…');
});
