import DeployVersionBar from './DeployVersionBar.vue';

const BASE = {
  version: 'v1.4.2',
  enable: true,
  autoPublish: false,
  multiVersion: false,
  stats: { total: 4, same: 4, diff: 0 },
  nodesFailed: false,
  refreshing: false,
  busy: false,
  syncing: false,
};

export const stories = [
  // 测试状态：全部节点一致，刷新按钮可点，文案「刷新」
  { id: 'DeployVersionBar/Default', component: DeployVersionBar, props: { ...BASE } },

  // 测试状态：刷新中，按钮禁用、aria-busy=true、图标旋转、文案「刷新中…」（AGENTS.md §11）
  { id: 'DeployVersionBar/Refreshing', component: DeployVersionBar, props: { ...BASE, refreshing: true } },

  // 测试状态：有节点操作在进行，刷新按钮禁用，但不是刷新中
  { id: 'DeployVersionBar/Busy', component: DeployVersionBar, props: { ...BASE, busy: true } },

  // 测试状态：同步应用进行中（F06）：同步按钮禁用、旋转、「同步中…」，编辑/刷新按钮同样禁用
  { id: 'DeployVersionBar/Syncing', component: DeployVersionBar, props: { ...BASE, syncing: true } },

  // 测试状态：节点加载失败，统计处写「节点加载失败」
  {
    id: 'DeployVersionBar/NodesFailed',
    component: DeployVersionBar,
    props: { ...BASE, nodesFailed: true, stats: { total: 0, same: 0, diff: 0 } },
  },

  // 测试状态：没有启用的节点
  {
    id: 'DeployVersionBar/NoNodes',
    component: DeployVersionBar,
    props: { ...BASE, stats: { total: 0, same: 0, diff: 0 } },
  },

  // 测试状态：有不一致的节点，数字用警告色
  { id: 'DeployVersionBar/Diff', component: DeployVersionBar, props: { ...BASE, stats: { total: 4, same: 3, diff: 1 } } },

  // 测试状态：开了多版本，不一致属于允许，数字不用警告色，并显示「多版本」标签
  {
    id: 'DeployVersionBar/DiffMulti',
    component: DeployVersionBar,
    props: { ...BASE, multiVersion: true, stats: { total: 4, same: 3, diff: 1 } },
  },
];
