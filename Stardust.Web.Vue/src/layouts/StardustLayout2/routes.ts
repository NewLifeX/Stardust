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
const TraceDetail = () => import('../../../apps/stardust/src/views/Console/TraceDetail.vue');

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
  // 全局调用链页（参数可含逗号多值；?id= 旧 MVC 链接也落到这页，页面侧兼容）
  { path: '/trace/:traceId?', name: 'ConsoleTrace', component: TraceDetail, meta: { auth: true, title: '链路详情' } },
];

/**
 * 旧调用链隐藏页路由的让位声明（main.ts 里随 externalRoutes 并入）。
 *
 * 旧页 /Trace/<id>（apps/stardust/src/views/Trace/index.vue）已退役：/trace 统一由契约页
 * ConsoleTrace（Console/TraceDetail.vue）承载（单值瀑布 + 逗号多值并列 + 诊断框）。
 *
 * 关键：必须**按命名路由跳转**（方案 A），不能 redirect 回路径 `/trace/<id>`——
 * vue-router 路径匹配大小写不敏感，必选段 `/Trace/:id` 打分压过可选段 `/trace/:traceId?`，
 * 路径重定向会再次命中本记录 → 无限递归 RangeError 全部 /trace 深链白屏。
 * 命名跳转直接解析到 ConsoleTrace 记录，不经过 path 打分，一跳即达。
 */
export const legacyTraceRedirect: ConfigRoute = {
  path: '/Trace/:id',
  name: 'TraceIndex',
  redirect: (to) => ({
    name: 'ConsoleTrace',
    params: { traceId: to.params.id },
    query: to.query,
  }),
};
