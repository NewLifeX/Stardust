using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Serialization;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Configs;

/// <summary>配置数据</summary>
public partial class ConfigData : Entity<ConfigData>
{
    #region 对象操作
    static ConfigData()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(ConfigId));

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Modules.Add<UserModule>();
        Meta.Modules.Add<TimeModule>();
        Meta.Modules.Add<IPModule>();
    }

    /// <summary>
    /// 已删除标识
    /// </summary>
    public const String DELETED = "[[Deleted]]";

    /// <summary>
    /// 启用标识
    /// </summary>
    public const String ENABLED = "[[Enabled]]";

    /// <summary>
    /// 禁用标识
    /// </summary>
    public const String DISABLED = "[[Disabled]]";

    /// <summary>验证并修补数据，通过抛出异常的方式提示验证失败。</summary>
    /// <param name="isNew">是否插入</param>
    public override void Valid(Boolean isNew)
    {
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return;

        // 这里验证参数范围，建议抛出参数异常，指定参数名，前端用户界面可以捕获参数异常并聚焦到对应的参数输入框
        if (ConfigId <= 0) throw new ArgumentNullException(nameof(ConfigId), "应用不能为空！");
        if (Key.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Key), "名称不能为空！");
        //if (Scope.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Scope), "作用域不能为空！");

        // 建议先调用基类方法，基类方法会做一些统一处理
        base.Valid(isNew);

        Key = Key?.Trim();
        Value = Value?.Trim();
        Scope = Scope?.Trim();

        //if (Version <= 0) Version = 1;
    }

    /// <summary>初始化数据</summary>
    protected override void InitData()
    {
        if (Meta.Count > 0) return;

        var entity = new ConfigData
        {
            ConfigId = 1,
            Key = "PluginServer",
            Value = NewLife.Setting.Current.PluginServer,

            Enable = true,
            Version = 2,
            NewVersion = 2,
            Remark = "插件服务器。将从该网页上根据关键字分析链接并下载插件",
        };
        entity.Insert();
    }

    /// <summary>添加</summary>
    /// <returns></returns>
    protected override Int32 OnInsert()
    {
        var rs = base.OnInsert();

        ConfigHistory.Add(ConfigId, "Insert", true, this.ToJson());

        return rs;
    }

    /// <summary>更新</summary>
    /// <returns></returns>
    protected override Int32 OnUpdate()
    {
        if (HasDirty) ConfigHistory.Add(ConfigId, "Update", true, Dirtys.ToDictionary(e => e, e => this[e]).ToJson());

        return base.OnUpdate();
    }

    /// <summary>删除</summary>
    /// <returns></returns>
    protected override Int32 OnDelete()
    {
        ConfigHistory.Add(ConfigId, "Delete", true, this.ToJson());

        return base.OnDelete();
    }

    /// <summary>已重载。</summary>
    /// <returns></returns>
    public override String ToString() => Scope.IsNullOrEmpty() ? Key : $"{Key}-{Scope}";
    #endregion

    #region 扩展属性
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static ConfigData FindById(Int32 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>
    /// 根据应用查询所属配置，
    /// </summary>
    /// <param name="configId">=0查询全局</param>
    /// <returns></returns>
    public static IList<ConfigData> FindAllByApp(Int32 configId)
    {
        if (configId <= 0) return new List<ConfigData>();

        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ConfigId == configId);

        return FindAll(_.ConfigId == configId);
    }

    /// <summary>查找应用正在使用的配置，不包括未发布的新增和修改</summary>
    /// <param name="configId"></param>
    /// <param name="version"></param>
    /// <returns></returns>
    public static IList<ConfigData> FindAllLastRelease(Int32 configId, Int32 version)
    {
        var list = FindAllByApp(configId);

        // 先选择版本，再剔除被禁用项
        //list = SelectVersion(list, version);

        return list.Where(e => e.Version > 0 && e.Version <= version && e.Enable).ToList();
    }

    /// <summary>根据应用、名称查找</summary>
    /// <param name="appId">应用</param>
    /// <param name="key">名称</param>
    /// <returns>实体列表</returns>
    public static IList<ConfigData> FindAllByConfigIdAndKey(Int32 appId, String key)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ConfigId == appId && e.Key.EqualIgnoreCase(key));

        return FindAll(_.ConfigId == appId & _.Key == key);
    }

    /// <summary>根据应用、名称、作用域查找</summary>
    /// <param name="appId">应用</param>
    /// <param name="key">名称</param>
    /// <param name="scope">作用域</param>
    /// <returns>实体对象</returns>
    public static ConfigData FindByConfigIdAndKeyAndScope(Int32 appId, String key, String scope)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ConfigId == appId && e.Key.EqualIgnoreCase(key) && e.Scope.EqualIgnoreCase(scope));

        return Find(_.ConfigId == appId & _.Key == key & _.Scope == scope);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="configId">应用</param>
    /// <param name="name">名称</param>
    /// <param name="scope">作用域</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<ConfigData> Search(Int32 configId, String name, String scope, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (configId >= 0) exp &= _.ConfigId == configId;
        if (!name.IsNullOrEmpty()) exp &= _.Key == name;
        if (!scope.IsNullOrEmpty()) exp &= _.Scope == scope;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= _.Key.Contains(key) | _.Value.Contains(key) | _.Scope.Contains(key) | _.CreateIP.Contains(key) | _.UpdateIP.Contains(key) | _.Remark.Contains(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 业务操作
    /// <summary>申请配置，优先本应用，其次共享应用，如有指定作用域则优先作用域</summary>
    /// <param name="app">应用</param>
    /// <param name="key">键</param>
    /// <param name="scope">作用域</param>
    /// <returns></returns>
    public static ConfigData Acquire(AppConfig app, String key, String scope)
    {
        var locals = app.Configs;
        locals = locals.Where(_ => _.Key.EqualIgnoreCase(key)).ToList();
        //locals = SelectVersion(locals, app.Version);

        // 混合应用配置表
        var qs = app.GetQuotes();
        var shares = new List<ConfigData>();
        foreach (var item in qs)
        {
            var list = item.Configs;
            list = list.Where(_ => _.Key.EqualIgnoreCase(key)).ToList();
            //list = SelectVersion(list, item.Version);

            if (list.Count > 0) shares.AddRange(list);
        }

        if (locals.Count == 0 && shares.Count == 0) return null;

        // 如果未指定作用域
        if (scope.IsNullOrEmpty())
        {
            // 优先空作用域
            var cfg = locals.FirstOrDefault(_ => _.Scope.IsNullOrEmpty());
            if (cfg != null) return cfg;

            // 共享应用空作用域
            cfg = shares.FirstOrDefault(_ => _.Scope.IsNullOrEmpty());
            if (cfg != null) return cfg;

            // 任意作用域，最新优先
            if (locals.Count > 0) return locals.OrderByDescending(_ => _.Id).FirstOrDefault();
            if (shares.Count > 0) return shares.OrderByDescending(_ => _.Id).FirstOrDefault();
        }
        else
        {
            // 优先匹配作用域
            var cfg = locals.FirstOrDefault(_ => _.Scope.EqualIgnoreCase(scope));
            if (cfg != null) return cfg;

            // 共享应用该作用域
            cfg = shares.FirstOrDefault(_ => _.Scope.EqualIgnoreCase(scope));
            if (cfg != null) return cfg;

            // 没有找到匹配作用域，使用空作用域
            cfg = locals.FirstOrDefault(_ => _.Scope.IsNullOrEmpty());
            if (cfg != null) return cfg;

            // 共享应用空作用域
            cfg = shares.FirstOrDefault(_ => _.Scope.IsNullOrEmpty());
            if (cfg != null) return cfg;
        }

        // 都没有就返回空，要求去配置
        return null;
    }

    /// <summary>选择指定作用域</summary>
    /// <param name="list"></param>
    /// <param name="scope"></param>
    /// <returns></returns>
    public static IList<ConfigData> SelectScope(IEnumerable<ConfigData> list, String scope)
    {
        var dic = new Dictionary<String, ConfigData>();
        foreach (var item in list)
        {
            // 要么相同作用域，要么选择默认空域
            var key = $"{item.ConfigId}-{item.Key}";
            if (item.Scope.EqualIgnoreCase(scope))
                dic[key] = item;
            else if (item.Scope.IsNullOrEmpty() && !dic.ContainsKey(key))
                dic[key] = item;
        }

        return dic.Values.ToList();
    }

    /// <summary>发布应用下的修改数据</summary>
    /// <param name="list"></param>
    /// <param name="version"></param>
    /// <returns></returns>
    public static Int32 Publish(IEnumerable<ConfigData> list, Int32 version)
    {
        using var tran = Meta.CreateTrans();

        var rs = 0;
        foreach (var item in list)
        {
            // 发布指定版本
            if (item.NewVersion == version)
            {
                // 删除
                if (item.NewStatus.EqualIgnoreCase(DELETED))
                {
                    rs += item.Delete();
                }
                else
                {
                    if (item.NewStatus.EqualIgnoreCase(ENABLED))
                    {
                        item.Enable = true;
                    }
                    else if (item.NewStatus.EqualIgnoreCase(DISABLED))
                    {
                        item.Enable = false;
                    }

                    if (!item.NewValue.IsNullOrEmpty()) item.Value = item.NewValue;
                    item.NewValue = null;
                    item.NewStatus = null;
                    item.Version = version;

                    rs += item.Update();
                }
            }
        }

        tran.Commit();

        return rs;
    }
    #endregion
}