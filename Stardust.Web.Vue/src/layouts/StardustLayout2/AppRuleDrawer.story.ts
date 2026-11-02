import AppRuleDrawer from './AppRuleDrawer.vue';
import type { AppRuleForm } from './configCenter.logic.ts';

const editing: AppRuleForm = {
  rule: 'IP=10.*,172.*;LocalIP=192.*',
  result: 'Scope=dev',
  priority: 5,
  enable: true,
  remark: '本机与内网段走 dev 作用域',
};

export const stories = [
  // F11 新增规则：空表单
  {
    id: 'AppRuleDrawer/Create',
    component: AppRuleDrawer,
    props: { open: true, initial: null, title: '新增应用规则', sub: '命中后按 Result 给应用划分作用域（如 Scope=dev）', busy: false },
  },

  // F11 编辑规则：初值回填
  {
    id: 'AppRuleDrawer/Edit',
    component: AppRuleDrawer,
    props: { open: true, initial: editing, title: '编辑应用规则', sub: '规则分号分段，逗号间隔多项匹配值', busy: false },
  },

  // F11 保存中（AGENTS.md §11）
  {
    id: 'AppRuleDrawer/Busy',
    component: AppRuleDrawer,
    props: { open: true, initial: editing, title: '编辑应用规则', sub: '', busy: true },
  },
];
