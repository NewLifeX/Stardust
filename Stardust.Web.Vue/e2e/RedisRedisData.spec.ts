import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * Redis/RedisData/Index 迁移 e2e（TDD）
 *
 * 还原 MVC Redis 性能时序曲线（原 cshtml ECharts，后端迁移时已删除绘图，Vue 自行封装通用 EChart 组件）：
 *  - 隐藏页 [Menu(40,false)]，靠自定义视图兜底（框架 DefaultEntity 按视图目录兜底），/Redis/RedisData 可访问
 *  - 数据：GET /Redis/RedisData（GetPage，后端 Search 支持 redisId/dtStart/dtEnd）
 *  - 图表：X=CreateTime(时间轴)，Y=Speed(每秒操作数)/ConnectedClients(连接数)/UsedMemory(已用内存MB)/Commands(命令数)/入流量/出流量 曲线
 *  - 列表：RedisId/Name/Speed/ConnectedClients/UsedMemory/Commands/Keys/CreateTime 等（Id、TopCommand 已隐藏）
 *  - 搜索（加分项）：redisId 节点 + 日期范围 dtStart/dtEnd
 *
 * 种子数据为空时图表区显示空状态（el-empty），列表 0 行属正常：
 *   断言「页面渲染 + 无 >=400 错误 + 图表容器存在（canvas 或 空状态）」即可，不强行断言曲线数值。
 */
test('Redis/RedisData 页面渲染（EChart 时序曲线 + 列表 + 搜索）', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Redis/RedisData');
  const rendered = await assertPageRendered(page);
  expect(rendered).toBe('list');

  // 列表主体：.list-page / .el-table
  await expect(page.locator('main.cosmic-content .list-page')).toBeVisible();
  await expect(page.locator('main.cosmic-content .el-table').first()).toBeVisible();

  // 图表卡片标题
  await expect(page.locator('main.cosmic-content .lp-card-title')).toContainText('Redis 性能时序');

  // 图表容器：有数据时渲染 canvas（echarts 注入），无数据时显示空状态文案
  // 两者至少存在一个，不强行断言具体曲线
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

  // 列表表头：核心列集合与 MVC RedisDataController 静态 ListFields 一致
  // （图表系列 Speed/InputKbps/OutputKbps/ConnectedClients/UsedMemory/Keys；
  //   列表含 Uptime/碎片率/Keys/过期Keys/驱逐Keys/命中数/Miss数/命令数/读取数/写入数/平均过期/追踪）
  const dataHeaderText = await page
    .locator('main.cosmic-content .el-table')
    .first()
    .locator('.el-table__header-wrapper')
    .innerText()
    .catch(() => '');
  for (const col of ['Redis节点', '名称', '速度', '连接数', '已用内存', 'Keys数', '命令数', '创建时间']) {
    expect(dataHeaderText, `列表应包含列「${col}」`).toContain(col);
  }
  // 还原 MVC 完整列（本次补全项）的代表性存在性断言
  for (const col of ['启动时间', '碎片率', '过期Keys', '驱逐Keys', '命中数', 'Miss数', '读取数', '写入数', '平均过期', '追踪']) {
    expect(dataHeaderText, `列表应包含列「${col}」`).toContain(col);
  }

  // 搜索栏：redisId 节点输入框 + 日期范围
  await expect(page.locator('main.cosmic-content .rdd-search-input').first()).toBeVisible();

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
    path: 'e2e/screenshots/Redis/RedisData/list.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
