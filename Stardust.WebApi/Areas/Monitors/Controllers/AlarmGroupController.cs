using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Web;
using Stardust.Data;
using Stardust.Data.Monitors;
using Stardust.Web.Areas;
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
            var app = AlarmGroup.FindById(id);
            if (app != null) return new[] { CropRow(app) };
        }

        var name = p["name"];
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return AlarmGroup.Search(name, enable, start, end, p["Q"], p).Select(CropRow);
    }

    /// <summary>马上执行</summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [EntityAuthorize(PermissionFlags.Update)]
    public ActionResult ExecuteNow(String id)
    {
        try
        {
            var entity = AlarmGroup.FindById(id.ToInt());
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

    /// <summary>查看单行。出参投影掩码 WebHook（task-95 二期刀3）</summary>
    /// <param name="id">主键</param>
    /// <returns>掩码后的单行数据</returns>
    public override ApiResponse<AlarmGroup> Detail(String id)
    {
        var res = base.Detail(id);
        if (res?.Data != null) res.Data = CropRow(res.Data);
        return res;
    }

    /// <summary>添加数据。响应快照掩码 WebHook（task-95 刀3）</summary>
    /// <param name="model">提交的模型</param>
    /// <returns>掩码后的新增结果</returns>
    public override async Task<ApiResponse<AlarmGroup>> Insert(AlarmGroup model)
    {
        var res = await base.Insert(model);
        if (res?.Data != null) res.Data = CropRow(res.Data);
        return res;
    }

    /// <summary>更新数据。响应快照掩码 WebHook（task-95 刀3）</summary>
    /// <param name="id">主键</param>
    /// <param name="model">提交的模型</param>
    /// <returns>掩码后的更新结果</returns>
    public override async Task<ApiResponse<AlarmGroup>> Update(String id, AlarmGroup model)
    {
        var res = await base.Update(id, model);
        if (res?.Data != null) res.Data = CropRow(res.Data);
        return res;
    }

    /// <summary>删除数据。响应快照掩码 WebHook（task-95 刀3）</summary>
    /// <param name="id">主键</param>
    /// <returns>掩码后的删除结果</returns>
    public override ApiResponse<AlarmGroup> Delete(String id)
    {
        var res = base.Delete(id);
        if (res?.Data != null) res.Data = CropRow(res.Data);
        return res;
    }

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
            var old = AlarmGroup.FindById(entity.Id);
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

    /// <summary>出参投影：克隆实体并置 WebHook 为掩码哨兵，杜绝列表/详情/写响应泄漏（task-95 二期刀3）。
    /// 刀3/4 偏离一期「值级 null」惯例：F2 编辑回填需要哨兵语义（null 回填→PUT 空→静默清库），已随小步报披露。</summary>
    /// <param name="src">原始实体</param>
    /// <returns>WebHook 掩码的克隆实体</returns>
    private static AlarmGroup CropRow(AlarmGroup src)
    {
        var dst = new AlarmGroup();
        foreach (var fi in AlarmGroup.Meta.Fields)
        {
            dst[fi.Name] = src[fi.Name];
        }
        dst.WebHook = src.WebHook.IsNullOrEmpty() ? null : MaskPlaceholder;
        return dst;
    }
}
