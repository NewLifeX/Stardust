# 前端-01 StardustLayout2 重构计划（方案B / v2 交接包）

- 状态：进行中（M1 骨架、交互层、应用列表、应用工作台壳、发布页已有代码，多数未经浏览器验证，进度以 `TODO.md` 为准）。**第 10 节是最新结论，与前文冲突处以第 10 节为准**（输入4-1026 起）；**第 11 节（2026-10-28 追加）中 11.1 发布页组件化、11.3 项目过滤有效；11.2 概览并排已于 2026-10-29 作废，以 11.4 为准；11.4 页签方案与 11.5 顶栏「变更」均已由用户 2026-10-29 确认，其中 11.5 先落文档、暂不实现**
- 来源：输入1-1026（按原型重构）、输入2-1026（增加 StardustLayout2，全新实现，页面与路由均在前端注册）
- 权威规格：`Doc/Design/Stardust/design2/README.md` + `console/kit.css` + `colors_and_type.css`
- 视觉参照：`Doc/Design/Stardust/ui_kits/console/index.html`（浏览器打开对照）
- 相关既有文档：`Doc/DEV-06-还原SOP.md`、`Doc/设计系统控制台-SOP.md`、`Doc/星尘控制台-打磨loop-SOP.md`、`Doc/Stardust_交互设计重构方案.md`

## 1. 目标与边界

**做什么**：新增布局 StardustLayout2，完整还原 v2「此刻」事件驱动控制台：56px 顶栏 + 居中分段导航（此刻/应用/节点/发布/治理）、生产护栏线、项目切换、主题切换、⌘K 命令面板、P/C/L/N/A 动词键、` 呼出底部控制台、Esc 逐层关闭、Drawer / GuardModal / Toast。

**做哪些页面**：此刻、应用列表、应用工作台（概览/调用链/日志/配置/实例/发布六页签）、节点（星图 + 表格 + 节点抽屉）、发布、治理（注册中心/网关/AI·MCP/平台设置四页签）。

**不做什么**：
- 不改旧 StardustLayout 及其页面、路由（回滚 = 摘掉 main.ts 里的一行注册）。
- 不改后端。数据缺口只记录，不造接口。
- 不整体复制 kit.css；不带 KD 模拟数据进生产；不照搬 x- 前缀 class、window 全局、内联样式。
- 平台设置：磁贴概览 + 项目成员管理，并提供「编辑平台设置」入口跳转 `/Platform/StarServer`。
- 不做移动端（最小宽度 900px，断点 1180/1080/900）。

## 2. 关键决策

| 决策 | 理由 |
|---|---|
| 新布局 id 为 stardust2，目录 `src/layouts/StardustLayout2/`，页面目录 `apps/stardust/src/views/Console/` | 与旧布局物理隔离，互不影响 |
| 路由不加前缀，沿用原型默认语义：`/now`、`/apps`、`/app/:tab/:id`、`/nodes`、`/ship`、`/mesh`；`/` 重定向到 `/now`（输入4-1026，取代原 `/console/*` 假设；2026-10-28 更正：应用标识改为 `App.Id` 放最后，原 `/app/:name/:tab?` 作废，旧格式书签不做兼容重定向） | 旧版未上线，无需兼容；与原型 hash 路由一一对应。新路径是否与 Cube 自动路由冲突，M1 核实（P-19） |
| 布局切换是全局的（`registerLayout(option, true)` 或运行时 `setLayout(id)`），不能按路由指定布局。因此 StardustLayout2 注册为当前布局，旧 StardustLayout 仅注册、不启用，可经 LayoutSwitcher 切回（输入4-1026，取代"初期不设默认布局"假设） | 见第 10.2 节。旧 `/stardust/*` 页面在新布局下会套新外壳，旧版未上线，接受 |
| 颜色只用 `--el-*`；原型色值与 `--el-*` 的差异集中改 `design-system/element-theme-override.css`（项目既定唯一位置），不在布局或组件 style 里覆盖 `--el-*`，不新增任何自定义 CSS 变量 | 遵守 cube-layout 红线；旧版未上线，全局改动可接受（取代原"限定在根节点内"方案，见 10.7） |
| 图标用本地 KIcon.vue + 图标名到 path 映射（取自 kit-icons.jsx，stroke 1.5） | 沙箱禁止下载，不能装 lucide-vue-next |
| 发布流程状态用模块级 reactive 单例 | 原型要求切页不丢进度 |
| 路由由单一 routes.ts 集中注册，main.ts 只加一行 | 见问题 P-02 |
| 项目与环境上下文复用 `stores/stardustContext` | 保持与旧布局数据口径一致；原型"仅生产"，环境固定展示，不提供切换 |

## 3. 目录规划

`src/layouts/StardustLayout2/`
- index.vue：外壳（顶栏、内容区 max-width 1240、左右 32px、底部留 104px、dock、各浮层挂载点）
- TopBar.vue、NavSegment.vue、ProdGuardLine.vue（2px 红色渐变）
- ConsoleDock.vue（日志/终端 tab）、CommandPalette.vue（620px）
- Drawer.vue（560px）、GuardModal.vue、ToastHost.vue（左下角，2px 左色边）
- KIcon.vue、icons.ts
- routes.ts（本布局全部路由）
- ui.store.ts（dock/palette/drawer/modal/toast 状态）
- hotkeys.logic.ts（+ .test.ts）

`apps/stardust/src/views/Console/`
- Now/：index.vue、now.logic.ts、IncidentCard.vue、AppRow.vue、ChangeTimeline.vue
- Apps/：index.vue、apps.logic.ts
- AppWorkspace/：index.vue + 六页签组件（Overview、Trace、Logs、Config、Instances、Ship）
- Nodes/：index.vue、SkyCanvas.vue、NodeDrawer.vue、nodes.logic.ts
- Ship/：index.vue、ship.flow.ts、ship.logic.ts
- Mesh/：index.vue + Registry、Gateway、McpAi、Settings 四页签
- 共享组件：Chart、Spark、Waterfall、LogLine、StatusTag 等，对应 kit-ui.jsx

## 4. 里程碑与验收

| 阶段 | 内容 | 验收 |
|---|---|---|
| M0 预飞 | 写日志、TODO、本文档 | 日志含输入原文与任务拆解（已完成） |
| M1 骨架 | 核实 P-03、P-04、P-19、P-24、P-27（见第 10 节）；注册 stardust2 布局（置为当前布局）、routes.ts（externalRoutes）、空页占位、顶栏与导航、护栏线、主题切换 | /now 等路由可访问；`pnpm run check` 通过；旧 `/stardust/*` 页面在新外壳下不白屏（样式差异可接受） |
| M2 交互层 | KIcon、Drawer、GuardModal、Toast、⌘K、动词键、dock、Esc 逐层关闭 | 逻辑文件有 vitest；浏览器逐项对照原型 |
| M3 此刻 + 应用 | 此刻首页（N 件事需要你、告警卡展开三块证据 + 处置）、应用列表、应用工作台六页签 | 截图对照 |
| M4 节点 | 星图 canvas（264px）、筛选条、批量表格、节点抽屉 | 截图对照 |
| M5 发布 | ShipPanel 与时序：GuardModal → 阶段 0-2 各 700 ms → 灰度逐台 320 ms → 停 25% 等确认 → 再 GuardModal → 剩余逐台 240 ms → 写历史与时间轴 → toast | 时序单测通过；灰度取 round(n×0.25) 且至少 1 台，分批取 ceil(n/2)，全量取 n |
| M6 治理 | 四页签 | 同上 |
| M7 收尾 | 浏览器 5188 全屏对照、断点 1180/1080/900 检查、子代理红线审查 | 红线问题 0；`pnpm run check` 0 errors |

每个阶段结束都要跑 `pnpm run check`，并更新 TODO.md、日志。验证失败按 AGENTS.md 迭代：第 1 次调整实现，第 2 次换策略，第 3 次升级给人。

## 5. 数据策略

优先复用现有取数逻辑，不再复制：
- 节点：`nodes.model.ts` 的 deriveNodeStatus、mapNodes、aggregateStats
- 概览与事件：`overview.model.ts` 的 timeAgo、buildEvents
- 项目列表：`/Platform/GalaxyProject`
- 注册中心：`/Registry/App`、`/Registry/AppOnline`、`/Registry/AppService`
- 网关：`/Gateway/GatewayRoute`
- 项目与环境上下文：`stores/stardustContext`

状态映射：原型 ok/warn/off 对应现有 running/error/offline。没有接口的区块一律显示空态或"—"，不填演示数据。

## 6. 编码红线（沿用 AGENTS.md）

- 视图 `<script setup>` 不超过 50 行；取数、格式化、筛选抽到 `*.logic.ts`，并配 vitest。
- 颜色只用 `--el-*`，摆放只用 Tailwind 工具类；禁止硬编码色值、禁止新增自定义 CSS 变量、禁止引用遗留 `--sd-*`。
- 包管理用 pnpm；写 BOM 文件要保真；并行构建失败时 `-m:1` 串行。
- 文案：用"你"不用"您"；按钮用"动词 + 对象 + 目标状态"，不写"确定/提交/查看详情"；不用 emoji，状态用形状加颜色；数字一律 tabular-nums。

## 7. 问题与风险记录

| 编号 | 问题 | 状态 | 处理 |
|---|---|---|---|
| P-01 | 原型有 app.html 和 v2 交接包两套设计冲突 | 已决 | 用户选 B |
| P-02 | cube-add-page 技能写"禁止手写路由 / 改 main.ts"，与"所有路由前端注册"表面冲突 | 已决 | 项目 `main.ts` 已用 `initApp({ externalRoutes })` 注册前端路由（`/stardust/*` 即如此），沿用该机制。技能的禁令针对"后端 Controller 驱动的页面"，控制台页面没有对应 Controller，不适用。`main.ts` 只追加 StardustLayout2 的注册与 routes 引入 |
| P-03 | 路由如何绑定到指定布局 | 已核实（部分） | 不能绑定：布局全局切换（见 10.2）。仍待 M1 核实：现有 `registerLayout` 写法（技能示例带 `component`，而 `main.ts` 现有写法需对照）、`externalRoutes` 页面如何进 RootLayout 默认插槽 |
| P-04 | 原型 token（--surface-1 等）在工程里是否存在 | 已改判 | 业务代码只用 `--el-*`，不再依赖原型 token 是否存在。待核实：`element-theme-override.css` 是否已把原型品牌紫、success 等色值映射到 `--el-*`，缺则改该文件 |
| P-05 | --success-500 工程值 oklch(0.72 0.14 155)，原型值 oklch(0.80 0.18 152) | 已决 | 不改全局，只在新布局根节点范围内覆盖 |
| P-06 | 终端需要 PTY WebSocket，当前无接口 | 缺口 | 终端 tab 显示"待接入"，高危命令拦截（rm -rf、mkfs、shutdown、reboot）保留并加单测；缺口记 TODO |
| P-07 | AI/MCP 的 Token 与审计无后端接口 | 缺口 | 空态，记 TODO |
| P-08 | Gateway 根接口缺口（TODO 已记） | 缺口 | 先接 GatewayRoute，缺字段显示"—" |
| P-09 | 发布历史、变更时间轴、告警处置（open → fixing → resolved）的后端接口未确认 | 待核实 | M3 前读现有发布与告警页的取数方式；没有则前端内存态 + 记缺口 |
| P-10 | nodes.model.ts 和 overview.model.ts 各有一个同名 isNodeOnline，签名不同 | 已知 | 新代码只引用其一，不新增第三份 |
| P-11 | JetBrainsMono-Medium.woff2 缺失（回落 Regular）、中文 webfont 未打包、--fg-3 对比度约 4.2:1 | 已知偏差 | 沿用交接包结论，收尾时复核 |
| P-12 | 星图坐标在原型里是伪随机 | 已知 | 先照抄伪随机；有拓扑坐标后再换 |
| P-13 | git diff --check 有既有空行（Platform/GalaxyProject/index.vue:299） | 既有 | 非本次改动，不处理 |
| P-14 | Stardust.Web 有他会话未跟踪的 AppQuoteController.cs，构建会失败 | 既有 | 非本次改动，原样保留 |
| P-15 | 他会话可能同时改 Cube 源码，触发 vite reload 干扰验证 | 风险 | 验证失败先排除并发干扰再判定 |
| P-16 | write_file、run_terminal_command 默认禁用，需用户明确同意 | 部分已决 | 用户已明确要求写文档，故已写日志、TODO、本文档；M1 起写代码与跑 `pnpm run check`/vitest 前仍需用户同意 |
| P-17 | 路由前缀与默认布局假设 | 已决（输入4-1026） | 用户：路由用默认语义，不加前缀。布局全局切换，故 StardustLayout2 设为当前布局，见第 2 节与 10.2 |
| P-18 | 我无法查看截图，而每个页面都要截图验证 | 已决 | 每个停点停下并通知用户发图，规则见第 9 节；节奏会被停点打断，M3 至 M6 的耗时取决于用户回图速度 |

## 8. 开工条件

1. 路由前缀与默认布局已决（输入4-1026）：路由无前缀，StardustLayout2 设为当前布局，不再需要确认。
2. 用户决策 P-24（ui-spec 与原型在图标、圆角、阴影、hover 上冲突，含图标方案）与 P-27（顶栏取舍）。
3. 我读完第 10.10 F 的待读项，把结论写回第 10 节，并更新相关 P 编号状态。
4. 用户明确同意写入源码文件和执行命令（`pnpm run check`、vitest）。
5. 以上满足后顺序：M1 → 每阶段 `pnpm run check` → 每页按第 9 节截图验证 → M7 收尾；收尾时可复用规律入 `.forgeself/memory.md`（决策 → 背景 → 理由 → 准则）。


## 9. 页面截图验证门禁（输入3-1026 追加）

**规则**：每个页面完成后都必须截图验证。我无法截图，就停止并通知用户把截图发来。

1. 每完成一个页面，先过 `pnpm run check`，再做截图验证。截图对照通过之前，该页面不标 ✅，也不开始下一个页面。
2. 只有"我拿到截图并能看清内容"才算截图验证。我现有的工具读不了图片，所以默认视为无法截图：每个页面完成后都要停下，由用户截图发给我。
3. 停下时我会发出"请截图"通知，写清五项：访问地址（`http://localhost:5188/` 加页面路由，如 `/now`、`/apps`、`/app/OrderService/config`）、要截的状态（如告警卡展开、抽屉打开、GuardModal 弹出、深色或浅色主题）、对应的原型文件或位置、浏览器窗口宽度（1280 以上；有断点适配的页面再补 1180/1080/900 各一张）、本页 `pnpm run check` 的结果。同一个停点的多张截图可以一次发完。
4. 用户发回截图后，我按布局、间距、字号、颜色、状态徽章（形状加颜色）、文案逐项对照原型，列出偏差。有偏差就修复，并请用户重新截图；没有偏差才标记通过。
5. 通过后，在日志和 TODO 里记录页面名、截图时间和对照结论。
6. 不能用"代码看起来对"或"check 通过"替代截图结论，也不能因为"无法截图"就跳过这一步。

**停点清单**（我的理解，可调整）：外壳与交互层（M2 结束）、此刻、应用列表、应用工作台六个页签（逐个）、节点、发布、治理四个页签（逐个）。共约 15 个停点。发布页的灰度停在 25% 等确认、GuardModal 弹出这类中间状态，也要各截一张。

## 10. 框架技能登记（输入4-1026 追加，供后来者先读）

### 10.1 技能位置与清单

技能目录是 `Cube/NewLife.Cube.Vue/skills/`。注意：`AGENTS.md` 和 `Doc/DEV-01-项目地图.md` 里写的 `Cube/NewLife.Cube/NewLife.Cube.Vue/...` 路径不存在，是笔误，以本节为准。cube-vue 本体在 `Cube/NewLife.Cube.Vue/web`，其 `docs/standards/ui-spec.md` 是权威 UI 规范，`docs/decisions/` 存架构决策记录。

| 技能 | 状态 | 与本次重构的关系 |
|---|---|---|
| cube-layout | 已读 SKILL.md | 布局注册、插槽约定、Token 红线，核心 |
| cube-add-page | 已读 SKILL.md | 页面目录、`usePageApi`、元数据红线、收尾子代理审查 |
| cube-page-override | 已读 SKILL.md | Section 覆盖机制，注意 PascalCase 文件会被扫描（10.6） |
| cube-design | 已读 SKILL.md | EP + Tailwind 双系统、页面类型、组件选型、交互模式 |
| modal-organize | 已读 SKILL.md | 命令式弹窗组织，影响 GuardModal、接入向导 |
| cube-add-app、cube-add-api、cube-init、cube-lov、vue-component-visual-loop | 未读 | 待读；cube-lov 用于值集下拉，vue-component-visual-loop 可能与截图验证流程有关 |
| references 子文件 | 未读 | layout-implementation-guide.md、css-token-spec.md、api-and-styling.md、cube-design 的 principles / page-types / component-decision / interaction-patterns / spec-checklist，待读 |

### 10.2 布局机制（cube-layout）

- 注册入口只有 `registerLayout(option, setAsCurrent)`，来自 `@newlifex/cube-vue/core/composables/useLayout`。`option` 的合法字段是 `id`、`label`、`icon`、`description`、`component`，**禁止用 `name`**。`setAsCurrent=true` 立即切换并写入 `localStorage`。
- 不要用 `provide(app, LayoutKey, ...)`，那是旧机制，`RootLayout` 不读取。
- `RootLayout` 只向布局注入两个插槽：`default`（主内容，外层有 keep-alive 和 transition）、`tabs`（可选标签页视图）。`sidebar`、`header` 不是插槽，布局组件必须自己渲染顶栏和导航。
- 布局切换是全局的：`registerLayout(option, true)` 或运行时 `useLayout().setLayout(id)`。**不能在路由 meta 里指定布局**。所以 StardustLayout2 只能整体取代当前布局，不能和旧布局按路由并存。
- 项目实测约定（来自旧 StardustLayout）：页面经 RootLayout 默认插槽注入，布局里不能用 `router-view`；`route.meta.rightPanel` 之类的 meta 开关由布局自己读；主容器带 `cosmic-content` 类是旧 e2e 的契约，新布局是否沿用，待 M1 决定。

### 10.3 页面与路由机制（cube-add-page + 项目现状）

- 框架默认：页面是 `apps/<app>/src/views/{area}/{controller}/index.vue`（情形 A，两级）或 `apps/<area-app>/src/views/{controller}/index.vue`（情形 B）。`menuRoutes.ts` 用 `import.meta.glob('/apps/*/src/views/**/index.vue')` 扫描，并匹配后端菜单自动注册路由。标准列表页不需要写前端文件。
- 本项目是独立宿主，页面在 `Stardust.Web.Vue/apps/stardust/src/views/`。无 Controller 的控制台页面，现有做法是 PascalCase 文件（如 `views/Stardust/Workspace.vue`）加 `main.ts` 的 `externalRoutes` 显式注册。StardustLayout2 沿用这个做法，路由集中在 `routes.ts`。
- 元数据红线：实体页字段标签一律取后端 `/meta`，不手写 `t('模块.字段')` 和列清单。整页自定义（M3 档）仅限元数据表达不了的交互，且提交说明要写明理由。**本次所有 Console 页面都是工作台、图表、星图、发布流程这类自定义交互，属于允许范围，提交说明统一引用本条。**
- 取数用 `usePageApi(area, controller)`，真实导出路径是 `@newlifex/cube-vue/core/composables/useCubeApi`，写成 `usePageApi` 路径会找不到。分页从 0 开始，`getList` 传 `pageIndex: page - 1`，总数在 `res.page.totalCount`。`useCubeApi` 内置 `onFieldError` 会弹 `ElMessage.error`，业务页不要重复弹。已有的 `request`（自动加 `/api` 前缀并带 token）也可用，旧 TopBar 就是这样取项目列表。
- 收尾强制：派子代理审查并整改到 0 问题（对应 M7）。

