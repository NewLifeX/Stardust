# DEPLOY-3 前端 — 节点应用文件日志（需求说明）

> 版本：v1.0 | 日期：2026-11-03  
> 关联后端：[DEPLOY-1-节点应用日志.md](DEPLOY-1-节点应用日志.md)、[DEPLOY-2-本地应用日志联调环境.md](DEPLOY-2-本地应用日志联调环境.md)  
> 目标读者：Stardust.Web.Vue（Console / StardustLayout2）前端开发、测试、验收  
> 状态：**P1 MVP 已在 Layout2 有参考实现**；新控制台页面需按本文对齐或迁移

---

## 1. 产品定位

### 1.1 要解决什么问题

运维在控制台查看**部署在物理节点上的应用**写在**本地磁盘**的日志文件（如 `app.log`），而不是：

| 能力 | 路径 / 名称 | 与本需求关系 |
|------|-------------|--------------|
| **SDK 上报日志** | 应用工作台页签「日志」→ `AppLog.vue` → `/Registry/AppClientLog` | **不是**本需求；无文件路径、无 tail 节点磁盘 |
| **StarAgent 自身日志** | 节点详情「抓日志」等 | **不是**业务应用目录 |
| **发布中心「查看日志」** | `ReleaseCenter.vue` 等 | 多为**构建/流水线**日志，当前多为「待接入」；**不是**部署节点文件日志 |
| **SSH / bash tail** | 节点远程命令 | 应急手段；**日常入口必须是控制台 + API** |

本需求在文案上统一称为：**节点文件日志** / **部署节点本地文件日志**。抽屉标题示例：**节点文件日志**。

### 1.2 用户价值（P1）

1. 在**部署节点**行上一点「查看日志」，列出匹配规则的日志文件并阅读**文件尾部**内容。  
2. 在**部署节点编辑**里配置日志目录与匹配规则（可覆盖部署集默认）。  
3. 失败时看到**可行动**的错误（未配置目录、节点离线、Agent 未升级、无权操作等），而不是空白或假成功。

### 1.3 分期（前端视角）

| 阶段 | 前端范围 | 不在范围 |
|------|----------|----------|
| **P1 MVP** | 入口 + 抽屉 + 配置字段 + 手动刷新 + 错误透传 | 自动轮询 tail、向上翻页、下载、K8s Pod 日志 |
| **P2（2026-11-04 已交付）** | 抽屉自动轮询开关 + 1~5 秒间隔下拉（轮询走增量 offset）；配套 MCP 工具见 DEPLOY-1 §11 | 向上翻页、下载、K8s Pod 日志 |
| **P3（2026-11-04 已交付）** | 准实时 tail：轮询增量追新（offset=上次 fileSize，内容封顶 1MB，失败自动停止并提示） | SSE/WebSocket（留作后续可选增强） |

---

## 2. 信息架构与入口

### 2.1 主入口（必须）

| 场景 | 路由 / 容器 | 用户操作 | 打开组件 |
|------|-------------|----------|----------|
| **应用工作台 · 部署 · 节点** | **`/app/overview/:appId`**（新 Console 契约；页内二级区切到「部署」→「部署节点」） | `App.vue` → `ShipPanel` → 表格行 **查看日志** | `DeployLogDrawer` |
| **同能力其它 tab 深链** | `/app/:tab/:id`（如 `ship`/`history` 等，以 `App.vue` + `workbench.logic` 为准） | 部署区仍嵌 `ShipPanel` 时同上 | 同上 |
| **全局部署页（若有）** | `/ship` 等（若仍注册） | 同上，共用 `ShipPanel` | 同上 |

**行级数据要求**：点击时传入 **`AppDeployNode.Id`**（部署节点主键，非 `NodeId`、非应用 `AppId`）。参考：`ShipPanel.openDeployLogs(n)` 使用 `n.id` 作为 `deployNodeId`。

**并发**：节点行正在下发 install/start/stop 等（`busyKey` 非空）时，「查看日志」应禁用或忽略点击（与现网 `ShipPanel` 一致）。

### 2.2 配置入口（必须）

