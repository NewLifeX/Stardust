# Specification Quality Checklist: MCP HTTP 服务

**Purpose**: 验证 spec 完整性与质量，确保进入规划阶段前规格就绪
**Created**: 2026-07-16
**Updated**: 2026-07-16（补充审视：search_resources 提升为工具 + 补充 6 个运维/审计 action → 5 工具 + 27 action）
**Feature**: [spec.md](./spec.md)

## Content Quality

- [x] 无实现细节（语言、框架、API 细节）
- [x] 聚焦用户价值与业务需求
- [x] 面向非技术利益相关者可读
- [x] 所有必填章节已完成

## Requirement Completeness

- [x] 无 [NEEDS CLARIFICATION] 标记残留
- [x] 需求可测试且无歧义
- [x] 成功标准可度量
- [x] 成功标准与技术无关（无实现细节）
- [x] 所有验收场景已定义（9 个场景覆盖：LLM 接入/查询/写操作/搜索/Token 创建/禁用/动作过滤/过期/扩展）
- [x] 边缘情况已识别（Token 过期、Token 禁用、资源未授权、动作被禁用、间接资源校验、多资源同时校验）
- [x] 范围已明确界定（含 Out of Scope）
- [x] 依赖与假设已识别

## Clarify 会话决议（2026-07-16）

通过 5 个高影响问题完成澄清，关键决议：

1. **工具结构**：4 个 MCP 工具 = `list_authorized_resources` + `get_resource` + `list_actions` + `invoke_action`。`list_actions` 解决了"LLM 如何知道能调什么"的关键缺口
2. **扩展机制**：纯代码驱动（实现 `IMcpAction` 接口，反射自动注册，无 DB 配置表）
3. **资源类型**：`get_resource` 支持 6 类核心（project/node/app/deploy/pipeline/service），其余通过 action 间接访问
4. **授权模型**：仅框架层统一校验（action 声明 `RequiredResource`，框架提取资源 ID 校验，action 内不再校验）
5. **首批动作**：精简到 22 个高频 action（4+4+2+7+2+2+1），补充 `search_resources` 和 `deploy_list_nodes`

## 补充审视决议（2026-07-16）

在 22 action 基础上重新审视工具边界与运维覆盖度：

1. **search_resources 提升为第 5 个 MCP 工具**：与 `get_resource` 形成"按关键字搜索→按 ID 获取"的查询原语对，LLM 工作流语义一致
2. **补充 get_audit_log action**（system 模块）：LLM 自省用，查当前 Token 最近的调用记录，便于多轮对话回顾上下文
3. **补充 5 个运维 action**：
   - `app_restart` / `app_stop` / `app_start`（app 模块）：应用生命周期控制，通过 `app_send_command` 下发对应命令实现
   - `deploy_compile`（deploy 模块）：仅编译不部署，对应 `DeployService.Compile`
   - `pipeline_cancel`（deploy 模块）：取消正在运行的流水线
4. **最终规模**：5 个 MCP 工具 + 27 个 action（4+7+2+9+2+2+1）

## Feature Readiness

- [x] 所有功能需求有明确验收标准
- [x] 用户场景覆盖主要流程（含代码扩展场景）
- [x] 功能满足成功标准中定义的可度量结果
- [x] 规格中无实现细节泄漏
- [x] 5 工具 + 27 action 边界清晰，无重叠
- [x] 资源授权校验机制明确（直接/间接/多资源/列表过滤）
- [x] 扩展机制定义清晰（开发者添加实现类即可暴露新动作）
- [x] `search_resources` 与 `get_resource` 形成查询原语对，工具职责清晰
- [x] 运维 action 覆盖应用生命周期（restart/stop/start）+ 编译（compile）+ 流水线取消（cancel）

## Notes

### 关键设计调整（基于 clarify 会话）

1. **从 31 个工具收敛为 4 个工具 + 22 个 action**：避免工具列表臃肿，LLM 通过 `list_actions` 自发现可用动作及参数 schema
2. **代码驱动扩展机制**：无 DB 配置表，开发者添加 `IMcpAction` 实现类即可暴露新动作，符合 Stardust 现有风格
3. **框架层统一授权**：action 仅做业务合法性校验，资源授权校验统一由 `McpService` 框架层完成
4. **间接资源校验**：通过 `ResourceRequirement.Indirect + IndirectEntity` 声明，框架层自动反查（如 deploy_id → AppDeploy.ProjectId）

### 保留的设计

- `EnableMcp` 默认 `false`
- 独立 `McpToken` + `McpTokenResource` 两张表
- 三类资源 OR 关系（Project/Node/App）
- `IsAll=true` 通配授权
- `McpAudit` 审计表

### 待规划阶段处理

- `IMcpAction` 接口的具体实现细节（基类设计、依赖注入方式、反射扫描机制）
- `ResourceRequirement` 的间接查找实现（如何根据 `IndirectEntity` 反查 ProjectId/AppId）
- `InputSchema` 的具体 JSON Schema 结构定义
- `McpService` 的动作注册时序与并发安全
- 4 个 MCP 工具的具体实现结构
