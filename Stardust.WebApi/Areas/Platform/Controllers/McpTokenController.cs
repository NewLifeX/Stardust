using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Platform;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static Stardust.Data.Platform.McpToken;

namespace Stardust.Web.Areas.Platform.Controllers;

/// <summary>MCP令牌。用于LLM/智能体调用MCP服务的资源授权凭证</summary>
[Menu(30, true, Icon = "fa-table")]
[PlatformArea]
public class McpTokenController : WebEntityController<McpToken>
{
    static McpTokenController()
    {
        // 变更时写日志
        LogOnChange = true;

        // 列表页隐藏敏感字段和自动维护字段
        ListFields.RemoveField("Token", "LastIP", "TraceId");
        ListFields.RemoveCreateField().RemoveRemarkField();
        ListFields.TraceUrl("TraceId");

        // 编辑表单移除自动维护字段
        EditFormFields.RemoveField("CallCount", "LastTime", "LastIP", "TraceId");

        // 添加"重置Token"操作列
        {
            var df = ListFields.AddListField("Reset", "Enable");
            df.DisplayName = "重置Token";
            df.Description = "生成新Token字符串，旧Token立即失效";
            df.Url = "/Platform/McpToken/Reset?id={Id}";
            df.DataAction = "action";
        }
    }

    /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
    /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
    /// <returns></returns>
    protected override IEnumerable<McpToken> Search(Pager p)
    {
        var token = p["token"];
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return McpToken.Search(token, enable, start, end, p["Q"], p);
    }

    /// <summary>行级掩码规则（声明式，替代分散的 CropRow 与 Detail override，语义逐键保真）：
    /// 列表行/详情行/导出 Token 值级置 null（键不删，task-94 一期脱敏）。
    /// 写响应不掩码——create/reset 的一次性 Token 揭示 UX 依赖写响应回传明文（原行为）。</summary>
    private static readonly Dictionary<String, Object> _masks = new(StringComparer.OrdinalIgnoreCase) { ["Token"] = null };

    /// <summary>列表/详情/导出统一掩码；写视图不掩码（Token 揭示 UX，原行为）。</summary>
    protected override IDictionary<String, Object> GetRowMasks(ViewKinds view) => view is ViewKinds.List or ViewKinds.Detail ? _masks : null;

    /// <summary>插入时自动生成Token字符串并同步资源授权</summary>
    protected override Int32 OnInsert(McpToken entity)
    {
        // 自动生成Token字符串
        if (entity.Token.IsNullOrEmpty()) entity.Token = McpToken.GenerateToken();
        // 绑定了项目但没填授权范围时，默认只读，避免新令牌发布后只能自检
        if (entity.ProjectId > 0 && entity.Scopes.IsNullOrEmpty())
            entity.Scopes = McpToken.DefaultProjectReadScopes;

        var result = base.OnInsert(entity);
        if (result > 0)
        {
            // 同步资源授权（表单勾选）
            var resources = ParseResourcesFromForm();
            if (resources.Count > 0)
            {
                McpTokenResource.SyncByToken(entity.Id, resources, ManageProvider.UserHost);
            }
        }
        return result;
    }

    /// <summary>更新时同步资源授权（Token创建后不可修改）。仅当表单携带了资源授权时才同步；
    /// JSON/API 更新（前端 updateToken/setTokenEnable）不携带 res_* 字段，此时必须跳过同步，
    /// 否则 ParseResourcesFromForm 返回空列表会经 SyncByToken 清空该令牌全部资源授权。</summary>
    protected override Int32 OnUpdate(McpToken entity)
    {
        // Token创建后不可修改，恢复原值
        var old = McpToken.FindById(entity.Id);
        if (old != null) entity.Token = old.Token;

        var result = base.OnUpdate(entity);
        if (result > 0)
        {
            // 同步资源授权（删除旧授权+插入新授权）。与 OnInsert 相同，空列表不触发同步
            var resources = ParseResourcesFromForm();
            if (resources.Count > 0)
            {
                McpTokenResource.SyncByToken(entity.Id, resources, ManageProvider.UserHost);
            }
        }
        return result;
    }

    /// <summary>重置Token字符串，旧Token立即失效</summary>
    /// <param name="id">令牌编号</param>
    /// <returns></returns>
    [EntityAuthorize(PermissionFlags.Update)]
    // 必须显式路由：否则 GET /api/Platform/McpToken/Reset 会被 ReadOnlyEntityController.Detail 的 {id} 捕获，
    // id="Reset" 解析主键失败 → code 500（前端 GuardModal 确认后报「令牌重置失败」）。
    [HttpGet("/api/[area]/[controller]/Reset")]
    public ActionResult Reset([FromQuery] Int32 id)
    {
        var entity = McpToken.FindById(id);
        if (entity == null) throw new Exception(L.T("Token record not found."));

        var oldToken = entity.Token;
        // 生成新Token并更新（旧Token立即失效，因为数据库中Token字段已被替换）
        entity.Reset();

        // 写日志（LogOnChange=true会自动记录实体变更）
        var masked = oldToken?.Length > 10 ? oldToken.Substring(0, 10) + "***" : oldToken;
        XTrace.WriteLine("[McpToken] Token reset: Id={0}, Name={1}, old token={2}, IP={3}", entity.Id, entity.Name, masked, ManageProvider.UserHost);

        return JsonRefresh(new { id = entity.Id, token = entity.Token });
    }

    /// <summary>从表单解析资源授权列表。表单字段命名规则：res_all_{type}=true 表示全部资源；res_{type}=id 表示具体资源</summary>
    private List<McpTokenResource> ParseResourcesFromForm()
    {
        var resources = new List<McpTokenResource>();
        // JSON/API 请求没有表单内容类型，访问 Request.Form 会抛异常；此时跳过资源授权同步
        if (!Request.HasFormContentType) return resources;

        var form = Request.Form;

        // 表单字段使用小写协议名（res_all_project / res_project），存储统一转换为大驼峰（枚举成员名）
        foreach (var t in McpResourceTypeExtensions.DirectTypes)
        {
            var wire = t.ToWireName();

            // 检查是否勾选了"全部资源"
            if (form[$"res_all_{wire}"].ToString().ToBoolean())
            {
                resources.Add(new McpTokenResource { ResourceType = t.ToStorageName(), IsAll = true });
                continue;
            }

            // 否则收集具体资源ID
            var ids = form[$"res_{wire}"];
            foreach (var idStr in ids)
            {
                if (Int32.TryParse(idStr, out var rid) && rid > 0)
                {
                    resources.Add(new McpTokenResource { ResourceType = t.ToStorageName(), ResourceId = rid });
                }
            }
        }
        return resources;
    }
}