| 场景 | 组件 | 字段 |
|------|------|------|
| 新增/编辑**部署节点** | `NodeFormDrawer`，`kind === 'deploy'` | `logDirectory`、`logFilePattern` |
| **部署集**级默认（后端已支持） | 建议在 `DeployEditDrawer` **补字段**（当前参考实现未暴露） | `logDirectory`、`logFilePattern` |

合并规则（与后端一致，文案可展示在表单 hint）：

- 节点 `logDirectory` 非空 → 用节点值；否则用部署集 `LogDirectory`。  
- 节点 `logFilePattern` 非空 → 用节点值；否则用部署集；都空 → `*.log`。  
- 相对路径与 **EffectiveWorkingDirectory** 组合（见 DEPLOY-1）。

### 2.3 明确不做为 P1 入口

- 应用工作台 **「日志」页签**（`AppLog`）：保持 SDK 日志，不在此页做文件 tail。  
- 应用列表 / 应用详情顶栏「查看日志」：若未绑定部署节点，**不要**与本能力混用；需要单独产品决策后再做深链。  
- 编译节点（`kind === 'build'`）：无文件日志能力，无「查看日志」按钮。

---

## 3. 交互与界面规格（P1）

### 3.1 抽屉 `DeployLogDrawer`

**Props**

| 属性 | 类型 | 说明 |
|------|------|------|
| `open` | boolean | 是否显示 |
| `deployNodeId` | number | `AppDeployNode.Id` |
| `nodeName` | string | 展示用，如物理节点名 |

**布局（自上而下）**

1. **标题**：节点文件日志  
2. **副标题**：`{nodeName} · 部署节点 #{deployNodeId}`  
3. **说明文案**（固定一段，11–12px 次要色）：  
   - 说明数据来自 StarAgent 读取节点磁盘；  
   - 与「应用日志」页签（SDK 上报）不同；  
   - 未配置目录请到部署节点编辑填写。  
4. **工具行**
   - 下拉：**日志文件**（展示 `name (友好大小)`，如 `app.log (1.5 MB)`；无文件时占位「无匹配文件」/「加载文件列表…」）
   - 按钮：**刷新**（同时重拉列表；若已选文件则再 tail）
   - 按钮：**自动刷新**（P2，`data-spec="deploy-log-auto-toggle"`；开启后变 primary 并显示「自动刷新中 · 每 Ns」，再点停止）
   - 下拉：**间隔**（P2，`data-spec="deploy-log-interval"`；1~5 秒，默认 2s；未开启自动刷新时禁用，title 说明原因）
   - 按钮：**加载更早**（P2，`data-spec="deploy-log-earlier"`；读当前窗口之前一页（256KB）并裁齐前置拼接；自动刷新开启/请求中/已到窗口起点/累计超 2MB 时禁用，原因见 title 与提示行）
   - 按钮：**下载**（P2，`data-spec="deploy-log-download"`；导出当前窗口内容为 `{文件名}.{时间戳}.log`；内容为空时禁用）
5. **错误区**：`message` 全文，危险色，带 `data-spec="deploy-log-error"`  
6. **截断提示**（仅当 `truncated === true`）：说明当前仅为尾部片段，更大范围后续版本支持  
7. **内容区**：`<pre>` 等宽、可滚动（建议 max-height ~420px）、`whitespace-pre-wrap`，显示 tail 文本；加载中显示「读取中…」，无内容显示「—」

**行为**

