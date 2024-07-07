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

namespace Stardust.Data.Nodes;

/// <summary>Redis消息队列。Redis消息队列状态监控</summary>
[Serializable]
[DataObject]
[Description("Redis消息队列。Redis消息队列状态监控")]
[BindIndex("IX_RedisMessageQueue_RedisId", false, "RedisId")]
[BindTable("RedisMessageQueue", Description = "Redis消息队列。Redis消息队列状态监控", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class RedisMessageQueue
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _RedisId;
    /// <summary>Redis节点</summary>
    [DisplayName("Redis节点")]
    [Description("Redis节点")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("RedisId", "Redis节点", "")]
    public Int32 RedisId { get => _RedisId; set { if (OnPropertyChanging("RedisId", value)) { _RedisId = value; OnPropertyChanged("RedisId"); } } }

    private String _Name;
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Category;
    /// <summary>分类</summary>
    [DisplayName("分类")]
    [Description("分类")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Category", "分类", "")]
    public String Category { get => _Category; set { if (OnPropertyChanging("Category", value)) { _Category = value; OnPropertyChanged("Category"); } } }

    private Int32 _Db;
    /// <summary>库</summary>
    [DisplayName("库")]
    [Description("库")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Db", "库", "")]
    public Int32 Db { get => _Db; set { if (OnPropertyChanging("Db", value)) { _Db = value; OnPropertyChanged("Db"); } } }

    private String _Topic;
    /// <summary>主题。消息队列主题</summary>
    [DisplayName("主题")]
    [Description("主题。消息队列主题")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Topic", "主题。消息队列主题", "")]
    public String Topic { get => _Topic; set { if (OnPropertyChanging("Topic", value)) { _Topic = value; OnPropertyChanged("Topic"); } } }

    private String _Type;
    /// <summary>类型。消息队列类型</summary>
    [DisplayName("类型")]
    [Description("类型。消息队列类型")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Type", "类型。消息队列类型", "")]
    public String Type { get => _Type; set { if (OnPropertyChanging("Type", value)) { _Type = value; OnPropertyChanged("Type"); } } }

    private String _Groups;
    /// <summary>消费组。消费组名称</summary>
    [DisplayName("消费组")]
    [Description("消费组。消费组名称")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Groups", "消费组。消费组名称", "")]
    public String Groups { get => _Groups; set { if (OnPropertyChanging("Groups", value)) { _Groups = value; OnPropertyChanged("Groups"); } } }

    private Int32 _Consumers;
    /// <summary>消费者。消费者个数</summary>
    [DisplayName("消费者")]
    [Description("消费者。消费者个数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Consumers", "消费者。消费者个数", "")]
    public Int32 Consumers { get => _Consumers; set { if (OnPropertyChanging("Consumers", value)) { _Consumers = value; OnPropertyChanged("Consumers"); } } }

    private Int64 _Total;
    /// <summary>总消费。现有消费者的消费总数</summary>
    [DisplayName("总消费")]
    [Description("总消费。现有消费者的消费总数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Total", "总消费。现有消费者的消费总数", "")]
    public Int64 Total { get => _Total; set { if (OnPropertyChanging("Total", value)) { _Total = value; OnPropertyChanged("Total"); } } }

    private Int32 _Messages;
    /// <summary>消息数。积压下来，等待消费的消息个数</summary>
    [DisplayName("消息数")]
    [Description("消息数。积压下来，等待消费的消息个数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Messages", "消息数。积压下来，等待消费的消息个数", "")]
    public Int32 Messages { get => _Messages; set { if (OnPropertyChanging("Messages", value)) { _Messages = value; OnPropertyChanged("Messages"); } } }

    private Int32 _MaxMessages;
    /// <summary>最大积压。达到该值时告警，0表示不启用</summary>
    [DisplayName("最大积压")]
    [Description("最大积压。达到该值时告警，0表示不启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("MaxMessages", "最大积压。达到该值时告警，0表示不启用", "")]
    public Int32 MaxMessages { get => _MaxMessages; set { if (OnPropertyChanging("MaxMessages", value)) { _MaxMessages = value; OnPropertyChanged("MaxMessages"); } } }

    private Boolean _Enable;
    /// <summary>启用。停用的节点不再执行监控</summary>
    [DisplayName("启用")]
    [Description("启用。停用的节点不再执行监控")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用。停用的节点不再执行监控", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private String _ConsumerInfo;
    /// <summary>消费者信息</summary>
    [DisplayName("消费者信息")]
    [Description("消费者信息")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("ConsumerInfo", "消费者信息", "")]
    public String ConsumerInfo { get => _ConsumerInfo; set { if (OnPropertyChanging("ConsumerInfo", value)) { _ConsumerInfo = value; OnPropertyChanged("ConsumerInfo"); } } }

    private DateTime _FirstConsumer;
    /// <summary>最早消费</summary>
    [DisplayName("最早消费")]
    [Description("最早消费")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("FirstConsumer", "最早消费", "")]
    public DateTime FirstConsumer { get => _FirstConsumer; set { if (OnPropertyChanging("FirstConsumer", value)) { _FirstConsumer = value; OnPropertyChanged("FirstConsumer"); } } }

    private DateTime _LastActive;
    /// <summary>最后活跃</summary>
    [DisplayName("最后活跃")]
    [Description("最后活跃")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("LastActive", "最后活跃", "")]
    public DateTime LastActive { get => _LastActive; set { if (OnPropertyChanging("LastActive", value)) { _LastActive = value; OnPropertyChanged("LastActive"); } } }

    private String _WebHook;
    /// <summary>告警机器人。钉钉、企业微信等</summary>
    [DisplayName("告警机器人")]
    [Description("告警机器人。钉钉、企业微信等")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("WebHook", "告警机器人。钉钉、企业微信等", "")]
    public String WebHook { get => _WebHook; set { if (OnPropertyChanging("WebHook", value)) { _WebHook = value; OnPropertyChanged("WebHook"); } } }

    private String _TraceId;
    /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
    [Category("扩展")]
    [DisplayName("追踪")]
    [Description("追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("TraceId", "追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递", "")]
    public String TraceId { get => _TraceId; set { if (OnPropertyChanging("TraceId", value)) { _TraceId = value; OnPropertyChanged("TraceId"); } } }

    private String _CreateUser;
    /// <summary>创建人</summary>
    [Category("扩展")]
    [DisplayName("创建人")]
    [Description("创建人")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateUser", "创建人", "")]
    public String CreateUser { get => _CreateUser; set { if (OnPropertyChanging("CreateUser", value)) { _CreateUser = value; OnPropertyChanged("CreateUser"); } } }

    private Int32 _CreateUserID;
    /// <summary>创建者</summary>
    [Category("扩展")]
    [DisplayName("创建者")]
    [Description("创建者")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreateUserID", "创建者", "")]
    public Int32 CreateUserID { get => _CreateUserID; set { if (OnPropertyChanging("CreateUserID", value)) { _CreateUserID = value; OnPropertyChanged("CreateUserID"); } } }

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

    private String _UpdateUser;
    /// <summary>更新人</summary>
    [Category("扩展")]
    [DisplayName("更新人")]
    [Description("更新人")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UpdateUser", "更新人", "")]
    public String UpdateUser { get => _UpdateUser; set { if (OnPropertyChanging("UpdateUser", value)) { _UpdateUser = value; OnPropertyChanged("UpdateUser"); } } }

    private Int32 _UpdateUserID;
    /// <summary>更新者</summary>
    [Category("扩展")]
    [DisplayName("更新者")]
    [Description("更新者")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UpdateUserID", "更新者", "")]
    public Int32 UpdateUserID { get => _UpdateUserID; set { if (OnPropertyChanging("UpdateUserID", value)) { _UpdateUserID = value; OnPropertyChanged("UpdateUserID"); } } }

    private DateTime _UpdateTime;
    /// <summary>更新时间</summary>
    [Category("扩展")]
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("UpdateTime", "更新时间", "")]
    public DateTime UpdateTime { get => _UpdateTime; set { if (OnPropertyChanging("UpdateTime", value)) { _UpdateTime = value; OnPropertyChanged("UpdateTime"); } } }

    private String _UpdateIP;
    /// <summary>更新地址</summary>
    [Category("扩展")]
    [DisplayName("更新地址")]
    [Description("更新地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UpdateIP", "更新地址", "")]
    public String UpdateIP { get => _UpdateIP; set { if (OnPropertyChanging("UpdateIP", value)) { _UpdateIP = value; OnPropertyChanged("UpdateIP"); } } }

    private String _Remark;
    /// <summary>备注</summary>
    [Category("扩展")]
    [DisplayName("备注")]
    [Description("备注")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("Remark", "备注", "")]
    public String Remark { get => _Remark; set { if (OnPropertyChanging("Remark", value)) { _Remark = value; OnPropertyChanged("Remark"); } } }
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
            "RedisId" => _RedisId,
            "Name" => _Name,
            "Category" => _Category,
            "Db" => _Db,
            "Topic" => _Topic,
            "Type" => _Type,
            "Groups" => _Groups,
            "Consumers" => _Consumers,
            "Total" => _Total,
            "Messages" => _Messages,
            "MaxMessages" => _MaxMessages,
            "Enable" => _Enable,
            "ConsumerInfo" => _ConsumerInfo,
            "FirstConsumer" => _FirstConsumer,
            "LastActive" => _LastActive,
            "WebHook" => _WebHook,
            "TraceId" => _TraceId,
            "CreateUser" => _CreateUser,
            "CreateUserID" => _CreateUserID,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
            "UpdateUser" => _UpdateUser,
            "UpdateUserID" => _UpdateUserID,
            "UpdateTime" => _UpdateTime,
            "UpdateIP" => _UpdateIP,
            "Remark" => _Remark,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "RedisId": _RedisId = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Category": _Category = Convert.ToString(value); break;
                case "Db": _Db = value.ToInt(); break;
                case "Topic": _Topic = Convert.ToString(value); break;
                case "Type": _Type = Convert.ToString(value); break;
                case "Groups": _Groups = Convert.ToString(value); break;
                case "Consumers": _Consumers = value.ToInt(); break;
                case "Total": _Total = value.ToLong(); break;
                case "Messages": _Messages = value.ToInt(); break;
                case "MaxMessages": _MaxMessages = value.ToInt(); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "ConsumerInfo": _ConsumerInfo = Convert.ToString(value); break;
                case "FirstConsumer": _FirstConsumer = value.ToDateTime(); break;
                case "LastActive": _LastActive = value.ToDateTime(); break;
                case "WebHook": _WebHook = Convert.ToString(value); break;
                case "TraceId": _TraceId = Convert.ToString(value); break;
                case "CreateUser": _CreateUser = Convert.ToString(value); break;
                case "CreateUserID": _CreateUserID = value.ToInt(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "UpdateUser": _UpdateUser = Convert.ToString(value); break;
                case "UpdateUserID": _UpdateUserID = value.ToInt(); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                case "UpdateIP": _UpdateIP = Convert.ToString(value); break;
                case "Remark": _Remark = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    #endregion

    #region 字段名
    /// <summary>取得Redis消息队列字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>Redis节点</summary>
        public static readonly Field RedisId = FindByName("RedisId");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>分类</summary>
        public static readonly Field Category = FindByName("Category");

        /// <summary>库</summary>
        public static readonly Field Db = FindByName("Db");

        /// <summary>主题。消息队列主题</summary>
        public static readonly Field Topic = FindByName("Topic");

        /// <summary>类型。消息队列类型</summary>
        public static readonly Field Type = FindByName("Type");

        /// <summary>消费组。消费组名称</summary>
        public static readonly Field Groups = FindByName("Groups");

        /// <summary>消费者。消费者个数</summary>
        public static readonly Field Consumers = FindByName("Consumers");

        /// <summary>总消费。现有消费者的消费总数</summary>
        public static readonly Field Total = FindByName("Total");

        /// <summary>消息数。积压下来，等待消费的消息个数</summary>
        public static readonly Field Messages = FindByName("Messages");

        /// <summary>最大积压。达到该值时告警，0表示不启用</summary>
        public static readonly Field MaxMessages = FindByName("MaxMessages");

        /// <summary>启用。停用的节点不再执行监控</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>消费者信息</summary>
        public static readonly Field ConsumerInfo = FindByName("ConsumerInfo");

        /// <summary>最早消费</summary>
        public static readonly Field FirstConsumer = FindByName("FirstConsumer");

        /// <summary>最后活跃</summary>
        public static readonly Field LastActive = FindByName("LastActive");

        /// <summary>告警机器人。钉钉、企业微信等</summary>
        public static readonly Field WebHook = FindByName("WebHook");

        /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
        public static readonly Field TraceId = FindByName("TraceId");

        /// <summary>创建人</summary>
        public static readonly Field CreateUser = FindByName("CreateUser");

        /// <summary>创建者</summary>
        public static readonly Field CreateUserID = FindByName("CreateUserID");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>更新人</summary>
        public static readonly Field UpdateUser = FindByName("UpdateUser");

        /// <summary>更新者</summary>
        public static readonly Field UpdateUserID = FindByName("UpdateUserID");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        /// <summary>更新地址</summary>
        public static readonly Field UpdateIP = FindByName("UpdateIP");

        /// <summary>备注</summary>
        public static readonly Field Remark = FindByName("Remark");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得Redis消息队列字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>Redis节点</summary>
        public const String RedisId = "RedisId";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>分类</summary>
        public const String Category = "Category";

        /// <summary>库</summary>
        public const String Db = "Db";

        /// <summary>主题。消息队列主题</summary>
        public const String Topic = "Topic";

        /// <summary>类型。消息队列类型</summary>
        public const String Type = "Type";

        /// <summary>消费组。消费组名称</summary>
        public const String Groups = "Groups";

        /// <summary>消费者。消费者个数</summary>
        public const String Consumers = "Consumers";

        /// <summary>总消费。现有消费者的消费总数</summary>
        public const String Total = "Total";

        /// <summary>消息数。积压下来，等待消费的消息个数</summary>
        public const String Messages = "Messages";

        /// <summary>最大积压。达到该值时告警，0表示不启用</summary>
        public const String MaxMessages = "MaxMessages";

        /// <summary>启用。停用的节点不再执行监控</summary>
        public const String Enable = "Enable";

        /// <summary>消费者信息</summary>
        public const String ConsumerInfo = "ConsumerInfo";

        /// <summary>最早消费</summary>
        public const String FirstConsumer = "FirstConsumer";

        /// <summary>最后活跃</summary>
        public const String LastActive = "LastActive";

        /// <summary>告警机器人。钉钉、企业微信等</summary>
        public const String WebHook = "WebHook";

        /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
        public const String TraceId = "TraceId";

        /// <summary>创建人</summary>
        public const String CreateUser = "CreateUser";

        /// <summary>创建者</summary>
        public const String CreateUserID = "CreateUserID";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>更新人</summary>
        public const String UpdateUser = "UpdateUser";

        /// <summary>更新者</summary>
        public const String UpdateUserID = "UpdateUserID";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";

        /// <summary>更新地址</summary>
        public const String UpdateIP = "UpdateIP";

        /// <summary>备注</summary>
        public const String Remark = "Remark";
    }
    #endregion
}
