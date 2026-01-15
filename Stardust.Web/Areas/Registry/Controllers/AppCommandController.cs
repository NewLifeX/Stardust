using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data;

namespace Stardust.Web.Areas.Registry.Controllers;

[Menu(58, false)]
[RegistryArea]
public class AppCommandController : RegistryEntityController<AppCommand>
{
    static AppCommandController()
    {
        ListFields.RemoveField("StartTime", "Expire", "UpdateUserId");
        ListFields.RemoveCreateField();
        //ListFields.AddListField("StartTime", null, "Result");
        ListFields.AddListField("Expire", null, "Result");
        ListFields.AddListField("CreateUser", "UpdateTime");

        {
            var df = ListFields.GetField("Command") as ListField;
            df.Url = "/Registry/AppCommand?appId={AppId}&command={Command}";
        }
        ListFields.TraceUrl();
    }

    protected override IEnumerable<AppCommand> Search(Pager p)
    {
        var appId = p["appId"].ToInt(-1);
        var command = p["command"];

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return AppCommand.Search(appId, command, start, end, p["Q"], p);
    }
}