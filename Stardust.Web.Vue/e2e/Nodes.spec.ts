import { test, expect } from './fixtures';
import { gotoLeafPage, assertPageRendered, getRowCount, collectErrors } from './helpers';

// Nodes 区域叶子页：进入 → 页面渲染（列表或对象/配置）→ 数据接口无 >=400 错误
const PAGES: Array<[string, string]> = [
  ['Nodes/AgentDeploy', '代理部署'],
  ['Nodes/DomainProvider', '域名提供方'],
  ['Nodes/DotNetPackage', '.NET包'],
  ['Nodes/Node', '节点'],
  ['Nodes/NodeFramework', '节点框架'],
  ['Nodes/NodeLocation', '节点位置'],
  ['Nodes/NodeOnline', '节点在线'],
  ['Nodes/NodeRule', '节点规则'],
  ['Nodes/NodeStat', '节点统计'],
  ['Nodes/NodeVersion', '节点版本'],
  ['Nodes/ProductPackage', '产品包'],
  ['Nodes/ProductRelease', '产品发布'],
];

for (const [path, title] of PAGES) {
  test(`Nodes/${title} 页面渲染正常`, async ({ page }) => {
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
