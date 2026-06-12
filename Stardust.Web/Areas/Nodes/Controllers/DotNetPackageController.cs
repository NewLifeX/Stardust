using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Web;
using Stardust.Data.Nodes;
using XCode.Membership;

namespace Stardust.Web.Areas.Nodes.Controllers;

/// <summary>dotNet安装包管理。管理面向不同操作系统和CPU架构的.NET运行时安装包</summary>
[Menu(44)]
[NodesArea]
public class DotNetPackageController : EntityController<DotNetPackage>
{
    static DotNetPackageController()
    {
        LogOnChange = true;

        ListFields.RemoveField("Source", "Size", "FileHash", "Remark", "AutoImport", "CreateIP", "UpdateIP");
        ListFields.RemoveCreateField().RemoveRemarkField();

        {
            var df = ListFields.AddListField("Log", "UpdateTime");
            df.DisplayName = "审计日志";
            df.Header = "审计日志";
            df.Url = "/Admin/Log?category=dotNet安装包&linkId={Id}";
            df.Target = "_frame";
        }
    }

    protected override IEnumerable<DotNetPackage> Search(Pager p)
    {
        var version = p["version"];
        var kind = p["kind"];
        var osKind = (Stardust.Models.OSKind)p["osKind"].ToInt();
        var architecture = (Stardust.Models.CpuArch)p["architecture"].ToInt();
        var force = p["force"]?.ToBoolean();
        var channel = (NodeChannels)p["channel"].ToInt();
        var autoImport = p["autoImport"]?.ToBoolean();
        var enable = p["enable"]?.ToBoolean();
        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();
        var key = p["q"];

        return DotNetPackage.Search(version, kind, osKind, architecture, force, channel, autoImport, enable, start, end, key, p);
    }
}
