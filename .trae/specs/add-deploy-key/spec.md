# Add Deploy Key Spec

## Why
当前 DeployAgent 通过 HTTP 方式拉取 Gitea 仓库代码，在 Windows 服务模式下（Session 0）无法进行交互式认证（GCM OAuth 弹窗 / TTY 输入用户名密码）。需要支持 SSH 部署密钥方式，使 DeployAgent 能以非交互方式安全地拉取私有仓库。

## What Changes
- AppDeploy 实体新增 `DeployKey` 字段，存储 SSH 私钥
- CompileCommand 模型新增 `DeployKey` 字段，传递私钥到 DeployAgent
- DeployAgent 在 Git 操作前将私钥写入临时文件，通过 `GIT_SSH_COMMAND` 环境变量指定 SSH 密钥
- 支持通过 Repository 字段中的 SSH URL 格式（如 `git@host:port/user/repo.git`）拉取代码

## Impact
- Affected specs: 无
- Affected code:
  - `Stardust.Data/Deployment/Model.xml` — AppDeploy 表新增字段
  - `Stardust.Models/CompileCommand.cs` — 新增 DeployKey 属性
  - `DeployAgent/Commands/CompileCommand.Model.cs` — 新增 DeployKey 属性
  - `Stardust.Web/Services/DeployService.cs` — 传递 DeployKey
  - `DeployAgent/DeployService.cs` — SSH 密钥管理 + Git SSH 操作

## ADDED Requirements

### Requirement: AppDeploy 仓库密钥字段
系统 SHALL 在 AppDeploy 实体中提供 `DeployKey` 字段，用于存储 SSH 私钥文本。

#### Scenario: 存储 SSH 私钥
- **WHEN** 用户在应用部署编辑页面填写仓库密钥
- **THEN** 私钥内容被保存到 AppDeploy.DeployKey 字段
- **AND** 字段在 UI 中默认隐藏（密码类型），支持查看/隐藏切换

#### Scenario: 字段为空
- **WHEN** DeployKey 为空
- **THEN** Git 操作回退到原有 HTTP 方式（Repository URL 以 http/https 开头）

### Requirement: CompileCommand 传递 DeployKey
系统 SHALL 在编译命令中将 AppDeploy.DeployKey 传递到 DeployAgent。

#### Scenario: 服务端构造编译命令
- **WHEN** DeployService.Compile 构造 CompileCommand
- **THEN** CompileCommand.DeployKey  = app.DeployKey

### Requirement: DeployAgent SSH 密钥管理
DeployAgent SHALL 在收到 DeployKey 后，将私钥写入临时文件、配置 Git 使用该密钥，并在操作完成后清理临时文件。

#### Scenario: 有效 SSH 私钥
- **WHEN** DeployKey 非空且以 `-----BEGIN` 开头
- **THEN** 将私钥写入临时文件（如 `%TEMP%/stardust-deploy-key-{guid}`）
- **AND** 设置 `GIT_SSH_COMMAND=ssh -i <temp_file> -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null`
- **AND** Git 操作完成后删除临时文件

#### Scenario: 无效私钥格式
- **WHEN** DeployKey 非空但不以 `-----BEGIN` 开头
- **THEN** 记录警告日志，跳过 SSH 密钥设置，使用默认 SSH 配置

#### Scenario: 清理失败
- **WHEN** 临时文件删除失败
- **THEN** 记录警告日志，不抛出异常（不影响编译结果）

### Requirement: Git 操作使用 SSH
DeployAgent SHALL 在 GitClone 和 GitPull 时，通过 `GIT_SSH_COMMAND` 环境变量使用指定的 SSH 密钥。

#### Scenario: SSH URL 克隆
- **WHEN** Repository 为 SSH 格式（如 `git@host:port/repo.git` 或 `ssh://git@host:port/repo.git`）
- **AND** DeployKey 已设置
- **THEN** Git clone 使用指定的 SSH 私钥认证

#### Scenario: HTTP URL 回退
- **WHEN** Repository 为 HTTP 格式且 DeployKey 为空
- **THEN** Git clone 使用 HTTP 方式（原有行为，受 `GIT_TERMINAL_PROMPT=0` 保护）