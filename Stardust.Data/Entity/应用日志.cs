using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Stardust.Data;

/// <summary>应用日志</summary>
[Serializable]
[DataObject]
[Description("应用日志")]
[BindIndex("IX_AppClientLog_AppId_ClientId_ThreadId_Id", false, "AppId,ClientId,ThreadId,Id")]
[BindIndex("IX_AppClientLog_AppId_Id", false, "AppId,Id")]
[BindTable("AppClientLog", Description = "应用日志", ConnName = "StardustData", DbType = DatabaseType.None)]
public partial class AppClientLog
{
    #region 属性
    private Int64 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, false, false, 0)]
    [BindColumn("Id", "编号", "", DataScale = "time")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _AppId;
    /// <summary>应用</summary>
    [DisplayName("应用")]
    [Description("应用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AppId", "应用", "", Master = true)]
    public Int32 AppId { get => _AppId; set { if (OnPropertyChanging("AppId", value)) { _AppId = value; OnPropertyChanged("AppId"); } } }

    private String _ClientId;
    /// <summary>客户端</summary>
    [DisplayName("客户端")]
    [Description("客户端")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("ClientId", "客户端", "", Master = true)]
    public String ClientId { get => _ClientId; set { if (OnPropertyChanging("ClientId", value)) { _ClientId = value; OnPropertyChanged("ClientId"); } } }

    private String _Time;
    /// <summary>时间</summary>
    [DisplayName("时间")]
    [Description("时间")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Time", "时间", "")]
    public String Time { get => _Time; set { if (OnPropertyChanging("Time", value)) { _Time = value; OnPropertyChanged("Time"); } } }

    private String _ThreadId;
    /// <summary>线程</summary>
    [DisplayName("线程")]
    [Description("线程")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("ThreadId", "线程", "")]
    public String ThreadId { get => _ThreadId; set { if (OnPropertyChanging("ThreadId", value)) { _ThreadId = value; OnPropertyChanged("ThreadId"); } } }

    private String _Kind;
    /// <summary>类型</summary>
    [DisplayName("类型")]
    [Description("类型")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Kind", "类型", "")]
    public String Kind { get => _Kind; set { if (OnPropertyChanging("Kind", value)) { _Kind = value; OnPropertyChanged("Kind"); } } }

    private String _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称", "")]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Message;
    /// <summary>内容</summary>
    [DisplayName("内容")]
    [Description("内容")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("Message", "内容", "")]
    public String Message { get => _Message; set { if (OnPropertyChanging("Message", value)) { _Message = value; OnPropertyChanged("Message"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [Category("扩展")]
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private String _CreateIP;
    /// <summary>创建地址</summary>
    [Category("扩展")]
    [DisplayName("创建地址")]
    [Description("创建地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateIP", "创建地址", "")]
    public String CreateIP { get => _CreateIP; set { if (OnPropertyChanging("CreateIP", value)) { _CreateIP = value; OnPropertyChanged("CreateIP"); } } }
    #endregion

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "AppId" => _AppId,
            "ClientId" => _ClientId,
            "Time" => _Time,
            "ThreadId" => _ThreadId,
            "Kind" => _Kind,
            "Name" => _Name,
            "Message" => _Message,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "AppId": _AppId = value.ToInt(); break;
                case "ClientId": _ClientId = Convert.ToString(value); break;
                case "Time": _Time = Convert.ToString(value); break;
                case "ThreadId": _ThreadId = Convert.ToString(value); break;
                case "Kind": _Kind = Convert.ToString(value); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Message": _Message = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="threadId">线程</param>
    /// <param name="start">编号开始</param>
    /// <param name="end">编号结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AppClientLog> Search(String threadId, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!threadId.IsNullOrEmpty()) exp &= _.ThreadId == threadId;
        exp &= _.Id.Between(start, end, Meta.Factory.Snow);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 数据清理
    /// <summary>清理指定时间段内的数据</summary>
    /// <param name="start">开始时间。未指定时清理小于指定时间的所有数据</param>
    /// <param name="end">结束时间</param>
    /// <param name="maximumRows">最大删除行数。清理历史数据时，避免一次性删除过多导致数据库IO跟不上，0表示所有</param>
    /// <returns>清理行数</returns>
    public static Int32 DeleteWith(DateTime start, DateTime end, Int32 maximumRows = 0)
    {
        return Delete(_.Id.Between(start, end, Meta.Factory.Snow), maximumRows);
    }
    #endregion

    #region 字段名
    /// <summary>取得应用日志字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>应用</summary>
        public static readonly Field AppId = FindByName("AppId");

        /// <summary>客户端</summary>
        public static readonly Field ClientId = FindByName("ClientId");

        /// <summary>时间</summary>
        public static readonly Field Time = FindByName("Time");

        /// <summary>线程</summary>
        public static readonly Field ThreadId = FindByName("ThreadId");

        /// <summary>类型</summary>
        public static readonly Field Kind = FindByName("Kind");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>内容</summary>
        public static readonly Field Message = FindByName("Message");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得应用日志字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>应用</summary>
        public const String AppId = "AppId";

        /// <summary>客户端</summary>
        public const String ClientId = "ClientId";

        /// <summary>时间</summary>
        public const String Time = "Time";

        /// <summary>线程</summary>
        public const String ThreadId = "ThreadId";

        /// <summary>类型</summary>
        public const String Kind = "Kind";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>内容</summary>
        public const String Message = "Message";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";
    }
    #endregion
}
