using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Serialization;
using NewLife.Web;
using Stardust.Data.Configs;

namespace Stardust.Web.Areas.Configs.Controllers;

/// <summary>配置历史。记录配置的变更历史，支持版本追溯和回滚检查</summary>
[Menu(0, false)]
[ConfigsArea]
public class ConfigHistoryController : ConfigsEntityController<ConfigHistory>
{
    static ConfigHistoryController()
    {
        // 日志列表需要显示详细信息
        ListFields.AddDataField("Remark", null, "TraceId");

        //{
        //    var df = ListFields.GetField("TraceId") as ListField;
        //    df.DisplayName = "跟踪";
        //    df.Url = StarHelper.BuildUrl("{TraceId}");
        //    df.DataVisible = e => e is ConfigHistory entity && !entity.TraceId.IsNullOrEmpty();
        //}

        // 纯 API 后端：MVC 版 ListFields.TraceUrl() 依赖 NewLife.Cube.Web 的 StarUrlExtend，
        // 本地 NewLife.Cube(API版) 未提供该扩展，且 API 响应无需生成链路追踪 Url，故移除。
    }

    /// <summary>高级搜索。按条件分页查询</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<ConfigHistory> Search(Pager p)
    {
        var configId = p["configId"].ToInt(-1);
        var action = p["action"];
        var success = p["success"]?.ToBoolean();

        var start = p["dtStart"].ToDateTime();
        var end = p["dtEnd"].ToDateTime();

        return ConfigHistory.Search(configId, action, success, start, end, p["Q"], p);
    }

    /// <summary>获取配置历史与上一版本的差异。上一版为同配置更早的最近一次发布历史</summary>
    /// <param name="id">历史编号</param>
    /// <returns>返回Json，包含id、configId、newJson（本条Remark）、oldJson（上一版Remark，无则为null）、changedKeys（两JSON顶层键差异数组）</returns>
    public ActionResult Diff(Int32 id)
    {
        var hi = ConfigHistory.FindById(id);
        if (hi == null) return JsonRefresh("历史不存在！");

        // 找同一配置更早的最近一次发布历史，作为上一版本
        // 条件必须下推到数据库，避免 FindAll() 全表加载后在内存中过滤
        var old = ConfigHistory.FindAll(ConfigHistory._.ConfigId == hi.ConfigId & ConfigHistory._.Action == "Publish" & ConfigHistory._.CreateTime < hi.CreateTime)
            .OrderByDescending(e => e.CreateTime)
            .FirstOrDefault();

        var newJson = hi.Remark;
        var oldJson = old?.Remark;

        return Json(0, null, new
        {
            id = hi.Id,
            configId = hi.ConfigId,
            newJson,
            oldJson,
            changedKeys = GetDiffKeys(oldJson, newJson),
        }, 0);
    }

    /// <summary>回滚到历史版本。仅发布历史支持；恢复应用配置版本并重发布配置数据</summary>
    /// <param name="id">历史编号</param>
    public ActionResult Rollback(Int32 id)
    {
        var hi = ConfigHistory.FindById(id);
        if (hi == null) return JsonRefresh("历史不存在！");
        if (!hi.Action.EqualIgnoreCase("Publish")) return JsonRefresh("仅发布历史支持回滚！");

        var cfg = AppConfig.FindById(hi.ConfigId);
        if (cfg == null) return JsonRefresh("应用配置不存在！");

        var snap = hi.Remark.ToJsonEntity<AppConfig>();
        if (snap == null) return JsonRefresh("历史快照解析失败！");

        // 回滚到该次 Publish 引入的版本（NextVersion），无效（<=0）时用快照版本兜底
        cfg.Version = snap.NextVersion > 0 ? snap.NextVersion : snap.Version;
        cfg.NextVersion = snap.NextVersion;
        cfg.Update();

        ConfigHistory.Add(cfg.Id, "Rollback", true, cfg.ToJson());

        var rs = ConfigData.Publish(ConfigData.FindAllByApp(cfg.Id), cfg.Version);

        return JsonRefresh($"已回滚到版本 {cfg.Version}，重发布 {rs} 项");
    }

    /// <summary>比较两个JSON的顶层键差异。包含新增、删除或值变化的键，按键名排序</summary>
    /// <param name="oldJson">上一版JSON</param>
    /// <param name="newJson">当前JSON</param>
    /// <returns>差异键数组</returns>
    static String[] GetDiffKeys(String oldJson, String newJson)
    {
        var oldDic = DecodeJson(oldJson);
        var newDic = DecodeJson(newJson);

        if (oldDic == null || oldDic.Count == 0) return newDic?.Keys.OrderBy(e => e).ToArray() ?? new String[0];
        if (newDic == null || newDic.Count == 0) return oldDic.Keys.OrderBy(e => e).ToArray();

        return oldDic.Keys.Union(newDic.Keys)
            .Where(e => !oldDic.ContainsKey(e) || !newDic.ContainsKey(e) || !String.Equals(oldDic[e]?.ToJson(), newDic[e]?.ToJson()))
            .OrderBy(e => e)
            .ToArray();
    }

    /// <summary>解析JSON为顶层字典</summary>
    /// <param name="json">JSON字符串</param>
    /// <returns>解析失败返回null</returns>
    static IDictionary<String, Object> DecodeJson(String json)
    {
        if (json.IsNullOrEmpty()) return null;

        try
        {
            return new JsonParser(json).Decode() as IDictionary<String, Object>;
        }
        catch
        {
            return null;
        }
    }
}
