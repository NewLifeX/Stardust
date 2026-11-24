# NODE-33 局域网唤醒（WOL）

> 版本：v1.0 | 日期：2026-11-23
> 对应需求：NODE-33 节点唤醒
> 关联：[NODE-9 指令下发](NODE-9-指令下发.md) | [SVR-8 远程命令执行安全](SVR-8-远程命令执行安全.md)

---

## 1. 目标

停机/休眠的机器无法远程升级、部署、排障。平台在节点列表提供「唤醒」操作：由目标所在局域网内的在线 StarAgent 代发 Wake-on-LAN 魔术包，把目标机器唤醒后自动重新上线。

云上的 StarServer 无法把广播送进家庭/办公室局域网，因此采用“同网段在线节点代发”的方式。

## 2. 流程

```
StarWeb（Vue / MVC）
  │  GET /Nodes/Node/WakeUp?id=
  ▼
NodeController.WakeUp
  │  1. 校验节点存在、有 MAC
  │  2. NodeWakeService.FindRelays 挑选同网段在线节点
  │  3. 对每个代发节点下发 node/wakeonlan，参数 {"macs":"...","code":"...","name":"..."}
  ▼
StarAgent（代发节点）
  │  ParseMacs → BuildPacket → 逐网卡 UDP 广播（端口 9/7）
  ▼
目标网卡收到魔术包 → 机器上电 → StarAgent 重新登录 → 平台显示在线
```

## 3. 代发节点选择规则

按以下优先级取第一个非空结果，排除目标自身，最多 5 个，按网关延迟升序、心跳时间降序：

| 优先级 | 依据 | 说明 |
|---|---|---|
| 1 | 网关 MAC 相同 | `Node.Gateway` 形如 `IP/MAC`，同局域网最可靠的判断 |
| 2 | 网关 IP 相同 | 部分 Linux 机器取不到网关 MAC 时兜底 |
| 3 | 公网 IP 相同 | 家庭 NAT 场景兜底（`Node.LastLoginIP`） |
| 4 | 私有网段 /24 相同 | 换路由器/多网卡导致网关变化时兜底 |

在线节点来自 `NodeOnline`，只取最近 30 分钟仍有心跳的记录，并要求仍能查到对应 `Node`。目标有项目归属时，代发节点必须与目标同项目，避免把目标 MAC 与节点信息暴露给其它项目的命令记录。

## 4. Agent 命令

`node/wakeonlan`，参数支持 JSON 或纯 MAC 文本：

```json
{"macs":"AA-BB-CC-DD-EE-FF,11-22-33-44-55-66","code":"目标节点编码","name":"目标节点名"}
```

- MAC 支持 `AA-BB-CC-DD-EE-FF`、`aa:bb:cc:dd:ee:ff`、`aabb.ccdd.eeff`，多个用逗号/分号/空白分隔，重复项去重。
- 魔术包：6 个 `0xFF` + 16 次 MAC，共 102 字节。
- 逐网卡绑定本机地址后发送到子网定向广播与 `255.255.255.255` 的 UDP 9、7 端口，多网卡机器每张网卡都发一次。
- 3 秒内重复调用直接忽略，避免命令重试造成广播风暴。
- 返回英文技术结果（如 `WOL sent: 2 MAC(s), 4 packet(s)`），平台不等待回包。

## 5. 限制

1. 目标机器必须在 BIOS/UEFI 与网卡驱动中启用 WOL；Windows「快速启动」可能需要在网卡电源管理里允许魔术包唤醒。
2. 必须有同网段在线 Agent 代发；只有云上 StarServer 时无法直接唤醒。
3. 广播不能跨 VLAN/网段；同网段判断失效时按上表逐级兜底，仍无结果会明确提示。
4. Wi-Fi 网卡通常不支持 WOL（需 WOWLAN），有线网卡成功率高。
5. 原生 Agent（Go/Rust/C++）暂不支持该命令，会回复“不支持命令”。

后续可选项：StarServer/StarWeb 本机直发 WOL。只有在控制台与目标处于同一局域网（例如全部自建在同一机房/家庭网络）时才有意义；云上部署发了也到不了目标网段，因此本期不做，默认也不应开启。

## 6. 安全

- 控制台操作要求 `Update` 权限。
- 服务端要求下发命令的应用具备代发节点的控制权限（`AllowControlNodes`），与其它远程命令一致；控制台应用通常配置为 `*`。
- 命令只接受 MAC 地址，广播地址由 Agent 本机网卡计算，不能用于向任意主机/端口发包。
- 命令下发记录写入 `NodeCommand`（操作人、IP、时间、参数），可审计。
- Agent 侧限频，防止批量误操作或命令重试放大。

## 7. 测试

- `Stardust.Tests/WakeOnLanTests`：魔术包结构、MAC 多格式解析、非法输入。
- `Stardust.ServerTests/NodeWakeServiceTests`：网关 MAC/网关 IP/公网 IP/同网段兜底、排除自身与重复会话、数量上限。
- `Stardust.WebApiTests/Areas/NodeWakeTests`：节点不存在、无 MAC、无代发节点、匿名拒绝。
- 真机验证：目标机器休眠 → 控制台点唤醒 → 30~60 秒内重新上线。
