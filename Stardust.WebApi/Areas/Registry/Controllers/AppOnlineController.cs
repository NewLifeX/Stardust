using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Data;
using NewLife.Web;
using NewLife.Remoting.Models;
using Stardust.Data;
using Stardust.Data.Nodes;
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
        ListFields.RemoveField("ProjectName", "ProcessName", "MachineName", "UserName", "Token");

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
        ListFields.TraceUrl();
    }

    public AppOnlineController(StarFactory starFactory) => _starFactory = starFactory;

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

    /// <summary>实例快照。按应用返回所有在线实例及其最近一条性能数据</summary>
    /// <remarks>
    /// 每个 AppOnline 实例行按 clientId 取 AppMeter 最新一条（Id 为雪花倒序即最新）；
    /// availableMemory 取实例所在节点的 NodeOnline.AvailableMemory（AppMeter 无此字段）；
    /// 无性能数据时 cpu/memory/threads/connections/updateTime 为 null，不造 0。
    /// </remarks>
    /// <param name="appId">应用</param>
    /// <returns>实例快照列表</returns>
    [DisplayName("实例快照")]
    public ActionResult Snapshot(Int32 appId)
    {
        if (appId <= 0) throw new ArgumentNullException(nameof(appId), L.T("Application is required."));

        // 在线判定与服务端会话超时保持一致（默认600秒）
        var timeout = Stardust.Server.StarServerSetting.Current.SessionTimeout;
        if (timeout <= 0) timeout = 60;
        var onlineLine = DateTime.Now.AddSeconds(-timeout);

        var list = AppOnline.FindAllByApp(appId);
        var rs = new List<Object>();
        foreach (var item in list)
        {
            // AppMeter 按 clientId 取最新一条，无数据为 null
            AppMeter meter = null;
            if (!item.Client.IsNullOrEmpty())
            {
                var page = new PageParameter { Sort = AppMeter._.Id, Desc = true, PageSize = 1 };
                meter = AppMeter.FindAll(AppMeter._.AppId == appId & AppMeter._.ClientId == item.Client, page).FirstOrDefault();
            }

            // 可用内存来自节点在线信息（节点维度），节点无数据时为 null
            var nodeOnline = item.NodeId > 0 ? NodeOnline.FindByNodeId(item.NodeId) : null;

            rs.Add(new
            {
                id = item.Id,
                clientId = item.Client,
                nodeId = item.NodeId,
                name = item.Name,
                ip = item.IP,
                online = item.UpdateTime >= onlineLine,
                cpu = meter?.CpuUsage,
                memory = meter?.Memory,
                availableMemory = nodeOnline?.AvailableMemory,
                threads = meter?.Threads,
                connections = meter?.Connections,
                updateTime = meter?.Time,
            });
        }

        return Json(0, null, rs);
    }

    [DisplayName("执行命令")]
    [EntityAuthorize((PermissionFlags)16)]
    public async Task<ActionResult> Execute(String command, String argument)
    {
        if (GetRequest("keys") == null) throw new ArgumentNullException(nameof(SelectKeys));
        if (command.IsNullOrEmpty()) throw new ArgumentNullException(nameof(command));

        var ts = new List<(String name, Task<CommandReplyModel?> task)>();
        foreach (var item in SelectKeys)
        {
            var online = AppOnline.FindById(item.ToInt());
            if (online != null && online.App != null)
            {
                ts.Add((online.AppName, _starFactory.SendAppCommandAsync(online.App.Name, online.Client, command, argument, 0, 300, 0, HttpContext.RequestAborted)));
            }
        }

        await Task.WhenAll(ts.Select(t => t.task));

        var success = ts.Count(t => t.task.Result != null);
        var timeout = ts.Count(t => t.task.Result == null);
        var msg = $"操作成功！下发{ts.Count}个，响应{success}个，超时{timeout}个";
        foreach (var (name, task) in ts)
        {
            var reply = task.Result;
            if (reply != null)
                msg += $"\n{name}: {reply.Data ?? "(无返回数据)"}";
        }

        return JsonRefresh(msg);
    }

    [DisplayName("释放内存")]
    [EntityAuthorize((PermissionFlags)32)]
    public async Task<ActionResult> FreeMemory()
    {
        var ts = new List<(String name, Task<CommandReplyModel?> task)>();
        foreach (var item in SelectKeys)
        {
            var online = AppOnline.FindById(item.ToInt());
            if (online != null && online.App != null)
            {
                ts.Add((online.AppName, _starFactory.SendAppCommandAsync(online.App.Name, online.Client, "app/freeMemory", null, 0, 300, 5, HttpContext.RequestAborted)));
            }
        }

        await Task.WhenAll(ts.Select(t => t.task));

        var success = ts.Count(t => t.task.Result != null);
        var timeout = ts.Count(t => t.task.Result == null);
        var msg = $"操作成功！下发{ts.Count}个，响应{success}个，超时{timeout}个";
        foreach (var (name, task) in ts)
        {
            var reply = task.Result;
            if (reply != null)
                msg += $"\n{name}: {reply.Data ?? "(无返回数据)"}";
        }

        return JsonRefresh(msg);
    }
}