| 事件 | 行为 |
|------|------|
| 打开抽屉 | 清空上次选中文件与内容；调用 `LogFiles`；若有文件则默认选第一项并自动 `LogTail` |
| 切换文件 | 自动 `LogTail`（重置增量基线） |
| 点击刷新 | 重新 `LogFiles` + 必要时 `LogTail`（整体替换，不增量） |
| 开启自动刷新 | 立即拉一次给反馈，然后按间隔定时轮询；轮询带 `offset=上次 file_size`，经 `applyTailSnapshot` 合并（offset 接续且文件未回卷 → 追加新增，否则整体替换）；进行中跳过下一拍，防请求堆积 |
| 轮询失败 | **自动停止**轮询，错误区显示 `{message}（自动刷新已停止）` |
| 调整间隔 | 立即按新间隔重启定时器（不重置内容） |
| 加载更早 | 以 `offset=max(1, 窗口起点-256KB)` 读一页，与当前窗口取交集裁齐后前置拼接（`prependTailPage`）；窗口起点更新为本页 offset。**自动刷新开启时禁用**（轮询的替换语义会冲掉回看内容）；Agent offset=0 语义是「读尾部」，故文件开头 0 字节窗口不可达（windowStart≤1 时禁用并提示） |
| 下载 | 导出当前窗口内容（Blob + a.download，`{文件名}.{yyyyMMddHHmmss}.log`）；Agent 单次最多 512KB，导出的是抽屉已加载内容而非整文件 |
| 关闭抽屉 / 组件卸载 | 停止定时器；重开抽屉若开关仍开启则继续轮询 |

**Loading**

- 列表请求中：禁用下拉与刷新，或展示「加载中…」  
- Tail 请求中：刷新按钮可显示「加载中…」；内容区可显示「读取中…」

### 3.2 部署节点表单字段

在 `NodeFormDrawer`（deploy）中已有规格，新页面需保持一致：

| 字段 | 控件 | 占位 / 说明 |
|------|------|-------------|
| 日志目录 | 单行文本 | placeholder：`如 logs 或 D:/app/logs`；hint：空=跟随部署集；相对路径基于工作目录 |
| 日志文件匹配 | 单行文本 | 默认展示/占位 `*.log` |

保存：走现有部署节点 PUT/POST API（`deploy.logic` 的 `deployNodePutBody` 已含 `logDirectory` / `logFilePattern`）。

### 3.3 部署集级默认（建议 P1 补齐）

后端 `AppDeploy` 已有 `LogDirectory`、`LogFilePattern`。前端 **DeployEditDrawer / DeployEditModel** 当前未映射，建议在 P1 一并补上，避免只能逐节点配置：

- `toDeployEditModel` / `deployPutBody` 增加两字段  
- 表单增加与节点表单相同语义的两行输入  

若本迭代不做，需在部署节点说明中写清「部署集默认请在旧版 MVC 或数据库配置」，并在「未配置」错误中链到编辑抽屉。

---

## 4. 接口契约（WebApi）

基础路径：与现有 Console 一致，经 `@newlifex/cube-vue` 的 `request`，前缀一般为 **`/api`**（以项目 `request` 封装为准）。

### 4.1 列表文件

```http
GET /Deployment/AppDeployNode/LogFiles?id={deployNodeId}
```

**权限**：`AppDeployNode` **Detail**（与 Cube 权限体系一致）。

**成功**（`code === 0`，`data` 为列表结果）：

```json
{
  "files": [
    { "name": "app.log", "size": 12345, "lastWriteUtc": "2026-11-03T02:00:00Z" }
  ]
}
```

后端 C# 可能序列化为 PascalCase；前端 **必须** 同时兼容 `Name`/`name`、`Size`/`size`、`LastWriteUtc`/`lastWriteUtc`（见 `deployLog.logic.ts` 的 `pick`）。

**失败**（`code !== 0` 或 HTTP 异常）：

- 典型：`Log directory is not configured for this deploy node...`（未配置目录）  
- 典型：`Timed out waiting for the node...`（节点离线 / Agent 未响应）  
- 典型：`无权操作`（StarWebApi 应用 `AllowControlNodes`）  
- 典型：`[{id}]不存在`（无效 deployNodeId）

前端：**原样展示** `message`（经 `okRes` 解析），不要吞掉。

### 4.2 读取尾部

```http
GET /Deployment/AppDeployNode/LogTail?id={deployNodeId}&file={fileName}&offset=0&maxBytes=262144
```

| 参数 | 说明 |
|------|------|
| `file` | **仅文件名**，无路径、无 `..` |
| `offset` | P1 固定传 `0`（读尾部逻辑在 Agent；P2 再做向上翻页） |
| `maxBytes` | 默认 `262144`（256KB），与后端默认一致；上限 512KB |

