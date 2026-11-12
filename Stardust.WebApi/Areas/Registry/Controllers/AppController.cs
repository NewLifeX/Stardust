using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Data;
using NewLife.Web;
using Stardust.Data;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Areas.Registry.Controllers;

/// <summary>应用管理。接入星尘的应用</summary>
[RegistryArea]
[Menu(99)]
public class AppController : RegistryEntityController<App>
{
    static AppController()
    {
        LogOnChange = true;

        ListFields.RemoveField("ProjectId", "Secret", "WebHook", "AllowControlNodes", "Remark");
        ListFields.RemoveCreateField();
        ListFields.RemoveUpdateField();

        {
            var df = ListFields.GetField("ProjectName") as ListField;
            df.Url = "/Platform/GalaxyProject?projectId={ProjectId}";
            df.Target = "_frame";
        }
        {
            var df = ListFields.GetField("Name") as ListField;
            df.Url = "/Registry/App/Detail?id={Id}";
            df.Target = "_blank";
        }
        {
            var df = ListFields.AddListField("AppLog", "Version");
            df.DisplayName = "应用日志";
            df.Url = "/Registry/AppLog?appId={Id}";
        }
        {
            var df = ListFields.AddListField("Log", "CreateUser");
            df.DisplayName = "审计日志";
            df.Url = "/Admin/Log?category=应用系统&linkId={Id}";
            df.Target = "_frame";
        }
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<App> Search(Pager p)
    {
        var id = p["Id"].ToInt(-1);
        if (id > 0)
        {
            var node = App.FindById(id);
            if (node != null) return [CropRow(node)];
        }

        var projectId = p["projectId"].ToInt(-1);
        var category = p["category"];
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return App.Search(projectId, category, enable, start, end, p["Q"], p).Select(CropRow);
    }

    /// <summary>查看单行。出参投影裁剪 Secret/WebHook 明文（task-95 二期刀2，OBS-3 CropPfx 同款语义：值级置 null）。
    /// 预览链已改道 /Config/GetAllByApp（刀1），行/详情 Secret 零消费方。</summary>
    /// <param name="id">主键</param>
    /// <returns>裁剪后的单行数据</returns>
    public override ApiResponse<App> Detail(String id)
    {
        var res = base.Detail(id);
        if (res?.Data != null) res.Data = CropRow(res.Data);
        return res;
    }

    /// <summary>添加数据。响应快照裁剪敏感双键（task-95 刀2）</summary>
    /// <param name="model">提交的模型</param>
    /// <returns>裁剪后的新增结果</returns>
    public override async Task<ApiResponse<App>> Insert(App model)
    {
        var res = await base.Insert(model);
        if (res?.Data != null) res.Data = CropRow(res.Data);
        return res;
    }

    /// <summary>更新数据。响应快照裁剪敏感双键（task-95 刀2；新提交值与旧存量值都不得回显）</summary>
    /// <param name="id">主键</param>
    /// <param name="model">提交的模型</param>
    /// <returns>裁剪后的更新结果</returns>
    public override async Task<ApiResponse<App>> Update(String id, App model)
    {
        var res = await base.Update(id, model);
        if (res?.Data != null) res.Data = CropRow(res.Data);
        return res;
    }

    /// <summary>删除数据。响应快照裁剪敏感双键（task-95 刀2）</summary>
    /// <param name="id">主键</param>
    /// <returns>裁剪后的删除结果</returns>
    public override ApiResponse<App> Delete(String id)
    {
        var res = base.Delete(id);
        if (res?.Data != null) res.Data = CropRow(res.Data);
        return res;
    }

    /// <summary>写入校验：GET 回显掩码 + POST 守卫（task-95 刀2，AppDeployController.RepoPassword 先例同款）。
    /// Vue 编辑走 partial-PUT：Secret 留空不提交、WebHook 不在表单（不 Dirty）→ 天然安全；守卫双键仍拦 "*****"
    /// （lead 补令：防未来表单演进引入 Dirty 路径）。MVC 编辑表单回填 "*****" 原样提交 → 此处从库恢复原值并清脏标记。</summary>
    /// <param name="entity">实体</param>
    /// <param name="type">操作类型</param>
    /// <param name="post">是否提交</param>
    /// <returns>校验结果</returns>
    protected override Boolean Valid(App entity, DataObjectMethodType type, Boolean post)
    {
        if (!post)
        {
            // GET 查询后：编辑表单回显脱敏
            if (type == DataObjectMethodType.Update && !entity.Secret.IsNullOrEmpty()) entity.Secret = "*****";

            return base.Valid(entity, type, post);
        }

        // POST 保存前：掩码值恢复原值（双键守卫）；空值放行=允许显式清空
        var ent = entity as IEntity;
        foreach (var name in new[] { nameof(App._.Secret), nameof(App._.WebHook) })
        {
            if (ent.Dirtys[name] && (String?)entity[name] == "*****")
            {
                var old = App.FindById(entity.Id);
                if (old != null)
                {
                    entity[name] = old[name];
                    ent.Dirtys[name] = false;
                }
            }
        }

        return base.Valid(entity, type, post);
    }

    /// <summary>出参投影：克隆实体并置空 Secret/WebHook 明文，杜绝列表/详情/写响应泄漏（task-95 二期刀2）。
    /// 克隆为全新实例，不原地改写共享缓存中的原实体；Valid 的 GET 掩码/POST 回填链路走原实体，写路径语义不变。</summary>
    /// <param name="src">原始实体</param>
    /// <returns>不含 Secret/WebHook 明文的克隆实体</returns>
    private static App CropRow(App src)
    {
        var dst = new App();
        foreach (var fi in App.Meta.Fields)
        {
            dst[fi.Name] = src[fi.Name];
        }
        dst.Secret = null;
        dst.WebHook = null;
        return dst;
    }

    /// <summary>搜索</summary>
    /// <param name="category"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    [LovList]
    public ActionResult AppSearch(Int32 projectId, String category, String key = null)
    {
        var page = new PageParameter { PageSize = 20 };

        //// 默认排序
        //if (page.Sort.IsNullOrEmpty()) page.Sort = _.Name;

        var list = App.Search(projectId, category, true, DateTime.MinValue, DateTime.MinValue, key, page);

        return Json(0, null, list.Select(e => new
        {
            e.Id,
            e.Name,
            e.DisplayName,
            e.Category,
        }).ToArray());
    }

    /// <summary>启用禁用下线告警</summary>
    /// <param name="enable"></param>
    /// <returns></returns>
    [EntityAuthorize(PermissionFlags.Update)]
    public ActionResult SetAlarm(Boolean enable = true)
    {
        foreach (var item in SelectKeys)
        {
            var dt = App.FindById(item.ToInt());
            if (dt != null)
            {
                dt.AlarmOnOffline = enable;
                dt.Save();
            }
        }

        return JsonRefresh("操作成功！");
    }
}
