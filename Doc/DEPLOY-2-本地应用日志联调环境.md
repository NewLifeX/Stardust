# DEPLOY-2 本地应用日志联调环境

> 关联：[DEPLOY-1-节点应用日志.md](DEPLOY-1-节点应用日志.md)  
> 目标：在本机 repeatable 地跑通 **P1 文件日志**（`deploy/log/list|tail` → API `LogFiles`/`LogTail`），与 K8s Pod 日志无关。

---

## 1. 心智模型

```text
浏览器 / smoke 脚本
    → StarWebApi :6681（读 AppDeployNode、合并 LogDirectory）
    → StarFactory → StarServer :6600 Node/SendCommand
    → 在线 StarAgent（同一 StarServer、同一套 Bin\Data）
    → deploy/log/list|tail 读本机目录
```

**必须四条对齐**，缺一条就会「无权操作」或「超时」：

| # | 对齐项 | 典型错误 |
|---|--------|----------|
| 1 | StarServer、StarWebApi、SQLite 均用 **`Bin\Data`** | API 有配置、Agent 读不到 |
| 2 | StarAgent 的 **`Star.config` → `Server`** 指向**正在测**的 StarServer（本地联调 = `http://127.0.0.1:6600`） | 命令超时、节点不在线 |
| 3 | **`AppDeployNode.NodeId`** 指向 Agent **登录之后**的节点。服务端可能改写编码，不要假设一定是 `StarAgent1` | 命令发到离线节点 |
| 4 | 应用 **`StarWebApi`** 的 **`AllowControlNodes`** 包含该节点或 `*` | API 返回「无权操作！」 |

---

## 2. 档位（基建）

默认验收不碰本机 `Bin\Data`，也不要求开着 StarAgent。

```powershell
# CI：L0 指令函数 + L1 控制台接口 + L2 真节点通道
powershell -File Doc/scripts/deploy-log/run_verify.ps1 -Profile CI

# 只要某一档
powershell -File Doc/scripts/deploy-log/run_verify.ps1 -Profile L2
```

| 档 | 命令含义 | 证明 |
|----|----------|------|
| L0 | `ClientTest` `DeployLogCommands` | 读文件函数本身 |
| L1 | `Stardust.WebApiTests` `DeploymentDeployLogApiTests` | 控制台接口：未配置目录、缺文件、无效 id、合并目录 |
| L2 | `ServerTest` `DeployLogNodeCommand` | 真 StarServer + 真 StarClient + `ServiceManager.Attach` 后 list/tail |
| L3 | `.temp/smoke_deploy_log.py` 且 `DEPLOY_LOG_E2E=1` | 本机 `-run` 进程读磁盘。先执行 `start_local_agent.ps1`，窗口不要按键 |

L2 在 2026-11-03 通过：`DeployLog_ListAndTail_UsesRealHandler` 1/1，约 5 秒。它跟其它 ServerTests 共用 `Bin\Data\Stardust.db`，结束时删掉自己插入的行，不启动本机 Agent。节点登录若报 `The operation was canceled`，先确认 `Bin\Data\ip.gz` 存在（夹具会复制到测试输出目录），并在同一个 `StarClient` 上重试。

L3 会改本机 `Bin\Data` 里的部署节点。技能说明见 `.agents/skills/stardust-node-test/SKILL.md`。

```powershell
# 写库：StarWebApi 可控节点、样例日志目录（仅 L3）
python Doc/scripts/deploy-log/setup_local.py --apply
```

---

## 3. StarAgent（只在 L3 需要）

L0–L2 不启动 Agent。L3 用仓库构建的一次性进程，不安装 Windows 服务，不需要管理员：

```powershell
powershell -File Doc/scripts/deploy-log/start_local_stack.ps1
powershell -File Doc/scripts/deploy-log/start_local_agent.ps1
```

`start_local_agent.ps1` 执行 `Bin\Agent\net10.0\StarAgent.exe -run`，新开一个窗口。出现「任意键结束模拟运行」后不要按键，否则进程退出。标准输入被重定向时 `-run` 会马上退出，所以不能在脚本里把输出重定向走。

本地面板端口用 5510，避免和已安装服务的 5500 冲突。`Config\Star.config` 的 `Server` 必须是 `http://127.0.0.1:6600`。已安装的「星尘代理」服务可以继续跑，不要为了 L3 去 `-install` 替换它。

停已安装服务才会申请 UAC：`Doc/scripts/deploy-log/stop_local_agent.ps1`。

Agent 登录后的节点编码以服务端回写为准。`AppDeployNode` 要指向这个节点，否则 6681 的命令到不了当前进程。

### 3.3 样例日志目录

默认（可被 `setup_local.py` 写入 `AppDeployNode#1`）：

`{仓库根}/.temp/deploy-log-smoke/smoke-app.log`

内容需含一行 `deploy-log-smoke-ok`（脚本会自动生成）。

---

## 4. 手工起控制面（不用脚本时）

```powershell
cd D:\src\os-proj\X\Stardust\Bin\Server
dotnet StarServer.dll --urls http://127.0.0.1:6600

cd D:\src\os-proj\X\Stardust\Bin\WebApi
dotnet StarWebApi.dll --urls http://127.0.0.1:6681
```

登录冒烟默认：**admin / admin**（`Bin\Data\Membership.db`，与 CodeBuddy 前端 `Admin@1234` 不是同一套）。

---

## 5. 注册中心 ACL（为何曾「无权操作」）

StarWebApi 通过应用身份调用 `Node/SendCommand`。`StarApp.AllowControlNodes` 为空且 `AllowControlNodesWhenEmpty=false` 时，**一律拒绝**。

本地联调（仅本机 Bin\Data）可执行：

```sql
UPDATE StarApp SET AllowControlNodes='*' WHERE Name='StarWebApi';
```

`setup_local.py --apply` 会代做（勿用于生产）。

---

## 6. 模拟 / 复用环境（CI、同事机器）

| 产物 | 作用 |
|------|------|
| `Doc/scripts/deploy-log/setup_local.py` | 幂等写库 + 生成样例日志 |
| `Doc/scripts/deploy-log/local.env.json` | 记录本次 `-NodeCode`、`-DeployNodeId`、路径（apply 后生成，可 gitignore） |
| `.temp/smoke_deploy_log.py` | HTTP 负例 + 可选全链路 |
| `DEPLOY_LOG_E2E=1` | 强制全链路，失败即非 0 退出 |

新机器最小步骤：**装依赖 → k3s 不需要 → 起 6600/6681 → 对齐 Agent → `setup_local.py --apply` → smoke**。

---

## 7. 前端

逻辑单测（不依赖 Agent）：

```powershell
cd Stardust.Web.Vue
pnpm run test:unit -- deployLog.logic.test.ts
```

UI 走查：控制台部署节点 → **查看日志**（需后端 E2E 已通）。

---

## 8. 故障速查

| 现象 | 优先检查 |
|------|----------|
| 无权操作 | `StarWebApi.AllowControlNodes`、是否连对 6600 |
| 15s / 25s 超时 | Agent 是否在线；`Star.config` Server 是否本地 6600；Node.Code 是否一致 |
| 未配置日志目录 | `AppDeployNode.LogDirectory`；API 已用 `Find(_.Id==id)`，改库后无需重启 |
| PUT 部署节点 405 | 前端保存日志目录可能失败；联调可用 `setup_local.py` 写库 |
