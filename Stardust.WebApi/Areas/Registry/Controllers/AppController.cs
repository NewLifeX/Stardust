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
            if (node != null) return [node];
        }

        var projectId = p["projectId"].ToInt(-1);
        var category = p["category"];
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return App.Search(projectId, category, enable, start, end, p["Q"], p);
    }

    /// <summary>行级掩码规则（声明式，替代分散的 CropRow 与 Detail/Insert/Update/Delete override，语义逐键保真）：
    /// 全视图（列表/详情/导出/写响应）Secret/WebHook 值级置 null（键不删，task-95 刀2）。
    /// 预览链已改道 /Config/GetAllByApp（刀1），行/详情 Secret 零消费方。
    /// Valid 守卫仍负责 "*****" 回显与恢复；掩码只改写投影行，不触碰共享缓存中的原实体。</summary>
    private static readonly Dictionary<String, Object> _masks = new(StringComparer.OrdinalIgnoreCase) { ["Secret"] = null, ["WebHook"] = null };

    /// <summary>全视图统一掩码（原行为：五路出参全掩）。</summary>
    protected override IDictionary<String, Object> GetRowMasks(ViewKinds view) => _masks;

    /// <summary>详情面裁掉 Secret（密钥不随详情回传），WebHook 保留键、掩码为 null</summary>
    protected override ICollection<String> GetDetailHiddenFields() => ["Secret"];

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
