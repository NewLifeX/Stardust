# NODE-移动端代理（Android / iOS）

> 定位：StarAgent 在移动端的简化实现。复用 StarServer 的 `Node/*` 协议，按平台能力裁剪功能。

## 1. 能力边界

| 能力 | Android | iOS |
|------|---------|-----|
| 注册 / 登录 / 心跳 | ✅ 前台服务常驻 | ⚠️ 前台 + BGTask 唤醒时上报 |
| CPU / 内存 / 存储 / 电量 / 温度 | ✅ | ✅（系统级可用内存受限） |
| 网络上下行 / WiFi 信号 | ✅ | ⚠️ 仅应用级 |
| 日志上报 | ✅ | ✅ |
| `node/restart` | ✅ 重启应用 | ✅ 退出后由用户再次打开 |
| `node/setchannel` | ✅ | ✅ |
| `node/synctime` / `bash` / `cmd` / `node/reboot` | ❌ 返回不支持 | ❌ 返回不支持 |
| 进程守护 / 远程发布 / Web 面板 | ❌ | ❌ |

默认 `ProductCode=StarAgent-Mobile`，平台由 `OSKind` 区分。登录时通过 `NodeInfo.Plugins` 上报能力标签：

```text
agent:dotnet-mobile, cap:exec=false, cap:upgrade=false, cap:deploy=false, os:android | os:ios
```

## 2. 结构

```text
StarAgent.Mobile/     共享核心（net10.0）
├── MobileAgent       登录、心跳、命令分发、配置与日志目录
├── MobileStarClient  登录/心跳报文与能力标签
├── IMobilePlatform   平台适配接口
└── MobileAgentSetting

StarAgent.Android/    net10.0-android，前台服务 + 控制台界面
StarAgent.iOS/        net10.0-ios，场景式 App + BGTask + 控制台界面
```

命令随心跳响应下发（`PingResponse.Commands`），移动端不需要建立 WebSocket 长连接。

## 3. 构建与产物

```powershell
# Android：打包 APK（默认调试签名）
pwsh StarAgent.Android/pack-android.ps1 -Mode Aot
pwsh StarAgent.Android/pack-android.ps1 -Mode Jit

# Android：安装到设备并冒烟
pwsh StarAgent.Android/run-android.ps1 -Smoke
```

```bash
# iOS：macOS 上构建模拟器包并冒烟
bash StarAgent.iOS/run-ios.sh
```

产物统一输出：

```text
Bin/Agent/mobile/android/
├── com.newlife.staragent-Signed.apk
├── apk/StarAgent-Aot.apk
└── artifacts/android-*.log

Bin/Agent/mobile/ios/
├── *.app
└── artifacts/ios-smoke.json
```

## 4. 服务端约定

- 新注册的移动节点默认 `AlarmOnOffline=false`，避免 iOS 间歇在线产生告警噪音。
- `OSKinds` 已增加 `iOS=501`；移动平台不参与 RID 升级包匹配。
- 移动端不执行自升级，收到升级响应只记录日志。

## 5. 测试

- `Stardust.ServerTests/Communication/MobileAgentCommunicationTests.cs`：真实 StarServer + `MobileStarClient`，覆盖 Android/iOS 登录、自动注册、`ProductCode`、`OSKind`、下线告警默认值与心跳。
- `Stardust.Tests/MobileAgentTests.cs`：登录/心跳报文、iOS 识别与移动平台 RID 映射。
- Android/iOS 冒烟脚本：`run-android.ps1 -Smoke`、`run-ios.sh`，输出 `=== Agent ready ===` / `agent-smoke.json`。

## 6. 已知限制

1. iOS 无法常驻，节点在两次唤醒之间显示离线；后台任务由系统调度。
2. Android 保活受厂商策略影响，需要引导用户加入电池白名单。
3. iOS 系统级可用内存不可读，使用"物理内存 - 当前进程工作集"近似。
4. iOS 真机安装需要 Apple 开发者证书与描述文件；Windows 只能完成托管编译。
