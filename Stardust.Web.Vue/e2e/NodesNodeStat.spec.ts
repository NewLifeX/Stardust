import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * Nodes/NodeStat/Index 迁移 e2e（TDD）
 *
 * 还原 MVC 节点统计曲线（原 cshtml ECharts 已删除绘图，Vue 自行封装通用 EChart 组件）：
 *  - 可见页 [Menu(60)]，路由自动注册，/Nodes/NodeStat 可访问
 *  - 后端 API：GET /Nodes/NodeStat（GetPage，Search 默认 category=操作系统，dtStart/dtEnd）
 *  - 实体字段：Category / StatDate / Key / Total(总数) / Actives(活跃数) / ActivesT7 / ActivesT30 / News(新增) / NewsT7 / NewsT30
 *  - 图表：X=StatDate，Y 三条曲线（总数 Total / 活跃数 Actives / 新增数 News）
 *  - 搜索加分项：Category 下拉 + Key（节点/城市/版本维度） + 日期范围 dtStart/dtEnd
 *  - 种子数据为空时图表区显示空状态（el-empty），列表 0 行属正常
 *
 * 断言：「页面渲染 + 无 >=400 错误 + 图表容器存在（canvas 或 空状态文案）」即可。
 */
test('Nodes/NodeStat 页面渲染（EChart 曲线 + 列表 + 搜索）', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Nodes/NodeStat');
  const rendered = await assertPageRendered(page);
  expect(rendered).toBe('list');

  // 列表主体：.list-page / .el-table
  await expect(page.locator('main.cosmic-content .list-page')).toBeVisible();
  await expect(page.locator('main.cosmic-content .el-table').first()).toBeVisible();

  // 表头必须包含关键列名
  const headerText = await page
    .locator('main.cosmic-content .el-table')
    .first()
    .locator('.el-table__header-wrapper')
    .innerText()
    .catch(() => '');
  for (const col of ['统计日期', '类别', '统计项', '总数', '活跃数', '新增数']) {
    expect(headerText, `表头应含列「${col}」`).toContain(col);
  }

  // 图表卡片标题
  await expect(page.locator('main.cosmic-content .lp-card-title')).toContainText('节点统计曲线');

  // 图表容器：有数据时渲染 canvas（echarts 注入），无数据时显示空状态文案
  const hasCanvas = await page
    .locator('main.cosmic-content .lp-card canvas')
    .first()
    .isVisible()
    .catch(() => false);
  const hasEmpty = await page
    .locator('main.cosmic-content .lp-card .el-empty')
    .first()
    .isVisible()
    .catch(() => false);
  expect(hasCanvas || hasEmpty, '图表卡片应渲染 canvas 或空状态').toBeTruthy();

  // 搜索栏：类别下拉 + 日期范围
  await expect(page.locator('main.cosmic-content .ns-search-input').first()).toBeVisible();

  const captured = await errs;
  if (captured.errors.length) {
    console.log('NET_ERR=' + JSON.stringify(captured.errors));
    throw new Error('存在接口/资源 >=400 错误: ' + JSON.stringify(captured.errors));
  }
  if (captured.consoleErrors.length) {
    console.log('CONSOLE_ERR=' + JSON.stringify(captured.consoleErrors));
  }

  // 截图：列表 + 图表态（种子空时为空状态，仍应无塌陷/空白）
  await page.screenshot({
    path: 'e2e/screenshots/Nodes/NodeStat/list.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
