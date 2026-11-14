# Checklist

- [x] AppDeploy 实体包含 DeployKey 字段（DataType=String, Length=2000, Category="编译参数"）
- [x] Stardust.Models.CompileCommand 包含 DeployKey 属性
- [x] DeployAgent.CompileCommand 包含 DeployKey 属性
- [x] Web 端 DeployService.Compile 方法将 app.DeployKey 赋值给 CompileCommand.DeployKey
- [x] DeployAgent.DeployService 包含 SetupSshKey 方法，能将私钥写入临时文件
- [x] DeployAgent.DeployService 包含 CleanupSshKey 方法，能删除临时密钥文件
- [x] CreateProcessStartInfo 支持传入 GIT_SSH_COMMAND 环境变量
- [x] OnCompile 方法在 Git 操作前调用 SetupSshKey，在 finally 中调用 CleanupSshKey
- [x] GitClone 和 GitPull 正确传递 GIT_SSH_COMMAND 到 CreateProcessStartInfo
- [x] 当 DeployKey 为空时，Git 操作回退到原有 HTTP 行为（不设置 GIT_SSH_COMMAND）
- [x] 当 DeployKey 格式无效时（不以 -----BEGIN 开头），记录警告日志
- [x] 临时文件清理失败时记录警告日志，不抛出异常
- [x] 编译通过（dotnet publish DeployAgent -f net10.0）