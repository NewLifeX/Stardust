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
using XCode.DataAccessLayer;

namespace Stardust.Web.Areas;

/// <summary>星尘业务控制器基类（可写）。提供 MVC 版兼容的 JsonRefresh 与统一的导航/字段裁剪。</summary>
/// <remarks>
/// 行级掩码机制（2026-11-14 收敛 task-94/95 分散的 CropRow/Detail 掩码 override）：
/// 控制器只需重写 <see cref="GetRowMasks"/> 声明「键 → 掩码值」规则（一处声明，列表/详情/导出/写响应全视图生效），
/// 本基类在唯一投影出口 OnProject 统一应用，语义与原 CropRow 一致：值 null=键在值 null（键不删）；
/// 非空字符串=原值非空时替换为哨兵（条件哨兵，如 WebHook "••••••"）。永不触碰共享缓存中的实体本体。
/// </remarks>
public class WebEntityController<T> : ProjectScopedEntityController<T> where T : Entity<T>, new()
{
    private ViewKinds? _maskView;

    /// <summary>捕获本次投影的视图类型。基类所有响应都按 OnGetOutputFields→OnProject 成对调用
    /// （列表 :74/:78、详情 :107、写响应 :42/:174/:230/:349、导出 :516/:538），OnProject 不携带视图，借此传递。</summary>
    protected override IList<DataField> OnGetOutputFields(ViewKinds kind, T entity = null)
    {
        _maskView = kind;

        var fields = base.OnGetOutputFields(kind, entity);

        // 详情面裁剪凭据类字段（如 Secret），避免密钥随详情回传；列表与表单字段不受影响
        var hidden = GetDetailHiddenFields();
        if (kind == ViewKinds.Detail && hidden != null && hidden.Count > 0)
            return fields.Where(e => !hidden.Any(n => n.EqualIgnoreCase(e.Name))).ToList();

        return fields;
    }

    /// <summary>详情投影需要裁掉的字段名。默认不裁剪，凭据类字段由业务控制器声明</summary>
    protected virtual ICollection<String> GetDetailHiddenFields() => null;

    /// <summary>视图感知的行级掩码规则。键大小写不敏感；值 null=键在值 null（键不删）；
    /// 非空字符串=原值非空时替换为该哨兵。返回 null 表示该视图不掩码。缺省实现=不掩码。</summary>
    protected virtual IDictionary<String, Object> GetRowMasks(ViewKinds view) => null;

    /// <summary>统一投影出口：基类投影后按声明规则应用行级掩码（列表/详情/导出/写响应全视图汇入此处）。</summary>
    protected override ApiRow OnProject(T entity, IList<DataField> fields = null, Boolean camelCase = true)
    {
        var row = base.OnProject(entity, fields, camelCase);
        if (row == null) return null;

        var view = _maskView ?? ViewKinds.List;
        WebEntityCompat.ApplyRowMasks(row, GetRowMasks(view));
        WebEntityCompat.RemoveRowKeys(row, GetRowHiddenFields(view));
        return row;
    }

    /// <summary>投影行需要移除的键。默认不移除；用于还原字段集裁剪契约（如 MapField 回填的外键列）</summary>
    /// <param name="view">视图类型</param>
    /// <returns>键集合，大小写不敏感</returns>
    protected virtual ICollection<String> GetRowHiddenFields(ViewKinds view) => null;

    /// <summary>导出数据源：Csv/Excel 导出直写实体索引器值、不经 OnProject 投影，无法走行掩码；
    /// 此处按列表掩码规则克隆脱敏，保证 Json/Xml/Csv/Excel 四类导出与列表口径一致（Json/Xml 另经 OnProject 掩码，幂等）。</summary>
    protected override IEnumerable<T> ExportData(Int32 max = 0)
    {
        var masks = GetRowMasks(ViewKinds.List);
        if (masks == null || masks.Count == 0) return base.ExportData(max);

        return base.ExportData(max).Select(e => WebEntityCompat.CropEntity(e, masks));
    }

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
    public override async Task<ApiResponse<ApiRow>> Insert(T model)
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