### 10.4 样式与设计规则（cube-layout + cube-design）

- 颜色只用 `--el-*`，或框架保留的 `--cube-layout-*`（只读不改、不新增）。禁止硬编码色值，禁止新增任何自定义 CSS 变量（含 `--{布局名}-*`），禁止在 scoped style 里覆盖 `--el-*`。
- 摆放用 Tailwind 工具类；Tailwind 颜色和圆角走映射到 `--el-*` 的语义类，不用默认调色板（如 `bg-red-500`）。不加第三套体系，不与 `el-row`、`el-col` 栅格并存。
- 组件选型顺序：框架业务组件（CbTable、CubeSearch、LovSelect、DefaultEntity 等）→ Element Plus → Tailwind 摆放 → 都不满足才新建。
- 页面类型四选一：详情/设置、列表、仪表盘、全屏工作区。Console 页面属于仪表盘和全屏工作区。
- 加载、空态、错误、确认、反馈都选预设模式，不现场发明，细则待读 interaction-patterns.md。
- 新的 UI 决策按 ADR 格式写入 `Cube/NewLife.Cube.Vue/web/docs/decisions/`，并同步 `ui-spec.md`。

### 10.5 弹窗组织（modal-organize）

- 每个弹窗一个文件夹，放一个 Vue 组件加一个 `openXxx.ts` 打开函数，就近放在页面的 `modals/` 下；跨页面复用的提升到公共的 `components/modals/`。主页面只 import `openXxx`，不直接引用弹窗组件。
- 打开函数返回 Promise，取消或关闭时 `resolve(null)`。底层是 `useModal().openModal(...)`，支持 `component`、`config`、`render` 三种内容模式。
- 命名：文件夹 kebab-case，组件 PascalCase，文件 `openUserForm.ts`，函数 `openAddUser()`。
- 对本次的影响：GuardModal、接入向导按这个结构做，`openGuard()` 返回 `Promise<boolean>`。但原型 GuardModal 有自己的生产环境警示头，`useModal` 的外壳能否容纳，要读它的源码再定（P-20）。

