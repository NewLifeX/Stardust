# PUB-24 SSL 证书下发

> 版本：v1.0 | 日期：2026-11-22
> 对应功能：PUB-24 证书下发（PUB-10 SSL 证书管理的延伸）

---

## 1. 设计目标

把 SSL 证书从「跟着应用包走」改成「独立资源、按节点下发」，避免引入"主包 + 资源包"两套概念。

一句话：**证书归证书中心管，节点归证书节点管，下发是显式动作；应用发布只管应用。**

## 2. 概念

| 概念 | 说明 |
|------|------|
| 证书（SslCertificate） | 域名 + 证书文件，平台级资源，已有实体 |
| 证书节点（SslCertificateNode） | 证书与节点的多对多关联，决定这张证书要装到哪些机器 |
| 证书目录 | 节点上存放证书的目录，默认 `../certs`（相对 StarAgent 安装目录） |
| 证书名 | 节点上的文件名前缀，默认取域名去掉通配符，如 `*.newlifex.com` → `newlifex.com` |

落盘约定：

```
{证书目录}/
  {证书名}.pem                证书链（Linux/Nginx）
  {证书名}.privatekey.pem     私钥（Linux/Nginx，权限 600）
  {证书名}.pfx                PFX（Windows/IIS）
```

StarAgent 默认安装在 `/root/agent`，因此证书目录是 `/root/certs`，与现网服务器保持一致。

## 3. 使用步骤

1. **录入证书**：发布中心 → SSL证书 → 新增/编辑
   - 域名：`*.newlifex.com`
   - 证书名：留空自动得到 `newlifex.com`
   - PEM文件：上传证书链（含中间证书）
   - 私钥文件：上传私钥
   - Windows 节点另传 PFX 文件
2. **关联节点**：在证书行点「节点」进入证书节点页，手工新增，或回证书列表点「关联全部」把证书关联到全部启用节点
3. **下发**：证书行点「下发」，或在证书节点页勾选后「批量下发」
4. **验证**：节点上 `ls -l /root/certs` 应看到新文件；StarAgent 日志出现下载与 nginx reload 记录
5. **续期**：重新上传 PEM/私钥 → 再点「下发」即可，不需要重新发布应用

## 4. 服务端接口

| 接口 | 说明 |
|------|------|
| `Deploy/GetCerts?certId=` | 节点拉取分配给自己的证书。只返回与该节点关联且启用的证书；`certId=0` 返回全部 |
| 指令 `cert/sync` | 平台通知节点同步证书，参数 `{ certId }`。省略或 0 表示全部 |

安全约束：

- 节点只能取到自己关联的证书；不能通过伪造 `certId` 取别人的私钥
- 私钥下载地址按附件短时令牌签发（同部署包下载）
- 私钥落盘后设置 600 权限（仅属主读写）

## 5. StarAgent 行为

收到 `cert/sync` 后：

1. 调用 `Deploy/GetCerts` 拉取证书清单
2. `CertManager` 下载文件到目标目录：临时文件 → MD5 校验 → 覆盖
3. 更新本地清单 `{DataPath}/certs.json`，记录域名、证书名、文件路径、版本
4. 文件发生变化时执行 `nginx -t`，通过后 `nginx -s reload`

下载失败或校验失败不会覆盖旧文件，站点继续使用旧证书。

## 6. Nginx 配置中的证书

服务端生成应用默认站点配置时，HTTPS 站点会写入占位符：

```
ssl_certificate      {CertDir}/{CertName}.pem;
ssl_certificate_key  {CertDir}/{CertName}.privatekey.pem;
```

StarAgent 发布站点时：

- `{CertDir}` 替换为本机证书目录绝对路径
- `{CertName}` 按站点 `server_name` 匹配本地证书清单，匹配不到时使用域名本身
- 配置里监听 443 却没有 `ssl_certificate` 时，自动补全两条指令
- 写入目标配置后先 `nginx -t`；失败则回滚旧配置，不再 reload

因此应用包里不再需要写死 `/root/certs/...` 绝对路径；已有写死的配置保持兼容。

## 7. 与旧方案的差异

| 旧方案（已废弃） | 新方案 |
|------------------|--------|
| 应用发布时携带"资源包" | 证书独立下发，与应用解耦 |
| 需要理解主包/资源包 | 只需理解"证书 → 节点" |
| nginx 配置由服务端生成，证书路径未知 | 证书路径占位符由节点发布时解析 |

## 8. 相关代码

| 位置 | 说明 |
|------|------|
| `Stardust.Data/Deployment/SSL证书*.cs` | 证书实体与域名匹配 |
| `Stardust.Data/Deployment/证书节点*.cs` | 证书节点关联 |
| `Stardust/Deployment/CertManager.cs` | 证书下载、落盘、清单、Nginx 路径解析 |
| `Stardust.Server/Controllers/DeployController.cs` | `Deploy/GetCerts` |
| `StarAgent/Managers/CertSyncCommand.cs` | `cert/sync` 指令处理 |
| `StarAgent/Managers/NginxDeploy.cs` | 站点发布、`nginx -t` 校验与重载 |
| `Stardust.Web/Services/CertService.cs` | 控制台下发的编排与状态回写 |

## 9. 兼容性说明

- `cert/sync` 是新增指令，旧版 StarAgent 不支持。旧节点需要升级代理后才能接收下发
- 未升级代理时，「下发」会返回超时无响应，不影响现有证书继续使用
- 证书目录可通过 StarAgent 配置 `CertPath` 覆盖；证书实体和证书节点也可单独指定目标目录
