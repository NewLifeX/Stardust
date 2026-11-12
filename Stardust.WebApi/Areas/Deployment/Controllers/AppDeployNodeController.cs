using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using Stardust.Data.Deployment;
using Stardust.Web.Services;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Areas.Deployment.Controllers;

/// <summary>部署节点。部署集下的目标节点</summary>
[Menu(88, false)]
[DeploymentArea]
public class AppDeployNodeController : DeploymentEntityController<AppDeployNode>
{
    static AppDeployNodeController()
    {
        ListFields.RemoveCreateField();
        AddFormFields.RemoveCreateField();
        ListFields.TraceUrl();

        // 应用程序版本由心跳自动回填（AppDeployNode.Fill(AppOnline)/Fill(AppInfo)），install 发布回填部署版本（Version），均不开放手工编辑（与 Stardust.Web 同步）
        AddFormFields.RemoveField("AppVersion");
        EditFormFields.RemoveField("AppVersion");

        LogOnChange = true;

        {
            var df = ListFields.GetField("DeployName") as ListField;
            df.Url = "/Deployment/AppDeploy?deployId={DeployId}";
        }
    }

    private readonly DeployService _deployService;
    public AppDeployNodeController(DeployService deployService)
    {
        _deployService = deployService;
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<AppDeployNode> Search(Pager p)
    {
        var id = p["id"].ToInt(-1);
        if (id > 0)
        {
            var entity = AppDeployNode.FindById(id);
            if (entity != null) return [CropRow(entity)];
        }

        var deployId = p["deployId"].ToInt(-1);
        var appId = p["appId"].ToInt(-1);
        var nodeId = p["nodeId"].ToInt(-1);
        var enable = p["enable"]?.ToBoolean();

        PageSetting.EnableAdd = deployId > 0 || appId > 0;
        PageSetting.EnableNavbar = false;

        // 部署集优先：按部署集直接过滤其节点
        if (deployId > 0) return AppDeployNode.Search(deployId, nodeId, enable, p["Q"], p).Select(CropRow);

        // 按应用：先换取该应用全部部署集 Id，再按集合过滤；应用无部署集即无节点
        if (appId > 0)
        {
            var setIds = AppDeploy.FindAllByAppId(appId).Select(e => e.Id).ToArray();
            if (setIds.Length == 0) return [];

            return AppDeployNode.Search(setIds, nodeId, enable, p["Q"], p).Select(CropRow);
        }

        return AppDeployNode.Search(-1, nodeId, enable, p["Q"], p).Select(CropRow);
    }

    /// <summary>查看单行。UserName 掩码回显 "*****"（task-95 二期刀8/N2：Vue 编辑抽屉走 Detail 取数，掩码回显防 F1 静默清库）。
    /// 克隆后改写，不原地污染共享缓存中的原实体（原实体仍是 Valid 守卫的恢复源）。</summary>
    /// <param name="id">主键</param>
    /// <returns>掩码回显后的单行数据</returns>
    public override ApiResponse<AppDeployNode> Detail(String id)
    {
        var res = base.Detail(id);
        if (res?.Data != null)
        {
            var dst = new AppDeployNode();
            foreach (var fi in AppDeployNode.Meta.Fields)
            {
                dst[fi.Name] = res.Data[fi.Name];
            }
            if (!dst.UserName.IsNullOrEmpty()) dst.UserName = "*****";
            res.Data = dst;
        }
        return res;
    }

    /// <summary>写入校验：POST 守卫（task-95 二期刀8/N2，MySqlNode 刀0 同款）。前端编辑抽屉以 Detail 全量展开为 PUT 提交体（F1 形态），
    /// Detail 掩码回显 "*****"，未修改即原样提交，此处从库中恢复原值并清脏标记，杜绝静默清库；
    /// 空串放行=显式清空（允许移除运行身份用户名）。</summary>
    /// <param name="entity">实体</param>
    /// <param name="type">操作类型</param>
    /// <param name="post">是否提交</param>
    /// <returns>校验结果</returns>
    protected override Boolean Valid(AppDeployNode entity, DataObjectMethodType type, Boolean post)
    {
        if (!post) return base.Valid(entity, type, post);

        // POST 保存前：掩码值恢复原值，防 Detail 掩码回显经 PUT 全量展开造成静默清库
        if ((entity as IEntity).Dirtys[nameof(AppDeployNode._.UserName)])
        {
            if (entity.UserName == "*****")
            {
                // 未修改用户名：从数据库读取原值回填，清除脏标记
                var old = AppDeployNode.FindById(entity.Id);
                if (old != null)
                {
                    entity.UserName = old.UserName;
                    (entity as IEntity).Dirtys[nameof(AppDeployNode._.UserName)] = false;
                }
            }
            // 空串：保持空值，显式清空放行
        }

        var node = entity.Node;
        if (node != null) entity.IP = node.IP;

        entity.FixOldUserName();
        entity.Deploy?.Fix();

        return base.Valid(entity, type, post);
    }

    protected override Int32 OnInsert(AppDeployNode entity)
    {
        var rs = base.OnInsert(entity);
        entity.Deploy?.Fix();
        return rs;
    }

    protected override Int32 OnUpdate(AppDeployNode entity)
    {
        var rs = base.OnUpdate(entity);
        entity.Deploy?.Fix();
        return rs;
    }

    protected override Int32 OnDelete(AppDeployNode entity)
    {
        var rs = base.OnDelete(entity);
        entity.Deploy?.Fix();
        return rs;
    }

    /// <summary>执行操作</summary>
    /// <param name="act">操作。install/start/stop/restart/uninstall</param>
    /// <param name="id">节点编号</param>
    /// <returns></returns>
    [EntityAuthorize(PermissionFlags.Update)]
    public async Task<ActionResult> Operate(String act, Int32 id)
    {
        var dn = AppDeployNode.FindById(id);
        if (dn == null || dn.Node == null || dn.Deploy == null) return Json(500, $"[{id}]不存在");

        var deployName = dn.DeployName;
        if (deployName.IsNullOrEmpty()) deployName = dn.Deploy?.Name;
        await _deployService.Control(dn.Deploy, dn, act, UserHost, 0, 0, cancellationToken: HttpContext.RequestAborted);

        return JsonRefresh($"在节点[{dn.NodeName}]上对应用[{deployName}]执行[{act}]操作", 1);
    }

    /// <summary>批量执行操作</summary>
    /// <param name="act">操作。install/start/stop/restart/uninstall</param>
    /// <returns></returns>
    [EntityAuthorize(PermissionFlags.Update)]
    public async Task<ActionResult> BatchOperate(String act)
    {
        var ts = new List<Task>();
        var ids = SelectKeys.Select(e => e.ToInt()).Where(e => e > 0).Distinct().ToList();
        foreach (var id in ids)
        {
            var dn = AppDeployNode.FindById(id);
            if (dn != null && dn.Node != null && dn.Deploy != null)
            {
                ts.Add(_deployService.Control(dn.Deploy, dn, act, UserHost, dn.Delay, 0, cancellationToken: HttpContext.RequestAborted));
            }
        }

        await Task.WhenAll(ts);

        return JsonRefresh($"批量执行[{act}]操作", 1);
    }

    /// <summary>列出节点上配置的日志文件</summary>
    [EntityAuthorize(PermissionFlags.Detail)]
    public async Task<ActionResult> LogFiles(Int32 id)
    {
        var dn = AppDeployNode.Find(AppDeployNode._.Id == id);
        if (dn == null || dn.Node == null) return Json(500, $"[{id}]不存在");

        try
        {
            var rs = await _deployService.ListLogFilesAsync(dn, HttpContext.RequestAborted);
            return Json(0, null, rs);
        }
        catch (Exception ex)
        {
            return Json(500, ex.Message);
        }
    }

    /// <summary>读取日志文件尾部</summary>
    [EntityAuthorize(PermissionFlags.Detail)]
    public async Task<ActionResult> LogTail(Int32 id, String file, Int64 offset = 0, Int32 maxBytes = 262144)
    {
        var dn = AppDeployNode.Find(AppDeployNode._.Id == id);
        if (dn == null || dn.Node == null) return Json(500, $"[{id}]不存在");
        if (file.IsNullOrEmpty()) return Json(500, "请指定日志文件名");

        try
        {
            var rs = await _deployService.TailLogFileAsync(dn, file, offset, maxBytes, HttpContext.RequestAborted);
            return Json(0, null, rs);
        }
        catch (Exception ex)
        {
            return Json(500, ex.Message);
        }
    }

    /// <summary>出参投影：克隆实体并置空 UserName 明文，杜绝列表行泄漏（task-95 二期刀8/N2，一期 CropRow 同款语义：值级置 null，键不删）。
    /// 克隆为全新实例，不原地改写共享缓存中的原实体；Valid 守卫链路走原实体，写路径语义不变。</summary>
    /// <param name="src">原始实体</param>
    /// <returns>不含 UserName 明文的克隆实体</returns>
    private static AppDeployNode CropRow(AppDeployNode src)
    {
        var dst = new AppDeployNode();
        foreach (var fi in AppDeployNode.Meta.Fields)
        {
            dst[fi.Name] = src[fi.Name];
        }
        dst.UserName = null;
        return dst;
    }
}
