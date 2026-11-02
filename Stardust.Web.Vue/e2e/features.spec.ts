import { test, expect } from './fixtures';
import type { Page, Locator } from '@playwright/test';
import { TOP_MENU_SELECTOR } from './config';

/** 取页面上的一级菜单项（排除嵌套在 .menu-children 内的子项） */
async function getTopMenuItems(page: Page): Promise<Locator[]> {
  const all = page.locator(TOP_MENU_SELECTOR);
  const n = await all.count();
  const items: Locator[] = [];
  for (let i = 0; i < n; i++) {
    const item = all.nth(i);
    const isTop = await item.evaluate((el) => !el.closest('.menu-children'));
    if (isTop) items.push(item);
  }
  return items;
}

// 验证登录态有效、主框架与菜单渲染正常；全程走真实后端，绝不 mock。
test('登录态有效，主框架与菜单正常渲染', async ({ page }) => {
  await page.goto('/');
  await expect(page.locator('.cosmic-menu .menu-item').first(), '应渲染主框架菜单').toBeVisible({
    timeout: 30_000,
  });

  const topItems = await getTopMenuItems(page);
  expect(topItems.length, '一级菜单不应为空').toBeGreaterThan(0);

  const labels = await Promise.all(topItems.map((it) => it.innerText()));
  console.log('TOP_MENU_COUNT=' + topItems.length);
  console.log('TOP_MENU_LABELS=' + JSON.stringify(labels.map((t) => t.trim())));
});

test('遍历一级菜单均可展开/跳转且页面正常', async ({ page }) => {
  await page.goto('/');
  const topItems = await getTopMenuItems(page);

  for (let i = 0; i < topItems.length; i++) {
    const item = topItems[i];
    const label = (await item.innerText()).trim();
    await item.click();
    await page.waitForTimeout(800);

    await expect(page).not.toHaveURL(/\/login/);

    const errToast = page.locator('.el-message--error');
    await expect(errToast, `菜单「${label}」不应出现错误提示`).toHaveCount(0);

    console.log('MENU_OK=' + label);
  }
});
