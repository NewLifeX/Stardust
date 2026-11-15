// ShipPanel 容器链路测试（task-91 节点回显）：双入口都必须走同一条链——
//   行内「编辑」/ 日志抽屉空态「配置日志目录」→ askEditNode → fetchDeployNodeDetail（GET /Deployment/AppDeployNode/{id}）
//   → toDeployNodeForm → nodeDrawer.initial → NodeFormDrawer 物理节点选择器。
// 212 旧构建真身（Cube 投影只发映射列 nodeName、缺关联键 nodeId）→ 表单回填 nodeId=0 → 必须给可见警示 + 选择器无选中值；
// 当前栈真身（nodeId=1）→ 选择器选中且无警示（修复后形态钉死）。
// 请求层整体替换（ShipPanel 子树所有 cube import 都经 @newlifex/cube-vue/core/utils/request），按 URL 分发真实响应体。
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import ShipPanel from './ShipPanel.vue';
import detail212 from '@/layouts/StardustLayout2/__fixtures__/appdeploynode-detail.212.json';
import detailCurrent from '@/layouts/StardustLayout2/__fixtures__/appdeploynode-detail.current.json';

type Cfg = { url?: string; method?: string };

// vi.mock 工厂被提升，路由器经 hoisted 持有器中转（测试体里 set 换场景）
const mockState = vi.hoisted(() => {
  let router: (cfg: { url?: string; method?: string }) => unknown = () => ({ code: 0, data: [] });
  return { set: (r: (cfg: { url?: string; method?: string }) => unknown) => { router = r; }, get: () => router };
});

vi.mock('@newlifex/cube-vue/core/utils/request', () => ({
  request: (cfg: { url?: string; method?: string }) => Promise.resolve(mockState.get()(cfg)),
}));

/** 首屏 load 与编辑链要打的所有端点：deploy 行 / 节点列表 / 节点选项 / 详情（按场景换体）；其余端点回空数组 */
function makeRouter(detailBody: unknown) {
  const deployRow = { id: 25, appId: 7, name: 'mcpapi-app', version: '1.0.0', enable: true, autoPublish: false, multiVersion: false, logDirectory: '', nodes: 1 };
  // 节点列表行：iP 形（212 旧构建序列化），logDirectory 空（未配置，日志抽屉走配置引导）
  const nodeRow = { id: 76, deployId: 25, nodeId: 4, nodeName: '212-cwiocap01', iP: '10.188.32.212', enable: true, version: '', mode: 10, delay: 0, processId: 0, startTime: '', compile: '', lastActive: '', logDirectory: '' };
  const nodeOptions = [
    { id: 1, name: 'mcpapi-node', ip: '127.0.0.1' },
    { id: 2, name: 'node-2', ip: '10.0.0.2' },
    { id: 3, name: 'DGCWIAX10', ip: '10.0.0.3' },
  ];
  return (cfg: Cfg): unknown => {
    const url = cfg.url ?? '';
    if (cfg.method === 'GET' && /^\/Deployment\/AppDeployNode\/\d+$/.test(url)) return { code: 0, data: detailBody };
    if (cfg.method === 'GET' && url === '/Deployment/AppDeployNode') return { code: 0, data: [nodeRow] };
    if (cfg.method === 'GET' && url === '/Deployment/AppDeploy') return { code: 0, data: [deployRow] };
    if (cfg.method === 'GET' && url === '/Nodes/Node') return { code: 0, data: nodeOptions };
    return { code: 0, data: [] };
  };
}

beforeEach(() => {
  if (!('ResizeObserver' in globalThis)) {
    Object.defineProperty(globalThis, 'ResizeObserver', {
      configurable: true,
      writable: true,
      value: class {
        observe(): void {}
        unobserve(): void {}
        disconnect(): void {}
      },
    });
  }
  // 默认 212 形（bug 场景）；绿场景用例自行换成当前栈形
  mockState.set(makeRouter(detail212.data));
});

enableAutoUnmount(afterEach);
afterEach(() => {
  document.body.innerHTML = '';
});

function mountPanel() {
  return mount(ShipPanel, {
    props: { appId: 7, appName: 'mcpapi-app', section: 'nodes' as const },
    attachTo: document.body,
  });
}

describe('ShipPanel 节点回显容器链路（task-91）', () => {
  it('入口1 行内编辑（212 形详情缺 nodeId）：编辑抽屉警示可见 + 选择器无选中值', async () => {
    const w = mountPanel();
    await flushPromises(); // 首屏 load：deploy/节点列表/节点选项
    await w.find('[data-spec="ship-node-row"] [data-spec="ship-node-edit"]').trigger('click');
    await flushPromises(); // fetchDeployNodeDetail + loadNodeOptions
    const warn = w.find('[data-spec="node-echo-warning"]');
    expect(warn.exists()).toBe(true);
    expect(warn.text()).toContain('未能回显');
    const sel = w.find('[data-spec="node-form-node"]');
    expect(sel.exists()).toBe(true);
    expect((sel.element as HTMLSelectElement).value).toBe('');
  });

  it('入口2 日志抽屉空态「配置日志目录」（212 形）：复用同一编辑抽屉，警示可见 + 选择器无选中值 + 日志抽屉保持打开', async () => {
    const w = mountPanel();
    await flushPromises();
    await w.find('[data-spec="ship-node-row"] [data-spec="ship-node-logs"]').trigger('click');
    // 节点与部署集日志目录双空 → task-90 配置引导空态
    expect(w.find('[data-spec="deploy-log-config-empty"]').exists()).toBe(true);
    await w.find('[data-spec="deploy-log-configure"]').trigger('click');
    await flushPromises();
    const warn = w.find('[data-spec="node-echo-warning"]');
    expect(warn.exists()).toBe(true);
    expect(warn.text()).toContain('未能回显');
    expect((w.find('[data-spec="node-form-node"]').element as HTMLSelectElement).value).toBe('');
    // 双抽屉叠放（NodeFormDrawer :z-index 41 压 DeployLogDrawer 40）：日志抽屉未关
    expect(w.find('[data-spec="deploy-log-drawer"]').exists()).toBe(true);
  });

  it('当前栈形（nodeId=1 在可选项）：编辑抽屉正常选中且无警示', async () => {
    mockState.set(makeRouter(detailCurrent.data));
    const w = mountPanel();
    await flushPromises();
    await w.find('[data-spec="ship-node-row"] [data-spec="ship-node-edit"]').trigger('click');
    await flushPromises();
    expect(w.find('[data-spec="node-echo-warning"]').exists()).toBe(false);
    const sel = w.find('[data-spec="node-form-node"]');
    expect((sel.element as HTMLSelectElement).value).toBe('1');
  });
});
