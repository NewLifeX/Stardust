# Data Model — MCP HTTP 服务

**Created**: 2026-07-16
**Spec**: [spec.md](../spec.md)
**Research**: [research.md](./research.md)

## 概述

新增 3 张表到 `Stardust.Data/Platform/Model.xml`，遵循 XCode ORM 定义模式（参考 [GalaxyProject 定义](file:///d:/src/os-proj/X/Stardust/Stardust.Data/Platform/Model.xml#L44-L69)）。运行 `xcode` 工具自动生成 `{中文名}.cs` 和 `{中文名}.Biz.cs`，xcode 还会基于 `<CubeOutput>` 配置自动在 `Stardust.Web/Areas/Platform/Controllers/` 生成 Cube EntityController 骨架。

---

## 表 1：McpToken（MCP 令牌）

MCP 调用凭证，含启用状态、过期时间、调用统计。

### Model.xml 定义

```xml
<Table Name="McpToken" Description="MCP令牌。用于LLM/智能体调用MCP服务的资源授权凭证">
  <Columns>
    <Column Name="Id" DataType="Int32" Identity="True" PrimaryKey="True" Description="编号" />
    <Column Name="Name" DataType="String" Length="50" Master="True" Nullable="False" Description="名称。便于管理员识别，如运维助手-小明" />
    <Column Name="Token" DataType="String" Length="64" Nullable="False" Description="Token字符串。sdmcp_前缀+32位Base62随机字符，创建后不可修改" />
    <Column Name="Enable" DataType="Boolean" Description="启用" />
    <Column Name="ExpireTime" DataType="DateTime" Description="过期时间。空表示永不过期" />
    <Column Name="LastTime" DataType="DateTime" Description="最后调用时间" />
    <Column Name="LastIP" DataType="String" Length="50" Description="最后调用地址" />
    <Column Name="CallCount" DataType="Int32" Description="调用次数" />
    <Column Name="TraceId" DataType="String" Length="50" Category="扩展" Description="链路追踪" />
    <Column Name="CreateUserId" DataType="Int32" Map="XCode.Membership.User@ID@$@CreateUserName" Category="扩展" Description="创建者" />
    <Column Name="CreateTime" DataType="DateTime" Category="扩展" Description="创建时间" />
    <Column Name="CreateIP" DataType="String" Length="50" Category="扩展" Description="创建地址" />
    <Column Name="UpdateUserId" DataType="Int32" Map="XCode.Membership.User@ID@$@UpdateUserName" Category="扩展" Description="更新者" />
    <Column Name="UpdateTime" DataType="DateTime" Category="扩展" Description="更新时间" />
    <Column Name="UpdateIP" DataType="String" Length="50" Category="扩展" Description="更新地址" />
    <Column Name="Remark" DataType="String" Length="500" Category="扩展" Description="备注" />
  </Columns>
  <Indexes>
    <Index Columns="Token" Unique="True" />
    <Index Columns="Name" />
    <Index Columns="Enable" />
  </Indexes>
</Table>
```

### 字段说明

| 字段 | 类型 | 约束 | 说明 |
|---|---|---|---|
| Id | Int32 | PK, Identity | 主键 |
| Name | String(50) | NotNull, Master | Token 名称，便于管理员识别 |
| Token | String(64) | NotNull, Unique | Token 字符串，格式 `sdmcp_` + 32 位 Base62 |
| Enable | Boolean | - | 启用状态，false 时所有调用返回 -32001 |
| ExpireTime | DateTime | Nullable | 过期时间，null 表示永不过期 |
| LastTime | DateTime | Nullable | 最后调用时间（每次调用更新） |
| LastIP | String(50) | Nullable | 最后调用方 IP（每次调用更新） |
| CallCount | Int32 | - | 累计调用次数（每次调用自增） |
| TraceId | String(50) | Category=扩展 | 链路追踪 ID |
| CreateUserId | Int32 | Category=扩展, Map=User | 创建者用户 ID |
| CreateTime | DateTime | Category=扩展 | 创建时间 |
| CreateIP | String(50) | Category=扩展 | 创建 IP |
| UpdateUserId | Int32 | Category=扩展, Map=User | 更新者用户 ID |
| UpdateTime | DateTime | Category=扩展 | 更新时间 |
| UpdateIP | String(50) | Category=扩展 | 更新 IP |
| Remark | String(500) | Category=扩展 | 备注 |

### 索引

| 索引名 | 字段 | 唯一 | 用途 |
|---|---|---|---|
| `IU_McpToken_Token` | Token | ✓ | 按 Token 字符串快速查找（核心鉴权查询） |
| `IX_McpToken_Name` | Name | - | 按名称搜索 |
| `IX_McpToken_Enable` | Enable | - | 按启用状态过滤 |

### 关联

- **被 McpTokenResource 引用**：通过 `TokenId` 字段（弱关联，无 Map 属性，避免循环依赖）
- **被 McpAudit 引用**：通过 `TokenId` 字段（弱关联）

### 业务方法（`MCP令牌.Biz.cs`）

```csharp
public partial class McpToken : Entity<McpToken>
{
    static McpToken() => Meta.Factory.Table.InitData = InitData;

    /// <summary>生成Token字符串</summary>
    public static String GenerateToken() => "sdmcp_" + Rand.NextString(32, true, true, false);

    /// <summary>按Token字符串查找</summary>
    public static McpToken FindByToken(String token) => Find(_.Token, token);

    /// <summary>重置Token字符串</summary>
    public void Reset()
    {
        Token = GenerateToken();
        Update();
        WriteHistory("重置Token", true, "Token已重置", ManageProvider.UserHost);
    }

    /// <summary>校验Token是否有效</summary>
    public Boolean IsValid()
    {
        if (!Enable) return false;
        if (ExpireTime.HasValue && ExpireTime < DateTime.Now) return false;
        return true;
    }

    /// <summary>记录调用</summary>
    public void RecordCall(String ip)
    {
        CallCount++;
        LastTime = DateTime.Now;
        LastIP = ip;
        Update();
    }

    /// <summary>恒定时间比较Token字符串</summary>
    public static Boolean SafeEquals(String a, String b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        var result = 0;
        for (var i = 0; i < a.Length; i++) result |= (Byte)a[i] ^ (Byte)b[i];
        return result == 0;
    }

    private static void InitData() { /* 首次启动可插入默认数据 */ }
}
```

---

## 表 2：McpTokenResource（MCP 令牌资源授权）

Token 与资源（项目/节点/应用）的授权关系表。

### Model.xml 定义

```xml
<Table Name="McpTokenResource" Description="MCP令牌资源授权。Token与项目/节点/应用的授权关系">
  <Columns>
    <Column Name="Id" DataType="Int32" Identity="True" PrimaryKey="True" Description="编号" />
    <Column Name="TokenId" DataType="Int32" Map="Stardust.Data.Platform.McpToken@Id@Name@TokenName" Nullable="False" Description="令牌" />
    <Column Name="ResourceType" DataType="String" Length="20" Nullable="False" Description="资源类型。Project/Node/App" />
    <Column Name="ResourceId" DataType="Int32" Description="资源编号。对应GalaxyProject.Id/Node.ID/App.Id" />
    <Column Name="IsAll" DataType="Boolean" Description="全部资源。true时忽略ResourceId，授权该类型全部资源" />
    <Column Name="Enable" DataType="Boolean" Description="启用" />
    <Column Name="CreateTime" DataType="DateTime" Category="扩展" Description="授权时间" />
    <Column Name="CreateIP" DataType="String" Length="50" Category="扩展" Description="授权地址" />
    <Column Name="Remark" DataType="String" Length="500" Category="扩展" Description="备注" />
  </Columns>
  <Indexes>
    <Index Columns="TokenId,ResourceType,ResourceId" Unique="True" />
    <Index Columns="TokenId" />
    <Index Columns="ResourceType,ResourceId" />
  </Indexes>
</Table>
```

### 字段说明

| 字段 | 类型 | 约束 | 说明 |
|---|---|---|---|
| Id | Int32 | PK, Identity | 主键 |
| TokenId | Int32 | NotNull, Map=McpToken | 关联 McpToken.Id |
| ResourceType | String(20) | NotNull | `Project` / `Node` / `App` |
| ResourceId | Int32 | Nullable | 资源 ID（`IsAll=true` 时忽略） |
| IsAll | Boolean | - | 是否授权该类型全部资源 |
| Enable | Boolean | - | 启用状态（支持临时禁用某条授权） |
| CreateTime | DateTime | Category=扩展 | 授权时间 |
| CreateIP | String(50) | Category=扩展 | 授权 IP |
| Remark | String(500) | Category=扩展 | 备注 |

### 索引

| 索引名 | 字段 | 唯一 | 用途 |
|---|---|---|---|
| `IU_McpTokenResource_TokenId_ResourceType_ResourceId` | TokenId, ResourceType, ResourceId | ✓ | 防止重复授权 |
| `IX_McpTokenResource_TokenId` | TokenId | - | 按令牌查所有授权（鉴权核心查询） |
| `IX_McpTokenResource_ResourceType_ResourceId` | ResourceType, ResourceId | - | 反向查询（资源被哪些 Token 授权） |

### 关联

- **TokenId → McpToken.Id**：强关联（Map 属性），生成 `TokenName` 导航属性
- **ResourceType + ResourceId → GalaxyProject.Id / Node.ID / App.Id**：弱关联（多态），不建数据库外键

### 业务方法（`MCP令牌资源.Biz.cs`）

```csharp
public partial class McpTokenResource : Entity<McpTokenResource>
{
    /// <summary>按TokenId查所有授权</summary>
    public static IList<McpTokenResource> FindAllByToken(Int32 tokenId) 
        => FindAll(_.TokenId, tokenId);

    /// <summary>判断Token是否授权某资源</summary>
    public static Boolean IsAuthorized(Int32 tokenId, String resourceType, Int32 resourceId)
    {
        var list = FindAllByToken(tokenId);
        foreach (var r in list)
        {
            if (!r.Enable) continue;
            if (r.ResourceType != resourceType) continue;
            if (r.IsAll) return true;
            if (r.ResourceId == resourceId) return true;
        }
        return false;
    }

    /// <summary>获取Token授权的所有项目ID</summary>
    public static Int32[] GetAuthorizedProjectIds(Int32 tokenId)
    {
        var list = FindAllByToken(tokenId);
        var ids = new List<Int32>();
        foreach (var r in list.Where(r => r.Enable && r.ResourceType == "Project"))
        {
            if (r.IsAll) return null;  // null 表示全部项目
            ids.Add(r.ResourceId);
        }
        return ids.Count > 0 ? ids.Distinct().ToArray() : Array.Empty<Int32>();
    }

    /// <summary>同步Token资源授权（删除旧的，插入新的）</summary>
    public static void SyncByToken(Int32 tokenId, List<McpTokenResource> resources, String ip)
    {
        // 删除旧授权
        Delete(_.TokenId, tokenId);
        // 插入新授权
        foreach (var r in resources)
        {
            r.TokenId = tokenId;
            r.CreateTime = DateTime.Now;
            r.CreateIP = ip;
            r.Insert();
        }
    }
}
```

---

## 表 3：McpAudit（MCP 审计日志）

记录每次 MCP 工具调用的审计信息。

### Model.xml 定义

```xml
<Table Name="McpAudit" Description="MCP审计日志。记录每次MCP工具调用">
  <Columns>
    <Column Name="Id" DataType="Int64" Identity="True" PrimaryKey="True" Description="编号" />
    <Column Name="TokenId" DataType="Int32" Map="Stardust.Data.Platform.McpToken@Id@Name@TokenName" Description="令牌" />
    <Column Name="TokenName" DataType="String" Length="50" Description="令牌名称。快照，便于Token删除后审计" />
    <Column Name="ToolName" DataType="String" Length="50" Description="工具名。list_authorized_resources/search_resources/get_resource/list_actions/invoke_action" />
    <Column Name="ActionName" DataType="String" Length="50" Description="动作名。仅invoke_action时填写，如node_send_command" />
    <Column Name="CallerIp" DataType="String" Length="50" Description="调用方IP" />
    <Column Name="CallerUserAgent" DataType="String" Length="200" Description="客户端UA" />
    <Column Name="Arguments" DataType="String" Length="-1" Description="入参JSON。截断2000字符，敏感字段脱敏" />
    <Column Name="Success" DataType="Boolean" Description="成功" />
    <Column Name="ErrorMessage" DataType="String" Length="500" Description="错误信息" />
    <Column Name="Duration" DataType="Int32" Description="耗时。毫秒" />
    <Column Name="TraceId" DataType="String" Length="50" Category="扩展" Description="链路追踪" />
    <Column Name="CreateTime" DataType="DateTime" Category="扩展" Description="调用时间" />
  </Columns>
  <Indexes>
    <Index Columns="TokenId,Id" />
    <Index Columns="ToolName,CreateTime" />
    <Index Columns="ActionName,CreateTime" />
    <Index Columns="Success,CreateTime" />
    <Index Columns="CreateTime" />
  </Indexes>
</Table>
```

### 字段说明

| 字段 | 类型 | 约束 | 说明 |
|---|---|---|---|
| Id | Int64 | PK, Identity | 主键（使用 Int64 因预期数据量大） |
| TokenId | Int32 | Map=McpToken | 关联 McpToken.Id |
| TokenName | String(50) | - | Token 名称快照（Token 删除后仍可审计） |
| ToolName | String(50) | - | 工具名（5 个 MCP 工具之一） |
| ActionName | String(50) | Nullable | 动作名（仅 `invoke_action` 时填写） |
| CallerIp | String(50) | - | 调用方 IP |
| CallerUserAgent | String(200) | - | 客户端 User-Agent |
| Arguments | String(max) | - | 入参 JSON（截断 2000 字符，敏感字段脱敏） |
| Success | Boolean | - | 是否成功 |
| ErrorMessage | String(500) | Nullable | 失败原因 |
| Duration | Int32 | - | 耗时（毫秒） |
| TraceId | String(50) | Category=扩展 | 链路追踪 ID |
| CreateTime | DateTime | Category=扩展 | 调用时间 |

### 索引

| 索引名 | 字段 | 唯一 | 用途 |
|---|---|---|---|
| `IX_McpAudit_TokenId_Id` | TokenId, Id | - | 按 Token 查历史（`get_audit_log` 核心） |
| `IX_McpAudit_ToolName_CreateTime` | ToolName, CreateTime | - | 按工具名+时间筛选 |
| `IX_McpAudit_ActionName_CreateTime` | ActionName, CreateTime | - | 按动作名+时间筛选 |
| `IX_McpAudit_Success_CreateTime` | Success, CreateTime | - | 按成功/失败+时间筛选 |
| `IX_McpAudit_CreateTime` | CreateTime | - | 按时间排序（默认） |

### 关联

- **TokenId → McpToken.Id**：强关联（Map 属性），但保留 `TokenName` 快照字段
- **不删除**：即使 Token 被删除，McpAudit 记录保留（通过 `TokenName` 快照可识别）

### 业务方法（`MCP审计日志.Biz.cs`）

```csharp
public partial class McpAudit : Entity<McpAudit>
{
    /// <summary>按TokenId查审计日志</summary>
    public static IList<McpAudit> Search(Int32 tokenId, Boolean? success, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();
        if (tokenId > 0) exp &= _.TokenId == tokenId;
        if (success.HasValue) exp &= _.Success == success.Value;
        exp &= _.CreateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= (_.ActionName.Contains(key) | _.ToolName.Contains(key) | _.ErrorMessage.Contains(key));
        return FindAll(exp, page);
    }

    /// <summary>写入审计日志（异步）</summary>
    public static void WriteAsync(Int32 tokenId, String tokenName, String toolName, String actionName, 
        String ip, String ua, String arguments, Boolean success, String error, Int32 duration, String traceId)
    {
        Task.Run(() =>
        {
            var audit = new McpAudit
            {
                TokenId = tokenId,
                TokenName = tokenName,
                ToolName = toolName,
                ActionName = actionName,
                CallerIp = ip,
                CallerUserAgent = ua,
                Arguments = SanitizeArguments(arguments),
                Success = success,
                ErrorMessage = error?.Cut(500),
                Duration = duration,
                TraceId = traceId,
                CreateTime = DateTime.Now,
            };
            audit.Insert();
        });
    }

    /// <summary>脱敏敏感字段</summary>
    private static String SanitizeArguments(String json)
    {
        if (json.IsNullOrEmpty()) return json;
        // 截断 2000 字符
        if (json.Length > 2000) json = json.Substring(0, 2000) + "...";
        // 脱敏 password/secret/token/authorization 字段
        var sanitized = Regex.Replace(json, 
            @"(?i)(""(?:password|secret|token|authorization)""\s*:\s*"")[^""]*("")", 
            "$1***$2");
        return sanitized;
    }
}
```

---

## 表间关系图

```
┌─────────────┐       ┌─────────────────────┐       ┌──────────┐
│  McpToken   │ 1───N │ McpTokenResource    │       │ McpAudit │
│─────────────│       │─────────────────────│       │──────────│
│ Id (PK)     │◄──────│ TokenId (FK→Token)  │       │ Id (PK)  │
│ Name        │       │ ResourceType        │       │ TokenId  │──┐
│ Token       │       │ ResourceId          │──┐    │ TokenName│  │ (快照)
│ Enable      │       │ IsAll               │  │    │ ToolName │  │
│ ExpireTime  │       │ Enable              │  │    │ ActionName│ │
│ LastTime    │       └─────────────────────┘  │    │ Success  │  │
│ LastIP      │                                 │    │ Duration │  │
│ CallCount   │                                 │    │ CreateTime│ │
└─────────────┘                                 │    └──────────┘  │
                                                │                  │
                                                ▼                  │
                                    ┌─────────────────────┐        │
                                    │ GalaxyProject /     │        │
                                    │ Node / App          │◄───────┘
                                    │ (多态弱关联)         │ (通过 TokenId)
                                    └─────────────────────┘
```

---

## StarServerSetting 扩展

在 `Stardust.Server/Setting.cs` 的 `StarServerSetting` 类中新增 2 个字段：

```csharp
/// <summary>启用MCP服务。默认关闭</summary>
[Description("启用MCP服务。默认关闭")]
public Boolean EnableMcp { get; set; } = false;

/// <summary>MCP动作集。逗号分隔的模块名，*表示全部启用</summary>
[Description("MCP动作集。逗号分隔的模块名，*表示全部启用")]
public String McpActionSet { get; set; } = "*";
```

**访问方式**（每次请求实时读取，配置变更立即生效）：

```csharp
var setting = StarServerSetting.Current;
if (!setting.EnableMcp) return NotFound();
var enabledModules = setting.McpActionSet.Split(',').Select(s => s.Trim()).ToList();
```

---

## 数据保留策略

- **McpToken**：永久保留，支持软删除（`Enable=false`）
- **McpTokenResource**：随 Token 删除而级联删除（`SyncByToken` 时删除旧授权）
- **McpAudit**：建议保留 90 天，可通过 `DataRetentionService`（已存在的后台服务）定期清理

---

## xcode 生成步骤

1. 编辑 `Stardust.Data/Platform/Model.xml`，在 `<Tables>` 节点末尾追加 3 个 `<Table>` 节点
2. 在项目根目录执行 `xcode`（或运行 `Stardust.Data/xcodetool.bat`）
3. xcode 自动生成：
   - `Stardust.Data/Platform/MCP令牌.cs` + `MCP令牌.Biz.cs`
   - `Stardust.Data/Platform/MCP令牌资源.cs` + `MCP令牌资源.Biz.cs`
   - `Stardust.Data/Platform/MCP审计日志.cs` + `MCP审计日志.Biz.cs`
   - `Stardust.Web/Areas/Platform/Controllers/McpTokenController.cs`
   - `Stardust.Web/Areas/Platform/Controllers/McpTokenResourceController.cs`
   - `Stardust.Web/Areas/Platform/Controllers/McpAuditController.cs`
4. 手写业务方法到 `*.Biz.cs`（xcode 不会覆盖手写代码）
5. 手写 `McpToken/_Form_Body.cshtml` 视图（xcode 不自动生成自定义视图）