**成功** `data`：

```json
{
  "content": "....",
  "offset": 0,
  "fileSize": 999999,
  "truncated": false
}
```

同样兼容 PascalCase。`truncated === true` 时展示截断提示。

**失败**：

- 未传 `file`：`请指定日志文件名`  
- 其它同列表接口

### 4.3 信封解析

统一使用 `okRes(res)`（`appsModel.ts`）：

- 成功：`code === 0 | 200 | 1` 或 `success === true`  
- 失败：展示 `message`，**不要**把 `code !== 0` 当成功

业务逻辑层建议独立在 **`deployLog.logic.ts`**（便于 Vitest），组件只负责 UI 状态。

---

## 5. 状态与文案

### 5.1 前端本地校验（logic 层）

| 条件 | 文案 |
|------|------|
| `deployNodeId <= 0` | 缺少部署节点编号 |
| tail 时 file 为空 | 请选择日志文件 |

### 5.2 后端/网络错误（展示 message，可补充操作指引）

| 用户可见关键词 | 建议界面提示（可选第二行） |
|----------------|----------------------------|
| Log directory / 未配置 | 请在部署节点（或部署集）编辑中填写日志目录 |
| Timed out / 超时 | 检查节点是否在线、StarAgent 是否已升级到含 deploy/log 的版本 |
| 无权操作 | 联系管理员配置 StarWebApi 的 AllowControlNodes |
| 节点失败 / Empty response | 展示原文；提示查看节点 Agent 日志 |

### 5.3 空数据

| 情况 | 展示 |
|------|------|
| `files.length === 0` 且成功 | 下拉「无匹配文件」；内容区「—」 |
| 目录存在但 pattern 无匹配 | 同上 |

---

## 6. 权限与安全

- 仅登录且具备 **部署节点 Detail** 的用户可调用 LogFiles/LogTail。  
- 前端不拼接 shell、不通过 bash 读文件。  
- 文件名仅来自下拉选项（Agent 列表结果），不让用户自由输入路径（防 `../`）。  
- P1 不在浏览器持久化日志内容。

---

## 7. 与 Vue 重构的落地映射

仓库 **Stardust.Web.Vue** 内已有 P1 参考实现，新 Console 开发应**迁移或复用**，避免重复造轮子：

| 职责 | 路径 |
|------|------|
| API + 解析 | `src/layouts/StardustLayout2/deployLog.logic.ts` |
| 单测 | `src/layouts/StardustLayout2/deployLog.logic.test.ts` |
| 抽屉 UI | `src/layouts/StardustLayout2/DeployLogDrawer.vue` |
| 表格「查看日志」 | `src/layouts/StardustLayout2/DeployNodes.vue`（`data-spec="ship-node-logs"`） |
| 容器接线 | `apps/stardust/src/views/Console/ShipPanel.vue` |
| 节点配置 | `src/layouts/StardustLayout2/NodeFormDrawer.vue`（`data-spec="node-form-logdir"`） |
| 节点 CRUD 字段映射 | `src/layouts/StardustLayout2/deploy.logic.ts` |

**设计系统**：使用 StardustLayout2 现有 `Drawer`、`Btn`、CSS 变量（`--el-*`），与 `ShipPanel` / 部署节点表一致。

**组件测试 / E2E**：为关键 DOM 保留 `data-spec`：

- `deploy-log-drawer`  
- `deploy-log-file-select`  
- `deploy-log-refresh`  
- `deploy-log-error`  
- `deploy-log-content`  

---

## 8. 验收标准（P1）

### 8.1 功能

- [ ] 在应用工作台 → 部署 → 部署节点，每行可点「查看日志」，打开抽屉。  
- [ ] 已配置日志目录且节点在线、Agent 已升级：列出文件，默认选中第一个，展示 tail 内容。  
- [ ] 切换文件后内容更新。  
- [ ] 点击刷新后列表与内容更新。  
- [ ] 未配置目录：抽屉内展示明确错误，且不假装成功。  
- [ ] 节点离线或超时：展示超时类错误。  
- [ ] 部署节点编辑可保存 `logDirectory` / `logFilePattern`，保存后再次打开日志生效（需后端与 Agent 配合）。  

