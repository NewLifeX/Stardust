import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('NodeFormDrawer 部署节点编辑：初值回填，节点选择器选中对应节点', async ({ page }) => {
  await openStory(page, 'NodeFormDrawer/DeployEdit');
  await expect(page.locator('[data-spec="node-form-node"]')).toHaveValue('2');
  await expect(page.locator('[data-spec="node-form-node"]')).toContainText('node-b（10.0.0.2）');
  await expect(page.locator('[data-spec="node-form-port"]')).toHaveValue('8080');
  await expect(page.locator('[data-spec="node-form-delay"]')).toHaveValue('5');
  await expect(page.locator('[data-spec="node-form-maxmemory"]')).toHaveValue('1024');
  await expect(page.locator('[data-spec="node-form-environments"]')).toHaveValue('ENV=prod');
  await expect(page.locator('[data-spec="node-form-healthcheck"]')).toHaveValue('http://*/health');
  await expect(page.locator('[data-spec="node-form-enable"]')).toBeChecked();
  // 编译节点字段不应出现在部署节点表单里
  await expect(page.locator('[data-spec="node-form-sourcepath"]')).toHaveCount(0);
});

test('NodeFormDrawer 新增：空表单；校验错误在视线内且不发提交', async ({ page }) => {
  await openStory(page, 'NodeFormDrawer/DeployCreate');
  await expect(page.locator('[data-spec="node-form-node"]')).toHaveValue('');
  await page.locator('[data-spec="node-form-submit"]').click();
  await expect(page.locator('[data-spec="node-form-error"]')).toHaveText('请选择物理节点');
});

test('NodeFormDrawer 节点列表加载中：显示加载文案而不是空选择器', async ({ page }) => {
  await openStory(page, 'NodeFormDrawer/NodesLoading');
  await expect(page.locator('[data-spec="node-form-loading"]')).toContainText('节点列表加载中');
  await expect(page.locator('[data-spec="node-form-node"]')).toHaveCount(0);
});

test('NodeFormDrawer 编译节点：源代码目录与四个阶段开关回填', async ({ page }) => {
  await openStory(page, 'NodeFormDrawer/BuildEdit');
  await expect(page.locator('[data-spec="node-form-node"]')).toHaveValue('3');
  await expect(page.locator('[data-spec="node-form-sourcepath"]')).toHaveValue('/src/MindHub.Api');
  await expect(page.locator('[data-spec="node-form-outputpath"]')).toHaveValue('./publish');
  await expect(page.locator('[data-spec="node-form-pullcode"]')).toBeChecked();
  await expect(page.locator('[data-spec="node-form-build"]')).toBeChecked();
  await expect(page.locator('[data-spec="node-form-package"]')).toBeChecked();
  await expect(page.locator('[data-spec="node-form-upload"]')).toBeChecked();
  // 部署节点字段不应出现在编译节点表单里
  await expect(page.locator('[data-spec="node-form-port"]')).toHaveCount(0);
});

// AGENTS.md §11：保存请求进行中按钮禁用并写「保存中…」
test('NodeFormDrawer 保存中：提交按钮禁用写「保存中…」', async ({ page }) => {
  await openStory(page, 'NodeFormDrawer/Busy');
  const btn = page.locator('[data-spec="node-form-submit"]');
  await expect(btn).toBeDisabled();
  await expect(btn).toHaveText('保存中…');
  await expect(page.locator('[data-spec="node-form-cancel"]')).toBeEnabled();
});
