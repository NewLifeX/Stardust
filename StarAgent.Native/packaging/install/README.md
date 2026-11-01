# 部署模板（原生 Agent 前台进程）

原生 Agent（C++/Go/Rust）只提供前台常驻进程，**不注册系统服务**、不做进程守护。重启策略、开机自启、日志重定向由这里的模板交给操作系统托管。

| 文件 | 平台 | 用法 |
|------|------|------|
| `staragent.service` | Linux（systemd） | 复制到 `/etc/systemd/system/staragent.service` → `systemctl daemon-reload && systemctl enable --now staragent` |
| `staragent.init` | OpenWrt（procd） | 复制到 `/etc/init.d/staragent`（`chmod +x`）→ `/etc/init.d/staragent enable && /etc/init.d/staragent start` |
| `staragent-task.xml` | Windows（任务计划） | `schtasks /Create /TN StarAgentNative /XML staragent-task.xml /F` |
| `staragent.plist`（参考） | macOS（launchd） | 写入 `/Library/LaunchDaemons/com.newlifex.staragent.plist` → `launchctl load -w` |

约定：

- 安装目录默认 `/opt/staragent`（Linux）/ `C:\Program Files\StarAgent`（Windows），配置文件默认 `/etc/staragent/staragent.json`（Linux）/ `C:\ProgramData\StarAgent\staragent.json`（Windows）。
- 未创建配置文件时，`run` 需要 `--server` 才能启动；推荐先用 `once --server <url> --config <path>` 完成首次注册并落盘配置，再交给守护进程托管。
- 需要完整能力（自身升级、远程发布、插件等）时安装 .NET 版 StarAgent；原生 Agent 可通过 `node/bootstrap` 命令引导安装（见 `../spec/protocol.md`）。
