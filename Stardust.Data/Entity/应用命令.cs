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

/// <summary>应用命令</summary>
[Serializable]
[DataObject]
[Description("应用命令")]
[BindIndex("IX_AppCommand_AppId_Command", false, "AppId,Command")]
[BindIndex("IX_AppCommand_UpdateTime_AppId_Command", false, "UpdateTime,AppId,Command")]
[BindTable("AppCommand", Description = "应用命令", ConnName = "StardustData", DbType = DatabaseType.None)]
public partial class AppCommand
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _AppId;
    /// <summary>应用</summary>
    [DisplayName("应用")]
    [Description("应用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AppId", "应用", "")]
    public Int32 AppId { get => _AppId; set { if (OnPropertyChanging("AppId", value)) { _AppId = value; OnPropertyChanged("AppId"); } } }

    private String _Command;
    /// <summary>命令</summary>
    [DisplayName("命令")]
    [Description("命令")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Command", "命令", "", Master = true)]
    public String Command { get => _Command; set { if (OnPropertyChanging("Command", value)) { _Command = value; OnPropertyChanged("Command"); } } }

    private String _Argument;
    /// <summary>参数</summary>
    [DisplayName("参数")]
    [Description("参数")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Argument", "参数", "")]
    public String Argument { get => _Argument; set { if (OnPropertyChanging("Argument", value)) { _Argument = value; OnPropertyChanged("Argument"); } } }

    private DateTime _StartTime;
    /// <summary>开始执行时间。用于提前下发指令后延期执行，暂时不支持取消</summary>
    [DisplayName("开始执行时间")]
    [Description("开始执行时间。用于提前下发指令后延期执行，暂时不支持取消")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("StartTime", "开始执行时间。用于提前下发指令后延期执行，暂时不支持取消", "")]
    public DateTime StartTime { get => _StartTime; set { if (OnPropertyChanging("StartTime", value)) { _StartTime = value; OnPropertyChanged("StartTime"); } } }

    private DateTime _Expire;
    /// <summary>过期时间。未指定时表示不限制</summary>
    [DisplayName("过期时间")]
    [Description("过期时间。未指定时表示不限制")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("Expire", "过期时间。未指定时表示不限制", "")]
    public DateTime Expire { get => _Expire; set { if (OnPropertyChanging("Expire", value)) { _Expire = value; OnPropertyChanged("Expire"); } } }

    private NewLife.Remoting.Models.CommandStatus _Status;
    /// <summary>状态。命令状态</summary>
    [DisplayName("状态")]
    [Description("状态。命令状态")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Status", "状态。命令状态", "")]
    public NewLife.Remoting.Models.CommandStatus Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private Int32 _Times;
    /// <summary>次数。一共执行多少次，超过10次后取消</summary>
    [DisplayName("次数")]
    [Description("次数。一共执行多少次，超过10次后取消")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Times", "次数。一共执行多少次，超过10次后取消", "")]
    public Int32 Times { get => _Times; set { if (OnPropertyChanging("Times", value)) { _Times = value; OnPropertyChanged("Times"); } } }

    private String _Result;
    /// <summary>结果</summary>
    [DisplayName("结果")]
    [Description("结果")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Result", "结果", "")]
    public String Result { get => _Result; set { if (OnPropertyChanging("Result", value)) { _Result = value; OnPropertyChanged("Result"); } } }

    private String _TraceId;
    /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
    [Category("扩展")]
    [DisplayName("追踪")]
    [Description("追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("TraceId", "追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递", "")]
    public String TraceId { get => _TraceId; set { if (OnPropertyChanging("TraceId", value)) { _TraceId = value; OnPropertyChanged("TraceId"); } } }

    private String _CreateUser;
    /// <summary>创建者</summary>
    [Category("扩展")]
    [DisplayName("创建者")]
    [Description("创建者")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateUser", "创建者", "")]
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
    [BindColumn("UpdateTime", "更新时间", "", DataScale = "time")]
    public DateTime UpdateTime { get => _UpdateTime; set { if (OnPropertyChanging("UpdateTime", value)) { _UpdateTime = value; OnPropertyChanged("UpdateTime"); } } }

    private String _UpdateIP;
    /// <summary>更新地址</summary>
    [Category("扩展")]
    [DisplayName("更新地址")]
    [Description("更新地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UpdateIP", "更新地址", "")]
    public String UpdateIP { get => _UpdateIP; set { if (OnPropertyChanging("UpdateIP", value)) { _UpdateIP = value; OnPropertyChanged("UpdateIP"); } } }
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
            "Command" => _Command,
            "Argument" => _Argument,
            "StartTime" => _StartTime,
            "Expire" => _Expire,
            "Status" => _Status,
            "Times" => _Times,
            "Result" => _Result,
            "TraceId" => _TraceId,
            "CreateUser" => _CreateUser,
            "CreateUserID" => _CreateUserID,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
            "UpdateUserID" => _UpdateUserID,
            "UpdateTime" => _UpdateTime,
            "UpdateIP" => _UpdateIP,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "AppId": _AppId = value.ToInt(); break;
                case "Command": _Command = Convert.ToString(value); break;
                case "Argument": _Argument = Convert.ToString(value); break;
                case "StartTime": _StartTime = value.ToDateTime(); break;
                case "Expire": _Expire = value.ToDateTime(); break;
                case "Status": _Status = (NewLife.Remoting.Models.CommandStatus)value.ToInt(); break;
                case "Times": _Times = value.ToInt(); break;
                case "Result": _Result = Convert.ToString(value); break;
                case "TraceId": _TraceId = Convert.ToString(value); break;
                case "CreateUser": _CreateUser = Convert.ToString(value); break;
                case "CreateUserID": _CreateUserID = value.ToInt(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "UpdateUserID": _UpdateUserID = value.ToInt(); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                case "UpdateIP": _UpdateIP = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据更新时间、应用、命令查找</summary>
    /// <param name="updateTime">更新时间</param>
    /// <param name="appId">应用</param>
    /// <param name="command">命令</param>
    /// <returns>实体列表</returns>
    public static IList<AppCommand> FindAllByUpdateTimeAndAppIdAndCommand(DateTime updateTime, Int32 appId, String command)
    {
        if (updateTime.Year < 1000) return [];
        if (appId < 0) return [];
        if (command.IsNullOrEmpty()) return [];

        return FindAll(_.UpdateTime == updateTime & _.AppId == appId & _.Command == command);
    }

    /// <summary>根据更新时间查找</summary>
    /// <param name="updateTime">更新时间</param>
    /// <returns>实体列表</returns>
    public static IList<AppCommand> FindAllByUpdateTime(DateTime updateTime)
    {
        if (updateTime.Year < 1000) return [];

        return FindAll(_.UpdateTime == updateTime);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="appId">应用</param>
    /// <param name="status">状态。命令状态</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AppCommand> Search(Int32 appId, NewLife.Remoting.Models.CommandStatus status, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (appId >= 0) exp &= _.AppId == appId;
        if (status >= 0) exp &= _.Status == status;
        exp &= _.UpdateTime.Between(start, end);
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
        if (start == end) return Delete(_.UpdateTime == start);

        return Delete(_.UpdateTime.Between(start, end), maximumRows);
    }
    #endregion

    #region 字段名
    /// <summary>取得应用命令字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>应用</summary>
        public static readonly Field AppId = FindByName("AppId");

        /// <summary>命令</summary>
        public static readonly Field Command = FindByName("Command");

        /// <summary>参数</summary>
        public static readonly Field Argument = FindByName("Argument");

        /// <summary>开始执行时间。用于提前下发指令后延期执行，暂时不支持取消</summary>
        public static readonly Field StartTime = FindByName("StartTime");

        /// <summary>过期时间。未指定时表示不限制</summary>
        public static readonly Field Expire = FindByName("Expire");

        /// <summary>状态。命令状态</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>次数。一共执行多少次，超过10次后取消</summary>
        public static readonly Field Times = FindByName("Times");

        /// <summary>结果</summary>
        public static readonly Field Result = FindByName("Result");

        /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
        public static readonly Field TraceId = FindByName("TraceId");

        /// <summary>创建者</summary>
        public static readonly Field CreateUser = FindByName("CreateUser");

        /// <summary>创建者</summary>
        public static readonly Field CreateUserID = FindByName("CreateUserID");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>更新者</summary>
        public static readonly Field UpdateUserID = FindByName("UpdateUserID");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        /// <summary>更新地址</summary>
        public static readonly Field UpdateIP = FindByName("UpdateIP");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得应用命令字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>应用</summary>
        public const String AppId = "AppId";

        /// <summary>命令</summary>
        public const String Command = "Command";

        /// <summary>参数</summary>
        public const String Argument = "Argument";

        /// <summary>开始执行时间。用于提前下发指令后延期执行，暂时不支持取消</summary>
        public const String StartTime = "StartTime";

        /// <summary>过期时间。未指定时表示不限制</summary>
        public const String Expire = "Expire";

        /// <summary>状态。命令状态</summary>
        public const String Status = "Status";

        /// <summary>次数。一共执行多少次，超过10次后取消</summary>
        public const String Times = "Times";

        /// <summary>结果</summary>
        public const String Result = "Result";

        /// <summary>追踪。最新一次查看采样，可用于关联多个片段，建立依赖关系，随线程上下文、Http、Rpc传递</summary>
        public const String TraceId = "TraceId";

        /// <summary>创建者</summary>
        public const String CreateUser = "CreateUser";

        /// <summary>创建者</summary>
        public const String CreateUserID = "CreateUserID";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>更新者</summary>
        public const String UpdateUserID = "UpdateUserID";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";

        /// <summary>更新地址</summary>
        public const String UpdateIP = "UpdateIP";
    }
    #endregion
}
