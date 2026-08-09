using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Platform;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using XCode.Membership;
using static Stardust.Data.Platform.McpToken;

namespace Stardust.Web.Areas.Platform.Controllers;

/// <summary>MCP令牌。用于LLM/智能体调用MCP服务的资源授权凭证</summary>
[Menu(30, true, Icon = "fa-table")]
[PlatformArea]
public class McpTokenController : EntityController<McpToken>
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

    /// <summary>插入时自动生成Token字符串并同步资源授权</summary>
    protected override Int32 OnInsert(McpToken entity)
    {
        // 自动生成Token字符串
        if (entity.Token.IsNullOrEmpty()) entity.Token = McpToken.GenerateToken();

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

    /// <summary>更新时同步资源授权（Token创建后不可修改）</summary>
    protected override Int32 OnUpdate(McpToken entity)
    {
        // Token创建后不可修改，恢复原值
        var old = McpToken.FindById(entity.Id);
        if (old != null) entity.Token = old.Token;

        var result = base.OnUpdate(entity);
        if (result > 0)
        {
            // 同步资源授权（删除旧授权+插入新授权）
            var resources = ParseResourcesFromForm();
            McpTokenResource.SyncByToken(entity.Id, resources, ManageProvider.UserHost);
        }
        return result;
    }

    /// <summary>重置Token字符串，旧Token立即失效</summary>
    /// <param name="id">令牌编号</param>
    /// <returns></returns>
    [EntityAuthorize(PermissionFlags.Update)]
    public ActionResult Reset(Int32 id)
    {
        var entity = McpToken.FindById(id);
        if (entity == null) throw new Exception("找不到Token记录");

        var oldToken = entity.Token;
        // 生成新Token并更新（旧Token立即失效，因为数据库中Token字段已被替换）
        entity.Reset();

        // 写日志（LogOnChange=true会自动记录实体变更）
        var masked = oldToken?.Length > 10 ? oldToken.Substring(0, 10) + "***" : oldToken;
        XTrace.WriteLine("[McpToken] Token重置：Id={Id}, Name={Name}, 旧Token={Masked}, IP={IP}", entity.Id, entity.Name, masked, ManageProvider.UserHost);

        return JsonRefresh("Token已重置！");
    }

    /// <summary>从表单解析资源授权列表。表单字段命名规则：res_all_{type}=true 表示全部资源；res_{type}=id 表示具体资源</summary>
    private List<McpTokenResource> ParseResourcesFromForm()
    {
        var resources = new List<McpTokenResource>();
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
