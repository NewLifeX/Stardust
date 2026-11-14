# Tasks（TDD 方式：先写测试 → 测试失败 → 实现代码 → 测试通过）

- [ ] Task 1: AppDeploy 实体新增 RepoUserName / RepoPassword 字段
  - Model.xml 新增字段 → 运行 xcode 重生成

- [ ] Task 2: AppDeployController 统一处理脱敏、加密与脏数据
  - `post=false`：RepoPassword 设为 `*****`
  - `post=true`：`*****` 回填原密文 / 非空 AES 加密 / 空值清空
  - 列表隐藏密码字段
- [ ] AppDeploy.Biz.cs 不做任何修改

- [ ] Task 3: CompileCommand 模型扩展（两份）
  - 新增 RepoUserName / RepoPassword 属性，RedactForHistory 脱敏

- [ ] Task 4: DeployService.Compile 下发凭据
  - TokenSecret 解密存储密文 → Node.Secret 重新加密

- [ ] Task 5: DeployAgent.OnCompile 解密 + URL 组装 + 日志脱敏
  - StarSetting.Secret 解密 → BuildCloneUrl → 日志脱敏

- [ ] Task 6: 服务端集成测试（TDD：先写测试，再实现 Task 2/4）
  - 测试 AES 加解密往返
  - 测试密码保存/回显/未修改/修改/清空
  - 测试传输加密链路
  - 测试 URL 脱敏

- [ ] Task 7: DeployAgent 端 URL 组装测试（TDD：先写测试，再实现 Task 5）
  - 测试 BuildCloneUrl 各种场景 + 脱敏

# 依赖
- Task 3 ← 无；Task 4 ← Task 3；Task 5 ← Task 3；Task 6 ← Task 2,4；Task 7 ← Task 5
- 可并行：Task 1+2+3 第一批；Task 4+5 第二批；Task 6+7 第三批
