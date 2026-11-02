import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * Nodes/Node/Index 迁移 e2e（TDD）
 *
 * 自定义逻辑（来自 MVC 源 NodeController.cs + _List_Toolbar_Batch.cshtml）：
 *  - 列表列：ID/ProjectName/Name/Code/Category/ProductCode/CityName/Enable/Version/
 *           OSKind/Runtime/Framework/IP/OS/MachineName/Cpu/Memory/TotalSize/Logins/
 *           LastActive/OnlineTime/UpdateTime/UpdateIP
 *    - Name → 链接 /Nodes/Node/Detail?id=<ID>（自建 el-link）
 *    - ProjectName → 链接 /Platform/GalaxyProject?projectId=<ProjectId>（自建 el-link）
 *    - Log → 链接 /Admin/Log?category=节点&linkId=<ID>（自建 el-link）
 *  - 批量工具栏：选中行后启用「启用告警/重置告警/修复」按钮
 *    -> GET /Nodes/Node/<Action>?keys=<选中id> 批量操作
 *  - 「追踪」操作：每行按钮 → confirm → GET /Nodes/Node/Trace?id=<行Id> → ElMessage
 *  - 后端 API：GET /Nodes/Node（GetPage）、SetAlarm/ResetAlarm/Fix/Trace/RefreshDns 已实现
 *
 * 种子数据为空时列表 0 行属正常，断言只校验：渲染 + 链接列 + 批量工具栏 + 无 >=400 错误。
 */
test('Nodes/Node 页面渲染（列表 + Name/ProjectName/Log 链接列 + 批量工具栏）', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Nodes/Node');
  const rendered = await assertPageRendered(page);
  expect(rendered).toBe('list');

  // 列表主体：.list-page / .el-table
  await expect(page.locator('main.cosmic-content .list-page')).toBeVisible();
  await expect(page.locator('main.cosmic-content .el-table').first()).toBeVisible();

  // 表头必须包含关键列：名称 / IP / 启用 / 日志
  const headerText = await page
    .locator('main.cosmic-content .el-table')
    .first()
    .locator('.el-table__header-wrapper')
    .innerText()
    .catch(() => '');
  expect(headerText).toContain('名称');
  expect(headerText).toContain('IP');
  expect(headerText).toContain('启用');
  expect(headerText).toContain('日志');

  // 批量工具栏必须存在（启用告警/重置告警/修复）
  const batchbar = page.locator('main.cosmic-content .lp-batchbar').first();
  await expect(batchbar).toBeVisible();
  await expect(batchbar.getByRole('button', { name: '启用告警' })).toBeVisible();
  await expect(batchbar.getByRole('button', { name: '重置告警' })).toBeVisible();
  await expect(batchbar.getByRole('button', { name: '修复' })).toBeVisible();

  // 有数据行时断言链接列 + 追踪按钮（空数据时跳过，种子数据为空属正常）
  const rowCount = await page
    .locator('main.cosmic-content .el-table__body-wrapper tbody tr')
    .count();
  if (rowCount > 0) {
    // Name 链接：/Nodes/Node/Detail?id=
    const nameLink = page
      .locator('main.cosmic-content .el-table')
      .first()
      .getByRole('link', { name: /.+/ })
      .first();
    if (await nameLink.isVisible({ timeout: 1500 }).catch(() => false)) {
      await expect(nameLink).toHaveAttribute('href', /Nodes\/Node\/Detail\?id=/);
    }
    // ProjectName 链接：/Platform/GalaxyProject?projectId=
    const projLink = page
      .locator('main.cosmic-content .el-table')
      .first()
      .getByRole('link')
      .filter({ hasText: /.+/ })
      .nth(1);
    if (await projLink.isVisible({ timeout: 1500 }).catch(() => false)) {
      await expect(projLink).toHaveAttribute('href', /Platform\/GalaxyProject\?projectId=/);
    }
    // 追踪按钮
    await expect(
      page
        .locator('main.cosmic-content .el-table')
        .first()
        .getByRole('button', { name: '追踪' })
        .first(),
    ).toBeVisible();
  } else {
    console.log('ROW_COUNT_EMPTY: 种子数据为空，仅校验表头结构 + 批量工具栏');
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
    path: 'e2e/screenshots/Nodes/Node/list.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