### 10.6 Section 覆盖（cube-page-override）

- `views/` 下 **PascalCase 开头的 .vue 文件会被 Vite 插件当作 Section 收集**（`index.vue` 除外），通过虚拟模块 `virtual:@newlifex/cube-vue-sections` 注册。Section 只在名字命中框架预设覆盖点（如 `ListSearchBar`、`ListToolbar`）时才生效，文件名错了不会报错。
- 对本次的影响：计划里放在 `views/Console/` 下的 `IncidentCard.vue`、`SkyCanvas.vue` 等子组件会被一并收集，名字不命中预设点时应无副作用，旧的 `views/Stardust/*.vue` 就是这个状态。但为避免与预设名冲突，**非页面的共享子组件优先放 `apps/stardust/src/components/console/`**，views 下只放页面入口。此项在 M1 实测确认（P-21）。

### 10.7 对计划的修正（因技能内容）

| 修正 | 原因 |
|---|---|
| 路由取消 `/console` 前缀，`/` 重定向到 `/now` | 用户指令（输入4-1026） |
| StardustLayout2 设为当前布局，不再"初期不设默认" | 布局全局切换，无法按路由并存 |
| 撤销"在新布局根节点范围内覆盖 --el-* / 补原型 token"的做法，改为只改 `element-theme-override.css` | cube-layout 红线禁止在布局或组件里覆盖 `--el-*`、新增自定义变量；之前的方案违反红线，我在此更正 |
| 补入原型里有、计划里漏掉的**接入向导**（A 键；应用、节点两个分支；约 4 秒后自动判定心跳或上线；含 SDK 与 Agent 安装命令文案），归入 M2 的弹窗，页面侧在 M3、M4 接入 | 原 `kit-app.jsx` 有，计划第 1 节没列 |
| 共享子组件放 `components/console/`，views 下只放页面入口 | 见 10.6 |
| AGENTS.md、DEV-01 的技能路径笔误记入问题表（P-22） | 避免后来者再踩 |

### 10.8 仍待了解（M1 开工前读完）

1. `layout-implementation-guide.md`：框架组件清单、菜单对接、踩坑记录、验证清单。
2. `useModal` 源码：能否自定义头部与页脚，决定 GuardModal 的实现方式。
3. `useLayout` 和 `main.ts` 现有注册段：`registerLayout` 的实际写法，LayoutSwitcher 对新布局的显示。
4. `menuRoutes.ts` 与 `initApp` 对 `externalRoutes` 的处理：新路径 `/now`、`/apps`、`/nodes` 是否与自动路由冲突。
5. `web/docs/standards/ui-spec.md` 与 cube-design 的 references：交互模式、检查单。
6. `element-theme-override.css` 当前的品牌色、success 色映射。
7. 其余未读技能：cube-lov、vue-component-visual-loop 等。

### 10.9 新增问题记录

| 编号 | 问题 | 状态 | 处理 |
|---|---|---|---|
| P-19 | 无前缀路由与 Cube 自动路由是否冲突 | 待核实 | 读 menuRoutes / initApp（10.8 第 4 项） |
| P-20 | useModal 外壳能否承载原型 GuardModal 样式 | 待核实 | 读源码（10.8 第 2 项）；不行则 GuardModal 做成布局级组件，用 ui.store 驱动，仍对外提供 `openGuard()` Promise 接口 |
| P-21 | PascalCase 子组件被 Section 扫描的副作用 | 待核实 | 子组件放 `components/console/`；M1 实测 |
| P-22 | AGENTS.md、DEV-01 的技能路径笔误 | 待处理 | 本轮不改，需你同意再改；已在 10.1 登记正确路径 |
| P-23 | 前文第 1、2、3、8 节及 TODO、日志里残留 `/console` 前缀、"初期不设默认"、KIcon、Drawer/GuardModal/ToastHost、"回滚只摘一行"等旧表述 | 部分处理 | 文首已声明第 10 节优先；第 9 节 URL 与 M1 行已改；其余待 P-24、P-27 决策后统一重写前文，TODO 与日志同步 |

### 10.10 源码与规范核实结果（输入4-1026）

本节依据：useLayout.ts、initApp.ts、useModal.ts（只读了前 200 行，共 738 行）、ui-spec.md、spec-checklist.md、layout-implementation-guide.md。

**A. 布局注册与启动**

- `LayoutOption` 的 `component` 必填。内置布局只有 `top-menu` 和 `cyber`。SKILL.md 写的 `main` 布局、默认 `cyber` 与源码不符：localStorage 无有效值时取 `registeredLayouts[0]`，即 `top-menu`。以源码为准（P-25）。
- 同 id 重复注册会覆盖；`setAsCurrent=true` 会写 `localStorage['cube-layout']`。
- `initApp` 在挂载前用 `currentComponent` 通过 `LayoutKey` provide（track: true），所以 `registerLayout` 必须在 `initApp()` 之前调用。
- 每次启动 `registerLayout(…, true)` 都会把当前布局强制设为 stardust2，用户经 LayoutSwitcher 的选择刷新后会被重置。旧版未上线，接受（P-28）。
- 此前读到的 main.ts 摘要里 `registerLayout({ id: 'stardust', label, icon })` 没有 `component` 字段，而类型要求必填，疑似摘要省略，M1 要重读 main.ts 对应段落确认（P-03 剩余项）。

**B. 路由**

- `externalRoutes` 优先级高于框架静态表、微应用、后端菜单；同 path 或同 name 的框架路由会让位，落盘在 `app.use(router)` 之前。因此 `/now`、`/apps`、`/nodes` 等不会被框架路由抢走，反过来它们会接管框架的同形态路由。
- 待读 `router/routeOverride.ts`：同形态判断是否大小写敏感，后端菜单里的 `/Nodes`、`/Registry/App` 一类路径会不会被误接管（P-19 缩小为此项）。
- 现有 `/` 重定向到 `/stardust/overview`，改为 `/now`。旧 `/stardust/*` 路由保留不删。

**C. 弹窗与反馈**

- `openModal` 支持 dialog 和 drawer，提供 `footerButtons`（type 含 danger）、`size`、`direction`、`modalClass`、`showClose`、`closeOnPressEscape`、`attrs` 透传，`onConfirm` 返回 false 可阻止关闭，实例有 `setConfirmLoading`。默认确认文案是"确定"，必须按文案规范改成"动词 + 对象"。
- 结论：节点抽屉（560px，rtl）、应用抽屉、接入向导、GuardModal 优先用 `useModal`，按 modal-organize 放 `modals/<name>/` 加 `openXxx.ts`，不再自写 Drawer.vue、GuardModal.vue。GuardModal 的"Production · 生产环境"警示头放在组件内容里。P-20 倾向可行，待读 useModal 其余 538 行确认多层叠加的 z-index 与 Esc 逐层关闭。
- Toast：ui-spec 规定操作级反馈用 `ElMessage` 或统一 Notification，改用 `ElNotification`（position 为 bottom-left）。左侧 2px 语义色边的样式只写在 `element-theme-override.css`，用 `--el-*`。ToastHost.vue 取消。
- `ui.store` 只保留 dock、命令面板的开关状态。

**D. ui-spec 与原型的冲突（待决策，P-24）**

| 规范条款 | 原型要求 | 冲突情况 |
|---|---|---|
| 图标统一 `@element-plus/icons-vue`，禁止第二套图标库 | Lucide，stroke 1.5，README 禁其他图标 | 直接冲突。本地 KIcon 加 Lucide path 算第二套图标 |
| hover 只改背景或边框，禁止 translateY、缩放、装饰性弹跳 | kit.css 有 hover 与进场动效（x-pop、x-slide 等），具体样式需逐条核对 | 待核对。另 `element-theme-override.css` 已有卡片 hover `translateY(-4px)`，属旧遗留违规（P-29，需核实其选择器作用范围） |
| 圆角不超过 12px | Chip 为 999px 胶囊，搜索胶囊同理 | 直接冲突 |
| `el-card` 用 `shadow="never"`，用边框区分层级 | 卡片、抽屉、弹层有 `--elev-1/2` 阴影 | 冲突，抽屉与弹层可能需要阴影 |
| 页面类型四选一，列表 `max-w-[1200px]` | 内容列 1240px | 控制台属仪表盘和全屏工作区，不冲突 |
| 排版 | JetBrains Mono 加 tabular-nums | ui-spec 未定义，属 spec 缺口 |
| 图标按钮必须有 `aria-label` 或 `el-tooltip` | 原型顶栏图标按钮没有 | 实现时补上，不冲突 |

处理流程按 principles.md 第六节：标记缺口，决策者拍板，写入 ADR 并更新 ui-spec。你已要求"完全按原型还原"，我理解为原型优先，但这是一条需要你明确的豁免。建议把豁免写成星尘自己的文档（`Doc/前端-02-控制台UI规范豁免.md`），不改 `Cube/` 下框架文档，避免影响其他使用 cube-vue 的项目。

**E. layout-implementation-guide 与原型的差异**

guide 要求生产级布局具备：菜单对接 menuStore、UserProfile、LayoutSwitcher、ThemeSwitcher、ModeSwitcher、NotificationBell、面包屑、响应式。我的建议取舍如下，待你确认（P-27）：

- 导航：按 routes.ts 固定 5 项，不接 menuStore。理由是你要求所有页面与路由前端注册，且原型是 5 项分段导航。
- 头像与退出登录：必须保留，用框架 `UserProfile`（variant 为 navbar）放在原型头像位，待核实能否样式化成 34px 圆形。
- 主题：沿用项目 `design-system/theme.ts` 的 `toggleTheme`，不放 ModeSwitcher、ThemeSwitcher，避免两套明暗机制不同步。
- LayoutSwitcher、NotificationBell、面包屑、菜单搜索：原型没有，不放。
- guide 的 `variables.css` 加 `--aurora-xxx` 自定义变量写法与 SKILL 红线矛盾，以 SKILL 和 ui-spec 为准，登记为 P-26。
- guide 验证清单里可落地的项（刷新后布局与主题保持、用户头像下拉、路由跳转正常等）并入每个截图停点的通知。

**F. 仍待读（M1 开工前）**

