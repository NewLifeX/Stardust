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

/// <summary>MCP令牌资源授权。Token与项目/节点/应用的授权关系</summary>
[Serializable]
[DataObject]
[Description("MCP令牌资源授权。Token与项目/节点/应用的授权关系")]
[BindIndex("IU_McpTokenResource_TokenId_ResourceType_ResourceId", true, "TokenId,ResourceType,ResourceId")]
[BindIndex("IX_McpTokenResource_TokenId", false, "TokenId")]
[BindIndex("IX_McpTokenResource_ResourceType_ResourceId", false, "ResourceType,ResourceId")]
[BindTable("McpTokenResource", Description = "MCP令牌资源授权。Token与项目/节点/应用的授权关系", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class McpTokenResource
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _TokenId;
    /// <summary>令牌</summary>
    [DisplayName("令牌")]
    [Description("令牌")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TokenId", "令牌", "")]
    public Int32 TokenId { get => _TokenId; set { if (OnPropertyChanging("TokenId", value)) { _TokenId = value; OnPropertyChanged("TokenId"); } } }

    private String _ResourceType;
    /// <summary>资源类型。Project/Node/App</summary>
    [DisplayName("资源类型")]
    [Description("资源类型。Project/Node/App")]
    [DataObjectField(false, false, false, 20)]
    [BindColumn("ResourceType", "资源类型。Project/Node/App", "")]
    public String ResourceType { get => _ResourceType; set { if (OnPropertyChanging("ResourceType", value)) { _ResourceType = value; OnPropertyChanged("ResourceType"); } } }

    private Int32 _ResourceId;
    /// <summary>资源编号。对应GalaxyProject.Id/Node.ID/App.Id</summary>
    [DisplayName("资源编号")]
    [Description("资源编号。对应GalaxyProject.Id/Node.ID/App.Id")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ResourceId", "资源编号。对应GalaxyProject.Id/Node.ID/App.Id", "")]
    public Int32 ResourceId { get => _ResourceId; set { if (OnPropertyChanging("ResourceId", value)) { _ResourceId = value; OnPropertyChanged("ResourceId"); } } }

    private Boolean _IsAll;
    /// <summary>全部资源。true时忽略ResourceId，授权该类型全部资源</summary>
    [DisplayName("全部资源")]
    [Description("全部资源。true时忽略ResourceId，授权该类型全部资源")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IsAll", "全部资源。true时忽略ResourceId，授权该类型全部资源", "")]
    public Boolean IsAll { get => _IsAll; set { if (OnPropertyChanging("IsAll", value)) { _IsAll = value; OnPropertyChanged("IsAll"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private DateTime _CreateTime;
    /// <summary>授权时间</summary>
    [Category("扩展")]
    [DisplayName("授权时间")]
    [Description("授权时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "授权时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private String _CreateIP;
    /// <summary>授权地址</summary>
    [Category("扩展")]
    [DisplayName("授权地址")]
    [Description("授权地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateIP", "授权地址", "")]
    public String CreateIP { get => _CreateIP; set { if (OnPropertyChanging("CreateIP", value)) { _CreateIP = value; OnPropertyChanged("CreateIP"); } } }

    private String _Remark;
    /// <summary>备注</summary>
    [Category("扩展")]
    [DisplayName("备注")]
    [Description("备注")]
    [DataObjectField(false, false, true, 500)]
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
            "TokenId" => _TokenId,
            "ResourceType" => _ResourceType,
            "ResourceId" => _ResourceId,
            "IsAll" => _IsAll,
            "Enable" => _Enable,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
            "Remark" => _Remark,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "TokenId": _TokenId = value.ToInt(); break;
                case "ResourceType": _ResourceType = Convert.ToString(value); break;
                case "ResourceId": _ResourceId = value.ToInt(); break;
                case "IsAll": _IsAll = value.ToBoolean(); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "Remark": _Remark = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    /// <summary>令牌</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Stardust.Data.Platform.McpToken Token => Extends.Get(nameof(Token), k => Stardust.Data.Platform.McpToken.FindById(TokenId));

    /// <summary>令牌</summary>
    [Map(nameof(TokenId), typeof(Stardust.Data.Platform.McpToken), "Id")]
    public String TokenName => Token?.ToString();

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static McpTokenResource FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据令牌、资源类型、资源编号查找</summary>
    /// <param name="tokenId">令牌</param>
    /// <param name="resourceType">资源类型</param>
    /// <param name="resourceId">资源编号</param>
    /// <returns>实体对象</returns>
    public static McpTokenResource FindByTokenIdAndResourceTypeAndResourceId(Int32 tokenId, String resourceType, Int32 resourceId)
    {
        if (tokenId < 0) return null;
        if (resourceType.IsNullOrEmpty()) return null;
        if (resourceId < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.TokenId == tokenId && e.ResourceType.EqualIgnoreCase(resourceType) && e.ResourceId == resourceId);

        return Find(_.TokenId == tokenId & _.ResourceType == resourceType & _.ResourceId == resourceId);
    }

    /// <summary>根据令牌查找</summary>
    /// <param name="tokenId">令牌</param>
    /// <returns>实体列表</returns>
    public static IList<McpTokenResource> FindAllByTokenId(Int32 tokenId)
    {
        if (tokenId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.TokenId == tokenId);

        return FindAll(_.TokenId == tokenId);
    }

    /// <summary>根据令牌、资源类型查找</summary>
    /// <param name="tokenId">令牌</param>
    /// <param name="resourceType">资源类型</param>
    /// <returns>实体列表</returns>
    public static IList<McpTokenResource> FindAllByTokenIdAndResourceType(Int32 tokenId, String resourceType)
    {
        if (tokenId < 0) return [];
        if (resourceType.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.TokenId == tokenId && e.ResourceType.EqualIgnoreCase(resourceType));

        return FindAll(_.TokenId == tokenId & _.ResourceType == resourceType);
    }

    /// <summary>根据资源类型、资源编号查找</summary>
    /// <param name="resourceType">资源类型</param>
    /// <param name="resourceId">资源编号</param>
    /// <returns>实体列表</returns>
    public static IList<McpTokenResource> FindAllByResourceTypeAndResourceId(String resourceType, Int32 resourceId)
    {
        if (resourceType.IsNullOrEmpty()) return [];
        if (resourceId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ResourceType.EqualIgnoreCase(resourceType) && e.ResourceId == resourceId);

        return FindAll(_.ResourceType == resourceType & _.ResourceId == resourceId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="tokenId">令牌</param>
    /// <param name="resourceType">资源类型。Project/Node/App</param>
    /// <param name="resourceId">资源编号。对应GalaxyProject.Id/Node.ID/App.Id</param>
    /// <param name="isAll">全部资源。true时忽略ResourceId，授权该类型全部资源</param>
    /// <param name="enable">启用</param>
    /// <param name="start">授权时间开始</param>
    /// <param name="end">授权时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<McpTokenResource> Search(Int32 tokenId, String resourceType, Int32 resourceId, Boolean? isAll, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (tokenId >= 0) exp &= _.TokenId == tokenId;
        if (!resourceType.IsNullOrEmpty()) exp &= _.ResourceType == resourceType;
        if (resourceId >= 0) exp &= _.ResourceId == resourceId;
        if (isAll != null) exp &= _.IsAll == isAll;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.CreateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得MCP令牌资源授权字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>令牌</summary>
        public static readonly Field TokenId = FindByName("TokenId");

        /// <summary>资源类型。Project/Node/App</summary>
        public static readonly Field ResourceType = FindByName("ResourceType");

        /// <summary>资源编号。对应GalaxyProject.Id/Node.ID/App.Id</summary>
        public static readonly Field ResourceId = FindByName("ResourceId");

        /// <summary>全部资源。true时忽略ResourceId，授权该类型全部资源</summary>
        public static readonly Field IsAll = FindByName("IsAll");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>授权时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>授权地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>备注</summary>
        public static readonly Field Remark = FindByName("Remark");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得MCP令牌资源授权字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>令牌</summary>
        public const String TokenId = "TokenId";

        /// <summary>资源类型。Project/Node/App</summary>
        public const String ResourceType = "ResourceType";

        /// <summary>资源编号。对应GalaxyProject.Id/Node.ID/App.Id</summary>
        public const String ResourceId = "ResourceId";

        /// <summary>全部资源。true时忽略ResourceId，授权该类型全部资源</summary>
        public const String IsAll = "IsAll";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>授权时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>授权地址</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>备注</summary>
        public const String Remark = "Remark";
    }
    #endregion
}
