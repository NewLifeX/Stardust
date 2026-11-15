import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('PipelineConfigDrawer 编辑回填：名称/分支/编译节点/部署节点勾选/自动部署/启用/项目级联只读', async ({ page }) => {
  await openStory(page, 'PipelineConfigDrawer/Edit');
  await expect(page.locator('[data-spec="pipeline-form-name"]')).toHaveValue('build-main');
  await expect(page.locator('[data-spec="pipeline-form-branch"]')).toHaveValue('release/*');
  await expect(page.locator('[data-spec="pipeline-form-buildnode"]')).toHaveValue('#3');
  await expect(page.locator('[data-spec="pipeline-form-node-1"]')).toBeChecked();
  await expect(page.locator('[data-spec="pipeline-form-node-2"]')).toBeChecked();
  await expect(page.locator('[data-spec="pipeline-form-autodeploy"]')).toBeChecked();
  await expect(page.locator('[data-spec="pipeline-form-enable"]')).toBeChecked();
  await expect(page.locator('[data-spec="pipeline-form-project"]')).toContainText('#88');
  // 敏感字段不进表单（实现规范 §6.5 + 裁定 #6）：Token/Secret/TraceId 无输入位
  const html = await page.locator('[data-spec="drawer"]').innerHTML();
  expect(html).not.toContain('Token');
  expect(html).not.toContain('Secret');
  await expect(page.locator('[data-spec="pipeline-form-submit"]')).toContainText('保存');
  await expect(page.locator('[data-spec="pipeline-form-cancel"]')).toContainText('取消');
});

test('PipelineConfigDrawer 校验：名称为空提交 → 「名称不能为空」逐字', async ({ page }) => {
  await openStory(page, 'PipelineConfigDrawer/Create');
  await expect(page.locator('[data-spec="pipeline-form-name"]')).toHaveValue('');
  await page.locator('[data-spec="pipeline-form-submit"]').click();
  await expect(page.locator('[data-spec="pipeline-form-error"]')).toHaveText('名称不能为空');
  // 填上名称后错误消失
  await page.locator('[data-spec="pipeline-form-name"]').fill('build-main');
  await page.locator('[data-spec="pipeline-form-submit"]').click();
  await expect(page.locator('[data-spec="pipeline-form-error"]')).toHaveCount(0);
});

test('PipelineConfigDrawer 保存中：提交按钮禁用「保存中…」（AGENTS.md §11）', async ({ page }) => {
  await openStory(page, 'PipelineConfigDrawer/Busy');
  const submit = page.locator('[data-spec="pipeline-form-submit"]');
  await expect(submit).toBeDisabled();
  await expect(submit).toContainText('保存中…');
  await expect(page.locator('[data-spec="pipeline-form-cancel"]')).toBeEnabled();
});

test('PipelineConfigDrawer 级联态：部署节点加载中；节点为空与 suggest 失败降级文案', async ({ page }) => {
  await openStory(page, 'PipelineConfigDrawer/Create');
  await expect(page.locator('[data-spec="pipeline-form-nodesloading"]')).toContainText('节点列表加载中');

  await openStory(page, 'PipelineConfigDrawer/NodesEmpty');
  await expect(page.locator('[data-spec="pipeline-form-nodesempty"]')).toContainText('部署集还没有启用节点');
});