1. `useModal.ts` 第 200 行之后。
2. `router/routeOverride.ts`。
3. `main.ts` 的 registerLayout 段落。
4. `UserProfile.vue`。
5. `src/theme/tailwind.css` 的 Tailwind 到 `--el-*` 语义映射，确认 Stardust.Web.Vue 有无对应配置。
6. cube-design 的 principles、page-types、component-decision、interaction-patterns。
7. `web/docs/decisions/0003`。
8. 技能 cube-lov、vue-component-visual-loop、cube-add-app、cube-init、cube-add-api。

### 10.11 新增问题记录

| 编号 | 问题 | 状态 | 处理 |
|---|---|---|---|
| P-24 | ui-spec 与原型在图标、圆角、阴影、hover、排版上冲突 | 待你决策 | 见 10.10 D；建议原型优先，豁免写入 `Doc/前端-02-控制台UI规范豁免.md`；图标二选一：用 Element Plus 图标近似替代，或豁免后保留原型 Lucide path |
| P-25 | SKILL.md 与源码不一致（`main` 布局不存在，默认布局是 top-menu） | 已知 | 以源码为准 |
| P-26 | guide 的布局自定义变量写法与 SKILL 红线矛盾 | 已决 | 以 SKILL 和 ui-spec 为准 |
| P-27 | 顶栏取舍：导航固定 5 项、UserProfile、LayoutSwitcher、NotificationBell | 待你确认 | 见 10.10 E |
| P-28 | 每次启动 `registerLayout(…, true)` 会重置用户的布局选择 | 已知 | 接受 |
| P-29 | `element-theme-override.css` 已有卡片 hover `translateY(-4px)`，违反 ui-spec | 待核实 | 先查选择器作用范围，新布局不依赖该效果；是否清理另议 |

### 10.12 对前文的第二轮修正（以本节为准）

| 修正 | 原因 |
|---|---|
| 第 3 节目录：取消 Drawer.vue、GuardModal.vue、ToastHost.vue，改为 `modals/<name>/` 加 `openXxx.ts`，Toast 用 `ElNotification` | 见 10.10 C |
| KIcon.vue 与 icons.ts 暂挂起 | 待 P-24 决定图标方案 |
| 第 1 节"回滚只需摘 main.ts 一行"作废 | 布局全局切换，StardustLayout2 已是当前布局 |
| M1 前置增加：读 routeOverride、main.ts、UserProfile、tailwind 映射 | 见 10.10 F |
| 第 8 节开工条件改为：你决策 P-24、P-27，并明确同意写源码与执行命令 | 路由前缀已决 |

### 10.13 第二批源码核实（输入4-1026 续）

本节依据：cube-vue `core/App.vue`、`router/index.ts`、`router/routeOverride.ts`、`composables/useModal.ts`（全文）、`components/UserProfile.vue`（只读前 120 行，共 460 行）、`components/` 目录清单，以及 `Stardust.Web.Vue/src/main.ts`（全文）。未读的部分在各条里明确标注。

**A. 路由（P-19 缩小范围）**

- `routeOverride` 的"同形态"比较（`normalizeRoutePath`）只把 `:param` 统一成占位符，**区分大小写**。
- `router/index.ts` 创建路由时没有传 `sensitive`、`strict`，vue-router 默认匹配**不区分大小写**。两边不一致：外部声明的 `/nodes` 不会让框架的 `/Nodes` 让位，两条都会注册，最终哪条命中由 vue-router 的匹配排序决定，我没有验证。
- 框架静态路由（`core/routes`）和后端菜单路由里有没有与 `/now`、`/apps`、`/app/:name`、`/nodes`、`/ship`、`/mesh` 不区分大小写后同形态的路径，**未验证**，下一步读 `core/routes/index.ts` 与 `utils/menuRoutes.ts`。
- M1 的实测办法：`initApp` 会把路由挂到 `window.router`，登录后在浏览器控制台执行 `router.resolve('/nodes')` 看命中的 name；`getRoutePrioritySnapshot()` 可看外部声明与已登记记录。后端菜单路由是登录后才注册的，所以必须登录后测。
- 外部路由的约束：必须有字符串 `name`（否则只告警，且无法撤销）；必须有 component、redirect 或 children；`meta.auth` 缺省按 true 处理。

**B. 导航守卫**

- 框架 `beforeEach` 依次做：处理 URL hash token；微应用路由未初始化时跳 `/loading`；有 token 时拉用户信息和菜单，首次有菜单就 `registerMenuRoutes` 并强制重导航一次；无 token 且 `meta.auth` 不为 false 时跳登录页。守卫里**没有按路径前缀放行或拦截的逻辑**。
- `main.ts` 的注释提到"beforeEach 守卫前缀"，在框架守卫里没有找到，可能是过期注释。项目自己有没有另加守卫，未确认（`src/` 下没有 router 目录，但我没有搜过全项目），授权后用 `git grep beforeEach` 确认。

**C. 弹窗宿主与行为（P-20 部分解除）**

- `App.vue` 在 `RootLayout` 之外挂了全局 `ModalContainer`，所以 StardustLayout2 **不需要自己挂弹窗宿主**。
- `openModal` 返回的是实例，不是 Promise；Promise 化要在 `openXxx.ts` 里自己包一层（modal-organize 的写法就是这样）。
- `closeModal` 每次都会先调 `onCancel`，**包括确认成功后的关闭**。Promise 只取第一次 resolve，所以在 `onConfirm` 里先 `resolve(true)` 即可；但 `onCancel` 里**不能放副作用**（比如弹"已取消"的提示），否则确认时也会触发。
- 关闭动画 300 ms 之后才触发 `onClosed` 并从 `modals` 列表移除。`closeAll()` 可一次关掉所有弹窗。
- 弹窗的 z-index、Esc 行为、抽屉宽度由 `ModalContainer.vue`（15.8 KB）决定，**未读**。原型要求 Esc 逐层关闭，需要读完它再定怎么和命令面板、底部控制台配合。

**D. 头像与退出登录（P-27 细化）**

- `UserProfile` 是自带触发按钮（头像、用户名、箭头）和下拉菜单的整体组件，样式写在组件内部。个人资料走 `router.push('/profile')`，退出走 `userStore.logout()`，头像地址是 `resolveAssetUrl('/Cube/Avatar/{id}')`。它的样式部分（120 至 460 行）未读。
- 要做成原型的 34px 圆形头像，要么去覆盖它的内部样式，要么自己做一个小组件。我倾向后者：在 StardustLayout2 里做 `UserAvatarMenu`，用同样的 `useUserStore().userInfo`、`resolveAssetUrl`、`userStore.logout()` 加 `ElDropdown`，行为与框架一致，样式自己控制。
- 框架里还有 `UserAvatar.vue`、`UserMenu.vue`、`LogoutButton.vue`，**未读**，实现前先读，看能否直接复用。
- 框架有 `components/Notification.ts`（2.9 KB），**未读**。ui-spec 说的"统一 Notification"很可能就是它，Toast 实现前先读，不直接用裸 `ElNotification`。

**E. 项目 main.ts 现状（P-03 疑点解除）**

- `registerLayout` 确实带 `component: StardustLayout`，此前摘要省略了该字段。它在 `initApp` 之前调用，第二参数为 true。
- 所有 `externalRoutes` 都带字符串 `name` 和 `meta: { auth, title }`；`/` 是 `HomeRedirect`，目前重定向到 `/stardust/overview`，改为 `/now`。旧布局用 `meta.rightPanel` 控制右栏，新布局没有右栏，不需要该字段。
- 页面基本用 `() => import('../apps/stardust/src/views/...')` 懒加载。新路由沿用这个写法，集中放在 `routes.ts`，`main.ts` 里用展开运算符并入 `externalRoutes`。
- `main.ts` 当前引入的样式层：`tokens.css`、`tokens.stardust.css`、`element-theme-override.css`、`list-page.css`、`styles/global.css`，并调用 `initTheme()`。StardustLayout2 不再新增样式层，原型与 `--el-*` 的色值差异只改 `element-theme-override.css`。

**F. 待读清单更新**

已读完：`useModal.ts` 全文、`main.ts`、`routeOverride.ts`、`router/index.ts`、`App.vue`。仍待读：

1. `core/routes/index.ts`、`utils/menuRoutes.ts`（P-19）。
2. `layouts/RootLayout.vue`（`meta.layout=false` 的处理、keep-alive 对页面的影响）。
3. `components/ModalContainer.vue`（z-index、Esc、抽屉宽度）。
4. `components/Notification.ts`、`UserAvatar.vue`、`UserMenu.vue`、`LogoutButton.vue`。
5. `Stardust.Web.Vue` 的 Tailwind 配置及到 `--el-*` 的语义映射。
6. cube-design 的 principles、page-types、component-decision、interaction-patterns，以及 ADR 0003。
7. 技能 cube-lov、vue-component-visual-loop、cube-add-app、cube-init、cube-add-api。

**G. 状态更新**

| 编号 | 新状态 | 说明 |
|---|---|---|
| P-03 | 已解除 | `registerLayout` 带 component，布局全局切换，均已核实 |
| P-19 | 缩小为大小写与匹配排序 | 见 A；待读 `core/routes` 与 `menuRoutes.ts`，M1 登录后实测 |
| P-20 | 部分解除 | 宿主不用自挂；Esc、z-index 待读 `ModalContainer` |
| P-27 | 细化 | 头像菜单自做 `UserAvatarMenu`，其余取舍不变，仍待你确认 |
| P-30（新） | 待核实 | `main.ts` 注释里的"beforeEach 守卫前缀"是否仍有效，授权后 `git grep` |

### 10.14 第三批源码核实（core/routes、menuRoutes、ModalContainer、RootLayout）

来源：输入4-1026 之后的源码阅读。与前文冲突处以本节为准。

**A. 路由（P-19 基本解除）**
- 框架静态路由仅有：`/`（name `home`）、`/login`、`/activate`、`/profile/security`、`/unauthorized`、`/loading`、兜底 `/:pathMatch(.*)*`（DefaultEntity，auth true）。没有 `/now`、`/apps`、`/nodes`、`/ship`、`/mesh`，不会与新路由冲突。
- 外部路由 `/` 重定向 `/now`：按 name `home` 与归一化 path 接管框架首页，旧首页让位。
- 后端菜单叶子节点经 `registerMenuRoutes` 注册为 PascalCase 路径（如 `/Nodes/Node`），按精确 path 去重，命中外部声明时让位。与我们的小写单段路径形态不同。
- 外部路由必须写字符串 `name`、`meta.auth: true`、`meta.title`；不要用 `meta.layout`，布局是全局的。
- 仍待验证：vue-router 未传 `sensitive`，默认不区分大小写，若后端菜单恰有 `/Nodes` 这类单段路径，会与 `/nodes` 同形态。M1 登录后用 `router.resolve('/nodes')` 实测。
- 副作用：`menuRoutes` 的 glob 会扫描 `apps/*/src/views/**/index.vue`，`Console/*/index.vue` 会被菜单解析器看到。仅当后端菜单出现 `/Console/...` 才命中，风险低，只记录。页面文件继续用 `index.vue`（cube-add-page 约定）。

