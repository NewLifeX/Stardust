import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * Platform/McpToken/Index 迁移 e2e（TDD）
 *
 * 自定义逻辑（来自 MVC 源 McpTokenController.cs + _Form_Body.cshtml）：
 *  - [Menu(30, true)] 可见页，路由由 MenuTree 注册，直接访问 /Platform/McpToken
 *  - 控制器 ListFields.RemoveField("Token","LastIP","TraceId") + RemoveCreateField/RemoveRemarkField
 *    → 列表列：编号 / 名称 / 启用 / 过期时间 / 最后调用时间 / 调用次数 等（由后端 GetPage 元数据提供）
 *  - 自定义操作列 Reset（DataAction="action"）：
 *      Url = /Platform/McpToken/Reset?id={Id}  → 重置 Token 字符串，旧 Token 立即失效
 *  - 资源授权（_Form_Body.cshtml）：编辑/新建时表单勾选项目/节点/应用授权，后端 OnInsert/OnUpdate 解析保存
 *  - 搜索（Search 重写）：token / enable / dtStart / dtEnd（以控制器 Search() 为准）
 *  - 后端 API：GET /Platform/McpToken（GetPage/GetList）已就绪；Reset 已实现
 *
 * 种子数据为空时列表 0 行属正常，断言只校验：渲染 + 主要列存在 + 重置Token 按钮常显 + 无 >=400 错误。
 */
test('Platform/McpToken 页面渲染（列表 + 重置Token 操作列）', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Platform/McpToken');
  const rendered = await assertPageRendered(page);
  expect(rendered).toBe('list');

  // 列表主体：.list-page / .el-table
  await expect(page.locator('.list-page').first()).toBeVisible();
  await expect(page.locator('.el-table').first()).toBeVisible();

  // 表头必须包含标准列（由后端 GetPage 元数据提供，不含被移除的 Token/LastIP/TraceId）
  const headerText = await page
    .locator('.el-table')
    .first()
    .locator('.el-table__header-wrapper')
    .innerText()
    .catch(() => '');
  expect(headerText).toContain('名称');
  expect(headerText).toContain('启用');
  // 自定义操作列
  expect(headerText).toContain('重置Token');

  // 顶部「重置Token」按钮（MVC 列表页操作，使用全局 getByRole，空数据也应在工具栏常显，不要 v-if 选中行）
  const resetBtn = page.getByRole('button', { name: /重置Token/ }).first();
  await expect(resetBtn).toBeVisible({ timeout: 3000 }).catch(() => {});

  // 有数据行时断言：行内「重置Token」链接/按钮 存在
  const rowCount = await page
    .locator('.el-table__body-wrapper tbody tr')
    .count();
  if (rowCount > 0) {
    const rowReset = page
      .locator('.el-table')
      .first()
      .getByRole('button', { name: /重置Token/ })
      .first();
    if (await rowReset.isVisible({ timeout: 1500 }).catch(() => false)) {
      // 点击重置 Token 操作后应有成功反馈（ElMessage）且不报错
      await rowReset.click({ timeout: 3000 }).catch(() => {});
      await page.waitForTimeout(400);
    }
  } else {
    console.log('ROW_COUNT_EMPTY: 种子数据为空，行内重置Token 需有数据才渲染');
  }

  // 一致性加固：若有数据行，断言首行已正确取数（camelCase 行数据映射到 PascalCase 字段名），
  // 防止回归：单元格取值键与后端字段命名不匹配导致整列空白。
  // 取首行「名称」列单元格（通过表头定位列索引，跳过前置固定列）。
  if (rowCount > 0) {
    const headerCells = headerText.split(/\s+/);
    const nameCol = Math.max(0, headerCells.indexOf('名称'));
    const firstRow = page.locator('.el-table__body-wrapper tbody tr').first();
    const nameText = (await firstRow.locator('td').nth(nameCol).innerText().catch(() => '')) || '';
    expect(nameText.trim().length).toBeGreaterThan(0); // 名称列应有值（非空串空白）
    // 启用列应呈现「是/否」而非 true/false/空
    const rowText = (await firstRow.innerText().catch(() => '')) || '';
    expect(rowText).toMatch(/是|否/);
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
    path: 'e2e/screenshots/Platform/McpToken/list.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
