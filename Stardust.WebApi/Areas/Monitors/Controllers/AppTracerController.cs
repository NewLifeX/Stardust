using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Log;
using NewLife.Web;
using Stardust.Data;
using Stardust.Data.Monitors;
using Stardust.Server.Services;
using Stardust.Web.Areas;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Areas.Monitors.Controllers;

/// <summary>应用跟踪器。管理每个应用实例的采样率、周期、WebHook 等参数配置</summary>
[Menu(90)]
[MonitorsArea]
public class AppTracerController : WebEntityController<AppTracer>
{
    private readonly ITraceItemStatService _traceItemStatService;

    static AppTracerController()
    {
        LogOnChange = true;

        ListFields.RemoveField("AppId", "TimeoutExcludes", "VipClients", "Nodes", "AlarmRobot", "WhiteList", "Excludes");
        ListFields.RemoveCreateField();

        {
            var df = ListFields.GetField("ProjectName") as ListField;
            df.Url = "/Platform/GalaxyProject?Id={ProjectId}";
            df.Target = "_frame";
        }
        {
            var df = ListFields.AddListField("DayMonitor", null, "Category");
            df.Header = "每日监控";
            df.DisplayName = "每日监控";
            df.Title = "{AppName}监控。该应用每日监控数据";
            df.Url = "/Monitors/AppDaystat?monitorId={ID}";
            df.Target = "_frame";
        }

        {
            var df = ListFields.GetField("ItemCount") as ListField;
            //df.Header = "每日监控";
            df.Text = "{ItemCount}";
            df.Title = "{AppName}监控项。应用下的所有监控项（埋点）";
            df.Url = "/Monitors/TraceItem?appId={ID}";
            df.Target = "_frame";
        }

        //{
        //    var df = ListFields.AddListField("Online", "UpdateUser");
        //    df.Header = "在线实例";
        //    df.DisplayName = "在线实例";
        //    df.Title = "查看该应用的在线实例应用";
        //    df.Url = "/registry/AppOnline?appId={AppId}";
        //    df.DataVisible = e => e is AppTracer entity && entity.AppId > 0;
        //}

        //{
        //    var df = ListFields.AddListField("Meter", "UpdateUser");
        //    df.DisplayName = "性能";
        //    df.Header = "性能";
        //    df.Url = "/Registry/AppMeter?appId={ID}";
        //}

        {
            var df = ListFields.AddListField("Log", "UpdateUser");
            df.DisplayName = "审计日志";
            df.Header = "审计日志";
            df.Url = "/Admin/Log?category=应用跟踪器&linkId={ID}";
            df.Target = "_frame";
        }
    }

    public AppTracerController(ITraceItemStatService traceItemStatService) => _traceItemStatService = traceItemStatService;

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<AppTracer> Search(Pager p)
    {
        var id = p["monitorId"].ToInt(-1);
        if (id <= 0) id = p["id"].ToInt(-1);
        if (id > 0)
        {
            var app = AppTracer.FindByKey(id);
            if (app != null) return [CropRow(app)];
        }
        var appId = p["appId"].ToInt(-1);
        if (appId > 0)
        {
            var list = AppTracer.FindAllByAppId(appId);
            if (list.Count == 0)
            {
                var app = App.FindById(appId);
                if (app != null)
                {
                    var entity = new AppTracer
                    {
                        AppId = appId,
                        Name = app.Name,
                        ProjectId = app.ProjectId,
                        Category = app.Category
                    };
                    entity.Insert();
                }
            }
        }

        var projectId = p["projectId"].ToInt(-1);
        var category = p["category"];
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        p.RetrieveState = true;

        return AppTracer.Search(projectId, appId, category, enable, start, end, p["Q"], p).Select(CropRow);
    }

    protected override Int32 OnDelete(AppTracer entity)
    {
        using var tran = AppTracer.Meta.CreateTrans();

        var rs = base.OnDelete(entity);

        var list = AppDayStat.FindAllByAppId(entity.ID);
        list.Delete();

        //var list2 = TraceDayStat.FindAllByAppId(entity.ID);
        //list2.Delete();
        TraceDayStat.DeleteByAppAndItem(entity.ID, 0);

        tran.Commit();

        return rs;
    }

    protected override Boolean Valid(AppTracer entity, DataObjectMethodType type, Boolean post)
    {
        if (!post)
        {
            // task-95 二期刀4：GET 查询后编辑表单回显掩码
            if (type == DataObjectMethodType.Update && !entity.WebHook.IsNullOrEmpty()) entity.WebHook = MaskPlaceholder;

            return base.Valid(entity, type, post);
        }

        // task-95 二期刀4：POST 保存前哨兵恢复原值——必须先于既有 Fix/Update 链，
        // 否则掩码哨兵会随 entity.Update() 写库（F2 消解：前端行回填+PUT 恒带键）
        var ent = entity as IEntity;
        if (ent.Dirtys[nameof(AppTracer._.WebHook)] && entity.WebHook == MaskPlaceholder)
        {
            var old = AppTracer.FindByKey(entity.ID);
            if (old != null)
            {
                entity.WebHook = old.WebHook;
                ent.Dirtys[nameof(AppTracer._.WebHook)] = false;
            }
        }

        if (post)
        {
            // 更新时关联应用
            switch (type)
            {
                case DataObjectMethodType.Update:
                case DataObjectMethodType.Insert:
                    if (entity.AppId == 0)
                    {
                        var app = App.FindByName(entity.Name);
                        if (app != null) entity.AppId = app.Id;
                    }

                    break;
            }

            // 修正埋点数
            switch (type)
            {
                case DataObjectMethodType.Update:
                    entity.Fix();
                    entity.Update();

                    break;
            }
        }

        var rs = base.Valid(entity, type, post);

        if (post && type == DataObjectMethodType.Delete)
        {
            var list = AppDayStat.FindAllByAppId(entity.ID);
            list.Delete();

            var list2 = TraceItem.FindAllByApp(entity.ID);
            list2.Delete();

            TraceDayStat.DeleteByAppAndItem(entity.ID, 0);
        }

        return rs;
    }

