using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Remoting.Models;
using NewLife.Serialization;
using NewLife.Web;
using Stardust.Data.Nodes;
using XCode.Membership;
using static Stardust.Data.Nodes.NodeOnline;
using Node = Stardust.Data.Nodes.Node;

namespace Stardust.Web.Areas.Nodes.Controllers;

/// <summary>节点在线。管理节点 WebSocket 长连接会话，查看各节点实时在线状态和性能指标</summary>
[Menu(80)]
[NodesArea]
public class NodeOnlineController : NodesEntityController<NodeOnline>
{
    private readonly StarFactory _starFactory;

    static NodeOnlineController()
    {
        //ListFields.RemoveField("SessionID", "IP", "ProvinceID", "CityID", "Macs", "Token");

        var list = ListFields;
        list.Clear();
        var allows = new[] { "ID", "ProjectName", "Name", "Category", "ProductCode", "CityName", "Address", "PingCount", "LongLink", "Version", "OSKind", "IP", "AvailableMemory", "MemoryUsed", "AvailableFreeSpace", "SpaceUsed", "CpuRate", "ProcessCount", __.Signal, __.Offset, "UplinkSpeed", "DownlinkSpeed", "IntranetScore", "InternetScore", "TraceId", "LocalTime", "CreateTime", "UpdateTime", "UpdateIP" };
        foreach (var item in allows)
        {
            list.AddListField(item);
        }

        {
            var df = ListFields.GetField("Name") as ListField;
            df.Text = "{Name}";
            df.Url = "/Nodes/Node/Detail?Id={NodeID}";
            df.Target = "_blank";
        }
        {
            // PingCount 是心跳次数，不是延迟，避免被误读为心跳耗时
            var df = ListFields.GetField("PingCount") as ListField;
            if (df != null) df.DisplayName = "心跳次数";
        }

        ListFields.LocalizedTraceUrl();
    }

    public NodeOnlineController(StarFactory starFactory) => _starFactory = starFactory;

