using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data.Platform;
using XCode.Membership;

namespace Stardust.Web.Areas.Platform.Controllers;

/// <summary>项目用户关系。每个项目拥有哪些用户</summary>
[Menu(10, false, Icon = "fa-table")]
[PlatformArea]
public class ProjectUserController : WebEntityController<ProjectUser>
{
    static ProjectUserController()
    {
        //LogOnChange = true;

        //ListFields.RemoveField("Id", "Creator");
        ListFields.RemoveCreateField().RemoveRemarkField();

        //{
        //    var df = ListFields.GetField("Code") as ListField;
        //    df.Url = "?code={Code}";
        //}
        //{
        //    var df = ListFields.AddListField("devices", null, "Onlines");
        //    df.DisplayName = "查看设备";
        //    df.Url = "Device?groupId={Id}";
        //    df.DataVisible = e => (e as ProjectUser).Devices > 0;
        //}
        //{
        //    var df = ListFields.GetField("Kind") as ListField;
        //    df.GetValue = e => ((Int32)(e as ProjectUser).Kind).ToString("X4");
        //}
        //ListFields.TraceUrl("TraceId");
    }

    //private readonly ITracer _tracer;

    //public ProjectUserController(ITracer tracer)
    //{
    //    _tracer = tracer;
    //}

    /// <summary>高级搜索。按项目、用户、启用状态分页查询。未传的编号为 -1，表示不按该列过滤。</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<ProjectUser> Search(Pager p)
    {
        var projectId = p["projectId"].ToInt(-1);
        var userId = p["userId"].ToInt(-1);
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return ProjectUser.Search(projectId, userId, enable, start, end, p["Q"], p);
    }

    /// <summary>插入前确认项目和用户都存在。与 Web 宿主同逻辑。</summary>
    protected override Int32 OnInsert(ProjectUser entity)
    {
        entity.EnsureLink();
        return base.OnInsert(entity);
    }

    /// <summary>启用/停用（走查 C06/C07 修复，j-config 排程令 2026-11-10）。
    /// 前端 Stardust.Web.Vue projectMember.logic.ts:206-222 契约=PUT 集合路径 body{id,enable} 部分更新；
    /// NewLife.Cube(API版) 泛型 Update 要求主键在路径 {id}，集合路径 PUT 无路由匹配返回 404，
    /// 故按 McpTokenController.Reset 先例补显式集合路径路由。仅更新 Enable，未提交字段保持原值。
    /// 与 Web 宿主不对称说明：MVC 编辑页走表单 POST（Cube MVC Update 模板含 {id}），无此缺口，Web 侧无需对等动作。</summary>
    /// <param name="model">绑定自 body 的部分更新载荷，仅 Id 与 Enable 有效</param>
    /// <returns>统一信封，Code=0 更新成功（Data=更新后实体），否则更新失败</returns>
    [EntityAuthorize(PermissionFlags.Update)]
    [HttpPut("/api/[area]/[controller]")]
    public ApiResponse<ProjectUser> UpdateEnable(ProjectUser model)
    {
        var rs = new ApiResponse<ProjectUser>();
        try
        {
            if (model == null || model.Id == 0) throw new ArgumentException("The record ID is required");

            var old = ProjectUser.FindById(model.Id);
            if (old == null) throw new ArgumentException($"Record #{model.Id} does not exist");

            // 契约式部分更新：仅复制 Enable，未提交字段保持库中原值（绑定模型其它属性为默认值，不可整体落库）
            old.Enable = model.Enable;
            old.Update();

            rs.Code = 0;
            rs.Message = "Update succeeded!";
            rs.Data = old;
        }
        catch (Exception ex)
        {
            rs.Code = 500;
            rs.Message = "Update failed! " + ex.Message;
            rs.Data = model;
        }
        return rs;
    }
}
