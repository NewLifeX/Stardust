import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

const LOV = '[data-spec="node-data-lov-input"] input';
const OPTION = '[data-spec="node-data-lov-option"]';

// CARD-NODE-07 §9.3：节点搜索 Lov（与 NODE-01 的 AppLov 同形，契约 NodeSearch）
test('NodeLov 联想：输入出候选、锚点在选项行、命中过滤', async ({ page }) => {
  await openStory(page, 'NodeLov/Default');

  // 空关键字不弹候选（trigger-on-focus=false）
  await page.locator(LOV).click();
  await expect(page.locator('.el-autocomplete-suggestion li')).toHaveCount(0);

  // 输入 → 替身数据出两条命中（web → web-1/web-2），atlas-node 被过滤剔除；
  // data-spec 锚挂在外层包裹 div（el-autocomplete inheritAttrs:false，attrs 透传不可靠）
  await page.locator(LOV).fill('web');
  await expect(page.locator('.el-autocomplete-suggestion li')).toHaveCount(2);
  await expect(page.locator(OPTION).nth(0)).toContainText('web-1');
  await expect(page.locator(OPTION).nth(0)).toContainText('#1');
  await expect(page.locator('.el-autocomplete-suggestion li')).not.toContainText(['atlas-node']);

  // 不命中的关键字 → 空列表
  await page.locator(LOV).fill('zzz');
  await expect(page.locator('.el-autocomplete-suggestion li')).toHaveCount(0);
});

test('NodeLov 选中：回调带 id、输入框清空', async ({ page }) => {
  await openStory(page, 'NodeLov/Default');
  await page.locator(LOV).fill('web');
  await expect(page.locator('.el-autocomplete-suggestion li')).toHaveCount(2);

  await page.locator('.el-autocomplete-suggestion li').nth(1).click();
  // 选中后输入框清空（与 AppLov 行为一致，便于连续切换）
  await expect(page.locator(LOV)).toHaveValue('');
  // story 的 onSelect 把选中值写到 window（gallery 无 emit 渲染区）
  const last = await page.evaluate(() => (window as unknown as { __lovLast?: { value: string; id: number } }).__lovLast);
  expect(last).toEqual({ value: 'web-2', id: 2 });
});

test('NodeLov 无候选：不弹层不报错', async ({ page }) => {
  await openStory(page, 'NodeLov/NoHit');
  await page.locator(LOV).fill('any');
  await expect(page.locator('.el-autocomplete-suggestion li')).toHaveCount(0);
});
