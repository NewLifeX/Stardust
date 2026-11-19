import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

// CARD-MON-03：告警记录编辑抽屉（编辑-only，保存=PUT /Monitors/AlarmRecord/{id}，主键在路径）。
test('Edit：回填记录行（名称/组/类别/对象/状态/起止/时长/次数/内容），内容 maxlength=2000', async ({ page }) => {
  await openStory(page, 'AlarmRecordDrawer/Edit');
  await expect(page.locator('[data-spec="drawer"] h2')).toContainText('编辑告警记录 · node-b 已下线');
  await expect(page.locator('[data-spec="alarm-record-form-name"]')).toHaveValue('node-b 已下线');
  await expect(page.locator('[data-spec="alarm-record-form-group"]')).toHaveValue('2');
  await expect(page.locator('[data-spec="alarm-record-form-category"]')).toHaveValue('节点下线');
  await expect(page.locator('[data-spec="alarm-record-form-action"]')).toHaveValue('node-b');
  await expect(page.locator('[data-spec="alarm-record-form-status"]')).toHaveValue('1');
  await expect(page.locator('[data-spec="alarm-record-form-start"]')).toHaveValue('2026-11-07T10:00');
  await expect(page.locator('[data-spec="alarm-record-form-end"]')).toHaveValue('');
  await expect(page.locator('[data-spec="alarm-record-form-duration"]')).toHaveValue('300');
  await expect(page.locator('[data-spec="alarm-record-form-times"]')).toHaveValue('1');
  await expect(page.locator('[data-spec="alarm-record-form-content"]')).toHaveValue('连续 6 次心跳丢失。');
  // Content 列宽 2000（报警记录.Biz.cs:46-47 服务端截断）→ 前端 maxlength 兜底
  await expect(page.locator('[data-spec="alarm-record-form-content"]')).toHaveAttribute('maxlength', '2000');
});

test('Name 必填（C10）：清空提交 → 「名称不能为空」role=alert，抽屉不关', async ({ page }) => {
  await openStory(page, 'AlarmRecordDrawer/Edit');
  await page.locator('[data-spec="alarm-record-form-name"]').fill('');
  await page.locator('[data-spec="alarm-record-form-submit"]').click();
  const err = page.locator('[data-spec="alarm-record-form-error"]');
  await expect(err).toHaveAttribute('role', 'alert');
  await expect(err).toHaveText('名称不能为空');
  await expect(page.locator('[data-spec="alarm-record-form"]')).toBeVisible();
});

test('提交录制：改名 + 状态选「已恢复」+ 补结束时间 → submit 表单对象 status=2', async ({ page }) => {
  await openStory(page, 'AlarmRecordDrawer/Edit');
  await page.locator('[data-spec="alarm-record-form-name"]').fill('node-b 已下线（改）');
  await page.locator('[data-spec="alarm-record-form-status"]').selectOption({ label: '已恢复' });
  await page.locator('[data-spec="alarm-record-form-end"]').fill('2026-11-07T11:10');
  await page.locator('[data-spec="alarm-record-form-submit"]').click();
  const picked = await page.evaluate(
    () => (window as unknown as { __recordSubmit?: Array<Record<string, unknown>> }).__recordSubmit,
  );
  expect(picked).toHaveLength(1);
  expect(picked?.[0]).toMatchObject({
    id: '7',
    name: 'node-b 已下线（改）',
    groupId: '2',
    status: 2,
    endTime: '2026-11-07T11:10',
  });
});

test('Busy：保存中按钮禁用 + 文案「保存中…」（§11）', async ({ page }) => {
  await openStory(page, 'AlarmRecordDrawer/Busy');
  await expect(page.locator('[data-spec="alarm-record-form-submit"]')).toBeDisabled();
  await expect(page.locator('[data-spec="alarm-record-form-submit"]')).toContainText('保存中…');
});

test('Closed：open=false 不渲染抽屉', async ({ page }) => {
  await openStory(page, 'AlarmRecordDrawer/Closed');
  // ElDrawer 关闭态：overlay 根元素仍挂在 DOM（v-show 隐藏）——断言「不可见」而非「不在 DOM」
  await expect(page.locator('[data-spec="drawer"]')).toBeHidden();
});

test('Edit：视觉基线（首次生成后复跑验证稳定）', async ({ page }) => {
  await openStory(page, 'AlarmRecordDrawer/Edit');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});
