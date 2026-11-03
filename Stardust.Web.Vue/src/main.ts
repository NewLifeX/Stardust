// src/main.ts
import { initApp } from '@newlifex/cube-vue/core/initApp';
import { registerLayout } from '@newlifex/cube-vue/core/composables/useLayout';
import type { ConfigRoute } from '@newlifex/cube-vue/core/typings';
import { resolvePageComponent } from '@newlifex/cube-vue/core/utils/menuRoutes';
// import CosmicLayout from './layouts/CosmicLayout/index.vue';
// import './layouts/CosmicLayout/variables.css';
import '@newlifex/cube-vue/core/global.css';
// 星尘设计系统：设计令牌 + Element 主题覆盖（独立 import，避免 PostCSS @import 相对路径解析异常）
import './design-system/tokens.css';
// 设计交接包权威令牌层（星尘紫 + 深空底）：晚于 tokens.css 载入即整体覆盖 --sd-*，
// 因 --el-* 写成 var(--sd-*) 惰性求值 → 一处改色全站生效；摘掉本两行即可回滚。
import './design-system/tokens.stardust.css';
import './design-system/element-theme-override.css';
// A6 列表页统一层：表格行 hover / 留白节奏 / KPI 条 / 工具条
import './design-system/list-page.css';
// 全局样式（仅 @tailwind 指令）
import './styles/global.css';
import StardustLayout from './layouts/StardustLayout/index.vue';
import './layouts/StardustLayout/variables.css';
import StardustLayout2 from './layouts/StardustLayout2/index.vue';
import { consoleRoutes, legacyTraceRedirect } from './layouts/StardustLayout2/routes';
import { initTheme } from './design-system/theme';

// 主题初始化：默认暗色，localStorage['stardust-theme'] 可切浅色（设计稿 [data-theme='light']）
// Element Plus 暗色 --el-* 定义在 html.dark 下，故切换时 data-theme 与 .dark 必须同步。
initTheme();
// 调用链可视化隐藏页（根 Area / Monitors Area），catch-all 仅扫 **/index.vue，
// 且三级路由 /Trace/:id 不会被菜单注册，故手动声明兜底（参考 Doc/待迁移页面.md §3.6）。
// 注意：main.ts 位于 src/ 下，apps 目录与 src 同级，故相对路径为 ../apps/...
// 旧 /Trace/<id> 整页（Trace/index.vue）已被契约页
// /trace/:traceId?（Console/TraceDetail.vue）取代——/Trace/:id 重定向到契约页，
// 旧深链（Monitors/Trace/index.vue 的 push、书签）全部落到新页；/Trace/Detail、/Trace/Graph 保留。
import TraceDetail from '../apps/stardust/src/views/Trace/detail.vue';
import TraceGraph from '../apps/stardust/src/views/Trace/graph.vue';
import MonitorsTraceGraph from '../apps/stardust/src/views/Monitors/Trace/graph.vue';
// 星尘控制台 4 页空壳（待 A2 批次实现内容）
import StardustOverview from '../apps/stardust/src/views/Stardust/Overview.vue';
import StardustNodes from '../apps/stardust/src/views/Stardust/Nodes.vue';
// import StardustApps from '../apps/stardust/src/views/Stardust/Apps.vue';
import StardustSettings from '../apps/stardust/src/views/Stardust/Settings.vue';
import StardustLogin from '../apps/stardust/src/views/Stardust/Login.vue';
import StardustAppDetail from '../apps/stardust/src/views/Stardust/AppDetail.vue';

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

// 星尘控制台布局：旧 stardust 仅注册（可在布局切换器手动选回），StardustLayout2 设为当前布局（第二参 true）。
registerLayout(
  {
    id: 'stardust',
    label: '星尘控制台',
    icon: '✦',
    description: '星尘设计系统控制台布局',
    component: StardustLayout,
  },
  false,
);
registerLayout(
  {
    id: 'stardust2',
    label: '星尘控制台 v2',
    icon: '✦',
    description: '设计交接包 v2：此刻 / 应用 / 节点 / 发布 / 治理',
    component: StardustLayout2,
  },
  true,
);

/**
 * 外部（业务）路由声明：**优先级高于框架所有来源**（静态表 / 微应用 / 后端菜单）。
 *
 * 框架 `core/routes/index.ts` 里的 `'/'`（home）等静态路由在 createRouter 阶段就已注册，
 * 而 vue-router 对同形态 path 是「先注册者胜」，因此业务侧裸调 `router.addRoute` 无法覆盖，
 * 首页重定向会成为死路由。这里改走 `initApp` 的 `externalRoutes`：
 * - 框架侧注册同形态 path / 同 name 的路由时自动让位；
 * - 本列表在 `app.use(router)` 之前落盘，先于首次导航解析。
 */
