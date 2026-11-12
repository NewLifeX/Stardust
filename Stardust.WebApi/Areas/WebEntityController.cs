using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.ViewModels;
using NewLife.Reflection;
using XCode;

namespace Stardust.Web.Areas;

/// <summary>星尘业务控制器基类（可写）。提供 MVC 版兼容的 JsonRefresh 与统一的导航/字段裁剪。</summary>
public class WebEntityController<T> : EntityController<T> where T : Entity<T>, new()
{
    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        base.OnActionExecuting(filterContext);

        WebEntityCompat.ApplyNav(PageSetting, name => GetRequest(name));
    }

    /// <summary>新增实体。修复 F-PLT-1：API 版 NewLife.Cube 的模型绑定把 JSON 请求值落在实体属性上，
    /// 但 XCode 持久化读取的值存储（Extends）与之脱节（INSERT 渲染 BindColumn 默认值 →「SQL logic error near ','」；
    /// build#7 证据：SetItem 写入的值出现在响应 echo 的扩展字典槽 `Name`，属性槽 `name` 为 null →
    /// 属性=Valid/业务读取通道，Extends 值存储=持久化读取通道）。
    /// 修复=双通道补值：绑定实体本体逐业务字段 SetItem 把属性值同步进值存储（保住属性值供 Valid 与业务读取），
    /// 再 OnInsert（业务钩子）→ 二次同步（钩子生成的字段如 Token）→ Insert（内部 Valid 在 OnInsert 之后，与部署版顺序一致）。</summary>
    public override async Task<ApiResponse<T>> Insert(T model)
    {
        T entity;
        if (model is T src) entity = src;
        else
        {
            entity = new T();
            if (model != null)
            {
                // 裸字典形态（NewLife JsonDictionary 等）：逐键 SetItem（大小写不敏感），主键与审计字段不迁移
                IDictionary<String, Object> dict = null;
                if (model is IDictionary<String, Object> generic) dict = generic;
                else if (model is IDictionary raw)
                {
                    dict = new Dictionary<String, Object>(StringComparer.OrdinalIgnoreCase);
                    foreach (System.Collections.DictionaryEntry kv in raw) dict[kv.Key + ""] = kv.Value;
                }
                if (dict != null)
                    foreach (var kv in dict)
                    {
                        if (kv.Key.IsNullOrEmpty()) continue;
                        if (kv.Key.EqualIgnoreCase("Id", "CreateTime", "UpdateTime", "CreateIP", "UpdateIP")) continue;
                        entity.SetItem(kv.Key, kv.Value);
                    }
            }
        }

        var fields = SyncValues(entity);

        var rs = new ApiResponse<T>();
        try
        {
            // 主键语义：身份列必须为空（与 Cube ProcessInsert 对齐；索引器取不到属性值，用属性反射取）
            var uk = Factory.Unique;
            if (uk != null && uk.IsIdentity)
            {
                var pi = typeof(T).GetProperty(uk.Name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                var id = pi != null && pi.CanRead ? (pi.GetValue(entity) + "").Trim() : "";
                if (!id.IsNullOrEmpty() && id != "0") throw new Exception("添加数据不能带主键");
            }

            // OnInsert 为持久化钩子（McpTokenController 等在此生成业务字段）
            var rows = OnInsert(entity);

            // 二次同步：钩子在属性上生成的字段（如 Token）补进值存储
            SyncValues(entity);

            if (rows <= 0) rows = entity.Insert();
            if (rows <= 0) throw new Exception("数据库无变更");

            rs.Code = 0;
            rs.Message = "添加成功！";
            rs.Data = entity;
        }
        catch (Exception ex)
        {
            rs.Code = 500;
            rs.Message = "添加失败！ " + ex.Message;
            rs.Data = entity;
        }

        return await Task.FromResult(rs);
    }

    /// <summary>更新实体（PUT）。DEF-MW-SRV-01 修复：基类 Update 的提交拷贝只走属性反射通道（CopySubmitted），
    /// 而 XCode 属性 setter 不写 Extends 值存储——全字段提交时持久化读到旧值（改名 PUT code=0 静默空更新）；
    /// 部分提交（UI 启停仅 {id,enable}）时未提交字段的属性通道保持默认空，Valid 读 Name 属性=空
    /// 抛「名称不能为空」（500）。本 override 镜像 Insert 的双通道补值：提交字段=属性+SetItem 双写并显式置脏，
    /// 未提交字段=属性通道对齐 Extends 库值。缺主键/主键不一致/数据不存在三种守卫全部委托基类，
    /// 继承其原始异常语义（基类在拷贝前抛出，无重复落库风险）。</summary>
    public override async Task<ApiResponse<T>> Update(String id, T model)
    {
        if (id.IsNullOrEmpty()) return await base.Update(id, model);

        var uk = Factory.Unique;
        var pi = typeof(T).GetProperty(uk.Name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        var current = ((pi != null && pi.CanRead ? pi.GetValue(model) : null) + "").Trim();
        if (!current.IsNullOrEmpty() && current != "0" && current != id)
            return await base.Update(id, model);

        var key = id.ChangeType(uk.Type);
        var entity = FindData(key);
        if (entity == null || (entity as IEntity).IsNullKey) return await base.Update(id, model);

        // 属性通道播种：loaded 实体属性通道为空（Valid 读属性→toggle 误炸根因），Extends=库值。
        // SetValue 有「等值跳过」语义（基准=Extends 现值，探针 v5 P3/P5 实证），等值播种会被吞——
        // 三步穿透：SetItem(null) 放哨兵 → SetValue(stored) 必写属性通道 → SetItem(stored) 归位 Extends。
        // 播种后 Dirtys.Clear() 还原洁净态，保证 UPDATE SET 只含真提交字段。
        var imeEnt = (IEntity)entity;
        try
        {
            foreach (var fi in Factory.Fields)
            {
                var name = fi.Name;
                var fpi = typeof(T).GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (fpi == null || !fpi.CanWrite) continue;
                var stored = imeEnt[name];
                if (stored == null) continue;
                if (stored.GetType() != fpi.PropertyType) stored = stored.ChangeType(fpi.PropertyType);
                imeEnt.SetItem(name, null);
                fpi.SetValue(entity, stored);
                imeEnt.SetItem(name, stored);
            }
            imeEnt.Dirtys.Clear();
        }
        catch { /* 播种失败不阻断：退回原行为 */ }

        var fields = await GetSubmittedFieldsAsync();
        try
        {
            SyncSubmitted(entity, model, fields);
        }
        catch (Exception ex)
        {
            var rs = new ApiResponse<T> { Code = 500, Message = "保存失败！ [Sync] " + ex.Message, Data = entity };
            return await Task.FromResult(rs);
        }

        return await ProcessUpdate(entity);
    }

    /// <summary>DEF-MW-SRV-01 双通道补值（Insert.SyncValues 的 Update 对偶）。
    /// 提交字段（fields 命中；体不可重读时退回 Dirtys/属性差集兜底）：属性通道（pi.SetValue，供 Valid
    /// 与业务读取）+ 值存储（SetItem，F-PLT-1 实证持久化读 Extends）双写，并显式置脏（Dirtys，
    /// 供 entity.Update 渲染 SET 子句）。未提交字段不触碰：属性通道已由 Update 内播种对齐 Extends 库值，
    /// 且不置脏→不进 SET。主键与审计字段不同步（Insert.SyncValues 同口径）。返回置脏字段数。</summary>
    private static Int32 SyncSubmitted(T entity, T model, ISet<String> fields)
    {
        var n = 0;
        var ukName = Factory.Unique?.Name;
        var type = typeof(T);
        var ime = (IEntity)model;
        var dirtys = ime.Dirtys;
        foreach (var fi in Factory.Fields)
        {
            var name = fi.Name;
            if (name.EqualIgnoreCase(ukName, "CreateTime", "UpdateTime", "CreateIP", "UpdateIP")) continue;

            var pi = type.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            if (pi == null || !pi.CanRead || !pi.CanWrite) continue;

            // 提交值取属性通道（绑定器/C# 初始化器都经属性 setter 写 dataSlot；Dirtys=true 仅表示已赋值，
            // Extends 对 fresh 模型恒空——探针 v1/v3 实证），类型对齐后使用
            var value = pi.GetValue(model);
            if (value != null && value.GetType() != pi.PropertyType) value = value.ChangeType(pi.PropertyType);

            var submitted = fields != null
                ? fields.Contains(name)
                : ime.Dirtys.Contains(name) || !Object.Equals(value, pi.GetValue(entity));

            if (submitted)
            {
                pi.SetValue(entity, value);              // 属性通道：Valid/业务读取
                entity.SetItem(name, value);             // 值存储：持久化读取
                ((IEntity)entity).Dirtys[name] = true;   // 显式置脏：Update 渲染 SET 不依赖 setter 内部行为
                n++;
            }
        }
        return n;
    }

    /// <summary>双通道补值：把属性值经 SetItem 同步进持久化值存储（Extends），返回成功设置的字段数。
    /// 主键与审计字段不同步（审计交拦截器）。</summary>
    private static Int32 SyncValues(T entity)
    {
        var n = 0;
        foreach (var fi in Factory.Fields)
        {
            if (fi.Name.EqualIgnoreCase("Id", "CreateTime", "UpdateTime", "CreateIP", "UpdateIP")) continue;
            var pi = entity.GetType().GetProperty(fi.Name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            if (pi == null || !pi.CanRead) continue;
            if (entity.SetItem(fi.Name, pi.GetValue(entity))) n++;
        }
        return n;
    }

    protected override FieldCollection OnGetFields(ViewKinds kind, Object model)
    {
        var fields = base.OnGetFields(kind, model);

        return WebEntityCompat.TrimFields(fields, kind, name => GetRequest(name));
    }

    /// <summary>Json 刷新响应（消息重载）。</summary>
    protected virtual ActionResult JsonRefresh(String message, Int32 delay = 1) => WebEntityCompat.JsonRefresh(message);

    /// <summary>Json 刷新响应（数据重载）。</summary>
    protected virtual ActionResult JsonRefresh(Object data) => WebEntityCompat.JsonRefresh(data);
}

/// <summary>
/// 星尘只读业务控制器基类。与 Web 版对齐：统计/历史类表（TraceData、NodeStat、AppMinuteStat 等）
/// 在 MVC 版继承 ReadOnlyEntityController，不提供 Add/Edit/Delete 端点，API 版必须保持同样的只读边界。
/// 本基类在只读语义之上补充本地 NewLife.Cube(API版) 缺失的 JsonRefresh 兼容方法。
/// </summary>
public class WebReadOnlyEntityController<T> : ReadOnlyEntityController<T> where T : Entity<T>, new()
{
    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        base.OnActionExecuting(filterContext);

        WebEntityCompat.ApplyNav(PageSetting, name => GetRequest(name));
    }

    protected override FieldCollection OnGetFields(ViewKinds kind, Object model)
    {
        var fields = base.OnGetFields(kind, model);

        return WebEntityCompat.TrimFields(fields, kind, name => GetRequest(name));
    }

    /// <summary>Json 刷新响应（消息重载）。</summary>
    protected virtual ActionResult JsonRefresh(String message, Int32 delay = 1) => WebEntityCompat.JsonRefresh(message);

    /// <summary>Json 刷新响应（数据重载）。</summary>
    protected virtual ActionResult JsonRefresh(Object data) => WebEntityCompat.JsonRefresh(data);
}

/// <summary>可写/只读两个基类共用的兼容逻辑，避免重复实现。</summary>
static class WebEntityCompat
{
    /// <summary>按请求参数设置导航视图，与 MVC 版 OnActionExecuting 保持一致。</summary>
    public static void ApplyNav(PageSetting setting, Func<String, String> getRequest)
    {
        var appId = getRequest("appId").ToInt(-1);
        if (appId <= 0) appId = getRequest("configId").ToInt(-1);
        if (appId <= 0) appId = getRequest("deployId").ToInt(-1);
        if (appId <= 0) appId = getRequest("monitorId").ToInt(-1);
        if (appId > 0)
        {
            setting.NavView = "_App_Nav";
            setting.EnableNavbar = false;
        }

        var projectId = getRequest("projectId").ToInt(-1);
        if (projectId > 0)
        {
            setting.NavView = "_Project_Nav";
            setting.EnableNavbar = false;
        }

        var nodeId = getRequest("nodeId").ToInt(-1);
        if (nodeId > 0)
        {
            setting.NavView = "_Node_Nav";
            setting.EnableNavbar = false;
        }
    }

    /// <summary>列表场景下裁剪冗余外键列，与 MVC 版 OnGetFields 保持一致。</summary>
    public static FieldCollection TrimFields(FieldCollection fields, ViewKinds kind, Func<String, String> getRequest)
    {
        if (kind == ViewKinds.List)
        {
            if (getRequest("appId").ToInt(-1) > 0) fields.RemoveField("ProjectName", "AppName", "Category");

            if (getRequest("projectId").ToInt(-1) > 0) fields.RemoveField("ProjectName");

            if (getRequest("deployId").ToInt(-1) > 0) fields.RemoveField("DeployName");

            if (getRequest("configId").ToInt(-1) > 0) fields.RemoveField("ConfigName");

            if (getRequest("nodeId").ToInt(-1) > 0) fields.RemoveField("NodeName");
        }

        return fields;
    }

    /// <summary>
    /// Json 刷新响应。兼容 MVC 版 NewLife.Cube.Core 的 EntityController.JsonRefresh，
    /// 本地 NewLife.Cube(API版) 未提供该方法，故在星尘基类统一补充。
    /// 纯 API 场景下等价于返回统一响应 {code:0, message} / {code:0, data}。
    /// </summary>
    public static ActionResult JsonRefresh(String message, Int32 delay = 1) =>
        new JsonResult(new ApiResponse<Object> { Code = 0, Message = message });

    /// <summary>Json 刷新响应（数据重载）。</summary>
    public static ActionResult JsonRefresh(Object data) =>
        new JsonResult(new ApiResponse<Object> { Code = 0, Data = data });
}
