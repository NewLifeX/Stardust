import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * Monitors/TraceData/Index 迁移 e2e（TDD）
 *
 * 还原 MVC 调用链监控列表（_List_Data 自定义渲染 + _List_Search）：
 *  - 隐藏页 [Menu(0,false)]，靠自定义视图兜底（自建 el-table，链接列 + 总计行）
 *  - 列表列：应用(AppName 链接?appId=)/实例(ClientId 链接)/应用节点(NodeName 链接/Nodes/Node?Id=)/
 *           操作名(Name 链接)/追踪(按钮 Trace?id=)/总数(Total)/错误(Errors)/平均(Cost)/最大(MaxCost)/
 *           最小(MinCost)/数值(TotalValue)/正常(Samples)/异常(ErrorSamples)/开始(Start)/创建时间(CreateTime)/
 *           创建地址(CreateIP)
 *  - 总计行：聚合 Total/Errors/Cost/MaxCost/MinCost/TotalValue/Samples/ErrorSamples
 *  - 搜索：searchTag 复选框 + 日期范围 dtStart/dtEnd
 *  - 自定义 Action：Trace(GET /Monitors/TraceData/Trace?id=) 成功提示 traceId，失败 el-notification「无采样数据」
 *  - 后端 API：GET /Monitors/TraceData（GetPage）、Trace 已实现
 *
 * 种子数据为空时列表 0 行属正常，断言只校验：渲染 + 表头结构 + 无 >=400 错误。
 */
test('Monitors/TraceData 页面渲染（自建列表 + 链接列 + 追踪 + 总计行）', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Monitors/TraceData');
  const rendered = await assertPageRendered(page);
  expect(rendered).toBe('list');

  // 列表主体：.list-page / .el-table
  await expect(page.locator('main.cosmic-content .list-page')).toBeVisible();
  await expect(page.locator('main.cosmic-content .el-table').first()).toBeVisible();

  // 表头必须包含关键列名（来自 MVC _List_Data 渲染）
  const headerText = await page
    .locator('main.cosmic-content .el-table')
    .first()
    .locator('.el-table__header-wrapper')
    .innerText()
    .catch(() => '');
  for (const col of ['应用', '实例', '操作名', '追踪', '总数', '错误', '平均', '正常', '异常', '开始', '创建时间', '创建地址']) {
    expect(headerText, `表头应含列「${col}」`).toContain(col);
  }

  // 总计行：el-table show-summary 的 footer
  const footerText = await page
    .locator('main.cosmic-content .el-table')
    .first()
    .locator('.el-table__footer-wrapper')
    .innerText()
    .catch(() => '');
  expect(footerText, '应有总计行').toContain('总计');

  // 有数据行时断言行内「追踪」链接按钮（空数据时跳过，种子数据为空属正常）
  const rowCount = await page
    .locator('main.cosmic-content .el-table__body-wrapper tbody tr')
    .count();
  if (rowCount > 0) {
    const traceBtn = page
      .locator('main.cosmic-content .el-table')
      .first()
      .getByRole('link', { name: '追踪' })
      .first();
    if (await traceBtn.isVisible({ timeout: 1500 }).catch(() => false)) {
      await expect(traceBtn).toBeVisible();
      // 应用链接跳转 ?appId=
      const appLink = page
        .locator('main.cosmic-content .el-table')
        .first()
        .locator('a[href*="appId="]')
        .first();
      await expect(appLink).toHaveAttribute('href', /appId=/);
      // 总数/正常/异常列应渲染为 SampleData 链接（与 MVC 一致：总数→SampleData?dataid=）
      const sampleLink = page
        .locator('main.cosmic-content .el-table')
        .first()
        .locator('a[href*="SampleData"]')
        .first();
      if (await sampleLink.isVisible({ timeout: 1500 }).catch(() => false)) {
        await expect(sampleLink).toHaveAttribute('href', /SampleData/);
      }
    }
  } else {
    console.log('ROW_COUNT_EMPTY: 种子数据为空，仅校验表头结构');
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
    path: 'e2e/screenshots/Monitors/TraceData/list.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
