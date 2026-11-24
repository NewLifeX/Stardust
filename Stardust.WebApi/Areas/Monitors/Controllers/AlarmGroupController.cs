using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data;
using Stardust.Data.Monitors;
using Stardust.Web.Areas;
using Stardust.Data.Platform;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Areas.Monitors.Controllers;

/// <summary>告警组。按应用/节点分组的告警规则</summary>
[Menu(20)]
[MonitorsArea]
public class AlarmGroupController : WebEntityController<AlarmGroup>
{
    static AlarmGroupController()
    {
        LogOnChange = true;

        {
            var df = ListFields.AddListField("test", "CreateUser");
            df.DisplayName = "测试";
            df.Url = "/Monitors/AlarmGroup/ExecuteNow?id={Id}";
            df.DataAction = "action";
        }
        {
            var df = ListFields.AddListField("History", "CreateUser");
            df.DisplayName = "告警历史";
            df.Header = "告警历史";
            df.Url = "/Monitors/AlarmHistory?groupId={Id}";
        }
        {
            var df = ListFields.AddListField("Log", "CreateUser");
            df.DisplayName = "审计日志";
            df.Header = "审计日志";
            df.Url = "/Admin/Log?category=告警组&linkId={Id}";
            df.Target = "_frame";
        }
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<AlarmGroup> Search(Pager p)
    {
        var id = p["Id"].ToInt(-1);
        if (id > 0)
        {
            var app = ProjectScopeRules.FilterVisible(AlarmGroup.FindById(id));
            if (app != null) return new[] { app };
        }

        var name = p["name"];
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return AlarmGroup.Search(name, enable, start, end, p["Q"], p);
    }

    /// <summary>马上执行</summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [EntityAuthorize(PermissionFlags.Update)]
    public ActionResult ExecuteNow(String id)
    {
        try
        {
            var entity = ProjectScopeRules.FilterVisible(AlarmGroup.FindById(id.ToInt()));
            if (entity != null && entity.Enable)
            {
                var hi = RobotHelper.SendAlarm(entity.Name, entity.WebHook, "报警测试", entity.Content);
                if (!hi.Success && !hi.Error.IsNullOrEmpty()) throw new Exception(hi.Error);
            }

            return JsonRefresh("OK", 0);
        }
        catch (Exception ex)
        {
            return Json(500, "执行失败！" + ex.Message);
        }
    }

    /// <summary>行级掩码规则（声明式，替代分散的 CropRow 与 Detail/Insert/Update/Delete override，语义逐键保真）：
    /// 全视图（列表/详情/导出/写响应）WebHook 非空时掩码哨兵 "••••••"（非部分明文，回填提交由 Valid 守卫识别还原）。
    /// 刀3 偏离一期「值级 null」惯例：F2 编辑回填需要哨兵语义（null 回填→PUT 空→静默清库）。
    /// 掩码只改写投影行，不触碰共享缓存中的原实体。</summary>
    private static readonly Dictionary<String, Object> _masks = new(StringComparer.OrdinalIgnoreCase) { ["WebHook"] = MaskPlaceholder };

    /// <summary>全视图统一掩码（原行为：五路出参全掩）。</summary>
    protected override IDictionary<String, Object> GetRowMasks(ViewKinds view) => _masks;

    /// <summary>写入校验：GET 回显掩码 + POST 守卫（task-95 二期刀3，F2 消解闭环）。
    /// 前端编辑抽屉从列表行回填且 PUT 恒带 webHook 键（alarmGroup.logic groupBody）——行输出
    /// 掩码哨兵 "••••••"（非 null）回填表单后原样提交，此处识别哨兵恢复库中原值并清脏标记，
    /// 避免「改任意字段→webHook 被行裁空值静默清库」；空值放行=允许显式清空；用户输入真实 URL 直通。
    /// ExecuteNow 服务端取库中实体，不受出参掩码影响。</summary>
    /// <param name="entity">实体</param>
    /// <param name="type">操作类型</param>
    /// <param name="post">是否提交</param>
    /// <returns>校验结果</returns>
    protected override Boolean Valid(AlarmGroup entity, DataObjectMethodType type, Boolean post)
    {
        if (!post)
        {
            // GET 查询后：编辑表单回显掩码
            if (type == DataObjectMethodType.Update && !entity.WebHook.IsNullOrEmpty()) entity.WebHook = MaskPlaceholder;

            return base.Valid(entity, type, post);
        }

        // POST 保存前：哨兵值恢复原值
        var ent = entity as IEntity;
        if (ent.Dirtys[nameof(AlarmGroup._.WebHook)] && entity.WebHook == MaskPlaceholder)
        {
            var old = ProjectScopeRules.FilterVisible(AlarmGroup.FindById(entity.Id));
            if (old != null)
            {
                entity.WebHook = old.WebHook;
                ent.Dirtys[nameof(AlarmGroup._.WebHook)] = false;
            }
        }

        return base.Valid(entity, type, post);
    }

    /// <summary>WebHook 掩码哨兵（lead 刀6 同款定值；非部分明文，回填提交由 Valid 守卫识别还原）</summary>
    private const String MaskPlaceholder = "••••••";
}
