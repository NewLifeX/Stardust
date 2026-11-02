import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * Platform/McpTokenResource/Index 迁移 e2e（TDD）
 *
 * 后端 McpTokenResourceController.cs [Menu(20, true)] 可见页，路由由 MenuTree 注册，
 * 直接访问 /Platform/McpTokenResource。
 * 实体 McpTokenResource.cs 字段：Id / TokenId(令牌) / ResourceType(资源类型) /
 *   ResourceId(资源编号) / IsAll(全部资源) / Enable(启用) / CreateTime(授权时间)。
 * 控制器 ListFields.RemoveField("Remark","CreateIP") + RemoveCreateField
 *   → 列表列不含 Remark/CreateIP/CreateTime 等创建字段。
 *   实测后端 GetPage 元数据返回的列表列为：编号 / 令牌 / 资源类型 / 资源编号 / 全部资源 / 启用。
 * 列表列由后端 GetPage 元数据提供；本页自建 el-table 还原字段。
 *
 * 后端 API：GET /Platform/McpTokenResource（GetPage/GetList）已就绪。
 * 种子数据为空时列表 0 行属正常，断言只校验：渲染 + 主要列存在 + 无 >=400 错误。
 */
test('Platform/McpTokenResource 页面渲染（资源授权列表）', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Platform/McpTokenResource');
  const rendered = await assertPageRendered(page);
  expect(rendered).toBe('list');

  // 列表主体：.list-page / .el-table
  await expect(page.locator('.list-page').first()).toBeVisible();
  await expect(page.locator('.el-table').first()).toBeVisible();

  // 表头必须包含标准列（由后端 GetPage 元数据提供）
  const headerText = await page
    .locator('.el-table')
    .first()
    .locator('.el-table__header-wrapper')
    .innerText()
    .catch(() => '');
  expect(headerText).toContain('编号');
  expect(headerText).toContain('令牌');
  expect(headerText).toContain('资源类型');
  expect(headerText).toContain('资源编号');
  expect(headerText).toContain('全部资源');
  expect(headerText).toContain('启用');

  // 不应出现被移除的扩展字段（RemoveField + RemoveCreateField）
  expect(headerText).not.toContain('授权地址');
  expect(headerText).not.toContain('备注');
  expect(headerText).not.toContain('授权时间');

  // 一致性加固：若有数据行，断言首行已正确取数（camelCase 行数据映射到 PascalCase 字段名），
  // 防止回归：单元格取值键与后端字段命名不匹配导致整列空白。
  const rowCount2 = await page
    .locator('.el-table__body-wrapper tbody tr')
    .count();
  if (rowCount2 > 0) {
    const headerCells = headerText.split(/\s+/);
    const tokenCol = Math.max(0, headerCells.indexOf('令牌'));
    const firstRow = page.locator('.el-table__body-wrapper tbody tr').first();
    const tokenText = (await firstRow.locator('td').nth(tokenCol).innerText().catch(() => '')) || '';
    expect(tokenText.trim().length).toBeGreaterThan(0); // 令牌列应有值（非空串空白）
    expect(tokenText.trim().toLowerCase()).not.toBe('undefined'); // 字段大小写修复回归：不得显示 undefined
    // 启用/全部资源列应呈现「是/否」
    const rowText2 = (await firstRow.innerText().catch(() => '')) || '';
    expect(rowText2).toMatch(/是|否/);
    // 整行不得出现字面量 undefined（字段名大小写不匹配的退化信号）
    expect(rowText2.toLowerCase()).not.toContain('undefined');
    // 若后端返回授权时间（CreateTime，PascalCase）列，断言其取到真实日期而非 undefined（大小写兼容加固）
    const createTimeCol = headerCells.indexOf('授权时间');
    if (createTimeCol >= 0) {
      const ctText = (await firstRow.locator('td').nth(createTimeCol).innerText().catch(() => '')) || '';
      expect(ctText.trim()).toMatch(/\d{4}-\d{2}-\d{2}/); // 时间字段取到真实值（日期格式）
    }
  } else {
    console.log('ROW_COUNT_EMPTY: 种子数据为空，资源授权列表单元格取值需有数据才验证');
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
    path: 'e2e/screenshots/Platform/McpTokenResource/list.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
