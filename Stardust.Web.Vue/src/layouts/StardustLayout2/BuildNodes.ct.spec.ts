import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

const ROW = '[data-spec="build-node-row"]';

test('BuildNodes 默认：三行编译节点，节点名、目录、Check/X 各自正确', async ({ page }) => {
  await openStory(page, 'BuildNodes/Default');
  await expect(page.locator(ROW)).toHaveCount(3);

  const a = page.locator(ROW).nth(0);
  await expect(a).toContainText('node-build-a');
  await expect(a).toContainText('10.0.0.11');
  await expect(a).toContainText('/src/MindHub.Api');
  await expect(a).toContainText('./publish');
  await expect(a).toContainText('2026-10-30 09:30');

  // 第二行：不拉代码（X），编译项目开（Check）；组件不渲染 remark（'x64' 断言曾与本组件漂移）
  const b = page.locator(ROW).nth(1);
  await expect(b).toContainText('node-build-b');
  await expect(b).toContainText('/src/Stardust');

  // 第三行：无名字 → #13，无 IP → #13
  const c = page.locator(ROW).nth(2);
  await expect(c).toContainText('#13');
});

test('BuildNodes 每行两个操作按钮，文案固定，空闲时都可点', async ({ page }) => {
  await openStory(page, 'BuildNodes/Default');
  const row = page.locator(ROW).nth(0);
  await expect(row.locator('[data-spec="build-op-Build-Upload"]')).toHaveText('编译上传');
  await expect(row.locator('[data-spec="build-op-Package-Upload"]')).toHaveText('打包上传');
  await expect(row.locator('[data-spec="build-op-Build-Upload"]')).toBeEnabled();
  await expect(row.locator('[data-spec="build-op-Package-Upload"]')).toBeEnabled();
});

// F08：新增编译节点入口 + 每行编辑/删除；删除进行中该行写「删除中…」且全禁用
test('BuildNodes 新增/编辑/删除入口；删除进行中禁用并写「删除中…」', async ({ page }) => {
  await openStory(page, 'BuildNodes/Default');
  await expect(page.locator('[data-spec="build-node-create"]')).toBeEnabled();
  const row = page.locator(ROW).nth(0);
  await expect(row.locator('[data-spec="build-node-edit"]')).toBeEnabled();
  await expect(row.locator('[data-spec="build-node-remove"]')).toBeEnabled();

  await openStory(page, 'BuildNodes/Removing');
  await expect(page.locator(ROW).nth(1).locator('[data-spec="build-node-remove"]')).toHaveText('删除中…');
  await expect(page.locator('[data-spec="build-node-create"]')).toBeDisabled();
  for (let r = 0; r < 3; r++) {
    await expect(page.locator(ROW).nth(r).locator('[data-spec="build-node-edit"]')).toBeDisabled();
    await expect(page.locator(ROW).nth(r).locator('[data-spec="build-node-remove"]')).toBeDisabled();
  }
});

test('BuildNodes 处理中：对应按钮写「处理中…」，所有按钮禁用', async ({ page }) => {
  await openStory(page, 'BuildNodes/Busy');
  await expect(page.locator(ROW).nth(0).locator('[data-spec="build-op-Build-Upload"]')).toHaveText('处理中…');
  for (let r = 0; r < 3; r++) {
    await expect(page.locator(ROW).nth(r).locator('[data-spec="build-op-Build-Upload"]')).toBeDisabled();
    await expect(page.locator(ROW).nth(r).locator('[data-spec="build-op-Package-Upload"]')).toBeDisabled();
  }
});

test('BuildNodes 空态与失败态文案不同', async ({ page }) => {
  await openStory(page, 'BuildNodes/Empty');
  await expect(page.locator('[data-spec="build-nodes-empty"]')).toContainText('该部署集还没有配置编译节点');
  await expect(page.locator('[data-spec="build-nodes-failed"]')).toHaveCount(0);

  await openStory(page, 'BuildNodes/Failed');
  await expect(page.locator('[data-spec="build-nodes-failed"]')).toContainText('编译节点加载失败');
  await expect(page.locator('[data-spec="build-nodes-empty"]')).toHaveCount(0);
});
