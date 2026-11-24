# NewLife/StarServer 应用包

StarServer 是发布中心的一个应用部署集（部署集名 `StarServer`，项目 `NewLife`），不是独立的包表。

## 前置

- 已部署 StarServer，并在「发布中心 → 应用部署」创建部署集：
  - 项目：`NewLife`
  - 名称：`StarServer`
  - 启动文件：`StarServer.dll`
- 目标节点已安装 StarAgent，且 `StarAgentSetting.Server` 指向该 StarServer。

## 打包

```powershell
dotnet publish Stardust.Server/Stardust.Server.csproj -c Release -o publish/starserver

# 可选：把示例清单放到包根目录；不放置时服务端会根据部署集生成清单
Copy-Item Samples/Packages/newlife-starserver/star.json publish/starserver/star.json

Compress-Archive -Path publish/starserver/* -DestinationPath NewLife-StarServer.zip -Force
```

## 上传

1. 打开星尘控制台 → 发布中心 → 应用部署 → `NewLife/StarServer`。
2. 进入版本列表，点「上传版本」，选择 `NewLife-StarServer.zip`。
3. 包内有 `star.json` 时自动填充版本、平台和启动参数；没有时使用部署集上已有的启动参数。

## 运行

```bash
star pull NewLife/StarServer
star run -d NewLife/StarServer
```

前台调试（Ctrl+C 停止）：

```bash
star run NewLife/StarServer
```

## 验证

```bash
star ls
star info NewLife/StarServer
```

- `star ls` 应显示 `NewLife/StarServer`，版本与部署中心最新版本一致。
- 服务端监听 `http://节点IP:6600`。
- `star stop NewLife/StarServer` 停止；`star start NewLife/StarServer` 重新启动。
