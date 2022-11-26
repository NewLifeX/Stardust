using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
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
    [BindIndex("IX_Node_IP", false, "IP")]
    [BindIndex("IX_Node_Category", false, "Category")]
    [BindIndex("IX_Node_ProductCode", false, "ProductCode")]
    [BindIndex("IX_Node_UpdateTime", false, "UpdateTime")]
    [BindTable("Node", Description = "节点", ConnName = "Stardust", DbType = DatabaseType.None)]
    public partial class Node
    {
        #region 属性
        private Int32 _ID;
        /// <summary>编号</summary>
        [DisplayName("编号")]
        [Description("编号")]
        [DataObjectField(true, true, false, 0)]
        [BindColumn("ID", "编号", "")]
        public Int32 ID { get => _ID; set { if (OnPropertyChanging("ID", value)) { _ID = value; OnPropertyChanged("ID"); } } }

        private String _Name;
        /// <summary>名称</summary>
        [DisplayName("名称")]
        [Description("名称")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Name", "名称", "", Master = true)]
        public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

        private String _Code;
        /// <summary>编码。NodeKey</summary>
        [DisplayName("编码")]
        [Description("编码。NodeKey")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Code", "编码。NodeKey", "")]
        public String Code { get => _Code; set { if (OnPropertyChanging("Code", value)) { _Code = value; OnPropertyChanged("Code"); } } }

        private String _Secret;
        /// <summary>密钥。NodeSecret</summary>
        [DisplayName("密钥")]
        [Description("密钥。NodeSecret")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Secret", "密钥。NodeSecret", "")]
        public String Secret { get => _Secret; set { if (OnPropertyChanging("Secret", value)) { _Secret = value; OnPropertyChanged("Secret"); } } }

        private Boolean _Enable;
        /// <summary>启用</summary>
        [DisplayName("启用")]
        [Description("启用")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Enable", "启用", "")]
        public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

        private String _ProductCode;
        /// <summary>产品。产品编码，用于区分不同类型节点</summary>
        [DisplayName("产品")]
        [Description("产品。产品编码，用于区分不同类型节点")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("ProductCode", "产品。产品编码，用于区分不同类型节点", "")]
        public String ProductCode { get => _ProductCode; set { if (OnPropertyChanging("ProductCode", value)) { _ProductCode = value; OnPropertyChanged("ProductCode"); } } }

        private String _Category;
        /// <summary>分类</summary>
        [DisplayName("分类")]
        [Description("分类")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Category", "分类", "")]
        public String Category { get => _Category; set { if (OnPropertyChanging("Category", value)) { _Category = value; OnPropertyChanged("Category"); } } }

        private String _Version;
        /// <summary>版本</summary>
        [DisplayName("版本")]
        [Description("版本")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Version", "版本", "")]
        public String Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

        private DateTime _CompileTime;
        /// <summary>编译时间</summary>
        [DisplayName("编译时间")]
        [Description("编译时间")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("CompileTime", "编译时间", "")]
        public DateTime CompileTime { get => _CompileTime; set { if (OnPropertyChanging("CompileTime", value)) { _CompileTime = value; OnPropertyChanged("CompileTime"); } } }

        private String _OS;
        /// <summary>操作系统</summary>
        [Category("硬件信息")]
        [DisplayName("操作系统")]
        [Description("操作系统")]
        [DataObjectField(false, false, true, 100)]
        [BindColumn("OS", "操作系统", "")]
        public String OS { get => _OS; set { if (OnPropertyChanging("OS", value)) { _OS = value; OnPropertyChanged("OS"); } } }

        private String _OSVersion;
        /// <summary>系统版本</summary>
        [Category("硬件信息")]
        [DisplayName("系统版本")]
        [Description("系统版本")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("OSVersion", "系统版本", "")]
        public String OSVersion { get => _OSVersion; set { if (OnPropertyChanging("OSVersion", value)) { _OSVersion = value; OnPropertyChanged("OSVersion"); } } }

        private String _Architecture;
        /// <summary>架构。处理器架构，X86/X64/Arm/Arm64</summary>
        [Category("硬件信息")]
        [DisplayName("架构")]
        [Description("架构。处理器架构，X86/X64/Arm/Arm64")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Architecture", "架构。处理器架构，X86/X64/Arm/Arm64", "")]
        public String Architecture { get => _Architecture; set { if (OnPropertyChanging("Architecture", value)) { _Architecture = value; OnPropertyChanged("Architecture"); } } }

        private String _MachineName;
        /// <summary>机器名称</summary>
        [Category("硬件信息")]
        [DisplayName("机器名称")]
        [Description("机器名称")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("MachineName", "机器名称", "")]
        public String MachineName { get => _MachineName; set { if (OnPropertyChanging("MachineName", value)) { _MachineName = value; OnPropertyChanged("MachineName"); } } }

        private String _UserName;
        /// <summary>用户名称</summary>
        [Category("硬件信息")]
        [DisplayName("用户名称")]
        [Description("用户名称")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("UserName", "用户名称", "")]
        public String UserName { get => _UserName; set { if (OnPropertyChanging("UserName", value)) { _UserName = value; OnPropertyChanged("UserName"); } } }

        private String _IP;
        /// <summary>本地IP</summary>
        [Category("硬件信息")]
        [DisplayName("本地IP")]
        [Description("本地IP")]
        [DataObjectField(false, false, true, 200)]
        [BindColumn("IP", "本地IP", "")]
        public String IP { get => _IP; set { if (OnPropertyChanging("IP", value)) { _IP = value; OnPropertyChanged("IP"); } } }

        private Int32 _Cpu;
        /// <summary>CPU。处理器核心数</summary>
        [Category("硬件信息")]
        [DisplayName("CPU")]
        [Description("CPU。处理器核心数")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Cpu", "CPU。处理器核心数", "")]
        public Int32 Cpu { get => _Cpu; set { if (OnPropertyChanging("Cpu", value)) { _Cpu = value; OnPropertyChanged("Cpu"); } } }

        private Int32 _Memory;
        /// <summary>内存。单位M</summary>
        [Category("硬件信息")]
        [DisplayName("内存")]
        [Description("内存。单位M")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Memory", "内存。单位M", "")]
        public Int32 Memory { get => _Memory; set { if (OnPropertyChanging("Memory", value)) { _Memory = value; OnPropertyChanged("Memory"); } } }

        private Int32 _TotalSize;
        /// <summary>磁盘。应用所在盘，单位M</summary>
        [Category("硬件信息")]
        [DisplayName("磁盘")]
        [Description("磁盘。应用所在盘，单位M")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("TotalSize", "磁盘。应用所在盘，单位M", "")]
        public Int32 TotalSize { get => _TotalSize; set { if (OnPropertyChanging("TotalSize", value)) { _TotalSize = value; OnPropertyChanged("TotalSize"); } } }

        private Int32 _MaxOpenFiles;
        /// <summary>最大打开文件。Linux上的ulimit -n</summary>
        [Category("硬件信息")]
        [DisplayName("最大打开文件")]
        [Description("最大打开文件。Linux上的ulimit -n")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("MaxOpenFiles", "最大打开文件。Linux上的ulimit -n", "")]
        public Int32 MaxOpenFiles { get => _MaxOpenFiles; set { if (OnPropertyChanging("MaxOpenFiles", value)) { _MaxOpenFiles = value; OnPropertyChanged("MaxOpenFiles"); } } }

        private String _Dpi;
        /// <summary>像素点。默认96*96</summary>
        [Category("硬件信息")]
        [DisplayName("像素点")]
        [Description("像素点。默认96*96")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Dpi", "像素点。默认96*96", "")]
        public String Dpi { get => _Dpi; set { if (OnPropertyChanging("Dpi", value)) { _Dpi = value; OnPropertyChanged("Dpi"); } } }

        private String _Resolution;
        /// <summary>分辨率。例如1024*768</summary>
        [Category("硬件信息")]
        [DisplayName("分辨率")]
        [Description("分辨率。例如1024*768")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Resolution", "分辨率。例如1024*768", "")]
        public String Resolution { get => _Resolution; set { if (OnPropertyChanging("Resolution", value)) { _Resolution = value; OnPropertyChanged("Resolution"); } } }

        private String _Product;
        /// <summary>产品名</summary>
        [Category("硬件信息")]
        [DisplayName("产品名")]
        [Description("产品名")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Product", "产品名", "")]
        public String Product { get => _Product; set { if (OnPropertyChanging("Product", value)) { _Product = value; OnPropertyChanged("Product"); } } }

        private String _Processor;
        /// <summary>处理器</summary>
        [Category("硬件信息")]
        [DisplayName("处理器")]
        [Description("处理器")]
        [DataObjectField(false, false, true, 200)]
        [BindColumn("Processor", "处理器", "")]
        public String Processor { get => _Processor; set { if (OnPropertyChanging("Processor", value)) { _Processor = value; OnPropertyChanged("Processor"); } } }

        private String _Uuid;
        /// <summary>唯一标识</summary>
        [Category("硬件信息")]
        [DisplayName("唯一标识")]
        [Description("唯一标识")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Uuid", "唯一标识", "")]
        public String Uuid { get => _Uuid; set { if (OnPropertyChanging("Uuid", value)) { _Uuid = value; OnPropertyChanged("Uuid"); } } }

        private String _MachineGuid;
        /// <summary>机器标识</summary>
        [Category("硬件信息")]
        [DisplayName("机器标识")]
        [Description("机器标识")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("MachineGuid", "机器标识", "")]
        public String MachineGuid { get => _MachineGuid; set { if (OnPropertyChanging("MachineGuid", value)) { _MachineGuid = value; OnPropertyChanged("MachineGuid"); } } }

        private String _SerialNumber;
        /// <summary>序列号。适用于品牌机，跟笔记本标签显示一致</summary>
        [Category("硬件信息")]
        [DisplayName("序列号")]
        [Description("序列号。适用于品牌机，跟笔记本标签显示一致")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("SerialNumber", "序列号。适用于品牌机，跟笔记本标签显示一致", "")]
        public String SerialNumber { get => _SerialNumber; set { if (OnPropertyChanging("SerialNumber", value)) { _SerialNumber = value; OnPropertyChanged("SerialNumber"); } } }

        private String _Board;
        /// <summary>主板。序列号或家族信息</summary>
        [Category("硬件信息")]
        [DisplayName("主板")]
        [Description("主板。序列号或家族信息")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Board", "主板。序列号或家族信息", "")]
        public String Board { get => _Board; set { if (OnPropertyChanging("Board", value)) { _Board = value; OnPropertyChanged("Board"); } } }

        private String _DiskID;
        /// <summary>磁盘序列号</summary>
        [Category("硬件信息")]
        [DisplayName("磁盘序列号")]
        [Description("磁盘序列号")]
        [DataObjectField(false, false, true, 200)]
        [BindColumn("DiskID", "磁盘序列号", "")]
        public String DiskID { get => _DiskID; set { if (OnPropertyChanging("DiskID", value)) { _DiskID = value; OnPropertyChanged("DiskID"); } } }

        private String _MACs;
        /// <summary>网卡</summary>
        [Category("硬件信息")]
        [DisplayName("网卡")]
        [Description("网卡")]
        [DataObjectField(false, false, true, 200)]
        [BindColumn("MACs", "网卡", "")]
        public String MACs { get => _MACs; set { if (OnPropertyChanging("MACs", value)) { _MACs = value; OnPropertyChanged("MACs"); } } }

        private String _InstallPath;
        /// <summary>安装路径</summary>
        [Category("硬件信息")]
        [DisplayName("安装路径")]
        [Description("安装路径")]
        [DataObjectField(false, false, true, 200)]
        [BindColumn("InstallPath", "安装路径", "")]
        public String InstallPath { get => _InstallPath; set { if (OnPropertyChanging("InstallPath", value)) { _InstallPath = value; OnPropertyChanged("InstallPath"); } } }

        private String _Runtime;
        /// <summary>运行时。.Net运行时版本</summary>
        [Category("硬件信息")]
        [DisplayName("运行时")]
        [Description("运行时。.Net运行时版本")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Runtime", "运行时。.Net运行时版本", "")]
        public String Runtime { get => _Runtime; set { if (OnPropertyChanging("Runtime", value)) { _Runtime = value; OnPropertyChanged("Runtime"); } } }

        private String _Framework;
        /// <summary>框架。本地支持的最高版本框架</summary>
        [Category("硬件信息")]
        [DisplayName("框架")]
        [Description("框架。本地支持的最高版本框架")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Framework", "框架。本地支持的最高版本框架", "")]
        public String Framework { get => _Framework; set { if (OnPropertyChanging("Framework", value)) { _Framework = value; OnPropertyChanged("Framework"); } } }

        private Int32 _ProvinceID;
        /// <summary>省份</summary>
        [DisplayName("省份")]
        [Description("省份")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("ProvinceID", "省份", "")]
        public Int32 ProvinceID { get => _ProvinceID; set { if (OnPropertyChanging("ProvinceID", value)) { _ProvinceID = value; OnPropertyChanged("ProvinceID"); } } }

        private Int32 _CityID;
        /// <summary>城市</summary>
        [DisplayName("城市")]
        [Description("城市")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("CityID", "城市", "")]
        public Int32 CityID { get => _CityID; set { if (OnPropertyChanging("CityID", value)) { _CityID = value; OnPropertyChanged("CityID"); } } }

        private String _Address;
        /// <summary>地址。该节点所处位置</summary>
        [DisplayName("地址")]
        [Description("地址。该节点所处位置")]
        [DataObjectField(false, false, true, 200)]
        [BindColumn("Address", "地址。该节点所处位置", "")]
        public String Address { get => _Address; set { if (OnPropertyChanging("Address", value)) { _Address = value; OnPropertyChanged("Address"); } } }

        private Int32 _Period;
        /// <summary>采样周期。默认60秒</summary>
        [DisplayName("采样周期")]
        [Description("采样周期。默认60秒")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Period", "采样周期。默认60秒", "")]
        public Int32 Period { get => _Period; set { if (OnPropertyChanging("Period", value)) { _Period = value; OnPropertyChanged("Period"); } } }

        private String _WebHook;
        /// <summary>告警机器人。钉钉、企业微信等</summary>
        [Category("告警")]
        [DisplayName("告警机器人")]
        [Description("告警机器人。钉钉、企业微信等")]
        [DataObjectField(false, false, true, 500)]
        [BindColumn("WebHook", "告警机器人。钉钉、企业微信等", "")]
        public String WebHook { get => _WebHook; set { if (OnPropertyChanging("WebHook", value)) { _WebHook = value; OnPropertyChanged("WebHook"); } } }

        private Int32 _AlarmCpuRate;
        /// <summary>CPU告警。CPU告警的百分比阈值，CPU使用率达到该值时告警，百分之一</summary>
        [Category("告警")]
        [DisplayName("CPU告警")]
        [Description("CPU告警。CPU告警的百分比阈值，CPU使用率达到该值时告警，百分之一")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("AlarmCpuRate", "CPU告警。CPU告警的百分比阈值，CPU使用率达到该值时告警，百分之一", "")]
        public Int32 AlarmCpuRate { get => _AlarmCpuRate; set { if (OnPropertyChanging("AlarmCpuRate", value)) { _AlarmCpuRate = value; OnPropertyChanged("AlarmCpuRate"); } } }

        private Int32 _AlarmMemoryRate;
        /// <summary>内存告警。内存告警的百分比阈值，内存使用率达到该值时告警，百分之一</summary>
        [Category("告警")]
        [DisplayName("内存告警")]
        [Description("内存告警。内存告警的百分比阈值，内存使用率达到该值时告警，百分之一")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("AlarmMemoryRate", "内存告警。内存告警的百分比阈值，内存使用率达到该值时告警，百分之一", "")]
        public Int32 AlarmMemoryRate { get => _AlarmMemoryRate; set { if (OnPropertyChanging("AlarmMemoryRate", value)) { _AlarmMemoryRate = value; OnPropertyChanged("AlarmMemoryRate"); } } }

        private Int32 _AlarmDiskRate;
        /// <summary>磁盘告警。磁盘告警的百分比阈值，磁盘使用率达到该值时告警，百分之一</summary>
        [Category("告警")]
        [DisplayName("磁盘告警")]
        [Description("磁盘告警。磁盘告警的百分比阈值，磁盘使用率达到该值时告警，百分之一")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("AlarmDiskRate", "磁盘告警。磁盘告警的百分比阈值，磁盘使用率达到该值时告警，百分之一", "")]
        public Int32 AlarmDiskRate { get => _AlarmDiskRate; set { if (OnPropertyChanging("AlarmDiskRate", value)) { _AlarmDiskRate = value; OnPropertyChanged("AlarmDiskRate"); } } }

        private Int32 _AlarmTcp;
        /// <summary>连接数告警。TCP连接数达到该值时告警，包括连接数、主动关闭和被动关闭</summary>
        [Category("告警")]
        [DisplayName("连接数告警")]
        [Description("连接数告警。TCP连接数达到该值时告警，包括连接数、主动关闭和被动关闭")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("AlarmTcp", "连接数告警。TCP连接数达到该值时告警，包括连接数、主动关闭和被动关闭", "")]
        public Int32 AlarmTcp { get => _AlarmTcp; set { if (OnPropertyChanging("AlarmTcp", value)) { _AlarmTcp = value; OnPropertyChanged("AlarmTcp"); } } }

        private String _AlarmProcesses;
        /// <summary>进程告警。要守护的进程不存在时告警，多进程逗号隔开，支持*模糊匹配</summary>
        [Category("告警")]
        [DisplayName("进程告警")]
        [Description("进程告警。要守护的进程不存在时告警，多进程逗号隔开，支持*模糊匹配")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("AlarmProcesses", "进程告警。要守护的进程不存在时告警，多进程逗号隔开，支持*模糊匹配", "")]
        public String AlarmProcesses { get => _AlarmProcesses; set { if (OnPropertyChanging("AlarmProcesses", value)) { _AlarmProcesses = value; OnPropertyChanged("AlarmProcesses"); } } }

        private Boolean _AlarmOnOffline;
        /// <summary>下线告警。节点下线时，发送告警</summary>
        [Category("告警")]
        [DisplayName("下线告警")]
        [Description("下线告警。节点下线时，发送告警")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("AlarmOnOffline", "下线告警。节点下线时，发送告警", "")]
        public Boolean AlarmOnOffline { get => _AlarmOnOffline; set { if (OnPropertyChanging("AlarmOnOffline", value)) { _AlarmOnOffline = value; OnPropertyChanged("AlarmOnOffline"); } } }

        private Int32 _Logins;
        /// <summary>登录次数</summary>
        [DisplayName("登录次数")]
        [Description("登录次数")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Logins", "登录次数", "")]
        public Int32 Logins { get => _Logins; set { if (OnPropertyChanging("Logins", value)) { _Logins = value; OnPropertyChanged("Logins"); } } }

        private DateTime _LastLogin;
        /// <summary>最后登录</summary>
        [DisplayName("最后登录")]
        [Description("最后登录")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("LastLogin", "最后登录", "")]
        public DateTime LastLogin { get => _LastLogin; set { if (OnPropertyChanging("LastLogin", value)) { _LastLogin = value; OnPropertyChanged("LastLogin"); } } }

        private String _LastLoginIP;
        /// <summary>最后IP。最后的公网IP地址</summary>
        [DisplayName("最后IP")]
        [Description("最后IP。最后的公网IP地址")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("LastLoginIP", "最后IP。最后的公网IP地址", "")]
        public String LastLoginIP { get => _LastLoginIP; set { if (OnPropertyChanging("LastLoginIP", value)) { _LastLoginIP = value; OnPropertyChanged("LastLoginIP"); } } }

        private Int32 _OnlineTime;
        /// <summary>在线时长。单位，秒</summary>
        [DisplayName("在线时长")]
        [Description("在线时长。单位，秒")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("OnlineTime", "在线时长。单位，秒", "")]
        public Int32 OnlineTime { get => _OnlineTime; set { if (OnPropertyChanging("OnlineTime", value)) { _OnlineTime = value; OnPropertyChanged("OnlineTime"); } } }

        private String _LastVersion;
        /// <summary>最后版本。最后一次升级所使用的版本号，避免重复升级同一个版本</summary>
        [DisplayName("最后版本")]
        [Description("最后版本。最后一次升级所使用的版本号，避免重复升级同一个版本")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("LastVersion", "最后版本。最后一次升级所使用的版本号，避免重复升级同一个版本", "")]
        public String LastVersion { get => _LastVersion; set { if (OnPropertyChanging("LastVersion", value)) { _LastVersion = value; OnPropertyChanged("LastVersion"); } } }

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
            get
            {
                switch (name)
                {
                    case "ID": return _ID;
                    case "Name": return _Name;
                    case "Code": return _Code;
                    case "Secret": return _Secret;
                    case "Enable": return _Enable;
                    case "ProductCode": return _ProductCode;
                    case "Category": return _Category;
                    case "Version": return _Version;
                    case "CompileTime": return _CompileTime;
                    case "OS": return _OS;
                    case "OSVersion": return _OSVersion;
                    case "Architecture": return _Architecture;
                    case "MachineName": return _MachineName;
                    case "UserName": return _UserName;
                    case "IP": return _IP;
                    case "Cpu": return _Cpu;
                    case "Memory": return _Memory;
                    case "TotalSize": return _TotalSize;
                    case "MaxOpenFiles": return _MaxOpenFiles;
                    case "Dpi": return _Dpi;
                    case "Resolution": return _Resolution;
                    case "Product": return _Product;
                    case "Processor": return _Processor;
                    case "Uuid": return _Uuid;
                    case "MachineGuid": return _MachineGuid;
                    case "SerialNumber": return _SerialNumber;
                    case "Board": return _Board;
                    case "DiskID": return _DiskID;
                    case "MACs": return _MACs;
                    case "InstallPath": return _InstallPath;
                    case "Runtime": return _Runtime;
                    case "Framework": return _Framework;
                    case "ProvinceID": return _ProvinceID;
                    case "CityID": return _CityID;
                    case "Address": return _Address;
                    case "Period": return _Period;
                    case "WebHook": return _WebHook;
                    case "AlarmCpuRate": return _AlarmCpuRate;
                    case "AlarmMemoryRate": return _AlarmMemoryRate;
                    case "AlarmDiskRate": return _AlarmDiskRate;
                    case "AlarmTcp": return _AlarmTcp;
                    case "AlarmProcesses": return _AlarmProcesses;
                    case "AlarmOnOffline": return _AlarmOnOffline;
                    case "Logins": return _Logins;
                    case "LastLogin": return _LastLogin;
                    case "LastLoginIP": return _LastLoginIP;
                    case "OnlineTime": return _OnlineTime;
                    case "LastVersion": return _LastVersion;
                    case "CreateUserID": return _CreateUserID;
                    case "CreateTime": return _CreateTime;
                    case "CreateIP": return _CreateIP;
                    case "UpdateUserID": return _UpdateUserID;
                    case "UpdateTime": return _UpdateTime;
                    case "UpdateIP": return _UpdateIP;
                    case "Remark": return _Remark;
                    default: return base[name];
                }
            }
            set
            {
                switch (name)
                {
                    case "ID": _ID = value.ToInt(); break;
                    case "Name": _Name = Convert.ToString(value); break;
                    case "Code": _Code = Convert.ToString(value); break;
                    case "Secret": _Secret = Convert.ToString(value); break;
                    case "Enable": _Enable = value.ToBoolean(); break;
                    case "ProductCode": _ProductCode = Convert.ToString(value); break;
                    case "Category": _Category = Convert.ToString(value); break;
                    case "Version": _Version = Convert.ToString(value); break;
                    case "CompileTime": _CompileTime = value.ToDateTime(); break;
                    case "OS": _OS = Convert.ToString(value); break;
                    case "OSVersion": _OSVersion = Convert.ToString(value); break;
                    case "Architecture": _Architecture = Convert.ToString(value); break;
                    case "MachineName": _MachineName = Convert.ToString(value); break;
                    case "UserName": _UserName = Convert.ToString(value); break;
                    case "IP": _IP = Convert.ToString(value); break;
                    case "Cpu": _Cpu = value.ToInt(); break;
                    case "Memory": _Memory = value.ToInt(); break;
                    case "TotalSize": _TotalSize = value.ToInt(); break;
                    case "MaxOpenFiles": _MaxOpenFiles = value.ToInt(); break;
                    case "Dpi": _Dpi = Convert.ToString(value); break;
                    case "Resolution": _Resolution = Convert.ToString(value); break;
                    case "Product": _Product = Convert.ToString(value); break;
                    case "Processor": _Processor = Convert.ToString(value); break;
                    case "Uuid": _Uuid = Convert.ToString(value); break;
                    case "MachineGuid": _MachineGuid = Convert.ToString(value); break;
                    case "SerialNumber": _SerialNumber = Convert.ToString(value); break;
                    case "Board": _Board = Convert.ToString(value); break;
                    case "DiskID": _DiskID = Convert.ToString(value); break;
                    case "MACs": _MACs = Convert.ToString(value); break;
                    case "InstallPath": _InstallPath = Convert.ToString(value); break;
                    case "Runtime": _Runtime = Convert.ToString(value); break;
                    case "Framework": _Framework = Convert.ToString(value); break;
                    case "ProvinceID": _ProvinceID = value.ToInt(); break;
                    case "CityID": _CityID = value.ToInt(); break;
                    case "Address": _Address = Convert.ToString(value); break;
                    case "Period": _Period = value.ToInt(); break;
                    case "WebHook": _WebHook = Convert.ToString(value); break;
                    case "AlarmCpuRate": _AlarmCpuRate = value.ToInt(); break;
                    case "AlarmMemoryRate": _AlarmMemoryRate = value.ToInt(); break;
                    case "AlarmDiskRate": _AlarmDiskRate = value.ToInt(); break;
                    case "AlarmTcp": _AlarmTcp = value.ToInt(); break;
                    case "AlarmProcesses": _AlarmProcesses = Convert.ToString(value); break;
                    case "AlarmOnOffline": _AlarmOnOffline = value.ToBoolean(); break;
                    case "Logins": _Logins = value.ToInt(); break;
                    case "LastLogin": _LastLogin = value.ToDateTime(); break;
                    case "LastLoginIP": _LastLoginIP = Convert.ToString(value); break;
                    case "OnlineTime": _OnlineTime = value.ToInt(); break;
                    case "LastVersion": _LastVersion = Convert.ToString(value); break;
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

        #region 字段名
        /// <summary>取得节点字段信息的快捷方式</summary>
        public partial class _
        {
            /// <summary>编号</summary>
            public static readonly Field ID = FindByName("ID");

            /// <summary>名称</summary>
            public static readonly Field Name = FindByName("Name");

            /// <summary>编码。NodeKey</summary>
            public static readonly Field Code = FindByName("Code");

            /// <summary>密钥。NodeSecret</summary>
            public static readonly Field Secret = FindByName("Secret");

            /// <summary>启用</summary>
            public static readonly Field Enable = FindByName("Enable");

            /// <summary>产品。产品编码，用于区分不同类型节点</summary>
            public static readonly Field ProductCode = FindByName("ProductCode");

            /// <summary>分类</summary>
            public static readonly Field Category = FindByName("Category");

            /// <summary>版本</summary>
            public static readonly Field Version = FindByName("Version");

            /// <summary>编译时间</summary>
            public static readonly Field CompileTime = FindByName("CompileTime");

            /// <summary>操作系统</summary>
            public static readonly Field OS = FindByName("OS");

            /// <summary>系统版本</summary>
            public static readonly Field OSVersion = FindByName("OSVersion");

            /// <summary>架构。处理器架构，X86/X64/Arm/Arm64</summary>
            public static readonly Field Architecture = FindByName("Architecture");

            /// <summary>机器名称</summary>
            public static readonly Field MachineName = FindByName("MachineName");

            /// <summary>用户名称</summary>
            public static readonly Field UserName = FindByName("UserName");

            /// <summary>本地IP</summary>
            public static readonly Field IP = FindByName("IP");

            /// <summary>CPU。处理器核心数</summary>
            public static readonly Field Cpu = FindByName("Cpu");

            /// <summary>内存。单位M</summary>
            public static readonly Field Memory = FindByName("Memory");

            /// <summary>磁盘。应用所在盘，单位M</summary>
            public static readonly Field TotalSize = FindByName("TotalSize");

            /// <summary>最大打开文件。Linux上的ulimit -n</summary>
            public static readonly Field MaxOpenFiles = FindByName("MaxOpenFiles");

            /// <summary>像素点。默认96*96</summary>
            public static readonly Field Dpi = FindByName("Dpi");

            /// <summary>分辨率。例如1024*768</summary>
            public static readonly Field Resolution = FindByName("Resolution");

            /// <summary>产品名</summary>
            public static readonly Field Product = FindByName("Product");

            /// <summary>处理器</summary>
            public static readonly Field Processor = FindByName("Processor");

            /// <summary>唯一标识</summary>
            public static readonly Field Uuid = FindByName("Uuid");

            /// <summary>机器标识</summary>
            public static readonly Field MachineGuid = FindByName("MachineGuid");

            /// <summary>序列号。适用于品牌机，跟笔记本标签显示一致</summary>
            public static readonly Field SerialNumber = FindByName("SerialNumber");

            /// <summary>主板。序列号或家族信息</summary>
            public static readonly Field Board = FindByName("Board");

            /// <summary>磁盘序列号</summary>
            public static readonly Field DiskID = FindByName("DiskID");

            /// <summary>网卡</summary>
            public static readonly Field MACs = FindByName("MACs");

            /// <summary>安装路径</summary>
            public static readonly Field InstallPath = FindByName("InstallPath");

            /// <summary>运行时。.Net运行时版本</summary>
            public static readonly Field Runtime = FindByName("Runtime");

            /// <summary>框架。本地支持的最高版本框架</summary>
            public static readonly Field Framework = FindByName("Framework");

            /// <summary>省份</summary>
            public static readonly Field ProvinceID = FindByName("ProvinceID");

            /// <summary>城市</summary>
            public static readonly Field CityID = FindByName("CityID");

            /// <summary>地址。该节点所处位置</summary>
            public static readonly Field Address = FindByName("Address");

            /// <summary>采样周期。默认60秒</summary>
            public static readonly Field Period = FindByName("Period");

            /// <summary>告警机器人。钉钉、企业微信等</summary>
            public static readonly Field WebHook = FindByName("WebHook");

            /// <summary>CPU告警。CPU告警的百分比阈值，CPU使用率达到该值时告警，百分之一</summary>
            public static readonly Field AlarmCpuRate = FindByName("AlarmCpuRate");

            /// <summary>内存告警。内存告警的百分比阈值，内存使用率达到该值时告警，百分之一</summary>
            public static readonly Field AlarmMemoryRate = FindByName("AlarmMemoryRate");

            /// <summary>磁盘告警。磁盘告警的百分比阈值，磁盘使用率达到该值时告警，百分之一</summary>
            public static readonly Field AlarmDiskRate = FindByName("AlarmDiskRate");

            /// <summary>连接数告警。TCP连接数达到该值时告警，包括连接数、主动关闭和被动关闭</summary>
            public static readonly Field AlarmTcp = FindByName("AlarmTcp");

            /// <summary>进程告警。要守护的进程不存在时告警，多进程逗号隔开，支持*模糊匹配</summary>
            public static readonly Field AlarmProcesses = FindByName("AlarmProcesses");

            /// <summary>下线告警。节点下线时，发送告警</summary>
            public static readonly Field AlarmOnOffline = FindByName("AlarmOnOffline");

            /// <summary>登录次数</summary>
            public static readonly Field Logins = FindByName("Logins");

            /// <summary>最后登录</summary>
            public static readonly Field LastLogin = FindByName("LastLogin");

            /// <summary>最后IP。最后的公网IP地址</summary>
            public static readonly Field LastLoginIP = FindByName("LastLoginIP");

            /// <summary>在线时长。单位，秒</summary>
            public static readonly Field OnlineTime = FindByName("OnlineTime");

            /// <summary>最后版本。最后一次升级所使用的版本号，避免重复升级同一个版本</summary>
            public static readonly Field LastVersion = FindByName("LastVersion");

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

            /// <summary>产品。产品编码，用于区分不同类型节点</summary>
            public const String ProductCode = "ProductCode";

            /// <summary>分类</summary>
            public const String Category = "Category";

            /// <summary>版本</summary>
            public const String Version = "Version";

            /// <summary>编译时间</summary>
            public const String CompileTime = "CompileTime";

            /// <summary>操作系统</summary>
            public const String OS = "OS";

            /// <summary>系统版本</summary>
            public const String OSVersion = "OSVersion";

            /// <summary>架构。处理器架构，X86/X64/Arm/Arm64</summary>
            public const String Architecture = "Architecture";

            /// <summary>机器名称</summary>
            public const String MachineName = "MachineName";

            /// <summary>用户名称</summary>
            public const String UserName = "UserName";

            /// <summary>本地IP</summary>
            public const String IP = "IP";

            /// <summary>CPU。处理器核心数</summary>
            public const String Cpu = "Cpu";

            /// <summary>内存。单位M</summary>
            public const String Memory = "Memory";

            /// <summary>磁盘。应用所在盘，单位M</summary>
            public const String TotalSize = "TotalSize";

            /// <summary>最大打开文件。Linux上的ulimit -n</summary>
            public const String MaxOpenFiles = "MaxOpenFiles";

            /// <summary>像素点。默认96*96</summary>
            public const String Dpi = "Dpi";

            /// <summary>分辨率。例如1024*768</summary>
            public const String Resolution = "Resolution";

            /// <summary>产品名</summary>
            public const String Product = "Product";

            /// <summary>处理器</summary>
            public const String Processor = "Processor";

            /// <summary>唯一标识</summary>
            public const String Uuid = "Uuid";

            /// <summary>机器标识</summary>
            public const String MachineGuid = "MachineGuid";

            /// <summary>序列号。适用于品牌机，跟笔记本标签显示一致</summary>
            public const String SerialNumber = "SerialNumber";

            /// <summary>主板。序列号或家族信息</summary>
            public const String Board = "Board";

            /// <summary>磁盘序列号</summary>
            public const String DiskID = "DiskID";

            /// <summary>网卡</summary>
            public const String MACs = "MACs";

            /// <summary>安装路径</summary>
            public const String InstallPath = "InstallPath";

            /// <summary>运行时。.Net运行时版本</summary>
            public const String Runtime = "Runtime";

            /// <summary>框架。本地支持的最高版本框架</summary>
            public const String Framework = "Framework";

            /// <summary>省份</summary>
            public const String ProvinceID = "ProvinceID";

            /// <summary>城市</summary>
            public const String CityID = "CityID";

            /// <summary>地址。该节点所处位置</summary>
            public const String Address = "Address";

            /// <summary>采样周期。默认60秒</summary>
            public const String Period = "Period";

            /// <summary>告警机器人。钉钉、企业微信等</summary>
            public const String WebHook = "WebHook";

            /// <summary>CPU告警。CPU告警的百分比阈值，CPU使用率达到该值时告警，百分之一</summary>
            public const String AlarmCpuRate = "AlarmCpuRate";

            /// <summary>内存告警。内存告警的百分比阈值，内存使用率达到该值时告警，百分之一</summary>
            public const String AlarmMemoryRate = "AlarmMemoryRate";

            /// <summary>磁盘告警。磁盘告警的百分比阈值，磁盘使用率达到该值时告警，百分之一</summary>
            public const String AlarmDiskRate = "AlarmDiskRate";

            /// <summary>连接数告警。TCP连接数达到该值时告警，包括连接数、主动关闭和被动关闭</summary>
            public const String AlarmTcp = "AlarmTcp";

            /// <summary>进程告警。要守护的进程不存在时告警，多进程逗号隔开，支持*模糊匹配</summary>
            public const String AlarmProcesses = "AlarmProcesses";

            /// <summary>下线告警。节点下线时，发送告警</summary>
            public const String AlarmOnOffline = "AlarmOnOffline";

            /// <summary>登录次数</summary>
            public const String Logins = "Logins";

            /// <summary>最后登录</summary>
            public const String LastLogin = "LastLogin";

            /// <summary>最后IP。最后的公网IP地址</summary>
            public const String LastLoginIP = "LastLoginIP";

            /// <summary>在线时长。单位，秒</summary>
            public const String OnlineTime = "OnlineTime";

            /// <summary>最后版本。最后一次升级所使用的版本号，避免重复升级同一个版本</summary>
            public const String LastVersion = "LastVersion";

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
}