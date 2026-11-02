import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, getRowCount, collectErrors } from './helpers';

// Cube 区域叶子页：进入 → 页面渲染（列表或对象/配置）→ 数据接口无 >=400 错误
// 其中 Admin/Cube 类配置页在 Cube 区域可能表现为单对象页，其余为列表页。
const PAGES: Array<[string, string]> = [
  ['Cube/App', '应用'],
  ['Cube/AppModule', '应用模块'],
  ['Cube/Area', '区域'],
  ['Cube/Attachment', '附件'],
  ['Cube/CronJob', '定时任务'],
  ['Cube/OrderManager', '订单管理'],
  ['Cube/PrincipalAgent', '主体代理'],
];

for (const [path, title] of PAGES) {
  test(`Cube/${title} 页面渲染正常`, async ({ page }) => {
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
