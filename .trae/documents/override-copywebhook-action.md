# 重载列表操作渲染实现"复制WebHook"功能

## 摘要

通过重载 Cube 框架的 `_List_Data_Action_Adv.cshtml` 分部视图，为 AppPipeline 列表页实现"复制WebHook"按钮：点击按钮 → 发起 AJAX 请求 → 解析返回 JSON 的 `data` 字段 → 将 URL 写入剪贴板。

## 当前状态分析

### 现有代码

- `AppPipelineController.cs` 的静态构造函数中配置了 `CopyWebhook` 和 `Trigger` 两个 `ListField`，均使用 `DataAction="action"`
- `CopyWebhook` action 方法已实现：查询实体 → 拼接 `StarSetting.Current.ServiceAddress` + 路径 → `return Json(0, "ok", url)`
- 当前 `DataAction="action"` 依赖 Cube.js 的 `onActionComplete` 机制处理 AJAX 响应，但 Cube.js 只做 toast 提示，不支持写入剪贴板

### 框架渲染机制

- `_List_Data.cshtml`（默认 Cube 内置视图）渲染表格，每行调用 `_List_Data_Action.cshtml`（处理 CRUD）+ `_List_Data_Action_Adv.cshtml`（处理 ListField 高级操作）
- `_List_Data_Action_Adv.cshtml` 遍历 `ViewBag.Fields` 中的 `ListField`，对有 `Url` 或 `DataAction` 的字段渲染为操作项
- 在 `Areas/Deployment/Views/AppPipeline/` 下创建同名文件即可**覆盖**默认实现，不影响其他控制器

## 变更方案

### 1. 创建 `_List_Data_Action_Adv.cshtml`（重载）

**文件**：`Areas/Deployment/Views/AppPipeline/_List_Data_Action_Adv.cshtml`

**逻辑**：
- 从 `ViewBag.Fields` 获取 `FieldCollection`，遍历找出 `ListField`
- 对于 `CopyWebhook` 字段（字段名匹配）：
  - 渲染 `<a>` 链接，`href` 使用 `df.GetUrl(entity, page)` 获取带实体 ID 的 URL
  - `onclick` 调用 JS：`fetch(url).then(r=>r.json()).then(d=>navigator.clipboard.writeText(d.data))` → `return false` 阻止导航
  - 显示名使用 `df.GetDisplayName(entity, page) ?? df.Header`
- 对于 `Trigger` 及其他字段：
  - 保持标准渲染：`<a href="url" data-action="action">`，让 Cube.js 处理
- 保留权限判断（`this.Has(PermissionFlags.Detail)`）

### 2. 控制器不变

- `CopyWebhook` action 已返回 `Json(0, "ok", url)`，其中 `data` 字段包含 webhook URL
- `DataAction="action"` 和 `Url` 配置不变，但 `_List_Data_Action_Adv.cshtml` 中的自定义渲染会接管点击行为

## 假设与决策

| 决策 | 说明 |
|------|------|
| 重载 `_List_Data_Action_Adv.cshtml` | 这是 Cube 框架为 ListField 操作提供的专用渲染视图，覆盖它是最正轨的方式 |
| 使用 `fetch` + `navigator.clipboard` | 原生浏览器 API，无需引入 jQuery，支持 HTTPS 环境下的剪贴板写入 |
| `return false` 阻止默认行为 | 防止 `href` 的 URL 被浏览器导航，确保 JS 完全控制点击行为 |

## 验证步骤

1. `dotnet build Stardust.Web\Stardust.Web.csproj` 编译通过
2. 运行 Web 项目，打开流水线列表页
3. 点击"复制WebHook"，确认 AJAX 请求正常返回，URL 写入剪贴板
4. 点击"启动"，确认标准 `data-action="action"` 行为正常