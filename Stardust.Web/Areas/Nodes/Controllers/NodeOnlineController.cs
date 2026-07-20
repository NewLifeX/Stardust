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
        var allows = new[] { "ID", "ProjectName", "Name", "Category", "ProductCode", "CityName", "Address", "PingCount", "WebSocket", "Version", "OSKind", "IP", "AvailableMemory", "MemoryUsed", "AvailableFreeSpace", "SpaceUsed", "CpuRate", "ProcessCount", __.Signal, __.Offset, "UplinkSpeed", "DownlinkSpeed", "IntranetScore", "InternetScore", "TraceId", "LocalTime", "CreateTime", "UpdateTime", "UpdateIP" };
        foreach (var item in allows)
        {
            list.AddListField(item);
        }

        {
            var df = ListFields.GetField("Name") as ListField;
            df.DisplayName = "{Name}";
            df.Url = "/Nodes/Node/Detail?Id={NodeID}";
            df.Target = "_blank";
        }

        ListFields.TraceUrl();
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

    [DisplayName("检查更新")]
    [EntityAuthorize((PermissionFlags)16)]
    public async Task<ActionResult> CheckUpgrade()
    {
        var ts = new List<(String name, Task<CommandReplyModel?> task)>();
        foreach (var item in SelectKeys)
        {
            var online = NodeOnline.FindById(item.ToInt());
            if (online?.Node != null)
            {
                ts.Add((online.Name, _starFactory.SendNodeCommandAsync(online.Node.Code, "node/upgrade", null, 0, 600, 10, HttpContext.RequestAborted)));
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

    [DisplayName("升级到版本")]
    [EntityAuthorize((PermissionFlags)16)]
    public async Task<ActionResult> UpgradeTo(String releaseId)
    {
        if (releaseId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(releaseId));

        var release = ProductRelease.FindById(releaseId.ToInt());
        if (release == null) return JsonRefresh("指定版本不存在！");

        var ts = new List<(String name, Task<CommandReplyModel?> task)>();
        var skipCount = 0;
        foreach (var item in SelectKeys)
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

                // 双层取值：Package优先级高于Release，客户端自行处理空Executor
                var executor = !pkg.Executor.IsNullOrEmpty() ? pkg.Executor : release.Executor;
                var preinstall = !pkg.Preinstall.IsNullOrEmpty() ? pkg.Preinstall : release.Preinstall;

                var info = new UpgradeInfo
                {
                    Version = release.Version,
                    Source = pkg.Source,
                    FileHash = pkg.FileHash,
                    FileSize = pkg.Size,
                    Preinstall = preinstall,
                    Executor = executor,
                    Force = release.Force,
                    Description = release.Remark,
                };
                var args = info.ToJson();
                ts.Add((online.Name, _starFactory.SendNodeCommandAsync(online.Node.Code, "node/upgrade", args, 0, 600, 0, HttpContext.RequestAborted)));
            }
        }

        if (ts.Count == 0) return JsonRefresh($"选中节点均无匹配的包。跳过{skipCount}个");

        await Task.WhenAll(ts.Select(t => t.task));

        var success = ts.Count(t => t.task.Result != null);
        var timeout = ts.Count(t => t.task.Result == null);
        var msg = $"操作成功！下发{ts.Count}个，响应{success}个，超时{timeout}个，跳过{skipCount}个";
        foreach (var (name, task) in ts)
        {
            var reply = task.Result;
            if (reply != null)
                msg += $"\n{name}: {reply.Data ?? "(无返回数据)"}";
        }

        return JsonRefresh(msg);
    }

    [DisplayName("同步时间")]
    [EntityAuthorize((PermissionFlags)16)]
    public async Task<ActionResult> SyncTime()
    {
        var ts = new List<(String name, Task<CommandReplyModel?> task)>();
        foreach (var item in SelectKeys)
        {
            var online = NodeOnline.FindById(item.ToInt());
            if (online?.Node != null)
            {
                ts.Add((online.Name, _starFactory.SendNodeCommandAsync(online.Node.Code, "node/syncTime", null, 0, 600, 5, HttpContext.RequestAborted)));
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

    [DisplayName("重启服务")]
    [EntityAuthorize((PermissionFlags)32)]
    public async Task<ActionResult> Restart()
    {
        var ts = new List<(String name, Task<CommandReplyModel?> task)>();
        foreach (var item in SelectKeys)
        {
            var online = NodeOnline.FindById(item.ToInt());
            if (online?.Node != null)
            {
                ts.Add((online.Name, _starFactory.SendNodeCommandAsync(online.Node.Code, "node/restart", null, 0, 600, 5, HttpContext.RequestAborted)));
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

    [DisplayName("重启系统")]
    [EntityAuthorize((PermissionFlags)64)]
    public async Task<ActionResult> Reboot()
    {
        var ts = new List<(String name, Task<CommandReplyModel?> task)>();
        foreach (var item in SelectKeys)
        {
            var online = NodeOnline.FindById(item.ToInt());
            if (online?.Node != null)
            {
                ts.Add((online.Name, _starFactory.SendNodeCommandAsync(online.Node.Code, "node/reboot", null, 0, 600, 5, HttpContext.RequestAborted)));
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

    [DisplayName("执行命令")]
    [EntityAuthorize((PermissionFlags)16)]
    public async Task<ActionResult> Execute(String command, String argument)
    {
        if (GetRequest("keys") == null) throw new ArgumentNullException(nameof(SelectKeys));
        if (command.IsNullOrEmpty()) throw new ArgumentNullException(nameof(command));

        var ts = new List<(String name, Task<CommandReplyModel?> task)>();
        foreach (var item in SelectKeys)
        {
            var online = NodeOnline.FindById(item.ToInt());
            if (online != null && online.Node != null)
            {
                ts.Add((online.Name, _starFactory.SendNodeCommandAsync(online.Node.Code, command, argument, 0, 300, 0, HttpContext.RequestAborted)));
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
