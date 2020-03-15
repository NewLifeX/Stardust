using System;
using System.Collections.Generic;
using System.ComponentModel;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Stardust.Data.Nodes
{
    /// <summary>节点</summary>
    [Serializable]
    [DataObject]
    [Description("节点")]
    [BindIndex("IU_Node_Code", true, "Code")]
    [BindIndex("IX_Node_Uuid_MachineGuid_MACs", false, "Uuid,MachineGuid,MACs")]
    [BindIndex("IX_Node_UpdateTime", false, "UpdateTime")]
    [BindTable("Node", Description = "节点", ConnName = "Node", DbType = DatabaseType.None)]
    public partial class Node : INode
    {
        #region 属性
        private Int32 _ID;
        /// <summary>编号</summary>
        [DisplayName("编号")]
        [Description("编号")]
        [DataObjectField(true, true, false, 0)]
        [BindColumn("ID", "编号", "")]
        public Int32 ID { get => _ID; set { if (OnPropertyChanging(__.ID, value)) { _ID = value; OnPropertyChanged(__.ID); } } }

        private String _Name;
        /// <summary>名称</summary>
        [DisplayName("名称")]
        [Description("名称")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Name", "名称", "", Master = true)]
        public String Name { get => _Name; set { if (OnPropertyChanging(__.Name, value)) { _Name = value; OnPropertyChanged(__.Name); } } }

        private String _Code;
        /// <summary>编码。NodeKey</summary>
        [DisplayName("编码")]
        [Description("编码。NodeKey")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Code", "编码。NodeKey", "")]
        public String Code { get => _Code; set { if (OnPropertyChanging(__.Code, value)) { _Code = value; OnPropertyChanged(__.Code); } } }

        private String _Secret;
        /// <summary>密钥。NodeSecret</summary>
        [DisplayName("密钥")]
        [Description("密钥。NodeSecret")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Secret", "密钥。NodeSecret", "")]
        public String Secret { get => _Secret; set { if (OnPropertyChanging(__.Secret, value)) { _Secret = value; OnPropertyChanged(__.Secret); } } }

        private Boolean _Enable;
        /// <summary>启用</summary>
        [DisplayName("启用")]
        [Description("启用")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Enable", "启用", "")]
        public Boolean Enable { get => _Enable; set { if (OnPropertyChanging(__.Enable, value)) { _Enable = value; OnPropertyChanged(__.Enable); } } }

        private String _Version;
        /// <summary>版本</summary>
        [DisplayName("版本")]
        [Description("版本")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Version", "版本", "")]
        public String Version { get => _Version; set { if (OnPropertyChanging(__.Version, value)) { _Version = value; OnPropertyChanged(__.Version); } } }

        private DateTime _CompileTime;
        /// <summary>编译时间</summary>
        [DisplayName("编译时间")]
        [Description("编译时间")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("CompileTime", "编译时间", "")]
        public DateTime CompileTime { get => _CompileTime; set { if (OnPropertyChanging(__.CompileTime, value)) { _CompileTime = value; OnPropertyChanged(__.CompileTime); } } }

        private String _OS;
        /// <summary>操作系统</summary>
        [DisplayName("操作系统")]
        [Description("操作系统")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("OS", "操作系统", "")]
        public String OS { get => _OS; set { if (OnPropertyChanging(__.OS, value)) { _OS = value; OnPropertyChanged(__.OS); } } }

        private String _OSVersion;
        /// <summary>系统版本</summary>
        [DisplayName("系统版本")]
        [Description("系统版本")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("OSVersion", "系统版本", "")]
        public String OSVersion { get => _OSVersion; set { if (OnPropertyChanging(__.OSVersion, value)) { _OSVersion = value; OnPropertyChanged(__.OSVersion); } } }

        private String _MachineName;
        /// <summary>机器名称</summary>
        [DisplayName("机器名称")]
        [Description("机器名称")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("MachineName", "机器名称", "")]
        public String MachineName { get => _MachineName; set { if (OnPropertyChanging(__.MachineName, value)) { _MachineName = value; OnPropertyChanged(__.MachineName); } } }

        private String _UserName;
        /// <summary>用户名称</summary>
        [DisplayName("用户名称")]
        [Description("用户名称")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("UserName", "用户名称", "")]
        public String UserName { get => _UserName; set { if (OnPropertyChanging(__.UserName, value)) { _UserName = value; OnPropertyChanged(__.UserName); } } }

        private Int32 _Cpu;
        /// <summary>CPU</summary>
        [DisplayName("CPU")]
        [Description("CPU")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Cpu", "CPU", "")]
        public Int32 Cpu { get => _Cpu; set { if (OnPropertyChanging(__.Cpu, value)) { _Cpu = value; OnPropertyChanged(__.Cpu); } } }

        private Int32 _Memory;
        /// <summary>内存。单位M</summary>
        [DisplayName("内存")]
        [Description("内存。单位M")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Memory", "内存。单位M", "")]
        public Int32 Memory { get => _Memory; set { if (OnPropertyChanging(__.Memory, value)) { _Memory = value; OnPropertyChanged(__.Memory); } } }

        private String _Processor;
        /// <summary>处理器</summary>
        [DisplayName("处理器")]
        [Description("处理器")]
        [DataObjectField(false, false, true, 200)]
        [BindColumn("Processor", "处理器", "")]
        public String Processor { get => _Processor; set { if (OnPropertyChanging(__.Processor, value)) { _Processor = value; OnPropertyChanged(__.Processor); } } }

        private String _CpuID;
        /// <summary>CPU标识</summary>
        [DisplayName("CPU标识")]
        [Description("CPU标识")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("CpuID", "CPU标识", "")]
        public String CpuID { get => _CpuID; set { if (OnPropertyChanging(__.CpuID, value)) { _CpuID = value; OnPropertyChanged(__.CpuID); } } }

        private String _Uuid;
        /// <summary>唯一标识</summary>
        [DisplayName("唯一标识")]
        [Description("唯一标识")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Uuid", "唯一标识", "")]
        public String Uuid { get => _Uuid; set { if (OnPropertyChanging(__.Uuid, value)) { _Uuid = value; OnPropertyChanged(__.Uuid); } } }

        private String _MachineGuid;
        /// <summary>机器标识</summary>
        [DisplayName("机器标识")]
        [Description("机器标识")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("MachineGuid", "机器标识", "")]
        public String MachineGuid { get => _MachineGuid; set { if (OnPropertyChanging(__.MachineGuid, value)) { _MachineGuid = value; OnPropertyChanged(__.MachineGuid); } } }

        private String _MACs;
        /// <summary>网卡</summary>
        [DisplayName("网卡")]
        [Description("网卡")]
        [DataObjectField(false, false, true, 200)]
        [BindColumn("MACs", "网卡", "")]
        public String MACs { get => _MACs; set { if (OnPropertyChanging(__.MACs, value)) { _MACs = value; OnPropertyChanged(__.MACs); } } }

        private String _COMs;
        /// <summary>串口</summary>
        [DisplayName("串口")]
        [Description("串口")]
        [DataObjectField(false, false, true, 200)]
        [BindColumn("COMs", "串口", "")]
        public String COMs { get => _COMs; set { if (OnPropertyChanging(__.COMs, value)) { _COMs = value; OnPropertyChanged(__.COMs); } } }

        private String _InstallPath;
        /// <summary>安装路径</summary>
        [DisplayName("安装路径")]
        [Description("安装路径")]
        [DataObjectField(false, false, true, 200)]
        [BindColumn("InstallPath", "安装路径", "")]
        public String InstallPath { get => _InstallPath; set { if (OnPropertyChanging(__.InstallPath, value)) { _InstallPath = value; OnPropertyChanged(__.InstallPath); } } }

        private String _Runtime;
        /// <summary>运行时。.Net版本</summary>
        [DisplayName("运行时")]
        [Description("运行时。.Net版本")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Runtime", "运行时。.Net版本", "")]
        public String Runtime { get => _Runtime; set { if (OnPropertyChanging(__.Runtime, value)) { _Runtime = value; OnPropertyChanged(__.Runtime); } } }

        private Int32 _ProvinceID;
        /// <summary>省份</summary>
        [DisplayName("省份")]
        [Description("省份")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("ProvinceID", "省份", "")]
        public Int32 ProvinceID { get => _ProvinceID; set { if (OnPropertyChanging(__.ProvinceID, value)) { _ProvinceID = value; OnPropertyChanged(__.ProvinceID); } } }

        private Int32 _CityID;
        /// <summary>城市</summary>
        [DisplayName("城市")]
        [Description("城市")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("CityID", "城市", "")]
        public Int32 CityID { get => _CityID; set { if (OnPropertyChanging(__.CityID, value)) { _CityID = value; OnPropertyChanged(__.CityID); } } }

        private String _Address;
        /// <summary>地址</summary>
        [DisplayName("地址")]
        [Description("地址")]
        [DataObjectField(false, false, true, 200)]
        [BindColumn("Address", "地址", "")]
        public String Address { get => _Address; set { if (OnPropertyChanging(__.Address, value)) { _Address = value; OnPropertyChanged(__.Address); } } }

        private Int32 _Logins;
        /// <summary>登录次数</summary>
        [DisplayName("登录次数")]
        [Description("登录次数")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Logins", "登录次数", "")]
        public Int32 Logins { get => _Logins; set { if (OnPropertyChanging(__.Logins, value)) { _Logins = value; OnPropertyChanged(__.Logins); } } }

        private DateTime _LastLogin;
        /// <summary>最后登录</summary>
        [DisplayName("最后登录")]
        [Description("最后登录")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("LastLogin", "最后登录", "")]
        public DateTime LastLogin { get => _LastLogin; set { if (OnPropertyChanging(__.LastLogin, value)) { _LastLogin = value; OnPropertyChanged(__.LastLogin); } } }

        private String _LastLoginIP;
        /// <summary>最后IP。最后的公网IP地址</summary>
        [DisplayName("最后IP")]
        [Description("最后IP。最后的公网IP地址")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("LastLoginIP", "最后IP。最后的公网IP地址", "")]
        public String LastLoginIP { get => _LastLoginIP; set { if (OnPropertyChanging(__.LastLoginIP, value)) { _LastLoginIP = value; OnPropertyChanged(__.LastLoginIP); } } }

        private Int32 _OnlineTime;
        /// <summary>在线时长。单位，秒</summary>
        [DisplayName("在线时长")]
        [Description("在线时长。单位，秒")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("OnlineTime", "在线时长。单位，秒", "")]
        public Int32 OnlineTime { get => _OnlineTime; set { if (OnPropertyChanging(__.OnlineTime, value)) { _OnlineTime = value; OnPropertyChanged(__.OnlineTime); } } }

        private Int32 _CreateUserID;
        /// <summary>创建者</summary>
        [DisplayName("创建者")]
        [Description("创建者")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("CreateUserID", "创建者", "")]
        public Int32 CreateUserID { get => _CreateUserID; set { if (OnPropertyChanging(__.CreateUserID, value)) { _CreateUserID = value; OnPropertyChanged(__.CreateUserID); } } }

        private DateTime _CreateTime;
        /// <summary>创建时间</summary>
        [DisplayName("创建时间")]
        [Description("创建时间")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("CreateTime", "创建时间", "")]
        public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging(__.CreateTime, value)) { _CreateTime = value; OnPropertyChanged(__.CreateTime); } } }

        private String _CreateIP;
        /// <summary>创建地址</summary>
        [DisplayName("创建地址")]
        [Description("创建地址")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("CreateIP", "创建地址", "")]
        public String CreateIP { get => _CreateIP; set { if (OnPropertyChanging(__.CreateIP, value)) { _CreateIP = value; OnPropertyChanged(__.CreateIP); } } }

        private Int32 _UpdateUserID;
        /// <summary>更新者</summary>
        [DisplayName("更新者")]
        [Description("更新者")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("UpdateUserID", "更新者", "")]
        public Int32 UpdateUserID { get => _UpdateUserID; set { if (OnPropertyChanging(__.UpdateUserID, value)) { _UpdateUserID = value; OnPropertyChanged(__.UpdateUserID); } } }

        private DateTime _UpdateTime;
        /// <summary>更新时间</summary>
        [DisplayName("更新时间")]
        [Description("更新时间")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("UpdateTime", "更新时间", "")]
        public DateTime UpdateTime { get => _UpdateTime; set { if (OnPropertyChanging(__.UpdateTime, value)) { _UpdateTime = value; OnPropertyChanged(__.UpdateTime); } } }

        private String _UpdateIP;
        /// <summary>更新地址</summary>
        [DisplayName("更新地址")]
        [Description("更新地址")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("UpdateIP", "更新地址", "")]
        public String UpdateIP { get => _UpdateIP; set { if (OnPropertyChanging(__.UpdateIP, value)) { _UpdateIP = value; OnPropertyChanged(__.UpdateIP); } } }

        private String _Remark;
        /// <summary>备注</summary>
        [DisplayName("备注")]
        [Description("备注")]
        [DataObjectField(false, false, true, 500)]
        [BindColumn("Remark", "备注", "")]
        public String Remark { get => _Remark; set { if (OnPropertyChanging(__.Remark, value)) { _Remark = value; OnPropertyChanged(__.Remark); } } }
        #endregion

        #region 获取/设置 字段值
        /// <summary>获取/设置 字段值</summary>
        /// <param name="name">字段名</param>
        /// <returns></returns>
        public override Object this[String name]
        {
            get
            {
                switch (name)
                {
                    case __.ID: return _ID;
                    case __.Name: return _Name;
                    case __.Code: return _Code;
                    case __.Secret: return _Secret;
                    case __.Enable: return _Enable;
                    case __.Version: return _Version;
                    case __.CompileTime: return _CompileTime;
                    case __.OS: return _OS;
                    case __.OSVersion: return _OSVersion;
                    case __.MachineName: return _MachineName;
                    case __.UserName: return _UserName;
                    case __.Cpu: return _Cpu;
                    case __.Memory: return _Memory;
                    case __.Processor: return _Processor;
                    case __.CpuID: return _CpuID;
                    case __.Uuid: return _Uuid;
                    case __.MachineGuid: return _MachineGuid;
                    case __.MACs: return _MACs;
                    case __.COMs: return _COMs;
                    case __.InstallPath: return _InstallPath;
                    case __.Runtime: return _Runtime;
                    case __.ProvinceID: return _ProvinceID;
                    case __.CityID: return _CityID;
                    case __.Address: return _Address;
                    case __.Logins: return _Logins;
                    case __.LastLogin: return _LastLogin;
                    case __.LastLoginIP: return _LastLoginIP;
                    case __.OnlineTime: return _OnlineTime;
                    case __.CreateUserID: return _CreateUserID;
                    case __.CreateTime: return _CreateTime;
                    case __.CreateIP: return _CreateIP;
                    case __.UpdateUserID: return _UpdateUserID;
                    case __.UpdateTime: return _UpdateTime;
                    case __.UpdateIP: return _UpdateIP;
                    case __.Remark: return _Remark;
                    default: return base[name];
                }
            }
            set
            {
                switch (name)
                {
                    case __.ID: _ID = value.ToInt(); break;
                    case __.Name: _Name = Convert.ToString(value); break;
                    case __.Code: _Code = Convert.ToString(value); break;
                    case __.Secret: _Secret = Convert.ToString(value); break;
                    case __.Enable: _Enable = value.ToBoolean(); break;
                    case __.Version: _Version = Convert.ToString(value); break;
                    case __.CompileTime: _CompileTime = value.ToDateTime(); break;
                    case __.OS: _OS = Convert.ToString(value); break;
                    case __.OSVersion: _OSVersion = Convert.ToString(value); break;
                    case __.MachineName: _MachineName = Convert.ToString(value); break;
                    case __.UserName: _UserName = Convert.ToString(value); break;
                    case __.Cpu: _Cpu = value.ToInt(); break;
                    case __.Memory: _Memory = value.ToInt(); break;
                    case __.Processor: _Processor = Convert.ToString(value); break;
                    case __.CpuID: _CpuID = Convert.ToString(value); break;
                    case __.Uuid: _Uuid = Convert.ToString(value); break;
                    case __.MachineGuid: _MachineGuid = Convert.ToString(value); break;
                    case __.MACs: _MACs = Convert.ToString(value); break;
                    case __.COMs: _COMs = Convert.ToString(value); break;
                    case __.InstallPath: _InstallPath = Convert.ToString(value); break;
                    case __.Runtime: _Runtime = Convert.ToString(value); break;
                    case __.ProvinceID: _ProvinceID = value.ToInt(); break;
                    case __.CityID: _CityID = value.ToInt(); break;
                    case __.Address: _Address = Convert.ToString(value); break;
                    case __.Logins: _Logins = value.ToInt(); break;
                    case __.LastLogin: _LastLogin = value.ToDateTime(); break;
                    case __.LastLoginIP: _LastLoginIP = Convert.ToString(value); break;
                    case __.OnlineTime: _OnlineTime = value.ToInt(); break;
                    case __.CreateUserID: _CreateUserID = value.ToInt(); break;
                    case __.CreateTime: _CreateTime = value.ToDateTime(); break;
                    case __.CreateIP: _CreateIP = Convert.ToString(value); break;
                    case __.UpdateUserID: _UpdateUserID = value.ToInt(); break;
                    case __.UpdateTime: _UpdateTime = value.ToDateTime(); break;
                    case __.UpdateIP: _UpdateIP = Convert.ToString(value); break;
                    case __.Remark: _Remark = Convert.ToString(value); break;
                    default: base[name] = value; break;
                }
            }
        }
        #endregion

        #region 字段名
        /// <summary>取得节点字段信息的快捷方式</summary>
        public partial class _
        {
            /// <summary>编号</summary>
            public static readonly Field ID = FindByName(__.ID);

            /// <summary>名称</summary>
            public static readonly Field Name = FindByName(__.Name);

            /// <summary>编码。NodeKey</summary>
            public static readonly Field Code = FindByName(__.Code);

            /// <summary>密钥。NodeSecret</summary>
            public static readonly Field Secret = FindByName(__.Secret);

            /// <summary>启用</summary>
            public static readonly Field Enable = FindByName(__.Enable);

            /// <summary>版本</summary>
            public static readonly Field Version = FindByName(__.Version);

            /// <summary>编译时间</summary>
            public static readonly Field CompileTime = FindByName(__.CompileTime);

            /// <summary>操作系统</summary>
            public static readonly Field OS = FindByName(__.OS);

            /// <summary>系统版本</summary>
            public static readonly Field OSVersion = FindByName(__.OSVersion);

            /// <summary>机器名称</summary>
            public static readonly Field MachineName = FindByName(__.MachineName);

            /// <summary>用户名称</summary>
            public static readonly Field UserName = FindByName(__.UserName);

            /// <summary>CPU</summary>
            public static readonly Field Cpu = FindByName(__.Cpu);

            /// <summary>内存。单位M</summary>
            public static readonly Field Memory = FindByName(__.Memory);

            /// <summary>处理器</summary>
            public static readonly Field Processor = FindByName(__.Processor);

            /// <summary>CPU标识</summary>
            public static readonly Field CpuID = FindByName(__.CpuID);

            /// <summary>唯一标识</summary>
            public static readonly Field Uuid = FindByName(__.Uuid);

            /// <summary>机器标识</summary>
            public static readonly Field MachineGuid = FindByName(__.MachineGuid);

            /// <summary>网卡</summary>
            public static readonly Field MACs = FindByName(__.MACs);

            /// <summary>串口</summary>
            public static readonly Field COMs = FindByName(__.COMs);

            /// <summary>安装路径</summary>
            public static readonly Field InstallPath = FindByName(__.InstallPath);

            /// <summary>运行时。.Net版本</summary>
            public static readonly Field Runtime = FindByName(__.Runtime);

            /// <summary>省份</summary>
            public static readonly Field ProvinceID = FindByName(__.ProvinceID);

            /// <summary>城市</summary>
            public static readonly Field CityID = FindByName(__.CityID);

            /// <summary>地址</summary>
            public static readonly Field Address = FindByName(__.Address);

            /// <summary>登录次数</summary>
            public static readonly Field Logins = FindByName(__.Logins);

            /// <summary>最后登录</summary>
            public static readonly Field LastLogin = FindByName(__.LastLogin);

            /// <summary>最后IP。最后的公网IP地址</summary>
            public static readonly Field LastLoginIP = FindByName(__.LastLoginIP);

            /// <summary>在线时长。单位，秒</summary>
            public static readonly Field OnlineTime = FindByName(__.OnlineTime);

            /// <summary>创建者</summary>
            public static readonly Field CreateUserID = FindByName(__.CreateUserID);

            /// <summary>创建时间</summary>
            public static readonly Field CreateTime = FindByName(__.CreateTime);

            /// <summary>创建地址</summary>
            public static readonly Field CreateIP = FindByName(__.CreateIP);

            /// <summary>更新者</summary>
            public static readonly Field UpdateUserID = FindByName(__.UpdateUserID);

            /// <summary>更新时间</summary>
            public static readonly Field UpdateTime = FindByName(__.UpdateTime);

            /// <summary>更新地址</summary>
            public static readonly Field UpdateIP = FindByName(__.UpdateIP);

            /// <summary>备注</summary>
            public static readonly Field Remark = FindByName(__.Remark);

            static Field FindByName(String name) => Meta.Table.FindByName(name);
        }

        /// <summary>取得节点字段名称的快捷方式</summary>
        public partial class __
        {
            /// <summary>编号</summary>
            public const String ID = "ID";

            /// <summary>名称</summary>
            public const String Name = "Name";

            /// <summary>编码。NodeKey</summary>
            public const String Code = "Code";

            /// <summary>密钥。NodeSecret</summary>
            public const String Secret = "Secret";

            /// <summary>启用</summary>
            public const String Enable = "Enable";

            /// <summary>版本</summary>
            public const String Version = "Version";

            /// <summary>编译时间</summary>
            public const String CompileTime = "CompileTime";

            /// <summary>操作系统</summary>
            public const String OS = "OS";

            /// <summary>系统版本</summary>
            public const String OSVersion = "OSVersion";

            /// <summary>机器名称</summary>
            public const String MachineName = "MachineName";

            /// <summary>用户名称</summary>
            public const String UserName = "UserName";

            /// <summary>CPU</summary>
            public const String Cpu = "Cpu";

            /// <summary>内存。单位M</summary>
            public const String Memory = "Memory";

            /// <summary>处理器</summary>
            public const String Processor = "Processor";

            /// <summary>CPU标识</summary>
            public const String CpuID = "CpuID";

            /// <summary>唯一标识</summary>
            public const String Uuid = "Uuid";

            /// <summary>机器标识</summary>
            public const String MachineGuid = "MachineGuid";

            /// <summary>网卡</summary>
            public const String MACs = "MACs";

            /// <summary>串口</summary>
            public const String COMs = "COMs";

            /// <summary>安装路径</summary>
            public const String InstallPath = "InstallPath";

            /// <summary>运行时。.Net版本</summary>
            public const String Runtime = "Runtime";

            /// <summary>省份</summary>
            public const String ProvinceID = "ProvinceID";

            /// <summary>城市</summary>
            public const String CityID = "CityID";

            /// <summary>地址</summary>
            public const String Address = "Address";

            /// <summary>登录次数</summary>
            public const String Logins = "Logins";

            /// <summary>最后登录</summary>
            public const String LastLogin = "LastLogin";

            /// <summary>最后IP。最后的公网IP地址</summary>
            public const String LastLoginIP = "LastLoginIP";

            /// <summary>在线时长。单位，秒</summary>
            public const String OnlineTime = "OnlineTime";

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

    /// <summary>节点接口</summary>
    public partial interface INode
    {
        #region 属性
        /// <summary>编号</summary>
        Int32 ID { get; set; }

        /// <summary>名称</summary>
        String Name { get; set; }

        /// <summary>编码。NodeKey</summary>
        String Code { get; set; }

        /// <summary>密钥。NodeSecret</summary>
        String Secret { get; set; }

        /// <summary>启用</summary>
        Boolean Enable { get; set; }

        /// <summary>版本</summary>
        String Version { get; set; }

        /// <summary>编译时间</summary>
        DateTime CompileTime { get; set; }

        /// <summary>操作系统</summary>
        String OS { get; set; }

        /// <summary>系统版本</summary>
        String OSVersion { get; set; }

        /// <summary>机器名称</summary>
        String MachineName { get; set; }

        /// <summary>用户名称</summary>
        String UserName { get; set; }

        /// <summary>CPU</summary>
        Int32 Cpu { get; set; }

        /// <summary>内存。单位M</summary>
        Int32 Memory { get; set; }

        /// <summary>处理器</summary>
        String Processor { get; set; }

        /// <summary>CPU标识</summary>
        String CpuID { get; set; }

        /// <summary>唯一标识</summary>
        String Uuid { get; set; }

        /// <summary>机器标识</summary>
        String MachineGuid { get; set; }

        /// <summary>网卡</summary>
        String MACs { get; set; }

        /// <summary>串口</summary>
        String COMs { get; set; }

        /// <summary>安装路径</summary>
        String InstallPath { get; set; }

        /// <summary>运行时。.Net版本</summary>
        String Runtime { get; set; }

        /// <summary>省份</summary>
        Int32 ProvinceID { get; set; }

        /// <summary>城市</summary>
        Int32 CityID { get; set; }

        /// <summary>地址</summary>
        String Address { get; set; }

        /// <summary>登录次数</summary>
        Int32 Logins { get; set; }

        /// <summary>最后登录</summary>
        DateTime LastLogin { get; set; }

        /// <summary>最后IP。最后的公网IP地址</summary>
        String LastLoginIP { get; set; }

        /// <summary>在线时长。单位，秒</summary>
        Int32 OnlineTime { get; set; }

        /// <summary>创建者</summary>
        Int32 CreateUserID { get; set; }

        /// <summary>创建时间</summary>
        DateTime CreateTime { get; set; }

        /// <summary>创建地址</summary>
        String CreateIP { get; set; }

        /// <summary>更新者</summary>
        Int32 UpdateUserID { get; set; }

        /// <summary>更新时间</summary>
        DateTime UpdateTime { get; set; }

        /// <summary>更新地址</summary>
        String UpdateIP { get; set; }

        /// <summary>备注</summary>
        String Remark { get; set; }
        #endregion

        #region 获取/设置 字段值
        /// <summary>获取/设置 字段值</summary>
        /// <param name="name">字段名</param>
        /// <returns></returns>
        Object this[String name] { get; set; }
        #endregion
    }
}