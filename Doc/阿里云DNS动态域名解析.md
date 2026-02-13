# StarAgent 阿里云DNS动态域名解析

## 功能概述

StarAgent 集成了阿里云DNS动态域名解析功能，允许客户端自动将其公网IP地址注册到指定的域名记录，实现动态DNS（DDNS）功能。

本功能为纯客户端实现，无需星尘平台服务端干预，适用于以下场景：

- 家庭/办公室宽带动态IP环境
- 需要远程访问内网服务器
- 公网IP频繁变化的场景
- 轻量级DDNS解决方案

## 配置说明

在 `StarAgent.config` 配置文件中添加以下配置项：

### 配置示例

```json
{
  "AliyunAccessKeyId": "LTAI4***",
  "AliyunAccessKeySecret": "xPZQ***",
  "AliyunDnsDomain": "example.com",
  "AliyunDnsRecord": "home",
  "AliyunDnsRecordType": "A",
  "AliyunDnsInterval": 300
}
```

### 配置项说明

| 配置项 | 类型 | 必填 | 默认值 | 说明 |
|-------|------|------|--------|------|
| `AliyunAccessKeyId` | String | 是 | - | 阿里云访问密钥ID |
| `AliyunAccessKeySecret` | String | 是 | - | 阿里云访问密钥Secret |
| `AliyunDnsDomain` | String | 是 | - | 域名，例如：example.com |
| `AliyunDnsRecord` | String | 是 | - | 记录名，例如：home 表示 home.example.com，@ 表示根域名 |
| `AliyunDnsRecordType` | String | 否 | A | 记录类型，通常为 A（IPv4地址） |
| `AliyunDnsInterval` | Int32 | 否 | 300 | DNS更新间隔（秒），默认5分钟 |

## 获取阿里云访问密钥

1. 登录阿里云控制台：https://ram.console.aliyun.com/
2. 进入 **访问控制（RAM）** > **用户**
3. 创建新用户或选择已有用户
4. 为用户添加权限：`AliyunDNSFullAccess`（DNS完全访问权限）
5. 创建 AccessKey，保存 AccessKeyId 和 AccessKeySecret

**安全提示**：
- 建议为DNS功能创建专用的RAM用户
- 仅授予DNS相关权限，遵循最小权限原则
- 定期轮换访问密钥
- 妥善保管密钥，不要提交到代码仓库

## 工作原理

1. **启动阶段**：StarAgent 启动时读取配置，初始化阿里云DNS客户端
2. **IP获取**：通过公共IP查询服务获取当前公网IP地址
3. **记录查询**：查询阿里云DNS，获取指定域名的记录ID
4. **DNS更新**：
   - 如果记录不存在，则添加新记录
   - 如果记录已存在且IP变化，则更新记录
   - 如果IP未变化，则跳过更新
5. **定期更新**：按配置的间隔定期检查并更新DNS记录

## 日志信息

启动成功时会输出以下日志：

```
启动阿里云DNS动态域名解析：home.example.com
获取公网IP地址：123.45.67.89 from https://api.ipify.org
找到DNS记录ID：123456789
成功更新阿里云DNS记录：home.example.com => 123.45.67.89
阿里云DNS更新间隔：300秒
```

## 公网IP获取

系统会依次尝试以下公共IP查询服务：

1. https://api.ipify.org
2. https://ifconfig.me/ip
3. https://icanhazip.com

如果第一个服务不可用，会自动切换到下一个，确保高可用性。

## 阿里云DNS API说明

本功能使用阿里云云解析DNS的OpenAPI，主要接口：

- **DescribeDomainRecords**：查询域名解析记录列表
- **AddDomainRecord**：添加域名解析记录
- **UpdateDomainRecord**：修改域名解析记录

API文档：https://help.aliyun.com/document_detail/29739.html

## 注意事项

### 配置要求

1. 域名必须已在阿里云DNS托管
2. AccessKey必须具有DNS操作权限
3. 网络环境必须能访问阿里云API和公网IP查询服务
4. 建议更新间隔不小于60秒，避免频繁调用API

### 安全建议

1. 使用RAM子账号，不要使用主账号AccessKey
2. 仅授予DNS相关权限
3. 定期检查和轮换访问密钥
4. 在配置文件中妥善保管密钥
5. 避免在公开渠道暴露配置文件

### 故障处理

**DNS更新失败**
- 检查AccessKey是否正确
- 确认域名已在阿里云DNS托管
- 验证网络连接是否正常
- 查看日志获取详细错误信息

**无法获取公网IP**
- 检查网络连接
- 尝试手动访问IP查询服务URL
- 确认防火墙/代理设置

**记录未更新**
- IP地址未变化时不会触发更新
- 检查更新间隔配置
- 查看日志确认更新状态

## 示例场景

### 场景一：家庭宽带远程访问

家庭宽带使用动态公网IP，需要远程访问家中的服务器：

```json
{
  "AliyunAccessKeyId": "LTAI4***",
  "AliyunAccessKeySecret": "xPZQ***",
  "AliyunDnsDomain": "mydomain.com",
  "AliyunDnsRecord": "home",
  "AliyunDnsInterval": 300
}
```

配置后，StarAgent 会自动将当前公网IP更新到 `home.mydomain.com`，每5分钟检查一次。

### 场景二：多节点同域名

如果有多个节点需要绑定不同子域名：

**节点1配置：**
```json
{
  "AliyunDnsRecord": "node1",
  "AliyunDnsDomain": "cluster.com"
}
```

**节点2配置：**
```json
{
  "AliyunDnsRecord": "node2",
  "AliyunDnsDomain": "cluster.com"
}
```

分别绑定到 `node1.cluster.com` 和 `node2.cluster.com`。

### 场景三：快速更新

对于IP变化频繁的环境，可以缩短更新间隔：

```json
{
  "AliyunDnsInterval": 60
}
```

每分钟检查一次（不建议低于60秒）。

## 技术实现

### 签名算法

使用阿里云标准的签名方法（HMAC-SHA1）：

1. 构建规范化请求字符串
2. 使用 `AccessKeySecret&` 作为密钥
3. 计算 HMAC-SHA1 签名
4. Base64 编码签名结果

### 错误处理

- 网络异常自动重试
- API调用失败记录日志
- IP获取失败尝试备用服务
- 定时器异常不影响主服务

### 性能优化

- IP地址缓存，未变化时跳过更新
- 记录ID缓存，减少查询次数
- 异步执行，不阻塞主线程

## 未来计划

- [ ] 支持 IPv6（AAAA记录）
- [ ] 支持其他DNS服务商（腾讯云、CloudFlare等）
- [ ] 支持多域名/多记录配置
- [ ] 增加DNS更新成功/失败通知
- [ ] 提供Web界面配置

## 相关资源

- [阿里云DNS产品文档](https://help.aliyun.com/product/29697.html)
- [阿里云DNS API文档](https://help.aliyun.com/document_detail/29739.html)
- [RAM访问控制](https://ram.console.aliyun.com/)
- [StarAgent文档](https://newlifex.com/stardust)

## 问题反馈

如有问题或建议，请访问：https://github.com/NewLifeX/Stardust/issues
