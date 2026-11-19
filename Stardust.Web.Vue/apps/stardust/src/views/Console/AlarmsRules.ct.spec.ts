import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

// CARD-MON-13：跟踪规则区块（/alarms「跟踪规则」视图）。逐字文案见 traceRule.logic.ts 导出常量与旅程 §6.8。
test('Data：四行数据，名单/方式/启用 Tag、优先级、操作两链', async ({ page }) => {
  await openStory(page, 'AlarmsRules/Data');
  const rows = page.locator('[data-spec="trace-rule-row"]');
  await expect(rows).toHaveCount(4);
  await expect(rows.first()).toContainText('^/Admin/[A-Z]\\w+');
  // 名单：白名单(ok)/黑名单(err)（实体描述「白名单。否则是黑名单」）
  await expect(rows.first()).toContainText('白名单');
  await expect(rows.nth(3)).toContainText('黑名单');
  // 方式：正则/模糊
  await expect(rows.first()).toContainText('正则');
  await expect(rows.nth(2)).toContainText('模糊');
  // 启用/停用 + 优先级
  await expect(rows.nth(3)).toContainText('停用');
  await expect(rows.first()).toContainText('100');
  // 行操作：编辑 / 删除
  await expect(rows.first().locator('[data-spec="trace-rule-edit"]')).toContainText('编辑');
  await expect(rows.first().locator('[data-spec="trace-rule-remove"]')).toContainText('删除');
  // 语义说明（实体描述 + C04 匹配语义）在区块头部可见
  await expect(page.locator('[data-spec="alarms-rules"]')).toContainText('全局黑白名单，白名单放行，黑名单拦截。优先级越大越在前面');
});

test('Data：客户端搜索（备注关键词）→ 纯列表空态，清空恢复；不提供三项过滤控件（假筛选硬条款）', async ({ page }) => {
  await openStory(page, 'AlarmsRules/Data');
  await page.locator('[data-spec="trace-rule-search"]').fill('单点登录');
  await expect(page.locator('[data-spec="trace-rule-row"]')).toHaveCount(1);
  await expect(page.locator('[data-spec="trace-rule-row"]').first()).toContainText('/Sso/*');
  await page.locator('[data-spec="trace-rule-search"]').fill('不存在的规则');
  await expect(page.locator('[data-spec="trace-rule-filter-empty"]')).toContainText('暂无匹配的跟踪规则');
  await page.locator('[data-spec="trace-rule-search"]').fill('');
  await expect(page.locator('[data-spec="trace-rule-row"]')).toHaveCount(4);
  // 假筛选硬条款：工具条不得出现 isWhite/isRegex/enable 过滤下拉
  await expect(page.locator('[data-spec="alarms-rules"] select')).toHaveCount(0);
});

test('Data：点行展开备注全文与创建信息，再点收起', async ({ page }) => {
  await openStory(page, 'AlarmsRules/Data');
  await page.locator('[data-spec="trace-rule-row"]').first().click();
  const detail = page.locator('[data-spec="trace-rule-detail"]');
  await expect(detail).toBeVisible();
  await expect(detail).toContainText('后台管理默认放行');
  await expect(detail).toContainText('创建者：admin');
  await expect(detail).toContainText('创建地址：127.0.0.1');
  await page.locator('[data-spec="trace-rule-row"]').first().click();
  await expect(page.locator('[data-spec="trace-rule-detail"]')).toHaveCount(0);
});

