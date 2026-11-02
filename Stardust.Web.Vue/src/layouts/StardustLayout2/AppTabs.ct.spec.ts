import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

const TAB = '[data-spec="app-tab"]';

// 语义断言：十四个页签（原型五个、注册侧五个、部署四个），「实例」带数字
test('AppTabs 十四个页签，顺序固定，实例带数字', async ({ page }) => {
  await openStory(page, 'AppTabs/Overview');
  await expect(page.locator(TAB)).toHaveText([
    '概览', '调用链', '日志', '配置', '实例 3',
    '应用历史', '命令', '性能', '注册', '消费',
    '版本', '节点', '部署历史', '流水线',
  ]);
});

test('AppTabs 实例数为 0 也显示', async ({ page }) => {
  await openStory(page, 'AppTabs/NoInstance');
  await expect(page.locator(`${TAB}[data-key="instances"]`)).toHaveText('实例 0');
});

// 语义断言：高 34，仅激活项 aria-selected，激活项下边框 2px
test('AppTabs 页签高 34，只有激活项被选中', async ({ page }) => {
  await openStory(page, 'AppTabs/Versions');
  await expect(page.locator(TAB).first()).toHaveCSS('height', '34px');
  await expect(page.locator(`${TAB}[aria-selected="true"]`)).toHaveCount(1);
  await expect(page.locator(`${TAB}[data-key="versions"]`)).toHaveAttribute('aria-selected', 'true');
  await expect(page.locator(`${TAB}[data-key="versions"]`)).toHaveCSS('border-bottom-width', '2px');
  await expect(page.locator(`${TAB}[data-key="overview"]`)).toHaveAttribute('aria-selected', 'false');
});

// 语义断言：激活项与非激活项文字色不同；悬停非激活项文字色变化（过渡约 120ms）
test('AppTabs 激活色与悬停色', async ({ page }) => {
  await openStory(page, 'AppTabs/Overview');
  const color = (key: string) =>
    page.locator(`${TAB}[data-key="${key}"]`).evaluate((el) => getComputedStyle(el).color);
  const active = await color('overview');
  const idle = await color('logs');
  expect(active).not.toBe(idle);

  await page.locator(`${TAB}[data-key="logs"]`).hover();
  await page.waitForTimeout(250);
  expect(await color('logs')).not.toBe(idle);
});