**B. 弹窗（P-20 基本解除）**
- `ModalContainer` 用 `ElDialog`/`ElDrawer`，由全局 `App.vue` 挂载，Teleport 到 body。
- 支持透传：`modalClass`、`zIndex`、`closeOnPressEscape`、`closeOnClickModal`、`showClose`、`size`、`direction`（默认 rtl）、`withHeader`、`attrs`。节点抽屉用 `size: '560px'`，自绘头部用 `withHeader: false`。
- 内容三选一：`component`（带 `componentProps`/`componentEvents`）、`render(ctx)`（ctx 有 `close`/`confirm`/`setConfirmLoading`）、`config`（表单，仅 8 种控件）。GuardModal 用 render 模式，抽屉与向导用 component 模式。
- 底部：`footerButtons` 可自定义（`type: 'danger'` 可用）。默认按钮文案取 i18n `button.confirm`，必须覆盖为「动词+对象」；`cancelText`/`confirmText` 可改。
- 底部容器的内边距与上边框是组件内写死的内联样式，无法覆盖，截图对照时容忍，记 P-31。
- Esc 逐层关闭：Element Plus 原生按弹层栈处理。命令面板与 dock 不是 `useModal` 弹窗，由 `hotkeys.logic.ts` 判断顺序：先让位给打开的弹层，再关面板，最后收 dock。
- z-index：Element Plus 弹层默认 2000 起，高于 dock 与顶栏，不需要改。

**C. RootLayout 契约**
- 布局组件只会收到 `#default` 与 `#tabs` 两个插槽；页面通过 `<slot />` 渲染，不能用 `router-view`。
- `route.path === '/login'` 或 `meta.layout === false` 时不渲染布局，登录页即属此类。
- 路由切换时 `tabsStore.addTab` 对所有带布局的页面执行。StardustLayout2 不渲染 `#tabs` 插槽，标签页不显示，但数据仍累积，无害，记 P-33。
- 布局在路由切换间保持挂载，快捷键监听与全局状态放布局内即可；进入 `/login` 时布局卸载，需在 `onBeforeUnmount` 解除监听。
- `onMounted` 在有 token 时拉用户信息与菜单，布局无需重复拉取。
- 兜底布局是 TopMenuLayout。`meta.keepAlive`、`meta.transition` 由 RootLayout 处理，我们的页面不需要设置。

**D. 新增与更新的问题记录**

| 编号 | 内容 | 状态 |
|---|---|---|
| P-19 | 无前缀路由与框架路由冲突 | 基本解除；剩 sensitive 与 `router.resolve` 实测（M1） |
| P-20 | useModal 承载 GuardModal | 基本解除；GuardModal 用 render 模式，抽屉用 component 模式 |
| P-31 | ModalContainer 底部内边距与上边框写死 | 已决：容忍差异，截图对照时说明 |
| P-32 | menuRoutes glob 会扫到 Console/*/index.vue | 已决：风险低，不处理 |
| P-33 | tabsStore 在不渲染 #tabs 时仍累积 | 已决：无害，不处理 |

**E. 待读清单（更新）**
Notification.ts、UserAvatar.vue、UserMenu.vue、LogoutButton.vue、Tailwind 配置与 `src/theme/tailwind.css`、cube-design 文档（principles/component-decision/interaction-patterns/page-types）、技能 cube-lov、vue-component-visual-loop、cube-add-app、cube-init、cube-add-api，以及 `git grep beforeEach`（P-30）。

### 10.15 实现规范：组件与逻辑分离（输入5-1026，参照 LovSelect）

来源：用户指定参照 `Cube/NewLife.Cube.Vue/web/core/components/LovSelect/`。已读：目录清单、`index.vue`（全文）、`LovSelect.vue`（1-120 行，共 267 行）、`useLovSelect.ts`（1-120 行，共 357 行）、`README.md`（1-140 行，共 259 行）、`LovSelect.test.ts`（1-80 行，共 388 行）。**未读**：`LovSelect.vue` 120 行后的 script、`useLovSelect.ts` 120 行后、`README.md` 140 行后（Part C 不变量与踩坑）、`lovStore.ts`、`LovSelect.story.ts`、`LovSelect.ct.spec.ts`、`LovSelect.mockFidelity.spec.ts`。

**A. LovSelect 的目录分层（已读部分）**

| 文件 | 职责 |
|---|---|
| `index.vue` | 目录入口。普通 `<script lang="ts">` 里 `export { default } from './LovSelect.vue'`，使目录可被直接引用；组件名由 `LovSelect.vue` 内 `defineOptions({ name })` 保证。 |
| `LovSelect.vue` | 纯 UI 薄壳：调用 `useLovSelect`，模板只做绑定，并把 hook 返回值转成 `emit`。 |
| `useLovSelect.ts` | 逻辑与事件层：状态、watch、加载、事件动作都在这里。 |
| `lovStore.ts` | 数据与缓存层，供 hook 委托。 |
| `LovSelect.test.ts` | Vitest：只测 hook 的逻辑与事件返回值，不渲染 DOM。 |
| `*.ct.spec.ts`、`*.story.ts`、`*.mockFidelity.spec.ts` | 组件测试、故事、桩忠实性测试（本项目暂不照搬，见 C）。 |
| `README.md` | 按技能 `vue-component-visual-loop` §七 模板：Part A 开发前必填（功能需求、能力矩阵、设计、流程、验证清单、不变量、数据契约、边界），Part B 开发中沉淀。 |

**B. 逻辑层的写法要点（已读部分）**

1. **不依赖 `emit`。** 所有"事件"以返回值暴露：`onEnumChange(val)` 返回应 emit 的值，组件只负责转成 `emit`。因此 hook 可脱离渲染直接测。
2. **不含生命周期。** hook 里没有 `onMounted`，加载动作（`loadMeta`）由组件在 `onMounted` 里显式调用。`watch`（类型解析、code 切换、回显）留在 hook 内，属于可测逻辑。
3. **入参全部是 `Ref`。** `UseLovSelectOptions` 里的 `code`、`modelValue`、`multiple`、`parent`、`valueType` 都是 `Ref`；返回值是一组 `Ref`、`ComputedRef` 和函数，类型显式导出（`UseLovSelectOptions`、`UseLovSelectReturn`）。
4. **依赖可注入。** `fetchMeta` 可选注入，测试传确定性 mock，不必 mock 网络层。
5. **测试隔离。** 测试用 `effectScope` 包裹 hook，`flushPromises` 等异步；外部模块用 `vi.mock` 桩掉。
6. **文档与测试互相对应。** README 有"功能、故事、测试"一一对应表和不变量红线（INV-n），每条不变量有守卫测试。
7. **别名导入铁律。** 被 mock 的模块必须用项目别名导入，不能用相对路径，否则会绕过桩（README §5 记录的踩坑）。

**C. 套用到 StardustLayout2 与 Console 页面的约定（决策）**

- **一个有逻辑的组件 = 一个目录**：`index.vue`（重导出）+ `Xxx.vue`（纯 UI）+ `useXxx.ts`（状态与事件）+ `Xxx.test.ts`（Vitest）。无状态的纯函数放 `xxx.logic.ts`，与 AGENTS.md 的"逻辑抽 `*.logic.ts`"一致；有状态、有 watch 的才写 `useXxx.ts`。
- **组件脚本保持薄**：`<script setup>` 只调用 hook、转发 emit、在 `onMounted` 里触发加载，目标 ≤50 行（AGENTS.md 红线，对 `index.vue` 与视图 `<script setup>`）。
- **事件以返回值暴露**：hook 不接 `emit`；弹窗确认、发布时序、命令面板执行等都返回"应触发的动作/值"，由组件转发。
- **依赖可注入**：取数函数、定时器（发布时序的 700 ms、320 ms、240 ms）、路由跳转函数都作为可选参数注入，测试里传假实现。发布时序要用 `vi.useFakeTimers()` 验证，不用真实等待。
- **hook 无生命周期**：监听快捷键、定时器的注册与卸载由组件在 `onMounted`/`onBeforeUnmount` 里显式调用 hook 暴露的 `start`/`stop`。
- **不做** `*.ct.spec.ts`、`*.story.ts`、`*.mockFidelity.spec.ts`：这是 cube-vue 框架自己的 Gallery/CT 体系，本项目没有对应基建，已有的验证手段是 Vitest + 截图对照。若后续需要，记 TODO。
- **README 不逐组件写**：只在计划文档里按页面保留"功能清单、验证项、不变量"，不变量照 INV 风格写成负向约束（例如：发布未经 GuardModal 确认不得推进；灰度台数不得小于 1）。
- 页面级（`views/Console/*/index.vue`）沿用同一套：`index.vue` 薄壳，取数与格式化进 `*.logic.ts`，已有 `nodes.model.ts`、`overview.model.ts` 继续复用。

**D. 新增问题**

| 编号 | 内容 | 状态 |
|---|---|---|
| P-34 | 现有单测是 `node --experimental-strip-types` 直接运行的普通脚本（`nodes.model.test.ts`、`appsModel.test.ts`），导入带 `.ts` 后缀、不用 `@` 别名；`Stardust.Web.Vue` 无 vitest 依赖与配置，`pnpm run check` 不跑测试。此前文档与 AGENTS.md 里的"vitest"与现状不符。前文误写的 `overview.model.test.ts` 不存在，`overview.model.ts` 目前没有测试。 | 已核实现状，测试方案待你决策，见 10.16 |
| P-35 | 上面 C 里的 `useXxx.ts` 与 AGENTS.md 的 `*.logic.ts` 命名并存，是我对"逻辑分离"的推断，未经你确认。 | 已按推断执行，有异议请改 |
| P-36 | LovSelect 未读部分（README Part C、story、CT）可能含更多约定。 | 需要时再读，不阻塞 M1 |


### 10.16 测试运行方式核实（P-34，输入5-1026 续）

**现状（已核实）**
- `Stardust.Web.Vue` 的测试文件是自带 `ok(cond, msg)` 计数的脚本，用 `node --experimental-strip-types <file>` 运行，导入写 `./xxx.ts`（带后缀）。`git grep vitest` 在 `Stardust.Web.Vue` 下无任何命中，`vitest.config.ts` 不存在。
- 因此 10.15 C 里的 "Vitest"、`vi.useFakeTimers()`、`vi.mock` 在本工程当前不可用。LovSelect 的 `pnpm test:unit` / `vitest.config.unit.ts` 是 cube-vue 自己的体系，Cube 是否为子模块、其 vitest 是否可被本工程复用，**未验证**。
- strip-types 的限制：不支持 `enum`、构造函数参数属性、`@` 别名和无后缀的相对导入。所以要被测的 `*.logic.ts` / `useXxx.ts` 只能用相对路径并带 `.ts` 后缀导入，且不能 import `.vue` 或依赖 Element Plus。

**两个方案（待你选）**
- **方案 1：沿用现有脚本约定。** 零依赖，与现有测试一致。时序类逻辑（发布 700/320/240 ms）靠注入 `delay` 函数和假定时器实现，不用 `vi`。缺点：没有 mock、没有 `effectScope` 辅助，`useXxx.ts` 里的 `watch` 测起来更费劲。
- **方案 2：给 Stardust.Web.Vue 新增 vitest。** 需要改 `package.json`（devDependencies）、新增配置、新增 `test:unit` 脚本，并跑 `pnpm install`，还要处理 `patches/element-plus.patch`。能完整套用 LovSelect 的写法，代价是引入新依赖和改工程配置。

**我的默认选择：方案 1 起步**，不加依赖，先把 M1 骨架跑通；`useXxx.ts` 里把状态机写成纯函数（输入状态与事件、返回新状态），watch 只做薄转发，这样脚本测试够用。你若选方案 2，我先做一次单独的"接入 vitest"提交再继续。

**对 10.15 C 的修正**：凡写"Vitest"处，在方案确定前一律理解为"逻辑测试"；运行命令以方案为准。`check` 不含测试，所以每次改动除了 `pnpm run check`，还要单独跑相关测试脚本并把结果写进日志。


### 10.17 Cube 测试体系与令牌写法核实（输入5-1026 续）

**A. Cube 是否被根仓库跟踪（部分核实）**
- `git ls-files Cube/NewLife.Cube.Vue/web/package.json` 退出码 0 但无输出，说明该文件不被根仓库跟踪；`git check-ignore -v` 退出码 1，说明也不是被 `.gitignore` 排除。`git submodule status` 输出为空，所以也不是子模块。
- 原因（Cube 自带独立 `.git`，还是单纯未提交）**未验证**，核实命令：`git -C Cube/NewLife.Cube.Vue rev-parse --show-toplevel`。
- 影响：修改 Cube 内文件无法从根目录 `git diff` 回溯。本次任务不改 Cube。

**B. Cube 的 vitest（部分核实）**
- `Cube/NewLife.Cube.Vue/web/package.json` 的 `test:unit` 为 `vitest run --config vitest.config.unit.ts`，`vitest ^4.1.11`、`jsdom`、`@vue/test-utils` 都在该包的 `devDependencies`，该包是 pnpm 工作区成员。
- `vitest.config.unit.ts` 是最小配置：`@vitejs/plugin-vue` 加 jsdom，外加桩掉虚拟模块 `virtual:@newlifex/cube-vue-config`。已读前 40 行（共 57 行），`include` 范围与别名**未读**。
- `Stardust.Web.Vue/package.json` 未声明 vitest。pnpm 默认不把依赖提升到其他包，所以 Stardust 目录下直接用 Cube 的 vitest 能否成功**未验证**。
- 结论：P-34 不变，默认仍按方案 1（沿用 `node --experimental-strip-types` 脚本）。方案 2 若选，需单独评估新增依赖的成本。

**C. 旧布局的样式写法与红线冲突（新发现）**
- 旧 `StardustLayout/TopBar.vue` 用 scoped `<style>`，引用 `--border-1`、`--fg-1`、`--fg-3`、`--surface-1/2`、`--brand-*`、`--success-500`、`--danger-500`、`--radius-*`、`--dur-micro`、`--ease-out`（均来自 `tokens.stardust.css`），并硬编码 `color: #ffffff`（`.sd-avatar`）。
- 这与 `cube-layout` 技能（只用 `--el-*` 与 `--cube-layout-*`）及 AGENTS.md（禁止硬编码色值）冲突。此前日志里"原型令牌已齐备，直接复用"的说法与 P-04"只用 `--el-*`"互相矛盾，以 P-04 为准。
- StardustLayout2 不照搬旧 TopBar 的写法。摆放用 Tailwind，颜色用 `--el-*`，原型独有的色值差异只改 `element-theme-override.css`。
- 待读 `element-theme-override.css` 全文，确认 `--el-*` 能否覆盖原型所需的文字层级、边框层级、阴影、成功色。

