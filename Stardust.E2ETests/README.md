# StarWeb 端到端测试（Stardust.E2ETests）

基于 xUnit + Microsoft.Playwright 的 StarWeb（魔方 MVC 管理后台）全站冒烟测试。

## 覆盖内容

| 用例 | 说明 |
|---|---|
| 登录后进入管理后台 | 真实 UI 以 admin 登录，验证跳转与管理后台标题 |
| 未登录访问业务页面被重定向到登录页 | 无会话上下文访问 `/Configs/AppConfig`，应跳转登录或被 401/403 拒绝 |
| 全部菜单页面可正常访问 | 从 `Membership` 库读取全部可见菜单页面，逐页访问并断言：HTTP 200、无服务端错误标记、无 JS 未捕获异常、非空白页；失败自动截图 |

## 运行前提

1. 先构建站点：`dotnet build 星尘.slnx -c Release`（夹具使用 `Bin/Web` 产物）
2. 首次使用 Playwright 需安装浏览器：`pwsh Stardust.E2ETests/bin/Release/net10.0/playwright.ps1 install chromium`
   （或全局已安装到 `%LOCALAPPDATA%\ms-playwright`）

## 运行

```powershell
dotnet test Stardust.E2ETests/Stardust.E2ETests.csproj -c Release
```

默认行为：把自己的 `Bin/Web` 复制到系统临时目录，改写 `appsettings.json` 连接到临时 SQLite 库
（`Stardust/StardustData/Membership`），用随机端口启动真实 Kestrel 站点，测试结束后清理。
**不会读写 `Bin/Data` 下的开发数据库。**

接入已运行的站点（例如调试中实例）：

```powershell
$env:STARWEB_BASE_URL = "http://127.0.0.1:6680"
$env:STARWEB_MEMBERSHIP_DB = "C:\X\Stardust\Bin\Data\Membership.db"
$env:STARWEB_USER = "admin"
$env:STARWEB_PASS = "admin"
dotnet test Stardust.E2ETests/Stardust.E2ETests.csproj -c Release
```

## 输出

- `TestResults/starweb-pages.md`：逐页巡检报告（状态、耗时、JS 错误数、标题、结果）
- `TestResults/starweb-screenshots/*.png`：失败页面截图

## 已知问题

- `/Cube/Area`（区域地图）：百度地图 AK 未授权（`getscript` 返回 `unauth=1`），ECharts bmap 抛
  `r.getAxesOnZeroOf is not a function`。测试按"已知外部问题"告警，不计入回归失败；更新有效 AK 后可移除豁免。

## 注意

本工程**未加入** `星尘.slnx`，因此不会随 `dotnet test 星尘.slnx` 在 CI 中执行（CI 无 Chrome 浏览器与站点启动条件）。
