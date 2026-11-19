import GalaxyProjectFormDrawer from './GalaxyProjectFormDrawer.vue';
import { emptyProjectForm } from './galaxyProject.logic.ts';

export const stories = [
  // 新建态：空表单
  { id: 'GalaxyProjectFormDrawer/Create', component: GalaxyProjectFormDrawer, props: { open: true, editName: null, model: emptyProjectForm(), nameError: '', busy: false } },
  // 编辑态：回显既有项目
  { id: 'GalaxyProjectFormDrawer/Edit', component: GalaxyProjectFormDrawer, props: { open: true, editName: 'prod', model: { ...emptyProjectForm(), name: 'prod', isGlobal: true, tenantId: 2, sort: 4, whiteIPs: '10.0.0.1', remark: '生产项目' }, nameError: '', busy: false } },
  // 校验错误态：名称必填内联提示（旧壳逐字文案）
  { id: 'GalaxyProjectFormDrawer/NameError', component: GalaxyProjectFormDrawer, props: { open: true, editName: null, model: emptyProjectForm(), nameError: '请输入项目名称', busy: false } },
  // 保存中态：按钮禁用 + spinner + 输入禁用
  { id: 'GalaxyProjectFormDrawer/Busy', component: GalaxyProjectFormDrawer, props: { open: true, editName: null, model: { ...emptyProjectForm(), name: 'demo' }, nameError: '', busy: true } },
  // 关闭态（不渲染内容，占位回归）
  { id: 'GalaxyProjectFormDrawer/Closed', component: GalaxyProjectFormDrawer, props: { open: false, editName: null, model: emptyProjectForm() } },
];