**D. 新增问题**

| 编号 | 内容 | 状态 |
|---|---|---|
| P-37 | 旧 TopBar 使用原型令牌与硬编码色值，与红线冲突；新布局不得照搬 | 已决：只用 `--el-*`；原型色值差异放 `element-theme-override.css` |
| P-38 | Cube 不被根仓库跟踪，改动无法从根目录回溯 | 已记录：本次不改 Cube；原因待 `git -C` 核实 |


### 10.18 对 10.17 的更新（输入5-1026 续3；与 10.17 冲突处以本节为准）

**A. Cube 仓库归属（已核实，取代 10.17 A 的"待核实"）**
- `git -C Cube/NewLife.Cube.Vue rev-parse --show-toplevel` 返回 `.../Stardust/Cube`：`Cube/` 是嵌套在根仓库目录内的**独立 git 仓库**。根仓库不跟踪它（`git ls-files` 为空），也不是子模块（`git submodule status` 为空）。
- 影响：根目录的 `git diff`、`git grep` 看不到 Cube 内容，查 Cube 须用 `git -C Cube ...`。此前"无 vitest"的 `git grep` 结论只对 `Stardust.Web.Vue` 成立（该目录被根仓库跟踪，`tsconfig.json` 在 `git ls-files` 中）。
- 本次任务不改 Cube。P-38 已决。

**B. Cube 的 vitest 能否复用（部分核实）**
- `vitest.config.unit.ts` 全文已读：环境 jsdom、`globals: true`；`include` 为 `core/__tests__/**/*.{spec,test}.ts` 与 `core/**/*.{spec,test}.ts`（相对 Cube/web 根）；排除 `*.ct.spec.ts`；别名 `@newlifex/cube-vue` 指向 web 目录；有虚拟模块桩。
- 结论：该配置**不会收集** `Stardust.Web.Vue` 的测试。方案 2 须在 Stardust.Web.Vue 另建配置并安装 vitest、jsdom、`@vue/test-utils`（Cube 用的是 vitest ^4.1.11），Stardust 目录下能否直接解析 Cube 的 vitest **未验证**。
- 方案选择不变：默认方案 1。

**C. element-theme-override.css 核实（239 行全文已读）**
- 作用：把 `--el-*` 指向 `--sd-*`（`tokens.css` 与 `tokens.stardust.css` 定义 `--sd-*`），声明在 `:root, html.dark` 一组。它是 `--sd-*` 到 `--el-*` 的集中映射层，业务代码只用 `--el-*` 合规。
- 覆盖的 `--el-*`：主色、成功、警告、危险、信息色及其 light/dark 梯度；文本 5 级（primary、regular、secondary、placeholder、disabled）；边框 5 级；背景 3 级（bg-color、page、overlay）；填充 4 级；圆角 base、small、round、circle；字体与字号 6 级；阴影 3 级。
- 原型需要但 `--el-*` 没有对应项：①原型文字 4 级与 EP 文本 5 级的对应关系；②原型圆角 10、8、999 px（EP base 为 6、round 为 20）；③`--elev-1/2`、`--ease-out`、`--dur-*`。后两类按 Tailwind 任意值处理（非颜色，不违反红线），圆角仍属 P-24 豁免范围。
- 数值对照**未做**：`--el-color-primary` 是否等于原型 `--brand-500`、`--el-color-success` 是否等于 `--success-500`（P-05）、危险色、文字与边框层级，都要读 `tokens.stardust.css` 的实际值再定。文字层级候选：fg-1 对 primary、fg-2 对 regular、fg-3 对 secondary、fg-4 对 placeholder，仅为候选，数值核对前不写进代码。
- 对新布局生效的全局规则（需知悉）：
  1. 所有 `button`、`input`、`textarea`、`select`、`[tabindex]` 的 `:focus-visible` 统一为青色焦点环，并强制 `border-radius: var(--sd-radius-md)`，可能破坏原型 999 px 胶囊搜索与圆形头像的焦点外形（P-40）。
  2. `.sd-card`、`.el-card.is-clickable`、`.app-card` 悬浮上移 4 px。仅这三个选择器，普通 `el-card` 不受影响。新页面不使用这三个类即可避开（P-29 的作用范围已澄清）。
  3. `.el-drawer`、`.el-dialog` 的背景与阴影、按钮/链接/输入/tag 的 200 ms 过渡为全局统一。
  4. 文件里只有 `:root, html.dark` 一组声明，没有独立的亮色分支，亮色模式能否正确未核对。
- 新代码不复用 `sd-tabs`、`sd-table`、`sd-input`、`sd-switch` 等既有类名，避免被旧规则影响。
- 因旧布局未上线（输入4-1026），改动该文件不用考虑旧页面兼容，但每次修改后仍要跑 `pnpm run check`。

**D. 问题记录更新**

| 编号 | 内容 | 状态 |
|---|---|---|
| P-29 | 卡片悬浮位移的作用范围 | 已澄清：仅 `.sd-card`、`.el-card.is-clickable`、`.app-card`，新页面不用这三个类 |
| P-38 | Cube 不被根仓库跟踪 | 已决：Cube 是嵌套独立仓库，用 `git -C Cube` 查，本次不改 |
| P-39 | 原型令牌与 `--el-*` 的数值对照未做 | 待核实：读 `tokens.stardust.css` 的 `--fg-*`、`--border-*`、`--brand-500`、`--success-500`、`--danger-500` |
| P-40 | 全局焦点环强制 `--sd-radius-md`，可能影响胶囊与圆形元素 | 待截图验证；必要时只改 `element-theme-override.css` |

**E. 下一步**
1. 读 `tokens.stardust.css` 相关数值，定稿映射（P-39）。
2. 读 `main.ts` 的 `registerLayout` 段与原型 `kit.css` 顶栏、导航段。
3. 同步 TODO.md 第 25 行（问题范围改为 P-01~P-40，写入测试方案与 10.15 至 10.18 的结论）。
4. 建 `StardustLayout2` 骨架，跑 `pnpm run check`，停下请用户截图。

### 10.19 令牌数值对照与 main.ts 更正（输入5-1026 续4；与前文冲突处以本节为准）

依据：`tokens.stardust.css` 全文（433 行）、`main.ts` 全文（274 行）、`kit.css` 1-200 行。

**A. 原型令牌与 `--el-*` 的对照（P-39 已核实，取代 10.18 C 的候选）**

