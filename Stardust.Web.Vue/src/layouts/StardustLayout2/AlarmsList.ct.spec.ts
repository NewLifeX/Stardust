import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('Mixed：四行，级别与状态 data 属性正确', async ({ page }) => {
  await openStory(page, 'AlarmsList/Mixed');
  const rows = page.locator('[data-spec="alarm-row"]');
  await expect(rows).toHaveCount(4);
  await expect(rows.nth(0)).toHaveAttribute('data-level', 'critical');
  await expect(rows.nth(0)).toHaveAttribute('data-status', 'active');
  await expect(rows.nth(3)).toHaveAttribute('data-status', 'resolved');
});

test('Mixed：统计条显示当前 Critical=1 / Warning=1', async ({ page }) => {
  await openStory(page, 'AlarmsList/Mixed');
  await expect(page.locator('[data-spec="alarms-stats"]')).toContainText('当前 1 Critical');
  await expect(page.locator('[data-spec="alarms-stats"]')).toContainText('当前 1 Warning');
});

test('Mixed：第一行显示 Critical 标签与未处理 Tag', async ({ page }) => {
  await openStory(page, 'AlarmsList/Mixed');
  const first = page.locator('[data-spec="alarm-row"]').first();
  await expect(first).toContainText('node-b 已下线');
  await expect(first).toContainText('未处理');
});

test('Empty：没有告警时显示空态文案，不渲染表格', async ({ page }) => {
  await openStory(page, 'AlarmsList/Empty');
  await expect(page.locator('[data-spec="alarms-empty"]')).toContainText('没有告警');
  await expect(page.locator('[data-spec="alarm-row"]')).toHaveCount(0);
});

test('Loading：spinner 加载中，不渲染空表', async ({ page }) => {
  await openStory(page, 'AlarmsList/Loading');
  await expect(page.locator('[data-spec="alarms-loading"]')).toContainText('告警加载中');
  await expect(page.locator('[data-spec="alarm-row"]')).toHaveCount(0);
});

test('Failed：请求失败显示可见错误与重试按钮，不当作空态', async ({ page }) => {
  await openStory(page, 'AlarmsList/Failed');
  await expect(page.locator('[data-spec="alarms-failed"]')).toContainText('告警加载失败');
  await expect(page.locator('[data-spec="alarms-retry"]')).toContainText('重试');
  await expect(page.locator('[data-spec="alarms-empty"]')).toHaveCount(0);
});

test('过滤：级别下拉选 Critical 后只剩 1 行', async ({ page }) => {
  await openStory(page, 'AlarmsList/Mixed');
  await page.locator('[data-spec="alarms-filter-level"]').selectOption('critical');
  await expect(page.locator('[data-spec="alarm-row"]')).toHaveCount(1);
  await expect(page.locator('[data-spec="alarm-row"]')).toContainText('node-b 已下线');
});

test('过滤：搜索框输入 orderservice 后只剩 1 行', async ({ page }) => {
  await openStory(page, 'AlarmsList/Mixed');
  await page.locator('[data-spec="alarms-filter-q"]').fill('orderservice');
  await expect(page.locator('[data-spec="alarm-row"]')).toHaveCount(1);
  await expect(page.locator('[data-spec="alarm-row"]')).toContainText('OrderService');
});

test('展开行：点标题行展开详情，显示 Content；再点收起', async ({ page }) => {
  await openStory(page, 'AlarmsList/Mixed');
  await expect(page.locator('[data-spec="alarm-detail"]')).toHaveCount(0);
  await page.locator('[data-spec="alarm-row"]').nth(1).click();
  await expect(page.locator('[data-spec="alarm-detail"]')).toHaveCount(1);
  await expect(page.locator('[data-spec="alarm-content"]')).toContainText('错误率 12%');
  await page.locator('[data-spec="alarm-row"]').nth(1).click();
  await expect(page.locator('[data-spec="alarm-detail"]')).toHaveCount(0);
});

test('Recorder：点重试按钮把 1 写进 window.__alarmRetry', async ({ page }) => {
  await openStory(page, 'AlarmsList/Recorder');
  await page.locator('[data-spec="alarms-retry"]').click();
  const picked = await page.evaluate(() => (window as unknown as { __alarmRetry?: number[] }).__alarmRetry);
  expect(picked).toEqual([1]);
});

// ---------- CARD-MON-02：组过滤 / 时间范围 / 告警历史链接 / 错误格 ----------

function pickedFilter(page: Page): Promise<[string, unknown][] | undefined> {
  return page.evaluate(() => (window as unknown as { __alarmFilter?: [string, unknown][] }).__alarmFilter);
}

