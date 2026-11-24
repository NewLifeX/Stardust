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

namespace Stardust.Data.Deployment;

/// <summary>证书节点。SSL证书与节点服务器的关联，控制证书下发到哪些节点</summary>
[Serializable]
[DataObject]
[Description("证书节点。SSL证书与节点服务器的关联，控制证书下发到哪些节点")]
[BindIndex("IU_SslCertificateNode_CertId_NodeId", true, "CertId,NodeId")]
[BindIndex("IX_SslCertificateNode_NodeId", false, "NodeId")]
[BindTable("SslCertificateNode", Description = "证书节点。SSL证书与节点服务器的关联，控制证书下发到哪些节点", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class SslCertificateNode
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _CertId;
    /// <summary>证书。对应SslCertificate</summary>
    [DisplayName("证书")]
    [Description("证书。对应SslCertificate")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CertId", "证书。对应SslCertificate", "")]
    public Int32 CertId { get => _CertId; set { if (OnPropertyChanging("CertId", value)) { _CertId = value; OnPropertyChanged("CertId"); } } }

    private Int32 _NodeId;
    /// <summary>节点。节点服务器</summary>
    [DisplayName("节点")]
    [Description("节点。节点服务器")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("NodeId", "节点。节点服务器", "")]
    public Int32 NodeId { get => _NodeId; set { if (OnPropertyChanging("NodeId", value)) { _NodeId = value; OnPropertyChanged("NodeId"); } } }

    private String _IP;
    /// <summary>IP地址。节点所在内网IP地址</summary>
    [DisplayName("IP地址")]
    [Description("IP地址。节点所在内网IP地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("IP", "IP地址。节点所在内网IP地址", "")]
    public String IP { get => _IP; set { if (OnPropertyChanging("IP", value)) { _IP = value; OnPropertyChanged("IP"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private String _TargetPath;
    /// <summary>目标目录。证书下发到该节点的目录，空则使用证书配置或默认 ../certs</summary>
    [DisplayName("目标目录")]
    [Description("目标目录。证书下发到该节点的目录，空则使用证书配置或默认 ../certs")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("TargetPath", "目标目录。证书下发到该节点的目录，空则使用证书配置或默认 ../certs", "")]
    public String TargetPath { get => _TargetPath; set { if (OnPropertyChanging("TargetPath", value)) { _TargetPath = value; OnPropertyChanged("TargetPath"); } } }

    private String _Version;
    /// <summary>版本。最近一次下发成功的证书标识</summary>
    [DisplayName("版本")]
    [Description("版本。最近一次下发成功的证书标识")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Version", "版本。最近一次下发成功的证书标识", "")]
    public String Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

    private DateTime _LastPush;
    /// <summary>最后下发。最近一次下发时间</summary>
    [DisplayName("最后下发")]
    [Description("最后下发。最近一次下发时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("LastPush", "最后下发。最近一次下发时间", "")]
    public DateTime LastPush { get => _LastPush; set { if (OnPropertyChanging("LastPush", value)) { _LastPush = value; OnPropertyChanged("LastPush"); } } }

    private String _Status;
    /// <summary>状态。最近一次下发结果</summary>
    [DisplayName("状态")]
    [Description("状态。最近一次下发结果")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Status", "状态。最近一次下发结果", "")]
    public String Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private Int32 _CreateUserId;
    /// <summary>创建者</summary>
    [Category("扩展")]
    [DisplayName("创建者")]
    [Description("创建者")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreateUserId", "创建者", "")]
    public Int32 CreateUserId { get => _CreateUserId; set { if (OnPropertyChanging("CreateUserId", value)) { _CreateUserId = value; OnPropertyChanged("CreateUserId"); } } }

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

    private Int32 _UpdateUserId;
    /// <summary>更新者</summary>
    [Category("扩展")]
    [DisplayName("更新者")]
    [Description("更新者")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UpdateUserId", "更新者", "")]
    public Int32 UpdateUserId { get => _UpdateUserId; set { if (OnPropertyChanging("UpdateUserId", value)) { _UpdateUserId = value; OnPropertyChanged("UpdateUserId"); } } }

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
            "CertId" => _CertId,
            "NodeId" => _NodeId,
            "IP" => _IP,
            "Enable" => _Enable,
            "TargetPath" => _TargetPath,
            "Version" => _Version,
            "LastPush" => _LastPush,
            "Status" => _Status,
            "CreateUserId" => _CreateUserId,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
            "UpdateUserId" => _UpdateUserId,
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
                case "CertId": _CertId = value.ToInt(); break;
                case "NodeId": _NodeId = value.ToInt(); break;
                case "IP": _IP = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "TargetPath": _TargetPath = Convert.ToString(value); break;
                case "Version": _Version = Convert.ToString(value); break;
                case "LastPush": _LastPush = value.ToDateTime(); break;
                case "Status": _Status = Convert.ToString(value); break;
                case "CreateUserId": _CreateUserId = value.ToInt(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "UpdateUserId": _UpdateUserId = value.ToInt(); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                case "UpdateIP": _UpdateIP = Convert.ToString(value); break;
                case "Remark": _Remark = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    /// <summary>证书</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public SslCertificate Cert => Extends.Get(nameof(Cert), k => SslCertificate.FindById(CertId));

    /// <summary>证书</summary>
    [Map(nameof(CertId), typeof(SslCertificate), "Id")]
    public String CertDomain => Cert?.Domain;

    /// <summary>节点</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Stardust.Data.Nodes.Node Node => Extends.Get(nameof(Node), k => Stardust.Data.Nodes.Node.FindByID(NodeId));

    /// <summary>节点</summary>
    [Map(nameof(NodeId), typeof(Stardust.Data.Nodes.Node), "ID")]
    public String NodeName => Node?.Name;

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static SslCertificateNode FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据证书、节点查找</summary>
    /// <param name="certId">证书</param>
    /// <param name="nodeId">节点</param>
    /// <returns>实体对象</returns>
    public static SslCertificateNode FindByCertIdAndNodeId(Int32 certId, Int32 nodeId)
    {
        if (certId < 0) return null;
        if (nodeId < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.CertId == certId && e.NodeId == nodeId);

        return Find(_.CertId == certId & _.NodeId == nodeId);
    }

    /// <summary>根据证书查找</summary>
    /// <param name="certId">证书</param>
    /// <returns>实体列表</returns>
    public static IList<SslCertificateNode> FindAllByCertId(Int32 certId)
    {
        if (certId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.CertId == certId);

        return FindAll(_.CertId == certId);
    }

    /// <summary>根据节点查找</summary>
    /// <param name="nodeId">节点</param>
    /// <returns>实体列表</returns>
    public static IList<SslCertificateNode> FindAllByNodeId(Int32 nodeId)
    {
        if (nodeId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.NodeId == nodeId);

        return FindAll(_.NodeId == nodeId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="certId">证书。对应SslCertificate</param>
    /// <param name="nodeId">节点。节点服务器</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<SslCertificateNode> Search(Int32 certId, Int32 nodeId, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (certId >= 0) exp &= _.CertId == certId;
        if (nodeId >= 0) exp &= _.NodeId == nodeId;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得证书节点字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>证书。对应SslCertificate</summary>
        public static readonly Field CertId = FindByName("CertId");

        /// <summary>节点。节点服务器</summary>
        public static readonly Field NodeId = FindByName("NodeId");

        /// <summary>IP地址。节点所在内网IP地址</summary>
        public static readonly Field IP = FindByName("IP");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>目标目录。证书下发到该节点的目录，空则使用证书配置或默认 ../certs</summary>
        public static readonly Field TargetPath = FindByName("TargetPath");

        /// <summary>版本。最近一次下发成功的证书标识</summary>
        public static readonly Field Version = FindByName("Version");

        /// <summary>最后下发。最近一次下发时间</summary>
        public static readonly Field LastPush = FindByName("LastPush");

        /// <summary>状态。最近一次下发结果</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>创建者</summary>
        public static readonly Field CreateUserId = FindByName("CreateUserId");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>更新者</summary>
        public static readonly Field UpdateUserId = FindByName("UpdateUserId");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        /// <summary>更新地址</summary>
        public static readonly Field UpdateIP = FindByName("UpdateIP");

        /// <summary>备注</summary>
        public static readonly Field Remark = FindByName("Remark");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得证书节点字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>证书。对应SslCertificate</summary>
        public const String CertId = "CertId";

        /// <summary>节点。节点服务器</summary>
        public const String NodeId = "NodeId";

        /// <summary>IP地址。节点所在内网IP地址</summary>
        public const String IP = "IP";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>目标目录。证书下发到该节点的目录，空则使用证书配置或默认 ../certs</summary>
        public const String TargetPath = "TargetPath";

        /// <summary>版本。最近一次下发成功的证书标识</summary>
        public const String Version = "Version";

        /// <summary>最后下发。最近一次下发时间</summary>
        public const String LastPush = "LastPush";

        /// <summary>状态。最近一次下发结果</summary>
        public const String Status = "Status";

        /// <summary>创建者</summary>
        public const String CreateUserId = "CreateUserId";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>更新者</summary>
        public const String UpdateUserId = "UpdateUserId";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";

        /// <summary>更新地址</summary>
        public const String UpdateIP = "UpdateIP";

        /// <summary>备注</summary>
        public const String Remark = "Remark";
    }
    #endregion
}