    [EntityAuthorize(PermissionFlags.Update)]
    public ActionResult Fix()
    {
        foreach (var item in SelectKeys)
        {
            var app = AppTracer.FindByID(item.ToInt());
            if (app != null)
            {
                XTrace.WriteLine("Correcting {0}/{1}", app.Name, app.ID);

                //_traceItemStatService.Add(app.ID);
                _traceItemStatService.Process(app.ID, 30);

                app.Fix();
                app.Update();
            }
        }

        return JsonRefresh("成功！");
    }

    [EntityAuthorize(PermissionFlags.Update)]
    public ActionResult FixOldData()
    {
        foreach (var item in SelectKeys)
        {
            var app = AppTracer.FindByID(item.ToInt());
            if (app != null)
            {
                XTrace.WriteLine("Correcting legacy data {0}/{1}", app.Name, app.ID);

                {
                    var list = TraceDayStat.FindAllByAppId(app.ID);
                    foreach (var st in list)
                    {
                        var ti = app.GetOrAddItem(st.Name);
                        if (st.ItemId == 0 && !st.Name.IsNullOrEmpty())
                        {
                            st.ItemId = ti.Id;
                            st.SaveAsync();
                        }
                        if (ti.CreateTime.Year < 2000 || ti.CreateTime > st.CreateTime)
                        {
                            ti.CreateTime = st.CreateTime;
                            ti.SaveAsync(3_000);
                        }
                    }

                    app.Days = list.DistinctBy(e => e.StatDate.Date).Count();
                    app.Total = list.Sum(e => e.Total);
                }
                {
                    var list = TraceHourStat.FindAllByAppId(app.ID);
                    foreach (var st in list)
                    {
                        if (st.ItemId == 0 && !st.Name.IsNullOrEmpty())
                        {
                            var ti = app.GetOrAddItem(st.Name);
                            st.ItemId = ti.Id;
                            st.SaveAsync();
                        }
                    }
                }

                //app.ItemCount = app.TraceItems.Count;
                app.ItemCount = TraceItem.FindCountByApp(app.ID);
                app.Update();
            }
        }

        return JsonRefresh("成功！");
    }

    /// <summary>查看单行。出参投影掩码 WebHook（task-95 二期刀4）</summary>
    /// <param name="id">主键</param>
    /// <returns>掩码后的单行数据</returns>
    public override ApiResponse<AppTracer> Detail(String id)
    {
        var res = base.Detail(id);
        if (res?.Data != null) res.Data = CropRow(res.Data);
        return res;
    }

    /// <summary>添加数据。响应快照掩码 WebHook（task-95 刀4）</summary>
    /// <param name="model">提交的模型</param>
    /// <returns>掩码后的新增结果</returns>
    public override async Task<ApiResponse<AppTracer>> Insert(AppTracer model)
    {
        var res = await base.Insert(model);
        if (res?.Data != null) res.Data = CropRow(res.Data);
        return res;
    }

    /// <summary>更新数据。响应快照掩码 WebHook（task-95 刀4）</summary>
    /// <param name="id">主键</param>
    /// <param name="model">提交的模型</param>
    /// <returns>掩码后的更新结果</returns>
    public override async Task<ApiResponse<AppTracer>> Update(String id, AppTracer model)
    {
        var res = await base.Update(id, model);
        if (res?.Data != null) res.Data = CropRow(res.Data);
        return res;
    }

    /// <summary>删除数据。响应快照掩码 WebHook（task-95 刀4）</summary>
    /// <param name="id">主键</param>
    /// <returns>掩码后的删除结果</returns>
    public override ApiResponse<AppTracer> Delete(String id)
    {
        var res = base.Delete(id);
        if (res?.Data != null) res.Data = CropRow(res.Data);
        return res;
    }

    /// <summary>WebHook 掩码哨兵（lead 刀6 同款定值；非部分明文，回填提交由 Valid 守卫识别还原）</summary>
    private const String MaskPlaceholder = "••••••";

    /// <summary>出参投影：克隆实体并置 WebHook 为掩码哨兵，杜绝列表/详情/写响应泄漏（task-95 二期刀4）。
    /// 刀3/4 偏离一期「值级 null」惯例：F2 编辑回填（tracerPutBody 恒带 webHook）需要哨兵语义（null 回填→PUT 空→静默清库），已随小步报披露。</summary>
    /// <param name="src">原始实体</param>
    /// <returns>WebHook 掩码的克隆实体</returns>
    private static AppTracer CropRow(AppTracer src)
    {
        var dst = new AppTracer();
        foreach (var fi in AppTracer.Meta.Fields)
        {
            dst[fi.Name] = src[fi.Name];
        }
        dst.WebHook = src.WebHook.IsNullOrEmpty() ? null : MaskPlaceholder;
        return dst;
    }
}
