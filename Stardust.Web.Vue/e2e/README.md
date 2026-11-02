# Stardust.Web.Vue 端到端测试（Playwright）

基于 `@playwright/test` 的确定性 e2e 回归套件，**真实对接前端 5188 + 后端 6681（SQLite），绝不 mock**。

## 基础设施约定（务必遵守）

| 项 | 取值 | 说明 |
|---|---|---|
| 前端 dev server | `http://localhost:5188`（Vite） | `playwright.config.ts` 的 `webServer` 会自动 `pnpm dev` 拉起；已是 `reuseExistingServer: true`，手动启动亦可 |
| 后端 API | `http://localhost:6681`（StarWebApi） | .env.development 的 `VITE_PROXY_TARGET` 同源代理转发 |
| 登录态 | `e2e/.auth/user.json` | 由 `auth.setup.ts` 以 `admin/admin` 真实 UI 登录生成，被 `chromium` 项目依赖复用 |
| 运行入口 | `./e2e/fixtures.ts` | 统一处理本地/全局 playwright 回退，所有用例从这里 `import { test, expect }`，**不要再写独立 .mjs 脚本** |

## 标准运行流程

```powershell
cd X\Stardust\Stardust.Web.Vue
$env:PLAYWRIGHT_BROWSERS_PATH=0          # 复用已下载的 chromium
pnpm exec playwright test --project=chromium --reporter=line
# 仅跑单个文件
pnpm exec playwright test e2e/CrudPlatformGalaxyProject.spec.ts --project=chromium --reporter=line
```

- **后端必须已启动**（6681）。前端可由 webServer 自动拉起，但后端不行——若 6681 未监听，先手动启动：
  ```powershell
  cd X\Stardust\Bin\WebApi           # 注意：是 X/Stardust/Bin/WebApi，不是 WebApi/Bin/WebApi
  dotnet StarWebApi.dll --urls http://localhost:6681
  ```
  > 坑：WebApi 的 `OutputPath` 指向 `..\Bin\WebApi`（即 `X/Stardust/Bin/WebApi`）。若启动报 `Could not load type 'System.TimeProvider'`，说明跑到了陈旧程序集，需用 `dotnet build -c Debug` 重建后再跑上面的命令。

## 用例组织

- `e2e/*.spec.ts`：迁移验证与 CRUD 闭环用例。
- `e2e/helpers.ts`：`gotoLeafPage` / `assertPageRendered` / `collectErrors` / `getRowCount` —— 进入 history 路由页、断言渲染、采集 >=400 接口错误。
- `e2e/fixtures.ts`：统一 playwright 加载与登录态依赖。
- 命名规范：`Crud<Area><Controller>.spec.ts` 表示带真实 CRUD 闭环；`<Area><Controller>.spec.ts` 表示纯渲染/对接验证。

## CRUD 用例范式（新增功能测试请照此写）

1. 用 `gotoLeafPage(page, '<Area>/<Controller>')` 进页（处理菜单异步注册）。
2. `await assertPageRendered(page)` 断言渲染类型。
3. 新建：点击 `getByRole('button',{name:'新建'})`（AutoPage）或自定义视图的顶部按钮（如「新建项目」），填表单，提交。
4. 回列表后轮询新行是否出现（最多 ~8s），再做断言。
5. **清理**：新建数据一律 `E2E_` + 时间戳 命名，验证后真实 Delete 删除，避免污染。
6. 用 `collectErrors` 断言无 >=400 接口错误。

## 当前覆盖情况（2026-08-21）

- 已存在 70+ 迁移验证/CRUD 用例（`CrudMonitors`、`CrudNodes`、`CrudPlatformRedisRegistry`、`Deployment*`、`Registry*` 等）。
- 本次新增：`CrudPlatformGalaxyProject.spec.ts`（新建项目 UI 闭环 + 新建按钮可见）。
- 后端 69 控制器中仍有 29 个前端未对接（详见 `X/Stardust/Doc/待迁移Api.md`「前端对接追踪」），相应页面缺失，暂无对应用例。