| 原型令牌 | 数值 | 对应的 `--el-*` | 备注 |
|---|---|---|---|
| `--brand-500` | oklch(0.62 0.19 285) | `--el-color-primary` | 一致 |
| `--success-500` | oklch(0.72 0.14 155) | `--el-color-success` | `kit.css` 的 `.x-tag.ok` 字面写 oklch(0.80 0.18 152)，两套值，见 P-41 |
| `--warning-500` | oklch(0.78 0.14 75) | `--el-color-warning` | 与 `kit.css` 字面值一致 |
| `--danger-500` | oklch(0.66 0.19 25) | `--el-color-danger` | 与 `kit.css` 字面值一致 |
| `--cyan-500` | oklch(0.75 0.14 210) | `--el-color-info` | 与 `kit.css` 字面值一致 |
| `--fg-1` / `--fg-2` / `--fg-3` | 主文本 / 次文本 / 说明 | `--el-text-color-primary` / `-regular` / `-secondary` | |
| `--fg-4` | 禁用 | `--el-text-color-placeholder`（也是 `-disabled`） | |
| `--border-1` | rgba(255,255,255,0.08) | `--el-border-color-light`（同值：`-lighter`、`-extra-light`） | |
| `--border-2` | rgba(255,255,255,0.14) | `--el-border-color` | |
| `--border-strong` | rgba(255,255,255,0.24) | `--el-border-color-dark` | |
| `--bg` | #0B0F1A | `--el-bg-color-page` | |
| `--surface-1` | oklch(0.14 0.015 240) | `--el-bg-color`（也是 `--el-fill-color-lighter`） | |
| `--surface-2` | oklch(0.18 0.016 240) | `--el-bg-color-overlay`（也是 `--el-fill-color`） | |
| `--surface-3` | oklch(0.22 0.016 240) | `--el-fill-color-light` | |
| `--elev-1` / `--elev-2` | 内高光 / 内高光加投影 | `--el-box-shadow-light` / `--el-box-shadow` | 纠正 10.18 C「无对应项」 |
| `--radius-xs` 4 / `-sm` 6 | | `--el-border-radius-small` / `-base` | |
| `--radius-pill` 999 | | `--el-border-radius-round`、`-circle` | |
| `--radius-md` 8 / `-lg` 10 / `-xl` 12 | | 无对应项 | 用 Tailwind 任意值 `rounded-[8px]` 等，属 P-24 豁免 |
| `--ease-out`、`--dur-*` | cubic-bezier(0.16,1,0.3,1)；120/200/320/480 ms | 无对应项 | Tailwind 任意值，如 `ease-[cubic-bezier(0.16,1,0.3,1)]`、`duration-[120ms]` |

**转换规则（写样式时统一使用）**
- 带透明度的色值：`oklch(0.66 0.19 25 / 0.35)` 写成 `color-mix(in oklab, var(--el-color-danger) 35%, transparent)`。Tailwind 里空格写下划线，如 `border-[color-mix(in_oklab,var(--el-color-danger)_35%,transparent)]`。
- 原型里的 `#fff`（`.x-avatar`、`.x-btn.primary`）：用 `--el-color-white`。这是 Element Plus 内置变量，M1 在浏览器里实测确认存在。
- 顶栏背景：`color-mix(in oklab, var(--el-bg-color-page) 82%, transparent)`。
- 令牌是 `:root` 上的 `var()` 链。`[data-theme='light']` 重定义 `--bg`、`--surface-*`、`--fg-*`、`--border-*`、`--elev-*` 后，`--el-*` 会跟随，前提是 `data-theme` 挂在 `html` 上（`theme.ts` 已同步）。亮色效果未实测。

**B. `main.ts` 更正与补充**
- 页面导入方式不统一：较早的页面（Trace 四个、Overview、Nodes、Settings、Login、AppDetail）是顶部静态 `import`，较新的页面是 `() => import()`。新路由统一用懒加载。
- 现有 `/` 路由 name 是 `HomeRedirect`，重定向 `/stardust/overview`；框架首页 name 是 `home`。两者 name 不同，靠 path 同形态让位，M1 实测 `router.resolve('/')`。改法：把 `redirect` 改为 `/now`。
- `registerLayout` 现有 `stardust`，第二参 `true`。决策：旧布局改为 `false`（仅注册），`stardust2` 传 `true`，避免两个都为 true 时依赖注册顺序。
- P-28 仍有效：每次启动 `registerLayout(…, true)` 会覆盖用户在布局切换器里的选择。
- P-30 已解除：`git grep beforeEach` 在 `Stardust.Web.Vue/src` 与 `apps` 下只命中 `main.ts:177` 的一行注释，项目没有自定义路由守卫，该注释已过期，不处理。

**C. `kit.css` 里不能照抄的字面色值（已读 1-200 行）**
- `.x-envtag`：边框与底色用 oklch(0.66 0.19 25 / 0.35)、/ 0.10。
- `.x-nav em`：背景 oklch(0.66 0.19 25 / 0.18)。
- `.x-btn.danger`：边框 / 0.4，hover 底 / 0.12。
- `.x-tag.ok|warn|err|info`：各用带 / 0.28 与 / 0.10 的 oklch。
- `.x-avatar`、`.x-btn.primary`：`color: #fff`。
- `.x-proj`：背景里 SVG data-uri 的 `stroke='%23808898'`。改用 Lucide 的 chevron 图标加 `currentColor`。
- `::selection`：oklch(0.62 0.19 285 / 0.35)。
- 合规的写法：`.x-top.is-prod::after` 用 `var(--danger-500)`，换成 `--el-color-danger` 即可。
- 布局事实：顶栏背景为 `--bg` 82% 混透明；居中导航用 `position: absolute; left: 50%; transform: translateX(-50%)`；1180 px 以下导航改 `static`，900 px 以下隐藏环境徽标与计数。

**D. 问题记录更新**

| 编号 | 内容 | 状态 |
|---|---|---|
| P-30 | `main.ts` 注释里的 beforeEach 守卫 | 已解除：只有过期注释 |
| P-39 | 令牌数值对照 | 已核实，见 A |
| P-40 | 全局焦点环 | 补充：原型焦点是 `2px solid brand-500`、offset 2，项目全局是青色环加辉光，二者不同，仍待截图验证 |
| P-41 | 成功色在 `tokens.stardust.css` 与 `kit.css` 里是两套值 | 待核实：读 `colors_and_type.css`；P-05 不变，不改全局 |
| P-42 | `--sd-bg-input` 指向 `--gray-900`，不随主题变，亮色下输入框底可能偏黑 | 待亮色实测，必要时只改 `element-theme-override.css` 或令牌层 |

**E. 下一步**
1. 同步 `TODO.md` 第 25 行（问题范围 P-01~P-42，写入测试方案与 10.15 至 10.19 的结论）。
2. 建 `StardustLayout2` 第一批文件，再改 `main.ts`。
3. `pnpm run check`，停下请用户截图。

### 10.20 成功色核实（P-41 已核实，输入5-1026 续5）

依据：`Doc/Design/Stardust/design2/colors_and_type.css` 全文（292 行）。该文件是 README 指定的唯一权威。

| 项 | 权威 `colors_and_type.css` | 项目 `tokens.stardust.css` |
|---|---|---|
| `--success-500`（暗） | oklch(0.80 0.18 152)，约 #4BDE81 | oklch(0.72 0.14 155) |
| `--success-700`（暗） | oklch(0.50 0.12 152) | oklch(0.52 0.13 155) |
| `--success-500`（亮） | oklch(0.62 0.18 150)，约 #00A245 | 无亮色覆盖 |
| `--success-700`（亮） | oklch(0.48 0.15 150) | 无亮色覆盖 |

- `kit.css` 的 `.x-tag.ok` 用 oklch(0.80 0.18 152)，与权威一致。偏离的是项目令牌，不是原型内部不一致。
- 已逐项对过且一致的：品牌紫、青、警告、危险，暗色的 `--fg-*`、`--border-*`、`--surface-*`、`--bg`、`--elev-*`、`--gray-900`。
- 权威文件 `--brand-fg: #ffffff` 在暗色下也存在；项目令牌只在亮色块里有 `--brand-fg`。M1 不用它，用到时再核对。
- `git grep` 对 `Doc/Design` 返回空，推断该目录不被 git 跟踪，改它没有 git 回溯。本次不改设计稿。

**决策**
- M1 外壳不使用成功色，不动令牌。
- 进入 M3 前，单独修改 `tokens.stardust.css`：成功色三个值对齐权威，并在 `[data-theme='light']` 补成功色覆盖，然后 `pnpm run check`。旧布局未上线，不顾及旧页面。
- 这改变了 P-05（原"不改全局"）。默认按此执行，用户有异议再调整。

| 编号 | 内容 | 状态 |
|---|---|---|
| P-05 | 是否改全局 `--success-500` | 改判：M3 前对齐权威，默认执行 |
| P-41 | 成功色两套值 | 已核实，见上 |

### 10.21 原型顶栏真实结构与 Tailwind 配置核实（输入5-1026 续6）

依据：`kit-shell.jsx` 1-90 行、`kit.css` 1-200 行、`Stardust.Web.Vue/tailwind.config.js` 全文（96 行）、`src/styles/global.css`、`src/assets/brand/` 目录。

**A. 原型顶栏（TopBar，kit-shell.jsx）的真实结构**
- 从左到右：品牌（logo-mark 24 px、「星尘」、`STARDUST` 小字，点击回 `now`）、项目选择器（原生 `select`，选项为项目名）、居中分段导航、弹性占位、环境徽标、搜索按钮、主题按钮、头像。
- 导航 5 项：此刻 `now`、应用 `apps`、节点 `nodes`、发布 `ship`、治理 `mesh`。「应用工作台」（`route.page === 'app'`）下「应用」项也高亮。计数 `em` 只出现在「此刻」上，来源是告警数 `incCount`，大于 0 才显示。
- 环境徽标文案为「生产 · 仅生产」。搜索按钮文案「搜索 / 执行…」加 `⌘K` 键帽，1180 px 以下隐藏文案。主题按钮按当前主题显示 Sun 或 Moon。头像是文字「你」。
- 顶栏没有告警铃铛，也没有旧 TopBar 里的「3 告警」。旧 TopBar 的这两项不进新布局。
- 顶栏容器有 `is-prod` 类时，底边下方画 2 px 红色渐变护栏线。本项目环境固定生产，所以常驻。
- 项目选择器用原生 `select`，不用 `el-select`。数据沿用旧 TopBar 的 `/Platform/GalaxyProject` 加兜底三项，通过 `stores/stardustContext` 写回。

