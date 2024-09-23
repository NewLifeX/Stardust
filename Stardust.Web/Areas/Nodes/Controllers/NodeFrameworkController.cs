using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Serialization;
using NewLife.Web;
using Stardust.Data.Nodes;
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
    public async Task<ActionResult> InstallFramework(String ver, String baseUrl)
    {
        if (GetRequest("keys") == null) throw new ArgumentNullException(nameof(SelectKeys));
        if (ver.IsNullOrEmpty()) throw new ArgumentNullException(nameof(ver));

        ver = ver?.Trim();
        baseUrl = baseUrl?.Trim();

        var bf = new BatchFinder<Int32, Node>();
        bf.Add(SelectKeys.Select(e => e.ToInt()));

        //var baseUrl = "";
        var set = NewLife.Setting.Current;
        var server = set.PluginServer;
        if (baseUrl.IsNullOrEmpty() && !server.IsNullOrEmpty() && !server.Contains("x.newlifex.com", StringComparison.CurrentCultureIgnoreCase))
        {
            baseUrl = server.TrimEnd('/');
            if (!baseUrl.EndsWithIgnoreCase("/dotnet")) baseUrl += "/dotnet";
        }

        var model = new FrameworkModel { Version = ver, BaseUrl = baseUrl, Force = true };
        var args = model.ToJson();

        var ts = new List<Task<Int32>>();
        foreach (var item in SelectKeys)
        {
            var node = bf.FindByKey(item.ToInt());
            if (node != null && !node.Code.IsNullOrEmpty())
            {
                ts.Add(_starFactory.SendNodeCommand(node.Code, "framework/install", args, 0, 30 * 24 * 3600, 0));

            }
        }

        var rs = await Task.WhenAll(ts);

        return JsonRefresh($"操作成功！下发指令{rs.Length}个，成功{rs.Count(e => e > 0)}个");
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

        var ts = new List<Task<Int32>>();
        foreach (var item in SelectKeys)
        {
            var node = bf.FindByKey(item.ToInt());
            if (node != null && !node.Code.IsNullOrEmpty())
            {
                ts.Add(_starFactory.SendNodeCommand(node.Code, "framework/uninstall", args, 0, 30 * 24 * 3600, 0));

            }
        }

        var rs = await Task.WhenAll(ts);

        return JsonRefresh($"操作成功！下发指令{rs.Length}个，成功{rs.Count(e => e > 0)}个");
    }
}