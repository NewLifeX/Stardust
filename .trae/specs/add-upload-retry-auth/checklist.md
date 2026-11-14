# Checklist

## 实现完整性
- [x] UploadPackageAsync 方法签名保持不变，调用方 OnCompile 无需修改
- [x] 新增 PostUploadAsync 方法封装单次上传逻辑，支持重试复用
- [x] 新增 EnsureLoginAsync 方法，调用 _client.Login 刷新 token
- [x] UploadResult 类能正确解析 code==401 作为重认证信号（IsUnauthorizedBody 辅助方法）

## 重试逻辑
- [x] 最多重试 3 次，第 N 次失败后延迟 (2^(N-1)) 秒（1s → 2s → 4s）
- [x] 每次重试打印"第 N 次重试，原因：xxx，等待 N 秒"日志
- [x] 捕获 HttpRequestException、IOException、SocketException、TaskCanceledException 等网络异常触发重试
- [x] 捕获服务端 5xx 状态码（500/502/503/504）触发重试
- [x] 所有重试耗尽后抛出含最后一次失败原因的异常

## 401 重认证逻辑
- [x] 检测 HTTP 401 状态码或响应体 code==401 触发重新登录
- [x] 调用 _client.Login("上传认证失效重试") 获取新 token
- [x] 用新 token 更新 HttpClient 的 X-Token 请求头
- [x] 401 触发的重试不消耗重试次数配额
- [x] 重新登录失败时抛出异常终止编译任务
- [x] 记录"Token 失效，重新登录"和"重新登录失败"日志

## 文件流安全
- [x] 每次重试重新 new FileStream(packagePath, ...) 创建新流
- [x] 上一次失败的文件流被 await using 正确释放
- [x] 文件流位置从 0 开始，确保完整重传

## 单元测试
- [x] 新增测试：模拟 401 后重新登录并重试成功
- [x] 新增测试：模拟连续网络异常后失败
- [x] 新增测试：模拟 401 后响应体 code=401 被正确识别
- [x] 所有原有 DeployControllerTests 测试仍通过（回归，10/10）

## 编译与集成
- [x] DeployAgent 项目编译无错误
- [x] ServerTest 项目编译无错误
- [x] dotnet test --filter "FullyQualifiedName~DeployControllerTests" 全部通过（10/10）