test('Empty：空库引导「还没有跟踪规则」+「新建规则」开抽屉；空规则保存报错可见', async ({ page }) => {
  await openStory(page, 'AlarmsRules/Empty');
  await expect(page.locator('[data-spec="trace-rule-empty"]')).toContainText('还没有跟踪规则');
  await page.locator('[data-spec="trace-rule-empty-create"]').click();
  await expect(page.locator('[data-spec="drawer"] h2')).toContainText('新增跟踪规则');
  // Rule 必填：空提交被拒，报错 role="alert" 且抽屉不关
  await page.locator('[data-spec="trace-rule-form-submit"]').click();
  await expect(page.locator('[data-spec="trace-rule-form-error"]')).toHaveText('规则不能为空');
  await expect(page.locator('[data-spec="drawer"] h2')).toContainText('新增跟踪规则');
  // 新增默认：白名单勾选、正则不勾选、启用勾选（白名单放行=主要场景）
  await expect(page.locator('[data-spec="trace-rule-form-iswhite"]')).toBeChecked();
  await expect(page.locator('[data-spec="trace-rule-form-isregex"]')).not.toBeChecked();
  await expect(page.locator('[data-spec="trace-rule-form-enable"]')).toBeChecked();
});

test('Data：编辑抽屉回显（标题、规则串、优先级），取消可关闭', async ({ page }) => {
  await openStory(page, 'AlarmsRules/Data');
  await page.locator('[data-spec="trace-rule-row"]').first().locator('[data-spec="trace-rule-edit"]').click();
  await expect(page.locator('[data-spec="drawer"] h2')).toContainText('编辑跟踪规则');
  await expect(page.locator('[data-spec="trace-rule-form-rule"]')).toHaveValue('^/Admin/[A-Z]\\w+');
  await expect(page.locator('[data-spec="trace-rule-form-priority"]')).toHaveValue('100');
  await expect(page.locator('[data-spec="trace-rule-form-iswhite"]')).toBeChecked();
  await expect(page.locator('[data-spec="trace-rule-form-isregex"]')).toBeChecked();
  await page.locator('[data-spec="trace-rule-form-cancel"]').click();
  await expect(page.locator('[data-spec="trace-rule-form"]')).toHaveCount(0);
});

test('Data：删除 Guard 文案逐字，确认后行消失', async ({ page }) => {
  await openStory(page, 'AlarmsRules/Data');
  await page.locator('[data-spec="trace-rule-row"]').first().locator('[data-spec="trace-rule-remove"]').click();
  // 两段式（标题含规则串、detail=后果）、按钮「取消 / 确认执行」
  await expect(page.locator('[data-spec="guard-title"]')).toHaveText('删除跟踪规则「^/Admin/[A-Z]\\w+」？');
  await expect(page.locator('[data-spec="guard-detail"]')).toHaveText('删除后该规则立即失效，不再参与链路数据的放行与拦截');
  await expect(page.locator('[data-spec="guard-cancel"]')).toHaveText('取消');
  await expect(page.locator('[data-spec="guard-confirm"]')).toHaveText('确认执行');
  // 取消：弹窗关闭、行还在
  await page.locator('[data-spec="guard-cancel"]').click();
  await expect(page.locator('[data-spec="guard-title"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="trace-rule-row"]')).toHaveCount(4);
  // 确认：行消失（story 桩有状态删除）
  await page.locator('[data-spec="trace-rule-row"]').first().locator('[data-spec="trace-rule-remove"]').click();
  await page.locator('[data-spec="guard-confirm"]').click();
  await expect(page.locator('[data-spec="trace-rule-row"]')).toHaveCount(3);
  await expect(page.locator('[data-spec="trace-rule-row"]').first()).toContainText('^/Cube/[A-Z]\\w+');
});

test('Failed：失败文案与重试按钮可见，不当作空态', async ({ page }) => {
  await openStory(page, 'AlarmsRules/Failed');
  await expect(page.locator('[data-spec="trace-rule-failed"]')).toContainText('跟踪规则加载失败');
  await expect(page.locator('[data-spec="trace-rule-failed"]')).toContainText('stub 500');
  await expect(page.locator('[data-spec="trace-rule-retry"]')).toBeVisible();
  await expect(page.locator('[data-spec="trace-rule-empty"]')).toHaveCount(0);
});

test('Data：视觉基线（首次生成后复跑验证稳定）', async ({ page }) => {
  await openStory(page, 'AlarmsRules/Data');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});
