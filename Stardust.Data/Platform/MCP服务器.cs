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

/// <summary>MCP服务器。远端MCP服务端点注册表，供平台展示与选择</summary>
[Serializable]
[DataObject]
[Description("MCP服务器。远端MCP服务端点注册表，供平台展示与选择")]
[BindIndex("IX_McpServer_Name", false, "Name")]
[BindIndex("IX_McpServer_Enable", false, "Enable")]
[BindTable("McpServer", Description = "MCP服务器。远端MCP服务端点注册表，供平台展示与选择", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class McpServer
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Name;
    /// <summary>名称。服务器显示名</summary>
    [DisplayName("名称")]
    [Description("名称。服务器显示名")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Name", "名称。服务器显示名", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Url;
    /// <summary>远端地址。MCP服务端点URL，如 http://node:6680/api/mcp</summary>
    [DisplayName("远端地址")]
    [Description("远端地址。MCP服务端点URL")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Url", "远端地址。MCP服务端点URL", "")]
    public String Url { get => _Url; set { if (OnPropertyChanging("Url", value)) { _Url = value; OnPropertyChanged("Url"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private Int32 _ToolCount;
    /// <summary>工具数。上报的工具数量快照，0表示未知</summary>
    [DisplayName("工具数")]
    [Description("工具数。上报的工具数量快照，0表示未知")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ToolCount", "工具数。上报的工具数量快照，0表示未知", "")]
    public Int32 ToolCount { get => _ToolCount; set { if (OnPropertyChanging("ToolCount", value)) { _ToolCount = value; OnPropertyChanged("ToolCount"); } } }

    private Int32 _ProjectId;
    /// <summary>项目编号。所属项目GalaxyProject.Id，0表示未归属</summary>
    [DisplayName("项目编号")]
    [Description("项目编号。所属项目GalaxyProject.Id，0表示未归属")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProjectId", "项目编号。所属项目GalaxyProject.Id，0表示未归属", "")]
    public Int32 ProjectId { get => _ProjectId; set { if (OnPropertyChanging("ProjectId", value)) { _ProjectId = value; OnPropertyChanged("ProjectId"); } } }

    private String _Remark;
    /// <summary>备注</summary>
    [DisplayName("备注")]
    [Description("备注")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Remark", "备注", "")]
    public String Remark { get => _Remark; set { if (OnPropertyChanging("Remark", value)) { _Remark = value; OnPropertyChanged("Remark"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private DateTime _UpdateTime;
    /// <summary>更新时间</summary>
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UpdateTime", "更新时间", "")]
    public DateTime UpdateTime { get => _UpdateTime; set { if (OnPropertyChanging("UpdateTime", value)) { _UpdateTime = value; OnPropertyChanged("UpdateTime"); } } }

    private String _CreateIP;
    /// <summary>创建地址</summary>
    [DisplayName("创建地址")]
    [Description("创建地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateIP", "创建地址", "")]
    public String CreateIP { get => _CreateIP; set { if (OnPropertyChanging("CreateIP", value)) { _CreateIP = value; OnPropertyChanged("CreateIP"); } } }

    private String _UpdateIP;
    /// <summary>更新地址</summary>
    [DisplayName("更新地址")]
    [Description("更新地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UpdateIP", "更新地址", "")]
    public String UpdateIP { get => _UpdateIP; set { if (OnPropertyChanging("UpdateIP", value)) { _UpdateIP = value; OnPropertyChanged("UpdateIP"); } } }
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static McpServer FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];
    }
    #endregion

    #region 字段名
    /// <summary>取得MCP服务器字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>名称。服务器显示名</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>远端地址。MCP服务端点URL</summary>
        public static readonly Field Url = FindByName("Url");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>工具数。上报的工具数量快照，0表示未知</summary>
        public static readonly Field ToolCount = FindByName("ToolCount");

        /// <summary>项目编号。所属项目GalaxyProject.Id，0表示未归属</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>备注</summary>
        public static readonly Field Remark = FindByName("Remark");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>更新地址</summary>
        public static readonly Field UpdateIP = FindByName("UpdateIP");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }
    #endregion
}
