# Deploy 客户端上传重试与 401 重新认证 Spec

## Why
DeployAgent 上传编译产物包到 `/Deploy/UploadBuildFile` 时，偶发 `Connection reset by peer`。根因：StarClient 持有的 JWT 令牌过期或失效时，服务端 `[ApiFilter]` 返回 401 并立即关闭连接；而客户端使用 `StreamContent` 流式上传，body 还在传输中，连接被服务端关闭后写入失败，表现为 `System.Net.Sockets.SocketException (104): Connection reset by peer`。

目前 `UploadPackageAsync` 没有任何重试机制，一次失败就整个编译任务失败，用户体验差且浪费时间。需要增加失败重试和 401 重新认证的能力。

## What Changes
- 修改 `DeployAgent/DeployService.cs` 的 `UploadPackageAsync` 方法，将单次上传改造为"带重试 + 401 重认证"的上传循环
- 抽取核心 HTTP POST 逻辑为独立方法 `PostUploadAsync`，支持重试复用
- 新增 `EnsureLoginAsync` 方法，在检测到 401 时主动调用 `_client.Login("上传认证失效重试")` 刷新 token
- 重试次数固定为 3 次，每次失败后延迟递增（1s → 2s → 4s）
- 401 触发重新登录后立即重试，不消耗重试次数配额
- 每次重试都打印清晰的日志（第几次重试、失败原因、等待时间）

## Impact
- Affected specs: 无（新能力，不修改既有 spec）
- Affected code:
  - `DeployAgent/DeployService.cs` — `UploadPackageAsync` 方法重构
  - `Stardust.ServerTests/Controllers/DeployControllerTests.cs` — 新增重试场景单元测试
- 受影响流程：DeployAgent 编译完成后的上传阶段
- 不受影响：服务端 `DeployController.UploadBuildFile`、`Attachment.SaveFile`、Web 端 `PipelineService`

## ADDED Requirements

### Requirement: 上传失败重试
UploadPackageAsync SHALL 在上传失败时自动重试，最多重试 3 次。

#### Scenario: 首次上传成功
- **WHEN** DeployAgent 调用 UploadPackageAsync 上传编译产物包
- **AND** 服务端返回 HTTP 200 且响应体 `code == 0`
- **THEN** 不触发重试，记录"上传成功"日志

#### Scenario: 网络异常重试后成功
- **WHEN** 上传过程中发生 `HttpRequestException` 或 `IOException`（如 Connection reset by peer）
- **THEN** 等待 1 秒后重试
- **AND** 第二次失败等待 2 秒后重试
- **AND** 第三次失败等待 4 秒后重试
- **AND** 第四次仍失败则抛出异常，终止编译任务
- **AND** 每次重试记录"第 N 次重试，原因：xxx，等待 N 秒"日志

#### Scenario: 服务端返回 5xx 重试
- **WHEN** 服务端返回 HTTP 500/502/503/504
- **THEN** 按同样的递增延迟策略重试，最多 3 次

### Requirement: 401 认证失效重新登录
UploadPackageAsync SHALL 在检测到 401 认证失效时，自动调用 `_client.Login` 刷新 token 后重试上传。

#### Scenario: Token 过期触发重新登录
- **WHEN** 服务端返回 HTTP 401 或响应体 `code == 401`
- **THEN** 调用 `_client.Login("上传认证失效重试")` 获取新 token
- **AND** 用新 token 重建 HttpClient 的 `X-Token` 请求头
- **AND** 立即重试上传，不消耗重试次数配额
- **AND** 记录"Token 失效，重新登录后重试上传"日志

#### Scenario: 重新登录也失败
- **WHEN** 401 后调用 `_client.Login` 失败（如服务端不可达、账号禁用）
- **THEN** 抛出异常，终止编译任务
- **AND** 记录"重新登录失败：xxx"日志

#### Scenario: 重新登录后上传仍失败
- **WHEN** 重新登录成功并重试上传，但返回非 401 错误
- **THEN** 按普通失败处理，继续重试次数配额（最多 3 次）

### Requirement: 重试不影响文件流
UploadPackageAsync SHALL 在每次重试时重新打开文件流，确保流式上传的可重入性。

#### Scenario: 重试时重新打开文件流
- **WHEN** 因任何原因触发重试
- **THEN** 重新 `new FileStream(packagePath, ...)` 创建新的文件流
- **AND** 上一次失败的文件流已被 `using`/`await using` 正确释放
- **AND** 文件流位置从 0 开始，确保完整重传

## MODIFIED Requirements

### Requirement: UploadPackageAsync 方法签名与调用方
UploadPackageAsync 的方法签名保持不变（`private async Task UploadPackageAsync(String server, String deployName, String packagePath, String? commitId, String? commitLog, String? commitTime)`），内部实现改为"重试 + 重认证"循环。调用方 `OnCompile` 无需修改。

#### Scenario: 调用方无感知
- **WHEN** `OnCompile` 调用 `await UploadPackageAsync(...)`
- **THEN** 方法签名、返回值、异常行为保持一致
- **AND** 重试和重认证逻辑完全封装在方法内部
