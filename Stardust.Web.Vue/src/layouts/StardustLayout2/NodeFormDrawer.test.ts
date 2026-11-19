// NodeFormDrawer 组件测试（vitest + jsdom 真实挂载）：task-91 节点回显警示。
// 背景：212 旧构建 Cube 投影只发映射列 nodeName、不发关联键 nodeId（上游 NewLife.Cube 5110d102 修复的缺陷家族），
// 编辑抽屉打开后选择器既没有选中值也无声息——必须给用户可见提示；绑定节点不在可选项（已停用等）同理。
// 抽屉壳（Drawer/Drawer.vue → ElDrawer）由 CT 覆盖，这里用透传 slot 的桩替换，聚焦表单行为。
import { mount } from '@vue/test-utils';
import { defineComponent } from 'vue';
import { describe, expect, it } from 'vitest';
import NodeFormDrawer from './NodeFormDrawer.vue';
import type { DeployNodeForm, NodeOption } from './deploy.logic.ts';

// 透传默认/动作插槽的 Drawer 桩（保持 actions 里的保存/取消按钮可交互）
const DrawerStub = defineComponent({
  name: 'Drawer',
  props: {
    open: { type: Boolean, default: false },
    title: { type: String, default: '' },
    sub: { type: String, default: '' },
    zIndex: { type: Number, default: 40 },
  },
  emits: ['close'],
  template: '<div data-spec="drawer-stub"><slot /><slot name="actions" /></div>',
});

// GET /Nodes/Node?enable=true 的映射行（与 fetchNodeOptions 输出同形）
const OPTIONS: NodeOption[] = [
  { id: 1, name: 'mcpapi-node', ip: '127.0.0.1' },
  { id: 2, name: 'mcpapi-crossnode', ip: '10.0.0.2' },
  { id: 3, name: 'DGCWIAX10', ip: '10.0.0.3' },
];

function deployInitial(nodeId: number): DeployNodeForm {
  return {
    nodeId, enable: true, port: 0, fileName: '', arguments: '', workingDirectory: '', userName: '',
    environments: '', maxMemory: 0, delay: 0, healthCheck: '', logDirectory: '', logFilePattern: '*.log', remark: '',
  };
}

function mountDrawer(initial: DeployNodeForm | null, opts: { nodeOptions?: NodeOption[]; nodesLoading?: boolean } = {}) {
  return mount(NodeFormDrawer, {
    props: {
      open: true,
      kind: 'deploy',
      initial,
      title: '编辑部署节点',
      sub: '#76',
      nodeOptions: opts.nodeOptions ?? OPTIONS,
      nodesLoading: opts.nodesLoading ?? false,
      busy: false,
    },
    global: { stubs: { Drawer: DrawerStub } },
  });
}

describe('NodeFormDrawer 节点回显警示（task-91）', () => {
  it('212 形态（详情缺 nodeId → 回填 0）打开编辑抽屉：警示可见 + 选择器无选中值', () => {
    const w = mountDrawer(deployInitial(0));
    const warn = w.find('[data-spec="node-echo-warning"]');
    expect(warn.exists()).toBe(true);
    expect(warn.text()).toContain('未能回显');
    const sel = w.find('[data-spec="node-form-node"]');
    expect(sel.exists()).toBe(true);
    expect((sel.element as HTMLSelectElement).value).toBe('');
  });

  it('正常回显（nodeId 在可选项中）：无警示 + 选择器选中该节点', () => {
    const w = mountDrawer(deployInitial(2));
    expect(w.find('[data-spec="node-echo-warning"]').exists()).toBe(false);
    const sel = w.find('[data-spec="node-form-node"]');
    expect((sel.element as HTMLSelectElement).value).toBe('2');
  });

  it('绑定节点不在可选项（已停用等）：unavailable 警示带编号', () => {
    const w = mountDrawer(deployInitial(4));
    const warn = w.find('[data-spec="node-echo-warning"]');
    expect(warn.exists()).toBe(true);
    expect(warn.text()).toContain('#4');
    expect(warn.text()).toContain('不在可选项');
  });

  it('用户重选节点后警示消失（回显健康度随 form.nodeId 反应）', async () => {
    const w = mountDrawer(deployInitial(0));
    expect(w.find('[data-spec="node-echo-warning"]').exists()).toBe(true);
    await w.find('[data-spec="node-form-node"]').setValue('3');
    expect(w.find('[data-spec="node-echo-warning"]').exists()).toBe(false);
    expect((w.find('[data-spec="node-form-node"]').element as HTMLSelectElement).value).toBe('3');
  });

  it('新增（initial=null）：不警示', () => {
    const w = mountDrawer(null);
    expect(w.find('[data-spec="node-echo-warning"]').exists()).toBe(false);
  });

  it('节点选项加载中：只显示加载态，不警示不抢跑', () => {
    const w = mountDrawer(deployInitial(0), { nodesLoading: true });
    expect(w.find('[data-spec="node-form-loading"]').exists()).toBe(true);
    expect(w.find('[data-spec="node-form-node"]').exists()).toBe(false);
    expect(w.find('[data-spec="node-echo-warning"]').exists()).toBe(false);
  });

  it('选项为空且不在加载（拉取失败已有 toast 兜底）：不重复警示', () => {
    const w = mountDrawer(deployInitial(4), { nodeOptions: [] });
    expect(w.find('[data-spec="node-form-node"]').exists()).toBe(true);
    expect(w.find('[data-spec="node-echo-warning"]').exists()).toBe(false);
  });
});
