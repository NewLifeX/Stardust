import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, getRowCount, collectErrors } from './helpers';

// Admin 区域叶子页：进入 → 页面渲染（列表或对象/配置）→ 数据接口无 >=400 错误
// Admin/Cube、Admin/Db、Admin/Parameter、Admin/Index/Main 为单对象/配置页（object），其余为列表页。
const PAGES: Array<[string, string]> = [
  ['Admin/Cube', '魔方设置'],
  ['Admin/Db', '数据库'],
  ['Admin/Department', '部门'],
  ['Admin/Index/Main', '后台首页'],
  ['Admin/Log', '日志'],
  ['Admin/Lov', '数据字典'],
  ['Admin/Menu', '菜单'],
  ['Admin/Parameter', '参数'],
  ['Admin/Role', '角色'],
  ['Admin/User', '用户'],
];

for (const [path, title] of PAGES) {
  test(`Admin/${title} 页面渲染正常`, async ({ page }) => {
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
