import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

const RUN = '[data-spec="ship-run"]';
const CELL = '[data-spec="ship-cell"]';
const STAGE = '[data-spec="ship-stages"] > div';
const LEGEND = '[data-spec="ship-legend"]';

test('DeployPipelineRuns 默认：选中运行的状态标签、说明、运行列表各行内容正确', async ({ page }) => {
  await openStory(page, 'DeployPipelineRuns/Default');
  await expect(page.locator('[data-spec="ship-pipeline"] h3')).toContainText('成功');
  await expect(page.locator('[data-spec="ship-pipeline"]')).toContainText('运行 #7 按节点的部署步骤');
  await expect(page.locator('[data-spec="ship-pipeline"]')).toContainText('一键回滚');
  // F09：回滚已是真实按钮，旧「待接入」占位不再出现
  await expect(page.locator('[data-spec="ship-pipeline"]')).not.toContainText('待接入');
  await expect(page.locator('[data-spec="ship-allskipped"]')).toHaveCount(0);

  await expect(page.locator(RUN)).toHaveCount(3);
  const first = page.locator(RUN).nth(0);
  await expect(first).toContainText('v1.4.7');
  await expect(first).toContainText('build-main');
  await expect(first).toContainText('Webhook');
  await expect(first).toContainText('zhang');
  await expect(first).toContainText('10-28 10:00');
  await expect(first).toContainText('成功');
  await expect(page.locator(RUN).nth(1)).toContainText('失败');
  await expect(page.locator(RUN).nth(2)).toContainText('已取消');
});

// F09：一键回滚是头部真实按钮：可点、title 带候选版本；置灰时 title 写原因；回滚中禁用旋转
test('DeployPipelineRuns 一键回滚按钮：可点、置灰原因、回滚中状态', async ({ page }) => {
  const BTN = '[data-spec="ship-rollback"]';
  await openStory(page, 'DeployPipelineRuns/Default');
  const btn = page.locator(BTN);
  await expect(btn).toBeEnabled();
  await expect(btn).toContainText('一键回滚');
  await expect(btn).toHaveAttribute('title', '回滚到上一个版本 v1.4.1');

  await openStory(page, 'DeployPipelineRuns/NoRollback');
  await expect(btn).toBeDisabled();
  await expect(btn).toHaveAttribute('title', '没有可回滚的版本（需要除当前生效版本之外的可用版本）');

  await openStory(page, 'DeployPipelineRuns/RollingBack');
  await expect(btn).toBeDisabled();
  await expect(btn).toContainText('回滚中…');
  await expect(btn.locator('.animate-spin')).toHaveCount(1);
});

test('DeployPipelineRuns 四段进度：已完成显示对勾，进行中、未开始显示序号', async ({ page }) => {
  await openStory(page, 'DeployPipelineRuns/Default');
  await expect(page.locator(STAGE)).toHaveCount(4);
  await expect(page.locator(STAGE)).toHaveText([/构建/, /上传/, /分发/, /部署/]);
  // 已完成：有图标、没有序号
  await expect(page.locator(STAGE).nth(0).locator('svg')).toHaveCount(1);
  await expect(page.locator(STAGE).nth(0)).not.toContainText('1');
  // 进行中、未开始：没有图标，显示自己的序号
  await expect(page.locator(STAGE).nth(2).locator('svg')).toHaveCount(0);
  await expect(page.locator(STAGE).nth(2)).toContainText('3');
  await expect(page.locator(STAGE).nth(3)).toContainText('4');
});

test('DeployPipelineRuns 节点格子：数量、序号、悬浮文案、状态颜色与图例', async ({ page }) => {
  await openStory(page, 'DeployPipelineRuns/Default');
  await expect(page.locator(CELL)).toHaveCount(8);
  await expect(page.locator(CELL).nth(0)).toHaveText('1');
  await expect(page.locator(CELL).nth(7)).toHaveText('8');
  await expect(page.locator(CELL).nth(0)).toHaveAttribute('title', 'node-a · 10.0.0.1 · 已部署');
  await expect(page.locator(CELL).nth(2)).toHaveAttribute('title', 'node-c · 10.0.0.3 · 部署失败：连接超时');

  // 语义断言：成功与失败格子的背景色不同
  const bg = (n: number) => page.locator(CELL).nth(n).evaluate((el) => getComputedStyle(el).backgroundColor);
  expect(await bg(2)).not.toBe(await bg(0));
  expect(await bg(0)).toBe(await bg(1));

  // 图例：只列出出现过的状态，每种一次
  const legend = page.locator(LEGEND);
  for (const label of ['已部署', '部署失败', '已跳过', '节点已停用', '等待下发', '部署中', '无部署记录']) {
    await expect(legend).toContainText(label);
  }
  await expect(legend).not.toContainText('旧版本');
  expect((await legend.innerText()).split('已部署').length - 1).toBe(1);
});

test('DeployPipelineRuns 选中行高亮；没有选中时没有高亮，也没有四段进度和状态标签', async ({ page }) => {
  await openStory(page, 'DeployPipelineRuns/Default');
  const bg = (n: number) => page.locator(RUN).nth(n).evaluate((el) => getComputedStyle(el).backgroundColor);
  expect(await bg(0)).not.toBe(await bg(1));

  await openStory(page, 'DeployPipelineRuns/NoSelection');
  expect(await bg(0)).toBe(await bg(1));
  await expect(page.locator('[data-spec="ship-stages"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="ship-pipeline"] h3')).not.toContainText('成功');
  await expect(page.locator('[data-spec="ship-pipeline"]')).toContainText('点一条运行记录查看按节点的部署步骤');
});

test('DeployPipelineRuns 全部跳过有单独提示；没有运行记录时显示占位且没有格子', async ({ page }) => {
  await openStory(page, 'DeployPipelineRuns/AllSkipped');
  await expect(page.locator('[data-spec="ship-allskipped"]')).toContainText('无节点实际部署');
  await expect(page.locator(CELL)).toHaveCount(2);

  await openStory(page, 'DeployPipelineRuns/Empty');
  await expect(page.locator('[data-spec="ship-runs-empty"]')).toContainText('暂无流水线运行记录');
  await expect(page.locator(RUN)).toHaveCount(0);
  await expect(page.locator(CELL)).toHaveCount(0);
  await expect(page.locator('[data-spec="ship-allskipped"]')).toHaveCount(0);
});
