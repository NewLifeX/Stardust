// StardustLayout2 新路由集中声明（路由不加前缀，沿用原型语义名）。
// main.ts 只需 `...consoleRoutes` 并入 externalRoutes；externalRoutes 优先级高于框架所有来源。
// 页面组件一律懒加载；M1 阶段共用占位页，M3 起逐页替换为真实页面。
import type { ConfigRoute } from '@newlifex/cube-vue/core/typings';

const Apps = () => import('../../../apps/stardust/src/views/Console/Apps.vue');
const App = () => import('../../../apps/stardust/src/views/Console/App.vue');
const Changes = () => import('../../../apps/stardust/src/views/Console/Changes.vue');
const Now = () => import('../../../apps/stardust/src/views/Console/Now.vue');
const Nodes = () => import('../../../apps/stardust/src/views/Console/Nodes.vue');
const Mesh = () => import('../../../apps/stardust/src/views/Console/Mesh.vue');
const Configs = () => import('../../../apps/stardust/src/views/Console/Configs.vue');
const Alarms = () => import('../../../apps/stardust/src/views/Console/Alarms.vue');

export const consoleRoutes: ConfigRoute[] = [
  { path: '/now', name: 'ConsoleNow', component: Now, meta: { auth: true, title: '此刻' } },
  { path: '/apps', name: 'ConsoleApps', component: Apps, meta: { auth: true, title: '应用' } },
  { path: '/app/:tab/:id', name: 'ConsoleApp', component: App, meta: { auth: true, title: '应用工作台' } },
  { path: '/nodes', name: 'ConsoleNodes', component: Nodes, meta: { auth: true, title: '节点' } },
  // 旧 /ship 整页随停点 20 取消，重定向到「变更」时间线（Ship.vue 文件保留备用，不再由路由引用）
  { path: '/ship', redirect: '/changes' },
  { path: '/changes', name: 'ConsoleChanges', component: Changes, meta: { auth: true, title: '变更' } },
  { path: '/mesh', name: 'ConsoleMesh', component: Mesh, meta: { auth: true, title: '治理' } },
  { path: '/configs', name: 'ConsoleConfigs', component: Configs, meta: { auth: true, title: '配置中心' } },
  { path: '/alarms', name: 'ConsoleAlarms', component: Alarms, meta: { auth: true, title: '告警中心' } },
];