**B. Tailwind 配置（已读全文）**
- `content` 含 `./apps/**/*.{vue,ts,js}`，`apps/` 下页面的工具类能生成。`@tailwind base` 已开，有 preflight。`darkMode: ['class','dark']`。
- `theme.extend.colors` 的名字（`brand`、`bg-surface`、`text-primary`、`border-base` 等）全部指向 `--sd-*`，**不是 `--el-*`**，也有 `text-primary` 这类易与 EP 混淆的名字。决策：一律不用这些语义颜色类，颜色写任意值，如 `bg-[var(--el-bg-color)]`、`text-[var(--el-text-color-primary)]`、`border-[var(--el-border-color-light)]`（P-44）。
- `borderRadius`：`rounded-sm`=4、`rounded-base`=6、`rounded-md`=8、`rounded-lg`=10、`rounded-full`=999。原型的 `--radius-sm` 是 6，所以**原型的 6 px 要写 `rounded-base`，不能写 `rounded-sm`**；原型的 4 px 写 `rounded-sm`，8 写 `rounded-md`，10 写 `rounded-lg`，999 写 `rounded-full`。这些类经 `--sd-radius-*` 间接取值，数值与原型一致；`--el-*` 里没有 8、10 的对应项，所以采用这几个类，不再另写任意值（P-43，待确认）。
- `fontSize`：`text-xs` 至 `text-3xl` 被改写为 `var(--sd-font-*)`，数值未核对。决策：字号一律写任意值，如 `text-[12.5px]`、`text-[13px]`，不用 `text-xs`、`text-sm`。
- `boxShadow` 的 `shadow-1/2/3` 指向 `--sd-shadow-*`（即 `--elev-1`、`--elev-2`、`--shadow-lg`）。原型的 `--elev-1/2` 用 `shadow-1`、`shadow-2` 即可，等同于 `--el-box-shadow-light` 与 `--el-box-shadow`。
- `height.topbar`=`var(--sd-topbar-h)`=56 px，可用 `h-topbar`。间距未被覆盖，保持 Tailwind 默认的 4 px 阶。
- Logo 资源：`src/assets/brand/logo-mark.svg`（760 B），布局里用 `@/assets/brand/logo-mark.svg` 导入。

**C. 新增问题**

| 编号 | 内容 | 状态 |
|---|---|---|
| P-43 | 用 Tailwind 的 `rounded-md/lg/base/sm/full`，数值经 `--sd-radius-*` 取得 | 已被 P-54 更正：md、lg、full 的数值与原型不一致，8、10 改写任意值，属 P-24 豁免 |
| P-44 | Tailwind 语义颜色类指向 `--sd-*`，且与 EP 命名易混 | 已决：不使用，颜色写 `var(--el-*)` 任意值 |
| P-45 | 「此刻」导航的计数来源（告警数）尚无接口 | M1 先不显示，M3 接入告警数据时再补，缺口记 TODO |
| P-46 | 头像文字：原型是「你」，实际应取登录用户名首字 | M1 先取用户名首字，取不到时用「你」，不影响验收 |
| P-52 | Toast 多条同时可见时堆叠间距固定 16px（`notify.mjs` 的 `GAP_SIZE`，`bottom` 由 JS 写进内联样式），原型是 8px | 接受偏差：CSS 改不了；toast 只显示 3.4 秒，仅并发多条时可见。要逐像素一致只能自写 ToastHost，会推翻 10.10 C 的决定，待用户拍板 |
| P-53 | `ElNotification` 监听 document 的 keydown，按 Esc 会关掉可见的 toast，原型里 Esc 不影响 toast | 接受偏差：组件没有关闭该监听的开关，不改 `node_modules`；影响很小 |
| P-54 | 本节 B 的圆角数值有误：`tokens.css` 里 `--sd-radius-md`=12、`-lg`=16、`-full`=50%，不是 8、10、999 | 已核实：P-43「数值与原型一致」不成立。原型的 8、10 一律写任意值 `rounded-[8px]`、`rounded-[10px]`（P-24 豁免）；4 写 `rounded-sm`、6 写 `rounded-base` 数值无误。NavSegment、TopBar 已按此改过 |

> P-47 至 P-51 目前只记在 `.forgeself/memory/2026-10-26.md` 与 `TODO.md`，尚未同步到本表，编号保留不复用。

## 11. 发布页组件化与项目过滤（2026-10-28 追加；11.2 已于 2026-10-29 作废，见 11.4）

> 11.1 与 11.3 有效。11.2 是我当时对用户意图的转述错误（用户要的是新增页签，概览页只一览应用），已作废；前文「发布页签只复用 ShipPanel」的写法同样待 11.4 重划分，以 11.4 为准。用户原话见 `.forgeself/memory/2026-10-28.md` 末节。

### 11.1 发布页组件化

- `ShipPanel.vue` 现为约 945 行的单文件，取数与状态都在里面，只有纯逻辑在 `ship.logic.ts`、`useManualRefresh.ts`；整块依赖真实请求层，所以刷新按钮 loading 无法做 CT。
- 拆为五个只收 props、以 emit 上抛操作的展示组件：`DeployVersionBar`（当前生效版本、版本一致性、刷新）、`DeployVersionList`（版本列表与「应用此版本」）、`DeployPipelineRuns`（流水线运行）、`DeployNodes`（节点安装、启动、停止、重启、卸载）、`DeployHistory`（部署历史）。
- 统一取数放 `useDeployData(appId)`：换 `appId` 整体重取；应用版本后版本条、节点、历史一起刷新；操作后刷新由它负责，刷新按钮遵守 AGENTS.md §11。
- 理由：五个组件各自取数很难在「应用某版本」之后保持同步；展示组件只收 props 后可直接写 story 与 CT。
- `ShipPanel.vue` 退化为容器（调用 `useDeployData` 并分发 props），`/ship` 页继续可用。

### 11.2 应用概览页签并排（已作废，保留作误读记录；以 11.4 为准）

- 概览页签顶部放 `DeployVersionBar`，下方并排版本列表、流水线运行、节点、部署历史（宽屏两列网格，窄屏回单列，补 1180/1080/900 截图）。
- 原型概览页签的 KPI、错误率图、此应用变更列表本次不做，仍属停点 4 遗留。
- 「发布」页签与 `/ship` 的保留范围待决策，暂定：「发布」页签放流水线运行与触发，`/ship` 放应用下拉加整套组件。

### 11.3 项目过滤

- 后端不改：App、AppOnline、AppDeploy、AppTracer、Node、NodeOnline 的列表接口都读 `projectId`，默认 -1，不传即不过滤。
- 请求层：`stores/stardustContext.ts` 的全局拦截器已把 `projectId`、`environment` 合并进 cubeAxios 请求，不在页面里手拼。
- 页面层是缺口：`Console/` 下页面没有监听上下文变化。做法：新增共用组合函数，监听 `ready|projectId|environment` 拼成的 key（沿用旧 `AdminCenter.vue`），首轮取数等 `ready`，变化后重取；key 拼接抽成纯函数写 Vitest。接入 `Apps`、`Ship`、`App` 工作台与命令面板数据（`ensurePaletteData` 缓存随之失效）。
- 「全部项目」：下拉加一个哨兵项，`projectId` 为 null 即不过滤；`resolveProjects` 需允许它保持选中。改 `TopBar` 会动带基线的 CT，须用户看图确认。
- 未核实：`request` 是否包装 cubeAxios；`/Platform/GalaxyProject` 是否被注入的 projectId 过滤；environment 在后端是否参与过滤。见 `TODO2.md`。
- 发布页数据均按 `appId` 取，应用确定即隐含项目，项目过滤只影响应用下拉、应用列表、命令面板与节点列表入口。

### 11.4 应用页签重划分：概览看全局，一个页签做一类事（2026-10-29，页签方案已由用户确认，实施进度见 `TODO.md` 停点 19）

- 用户原话（逐字）见 `.forgeself/memory/2026-10-28.md` 末节「输入（2026-10-29 应用页签重划分…）」。要点：发布页拆成独立组件并用参数控制当前项目与应用（11.1 已做）；应用页面按功能增加页签，一个功能一个页签；现有「发布」页签无特定职责；概览页第一部分是当前版本，其后呈现应用当时状态与需关注的事，需要操作时点概览页的按钮跳到对应页签；每个页签职责分明。
- 页签方案（已确认）：概览、调用链、日志、配置、实例，加上由发布拆出的 版本（`DeployVersionList`）、节点（`DeployNodes`）、部署历史（`DeployHistory`）、流水线（`DeployPipelineRuns`），取消「发布」页签。
- 概览草案：第一块 `DeployVersionBar`；其后是需关注事项（版本不一致的启用节点数、最近流水线运行失败或长期停在构建中、最近部署下发失败、在线实例数），每项带跳转按钮，点击切到对应页签（路由 `/app/:tab/:id`）。只用已有接口，取不到显示「待接入」，不造数据。概览不放操作表格。
- 代码影响（已确认，单测先行，进行中）：`palette.items.ts` 的 `AppTab`、`workbench.logic.ts` 页签清单与 `tabLayout`（原 overview→grid 不再需要，页签映射改为页签到组件）、`App.vue`、`ShipPanel` 的 `layout` 参数是否保留。五个展示组件与 `useDeployData` 不用改，只换挂载位置。
- 已决（2026-10-29，答「按你推荐的来」）：实例与节点页签分开；页签数量 6 变 9；`/ship` 整页取消（见 11.5）。

### 11.5 /ship 去留与顶栏「发布」位重想（2026-10-29，用户已选顶栏「变更」，先落文档、暂不实现，代码未动）

原型 §4.4 的「发布」是选应用、发起发布、灰度停在 25% 等人确认的动作页；后端没有灰度状态接口，这部分一直是「待接入」。现有 `ShipPanel` 已改成版本、节点、历史、流水线四件事放在一个应用下拉后面，与 11.4「一个页签做一类事」冲突，且占着顶栏一级入口只服务单个应用。结论：`/ship` 整页取消，五个 `Deploy*` 组件搬进 11.4 的应用页签；`/ship` 临时重定向到新入口。

组件层面：`ShipPanel` 已是容器加五个展示组件，不再为拆而拆；容器的 `layout` 参数换成 `section`（versions / nodes / history / pipeline），一个页签渲染一块；`useDeployData` 按页签取切片，版本页签需要节点数据（版本提示条），节点页签需要部署集版本（一致性标签），边界读代码后再定。

顶栏：事件驱动与最短路径在原型里由「此刻」（§4.1）承担，不另开「待办」入口。「发布」位推荐换成「变更」：跨应用时间线，行是 D 发布 / C 配置，点行直达应用对应页签并定位；「此刻」右栏「最近变更」取该页前 5 条。风险：`AppDeployHistory`、`AppPipelineRun` 能否不带 appId、pipelineId 按项目取，配置变更来源接口，均未核实；退路是只汇总当前项目前 N 个应用，或顶栏只留四项。

已决：顶栏换「变更」（用户 2026-10-29 选定）。待决：`/ship` 取消后重定向去哪；最高频旅程（发完版本确认结果、告警后定位原因、日常看应用状态、改配置并发布，为推断，非用量数据）哪条优先。详见 `TODO.md` 停点 20。