import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * Platform/GalaxyProject/Index 迁移 e2e（TDD）
 *
 * 自定义逻辑（来自 MVC 源 GalaxyProjectController.cs）：
 *  - [Menu(100, true)] 可见页，路由由 MenuTree 注册，直接访问 /Platform/GalaxyProject
 *  - 列表列：编号 Id / 名称 Name / 启用 Enable / 租户 TenantId / 管理者 ManagerId /
 *           节点数 Nodes / 应用数 Apps / 排序 Sort / 全局 IsGlobal / IP白名单 / IP黑名单 /
 *           新服务器 / 更新时间 UpdateTime（RemoveCreateField + RemoveRemarkField 已去掉创建/备注）
 *  - 自定义 url 跳转列（后端 ListFields 的 Url 字段，自建链接列）：
 *       Name  → /Platform/GalaxyProject/Edit?id={Id}   （MVC 编辑页，target=_blank）
 *       Nodes → /Nodes/Node?projectId={Id}             （前端 Nodes/Node 列表，target=_blank）
 *       Apps  → /Registry/App?projectId={Id}           （前端 Registry/App 列表，target=_blank）
 *  - 搜索：控制器 Search 支持 dtStart/dtEnd（日期范围）
 *  - 后端 API：GET /Platform/GalaxyProject（GetPage/GetList）已就绪；无自定义 Action
 *
 * 种子数据为空时列表 0 行属正常，断言只校验：渲染 + 表头含主要列 + 链接列存在 + 无 >=400 错误。
 */
test('Platform/GalaxyProject 页面渲染（列表 + 链接列 + 搜索）', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Platform/GalaxyProject');
  const rendered = await assertPageRendered(page);
  expect(rendered).toBe('list');

  // 列表主体：.list-page / .el-table
  await expect(page.locator('.list-page').first()).toBeVisible();
  await expect(page.locator('.el-table').first()).toBeVisible();

  // 表头必须包含主要列：名称（链接列）/ 节点数（链接列）/ 应用数（链接列）
  const headerText = await page
    .locator('.el-table')
    .first()
    .locator('.el-table__header-wrapper')
    .innerText()
    .catch(() => '');
  expect(headerText).toContain('名称');
  expect(headerText).toContain('节点数');
  expect(headerText).toContain('应用数');

  // 自定义搜索栏「时间」范围（MVC GalaxyProjectController.Search 支持 dtStart/dtEnd；框架 fallback 不含）
  const hasTime = await page
    .getByText('时间', { exact: true })
    .first()
    .isVisible({ timeout: 1500 })
    .catch(() => false);
  expect(hasTime, '自定义搜索栏应包含「时间」范围').toBe(true);

  // 有数据行时断言：行内「节点数」/「应用数」链接列（跳转到 Nodes/Node 与 Registry/App）
  const rowCount = await page
    .locator('.el-table__body-wrapper tbody tr')
    .count();
  if (rowCount > 0) {
    const firstRowLinks = page
      .locator('.el-table')
      .first()
      .locator('.el-table__body-wrapper tbody tr')
      .first()
      .locator('.el-link');
    const linkCount = await firstRowLinks.count();
    if (linkCount >= 2) {
      await expect(firstRowLinks.nth(1)).toHaveAttribute('href', /Nodes\/Node/);
      await expect(firstRowLinks.nth(2)).toHaveAttribute('href', /Registry\/App/);
    }
  } else {
    console.log('ROW_COUNT_EMPTY: 种子数据为空，链接列需有数据才渲染');
  }

  const captured = await errs;
  if (captured.errors.length) {
    console.log('NET_ERR=' + JSON.stringify(captured.errors));
    throw new Error('存在接口/资源 >=400 错误: ' + JSON.stringify(captured.errors));
  }
  if (captured.consoleErrors.length) {
    console.log('CONSOLE_ERR=' + JSON.stringify(captured.consoleErrors));
  }

  // 截图：列表态
  await page.screenshot({
    path: 'e2e/screenshots/Platform/GalaxyProject/list.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
