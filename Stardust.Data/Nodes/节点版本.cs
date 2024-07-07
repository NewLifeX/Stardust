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

/// <summary>节点版本。控制不同类型节点的版本更新，如StarAgent/CrazyCoder等。特殊支持dotNet用于推送安装.Net运行时</summary>
[Serializable]
[DataObject]
[Description("节点版本。控制不同类型节点的版本更新，如StarAgent/CrazyCoder等。特殊支持dotNet用于推送安装.Net运行时")]
[BindIndex("IU_NodeVersion_Version", true, "Version")]
[BindTable("NodeVersion", Description = "节点版本。控制不同类型节点的版本更新，如StarAgent/CrazyCoder等。特殊支持dotNet用于推送安装.Net运行时", ConnName = "Stardust", DbType = DatabaseType.None)]
public partial class NodeVersion
{
    #region 属性
    private Int32 _ID;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("ID", "编号", "")]
    public Int32 ID { get => _ID; set { if (OnPropertyChanging("ID", value)) { _ID = value; OnPropertyChanged("ID"); } } }

    private String _Version;
    /// <summary>版本号</summary>
    [DisplayName("版本号")]
    [Description("版本号")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Version", "版本号", "")]
    public String Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

    private String _ProductCode;
    /// <summary>产品。产品编码，用于区分不同类型节点</summary>
    [DisplayName("产品")]
    [Description("产品。产品编码，用于区分不同类型节点")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("ProductCode", "产品。产品编码，用于区分不同类型节点", "")]
    public String ProductCode { get => _ProductCode; set { if (OnPropertyChanging("ProductCode", value)) { _ProductCode = value; OnPropertyChanged("ProductCode"); } } }

    private Boolean _Enable;
    /// <summary>启用。启用/停用</summary>
    [DisplayName("启用")]
    [Description("启用。启用/停用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用。启用/停用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private Boolean _Force;
    /// <summary>强制。强制升级</summary>
    [DisplayName("强制")]
    [Description("强制。强制升级")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Force", "强制。强制升级", "")]
    public Boolean Force { get => _Force; set { if (OnPropertyChanging("Force", value)) { _Force = value; OnPropertyChanged("Force"); } } }

    private NodeChannels _Channel;
    /// <summary>升级通道</summary>
    [DisplayName("升级通道")]
    [Description("升级通道")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Channel", "升级通道", "")]
    public NodeChannels Channel { get => _Channel; set { if (OnPropertyChanging("Channel", value)) { _Channel = value; OnPropertyChanged("Channel"); } } }

    private String _Strategy;
    /// <summary>策略。升级策略，版本特别支持大于等于和小于等于，node=*abcd*;version>=1.0;runtime/framework/os/oskind/arch/province/city</summary>
    [DisplayName("策略")]
    [Description("策略。升级策略，版本特别支持大于等于和小于等于，node=*abcd*;version>=1.0;runtime/framework/os/oskind/arch/province/city")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Strategy", "策略。升级策略，版本特别支持大于等于和小于等于，node=*abcd*;version>=1.0;runtime/framework/os/oskind/arch/province/city", "")]
    public String Strategy { get => _Strategy; set { if (OnPropertyChanging("Strategy", value)) { _Strategy = value; OnPropertyChanged("Strategy"); } } }

    private String _Source;
    /// <summary>升级源</summary>
    [DisplayName("升级源")]
    [Description("升级源")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Source", "升级源", "", ItemType = "file-zip")]
    public String Source { get => _Source; set { if (OnPropertyChanging("Source", value)) { _Source = value; OnPropertyChanged("Source"); } } }

    private Int64 _Size;
    /// <summary>文件大小</summary>
    [DisplayName("文件大小")]
    [Description("文件大小")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Size", "文件大小", "", ItemType = "GMK")]
    public Int64 Size { get => _Size; set { if (OnPropertyChanging("Size", value)) { _Size = value; OnPropertyChanged("Size"); } } }

    private String _FileHash;
    /// <summary>文件哈希。MD5散列</summary>
    [DisplayName("文件哈希")]
    [Description("文件哈希。MD5散列")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("FileHash", "文件哈希。MD5散列", "")]
    public String FileHash { get => _FileHash; set { if (OnPropertyChanging("FileHash", value)) { _FileHash = value; OnPropertyChanged("FileHash"); } } }

    private String _Preinstall;
    /// <summary>预安装命令。更新前要执行的命令，解压缩后，在解压缩目录执行</summary>
    [DisplayName("预安装命令")]
    [Description("预安装命令。更新前要执行的命令，解压缩后，在解压缩目录执行")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Preinstall", "预安装命令。更新前要执行的命令，解压缩后，在解压缩目录执行", "")]
    public String Preinstall { get => _Preinstall; set { if (OnPropertyChanging("Preinstall", value)) { _Preinstall = value; OnPropertyChanged("Preinstall"); } } }

    private String _Executor;
    /// <summary>执行命令。空格前后为文件名和参数</summary>
    [DisplayName("执行命令")]
    [Description("执行命令。空格前后为文件名和参数")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Executor", "执行命令。空格前后为文件名和参数", "")]
    public String Executor { get => _Executor; set { if (OnPropertyChanging("Executor", value)) { _Executor = value; OnPropertyChanged("Executor"); } } }

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

    private String _Description;
    /// <summary>描述</summary>
    [Category("扩展")]
    [DisplayName("描述")]
    [Description("描述")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Description", "描述", "")]
    public String Description { get => _Description; set { if (OnPropertyChanging("Description", value)) { _Description = value; OnPropertyChanged("Description"); } } }
    #endregion

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object this[String name]
    {
        get => name switch
        {
            "ID" => _ID,
            "Version" => _Version,
            "ProductCode" => _ProductCode,
            "Enable" => _Enable,
            "Force" => _Force,
            "Channel" => _Channel,
            "Strategy" => _Strategy,
            "Source" => _Source,
            "Size" => _Size,
            "FileHash" => _FileHash,
            "Preinstall" => _Preinstall,
            "Executor" => _Executor,
            "CreateUserID" => _CreateUserID,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
            "UpdateUserID" => _UpdateUserID,
            "UpdateTime" => _UpdateTime,
            "UpdateIP" => _UpdateIP,
            "Description" => _Description,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "ID": _ID = value.ToInt(); break;
                case "Version": _Version = Convert.ToString(value); break;
                case "ProductCode": _ProductCode = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "Force": _Force = value.ToBoolean(); break;
                case "Channel": _Channel = (NodeChannels)value.ToInt(); break;
                case "Strategy": _Strategy = Convert.ToString(value); break;
                case "Source": _Source = Convert.ToString(value); break;
                case "Size": _Size = value.ToLong(); break;
                case "FileHash": _FileHash = Convert.ToString(value); break;
                case "Preinstall": _Preinstall = Convert.ToString(value); break;
                case "Executor": _Executor = Convert.ToString(value); break;
                case "CreateUserID": _CreateUserID = value.ToInt(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "UpdateUserID": _UpdateUserID = value.ToInt(); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                case "UpdateIP": _UpdateIP = Convert.ToString(value); break;
                case "Description": _Description = Convert.ToString(value); break;
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
    /// <summary>取得节点版本字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field ID = FindByName("ID");

        /// <summary>版本号</summary>
        public static readonly Field Version = FindByName("Version");

        /// <summary>产品。产品编码，用于区分不同类型节点</summary>
        public static readonly Field ProductCode = FindByName("ProductCode");

        /// <summary>启用。启用/停用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>强制。强制升级</summary>
        public static readonly Field Force = FindByName("Force");

        /// <summary>升级通道</summary>
        public static readonly Field Channel = FindByName("Channel");

        /// <summary>策略。升级策略，版本特别支持大于等于和小于等于，node=*abcd*;version>=1.0;runtime/framework/os/oskind/arch/province/city</summary>
        public static readonly Field Strategy = FindByName("Strategy");

        /// <summary>升级源</summary>
        public static readonly Field Source = FindByName("Source");

        /// <summary>文件大小</summary>
        public static readonly Field Size = FindByName("Size");

        /// <summary>文件哈希。MD5散列</summary>
        public static readonly Field FileHash = FindByName("FileHash");

        /// <summary>预安装命令。更新前要执行的命令，解压缩后，在解压缩目录执行</summary>
        public static readonly Field Preinstall = FindByName("Preinstall");

        /// <summary>执行命令。空格前后为文件名和参数</summary>
        public static readonly Field Executor = FindByName("Executor");

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

        /// <summary>描述</summary>
        public static readonly Field Description = FindByName("Description");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得节点版本字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String ID = "ID";

        /// <summary>版本号</summary>
        public const String Version = "Version";

        /// <summary>产品。产品编码，用于区分不同类型节点</summary>
        public const String ProductCode = "ProductCode";

        /// <summary>启用。启用/停用</summary>
        public const String Enable = "Enable";

        /// <summary>强制。强制升级</summary>
        public const String Force = "Force";

        /// <summary>升级通道</summary>
        public const String Channel = "Channel";

        /// <summary>策略。升级策略，版本特别支持大于等于和小于等于，node=*abcd*;version>=1.0;runtime/framework/os/oskind/arch/province/city</summary>
        public const String Strategy = "Strategy";

        /// <summary>升级源</summary>
        public const String Source = "Source";

        /// <summary>文件大小</summary>
        public const String Size = "Size";

        /// <summary>文件哈希。MD5散列</summary>
        public const String FileHash = "FileHash";

        /// <summary>预安装命令。更新前要执行的命令，解压缩后，在解压缩目录执行</summary>
        public const String Preinstall = "Preinstall";

        /// <summary>执行命令。空格前后为文件名和参数</summary>
        public const String Executor = "Executor";

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

        /// <summary>描述</summary>
        public const String Description = "Description";
    }
    #endregion
}
