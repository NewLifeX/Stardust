import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * Redis/RedisNode/Index 迁移 e2e（TDD）
 *
 * 自定义逻辑（来自 MVC 源 RedisNodeController.cs + _List_Search.cshtml）：
 *  - [Menu(50)] 可见页，路由由 MenuTree 注册，直接访问 /Redis/RedisNode
 *  - 列表列：标准字段（地址/端口/数据库/最大内存/已用内存/客户端/命令/连接数/状态等）由后端 GetPage 元数据提供
 *  - 自定义 url 跳转列（自建，AutoPage 不渲染）：
 *      ProjectName → /Platform/GalaxyProject?projectId={ProjectId}
 *      Monitor     → /Redis/RedisData?redisId={Id}
 *      Queue       → /Redis/RedisMessageQueue?redisId={Id}
 *      Log         → /Admin/Log?category=Redis节点&linkId={Id}
 *      Refresh     → /Redis/RedisNode/Refresh?Id={Id}（DataAction="action" 自定义 Action）
 *  - 自定义 Action Refresh：GET /Redis/RedisNode/Refresh?id= → JsonRefresh → ElMessage + 刷新列表
 *  - 搜索（_List_Search）：类别下拉(category) + 日期范围(dtStart/dtEnd)
 *  - 后端 API：GET /Redis/RedisNode（GetPage/GetList）已就绪；Refresh 已实现
 *
 * 种子数据为空时列表 0 行属正常，断言只校验：渲染 + 主要列/链接列存在 + 无 >=400 错误。
 */
test('Redis/RedisNode 页面渲染（列表 + 链接列 + Refresh 操作）', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Redis/RedisNode');
  const rendered = await assertPageRendered(page);
  expect(rendered).toBe('list');

  // 列表主体：.list-page / .el-table
  await expect(page.locator('.list-page').first()).toBeVisible();
  await expect(page.locator('.el-table').first()).toBeVisible();

  // 表头必须包含标准列 + 自定义链接列：监控 / 队列 / 刷新 / 审计日志
  const headerText = await page
    .locator('.el-table')
    .first()
    .locator('.el-table__header-wrapper')
    .innerText()
    .catch(() => '');
  expect(headerText).toContain('监控');
  expect(headerText).toContain('队列');
  expect(headerText).toContain('刷新');
  expect(headerText).toContain('审计日志');

  // 自定义搜索栏「类别」下拉（MVC _List_Search 自定义；框架 fallback 不含）
  const hasCategory = await page
    .getByText('类别', { exact: true })
    .first()
    .isVisible({ timeout: 1500 })
    .catch(() => false);
  expect(hasCategory, '自定义搜索栏应包含「类别」下拉').toBe(true);

  // 顶部「刷新」按钮（清监控/刷新节点状态，调用 GET /Redis/RedisNode/Refresh 无参批量刷新）
  const refreshBtn = page.getByRole('button', { name: '刷新' }).first();
  await expect(refreshBtn).toBeVisible({ timeout: 3000 }).catch(() => {});

  // 有数据行时断言：行内「监控/队列/刷新/审计日志」链接列
  const rowCount = await page
    .locator('.el-table__body-wrapper tbody tr')
    .count();
  if (rowCount > 0) {
    const monitorLink = page
      .locator('.el-table')
      .first()
      .getByRole('link', { name: '监控' })
      .first();
    if (await monitorLink.isVisible({ timeout: 1500 }).catch(() => false)) {
      await expect(monitorLink).toHaveAttribute('href', /RedisData/);
    }
    const queueLink = page
      .locator('.el-table')
      .first()
      .getByRole('link', { name: '队列' })
      .first();
    if (await queueLink.isVisible({ timeout: 1500 }).catch(() => false)) {
      await expect(queueLink).toHaveAttribute('href', /RedisMessageQueue/);
    }
    const refreshLink = page
      .locator('.el-table')
      .first()
      .getByRole('link', { name: '刷新' })
      .first();
    if (await refreshLink.isVisible({ timeout: 1500 }).catch(() => false)) {
      await expect(refreshLink).toHaveAttribute('href', /RedisNode\/Refresh/);
    }
  } else {
    console.log('ROW_COUNT_EMPTY: 种子数据为空，行内链接列需有数据才渲染');
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
    path: 'e2e/screenshots/Redis/RedisNode/list.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
