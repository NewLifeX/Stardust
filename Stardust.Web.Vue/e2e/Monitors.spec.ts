import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, getRowCount, collectErrors } from './helpers';

// Monitors 区域叶子页：进入 → 页面渲染（列表或对象/配置）→ 数据接口无 >=400 错误
const PAGES: Array<[string, string]> = [
  ['Monitors/AlarmGroup', '告警组'],
  ['Monitors/AlarmRecord', '告警记录'],
  ['Monitors/AppDayStat', '应用日统计'],
  ['Monitors/AppTracer', '应用链路'],
  ['Monitors/Trace', '链路'],
  ['Monitors/TraceRule', '链路规则'],
];

for (const [path, title] of PAGES) {
  test(`Monitors/${title} 页面渲染正常`, async ({ page }) => {
    const errs = collectErrors(page);
    await gotoLeafPage(page, path);
    const rendered = await assertPageRendered(page);
    console.log(`RENDER[${path}]=${rendered}`);
    if (rendered === 'list') {
      const rows = await getRowCount(page);
      console.log(`ROWS[${path}]=${rows}`);
    }
    const captured = await errs;
    if (captured.errors.length) {
      console.log('NET_ERR=' + JSON.stringify(captured.errors));
      throw new Error('存在接口/资源 >=400 错误: ' + JSON.stringify(captured.errors));
    }
    if (captured.consoleErrors.length) {
      console.log('CONSOLE_ERR=' + JSON.stringify(captured.consoleErrors));
    }
    expect(true).toBe(true);
  });
}
