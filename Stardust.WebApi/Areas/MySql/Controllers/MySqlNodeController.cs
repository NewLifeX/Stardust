using System.ComponentModel;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Nodes;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Data;
using NewLife.Log;
using NewLife.Web;
using XCode;
using XCode.Membership;
using Stardust.Server.Services;

namespace Stardust.Web.Areas.MySql.Controllers;

/// <summary>MySQL 节点。监控的 MySQL 实例</summary>
[Menu(50)]
[MySqlArea]
public class MySqlNodeController : WebEntityController<MySqlNode>
{
    private readonly IMySqlService _mySqlService;

    static MySqlNodeController()
    {
        LogOnChange = true;

        ListFields.RemoveField("WebHook", "AlarmConnections", "AlarmQPS", "AlarmSlowQuery");
        ListFields.RemoveField("Password", "UserName");
        ListFields.RemoveCreateField();
        ListFields.RemoveField("UpdateUser", "UpdateUserID", "UpdateIP", "Remark");

        {
            var df = ListFields.GetField("ProjectName") as ListField;
            df.Url = "/Platform/GalaxyProject?projectId={ProjectId}";
            df.Target = "_frame";
        }
        {
            var df = ListFields.AddListField("Monitor", "UpdateTime");
            df.DisplayName = "监控";
            df.Header = "监控";
            df.Url = "/MySql/MySqlData?mysqlId={Id}";
        }
        {
            var df = ListFields.AddListField("Refresh", "UpdateTime");
            df.DisplayName = "刷新";
            df.Header = "刷新";
            df.Url = "/MySql/MySqlNode/Refresh?Id={Id}";
            df.DataAction = "action";
        }
        {
            var df = ListFields.AddListField("Log", "UpdateTime");
            df.DisplayName = "审计日志";
            df.Header = "审计日志";
            df.Url = $"/Admin/Log?category={HttpUtility.UrlEncode("MySql节点")}&linkId={{Id}}";
            df.Target = "_frame";
        }
    }

    public MySqlNodeController(IMySqlService mySqlService) => _mySqlService = mySqlService;

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<MySqlNode> Search(Pager p)
    {
        var nodeId = p["Id"].ToInt(-1);
        if (nodeId > 0)
        {
            var node = MySqlNode.FindById(nodeId);
            if (node != null) return new[] { CropRow(node) };
        }

        var server = p["server"];
        var port = p["port"].ToInt(-1);
        var projectId = p["projectId"].ToInt(-1);
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return MySqlNode.Search(server, port, projectId, enable, start, end, p["Q"], p).Select(CropRow);
    }

    [EntityAuthorize(PermissionFlags.Update)]
    public ActionResult Refresh(Int32 id)
    {
        var node = MySqlNode.FindById(id);
        if (node != null)
        {
            XTrace.WriteLine("Refreshing {0}/{1} {2}:{3}", node.Name, node.Id, node.Server, node.Port);

            try
            {
                _mySqlService.TraceNode(node);

                LogProvider.Provider.WriteLog("MySqlNode", "Refresh", true, L.TLog("MySql node [{0}] refreshed successfully.", node));
            }
            catch (Exception ex)
            {
                LogProvider.Provider.WriteLog("MySqlNode", "Refresh", false, ex?.GetMessage());

                throw;
            }
        }

        return JsonRefresh($"刷新[{node}]成功！");
    }

    /// <summary>查看单行。出参投影裁剪 Password/UserName/WebHook 明文（task-95 二期刀0，OBS-3 CropPfx 同款语义：值级置 null）</summary>
    /// <param name="id">主键</param>
    /// <returns>裁剪后的单行数据</returns>
    public override ApiResponse<MySqlNode> Detail(String id)
    {
        var res = base.Detail(id);
        if (res?.Data != null) res.Data = CropRow(res.Data);
        return res;
    }

    /// <summary>添加数据。响应快照裁剪敏感三键（task-95 刀0；无回显需求）</summary>
    /// <param name="model">提交的模型</param>
    /// <returns>裁剪后的新增结果</returns>
    public override async Task<ApiResponse<MySqlNode>> Insert(MySqlNode model)
    {
        var res = await base.Insert(model);
        if (res?.Data != null) res.Data = CropRow(res.Data);
        return res;
    }

    /// <summary>更新数据。响应快照裁剪敏感三键（task-95 刀0；新提交值与旧存量值都不得回显）</summary>
    /// <param name="id">主键</param>
    /// <param name="model">提交的模型</param>
    /// <returns>裁剪后的更新结果</returns>
    public override async Task<ApiResponse<MySqlNode>> Update(String id, MySqlNode model)
    {
        var res = await base.Update(id, model);
        if (res?.Data != null) res.Data = CropRow(res.Data);
        return res;
    }

    /// <summary>删除数据。响应快照裁剪敏感三键（task-95 刀0；存量凭据不得随删除回显）</summary>
    /// <param name="id">主键</param>
    /// <returns>裁剪后的删除结果</returns>
    public override ApiResponse<MySqlNode> Delete(String id)
    {
        var res = base.Delete(id);
        if (res?.Data != null) res.Data = CropRow(res.Data);
        return res;
    }

    /// <summary>写入校验：GET 回显掩码 + POST 守卫（task-95 刀0，AppDeployController.RepoPassword 先例同款）。
    /// 前端编辑抽屉以 Detail 全量展开为 PUT 提交体（F1 形态），Detail/回显均给 "*****"，
    /// 未修改即原样提交 "*****"，此处从库中恢复原值并清脏标记，杜绝静默清库；
    /// 显式清空（空串）放行=允许移除凭据。</summary>
    /// <param name="entity">实体</param>
    /// <param name="type">操作类型</param>
    /// <param name="post">是否提交</param>
    /// <returns>校验结果</returns>
    protected override Boolean Valid(MySqlNode entity, DataObjectMethodType type, Boolean post)
    {
        if (!post)
        {
            // GET 查询后：编辑表单回显脱敏
            if (type == DataObjectMethodType.Update)
            {
                if (!entity.Password.IsNullOrEmpty()) entity.Password = "*****";
                if (!entity.WebHook.IsNullOrEmpty()) entity.WebHook = "*****";
            }

            return base.Valid(entity, type, post);
        }

        // POST 保存前：掩码值恢复原值，防 Detail 裁剪/回显掩码经 PUT 全量展开造成静默清库
        foreach (var name in new[] { nameof(MySqlNode._.Password), nameof(MySqlNode._.WebHook) })
        {
            if (!(entity as IEntity).Dirtys[name]) continue;
            if (entity[name] as String == "*****")
            {
                var old = MySqlNode.FindById(entity.Id);
                if (old != null)
                {
                    entity[name] = old[name];
                    (entity as IEntity).Dirtys[name] = false;
                }
            }
            // 空值：保持空值，允许显式清空
        }

        return base.Valid(entity, type, post);
    }

    /// <summary>出参投影：克隆实体并置空 Password/UserName/WebHook 明文，杜绝列表/详情/写响应泄漏（task-95 二期刀0）。
    /// 克隆为全新实例，不原地改写共享缓存中的原实体；Valid 的 GET 掩码/POST 回填链路走原实体，写路径语义不变。</summary>
    /// <param name="src">原始实体</param>
    /// <returns>不含凭据明文的克隆实体</returns>
    private static MySqlNode CropRow(MySqlNode src)
    {
        var dst = new MySqlNode();
        foreach (var fi in MySqlNode.Meta.Fields)
        {
            dst[fi.Name] = src[fi.Name];
        }
        dst.Password = null;
        dst.UserName = null;
        dst.WebHook = null;
        return dst;
    }
}
