import TopBar from './TopBar.vue';

// story = 一个渲染变体。只描述「传什么 props」，不手写任何 demo 页。
//
// TopBar 是纯 UI 组件：项目列表、当前项目、主题、导航高亮、计数全部由 props 传入，
// 交互只向上抛事件（go / project / toggle-theme / search / logout）。

const projects = [
  { name: 'stardust-prod', region: '华东', env: 'prod' },
  { name: 'stardust-staging', region: '华北', env: 'staging' },
];

export const stories = [
  // 测试状态：暗色主题 + 选中「此刻」，无计数徽标（最常见的首屏形态）
  {
    id: 'TopBar/DarkActiveNow',
    component: TopBar,
    props: {
      projects,
      projectName: 'stardust-prod',
      theme: 'dark',
      initial: '笑',
      active: 'now',
    },
  },

  // 测试状态：暗色主题 + 选中「应用」，验证高亮随 active 移动
  {
    id: 'TopBar/DarkActiveApps',
    component: TopBar,
    props: {
      projects,
      projectName: 'stardust-prod',
      theme: 'dark',
      initial: '笑',
      active: 'apps',
    },
  },

  // 测试状态：亮色主题 + 选中「节点」，验证主题图标切为 Moon、整体配色随令牌变化
  {
    id: 'TopBar/LightActiveNodes',
    component: TopBar,
    props: {
      projects,
      projectName: 'stardust-prod',
      theme: 'light',
      initial: '笑',
      active: 'nodes',
    },
  },

  // 测试状态：选中「此刻」且 nowCount=5，导航段显示红色计数徽标
  {
    id: 'TopBar/DarkWithNowCount',
    component: TopBar,
    props: {
      projects,
      projectName: 'stardust-prod',
      theme: 'dark',
      initial: '笑',
      active: 'now',
      nowCount: 5,
    },
  },

  // 测试状态：仅一个项目，验证项目下拉的极简形态
  {
    id: 'TopBar/SingleProject',
    component: TopBar,
    props: {
      projects: [projects[0]],
      projectName: 'stardust-prod',
      theme: 'dark',
      initial: '笑',
      active: 'now',
    },
  },
];
