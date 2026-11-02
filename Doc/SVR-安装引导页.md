# SVR-安装引导页

> 版本：v1.0 | 日期：2026-11-02
> 需求对应：[功能清单](功能清单.md) NODE-29

## 1. 目标

StarServer 根路径 `/` 是**平台门户**：上方展示节点/应用在线情况与今日调用统计，下方是 StarAgent 安装引导。
因为“安装目标地址”就是 StarServer 自身，页面与安装参数天然一致，只部署服务端的环境也能安装。

页面只读、匿名可访问；包的上传与管理仍在 StarWeb 后台完成。

## 2. 架构

```
浏览器 / 运维 ──► StarServer :6600
                    GET /                          安装引导页（内嵌 HTML，匿名）
                    GET /install?format=json       安装清单（匿名，缓存 60s）
                    GET /install.sh                Linux 安装脚本（动态生成）
                    GET /install.ps1               Windows 安装脚本（动态生成）
                    GET /install/download/{kind}/{id}  302 到实际地址（现场签发令牌）
                    GET /cube/file?id=             实际文件（既有链路）
                            │ 读
                    ProductRelease / ProductPackage / DotNetPackage（StarWeb 后台维护）
                            │ 缺包兜底
                    AgentDownloadBaseUrl（公共镜像 x.newlifex.com/star）
```

分层保持两层：`InstallController` 负责取参/响应；`InstallService` 负责清单组装、脚本渲染与下载地址解析；数据仍由 XCode 实体承载，不新增 Repository。

## 3. 接口

| 接口 | 说明 |
|------|------|
| `GET /` | 安装引导页。`Accept: application/json` 或 `?format=json` 时返回清单 |
| `GET /home/summary` | 平台概览：在线/总节点、在线/总应用、今日与昨日调用、错误率、版本、运行时长、近7天趋势 |
| `GET /install` | 安装清单 JSON：`server`、`versions[]`、`runtimes[]`、`mirror[]` |
| `GET /install.sh?os=linux&arch=x64&runtime=10&version=` | bash 脚本，含服务端地址、包地址与校验 |
| `GET /install.ps1?os=windows&arch=x64` | PowerShell 脚本 |
| `GET /install/download/{kind}/{id}` | `kind` 取 `agent`/`runtime`，只放行启用版本下的安装包，现场签发短时令牌后 302 |

**清单中的下载地址**：本站包是相对地址 `/install/download/...`，客户端按 `server` 补全；公共镜像包是绝对地址。

## 4. 数据来源与兜底

| 来源 | 内容 |
|------|------|
| `ProductRelease`（ProductCode=StarAgent、Enable、Channel） | 发布版本，新到旧 |
| `ProductPackage`（ReleaseId、OSKind、CpuArch、TargetRuntime） | 各平台安装包；`TargetRuntime` 为 `go`/`rust`/`cpp` 时归入原生 Agent |
| `DotNetPackage`（Kind=runtime） | .NET 运行时安装包；页面每个系统架构只保留最近 2 个主版本，避免列表过长 |
| `AgentDownloadBaseUrl` | 公共镜像兜底，仅在服务端未维护对应包时使用 |

清单缓存 60 秒（`ICacheProvider.Cache`），因此后台发布后最迟约一分钟可见。

**不区分升级通道**：页面与清单只按 `Enable` 判定是否上架，发布版本不分 Release/Beta，避免用户面对通道选择；
需要下线的版本在后台停用即可。

**同一套选包逻辑也服务于引导安装**：`POST /node/bootstrap` 由 `NodeService.GetBootstrap` 转调 `InstallService.GetBootstrap`，
与安装页共用版本优先级、系统/架构匹配与运行时就近匹配规则，不再各写一份。

## 5. 安装脚本

脚本由模板 `install-linux.tpl` / `install-windows.tpl` 参数化生成：

1. 缺少 dotnet 时先装运行时（tar.gz 解压或 exe 静默安装）
2. 下载 StarAgent 包，MD5 校验（32 位十六进制时才校验）
3. 解压到 `/opt/staragent` 或 `C:\Program Files\StarAgent`
4. 执行 `dotnet StarAgent.dll -install -server <服务端地址>`

