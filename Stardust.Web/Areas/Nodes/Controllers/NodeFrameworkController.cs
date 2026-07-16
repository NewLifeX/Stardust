using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Serialization;
using NewLife.Web;
using Stardust.Data.Nodes;
using NewLife.Remoting.Models;
using Stardust.Models;
using XCode;
using XCode.Membership;
using XCode.Model;

namespace Stardust.Web.Areas.Nodes.Controllers;

[DisplayName("节点框架")]
[Description("管理各个节点的.NET框架，安装新版框架，卸载旧版框架")]
[Menu(70)]
[NodesArea]
public class NodeFrameworkController : EntityController<Node>
{
    private readonly StarFactory _starFactory;

    public NodeFrameworkController(StarFactory starFactory)
    {
        _starFactory = starFactory;
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<Node> Search(Pager p)
    {
        var rids = p["areaId"].SplitAsInt("/");
        var provinceId = rids.Length > 0 ? rids[0] : -1;
        var cityId = rids.Length > 1 ? rids[1] : -1;

        var projectId = p["projectId"].ToInt(-1);
        var category = p["category"];
        var product = p["product"];
        var osKind = p["osKind"];
        var version = p["version"];
        var runtime = p["runtime"];
        var framework = p["framework"];
        var arch = p["arch"];
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        var kind = (OSKinds)osKind.ToInt(-1);
        if (kind < 0)
        {
            if (!Enum.TryParse(osKind, out kind)) kind = (OSKinds)(-1);
        }

        return Node.Search(projectId, provinceId, cityId, category, product, kind, version, runtime, framework, arch, enable, start, end, p["Q"], p);
    }

    [DisplayName("安装")]
    [EntityAuthorize((PermissionFlags)16)]
    public async Task<ActionResult> InstallFramework(String ver, String kind)
    {
        if (GetRequest("keys") == null) throw new ArgumentNullException(nameof(SelectKeys));
        if (ver.IsNullOrEmpty()) throw new ArgumentNullException(nameof(ver));

        ver = ver?.Trim();
        kind = kind?.Trim();

        var bf = new BatchFinder<Int32, Node>();
        bf.Add(SelectKeys.Select(e => e.ToInt()));

        var ts = new List<(String name, Task<CommandReplyModel?> task)>();
        foreach (var item in SelectKeys)
        {
            var node = bf.FindByKey(item.ToInt());
            if (node == null || node.Code.IsNullOrEmpty()) continue;

            // 为每个节点按 OS/Arch 解析匹配的安装包
            var pkg = DotNetPackage.ResolveForNode(ver, kind, node);
            if (pkg == null) continue;

            var source = pkg.Source;
            if (!source.IsNullOrEmpty() && !pkg.FileName.IsNullOrEmpty() && source.EndsWith(pkg.FileName))
                source = source.Substring(0, source.Length - pkg.FileName.Length);

            var fmodel = new FrameworkModel
            {
                Version = $"{pkg.Version}-{pkg.Kind}",
                BaseUrl = source,
                Force = true,
            };

            ts.Add((node.Name, _starFactory.SendNodeCommandAsync(node.Code, "framework/install", fmodel.ToJson(), 0, 30 * 24 * 3600, 0, HttpContext.RequestAborted)));
        }

        if (ts.Count == 0) return JsonRefresh("没有找到匹配的安装包，请检查版本和安装类型");

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

    [DisplayName("卸载")]
    [EntityAuthorize((PermissionFlags)16)]
    public async Task<ActionResult> UninstallFramework(String ver)
    {
        if (GetRequest("keys") == null) throw new ArgumentNullException(nameof(SelectKeys));
        if (ver.IsNullOrEmpty()) throw new ArgumentNullException(nameof(ver));

        ver = ver.Trim();

        var bf = new BatchFinder<Int32, Node>();
        bf.Add(SelectKeys.Select(e => e.ToInt()));

        var model = new FrameworkModel { Version = ver, BaseUrl = null, Force = true };
        var args = model.ToJson();

        var ts = new List<(String name, Task<CommandReplyModel?> task)>();
        foreach (var item in SelectKeys)
        {
            var node = bf.FindByKey(item.ToInt());
            if (node != null && !node.Code.IsNullOrEmpty())
            {
                ts.Add((node.Name, _starFactory.SendNodeCommandAsync(node.Code, "framework/uninstall", args, 0, 30 * 24 * 3600, 0, HttpContext.RequestAborted)));
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
