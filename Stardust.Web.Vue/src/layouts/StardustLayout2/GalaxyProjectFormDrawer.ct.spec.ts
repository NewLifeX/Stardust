import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

// CARD-PLT-01：GalaxyProjectFormDrawer（表单抽屉）四态 + 关闭态
test('Create：标题「新建项目」，全字段可见，默认启用开/全局关', async ({ page }) => {
  await openStory(page, 'GalaxyProjectFormDrawer/Create');
  await expect(page.locator('[data-spec="drawer"] h2')).toContainText('新建项目');
  await expect(page.locator('[data-spec="platform-project-form-name"]')).toHaveValue('');
  await expect(page.locator('[data-spec="platform-project-form-enable"]')).toBeChecked();
  await expect(page.locator('[data-spec="platform-project-form-global"]')).not.toBeChecked();
  await expect(page.locator('[data-spec="platform-project-form-tenant"]')).toHaveValue('0');
  await expect(page.locator('[data-spec="platform-project-form-manager"]')).toHaveValue('0');
  await expect(page.locator('[data-spec="platform-project-form-sort"]')).toHaveValue('0');
  await expect(page.locator('[data-spec="platform-project-form-whiteips"]')).toHaveValue('');
  await expect(page.locator('[data-spec="platform-project-form-blackips"]')).toHaveValue('');
  await expect(page.locator('[data-spec="platform-project-form-newserver"]')).toHaveValue('');
  await expect(page.locator('[data-spec="platform-project-form-remark"]')).toHaveValue('');
  // hint 逐字（旧壳/盘点旁证）
  await expect(page.locator('[data-spec="drawer"]')).toContainText('该项目的节点可以允许其它项目下应用选用');
  await expect(page.locator('[data-spec="drawer"]')).toContainText('符合条件的来源IP才允许访问，支持*通配符，多个逗号隔开');
  await expect(page.locator('[data-spec="drawer"]')).toContainText('符合条件的来源IP禁止访问，支持*通配符，多个逗号隔开');
  await expect(page.locator('[data-spec="drawer"]')).toContainText('该项目下的节点自动迁移到新的服务器地址');
});

test('Edit：标题「编辑项目」+ sub 显示项目名，字段回显', async ({ page }) => {
  await openStory(page, 'GalaxyProjectFormDrawer/Edit');
  await expect(page.locator('[data-spec="drawer"] h2')).toContainText('编辑项目');
  await expect(page.locator('[data-spec="platform-project-form-name"]')).toHaveValue('prod');
  await expect(page.locator('[data-spec="platform-project-form-global"]')).toBeChecked();
  await expect(page.locator('[data-spec="platform-project-form-tenant"]')).toHaveValue('2');
  await expect(page.locator('[data-spec="platform-project-form-sort"]')).toHaveValue('4');
  await expect(page.locator('[data-spec="platform-project-form-whiteips"]')).toHaveValue('10.0.0.1');
  await expect(page.locator('[data-spec="platform-project-form-remark"]')).toHaveValue('生产项目');
});

test('NameError：内联校验可见且 role=alert（旧壳逐字「请输入项目名称」）', async ({ page }) => {
  await openStory(page, 'GalaxyProjectFormDrawer/NameError');
  const err = page.locator('[data-spec="platform-project-form-name-error"]');
  await expect(err).toHaveAttribute('role', 'alert');
  await expect(err).toContainText('请输入项目名称');
});

test('Busy：保存中按钮禁用+文案「保存中…」，输入禁用', async ({ page }) => {
  await openStory(page, 'GalaxyProjectFormDrawer/Busy');
  await expect(page.locator('[data-spec="platform-project-form-save"]')).toBeDisabled();
  await expect(page.locator('[data-spec="platform-project-form-save"]')).toContainText('保存中…');
  await expect(page.locator('[data-spec="platform-project-form-cancel"]')).toBeDisabled();
  await expect(page.locator('[data-spec="platform-project-form-name"]')).toBeDisabled();
});

test('Closed：open=false 不渲染抽屉', async ({ page }) => {
  await openStory(page, 'GalaxyProjectFormDrawer/Closed');
  // ElDrawer 关闭态：overlay 根元素仍挂在 DOM（v-show 隐藏），destroy-on-close 只销毁内容——
  // 断言「不可见」而非「不在 DOM」，与 Drawer/Closed 既有约定一致。
  await expect(page.locator('[data-spec="drawer"]')).toBeHidden();
});
