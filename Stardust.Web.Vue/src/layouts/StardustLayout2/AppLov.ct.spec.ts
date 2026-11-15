import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

const LOV = '[data-spec="deploy-app-lov"] input';

// CARD-DEP-08 D5（J-REG-01 §8 行 9 移交项）：远程联想 → 选中回传 id
test('AppLov 联想：输入出候选、文案带显示名、命中过滤', async ({ page }) => {
  await openStory(page, 'AppLov/Default');

  // 空关键字不弹候选（trigger-on-focus=false）
  await page.locator(LOV).click();
  await expect(page.locator('.el-autocomplete-suggestion li')).toHaveCount(0);

  // 输入 → 替身数据出三条命中（star → StarServer/StarWeb/Stardust——Stardust 名含 star 子串），
  // Atlas 名与显示名均不含 star，被过滤剔除
  await page.locator(LOV).fill('star');
  await expect(page.locator('.el-autocomplete-suggestion li')).toHaveCount(3);
  await expect(page.locator('.el-autocomplete-suggestion li').nth(0)).toContainText('StarServer（服务端）');
  await expect(page.locator('.el-autocomplete-suggestion li').nth(1)).toContainText('StarWeb');
  await expect(page.locator('.el-autocomplete-suggestion li')).not.toContainText(['Atlas']);

  // 不命中的关键字 → 空列表
  await page.locator(LOV).fill('zzz');
  await expect(page.locator('.el-autocomplete-suggestion li')).toHaveCount(0);
});

test('AppLov 选中：回调带 id、value 带显示名，输入框清空', async ({ page }) => {
  await openStory(page, 'AppLov/Default');
  await page.locator(LOV).fill('star');
  await expect(page.locator('.el-autocomplete-suggestion li')).toHaveCount(3);

  await page.locator('.el-autocomplete-suggestion li').nth(0).click();
  // 选中后输入框清空（与旧壳行为一致，便于连续切换）
  await expect(page.locator(LOV)).toHaveValue('');
  // story 的 onSelect 把选中值写到 window（gallery 无 emit 渲染区）
  const last = await page.evaluate(() => (window as unknown as { __lovLast?: { value: string; id: number } }).__lovLast);
  expect(last).toEqual({ value: 'StarServer（服务端）', id: 3 });
});

test('AppLov 无候选：不弹层不报错', async ({ page }) => {
  await openStory(page, 'AppLov/NoHit');
  await page.locator(LOV).fill('any');
  await expect(page.locator('.el-autocomplete-suggestion li')).toHaveCount(0);
});
