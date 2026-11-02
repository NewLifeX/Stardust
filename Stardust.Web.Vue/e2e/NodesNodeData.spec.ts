import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, collectErrors } from './helpers';

/**
 * Nodes/NodeData/Index 迁移 e2e（TDD）
 *
 * 还原 MVC 节点性能时序数据曲线（原 cshtml ECharts，后端迁移时已删除绘图，Vue 自行封装通用 EChart 组件）：
 *  - 隐藏页 [Menu(60,false)]，靠自定义视图兜底（框架 DefaultEntity 按视图目录兜底），/Nodes/NodeData 可访问
 *  - 图表：X=LocalTime（采集时间），Y=CpuRate(CPU率)/内存/磁盘/网络/温度 多曲线（line）
 *  - 列表：NodeID/Name/LocalTime/CpuRate/AvailableMemory/FreeMemory/AvailableFreeSpace/UplinkSpeed/DownlinkSpeed/Temperature/.../CreateTime
 *  - 搜索：nodeId 下拉（加分项）+ 时间范围 dtStart/dtEnd
 *  - 后端 API：GET /Nodes/NodeData（GetPage）
 *
 * 种子数据为空时图表区显示空状态（el-empty），列表 0 行属正常：
 *   断言「页面渲染 + 无 >=400 错误 + 图表容器存在（canvas 或 空状态文案）」即可，不强行断言曲线数值。
 */
test('Nodes/NodeData 页面渲染（EChart 曲线 + 列表 + 搜索）', async ({ page }) => {
  const errs = collectErrors(page);
  await gotoLeafPage(page, 'Nodes/NodeData');
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
  for (const col of [
    '节点', '名称', '系统负载', 'CPU率', '可用内存', '空闲内存', '可用磁盘', '磁盘IOPS', '磁盘活动',
    '温度', '电量', '信号', '上行速度', '下行速度', '进程数', '连接数', '主动关闭', '被动关闭',
    '内网质量', '外网质量', '延迟', '偏移', '本地时间', '开机时间', '追踪', '创建者', '创建时间', '创建地址',
  ]) {
    expect(headerText, `表头应含列「${col}」`).toContain(col);
  }

  // 回归守护：历史上 NodeData 表头列名曾被错误加空格（如「系统 负载」「CPU 率」），
  // 后与 MVC/实体 DisplayName 对齐修正。这里针对源码确认确实曾被修复的列名追加「错误空格化写法不得出现」断言。
   
  for (const badCol of [
    '系统 负载', // 错误空格化写法（正确为「系统负载」）
    'CPU 率', // 错误空格化写法（正确为「CPU率」）
    '磁盘 IOPS', // 错误空格化写法（正确为「磁盘IOPS」）
    '磁盘 活动', // 错误空格化写法（正确为「磁盘活动」）
  ]) {
    expect(headerText, `表头不得含错误空格化列名「${badCol}」`).not.toContain(badCol);
  }

  // 通用守护：表头中任意两个非空格字符之间出现空格，即视为多余空格列名（回归防线）。
  // 跳过图例/单位等合法空格（如「系统负载(%)」），仅针对「表头单元格纯列名」做检查。
  const headerCells = await page
    .locator('main.cosmic-content .el-table')
    .first()
    .locator('.el-table__header-wrapper .cell')
    .allInnerTexts()
    .catch(() => [] as string[]);
  for (const cell of headerCells) {
    const trimmed = cell.trim();
    // 允许含括号单位（如「可用内存(M)」），仅当纯列名内部出现「非空格 非空格」才报错
    expect(/\S \S/.test(trimmed), `表头列名「${trimmed}」不得含多余空格`).toBe(false);
  }

  // 图表卡片标题
  await expect(page.locator('main.cosmic-content .lp-card-title')).toContainText('节点性能时序曲线');

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

  // 搜索栏：节点输入 + 日期范围
  await expect(page.locator('main.cosmic-content .nd-search-input').first()).toBeVisible();

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
    path: 'e2e/screenshots/Nodes/NodeData/list.png',
    fullPage: true,
  });

  expect(true).toBe(true);
});