        var rs = new ApiResponse<ApiRow>();
        try
        {
            // 主键语义：身份列必须为空（与 Cube ProcessInsert 对齐；索引器取不到属性值，用属性反射取）
            var uk = Factory.Unique;
            if (uk != null && uk.IsIdentity)
            {
                var pi = typeof(T).GetProperty(uk.Name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                var id = pi != null && pi.CanRead ? (pi.GetValue(entity) + "").Trim() : "";
                if (!id.IsNullOrEmpty() && id != "0") throw new Exception("Adding data must not include a primary key");
            }

            // 项目范围在业务钩子之前校验并补全项目编号
            ValidateProjectScope(entity, DataMethod.Insert);

            // OnInsert 为持久化钩子（McpTokenController 等在此生成业务字段）
            var rows = OnInsert(entity);

            // 二次同步：钩子在属性上生成的字段（如 Token）补进值存储
            SyncValues(entity);

            if (rows <= 0) rows = entity.Insert();
            if (rows <= 0) throw new Exception("No changes were made to the database");

            rs.Code = 0;
            rs.Message = "Add succeeded!";
            rs.Data = OnProject(entity, OnGetOutputFields(ViewKinds.EditForm, entity));
        }
        catch (Exception ex)
        {
            rs.Code = 500;
            rs.Message = "Add failed! " + ex.Message;
            rs.Data = OnProject(entity, OnGetOutputFields(ViewKinds.EditForm, entity));
        }

        return await Task.FromResult(rs);
    }

    /// <summary>更新实体（PUT）。DEF-MW-SRV-01 修复：基类 Update 的提交拷贝只走属性反射通道（CopySubmitted），
    /// 而 XCode 属性 setter 不写 Extends 值存储——全字段提交时持久化读到旧值（改名 PUT code=0 静默空更新）；
    /// 部分提交（UI 启停仅 {id,enable}）时未提交字段的属性通道保持默认空，Valid 读 Name 属性=空
    /// 抛「名称不能为空」（500）。本 override 镜像 Insert 的双通道补值：提交字段=属性+SetItem 双写并显式置脏，
    /// 未提交字段=属性通道对齐 Extends 库值。缺主键/主键不一致/数据不存在三种守卫全部委托基类，
    /// 继承其原始异常语义（基类在拷贝前抛出，无重复落库风险）。</summary>
    public override async Task<ApiResponse<ApiRow>> Update(String id, T model)
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
            var rs = new ApiResponse<ApiRow> { Code = 500, Message = "Save failed! [Sync] " + ex.Message, Data = OnProject(entity, OnGetOutputFields(ViewKinds.EditForm, entity)) };
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
public class WebReadOnlyEntityController<T> : ProjectScopedReadOnlyEntityController<T> where T : Entity<T>, new()
{
    private ViewKinds? _maskView;

    /// <summary>视图捕获，语义同 WebEntityController（只读基类无写响应，视图只会是 List/Detail/Export）。</summary>
    protected override IList<DataField> OnGetOutputFields(ViewKinds kind, T entity = null)
    {
        _maskView = kind;

        var fields = base.OnGetOutputFields(kind, entity);

        // 详情面裁剪凭据类字段，语义同 WebEntityController
        var hidden = GetDetailHiddenFields();
        if (kind == ViewKinds.Detail && hidden != null && hidden.Count > 0)
            return fields.Where(e => !hidden.Any(n => n.EqualIgnoreCase(e.Name))).ToList();

        return fields;
    }

    /// <summary>详情投影需要裁掉的字段名，语义同 WebEntityController</summary>
    protected virtual ICollection<String> GetDetailHiddenFields() => null;

    /// <summary>视图感知的行级掩码规则，语义同 WebEntityController。</summary>
    protected virtual IDictionary<String, Object> GetRowMasks(ViewKinds view) => null;

