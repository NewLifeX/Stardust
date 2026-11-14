# Tasks

- [x] Task 1: 抽取单次上传逻辑为 PostUploadAsync 方法
  - [x] SubTask 1.1: 在 `DeployAgent/DeployService.cs` 中新增 `PostUploadAsync(HttpClient httpClient, String uploadUrl, String packagePath)` 方法，封装"打开文件流 → 构建 MultipartFormDataContent → POST → 读取响应 → 释放流"的单次上传逻辑
  - [x] SubTask 1.2: 将响应解析逻辑（`UploadResult.Code != 0` 检查）移入 PostUploadAsync，返回结构化结果 `UploadAttempt { StatusCode, Body }`
  - [x] SubTask 1.3: 确保文件流在每次调用结束时正确释放（`await using`），支持重试时重新打开

- [x] Task 2: 新增 EnsureLoginAsync 重新认证方法
  - [x] SubTask 2.1: 在 `DeployAgent/DeployService.cs` 中新增 `EnsureLoginAsync()` 方法，调用 `_client.Login("上传认证失效重试")` 刷新 token
  - [x] SubTask 2.2: 方法返回新的 token 字符串（从 `_client.Client?.Token` 获取），登录失败时抛出异常
  - [x] SubTask 2.3: 记录"Token 失效，重新登录"日志，登录失败记录"重新登录失败"异常日志

- [x] Task 3: 重构 UploadPackageAsync 为"重试 + 401 重认证"循环
  - [x] SubTask 3.1: 保留原方法签名 `Task UploadPackageAsync(String server, String deployName, String packagePath, String? commitId, String? commitLog, String? commitTime)`
  - [x] SubTask 3.2: 实现 3 次重试循环，延迟递增（1s → 2s → 4s），每次重试打印"第 N 次重试，原因：xxx，等待 N 秒"
  - [x] SubTask 3.3: 检测 HTTP 401 或响应体 `code == 401`，调用 EnsureLoginAsync 获取新 token，更新 HttpClient 的 X-Token 头后立即重试（不消耗重试配额）
  - [x] SubTask 3.4: 捕获 `HttpRequestException`、`IOException`、`SocketException`、`TaskCanceledException` 等网络异常，按重试策略处理
  - [x] SubTask 3.5: 捕获服务端 5xx 状态码（500/502/503/504），按重试策略处理
  - [x] SubTask 3.6: 所有重试耗尽后抛出聚合异常（含最后一次失败原因），终止编译任务

- [x] Task 4: 更新 UploadResult 类支持 code==401 判定
  - [x] SubTask 4.1: 确认 `UploadResult` 类已有 `Code` 和 `Message` 字段
  - [x] SubTask 4.2: 在响应解析中增加对 `code == 401` 的识别（`IsUnauthorizedBody` 辅助方法），作为触发重认证的信号

- [x] Task 5: 新增单元测试验证重试与重认证逻辑
  - [x] SubTask 5.1: 在 `Stardust.ServerTests/Controllers/DeployControllerTests.cs` 新增测试：模拟 401 响应后重新登录并重试成功
  - [x] SubTask 5.2: 新增测试：模拟连续网络异常后失败，验证异常类型
  - [x] SubTask 5.3: 新增测试：模拟 401 后响应体 code=401 被正确识别
  - [x] SubTask 5.4: 验证所有原有测试仍通过（回归测试，10/10 通过）

- [x] Task 6: 编译验证与集成测试
  - [x] SubTask 6.1: 编译 DeployAgent 项目，确认无编译错误
  - [x] SubTask 6.2: 编译 ServerTest 项目，确认无编译错误
  - [x] SubTask 6.3: 运行 `dotnet test --filter "FullyQualifiedName~DeployControllerTests"`，全部通过（10/10）

# Task Dependencies
- [Task 2] 依赖 [Task 1] 完成（PostUploadAsync 需先抽出）
- [Task 3] 依赖 [Task 1] 和 [Task 2] 完成
- [Task 4] 与 [Task 1] 并行
- [Task 5] 依赖 [Task 3] 完成
- [Task 6] 依赖 [Task 5] 完成
