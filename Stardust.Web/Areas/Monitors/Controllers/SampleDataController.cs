using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data.Monitors;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Areas.Monitors.Controllers;

[Menu(0, false)]
[MonitorsArea]
public class SampleDataController : ReadOnlyEntityController<SampleData>
{
    static SampleDataController()
    {
        ListFields.RemoveField("Id", "DataId", "ItemId", "SpanId", "ParentId", "EndTime", "End");
        ListFields.AddListField("Tag", "CreateIP");

        {
            var df = ListFields.AddListField("AppName", "Name");
            df.Header = "应用";
            df.DisplayName = "{AppName}";
            df.Title = "应用监控图表";
            df.Url = "/Monitors/appDayStat?monitorId={AppId}";
        }
        //{
        //    var df = ListFields.AddListField("Name", "Success");
        //    df.Header = "操作名";
        //    df.DisplayName = "{Name}";
        //    df.Title = "{Tag}";
        //    df.Url = "traceDayStat?appId={AppId}&itemId={ItemId}";
        //}
        {
            var df = ListFields.GetField("Name") as ListField;
            df.Header = "操作名";
            df.DisplayName = "{Name}";
            df.Title = "{Tag}";
            df.Url = "/Monitors/traceDayStat?appId={AppId}&itemId={ItemId}";
        }
        //ListFields.TraceUrl();
        {
            var df = ListFields.GetField("TraceId") as ListField;
            df.Text = "追踪";
            df.Url = "/trace?id={TraceId}";
        }
    }

    protected override FieldCollection OnGetFields(ViewKinds kind, Object model)
    {
        var fields = base.OnGetFields(kind, model);

        if (kind == ViewKinds.List)
        {
            var appId = GetRequest("appId").ToInt(-1);
            if (appId > 0) fields.RemoveField("AppName");

            var itemId = GetRequest("itemId").ToInt(-1);
            if (itemId > 0) fields.RemoveField("ItemName", "Name");
        }

        return fields;
    }

    protected override IEnumerable<SampleData> Search(Pager p)
    {
        var dataId = p["dataId"].ToLong(-1);
        var traceId = p["traceId"];
        var appId = p["appId"].ToInt(-1);
        var itemId = p["itemId"].ToInt(-1);
        var success = p["success"]?.ToBoolean();

        // 指定追踪标识后，分页500
        if (!traceId.IsNullOrEmpty())
        {
            if (p.PageSize == 20) p.PageSize = 500;
        }
        if (p.Sort.IsNullOrEmpty()) p.OrderBy = SampleData._.Id.Desc();

        var start = DateTime.Today.AddDays(-30);
        var end = DateTime.Today;

        // 下钻查询
        if (dataId < 0 && appId > 0)
        {
            var kind = p["kind"];
            var date = p["date"].ToDateTime();
            var time = p["time"].ToDateTime();
            if (time.Year < 2000) time = date;

            return SampleData.Search(appId, itemId, success, kind, time, p["Q"], p);
        }

        return SampleData.Search(dataId, traceId, itemId, success, start, end, p["Q"], p);
    }

    protected override SampleData Find(Object key)
    {
        //var entity = base.Find(key);
        //var entity = SampleData.FindById(key.ToLong());
        var entity = SampleData.FindByKeyForEdit(key);
        if (entity != null) return entity;

        var entity2 = SampleData2.FindById(key.ToLong());
        if (entity2 != null)
        {
            entity = new SampleData();
            entity.CopyFrom(entity2, false);
            return entity;
        }

        throw new Exception($"无法找到数据[{key}]！");
    }
}