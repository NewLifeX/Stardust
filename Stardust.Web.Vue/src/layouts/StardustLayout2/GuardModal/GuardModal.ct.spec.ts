import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

// GuardModal 是 fixed 铺满视口，.gal-preview 只有 240px 高会裁掉它，
// 所以视觉用例截整个视口（不改预览容器样式，见 AGENTS.md §5.1 第 4 条）。
async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  // 等 x-fade / x-pop 入场动画走完，避免截到中间帧
  await page.waitForTimeout(500);
}

const MODAL = '[data-spec="guard-modal"]';
const DIALOG = '[data-spec="guard-modal"] [role="dialog"]';
const RESULT = '[data-spec="story-result"]';

test('GuardModal 典型文案', async ({ page }) => {
  await openStory(page, 'GuardModal/Basic');
  await expect(page).toHaveScreenshot();
});

test('GuardModal 长标题', async ({ page }) => {
  await openStory(page, 'GuardModal/LongLabel');
  await expect(page).toHaveScreenshot();
});

// 语义断言：文案
test('GuardModal 显示警示条、标题与副文字', async ({ page }) => {
  await openStory(page, 'GuardModal/Basic');
  await expect(page.locator('[data-spec="guard-warnbar"]')).toContainText('Production · 生产环境');
  await expect(page.locator('[data-spec="guard-title"]')).toHaveText('回滚 IotWeb 配置到 v12');
  await expect(page.locator(DIALOG)).toContainText('该操作会立即影响线上流量,并记入审计日志。');
  await expect(page.locator('[data-spec="guard-cancel"]')).toHaveText('取消');
  await expect(page.locator('[data-spec="guard-confirm"]')).toHaveText('确认执行');
});

// 语义断言：事件（组件只发事件，不自己关闭）
test('GuardModal 点取消发 cancel', async ({ page }) => {
  await openStory(page, 'GuardModal/Basic');
  await page.locator('[data-spec="guard-cancel"]').click();
  await expect(page.locator(RESULT)).toHaveText('cancel');
  await expect(page.locator(MODAL)).toBeVisible();
});

test('GuardModal 点确认发 confirm', async ({ page }) => {
  await openStory(page, 'GuardModal/Basic');
  await page.locator('[data-spec="guard-confirm"]').click();
  await expect(page.locator(RESULT)).toHaveText('confirm');
});

test('GuardModal 点遮罩发 cancel', async ({ page }) => {
  await openStory(page, 'GuardModal/Basic');
  // (100, 400) 在 1280 宽视口里落在弹窗左侧的遮罩上（弹窗居中，x 约 310 到 970）
  await page.mouse.click(100, 400);
  await expect(page.locator(RESULT)).toHaveText('cancel');
});

test('GuardModal 点弹窗本体不冒泡成取消', async ({ page }) => {
  await openStory(page, 'GuardModal/Basic');
  await page.locator('[data-spec="guard-title"]').click();
  await expect(page.locator(RESULT)).toHaveText('');
});

// 约定：Esc 不由组件处理，由外壳按原型顺序逐层关闭
test('GuardModal 自己不响应 Esc', async ({ page }) => {
  await openStory(page, 'GuardModal/Basic');
  await page.keyboard.press('Escape');
  await expect(page.locator(RESULT)).toHaveText('');
  await expect(page.locator(MODAL)).toBeVisible();
});

// 版式与层级（原型 --z-mask 为 60，.x-mask backdrop-filter blur(8px)，.x-modal 宽 min(660px, 96vw)）
test('GuardModal 遮罩层级 60 且带 8px 模糊', async ({ page }) => {
  await openStory(page, 'GuardModal/Basic');
  const mask = page.locator(MODAL);
  await expect(mask).toHaveCSS('z-index', '60');
  await expect(mask).toHaveCSS('backdrop-filter', 'blur(8px)');
});

test('GuardModal 弹窗宽 660', async ({ page }) => {
  await openStory(page, 'GuardModal/Basic');
  const box = await page.locator(DIALOG).boundingBox();
  expect(box).not.toBeNull();
  expect(Math.abs(box!.width - 660)).toBeLessThan(1);
});

test('GuardModal 长标题自然换行，不横向溢出', async ({ page }) => {
  await openStory(page, 'GuardModal/LongLabel');
  const overflow = await page.locator(DIALOG).evaluate((el) => el.scrollWidth > el.clientWidth);
  expect(overflow).toBe(false);
});
