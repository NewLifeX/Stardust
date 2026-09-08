// src/main.ts
import { initApp } from '@newlifex/cube-vue/core/initApp';
import { registerLayout } from '@newlifex/cube-vue/core/composables/useLayout';
import router from '@newlifex/cube-vue/core/router';
import { resolvePageComponent } from '@newlifex/cube-vue/core/utils/menuRoutes';
// import CosmicLayout from './layouts/CosmicLayout/index.vue';
// import './layouts/CosmicLayout/variables.css';
import '@newlifex/cube-vue/core/global.css';
// 星尘设计系统：设计令牌 + Element 主题覆盖（独立 import，避免 PostCSS @import 相对路径解析异常）
import './design-system/tokens.css';
import './design-system/element-theme-override.css';
// 全局样式（仅 @tailwind 指令）
import './styles/global.css';
import StardustLayout from './layouts/StardustLayout/index.vue';
import './layouts/StardustLayout/variables.css';

// 全局激活暗色：element-theme-override.css 的 html.dark 块依赖此 class
document.documentElement.classList.add('dark');
// 调用链可视化隐藏页（根 Area / Monitors Area），catch-all 仅扫 **/index.vue，
// 且三级路由 /Trace/:id 不会被菜单注册，故手动 addRoute 兜底（参考 Doc/待迁移页面.md §3.6）。
// 注意：main.ts 位于 src/ 下，apps 目录与 src 同级，故相对路径为 ../apps/...
import TraceIndex from '../apps/stardust/src/views/Trace/index.vue';
import TraceDetail from '../apps/stardust/src/views/Trace/detail.vue';
import TraceGraph from '../apps/stardust/src/views/Trace/graph.vue';
import MonitorsTraceGraph from '../apps/stardust/src/views/Monitors/Trace/graph.vue';
// 星尘控制台 4 页空壳（待 A2 批次实现内容）
import StardustOverview from '../apps/stardust/src/views/Stardust/Overview.vue';
import StardustNodes from '../apps/stardust/src/views/Stardust/Nodes.vue';
// import StardustApps from '../apps/stardust/src/views/Stardust/Apps.vue';
import StardustSettings from '../apps/stardust/src/views/Stardust/Settings.vue';

// registerLayout(
//   {
//     id: 'cosmic',
//     label: '无界星图',
//     icon: '🌌',
//     description: '星图风格布局 · 星空背景 + 导航坞 + 曲速命令',
//     component: CosmicLayout,
//   },
//   false,
// );

// 星尘控制台布局：设为默认布局（第二参 true），这是应用的唯一入口布局。
registerLayout(
  {
    id: 'stardust',
    label: '星尘控制台',
    icon: '🌠',
    description: '星尘设计系统控制台布局',
    component: StardustLayout,
  },
  true,
);

initApp((app: any) => {
  // 首页默认重定向到星尘概览页，避免根路径空白
  router.addRoute({
    path: '/',
    name: 'HomeRedirect',
    redirect: '/stardust/overview',
  });

  // 隐藏菜单页 Detail 兜底：Deployment/AppPipelineRun 为 [Menu(40,false)]，
  // 框架菜单路由不会注册 /Deployment/AppPipelineRun/<id> 三级路由，
  // 因此在此处手动注册动态路由，将其指向同一目录下的 index.vue，
  // 由 index.vue 根据 route.params.id 渲染 detail.vue 组件。
  router.addRoute({
    path: '/Deployment/AppPipelineRun/:id',
    name: 'AppPipelineRunDetail',
    component: resolvePageComponent('/Deployment/AppPipelineRun'),
    meta: { auth: true, title: '流水线运行详情' },
  });

  // —— 调用链可视化隐藏页兜底（§3.6：三级路由不被菜单注册，graph.vue 不被 glob 命中）——
  // Trace/Index：#/Trace/<traceId>（调用链树 + layout=detail 追踪明细），复用 index.vue
  router.addRoute({
    path: '/Trace/:id',
    name: 'TraceIndex',
    component: TraceIndex,
    meta: { auth: true, title: '调用链' },
  });
  // Trace/Detail：#/Trace/Detail?id=<traceId>
  router.addRoute({
    path: '/Trace/Detail',
    name: 'TraceDetail',
    component: TraceDetail,
    meta: { auth: true, title: '追踪明细' },
  });
  // Trace/Graph：#/Trace/Graph?traceId=<id>（文件名为 graph.vue，catch-all 不命中，必须手动注册）
  router.addRoute({
    path: '/Trace/Graph',
    name: 'TraceGraph',
    component: TraceGraph,
    meta: { auth: true, title: '调用链关系图' },
  });
  // Monitors/Trace/Graph：#/Monitors/Trace/Graph?traceId=<id>（同上，graph.vue 不命中 glob）
  router.addRoute({
    path: '/Monitors/Trace/Graph',
    name: 'MonitorsTraceGraph',
    component: MonitorsTraceGraph,
    meta: { auth: true, title: '调用链关系图' },
  });

  // —— AutoPage 详情页兜底：cube-vue 的 ViewResolver 不会自动注册 #/Area/Controller/:id
  // 三级详情路由（与 AppPipelineRun 同理），此处手动 addRoute 指向各业务目录的 detail.vue。
  // 节点详情需额外列出该节点部署的应用（AppDeployNode?nodeId）。
  router.addRoute({
    path: '/Nodes/Node/:id',
    name: 'NodeDetail',
    component: () => import('../apps/stardust/src/views/Nodes/Node/detail.vue'),
    meta: { auth: true, title: '节点详情' },
  });
  router.addRoute({
    path: '/Nodes/Node/Detail/:id',
    component: () => import('../apps/stardust/src/views/Nodes/Node/detail.vue'),
    meta: { auth: true, title: '节点详情' },
  });
  router.addRoute({
    path: '/Registry/App/:id',
    name: 'AppDetail',
    component: () => import('../apps/stardust/src/views/Registry/App/detail.vue'),
    meta: { auth: true, title: '应用详情' },
  });
  router.addRoute({
    path: '/Registry/App/Detail/:id',
    component: () => import('../apps/stardust/src/views/Registry/App/detail.vue'),
    meta: { auth: true, title: '应用详情' },
  });

  // —— 星尘控制台 4 页：手动 addRoute 注册（参考 Trace 先例），避免与 AutoPage 菜单树冲突 ——
  // 路由统一用小写 /stardust/*，与 SideBar.vue 的 router-link 及 beforeEach 守卫前缀一致。
  // main.ts 位于 src/ 下，apps 与 src 同级，故相对路径为 ../apps/...
  router.addRoute({
    path: '/stardust/overview',
    name: 'StardustOverview',
    component: StardustOverview,
    meta: { auth: true, title: '概览' },
  });
  router.addRoute({
    path: '/stardust/nodes',
    name: 'StardustNodes',
    component: StardustNodes,
    meta: { auth: true, title: '节点管理' },
  });
  router.addRoute({
    path: '/stardust/apps',
    name: 'StardustApps',
    component: () => import('../apps/stardust/src/views/Stardust/Apps.vue'),
    meta: { auth: true, title: '应用管理' },
  });
  router.addRoute({
    path: '/stardust/settings',
    name: 'StardustSettings',
    component: StardustSettings,
    meta: { auth: true, title: '设置' },
  });

  // 布局默认为 stardust（注册时已 setAsCurrent=true），不再做页面级布局切换。
  // 用户如需切回 cosmic 仍可通过布局切换器手动选择。

  // 兼容非菜单隐藏页访问时菜单高区：若后续需要，可再扩展此处兜底规则。
  void app;
});