test('CARD-MON-02 组过滤：下拉含「全部组」与组选项，选中值预置，变更上抛 groupId', async ({ page }) => {
  await openStory(page, 'AlarmsList/GroupFilter');
  const group = page.locator('[data-spec="alarms-filter-group"]');
  await expect(group).toBeVisible();
  const options = group.locator('option');
  await expect(options).toHaveCount(3);
  await expect(options.nth(0)).toHaveText('全部组');
  await expect(options.nth(1)).toHaveText('值班告警');
  await expect(options.nth(2)).toHaveText('节点巡检');
  await expect(group).toHaveValue('1');
  await group.selectOption({ label: '节点巡检' });
  const picked = await pickedFilter(page);
  expect(picked).toContainEqual(['groupId', '2']);
});

test('CARD-MON-02 时间范围：两日期输入预置值，变更上抛 dtStart/dtEnd（清空=空串）', async ({ page }) => {
  await openStory(page, 'AlarmsList/GroupFilter');
  const range = page.locator('[data-spec="alarms-filter-range"]');
  await expect(range).toBeVisible();
  const inputs = range.locator('input[type="date"]');
  await expect(inputs).toHaveCount(2);
  await expect(inputs.nth(0)).toHaveValue('2026-11-01');
  await expect(inputs.nth(1)).toHaveValue('2026-11-07');
  await inputs.nth(0).fill('2026-11-02');
  await inputs.nth(1).fill('');
  const picked = await pickedFilter(page);
  expect(picked).toContainEqual(['dtStart', '2026-11-02']);
  expect(picked).toContainEqual(['dtEnd', '']);
});

test('CARD-MON-02 组过滤与级别/关键字过滤叠加（客户端过滤保持，C08 复验）', async ({ page }) => {
  await openStory(page, 'AlarmsList/GroupFilter');
  await expect(page.locator('[data-spec="alarm-row"]')).toHaveCount(4);
  await page.locator('[data-spec="alarms-filter-level"]').selectOption('critical');
  await expect(page.locator('[data-spec="alarm-row"]')).toHaveCount(1);
  await expect(page.locator('[data-spec="alarm-row"]')).toContainText('node-b 已下线');
});

test('CARD-MON-02 告警历史链接：有组记录行显示（2 条），点击上抛组 Id 且不触发行展开；无组行不显示', async ({ page }) => {
  await openStory(page, 'AlarmsList/GroupFilter');
  const links = page.locator('[data-spec="alarms-history-link"]');
  await expect(links).toHaveCount(2);
  // node-a 无组（groupId=''）与已处理行不提供链接
  await expect(page.locator('[data-spec="alarm-row"]').nth(2).locator('[data-spec="alarms-history-link"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="alarm-detail"]')).toHaveCount(0);
  await links.nth(0).click(); // node-b 行（groupId '2'）
  const picked = await pickedFilter(page);
  expect(picked).toContainEqual(['history', '2']);
  // click.stop：链接点击不触发行展开
  await expect(page.locator('[data-spec="alarm-detail"]')).toHaveCount(0);
});

test('CARD-MON-02 展开区错误格（四格→五格）：Error 有值显原文，无值显「（空）」', async ({ page }) => {
  await openStory(page, 'AlarmsList/Mixed');
  await page.locator('[data-spec="alarm-row"]').nth(3).click();
  await expect(page.locator('[data-spec="alarm-error"]')).toContainText('错误：投递超时');
  await page.locator('[data-spec="alarm-row"]').nth(3).click();
  await page.locator('[data-spec="alarm-row"]').nth(2).click();
  await expect(page.locator('[data-spec="alarm-error"]')).toContainText('错误：（空）');
});

test('CARD-MON-02 告警组列表加载失败：下拉旁可见错误提示（§11 不静默）', async ({ page }) => {
  await openStory(page, 'AlarmsList/GroupError');
  await expect(page.locator('[data-spec="alarms-filter-group"]')).toBeVisible();
  await expect(page.locator('[data-spec="alarms-group-error"]')).toContainText('告警组列表加载失败');
});

// ---------- CARD-MON-03：已恢复视图 / 行操作 / 删除 Guard / 导出 CSV ----------

function pickedAction(page: Page): Promise<[string, unknown][] | undefined> {
  return page.evaluate(() => (window as unknown as { __alarmAction?: [string, unknown][] }).__alarmAction);
}

test('CARD-MON-03 状态下拉：受控预置空、四选项含「已恢复」，选中上抛 statusFilter 且受控回显', async ({ page }) => {
  await openStory(page, 'AlarmsList/RecordActions');
  const status = page.locator('[data-spec="alarms-filter-status"]');
  await expect(status).toBeVisible();
  const options = status.locator('option');
  await expect(options).toHaveCount(4);
  await expect(options.nth(0)).toHaveText('全部状态');
  await expect(options.nth(1)).toHaveText('未处理');
  await expect(options.nth(2)).toHaveText('已处理');
  await expect(options.nth(3)).toHaveText('已恢复');
  await expect(status).toHaveValue('');
  await status.selectOption('recovered');
  const picked = await pickedAction(page);
  expect(picked).toContainEqual(['statusFilter', 'recovered']);
  await expect(status).toHaveValue('recovered');
});

