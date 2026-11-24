# PUB-19 应用包规范

> 版本：v1.2 | 日期：2026-11-24
> 状态：规范已落地；2026-11-24 起应用包并入发布中心，完整设计与实现见 [PUB-25-应用包管理](PUB-25-应用包管理.md)

---

## 结论

应用包 = 发布中心的一个应用部署集 + 一个可下载的 zip；一个包只含一个应用，包名即 `项目名/应用名`。只做企业内部包管理，不面向公共市场。

- `star` 是 StarAgent 的别名命令：Windows 指向 `StarAgent.exe`，其它系统指向 `StarAgent` 或 `dotnet StarAgent.dll`。
- 包源是本机 StarAgent 连接的 StarServer；服务端直接从 `AppDeploy` / `AppDeployVersion` 返回包信息。
- 有 `-d` 交给 StarAgent 守护；无 `-d` 前台运行，Ctrl+C 停止。
- zip 根目录的 `star.json` 可选：有则校验并填充部署集运行参数；没有则由服务端按部署集生成清单。

## 包格式

```json
{
  "schema": 1,
  "name": "NewLife/StarServer",
  "version": "4.0.0",
  "displayName": "星尘服务端",
  "apps": [
    {
      "name": "starserver",
      "fileName": "StarServer.dll",
      "arguments": "--urls http://*:6600",
      "ports": [ 6600 ],
      "main": true
    }
  ]
}
```

| 字段 | 必填 | 说明 |
|---|:---:|---|
| `schema` | ✅ | 当前为 1 |
| `name` | ✅ | `项目名/应用名`，与 `AppDeploy.FullName` 一致，大小写不敏感 |
| `version` | ✅ | 版本号；上传时同步写入 `AppDeployVersion.Version` |
| `os` / `arch` | | 目标平台，0/空为通用 |
| `apps` | ✅ | **必须恰好一个应用**；多应用拆成多个包，每个包对应一个部署集 |

服务端上传校验：包名与部署集全名（或应用短名）一致；应用数量必须为 1；路径必须为相对路径且不含 `..`；`main` 最多一个；端口 1~65535。

## CLI 命令

| 命令 | 说明 |
|---|---|
| `star <包名>` | 前台运行。需要守护时用 `star run -d` |
| `star run <包名>[:版本] [-d] [参数]` | 运行；`-d` 守护 |
| `star pull <包名>[:版本]` | 只下载 |
| `star ls` | 已安装包 |
| `star info <包名>` | 包详情 |
| `star search [关键字]` | 搜索服务端包 |
| `star start/stop/restart <包名>` | 启停守护应用 |
| `star rm <包名>[:版本]` | 停止并删除 |
| `star run -f <zip>` | 运行本地包；本地 zip 仍要求根目录 `star.json` |

示例：

```bash
star run -d NewLife/StarServer   # 交给代理守护 StarServer
star run -d NewLife/StarWeb      # 交给代理守护 StarWeb
star run -d redis -port 6379     # 交给代理守护 Redis
star mysql/mysql                 # 前台运行 MySQL
```

## 详细设计

包格式、数据模型、API、控制台、CLI、升级回滚、安全与测试见 [PUB-25-应用包管理](PUB-25-应用包管理.md)。