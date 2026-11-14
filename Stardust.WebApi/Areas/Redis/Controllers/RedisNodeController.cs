using System.ComponentModel;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Data;
using NewLife.Log;
using NewLife.Web;
using Stardust.Data.Nodes;
using Stardust.Server.Services;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Areas.Redis.Controllers;

/// <summary>Redis 节点。监控的 Redis 实例</summary>
[Menu(50)]
[RedisArea]
public class RedisNodeController : WebEntityController<RedisNode>
{
    private readonly IRedisService _redisService;

    static RedisNodeController()
    {
        LogOnChange = true;

        ListFields.RemoveField("WebHook", "AlarmMemoryRate", "AlarmConnections", "AlarmSpeed", "AlarmInputKbps", "AlarmOutputKbps");
        ListFields.RemoveField("Password", "UserName");
        ListFields.RemoveCreateField();
        ListFields.RemoveField("UpdateUser", "UpdateUserID", "UpdateIP", "Remark");

        {
            var df = ListFields.GetField("ProjectName") as ListField;
            df.Url = "/Platform/GalaxyProject?projectId={ProjectId}";
            df.Target = "_frame";
        }
        {
            var df = ListFields.AddListField("Monitor", "UpdateTime");
            df.DisplayName = "监控";
            df.Header = "监控";
            df.Url = "/Redis/RedisData?redisId={Id}";
        }
        {
            var df = ListFields.AddListField("Queue", "UpdateTime");
            df.DisplayName = "队列";
            df.Header = "队列";
            df.Url = "/Redis/RedisMessageQueue?redisId={Id}";
        }
        {
            var df = ListFields.AddListField("Refresh", "UpdateTime");
            df.DisplayName = "刷新";
            df.Header = "刷新";
            df.Url = "/Redis/RedisNode/Refresh?Id={Id}";
            df.DataAction = "action";
        }
        {
            var df = ListFields.AddListField("Log", "UpdateTime");
            df.DisplayName = "审计日志";
            df.Header = "审计日志";
            df.Url = $"/Admin/Log?category={WebUtility.UrlEncode("Redis节点")}&linkId={{Id}}";
            df.Target = "_frame";
        }
    }

    public RedisNodeController(IRedisService redisService) => _redisService = redisService;

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<RedisNode> Search(Pager p)
    {
        var nodeId = p["Id"].ToInt(-1);
        if (nodeId > 0)
        {
            var node = RedisNode.FindById(nodeId);
            if (node != null) return new[] { node };
        }

        var category = p["category"];
        var server = p["server"];
        var enable = p["enable"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return RedisNode.Search(server, category, enable, start, end, p["Q"], p);
    }

    /// <summary>搜索</summary>
    /// <param name="provinceId"></param>
    /// <param name="cityId"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    [LovList]
    public ActionResult NodeSearch(String category, String key = null)
    {
        var page = new PageParameter { PageSize = 20 };

        // 默认排序
        if (page.Sort.IsNullOrEmpty()) page.Sort = RedisNode._.Name;

        var list = RedisNode.Search(null, category, true, DateTime.MinValue, DateTime.MinValue, key, page);

        return Json(0, null, list.Select(e => new
        {
            e.Id,
            e.Name,
            e.Server,
            e.Category,
        }).ToArray());
    }

    [EntityAuthorize(PermissionFlags.Update)]
    public ActionResult Refresh(Int32 id)
    {
        var node = RedisNode.FindById(id);
        if (node != null)
        {
            XTrace.WriteLine("Refreshing {0}/{1} {2}", node.Name, node.Id, node.Server);

            try
            {
                _redisService.TraceNode(node);

                var queues = RedisMessageQueue.FindAllByRedisId(node.Id);
                foreach (var item in queues.Where(e => e.Enable))
                {
                    _redisService.TraceQueue(item);
                    item.Update();
                }

                LogProvider.Provider.WriteLog("RedisNode", "Refresh", true, L.TLog("Redis node [{0}] refreshed successfully.", node));
            }
            catch (Exception ex)
            {
                LogProvider.Provider.WriteLog("RedisNode", "Refresh", false, ex?.GetMessage());

                throw;
            }
        }

        return JsonRefresh($"刷新[{node}]成功！");
    }

    /// <summary>行级掩码规则（声明式，替代分散的 CropRow 与 Detail/Insert/Update/Delete override，语义逐键保真）：
    /// 全视图（列表/详情/导出/写响应）Password/UserName/WebHook 值级置 null（键不删，task-95 刀0）；
    /// 新提交值与旧存量值都不得回显。Valid 守卫仍负责 "*****" 回显与恢复（POST 恢复原值防静默清库）；
    /// NodeSearch LovList 匿名投影（Id/Name/Server/Category）不含敏感键。掩码只改写投影行，不触碰共享缓存中的原实体。</summary>
    private static readonly Dictionary<String, Object> _masks = new(StringComparer.OrdinalIgnoreCase) { ["Password"] = null, ["UserName"] = null, ["WebHook"] = null };

    /// <summary>全视图统一掩码（原行为：五路出参全掩）。</summary>
    protected override IDictionary<String, Object> GetRowMasks(ViewKinds view) => _masks;

    /// <summary>写入校验：GET 回显掩码 + POST 守卫（task-95 刀0，AppDeployController.RepoPassword 先例同款）。
    /// 前端编辑抽屉以 Detail 全量展开为 PUT 提交体（F1 形态），Detail/回显均给 "*****"，
    /// 未修改即原样提交 "*****"，此处从库中恢复原值并清脏标记，杜绝静默清库；
    /// 显式清空（空串）放行=允许移除凭据。</summary>
    /// <param name="entity">实体</param>
    /// <param name="type">操作类型</param>
    /// <param name="post">是否提交</param>
    /// <returns>校验结果</returns>
    protected override Boolean Valid(RedisNode entity, DataObjectMethodType type, Boolean post)
    {
        if (!post)
        {
            // GET 查询后：编辑表单回显脱敏
            if (type == DataObjectMethodType.Update)
            {
                if (!entity.Password.IsNullOrEmpty()) entity.Password = "*****";
                if (!entity.WebHook.IsNullOrEmpty()) entity.WebHook = "*****";
            }

            return base.Valid(entity, type, post);
        }

        // POST 保存前：掩码值恢复原值，防 Detail 裁剪/回显掩码经 PUT 全量展开造成静默清库
        foreach (var name in new[] { nameof(RedisNode._.Password), nameof(RedisNode._.WebHook) })
        {
            if (!(entity as IEntity).Dirtys[name]) continue;
            if (entity[name] as String == "*****")
            {
                var old = RedisNode.FindById(entity.Id);
                if (old != null)
                {
                    entity[name] = old[name];
                    (entity as IEntity).Dirtys[name] = false;
                }
            }
            // 空值：保持空值，允许显式清空
        }

        return base.Valid(entity, type, post);
    }

}
