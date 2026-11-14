# Add Deploy Credentials Spec

## 需求
应用部署已有 SSH 密钥（`DeployKey`），现增加仓库用户名（`RepoUserName`）和密码（`RepoPassword`）字段，用于 HTTPS 方式克隆私有仓库。

## 加密方案
- **存储加密**：AES-CBC，PKCS7 填充，密钥长度由 TokenSecret 密钥部分决定，复用 `NewLife.Security.SecurityHelper` 扩展方法（`aes.Encrypt(data, pass)` / `aes.Decrypt(data, pass)`），输出 Hex
- **存储密钥**：`StarServerSetting.TokenSecret.Split(':')[1]`（已有配置，不新增）
- **传输加密**：同上 AES 算法，密钥用 `buildNode.Node.Secret`（服务端加密）/ `StarSetting.Current.Secret`（Agent 解密）
- **不新增任何文件/服务类**：加解密代码直接内联在需要的地方（3-5 行）

## 改动点
1. **AppDeploy 实体**：Model.xml 新增 `RepoUserName`（String, 50）和 `RepoPassword`（String, 200），运行 xcode 重生成
2. **AppDeployController**：`Valid` 中统一处理所有逻辑：
   - `post=false`（GET 查询后）：仅 `Update` 操作时 `RepoPassword` 设为 `*****`（Insert/Detail 不处理）
   - `post=true`（保存前）：`*****` 回填原密文 / 非空 AES 加密 / 空值清空
   - 列表通过 `ListFields.RemoveField("RepoPassword")` 隐藏密码字段
3. **CompileCommand**（两份）：
   - `Stardust.Models.CompileCommand`（服务端模型）：新增 `RepoUserName` / `RepoPassword` 属性，`RedactForHistory` 脱敏（含 RepoUserName 首字符+***、RepoPassword 置 null、URL 凭据剥离），新增 `BuildCloneUrl` / `RedactUrlForLog` 静态方法
   - `DeployAgent.Commands.CompileCommand`（Agent 端模型）：仅新增属性定义，不含业务方法
4. **DeployService.Compile**（`Stardust.Web/Services/DeployService.cs`）：下发前用 TokenSecret 解密存储密文 → 用 Node.Secret 重新加密
5. **DeployAgent.OnCompile**：用 StarSetting.Secret 解密 → `BuildCloneUrl` 组装 URL（SSH 优先 / HTTPS+凭据 / 原样）→ 日志用 `RedactUrlForLog` 脱敏
6. **测试**：`DeployCredentialsTests.cs`（服务端 12 个）+ `Test/DeployCredentialsTests.cs`（Agent 端 19 个）

## 不修改
- `Stardust.Server/Setting.cs`（复用已有 TokenSecret）
- 不新增任何文件（加解密内联）
