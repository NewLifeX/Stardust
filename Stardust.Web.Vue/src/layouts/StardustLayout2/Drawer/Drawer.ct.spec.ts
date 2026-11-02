import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  // 等入场过渡结束，避免截到动画中间帧
  await page.waitForTimeout(500);
}

// 截图：抽屉是 fixed 定位、贴视口右侧铺到底，.gal-preview 只有 240px 高会裁掉它，
// 所以 Drawer 的视觉用例截整个视口（不改预览容器样式，见 AGENTS.md §5.1 第 4 条）。
test('Drawer 标题加副标题', async ({ page }) => {
  await openStory(page, 'Drawer/Basic');
  await expect(page).toHaveScreenshot();
});

test('Drawer 无副标题', async ({ page }) => {
  await openStory(page, 'Drawer/NoSub');
  await expect(page).toHaveScreenshot();
});

test('Drawer 带操作按钮行', async ({ page }) => {
  await openStory(page, 'Drawer/WithActions');
  await expect(page).toHaveScreenshot();
});

// 语义断言
test('Drawer 头部显示标题与副标题', async ({ page }) => {
  await openStory(page, 'Drawer/Basic');
  const drawer = page.locator('[data-spec="drawer"]');
  await expect(drawer).toBeVisible();
  await expect(drawer).toContainText('bj-app-01');
  await expect(drawer).toContainText('10.1.10.1');
});

test('Drawer 没传 sub 时不渲染副标题', async ({ page }) => {
  await openStory(page, 'Drawer/NoSub');
  const drawer = page.locator('[data-spec="drawer"]');
  await expect(drawer).toContainText('bj-app-01');
  await expect(drawer).not.toContainText('10.1.10.1');
});

test('Drawer 只在传了 actions 插槽时渲染操作行', async ({ page }) => {
  await openStory(page, 'Drawer/Basic');
  await expect(page.locator('[data-spec="story-action"]')).toHaveCount(0);

  await openStory(page, 'Drawer/WithActions');
  await expect(page.locator('[data-spec="story-action"]')).toBeVisible();
});

test('Drawer 关闭态不可见', async ({ page }) => {
  await openStory(page, 'Drawer/Closed');
  await expect(page.locator('[data-spec="drawer"]')).toBeHidden();
});

// 版式：从 56px 顶栏下沿开始，宽 560，贴视口右侧
test('Drawer 从顶栏下沿开始、宽 560、贴右', async ({ page }) => {
  await openStory(page, 'Drawer/Basic');
  const box = await page.locator('[data-spec="drawer"]').boundingBox();
  const viewport = page.viewportSize();
  expect(box).not.toBeNull();
  expect(viewport).not.toBeNull();
  expect(box!.y).toBeCloseTo(56, 0);
  expect(box!.width).toBeCloseTo(560, 0);
  expect(box!.x + box!.width).toBeCloseTo(viewport!.width, 0);
});

// 层级：原型 --z-drawer 为 40，低于命令面板与模态框的 60
test('Drawer 遮罩层级 40 且带 8px 模糊', async ({ page }) => {
  await openStory(page, 'Drawer/Basic');
  const overlay = page.locator('.el-overlay');
  await expect(overlay).toHaveCSS('z-index', '40');
  await expect(overlay).toHaveCSS('backdrop-filter', 'blur(8px)');
});

// 关闭：X、点遮罩、Esc 都只发 close，由外壳把 open 置 false
test('Drawer 点 X 关闭', async ({ page }) => {
  await openStory(page, 'Drawer/Basic');
  await page.locator('[data-spec="drawer-close"]').click();
  await expect(page.locator('[data-spec="drawer"]')).toBeHidden();
});

test('Drawer 点遮罩关闭', async ({ page }) => {
  await openStory(page, 'Drawer/Basic');
  // 画廊左侧有 250px 宽的导航（z-index 9999，盖在遮罩上）；抽屉从 x=720 起，
  // 取两者之间的 (500, 400)，一定落在遮罩上
  await page.mouse.click(500, 400);
  await expect(page.locator('[data-spec="drawer"]')).toBeHidden();
});

test('Drawer 按 Esc 关闭', async ({ page }) => {
  await openStory(page, 'Drawer/Basic');
  await page.keyboard.press('Escape');
  await expect(page.locator('[data-spec="drawer"]')).toBeHidden();
});
