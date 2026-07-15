# M5-16 阿里云 DNS 动态域名解析

> 版本：v1.0 | 日期：2026-07-15
> 对应需求：M5-16 阿里云 DNS DDNS

---

## 功能概述

StarAgent 集成了阿里云DNS动态域名解析功能，允许客户端自动将其公网IP地址注册到指定的域名记录，实现动态DNS（DDNS）功能。

本功能为纯客户端实现，无需星尘平台服务端干预，适用于以下场景：
- 家庭/办公室宽带动态IP环境
- 需要远程访问内网服务器
- 公网IP频繁变化的场景
- 轻量级DDNS解决方案

## 配置说明

在 `StarAgent.config` 配置文件中添加以下配置项：

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

| 配置项 | 类型 | 必填 | 默认值 | 说明 |
|-------|------|------|--------|------|
| `AliyunAccessKeyId` | String | 是 | - | 阿里云访问密钥ID |
| `AliyunAccessKeySecret` | String | 是 | - | 阿里云访问密钥Secret |
| `AliyunDnsDomain` | String | 是 | - | 域名，例如：example.com |
| `AliyunDnsRecord` | String | 是 | - | 记录名，例如：home → home.example.com，@ 表示根域名 |
| `AliyunDnsRecordType` | String | 否 | A | 记录类型，通常为 A（IPv4地址） |
| `AliyunDnsInterval` | Int32 | 否 | 300 | DNS更新间隔（秒），默认5分钟 |

## 工作原理

1. **启动阶段**：StarAgent 启动时读取配置，初始化阿里云DNS客户端
2. **IP获取**：通过公共IP查询服务获取当前公网IP地址（依次尝试 api.ipify.org / ifconfig.me/ip / icanhazip.com）
3. **记录查询**：查询阿里云DNS，获取指定域名的记录ID
4. **DNS更新**：如果记录不存在则添加，IP变化则更新，IP未变化则跳过
5. **定期更新**：按配置的间隔定期检查并更新DNS记录

## 相关代码

- `StarAgent/AliyunDnsClient.cs` — DDNS 客户端实现
- `StarAgent/AliyunDnsSetting.cs` — DDNS 配置

## 注意事项

1. 域名必须已在阿里云DNS托管
2. AccessKey必须具有DNS操作权限（AliyunDNSFullAccess）
3. 建议使用RAM子账号，遵循最小权限原则
4. 更新间隔建议不小于60秒
