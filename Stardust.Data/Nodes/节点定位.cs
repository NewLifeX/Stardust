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

/// <summary>节点定位。根据网关IP和MAC规则，自动匹配节点所在地理位置</summary>
[Serializable]
[DataObject]
[Description("节点定位。根据网关IP和MAC规则，自动匹配节点所在地理位置")]
[BindIndex("IX_NodeLocation_AreaId", false, "AreaId")]
[BindIndex("IX_NodeLocation_Name", false, "Name")]
[BindTable("NodeLocation", Description = "节点定位。根据网关IP和MAC规则，自动匹配节点所在地理位置", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class NodeLocation
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
    /// <summary>名称</summary>
    [DisplayName("名称")]
    [Description("名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _LanIPRule;
    /// <summary>局域网IP。局域网IP地址规则，支持*匹配</summary>
    [DisplayName("局域网IP")]
    [Description("局域网IP。局域网IP地址规则，支持*匹配")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("LanIPRule", "局域网IP。局域网IP地址规则，支持*匹配", "")]
    public String LanIPRule { get => _LanIPRule; set { if (OnPropertyChanging("LanIPRule", value)) { _LanIPRule = value; OnPropertyChanged("LanIPRule"); } } }

    private String _MacRule;
    /// <summary>MAC规则。局域网网关MAC地址规则，支持*匹配</summary>
    [DisplayName("MAC规则")]
    [Description("MAC规则。局域网网关MAC地址规则，支持*匹配")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("MacRule", "MAC规则。局域网网关MAC地址规则，支持*匹配", "")]
    public String MacRule { get => _MacRule; set { if (OnPropertyChanging("MacRule", value)) { _MacRule = value; OnPropertyChanged("MacRule"); } } }

    private String _WanIPRule;
    /// <summary>公网IP。公网IP地址规则，支持*匹配</summary>
    [DisplayName("公网IP")]
    [Description("公网IP。公网IP地址规则，支持*匹配")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("WanIPRule", "公网IP。公网IP地址规则，支持*匹配", "")]
    public String WanIPRule { get => _WanIPRule; set { if (OnPropertyChanging("WanIPRule", value)) { _WanIPRule = value; OnPropertyChanged("WanIPRule"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private Int32 _AreaId;
    /// <summary>地区。省市区编码</summary>
    [DisplayName("地区")]
    [Description("地区。省市区编码")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AreaId", "地区。省市区编码", "", ItemType = "area3")]
    public Int32 AreaId { get => _AreaId; set { if (OnPropertyChanging("AreaId", value)) { _AreaId = value; OnPropertyChanged("AreaId"); } } }

    private String _Address;
    /// <summary>地址。地理地址</summary>
    [DisplayName("地址")]
    [Description("地址。地理地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Address", "地址。地理地址", "")]
    public String Address { get => _Address; set { if (OnPropertyChanging("Address", value)) { _Address = value; OnPropertyChanged("Address"); } } }

    private String _Location;
    /// <summary>位置。场地安装位置，或者经纬度</summary>
    [DisplayName("位置")]
    [Description("位置。场地安装位置，或者经纬度")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Location", "位置。场地安装位置，或者经纬度", "")]
    public String Location { get => _Location; set { if (OnPropertyChanging("Location", value)) { _Location = value; OnPropertyChanged("Location"); } } }

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
            "Name" => _Name,
            "LanIPRule" => _LanIPRule,
            "MacRule" => _MacRule,
            "WanIPRule" => _WanIPRule,
            "Enable" => _Enable,
            "AreaId" => _AreaId,
            "Address" => _Address,
            "Location" => _Location,
            "CreateUser" => _CreateUser,
            "CreateUserID" => _CreateUserID,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
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
                case "Name": _Name = Convert.ToString(value); break;
                case "LanIPRule": _LanIPRule = Convert.ToString(value); break;
                case "MacRule": _MacRule = Convert.ToString(value); break;
                case "WanIPRule": _WanIPRule = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "AreaId": _AreaId = value.ToInt(); break;
                case "Address": _Address = Convert.ToString(value); break;
                case "Location": _Location = Convert.ToString(value); break;
                case "CreateUser": _CreateUser = Convert.ToString(value); break;
                case "CreateUserID": _CreateUserID = value.ToInt(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
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
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static NodeLocation FindById(Int32 id)
    {
        if (id < 0) return null;

        return Find(_.Id == id);
    }

    /// <summary>根据地区查找</summary>
    /// <param name="areaId">地区</param>
    /// <returns>实体列表</returns>
    public static IList<NodeLocation> FindAllByAreaId(Int32 areaId)
    {
        if (areaId < 0) return [];

        return FindAll(_.AreaId == areaId);
    }

    /// <summary>根据名称查找</summary>
    /// <param name="name">名称</param>
    /// <returns>实体列表</returns>
    public static IList<NodeLocation> FindAllByName(String name)
    {
        if (name.IsNullOrEmpty()) return [];

        return FindAll(_.Name == name);
    }

    /// <summary>根据更新时间查找</summary>
    /// <param name="updateTime">更新时间</param>
    /// <returns>实体列表</returns>
    public static IList<NodeLocation> FindAllByUpdateTime(DateTime updateTime)
    {
        if (updateTime.Year < 1000) return [];

        return FindAll(_.UpdateTime == updateTime);
    }
    #endregion

    #region 数据清理
    /// <summary>清理指定时间段内的数据</summary>
    /// <param name="start">开始时间。未指定时清理小于指定时间的所有数据</param>
    /// <param name="end">结束时间</param>
    /// <returns>清理行数</returns>
    public static Int32 DeleteWith(DateTime start, DateTime end)
    {
        if (start == end) return Delete(_.UpdateTime == start);

        return Delete(_.UpdateTime.Between(start, end));
    }
    #endregion

    #region 字段名
    /// <summary>取得节点定位字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>局域网IP。局域网IP地址规则，支持*匹配</summary>
        public static readonly Field LanIPRule = FindByName("LanIPRule");

        /// <summary>MAC规则。局域网网关MAC地址规则，支持*匹配</summary>
        public static readonly Field MacRule = FindByName("MacRule");

        /// <summary>公网IP。公网IP地址规则，支持*匹配</summary>
        public static readonly Field WanIPRule = FindByName("WanIPRule");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>地区。省市区编码</summary>
        public static readonly Field AreaId = FindByName("AreaId");

        /// <summary>地址。地理地址</summary>
        public static readonly Field Address = FindByName("Address");

        /// <summary>位置。场地安装位置，或者经纬度</summary>
        public static readonly Field Location = FindByName("Location");

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

        /// <summary>备注</summary>
        public static readonly Field Remark = FindByName("Remark");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得节点定位字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>名称</summary>
        public const String Name = "Name";

        /// <summary>局域网IP。局域网IP地址规则，支持*匹配</summary>
        public const String LanIPRule = "LanIPRule";

        /// <summary>MAC规则。局域网网关MAC地址规则，支持*匹配</summary>
        public const String MacRule = "MacRule";

        /// <summary>公网IP。公网IP地址规则，支持*匹配</summary>
        public const String WanIPRule = "WanIPRule";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>地区。省市区编码</summary>
        public const String AreaId = "AreaId";

        /// <summary>地址。地理地址</summary>
        public const String Address = "Address";

        /// <summary>位置。场地安装位置，或者经纬度</summary>
        public const String Location = "Location";

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

        /// <summary>备注</summary>
        public const String Remark = "Remark";
    }
    #endregion
}