脚本内的下载地址一律是 `/install/download/...`，执行时才换令牌，所以页面打开很久后再跑脚本也不会失效。

## 6. 配置项（StarServer）

| 配置 | 默认 | 说明 |
|------|------|------|
| `AgentServerUrl` | 空 | 下发给客户端的服务端地址；为空时取请求地址。反代/端口映射时必须显式配置 |
| `EnableInstallPage` | true | 关闭后根路径恢复旧行为（跳转 StarWeb `/Agent`） |
| `HomeSummaryMode` | Magnitude | 首页概览展示模式：Hidden 不展示；Magnitude 只展示数量级（1,000+ / 1,287 万+）；Exact 展示真实数字 |
| `AgentDownloadBaseUrl` | `https://x.newlifex.com/star/` | 公共镜像地址 |

## 7. 上传管理

- 完整版 StarAgent：StarWeb → 节点管理 → 产品版本 → 产品发布包，按 OS/架构/运行时上传；上传后自动回填文件大小、MD5 与相对下载地址（`/cube/file?id=...`）。
- 原生 Agent：同一张发布包表，`TargetRuntime` 填 `go`/`rust`/`cpp`。
- .NET 运行时：`DotNetSyncService` 自动同步官方 releases.json；离线环境手工上传。
- 公共镜像：发布到 `x.newlifex.com/star`，作为兜底。

## 8. 安全

- 页面与清单匿名：引导安装发生在节点注册之前，没有令牌可用；只暴露公开的安装包元数据。
- **规模数据默认量级化**：`HomeSummaryMode=Magnitude` 时只展示 `1,200+`、`1,287 万+` 这类数量级，配合同比趋势与错误率，足以体现平台在跑而不泄露精确运营数据；公网部署建议保持默认，内网可改 `Exact`，完全不想展示则设 `Hidden`（服务端也不会读取与返回统计）。
- **趋势曲线只给形状**：近 7 天调用量在服务端按峰值归一化到 0~100 后下发，页面只能画出走势，拿不到任何一天的绝对值。
- **概览缓存 30 秒**：公开页面刷新频率高，缓存既降低数据库压力，也避免访客通过连续刷新反推实时数据。
- 下载入口只放行"启用版本下的安装包编号"，不开放附件表任意下载。
- 令牌短时（`AttachmentTokenExpire`，默认 600 秒）且不落库，每次访问现场签发。
- 脚本对 `server` 参数做字符校验，防止注入。

## 9. 测试

| 用例 | 覆盖 |
|------|------|
| `InstallServiceTests` | 清单组装、通道过滤、镜像兜底、令牌签发、停用版本拒绝、脚本内容、非法地址拒绝、页面资源可读 |
| `InstallServiceBootstrapTests` | 引导安装：原生返回组包、.NET 版返回空、缺包返回空、精确系统架构优先 |
| `InstallEndpointTests` | `/` 返回页面、`/install` 清单、`/home/summary` 概览（含 7 天趋势）、`/install.sh` 可执行、下载 302 携带令牌、未知包 404 |

## 10. 与 StarWeb 的关系

- StarWeb `/Agent` 仍是旧页面（清单硬编码），**配置 `AgentServerUrl` 后自动 302 到 StarServer 安装页**，不再维护两份清单；
  未配置时保留原页面，避免旧部署突然失效。
- `/Agent?format=json` 与 `Accept: application/json` 保持旧接口不变，兼容存量客户端。
- `Stardust.Web.Vue` 下的 `views/Agent/index.vue` 未注册路由，属孤立页面，本期不动。

## 11. 后续

- 页面若需要在 StarWeb 域名下直接展示（而不是跳转），可把 `install.html` 作为静态资源同样挂过去。

## 12. 本地调试

Visual Studio 默认启动地址为 `http://localhost:6600`（`Properties/launchSettings.json`），直接打开就是安装页。
若地址后面带 `/api` 路径，说明仍是旧的调试配置，重新加载解决方案即可。
