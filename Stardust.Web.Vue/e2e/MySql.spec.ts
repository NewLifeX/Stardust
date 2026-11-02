import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, getRowCount, collectErrors } from './helpers';

// MySql 区域叶子页：进入 → 页面渲染（列表）→ 数据接口无 >=400 错误
// MySqlData / MySqlNode 均为列表控制器（EntityController）。
const PAGES: Array<[string, string]> = [
  ['MySql/MySqlData', 'MySQL数据'],
  ['MySql/MySqlNode', 'MySQL节点'],
];

for (const [path, title] of PAGES) {
  test(`MySql/${title} 页面渲染正常`, async ({ page }) => {
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
