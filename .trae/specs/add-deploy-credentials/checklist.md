# Checklist

- [x] Model.xml 新增 RepoUserName / RepoPassword 字段，xcode 重生成
  - `Stardust.Data/Deployment/Model.xml` L61-L62
  - `Stardust.Data/Deployment/应用部署.cs` L148-L164（属性定义）、L598-L601（Field 元数据）

- [x] `Stardust.Server/Setting.cs` 未修改
  - 复用 `StarServerSetting.TokenSecret`（`Stardust.Server/Setting.cs`），不新增配置项

- [x] 不新增任何文件
  - 加解密代码内联在 Controller / Service / Agent 中，无独立服务类

- [x] AppDeploy.Biz.cs 不做任何修改
  - 所有逻辑在 `AppDeployController.Valid` 中处理

- [x] AppDeployController：`post=false` 脱敏为 `*****`，`post=true` 处理回填/加密/清空，列表隐藏密码
  - `Stardust.Web/Areas/Deployment/Controllers/AppDeployController.cs`
    - L26：`ListFields.RemoveField("RepoPassword")` 列表隐藏
    - L132-L185：`Valid(AppDeploy, DataObjectMethodType, Boolean)` 方法
    - L142-L146：`post=false` 时 `Update` 操作脱敏为 `*****`
    - L155-L183：`post=true` 时处理 `*****` 回填 / 非空 AES 加密 / 空值清空

- [x] CompileCommand（两份）新增 RepoUserName / RepoPassword，RedactForHistory 脱敏
  - `Stardust.Models.CompileCommand`（`Stardust/Models/CompileCommand.cs`）
    - L16-L19：`RepoUserName` / `RepoPassword` 属性
    - L59-L86：`RedactForHistory()` 方法（含 RepoUserName 首字符+***、RepoPassword 置 null、URL 凭据剥离）
    - L100-L147：`BuildCloneUrl()` 静态方法
    - L151-L168：`RedactUrlForLog()` 静态方法
  - `DeployAgent.Commands.CompileCommand`（`DeployAgent/Commands/CompileCommand.Model.cs`）
    - L17-L20：`RepoUserName` / `RepoPassword` 属性（仅数据模型，无业务方法）

- [x] DeployService.Compile：TokenSecret 解密 → Node.Secret 加密
  - `Stardust.Web/Services/DeployService.cs` L76-L87
  - L79：`StarServerSetting.Current.TokenSecret.Split(':')[1]` 获取存储密钥
  - L82：AES 解密存储密文得到明文密码
  - L84-L86：用 `buildNode.Node.Secret` 重新加密后设置到 `cmd.RepoPassword`

- [x] DeployAgent.OnCompile：StarSetting.Secret 解密 → BuildCloneUrl → 日志脱敏
  - `DeployAgent/DeployService.cs`
    - L153-L173：解密 `RepoPassword` + 调用 `BuildCloneUrl` 组装克隆 URL
    - L162：`Stardust.Models.CompileCommand.BuildCloneUrl(...)` SSH 优先
    - L176：`Stardust.Models.CompileCommand.RedactUrlForLog(cloneUrl)` 日志脱敏

- [x] 服务端测试通过（加解密往返、保存/回显/编辑/清空、传输链路、脱敏）— 12/12
  - `Stardust.ServerTests/Deployment/DeployCredentialsTests.cs`
  - 12 个 `[Fact]` 测试方法，覆盖 AES 加解密往返、不同密钥不同密文、保存加密、回显脱敏、编辑回填/重新加密/清空、CompileCommand 脱敏、TokenSecret 解析、传输链路加解密、错误密钥异常

- [x] Agent 端测试通过（BuildCloneUrl 各种场景 + 脱敏）— 19/19
  - `Test/DeployCredentialsTests.cs`
  - 19 个测试方法，覆盖 BuildCloneUrl 9 种场景（SSH+用户名/仅SSH/用户名+密码/SSH优先/无凭据/空/null/特殊字符/SSH替换用户名）、RedactForHistory 3 种、RedactUrlForLog 4 种、日志脱敏、完整链路

- [ ] 既有测试未回归（8 个既有失败与本次改动无关）
  - `Stardust.ServerTests/Deployment/PipelineResumeTests.cs` 5 个失败（Pipeline 恢复逻辑）
  - `Stardust.ServerTests/Controllers/DeployControllerTests.cs` 3 个失败（UploadBuildFile 参数校验）
  - 以上失败在改动前已存在，非本次引入

- [x] `dotnet build` 通过
  - `DeployAgent` 和 `Stardust` 项目均构建成功

- [x] 数据库 RepoPassword 为 Hex 密文（非明文）
  - AES-CBC 加密后 `cipherBytes.ToHex()` 输出 Hex 格式

- [x] API 响应 RepoPassword 为 `*****`
  - `AppDeployController.Valid` L142-L146：`post=false` 时设为 `*****`

- [x] 审计日志/历史记录/Agent 日志不含明文密码
  - `CompileCommand.RedactForHistory()` 脱敏 RepoPassword/DeployKey/URL 凭据
  - `CompileCommand.RedactUrlForLog()` 脱敏 URL 中密码
  - Agent 日志使用 `RedactUrlForLog` 输出脱敏后的 URL