    /// <summary>统一投影出口：应用行级掩码，语义同 WebEntityController。</summary>
    protected override ApiRow OnProject(T entity, IList<DataField> fields = null, Boolean camelCase = true)
    {
        var row = base.OnProject(entity, fields, camelCase);
        if (row == null) return null;

        var view = _maskView ?? ViewKinds.List;
        WebEntityCompat.ApplyRowMasks(row, GetRowMasks(view));
        WebEntityCompat.RemoveRowKeys(row, GetRowHiddenFields(view));
        return row;
    }

    /// <summary>投影行需要移除的键，语义同 WebEntityController</summary>
    protected virtual ICollection<String> GetRowHiddenFields(ViewKinds view) => null;

    /// <summary>导出数据源：按列表掩码规则克隆脱敏，语义同 WebEntityController（只读基类无写响应）。</summary>
    protected override IEnumerable<T> ExportData(Int32 max = 0)
    {
        var masks = GetRowMasks(ViewKinds.List);
        if (masks == null || masks.Count == 0) return base.ExportData(max);

        return base.ExportData(max).Select(e => WebEntityCompat.CropEntity(e, masks));
    }

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

    /// <summary>把行级掩码规则应用到投影行（task-94/95 各控制器 CropRow/Detail 掩码的统一实现）。
    /// 键大小写不敏感（ApiRow=OrdinalIgnoreCase 字典）。值 null=键在值 null（键不删，原「值级置 null」契约）；
    /// 非空字符串=原值非空时替换为该哨兵（条件哨兵，如 WebHook "••••••"），原值为空则保持不动。
    /// 规则键不在投影行（字段未下发）时跳过，不新增键。</summary>
    public static void ApplyRowMasks(IDictionary<String, Object> row, IDictionary<String, Object> masks)
    {
        if (row == null || masks == null || masks.Count == 0) return;
        foreach (var kv in masks)
        {
            if (kv.Key.IsNullOrEmpty()) continue;
            if (!row.TryGetValue(kv.Key, out var value)) continue;

            if (kv.Value is String sentinel && !sentinel.IsNullOrEmpty())
            {
                if (value != null && !(value + "").IsNullOrEmpty()) row[kv.Key] = sentinel;
            }
            else
            {
                row[kv.Key] = null;
            }
        }
    }

    /// <summary>从投影行移除指定键（大小写不敏感）。用于还原字段集裁剪契约，被裁掉的字段不得因派生逻辑回流。</summary>
    /// <param name="row">投影行</param>
    /// <param name="keys">要移除的键集合</param>
    public static void RemoveRowKeys(IDictionary<String, Object> row, ICollection<String> keys)
    {
        if (row == null || keys == null || keys.Count == 0) return;

        foreach (var key in keys)
        {
            if (key.IsNullOrEmpty()) continue;
            if (row.TryGetValue(key, out _)) row.Remove(key);
        }
    }

    /// <summary>按掩码规则克隆实体（键在值 null / 非空→哨兵），语义同 ApplyRowMasks。
    /// 供 Csv/Excel 导出（直写实体索引器值、不经 OnProject 投影）兜底；克隆为全新实例，不触碰共享缓存中的原实体。</summary>
    public static TEntity CropEntity<TEntity>(TEntity src, IDictionary<String, Object> masks) where TEntity : Entity<TEntity>, new()
    {
        if (src == null || masks == null || masks.Count == 0) return src;

        var dst = new TEntity();
        foreach (var fi in Entity<TEntity>.Meta.Fields)
        {
            dst[fi.Name] = src[fi.Name];
        }
        foreach (var kv in masks)
        {
            var value = dst[kv.Key];
            if (kv.Value is String sentinel && !sentinel.IsNullOrEmpty())
            {
                if (value != null && !(value + "").IsNullOrEmpty()) dst[kv.Key] = sentinel;
            }
            else
            {
                dst[kv.Key] = null;
            }
        }
        return dst;
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
