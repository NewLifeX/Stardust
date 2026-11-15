using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using NewLife.Remoting.Models;
using Stardust.Data;
using Stardust.Server;
using XCode.Membership;

namespace Stardust.Web.Areas.Registry.Controllers;

/// <summary>应用在线。应用当前在线的实例</summary>
[RegistryArea]
[Menu(95)]
public class AppOnlineController : RegistryEntityController<AppOnline>
{
    private readonly StarFactory _starFactory;

    static AppOnlineController()
    {
        ListFields.RemoveField("ProjectName", "ProcessName", "MachineName", "UserName", "Token", "WorkerId", "UUID", "Macs", "StartTime", "Compile", "Creator", "CreateTime", "CreateIP", "UpdateTime", "UpdateIP");

        {
            var df = ListFields.GetField("AppName") as ListField;
            df.Header = "应用";
            df.Text = "{AppName}";
            df.Url = "/Registry/App/Detail?id={AppId}";
            df.Target = "_blank";
        }
        {
            var df = ListFields.GetField("NodeName") as ListField;
            df.Header = "节点";
            df.Text = "{NodeName}";
            df.Url = "/Nodes/Node?Id={NodeId}";
            df.Target = "_blank";
        }
        {
            // PingCount 是心跳次数，不是延迟，避免被误读为心跳耗时
            var df = ListFields.GetField("PingCount") as ListField;
            if (df != null) df.DisplayName = "心跳次数";
        }
        {
            var df = ListFields.AddListField("OnlineState", null, "PingCount");
            df.Header = "状态";
            df.DisplayName = "状态";
            df.GetValue = e => IsOnline(e as AppOnline) ? "在线" : "离线";
            df.GetClass = e => IsOnline(e as AppOnline) ? "text-success" : "text-muted";
        }
        {
            var df = ListFields.AddListField("Meter", "LongLink");
            df.Header = "性能";
            df.DisplayName = "性能";
            df.Url = "/Registry/AppMeter?appId={AppId}&clientId={Client}";
        }
        {
            var df = ListFields.AddListField("History", "LongLink");
            df.DisplayName = "历史";
            df.Url = "/Registry/AppHistory?appId={AppId}&client={Client}";
        }
        //{
        //    var df = ListFields.GetField("TraceId") as ListField;
        //    df.DisplayName = "跟踪";
        //    df.Url = StarHelper.BuildUrl("{TraceId}");
        //    df.DataVisible = e => e is AppOnline entity && !entity.TraceId.IsNullOrEmpty();
        //}
        ListFields.LocalizedTraceUrl();
    }

    public AppOnlineController(StarFactory starFactory) => _starFactory = starFactory;

    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        base.OnActionExecuting(filterContext);

        PageSetting.EnableAdd = false;
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<AppOnline> Search(Pager p)
    {
        var appId = p["appId"].ToInt(-1);
        var nodeId = p["nodeId"].ToInt(-1);
        var projectId = p["projectId"].ToInt(-1);
        var category = p["category"];

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return AppOnline.Search(projectId, appId, nodeId, category, start, end, p["Q"], p);
    }

    [DisplayName("执行命令")]
    [EntityAuthorize((PermissionFlags)16)]
    public async Task<ActionResult> Execute(String command, String argument)
    {
        var keys = SelectKeys;
        if (keys == null || keys.Length == 0) return JsonRefresh(L.T("Please select at least one item."));
        if (command.IsNullOrEmpty()) return JsonRefresh(L.T("Command cannot be empty."));

        var ts = new List<(String name, Task<CommandReplyModel?> task)>();
        var skipped = 0;
        foreach (var item in keys)
        {
            var online = AppOnline.FindById(item.ToInt());
            if (online != null && online.App != null)
            {
                // 单例应用会保留最近2天的历史会话，离线会话不再下发，避免无谓等待
                if (!IsOnline(online))
                {
                    skipped++;
                    continue;
                }

                // 交互式命令等待短回执，便于页面直接展示执行结果
                ts.Add((online.AppName, _starFactory.SendAppCommandAsync(online.App.Name, online.Client, command, argument, 0, 300, 5, HttpContext.RequestAborted)));
            }
        }

        await Task.WhenAll(ts.Select(t => t.task));

        return JsonRefresh(Summary(ts, skipped));
    }

    [DisplayName("释放内存")]
    [EntityAuthorize((PermissionFlags)32)]
    public async Task<ActionResult> FreeMemory()
    {
        var keys = SelectKeys;
        if (keys == null || keys.Length == 0) return JsonRefresh(L.T("Please select at least one item."));

        var ts = new List<(String name, Task<CommandReplyModel?> task)>();
        var skipped = 0;
        foreach (var item in keys)
        {
            var online = AppOnline.FindById(item.ToInt());
            if (online != null && online.App != null)
            {
                // 单例应用会保留最近2天的历史会话，离线会话不再下发，避免无谓等待
                if (!IsOnline(online))
                {
                    skipped++;
                    continue;
                }

                ts.Add((online.AppName, _starFactory.SendAppCommandAsync(online.App.Name, online.Client, "app/freeMemory", null, 0, 300, 5, HttpContext.RequestAborted)));
            }
        }

        await Task.WhenAll(ts.Select(t => t.task));

        return JsonRefresh(Summary(ts, skipped));
    }

    /// <summary>批量命令结果摘要。区分已下发、有回执与无回执，避免把即发即忘误报成超时</summary>
    /// <param name="ts">每个目标的命令任务</param>
    /// <param name="skipped">跳过的离线实例数</param>
    /// <returns>本地化摘要文本</returns>
    private static String Summary(List<(String name, Task<CommandReplyModel?> task)> ts, Int32 skipped)
    {
        var replied = ts.Count(t => t.task.Result != null);
        var noReply = ts.Count - replied;

        var msg = L.T("Dispatched {0}, replied {1}, no reply {2}", ts.Count, replied, noReply);
        if (skipped > 0) msg += " " + L.T("Skipped {0} offline", skipped);
        foreach (var (name, task) in ts)
        {
            var reply = task.Result;
            if (reply?.Data != null) msg += $"\n{name}: {reply.Data}";
        }

        return msg;
    }

    /// <summary>判断在线会话是否仍然活跃</summary>
    /// <param name="online">在线会话</param>
    /// <returns>是否在线</returns>
    /// <remarks>单例应用下线后记录会保留2天，列表与批量命令必须按心跳时间区分在线状态</remarks>
    private static Boolean IsOnline(AppOnline online)
    {
        if (online == null) return false;

        var timeout = StarServerSetting.Current.SessionTimeout;
        if (timeout <= 0) timeout = 600;

        return (DateTime.Now - online.UpdateTime).TotalSeconds < timeout;
    }
}
