# Tasks

- [x] Task 1: AppDeploy 实体新增 DeployKey 字段
  - [x] 在 `Stardust.Data/Deployment/Model.xml` 的 AppDeploy 表中新增 `DeployKey` Column，DataType=String，Length=2000，Description="仓库密钥。SSH 私钥，用于非交互式拉取私有仓库代码"，Category="编译参数"
  - [x] 运行 xcode 生成实体代码

- [x] Task 2: CompileCommand 模型新增 DeployKey 属性
  - [x] 在 `Stardust.Models/CompileCommand.cs` 新增 `DeployKey` 属性（String?）
  - [x] 在 `DeployAgent/Commands/CompileCommand.Model.cs` 新增 `DeployKey` 属性（String?）

- [x] Task 3: Web 端 DeployService 传递 DeployKey
  - [x] 在 `Stardust.Web/Services/DeployService.cs` 的 Compile 方法中，构造 CompileCommand 时添加 `DeployKey = app.DeployKey`

- [x] Task 4: DeployAgent 实现 SSH 密钥管理和 Git SSH 操作
  - [x] 新增 `SetupSshKey(String? deployKey)` 方法：将私钥写入临时文件，返回临时文件路径（或 null）
  - [x] 新增 `CleanupSshKey(String? keyFile)` 方法：删除临时密钥文件
  - [x] 修改 `CreateProcessStartInfo` 方法，支持传入 `GIT_SSH_COMMAND` 环境变量
  - [x] 修改 `OnCompile` 方法：在 Git 操作前调用 `SetupSshKey`，在 finally 中调用 `CleanupSshKey`
  - [x] 修改 `GitClone` 和 `GitPull` 方法，接受 `GIT_SSH_COMMAND` 参数并传递给 `CreateProcessStartInfo`

# Task Dependencies
- Task 2 depends on Task 1（CompileCommand 字段与 AppDeploy 字段对应）
- Task 3 depends on Task 2（Web 端需要 CompileCommand 模型已有 DeployKey 属性）
- Task 4 depends on Task 2（DeployAgent 需要 CompileCommand 模型已有 DeployKey 属性）
- Task 3 和 Task 4 可并行执行