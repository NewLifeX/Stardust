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

namespace Stardust.Data.Platform;

/// <summary>MCP审计日志。记录每次MCP工具调用</summary>
[Serializable]
[DataObject]
[Description("MCP审计日志。记录每次MCP工具调用")]
[BindIndex("IX_McpAudit_TokenId_Id", false, "TokenId,Id")]
[BindIndex("IX_McpAudit_ToolName_CreateTime", false, "ToolName,CreateTime")]
[BindIndex("IX_McpAudit_ActionName_CreateTime", false, "ActionName,CreateTime")]
[BindIndex("IX_McpAudit_Success_CreateTime", false, "Success,CreateTime")]
[BindIndex("IX_McpAudit_CreateTime", false, "CreateTime")]
[BindTable("McpAudit", Description = "MCP审计日志。记录每次MCP工具调用", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class McpAudit
{
    #region 属性
    private Int64 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _TokenId;
    /// <summary>令牌</summary>
    [DisplayName("令牌")]
    [Description("令牌")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TokenId", "令牌", "")]
    public Int32 TokenId { get => _TokenId; set { if (OnPropertyChanging("TokenId", value)) { _TokenId = value; OnPropertyChanged("TokenId"); } } }

    private String _TokenName;
    /// <summary>令牌名称。快照，便于Token删除后审计</summary>
    [DisplayName("令牌名称")]
    [Description("令牌名称。快照，便于Token删除后审计")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("TokenName", "令牌名称。快照，便于Token删除后审计", "")]
    public String TokenName { get => _TokenName; set { if (OnPropertyChanging("TokenName", value)) { _TokenName = value; OnPropertyChanged("TokenName"); } } }

    private String _ToolName;
    /// <summary>工具名。list_authorized_resources/search_resources/get_resource/list_actions/invoke_action</summary>
    [DisplayName("工具名")]
    [Description("工具名。list_authorized_resources/search_resources/get_resource/list_actions/invoke_action")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("ToolName", "工具名。list_authorized_resources/search_resources/get_resource/list_actions/invoke_action", "")]
    public String ToolName { get => _ToolName; set { if (OnPropertyChanging("ToolName", value)) { _ToolName = value; OnPropertyChanged("ToolName"); } } }

    private String _ActionName;
    /// <summary>动作名。仅invoke_action时填写，如node_send_command</summary>
    [DisplayName("动作名")]
    [Description("动作名。仅invoke_action时填写，如node_send_command")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("ActionName", "动作名。仅invoke_action时填写，如node_send_command", "")]
    public String ActionName { get => _ActionName; set { if (OnPropertyChanging("ActionName", value)) { _ActionName = value; OnPropertyChanged("ActionName"); } } }

    private String _CallerIp;
    /// <summary>调用方IP</summary>
    [DisplayName("调用方IP")]
    [Description("调用方IP")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CallerIp", "调用方IP", "")]
    public String CallerIp { get => _CallerIp; set { if (OnPropertyChanging("CallerIp", value)) { _CallerIp = value; OnPropertyChanged("CallerIp"); } } }

    private String _CallerUserAgent;
    /// <summary>客户端UA</summary>
    [DisplayName("客户端UA")]
    [Description("客户端UA")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("CallerUserAgent", "客户端UA", "")]
    public String CallerUserAgent { get => _CallerUserAgent; set { if (OnPropertyChanging("CallerUserAgent", value)) { _CallerUserAgent = value; OnPropertyChanged("CallerUserAgent"); } } }

    private String _Arguments;
    /// <summary>入参JSON。截断2000字符，敏感字段脱敏</summary>
    [DisplayName("入参JSON")]
    [Description("入参JSON。截断2000字符，敏感字段脱敏")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("Arguments", "入参JSON。截断2000字符，敏感字段脱敏", "")]
    public String Arguments { get => _Arguments; set { if (OnPropertyChanging("Arguments", value)) { _Arguments = value; OnPropertyChanged("Arguments"); } } }

    private Boolean _Success;
    /// <summary>成功</summary>
    [DisplayName("成功")]
    [Description("成功")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Success", "成功", "")]
    public Boolean Success { get => _Success; set { if (OnPropertyChanging("Success", value)) { _Success = value; OnPropertyChanged("Success"); } } }

    private String _ErrorMessage;
    /// <summary>错误信息</summary>
    [DisplayName("错误信息")]
    [Description("错误信息")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("ErrorMessage", "错误信息", "")]
    public String ErrorMessage { get => _ErrorMessage; set { if (OnPropertyChanging("ErrorMessage", value)) { _ErrorMessage = value; OnPropertyChanged("ErrorMessage"); } } }

    private Int32 _Duration;
    /// <summary>耗时。毫秒</summary>
    [DisplayName("耗时")]
    [Description("耗时。毫秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Duration", "耗时。毫秒", "")]
    public Int32 Duration { get => _Duration; set { if (OnPropertyChanging("Duration", value)) { _Duration = value; OnPropertyChanged("Duration"); } } }

    private String _TraceId;
    /// <summary>链路追踪</summary>
    [Category("扩展")]
    [DisplayName("链路追踪")]
    [Description("链路追踪")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("TraceId", "链路追踪", "")]
    public String TraceId { get => _TraceId; set { if (OnPropertyChanging("TraceId", value)) { _TraceId = value; OnPropertyChanged("TraceId"); } } }

    private DateTime _CreateTime;
    /// <summary>调用时间</summary>
    [Category("扩展")]
    [DisplayName("调用时间")]
    [Description("调用时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "调用时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }
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
            "TokenId" => _TokenId,
            "TokenName" => _TokenName,
            "ToolName" => _ToolName,
            "ActionName" => _ActionName,
            "CallerIp" => _CallerIp,
            "CallerUserAgent" => _CallerUserAgent,
            "Arguments" => _Arguments,
            "Success" => _Success,
            "ErrorMessage" => _ErrorMessage,
            "Duration" => _Duration,
            "TraceId" => _TraceId,
            "CreateTime" => _CreateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "TokenId": _TokenId = value.ToInt(); break;
                case "TokenName": _TokenName = Convert.ToString(value); break;
                case "ToolName": _ToolName = Convert.ToString(value); break;
                case "ActionName": _ActionName = Convert.ToString(value); break;
                case "CallerIp": _CallerIp = Convert.ToString(value); break;
                case "CallerUserAgent": _CallerUserAgent = Convert.ToString(value); break;
                case "Arguments": _Arguments = Convert.ToString(value); break;
                case "Success": _Success = value.ToBoolean(); break;
                case "ErrorMessage": _ErrorMessage = Convert.ToString(value); break;
                case "Duration": _Duration = value.ToInt(); break;
                case "TraceId": _TraceId = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    /// <summary>令牌</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Stardust.Data.Platform.McpToken Token => Extends.Get(nameof(Token), k => Stardust.Data.Platform.McpToken.FindById(TokenId));

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static McpAudit FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据令牌查找</summary>
    /// <param name="tokenId">令牌</param>
    /// <returns>实体列表</returns>
    public static IList<McpAudit> FindAllByTokenId(Int32 tokenId)
    {
        if (tokenId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.TokenId == tokenId);

        return FindAll(_.TokenId == tokenId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="tokenId">令牌</param>
    /// <param name="toolName">工具名。list_authorized_resources/search_resources/get_resource/list_actions/invoke_action</param>
    /// <param name="actionName">动作名。仅invoke_action时填写，如node_send_command</param>
    /// <param name="success">成功</param>
    /// <param name="start">调用时间开始</param>
    /// <param name="end">调用时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<McpAudit> Search(Int32 tokenId, String toolName, String actionName, Boolean? success, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (tokenId >= 0) exp &= _.TokenId == tokenId;
        if (!toolName.IsNullOrEmpty()) exp &= _.ToolName == toolName;
        if (!actionName.IsNullOrEmpty()) exp &= _.ActionName == actionName;
        if (success != null) exp &= _.Success == success;
        exp &= _.CreateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得MCP审计日志字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>令牌</summary>
        public static readonly Field TokenId = FindByName("TokenId");

        /// <summary>令牌名称。快照，便于Token删除后审计</summary>
        public static readonly Field TokenName = FindByName("TokenName");

        /// <summary>工具名。list_authorized_resources/search_resources/get_resource/list_actions/invoke_action</summary>
        public static readonly Field ToolName = FindByName("ToolName");

        /// <summary>动作名。仅invoke_action时填写，如node_send_command</summary>
        public static readonly Field ActionName = FindByName("ActionName");

        /// <summary>调用方IP</summary>
        public static readonly Field CallerIp = FindByName("CallerIp");

        /// <summary>客户端UA</summary>
        public static readonly Field CallerUserAgent = FindByName("CallerUserAgent");

        /// <summary>入参JSON。截断2000字符，敏感字段脱敏</summary>
        public static readonly Field Arguments = FindByName("Arguments");

        /// <summary>成功</summary>
        public static readonly Field Success = FindByName("Success");

        /// <summary>错误信息</summary>
        public static readonly Field ErrorMessage = FindByName("ErrorMessage");

        /// <summary>耗时。毫秒</summary>
        public static readonly Field Duration = FindByName("Duration");

        /// <summary>链路追踪</summary>
        public static readonly Field TraceId = FindByName("TraceId");

        /// <summary>调用时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得MCP审计日志字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>令牌</summary>
        public const String TokenId = "TokenId";

        /// <summary>令牌名称。快照，便于Token删除后审计</summary>
        public const String TokenName = "TokenName";

        /// <summary>工具名。list_authorized_resources/search_resources/get_resource/list_actions/invoke_action</summary>
        public const String ToolName = "ToolName";

        /// <summary>动作名。仅invoke_action时填写，如node_send_command</summary>
        public const String ActionName = "ActionName";

        /// <summary>调用方IP</summary>
        public const String CallerIp = "CallerIp";

        /// <summary>客户端UA</summary>
        public const String CallerUserAgent = "CallerUserAgent";

        /// <summary>入参JSON。截断2000字符，敏感字段脱敏</summary>
        public const String Arguments = "Arguments";

        /// <summary>成功</summary>
        public const String Success = "Success";

        /// <summary>错误信息</summary>
        public const String ErrorMessage = "ErrorMessage";

        /// <summary>耗时。毫秒</summary>
        public const String Duration = "Duration";

        /// <summary>链路追踪</summary>
        public const String TraceId = "TraceId";

        /// <summary>调用时间</summary>
        public const String CreateTime = "CreateTime";
    }
    #endregion
}
