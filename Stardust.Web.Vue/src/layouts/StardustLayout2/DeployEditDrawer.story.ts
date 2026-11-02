import DeployEditDrawer from './DeployEditDrawer.vue';
import type { DeployEditModel } from './deploy.logic.ts';

const initial: DeployEditModel = {
  name: 'IoC容器发布集',
  category: '中间件',
  enable: true,
  packageName: 'ioc.zip',
  autoPublish: true,
  multiVersion: false,
  repository: 'https://git.example.com/x/ioc.git',
  repoUserName: 'deployer',
  repoPassword: '',
  branch: 'main',
  projectPath: 'src/ioc',
  buildArgs: '-c Release',
  packageFilters: '*.dll;*.exe',
  remark: '',
};

export const stories = [
  // F06 编辑部署集：初值回填；密码框永远为空、占位「留空=不修改」
  {
    id: 'DeployEditDrawer/Default',
    component: DeployEditDrawer,
    props: { open: true, initial, busy: false },
  },

  // F06 保存中：提交按钮禁用写「保存中…」（AGENTS.md §11）
  {
    id: 'DeployEditDrawer/Busy',
    component: DeployEditDrawer,
    props: { open: true, initial, busy: true },
  },
];
