import ConfigItemDrawer from './ConfigItemDrawer.vue';
import type { ConfigItemForm } from './appConfig.logic.ts';

const published: ConfigItemForm & { published: boolean } = {
  key: 'Star.Server',
  scope: '',
  value: 'tcp://127.0.0.1:6680',
  newValue: 'tcp://10.0.0.8:6680',
  enable: true,
  remark: '',
  published: true,
};

const unpublished: ConfigItemForm & { published: boolean } = {
  key: 'LocalIP',
  scope: 'dev',
  value: '192.168.*',
  newValue: '',
  enable: true,
  remark: '本机 IP 匹配',
  published: false,
};

export const stories = [
  // F10 编辑已发布行：Key/Scope/Value 只读 + 草稿提示 + NewValue 可改
  {
    id: 'ConfigItemDrawer/Published',
    component: ConfigItemDrawer,
    props: {
      open: true,
      initial: published,
      title: '编辑配置项 · Star.Server',
      sub: '应用配置：IoC容器发布集',
      busy: false,
    },
  },

  // F10 编辑未发布行：Key/Scope/Value 直接改
  {
    id: 'ConfigItemDrawer/Unpublished',
    component: ConfigItemDrawer,
    props: {
      open: true,
      initial: unpublished,
      title: '编辑配置项 · LocalIP',
      sub: '应用配置：IoC容器发布集',
      busy: false,
    },
  },

  // F10 新增：全字段可填
  {
    id: 'ConfigItemDrawer/Create',
    component: ConfigItemDrawer,
    props: {
      open: true,
      initial: null,
      title: '新增配置项',
      sub: '应用配置：IoC容器发布集',
      busy: false,
    },
  },

  // F10 保存中：提交按钮禁用（AGENTS.md §11）
  {
    id: 'ConfigItemDrawer/Busy',
    component: ConfigItemDrawer,
    props: {
      open: true,
      initial: published,
      title: '编辑配置项 · Star.Server',
      sub: '应用配置：IoC容器发布集',
      busy: true,
    },
  },
];
