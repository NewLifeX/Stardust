import McpServerForm from './McpServerForm.vue';
import type { McpServerRow } from './mesh.logic.ts';

const PROJECTS = [
  { id: 7, name: '默认项目' },
  { id: 12, name: '支付网关' },
];

const EDIT_SERVER: McpServerRow = {
  id: 1, name: 'stardust-core', url: 'http://localhost:6680/api/mcp', enable: false, tools: 18, projectId: 7, remark: '平台内置端点',
};

export const stories = [
  // 新建：空表单，启用默认勾选
  { id: 'McpServerForm/Create', component: McpServerForm, props: { open: true, mode: 'create', server: null, projects: PROJECTS, submitting: false } },
  // 编辑：回填名称/地址/启用/项目/备注
  { id: 'McpServerForm/Edit', component: McpServerForm, props: { open: true, mode: 'edit', server: EDIT_SERVER, projects: PROJECTS, submitting: false } },
  // 提交中：按钮旋转禁用
  { id: 'McpServerForm/Submitting', component: McpServerForm, props: { open: true, mode: 'create', server: null, projects: PROJECTS, submitting: true } },
];
