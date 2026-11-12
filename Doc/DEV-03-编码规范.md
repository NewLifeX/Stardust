# 编码规范 — 星尘（Stardust）

> 本文件从 AGENTS.md §4.2 拆出，记录前后端编码规范。

## 前端（Stardust.Web.Vue / cube-vue）

### 组件拆分
每个面板/功能区独立 `.vue` 文件，不允许大文件堆多面板逻辑。

### 样式
- 使用 Element Plus 组件 + Tailwind 布局类，颜色只走 `--el-*` 变量
- **禁止**新增 `--app-*` 等平行 token
- **禁止**硬编码 `bg-red-500` 之类默认调色板

### 关注点分离
- `index.vue` 的 `<script setup>` ≤ 50 行
- 业务逻辑抽成 `*.logic.ts` 纯函数
- 模板与脚本不得写业务逻辑（`new Date()`、`fetch()`、复杂 `filter/map` 等）

### 页面定制
统一用 `CubeTable` 具名插槽；`useSections` / `SectionKeyMap` 覆盖机制为遗留、禁止新增。

### Element Plus 组件导入
**禁止显式 `import { ElXxx } from 'element-plus'`**（type 导入如 `FormInstance`/`FormRules` 除外）：组件样式依赖按需注入，显式导入绕过自动解析导致组件无样式（已验证）。统一在模板中使用 `<ElXxx>` 由插件自动解析。

### cube-vue 组件导入
逐文件导入（见 `Doc/DEV-02-踩坑速记.md`），禁止 `core/components` 目录导入。

### 类型
所有新代码必须有 TypeScript 类型，不用 `any`。

### 技能调用
新增页面/布局/微应用/LOV/页面覆写前，先调用对应 cube-vue 技能：`cube-init`/`cube-layout`/`cube-add-page`/`cube-add-app`/`cube-lov`/`cube-page-override`（固化了红线：页面只用自身 class + `--el-*`；布局用 Tailwind 工具类；`usePageApi` 真实路径；LOV 的 `requestUrl` 以 `/` 开头须 `proxyRequest=false`）。

### 测试（TDD 优先）
新增逻辑先写 vitest 测试定义预期行为，再写实现使测试通过；修改逻辑先补/改测试覆盖新行为，再改实现。

---

## 后端（.NET）

### 分层
遵循现有分层：实体（`Stardust.Data`，XCode ORM）→ Services → Controllers（NewLife.Cube Controller-First CRUD）。

### 软链接复用
`Stardust.Web` 的 csproj 通过 `<Compile Include>` 软链接复用 `Stardust.Server` 的 `Services/*` 与 `Setting.cs`，改共享业务逻辑时注意两处生效。

### 异步
异步方法统一 `async/await`，不 `.Result` 阻塞。

### 测试（TDD 优先）
新增/修改业务逻辑先写 xUnit 测试，再写实现。

### 数据库变更
走 XCode 自动迁移，注意可回滚。

变更怎么拆、什么可以提交：`Doc/DEV-08-变更原则.md`。
前端审查与提交用同一套验收：`Doc/DEV-09-前端验收标准.md`。
