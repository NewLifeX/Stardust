# M5-15 StarAgent 远程部署

> 版本：v1.0 | 日期：2026-07-15
> 对应需求：M5-15 StarAgent 远程部署

---

## 功能概述

本功能支持通过 SSH 远程批量部署 StarAgent 到目标服务器。

## 功能特点

1. **批量部署**：支持单个IP、多个IP或CIDR网段（如 192.168.1.0/24）
2. **跨平台**：支持 Linux 和 Windows 系统
3. **灵活执行**：可选择 StarServer 或 StarWeb 执行部署
4. **无第三方依赖**：直接使用系统 SSH 命令
5. **自动配置**：部署时自动配置 StarAgent 指向指定的 StarServer

## 使用方法

1. 在 StarWeb 中导航到 **节点管理 → 远程部署**
2. 填写部署参数：目标主机（IP/CIDR）、SSH连接信息（用户名/密码/端口）、系统类型、.NET版本
3. 点击「测试连接」验证 SSH 连接
4. 点击「开始部署」执行远程部署

## 架构设计

```
StarWeb (前端) → HTTP API → StarServer (后端) → SSH + 脚本 → 目标服务器
```

## 相关代码

- `Stardust.Server/Controllers/AgentDeployController.cs` — 部署 API
- `Stardust.Server/Services/AgentDeployService.cs` — 部署服务
