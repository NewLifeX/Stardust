import AppFormDrawer from './AppFormDrawer.vue';
import type { AppForm } from './apps.logic.ts';

const editing: AppForm = {
  name: 'star.web',
  displayName: '星尘主站',
  category: 'IoT 平台',
  enable: true,
  period: 60,
  secret: '',
  remark: '',
};

export const stories = [
  // F12 新建应用：全字段可填，Secret 留空=服务端自动生成
  {
    id: 'AppFormDrawer/Create',
    component: AppFormDrawer,
    props: { open: true, initial: null, title: '新建应用', sub: '创建后可到工作台配置发布与实例', busy: false },
  },

  // F12 编辑应用：Name 只读、Secret 不回显（留空=不修改）
  {
    id: 'AppFormDrawer/Edit',
    component: AppFormDrawer,
    props: { open: true, initial: editing, title: '编辑应用 · star.web', sub: '应用名不可修改；密钥留空=保持原值', busy: false },
  },

  // F12 保存中（AGENTS.md §11）
  {
    id: 'AppFormDrawer/Busy',
    component: AppFormDrawer,
    props: { open: true, initial: editing, title: '编辑应用 · star.web', sub: '', busy: true },
  },
];