    override public void OnActionExecuting(ActionExecutingContext filterContext)
    {
        base.OnActionExecuting(filterContext);

        PageSetting.EnableAdd = false;
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<NodeOnline> Search(Pager p)
    {
        var nodeId = p["nodeId"].ToInt(-1);
        var rids = p["areaId"].SplitAsInt("/");
        var provinceId = rids.Length > 0 ? rids[0] : -1;
        var cityId = rids.Length > 1 ? rids[1] : -1;

        var projectId = p["projectId"].ToInt(-1);
        var category = p["category"];
        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        PageSetting.EnableSelect = true;
        p.RetrieveState = true;

        return NodeOnline.Search(projectId, nodeId, provinceId, cityId, category, start, end, p["Q"], p);
    }

    public async Task<ActionResult> Trace(Int32 id)
    {
        var node = Node.FindByID(id);
        if (node != null)
        {
            //NodeCommand.Add(node, "截屏");
            //NodeCommand.Add(node, "抓日志");

            await _starFactory.SendNodeCommandAsync(node.Code, "截屏", cancellationToken: HttpContext.RequestAborted);
            await _starFactory.SendNodeCommandAsync(node.Code, "抓日志", cancellationToken: HttpContext.RequestAborted);
        }

        return RedirectToAction("Index");
    }

    /// <summary>批量升级错峰间隔（秒）</summary>
    /// <remarks>
    /// 升级包约 2MB，文件服务出口带宽有限，几十个节点同时下载会互相挤占带宽。
    /// 把命令执行时间排到未来，接口立即返回，节点按各自时间点开始下载，避免并发送达引发集体超时。
    /// </remarks>
    private const Int32 UpgradeScheduleInterval = 3;

    [DisplayName("检查更新")]
    [EntityAuthorize((PermissionFlags)16)]
    public async Task<ActionResult> CheckUpgrade()
    {
        var keys = SelectKeys;
        if (keys == null || keys.Length == 0) return JsonRefresh(L.T("Please select at least one item."));

        var ts = new List<(String name, Task<CommandReplyModel?> task)>();
        foreach (var item in keys)
        {
            var online = NodeOnline.FindById(item.ToInt());
            if (online?.Node != null)
            {
                // 错峰执行：每个节点依次延后 3 秒开始，接口本身不等待
                var startTime = (ts.Count + 1) * UpgradeScheduleInterval;

                ts.Add((online.Name, _starFactory.SendNodeCommandAsync(online.Node.Code, "node/upgrade", null, startTime, 3600, 3, HttpContext.RequestAborted)));
            }
        }

        await Task.WhenAll(ts.Select(t => t.task));

        return JsonRefresh(Summary(ts));
    }

    [DisplayName("升级到版本")]
    [EntityAuthorize((PermissionFlags)16)]
    public async Task<ActionResult> UpgradeTo(String releaseId)
    {
        if (releaseId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(releaseId));

        var keys = SelectKeys;
        if (keys == null || keys.Length == 0) return JsonRefresh(L.T("Please select at least one item."));

        var release = ProductRelease.FindById(releaseId.ToInt());
        if (release == null) return JsonRefresh(L.T("The specified version does not exist."));

        var ts = new List<(String name, Task<CommandReplyModel?> task)>();
        var skipCount = 0;
        foreach (var item in keys)
        {
            var online = NodeOnline.FindById(item.ToInt());
            if (online?.Node != null)
            {
                var pkg = release.MatchPackage(online.Node);
                if (pkg == null)
                {
                    skipCount++;
                    continue;
                }

                // 错峰执行：每个节点依次延后 3 秒开始，接口本身不等待
                var startTime = (ts.Count + 1) * UpgradeScheduleInterval;

                // 手工升级到指定版本：下发通道与版本，代理据此拉取指定版本，不受自动去重与通道限制
                var args = $"channel={release.Channel}&version={release.Version}";
                ts.Add((online.Name, _starFactory.SendNodeCommandAsync(online.Node.Code, "node/upgrade", args, startTime, 3600, 0, HttpContext.RequestAborted)));
            }
        }

        if (ts.Count == 0) return JsonRefresh(L.T("No matched package for the selected nodes; skipped {0}", skipCount));

        await Task.WhenAll(ts.Select(t => t.task));

        return JsonRefresh(Queued(ts.Count, skipCount));
    }

    [DisplayName("同步时间")]
    [EntityAuthorize((PermissionFlags)16)]
    public async Task<ActionResult> SyncTime()
    {
        var keys = SelectKeys;
        if (keys == null || keys.Length == 0) return JsonRefresh(L.T("Please select at least one item."));

        var ts = new List<(String name, Task<CommandReplyModel?> task)>();
        foreach (var item in keys)
        {
            var online = NodeOnline.FindById(item.ToInt());
            if (online?.Node != null)
            {
                ts.Add((online.Name, _starFactory.SendNodeCommandAsync(online.Node.Code, "node/syncTime", null, 0, 600, 5, HttpContext.RequestAborted)));
            }
        }

        await Task.WhenAll(ts.Select(t => t.task));

        return JsonRefresh(Summary(ts));
    }

    [DisplayName("重启服务")]
    [EntityAuthorize((PermissionFlags)32)]
    public async Task<ActionResult> Restart()
    {
        var keys = SelectKeys;
        if (keys == null || keys.Length == 0) return JsonRefresh(L.T("Please select at least one item."));

        var ts = new List<(String name, Task<CommandReplyModel?> task)>();
        foreach (var item in keys)
        {
            var online = NodeOnline.FindById(item.ToInt());
            if (online?.Node != null)
            {
                ts.Add((online.Name, _starFactory.SendNodeCommandAsync(online.Node.Code, "node/restart", null, 0, 600, 5, HttpContext.RequestAborted)));
            }
        }

        await Task.WhenAll(ts.Select(t => t.task));

        return JsonRefresh(Summary(ts));
    }

    [DisplayName("重启系统")]
    [EntityAuthorize((PermissionFlags)64)]
    public async Task<ActionResult> Reboot()
    {
        var keys = SelectKeys;
        if (keys == null || keys.Length == 0) return JsonRefresh(L.T("Please select at least one item."));

        var ts = new List<(String name, Task<CommandReplyModel?> task)>();
        foreach (var item in keys)
        {
            var online = NodeOnline.FindById(item.ToInt());
            if (online?.Node != null)
            {
                ts.Add((online.Name, _starFactory.SendNodeCommandAsync(online.Node.Code, "node/reboot", null, 0, 600, 5, HttpContext.RequestAborted)));
            }
        }

        await Task.WhenAll(ts.Select(t => t.task));

        return JsonRefresh(Summary(ts));
    }

    [DisplayName("执行命令")]
    [EntityAuthorize((PermissionFlags)16)]
    public async Task<ActionResult> Execute(String command, String argument)
    {
        var keys = SelectKeys;
        if (keys == null || keys.Length == 0) return JsonRefresh(L.T("Please select at least one item."));
        if (command.IsNullOrEmpty()) return JsonRefresh(L.T("Command cannot be empty."));

        var ts = new List<(String name, Task<CommandReplyModel?> task)>();
        foreach (var item in keys)
        {
            var online = NodeOnline.FindById(item.ToInt());
            if (online != null && online.Node != null)
            {
                // 交互式命令等待短回执，便于页面直接展示执行结果
                ts.Add((online.Name, _starFactory.SendNodeCommandAsync(online.Node.Code, command, argument, 0, 300, 5, HttpContext.RequestAborted)));
            }
        }

        await Task.WhenAll(ts.Select(t => t.task));

        return JsonRefresh(Summary(ts));
    }

    /// <summary>批量命令结果摘要。区分已下发、有回执与无回执，避免把即发即忘误报成超时</summary>
    /// <param name="ts">每个目标的命令任务</param>
    /// <returns>本地化摘要文本</returns>
    private static String Summary(List<(String name, Task<CommandReplyModel?> task)> ts)
    {
        var replied = ts.Count(t => t.task.Result != null);
        var noReply = ts.Count - replied;

        var msg = L.T("Dispatched {0}, replied {1}, no reply {2}", ts.Count, replied, noReply);
        foreach (var (name, task) in ts)
        {
            var reply = task.Result;
            if (reply?.Data != null) msg += $"\n{name}: {reply.Data}";
        }

        return msg;
    }

    /// <summary>即发即忘命令摘要。命令已写入队列，回执通过事件异步上报</summary>
    /// <param name="dispatched">已下发数量</param>
    /// <param name="skipped">跳过数量</param>
    /// <returns>本地化摘要文本</returns>
    private static String Queued(Int32 dispatched, Int32 skipped = 0)
    {
        var msg = L.T("Dispatched {0}; awaiting node confirmation", dispatched);
        if (skipped > 0) msg += " " + L.T("Skipped {0} offline", skipped);

        return msg;
    }
}