test('CARD-MON-03 行操作：记录行（active/recovered）有编辑/删除，已处理行无；编辑上抛且不触发展开', async ({ page }) => {
  await openStory(page, 'AlarmsList/RecordActions');
  const rows = page.locator('[data-spec="alarm-row"]');
  await expect(rows).toHaveCount(5);
  // 4 条记录行（0,1,2,4）各有 编辑/删除；resolved 行（nth(3)）无（AlarmHistory 只读）
  for (const i of [0, 1, 2, 4]) {
    await expect(rows.nth(i).locator('[data-spec="alarm-record-edit"]')).toHaveCount(1);
    await expect(rows.nth(i).locator('[data-spec="alarm-record-remove"]')).toHaveCount(1);
  }
  await expect(rows.nth(3).locator('[data-spec="alarm-record-edit"]')).toHaveCount(0);
  await expect(rows.nth(3).locator('[data-spec="alarm-record-remove"]')).toHaveCount(0);
  // 编辑上抛整行（录制取 Id）；@click.stop 不触发行展开
  await rows.nth(4).locator('[data-spec="alarm-record-edit"]').click();
  const picked = await pickedAction(page);
  expect(picked).toContainEqual(['edit', '7']);
  await expect(page.locator('[data-spec="alarm-detail"]')).toHaveCount(0);
});

test('CARD-MON-03 已恢复 Tag：recovered 行 data-status=recovered，Tag「已恢复」', async ({ page }) => {
  await openStory(page, 'AlarmsList/RecordActions');
  const recovered = page.locator('[data-spec="alarm-row"][data-status="recovered"]');
  await expect(recovered).toHaveCount(1);
  await expect(recovered).toContainText('OrderService 已恢复');
  await expect(recovered.locator('[data-spec="tag"]')).toHaveText('已恢复');
});

test('CARD-MON-03 删除 Guard：标题含记录名、正文逐字；取消关闭不删，确认上抛 remove（@click.stop 不展开）', async ({ page }) => {
  await openStory(page, 'AlarmsList/RecordActions');
  await expect(page.locator('[data-spec="alarm-detail"]')).toHaveCount(0);
  await page.locator('[data-spec="alarm-row"]').first().locator('[data-spec="alarm-record-remove"]').click();
  // C12 逐字：标题含记录名、正文=后果、按钮「取消 / 确认执行」
  await expect(page.locator('[data-spec="guard-title"]')).toHaveText('删除告警记录「node-b 已下线」？');
  await expect(page.locator('[data-spec="guard-detail"]')).toHaveText('删除后该条告警生命周期记录不可恢复');
  await expect(page.locator('[data-spec="guard-confirm"]')).toHaveText('确认执行');
  // 取消：关闭且无 remove 记录
  await page.locator('[data-spec="guard-cancel"]').click();
  await expect(page.locator('[data-spec="guard-title"]')).toHaveCount(0);
  const afterCancel = (await pickedAction(page)) ?? [];
  expect(afterCancel).not.toContainEqual(['remove', expect.anything()]);
  // 确认：上抛记录 Id；删除按钮 @click.stop 不触发行展开
  await page.locator('[data-spec="alarm-row"]').first().locator('[data-spec="alarm-record-remove"]').click();
  await page.locator('[data-spec="guard-confirm"]').click();
  const picked = await pickedAction(page);
  expect(picked).toContainEqual(['remove', '11']);
  await expect(page.locator('[data-spec="alarm-detail"]')).toHaveCount(0);
});

test('CARD-MON-03 导出 CSV：触发下载「告警记录导出.csv」；空结果禁用且 title 说明原因（§11）', async ({ page }) => {
  await openStory(page, 'AlarmsList/RecordActions');
  await expect(page.locator('[data-spec="alarms-export-note"]')).toHaveText('当前筛选结果（最多最近 200 条）');
  const exportBtn = page.locator('[data-spec="alarms-export"]');
  const downloadPromise = page.waitForEvent('download');
  await exportBtn.click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toBe('告警记录导出.csv');
  // 空结果禁用，原因在 title 实时可见
  await page.locator('[data-spec="alarms-filter-q"]').fill('不存在的关键字xyz');
  await expect(exportBtn).toBeDisabled();
  await expect(exportBtn).toHaveAttribute('title', '当前筛选结果为空，无可导出数据');
});

test('CARD-MON-03 RecordActions 视觉基线（行操作 + 导出工具条 + 已恢复 Tag）', async ({ page }) => {
  await openStory(page, 'AlarmsList/RecordActions');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});
