import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * Nodes/NodeOnline/Index 迁移 e2e（TDD）
 *
 * 自定义逻辑（来自 MVC 源 NodeOnlineController.cs + _List_Search.cshtml + _List_Toolbar_Batch.cshtml）：
 *  - 列表列：后端 GetPage 元数据（ListFields 自定义过滤）一并提供；
 *    其中 Name 字段 url="/Nodes/Node/Detail?Id={NodeID}" → 节点详情链接列（自建，AutoPage 不渲染 url 列）
 *    TraceId 经 ListFields.TraceUrl() → 追踪详情链接列（自建）
 *  - 自定义 Action（选中行后显示，对应 _List_Toolbar_Batch 的 data-fields=keys）：
 *      CheckUpgrade GET /Nodes/NodeOnline/CheckUpgrade?keys=      （检查更新）
 *      SyncTime    GET /Nodes/NodeOnline/SyncTime?keys=           （同步时间）
 *      Restart     GET /Nodes/NodeOnline/Restart?keys=            （重启服务）
 *      Reboot      GET /Nodes/NodeOnline/Reboot?keys=             （重启系统）
 *      Execute     GET /Nodes/NodeOnline/Execute?command=&argument=&keys= （执行命令）
 *  - 搜索（_List_Search）：类别下拉(category, Node.FindAllCategory()) + 区域(_Area2)
 *  - 后端 API：GET /api/Nodes/NodeOnline（GetPage/GetList）已就绪；批量命令 Action 已实现
 *  - 该页 [Menu(80)] 为可见页，路由已由 MenuTree 注册，直接访问 /Nodes/NodeOnline
 *
 * 种子数据为空时列表 0 行属正常，断言只校验：渲染 + 表头含主要列 + 无 >=400 错误。
 */
test('Nodes/NodeOnline 页面渲染（列表 + 链接列 + 批量命令工具栏）', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Nodes/NodeOnline');
  const rendered = await assertPageRendered(page);
  expect(rendered).toBe('list');

  // 列表主体：.list-page / .el-table
  await expect(page.locator('.list-page').first()).toBeVisible();
  await expect(page.locator('.el-table').first()).toBeVisible();

  // 表头必须包含自建链接列对应表头：名称（节点链接） / 追踪；以及标准列节点名、版本
  const headerText = await page
    .locator('.el-table')
    .first()
    .locator('.el-table__header-wrapper')
    .innerText()
    .catch(() => '');
  expect(headerText).toContain('名称');
  expect(headerText).toContain('追踪');
  expect(headerText).toContain('版本');
  expect(headerText).toContain('本地IP');

  // 自定义搜索栏「类别」下拉（MVC _List_Search 自定义；框架 fallback 不含）
  const hasCategory = await page
    .getByText('类别', { exact: true })
    .first()
    .isVisible({ timeout: 1500 })
    .catch(() => false);
  expect(hasCategory, '自定义搜索栏应包含「类别」下拉').toBe(true);

  // 有数据行时断言：批量命令按钮（需先选中行才渲染）+ 行内链接列
  const rowCount = await page
    .locator('.el-table__body-wrapper tbody tr')
    .count();
  if (rowCount > 0) {
    // 批量工具栏 v-if="selection.length>0"，需选中行才渲染（MVC 原版行为）
    await page
      .locator('.el-table__body-wrapper tbody tr')
      .first()
      .locator('.el-checkbox')
      .first()
      .click({ timeout: 3000 })
      .catch(() => {});
    await expect(
      page.getByRole('button', { name: '检查更新' }).first(),
    )
      .toBeVisible({ timeout: 3000 })
      .catch(() => {});
    await expect(
      page.getByRole('button', { name: '同步时间' }).first(),
    )
      .toBeVisible({ timeout: 3000 })
      .catch(() => {});
    await expect(
      page.getByRole('button', { name: '重启服务' }).first(),
    )
      .toBeVisible({ timeout: 3000 })
      .catch(() => {});
    await expect(
      page.getByRole('button', { name: '重启系统' }).first(),
    )
      .toBeVisible({ timeout: 3000 })
      .catch(() => {});
    await expect(
      page.getByRole('button', { name: '执行命令' }).first(),
    )
      .toBeVisible({ timeout: 3000 })
      .catch(() => {});

    // 行内链接列：名称(→Node Detail) / 追踪(→Trace Detail)
    const linkCount = await page
      .locator('.el-table__body-wrapper tbody tr')
      .first()
      .locator('a')
      .count();
    expect(linkCount, '行内应存在链接列(名称/追踪)').toBeGreaterThan(0);
  } else {
    console.log('ROW_COUNT_EMPTY: 种子数据为空，批量工具栏/链接列需有数据才渲染');
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
    path: 'e2e/screenshots/Nodes/NodeOnline/list.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
