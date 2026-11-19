# __fixtures__ — 真实后端响应体夹具（task-91）

只放**真实捕获**的响应体，禁止手造业务数据。测试通过 `import body from '...json'` 使用。

| 文件 | 来源 | sha16 | 用途 |
| --- | --- | --- | --- |
| `appdeploynode-detail.212.json` | `GET http://10.188.32.212:6681/api/Deployment/AppDeployNode/76`（2026-11-12 11:05 前后，task-91 只读取证，原始件 `.forgeself/task91-212-detail-76.json`） | `FC3B7BEDD548C024` | 212 旧构建 DETAIL：36 键**缺 `nodeId`**（有 `nodeName`/`iP`/`logDirectory`/`logFilePattern`）——用户报障「编辑部署节点物理节点没回显」的缺陷形状 |
| `appdeploynode-detail.current.json` | `GET http://localhost:6681/api/Deployment/AppDeployNode/10`（2026-11-12 11:03 本地栈拉起取证，原始件 `.forgeself/task91-local-detail-10.json`；数据链 `Bin\WebApi\appsettings.json` → `..\Data\*.db` 本地 SQLite，已核验） | `62CA6BC76DEC639D` | 当前检出栈 DETAIL：35 键**含 `nodeId=1`**——修复后（部署窗②重发 212）应有形状 |

> 212 取证走只读 GET + 每会话一发登录（AGENTS §4①）；本地栈按 j-appcols task-92 同日先例：拉起→只读 GET→立即停→披露。