### 8.2 非功能

- [ ] `deployLog.logic.test.ts` 全部通过。  
- [ ] 与 `AppLog` 页签文案/标题区分清晰，用户不会误以为是同一数据源。  
- [ ] 无权限时错误可见。  

### 8.3 联调

- 后端/automation：见 DEPLOY-1 §9（L0/L1/L2）；全链路本机见 DEPLOY-2 L3。  
- 前端 logic 单测不依赖真实 Agent。

---

## 9. 测试用例摘要（前端）

| 编号 | 场景 | 预期 |
|------|------|------|
| F-01 | `fetchDeployLogFiles(id=0)` | ok=false，缺少部署节点编号 |
| F-02 | LogFiles 返回 code≠0 | message 透传，files=[] |
| F-03 | LogFiles 返回 PascalCase files | 映射为 name/size/lastWriteUtc |
| F-04 | tail 空文件名 | ok=false，请选择日志文件 |
| F-05 | tail 成功 truncated | UI 显示截断提示 |
| F-06 | 打开抽屉 | 自动请求 LogFiles + 首文件 LogTail |
| F-07 | deploy.logic 节点 body | 含 logDirectory、logFilePattern |
| F-08 | `applyTailSnapshot` 首读（上次 file_size=0） | 整体替换，非追加 |
| F-09 | offset 接续上次 file_size 且文件增长 | 追加新增内容 |
| F-10 | offset 不接续（无新增回读尾部 / 回卷变小） | 整体替换，不重复拼接 |
| F-11 | 追加超过 1MB 上限 | 从头截断到 `MAX_TAIL_KEEP` |
| F-12 | 轮询请求失败 | 自动刷新关闭，错误区含「自动刷新已停止」 |
| F-13 | 文件大小友好格式 | 0 B / 999 B / 1.0 KB / 1.5 MB（`formatFileSize`） |
| F-14 | 向上翻页 | `earlierOffset` 最小为 1；`canLoadEarlier`：windowStart>1 且累计 <2MB；`prependTailPage` 与窗口交集裁齐前置拼接、重叠不重复 |
| F-15 | 下载 | 当前窗口内容导出 `.log`；内容为空时按钮禁用 |

（实现见 `deployLog.logic.test.ts`（20 用例）、`deploy.logic.test.ts`。MCP 工具集成测试见 DEPLOY-1 §9。）

---

## 10. 依赖与发布说明

| 依赖 | 说明 |
|------|------|
| StarWebApi | LogFiles / LogTail 已发布 |
| StarServer | Node/SendCommand 通道 |
| StarAgent | 需含 `deploy/log/list|tail`（与 WebApi 同批次或更早升级节点） |
| 配置 | StarWebApi `AllowControlNodes`、AppDeployNode 日志目录 |

前端静态资源单独发布时，**仅发前端不会生效**，必须保证 API 与 Agent 能力已上线。

---

## 11. 附录：与应用工作台页签对照

| 页签 | 组件 | 数据源 | 本需求 |
|------|------|--------|--------|
| 日志 | `AppLog.vue` | AppClientLog 分表 | 否 |
| 部署 → 节点 | `DeployLogDrawer` | 节点磁盘文件 | **是** |

产品文案建议：

- 页签「日志」→ **应用日志（SDK 上报）**  
- 抽屉「节点文件日志」→ **节点本地文件**

---

## 12. 变更记录

| 日期 | 说明 |
|------|------|
| 2026-11-03 | 初版：对齐 DEPLOY-1、Stardust.Web.Vue 参考实现与 Console ShipPanel 接线 |
| 2026-11-04 | P2/P3 交付：自动轮询开关 + 1~5s 间隔下拉；`applyTailSnapshot` 增量合并（1MB 封顶）；失败自动停止。测试 F-08~F-12 |
| 2026-11-04（二） | P2 遗留交付：向上翻页（256KB/页、累计 2MB 上限、与自动刷新互斥）、下载当前窗口、文件大小友好格式。测试 F-13~F-15 |