const externalRoutes: ConfigRoute[] = [
  // StardustLayout2 新路由（/now /apps /app/:name/:tab? /nodes /ship /mesh），声明见 layouts/StardustLayout2/routes.ts
  ...consoleRoutes,

  // 应用详情（A8）：独立页面形态（设计稿 page-app-detail.jsx：面包屑 + 7 Tabs）
  { path: '/stardust/apps/:id', name: 'StardustAppDetail', component: StardustAppDetail, meta: { auth: true, title: '应用详情' } },

  // 登录页（A4）：外部声明接管框架静态路由 /login（meta 按框架安全提示显式给出）
  { path: '/login', name: 'StardustLogin', component: StardustLogin, meta: { auth: false, layout: false, title: '登录' } },

  // 首页默认重定向到星尘概览页，避免根路径空白（覆盖框架的 '/'）
  {
    path: '/',
    name: 'HomeRedirect',
    redirect: '/now',
  },

  // 隐藏菜单页 Detail 兜底：Deployment/AppPipelineRun 为 [Menu(40,false)]，
  // 框架菜单路由不会注册 /Deployment/AppPipelineRun/<id> 三级路由，
  // 因此在此处声明动态路由，将其指向同一目录下的 index.vue，
  // 由 index.vue 根据 route.params.id 渲染 detail.vue 组件。
  {
    path: '/Deployment/AppPipelineRun/:id',
    name: 'AppPipelineRunDetail',
    component: resolvePageComponent('/Deployment/AppPipelineRun'),
    meta: { auth: true, title: '流水线运行详情' },
  },

  // —— 调用链可视化隐藏页兜底（§3.6：三级路由不被菜单注册，graph.vue 不被 glob 命中）——
  // /Trace/<traceId> 旧页退役 → 命名路由跳转到契约页 /trace/:traceId（避免 path 重定向递归）
  //（Console/TraceDetail.vue，单值瀑布 + 逗号多值并列 + 诊断框已覆盖旧页能力）。
  // 必须 redirect 到 name='ConsoleTrace'（方案 A）：路径重定向会被本记录（必选段打分压过可选段）再次命中 → 无限递归白屏。
  legacyTraceRedirect,
  // Trace/Detail：/Trace/Detail?id=<traceId>
  {
    path: '/Trace/Detail',
    name: 'TraceDetail',
    component: TraceDetail,
    meta: { auth: true, title: '追踪明细' },
  },
  // Trace/Graph：/Trace/Graph?traceId=<id>（文件名为 graph.vue，catch-all 不命中，必须显式声明）
  {
    path: '/Trace/Graph',
    name: 'TraceGraph',
    component: TraceGraph,
    meta: { auth: true, title: '调用链关系图' },
  },
  // Monitors/Trace/Graph：/Monitors/Trace/Graph?traceId=<id>（同上，graph.vue 不命中 glob）
  {
    path: '/Monitors/Trace/Graph',
    name: 'MonitorsTraceGraph',
    component: MonitorsTraceGraph,
    meta: { auth: true, title: '调用链关系图' },
  },

  // —— AutoPage 详情页兜底：cube-vue 的 ViewResolver 不会自动注册 /Area/Controller/:id
  // 三级详情路由（与 AppPipelineRun 同理），此处显式声明指向各业务目录的 detail.vue。
  // 节点详情需额外列出该节点部署的应用（AppDeployNode?nodeId）。
  {
    path: '/Nodes/Node/:id',
    name: 'NodeDetail',
    component: () => import('../apps/stardust/src/views/Nodes/Node/detail.vue'),
    meta: { auth: true, title: '节点详情' },
  },
  {
    path: '/Nodes/Node/Detail/:id',
    name: 'NodeDetailById',
    component: () => import('../apps/stardust/src/views/Nodes/Node/detail.vue'),
    meta: { auth: true, title: '节点详情' },
  },
  {
    path: '/Registry/App/:id',
    name: 'AppDetail',
    component: () => import('../apps/stardust/src/views/Registry/App/detail.vue'),
    meta: { auth: true, title: '应用详情' },
  },
  {
    path: '/Registry/App/Detail/:id',
    name: 'AppDetailById',
    component: () => import('../apps/stardust/src/views/Registry/App/detail.vue'),
    meta: { auth: true, title: '应用详情' },
  },

  // —— 网关中心：Vue 无自定义视图，显式声明走 AutoPage 兜底（方案 §16 信息架构：集群 / 路由 / 节点）——
  {
    path: '/Gateway/GatewayCluster',
    name: 'GatewayCluster',
    component: resolvePageComponent('/Gateway/GatewayCluster'),
    meta: { auth: true, title: '网关集群' },
  },
  {
    path: '/Gateway/GatewayRoute',
    name: 'GatewayRoute',
    component: resolvePageComponent('/Gateway/GatewayRoute'),
    meta: { auth: true, title: '网关路由' },
  },
  {
    path: '/Gateway/GatewayNode',
    name: 'GatewayNode',
    component: resolvePageComponent('/Gateway/GatewayNode'),
    meta: { auth: true, title: '网关节点' },
  },

  // —— 星尘控制台 4 页：显式声明（参考 Trace 先例），避免与 AutoPage 菜单树冲突 ——
  // 路由统一用小写 /stardust/*，与 SideBar.vue 的 router-link 及 beforeEach 守卫前缀一致。
  // main.ts 位于 src/ 下，apps 与 src 同级，故相对路径为 ../apps/...,
  {
    path: '/stardust/overview',
    name: 'StardustOverview',
    component: () => import('../apps/stardust/src/views/Stardust/Workspace.vue'),
    meta: { auth: true, title: '概览' },
  },
  {
    path: '/stardust/nodes',
    name: 'StardustNodes',
    component: StardustNodes,
    meta: { auth: true, title: '节点管理' },
  },
  {
    path: '/stardust/nodes/:id',
    name: 'StardustNodeDetail',
    component: () => import('../apps/stardust/src/views/Stardust/NodeDetail.vue'),
    meta: { auth: true, title: '节点详情' },
  },
  {
    path: '/stardust/apps',
    name: 'StardustApps',
    component: () => import('../apps/stardust/src/views/Stardust/Apps.vue'),
    meta: { auth: true, title: '应用管理' },
  },
  {
    path: '/stardust/observability',
    name: 'StardustObservability',
    component: () => import('../apps/stardust/src/views/Stardust/Observability.vue'),
    meta: { auth: true, title: '可观测中心' },
  },
  {
    path: '/stardust/settings',
    name: 'StardustSettings',
    component: StardustSettings,
    meta: { auth: true, title: '设置' },
  },

  // —— 配置中心（§11）：列表 + 编辑器 Drawer + Diff 视图 ——
  {
    path: '/stardust/config',
    name: 'StardustConfig',
    component: () => import('../apps/stardust/src/views/Stardust/ConfigCenter.vue'),
    // 配置中心自带 320px 发布面板，右侧不再叠加全局数据面板
    meta: { auth: true, title: '配置中心', rightPanel: false },
  },

  // —— 发布中心（§12）：版本生命周期时间轴 ——
  {
    path: '/stardust/release',
    name: 'StardustRelease',
    component: () => import('../apps/stardust/src/views/Stardust/ReleaseCenter.vue'),
    meta: { auth: true, title: '发布中心' },
  },

  // —— 服务中心（§10）：服务注册/发现/拓扑 ——
  {
    path: '/stardust/service',
    name: 'StardustService',
    component: () => import('../apps/stardust/src/views/Stardust/ServiceCenter.vue'),
    meta: { auth: true, title: '服务中心' },
  },

  // —— 网关中心（§16）：集群 / 路由 / 节点（旧 /Gateway/* AutoPage 兜底路由保留，见上）——
  {
    path: '/stardust/gateway',
    name: 'StardustGateway',
    component: () => import('../apps/stardust/src/views/Stardust/GatewayCenter.vue'),
    meta: { auth: true, title: '网关中心' },
  },

  // —— 平台治理（§17.2 第七阶段）：用户管理 / 角色管理 ——
  {
    path: '/stardust/users',
    name: 'StardustUsers',
    component: () => import('../apps/stardust/src/views/Stardust/UserCenter.vue'),
    meta: { auth: true, title: '用户管理' },
  },
  {
    path: '/stardust/roles',
    name: 'StardustRoles',
    component: () => import('../apps/stardust/src/views/Stardust/RoleCenter.vue'),
    meta: { auth: true, title: '角色管理' },
  },
  {
    path: '/stardust/admin',
    name: 'StardustAdminCenter',
    component: () => import('../apps/stardust/src/views/Stardust/AdminCenter.vue'),
    meta: { auth: true, title: '平台管理' },
  },
];

// 布局默认为 stardust（registerLayout 第二参 true 已是当前布局），不做页面级布局切换：
// 用户如需切回 cosmic 仍可通过布局切换器手动选择。
// 路由覆盖统一由上面的 externalRoutes 声明承担，不再需要空的 configure 回调。
initApp({ externalRoutes });
