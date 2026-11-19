import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

// CARD-MON-01：告警组区块（/alarms「告警组」视图）。逐字文案见 alarmGroup.logic.ts 导出常量与旅程 C14/C15。
test('Data：两行数据，启用/停用 Tag、操作四链，停用组「测试」不禁用且 title 说明', async ({ page }) => {
  await openStory(page, 'AlarmsGroups/Data');
  const rows = page.locator('[data-spec="alarm-group-row"]');
  await expect(rows).toHaveCount(2);
  await expect(rows.first()).toContainText('值班告警');
  await expect(rows.first()).toContainText('启用');
  await expect(rows.nth(1)).toContainText('停用组');
  await expect(rows.nth(1)).toContainText('停用');
  // 行操作：测试 / 告警历史 / 编辑 / 删除（C05 实现口径=测试+告警历史两链，审计日志链缺位如实）
  const first = rows.first();
  await expect(first.locator('[data-spec="alarm-group-test"]')).toContainText('测试');
  await expect(first.locator('[data-spec="alarm-group-history"]')).toContainText('告警历史');
  await expect(first.locator('[data-spec="alarm-group-edit"]')).toContainText('编辑');
  await expect(first.locator('[data-spec="alarm-group-remove"]')).toContainText('删除');
  // 定稿裁定 4：停用组「测试」不禁用 + title 逐字说明；启用组 title 含组名
  const testDisabled = rows.nth(1).locator('[data-spec="alarm-group-test"]');
  await expect(testDisabled).toBeEnabled();
  await expect(testDisabled).toHaveAttribute('title', '未启用的组不会真实发送（后端直接返回 OK）');
  await expect(first.locator('[data-spec="alarm-group-test"]')).toHaveAttribute(
    'title',
    '向「值班告警」的机器人发送一条测试告警',
  );
  // 客户端搜索：无命中 → 「暂无告警组记录」；清空恢复全量
  await page.locator('[data-spec="alarm-group-search"]').fill('不存在的组');
  await expect(page.locator('[data-spec="alarm-group-filter-empty"]')).toContainText('暂无告警组记录');
  await page.locator('[data-spec="alarm-group-search"]').fill('');
  await expect(page.locator('[data-spec="alarm-group-row"]')).toHaveCount(2);
});

test('Empty：空库引导「还没有告警组」+「创建告警组」开抽屉；空名保存报错可见', async ({ page }) => {
  await openStory(page, 'AlarmsGroups/Empty');
  await expect(page.locator('[data-spec="alarm-group-empty"]')).toContainText('还没有告警组');
  await page.locator('[data-spec="alarm-group-empty-create"]').click();
  await expect(page.locator('[data-spec="drawer"] h2')).toContainText('新增告警组');
  // Name 必填（C08）：空名提交被拒，报错 role="alert" 且抽屉不关
  await page.locator('[data-spec="alarm-group-form-submit"]').click();
  await expect(page.locator('[data-spec="alarm-group-form-error"]')).toHaveText('名称不能为空');
  await expect(page.locator('[data-spec="drawer"] h2')).toContainText('新增告警组');
  // 项目下拉已注入（闭包数据）：含「不绑定项目」与项目名
  await expect(page.locator('[data-spec="alarm-group-form-project"] option')).toHaveCount(3);
});

test('Data：编辑抽屉回显（标题含组名、名称回填），取消可关闭', async ({ page }) => {
  await openStory(page, 'AlarmsGroups/Data');
  await page.locator('[data-spec="alarm-group-row"]').first().locator('[data-spec="alarm-group-edit"]').click();
  await expect(page.locator('[data-spec="drawer"] h2')).toContainText('编辑告警组 · 值班告警');
  await expect(page.locator('[data-spec="alarm-group-form-name"]')).toHaveValue('值班告警');
  await expect(page.locator('[data-spec="alarm-group-form-webhook"]')).toHaveValue(/dingtalk/);
  await page.locator('[data-spec="alarm-group-form-cancel"]').click();
  await expect(page.locator('[data-spec="alarm-group-form"]')).toHaveCount(0);
});

test('Data：删除 Guard 文案逐字，确认后行消失', async ({ page }) => {
  await openStory(page, 'AlarmsGroups/Data');
  await page.locator('[data-spec="alarm-group-row"]').first().locator('[data-spec="alarm-group-remove"]').click();
  // C14 逐字：两段式（标题含组名、detail=后果）、按钮「取消 / 确认执行」
  await expect(page.locator('[data-spec="guard-title"]')).toHaveText(
    '删除告警组「值班告警」？',
  );
  await expect(page.locator('[data-spec="guard-detail"]')).toHaveText(
    '删除后引用该组的监控项将无法投递',
  );
  await expect(page.locator('[data-spec="guard-cancel"]')).toHaveText('取消');
  await expect(page.locator('[data-spec="guard-confirm"]')).toHaveText('确认执行');
  // 取消：弹窗关闭、行还在
  await page.locator('[data-spec="guard-cancel"]').click();
  await expect(page.locator('[data-spec="guard-title"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="alarm-group-row"]')).toHaveCount(2);
  // 确认：行消失（story 桩有状态删除）
  await page.locator('[data-spec="alarm-group-row"]').first().locator('[data-spec="alarm-group-remove"]').click();
  await page.locator('[data-spec="guard-confirm"]').click();
  await expect(page.locator('[data-spec="alarm-group-row"]')).toHaveCount(1);
  await expect(page.locator('[data-spec="alarm-group-row"]').first()).toContainText('停用组');
});

test('Recorder：点行「告警历史」上抛事件，组 Id 写进 window.__alarmsGroupHistory', async ({ page }) => {
  await openStory(page, 'AlarmsGroups/Recorder');
  await page.locator('[data-spec="alarm-group-row"]').first().locator('[data-spec="alarm-group-history"]').click();
  const picked = await page.evaluate(() => (window as unknown as { __alarmsGroupHistory?: number[] }).__alarmsGroupHistory);
  expect(picked).toEqual([1]);
});

test('Failed：失败文案与重试按钮可见，不当作空态', async ({ page }) => {
  await openStory(page, 'AlarmsGroups/Failed');
  await expect(page.locator('[data-spec="alarm-group-failed"]')).toContainText('告警组加载失败');
  await expect(page.locator('[data-spec="alarm-group-failed"]')).toContainText('stub 500');
  await expect(page.locator('[data-spec="alarm-group-retry"]')).toBeVisible();
  await expect(page.locator('[data-spec="alarm-group-empty"]')).toHaveCount(0);
});

test('Data：视觉基线（首次生成后复跑验证稳定）', async ({ page }) => {
  await openStory(page, 'AlarmsGroups/Data');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});
