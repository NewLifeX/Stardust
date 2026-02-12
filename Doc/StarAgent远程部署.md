# StarAgent 远程部署功能

本功能支持通过 SSH 远程批量部署 StarAgent 到目标服务器。

## 功能特点

1. **批量部署**：支持单个IP、多个IP或CIDR网段（如 192.168.1.0/24）
2. **跨平台**：支持 Linux 和 Windows 系统
3. **灵活执行**：可选择 StarServer 或 StarWeb 执行部署
4. **无第三方依赖**：直接使用系统 SSH 命令，不引入第三方库
5. **自动配置**：部署时自动配置 StarAgent 指向指定的 StarServer

## 使用方法

### 1. 访问部署页面

在 StarWeb 中，导航到 **节点管理 → 远程部署**

### 2. 填写部署参数

- **目标主机**：
  - 单个IP：`192.168.1.100`
  - 多个IP：`192.168.1.100,192.168.1.101` 或换行分隔
  - CIDR网段：`192.168.1.0/24` （自动解析网段内所有IP）

- **SSH连接信息**：
  - 用户名：SSH登录用户（通常为 root）
  - 密码：SSH登录密码
  - SSH端口：默认 22

- **系统配置**：
  - 操作系统：Linux 或 Windows
  - .NET版本：8.0、9.0 或 10.0

- **服务器地址**：StarAgent 安装后连接的 StarServer 地址

- **下载地址**：StarAgent 安装包下载地址（默认：http://x.newlifex.com/star/）

- **执行位置**：
  - **StarServer执行**（推荐）：适合大规模部署，由 StarServer 执行部署任务
  - **StarWeb执行**：适合快速测试，由 StarWeb 本地执行（暂未实现）

### 3. 测试连接

点击"测试连接"按钮，验证SSH连接是否正常。

### 4. 开始部署

点击"开始部署"按钮，开始执行远程部署。部署完成后会显示每个主机的部署状态。

## 技术实现

### 架构设计

```
StarWeb (前端)
    ↓ HTTP API
StarServer (后端)
    ↓ SSH + 脚本
目标服务器
```

### Linux 部署流程

1. 检查并安装 .NET 运行时（如未安装）
2. 下载 StarAgent 安装包（tar.gz）
3. 解压到指定目录
4. 卸载旧版本（如存在）
5. 安装并配置服务
6. 启动 StarAgent 服务

### Windows 部署流程

1. 下载 StarAgent 安装包（zip）
2. 停止并删除旧版本（如存在）
3. 解压到 Program Files
4. 安装为 Windows 服务
5. 启动服务

### 网段解析

支持标准 CIDR 格式，例如：
- `192.168.1.0/24`：解析为 192.168.1.1 ~ 192.168.1.254
- `10.0.0.0/16`：解析为 10.0.0.1 ~ 10.0.255.254
- 最多解析 1024 个IP（防止误操作）

## 系统要求

### StarServer/StarWeb 服务器

- Linux系统（推荐）
- 已安装 `sshpass` 命令（用于自动化SSH密码输入）

```bash
# Ubuntu/Debian
sudo apt-get install sshpass

# CentOS/RHEL
sudo yum install sshpass
```

### 目标服务器

**Linux：**
- SSH服务已启动
- 允许root或sudo用户登录
- 网络可访问

**Windows：**
- OpenSSH Server 已安装（Windows 10/11 内置）
- 允许SSH登录
- 网络可访问

## API 接口

### POST /AgentDeploy/Deploy

执行远程部署

**请求参数：**
```json
{
  "hosts": "192.168.1.100,192.168.1.101",
  "port": 22,
  "userName": "root",
  "password": "***",
  "osType": "Linux",
  "serverUrl": "http://server:6600",
  "downloadUrl": "http://x.newlifex.com/star/",
  "dotnetVersion": 9
}
```

**响应：**
```json
[
  {
    "host": "192.168.1.100",
    "success": true,
    "message": "部署成功",
    "output": "..."
  }
]
```

### POST /AgentDeploy/TestConnection

测试SSH连接

**请求参数：**
```
host: 192.168.1.100
port: 22
userName: root
password: ***
```

**响应：**
```json
{
  "success": true,
  "message": "连接成功"
}
```

## 安全注意事项

1. **密码安全**：密码通过HTTPS传输，不会记录到日志
2. **权限控制**：建议使用专用账号进行部署，限制权限
3. **网络隔离**：在生产环境中，建议通过跳板机或VPN访问目标服务器
4. **审计日志**：所有部署操作都会记录审计日志

## 故障排查

### 连接失败

1. 检查目标服务器SSH服务是否启动
2. 检查防火墙规则是否允许SSH连接
3. 验证用户名和密码是否正确
4. 确认 sshpass 命令是否已安装

### 部署失败

1. 查看详细输出日志，了解具体错误
2. 检查目标服务器磁盘空间是否充足
3. 验证下载地址是否可访问
4. 检查 .NET 运行时是否正确安装

### Windows 特殊问题

1. 确认 OpenSSH Server 已安装并启动
2. 检查 PowerShell 执行策略（需要允许脚本执行）
3. 验证用户是否具有管理员权限

## 更新日志

### v1.0.0 (2026-02-12)

- ✅ 支持 Linux 远程部署
- ✅ 支持 Windows 远程部署
- ✅ 支持 CIDR 网段批量部署
- ✅ 提供 Web 可视化界面
- ✅ 支持连接测试
- ✅ 部署结果详细展示
