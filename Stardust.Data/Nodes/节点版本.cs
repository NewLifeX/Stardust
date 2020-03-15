using System;
using System.Collections.Generic;
using System.ComponentModel;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Stardust.Data.Nodes
{
    /// <summary>节点版本。发布更新</summary>
    [Serializable]
    [DataObject]
    [Description("节点版本。发布更新")]
    [BindIndex("IU_NodeVersion_Version", true, "Version")]
    [BindTable("NodeVersion", Description = "节点版本。发布更新", ConnName = "Node", DbType = DatabaseType.None)]
    public partial class NodeVersion : INodeVersion
    {
        #region 属性
        private Int32 _ID;
        /// <summary>编号</summary>
        [DisplayName("编号")]
        [Description("编号")]
        [DataObjectField(true, true, false, 0)]
        [BindColumn("ID", "编号", "")]
        public Int32 ID { get => _ID; set { if (OnPropertyChanging(__.ID, value)) { _ID = value; OnPropertyChanged(__.ID); } } }

        private String _Version;
        /// <summary>版本号</summary>
        [DisplayName("版本号")]
        [Description("版本号")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Version", "版本号", "")]
        public String Version { get => _Version; set { if (OnPropertyChanging(__.Version, value)) { _Version = value; OnPropertyChanged(__.Version); } } }

        private Boolean _Enable;
        /// <summary>启用。启用/停用</summary>
        [DisplayName("启用")]
        [Description("启用。启用/停用")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Enable", "启用。启用/停用", "")]
        public Boolean Enable { get => _Enable; set { if (OnPropertyChanging(__.Enable, value)) { _Enable = value; OnPropertyChanged(__.Enable); } } }

        private Boolean _Force;
        /// <summary>强制。强制升级</summary>
        [DisplayName("强制")]
        [Description("强制。强制升级")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Force", "强制。强制升级", "")]
        public Boolean Force { get => _Force; set { if (OnPropertyChanging(__.Force, value)) { _Force = value; OnPropertyChanged(__.Force); } } }

        private NodeChannels _Channel;
        /// <summary>升级通道</summary>
        [DisplayName("升级通道")]
        [Description("升级通道")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Channel", "升级通道", "")]
        public NodeChannels Channel { get => _Channel; set { if (OnPropertyChanging(__.Channel, value)) { _Channel = value; OnPropertyChanged(__.Channel); } } }

        private String _Strategy;
        /// <summary>策略。升级策略</summary>
        [DisplayName("策略")]
        [Description("策略。升级策略")]
        [DataObjectField(false, false, true, 500)]
        [BindColumn("Strategy", "策略。升级策略", "")]
        public String Strategy { get => _Strategy; set { if (OnPropertyChanging(__.Strategy, value)) { _Strategy = value; OnPropertyChanged(__.Strategy); } } }

        private String _Source;
        /// <summary>升级源</summary>
        [DisplayName("升级源")]
        [Description("升级源")]
        [DataObjectField(false, false, true, 200)]
        [BindColumn("Source", "升级源", "")]
        public String Source { get => _Source; set { if (OnPropertyChanging(__.Source, value)) { _Source = value; OnPropertyChanged(__.Source); } } }

        private String _Executor;
        /// <summary>执行命令。空格前后为文件名和参数</summary>
        [DisplayName("执行命令")]
        [Description("执行命令。空格前后为文件名和参数")]
        [DataObjectField(false, false, true, 200)]
        [BindColumn("Executor", "执行命令。空格前后为文件名和参数", "")]
        public String Executor { get => _Executor; set { if (OnPropertyChanging(__.Executor, value)) { _Executor = value; OnPropertyChanged(__.Executor); } } }

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

        private String _Description;
        /// <summary>描述</summary>
        [DisplayName("描述")]
        [Description("描述")]
        [DataObjectField(false, false, true, 500)]
        [BindColumn("Description", "描述", "")]
        public String Description { get => _Description; set { if (OnPropertyChanging(__.Description, value)) { _Description = value; OnPropertyChanged(__.Description); } } }
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
                    case __.Version: return _Version;
                    case __.Enable: return _Enable;
                    case __.Force: return _Force;
                    case __.Channel: return _Channel;
                    case __.Strategy: return _Strategy;
                    case __.Source: return _Source;
                    case __.Executor: return _Executor;
                    case __.CreateUserID: return _CreateUserID;
                    case __.CreateTime: return _CreateTime;
                    case __.CreateIP: return _CreateIP;
                    case __.UpdateUserID: return _UpdateUserID;
                    case __.UpdateTime: return _UpdateTime;
                    case __.UpdateIP: return _UpdateIP;
                    case __.Description: return _Description;
                    default: return base[name];
                }
            }
            set
            {
                switch (name)
                {
                    case __.ID: _ID = value.ToInt(); break;
                    case __.Version: _Version = Convert.ToString(value); break;
                    case __.Enable: _Enable = value.ToBoolean(); break;
                    case __.Force: _Force = value.ToBoolean(); break;
                    case __.Channel: _Channel = (NodeChannels)value; break;
                    case __.Strategy: _Strategy = Convert.ToString(value); break;
                    case __.Source: _Source = Convert.ToString(value); break;
                    case __.Executor: _Executor = Convert.ToString(value); break;
                    case __.CreateUserID: _CreateUserID = value.ToInt(); break;
                    case __.CreateTime: _CreateTime = value.ToDateTime(); break;
                    case __.CreateIP: _CreateIP = Convert.ToString(value); break;
                    case __.UpdateUserID: _UpdateUserID = value.ToInt(); break;
                    case __.UpdateTime: _UpdateTime = value.ToDateTime(); break;
                    case __.UpdateIP: _UpdateIP = Convert.ToString(value); break;
                    case __.Description: _Description = Convert.ToString(value); break;
                    default: base[name] = value; break;
                }
            }
        }
        #endregion

        #region 字段名
        /// <summary>取得节点版本字段信息的快捷方式</summary>
        public partial class _
        {
            /// <summary>编号</summary>
            public static readonly Field ID = FindByName(__.ID);

            /// <summary>版本号</summary>
            public static readonly Field Version = FindByName(__.Version);

            /// <summary>启用。启用/停用</summary>
            public static readonly Field Enable = FindByName(__.Enable);

            /// <summary>强制。强制升级</summary>
            public static readonly Field Force = FindByName(__.Force);

            /// <summary>升级通道</summary>
            public static readonly Field Channel = FindByName(__.Channel);

            /// <summary>策略。升级策略</summary>
            public static readonly Field Strategy = FindByName(__.Strategy);

            /// <summary>升级源</summary>
            public static readonly Field Source = FindByName(__.Source);

            /// <summary>执行命令。空格前后为文件名和参数</summary>
            public static readonly Field Executor = FindByName(__.Executor);

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

            /// <summary>描述</summary>
            public static readonly Field Description = FindByName(__.Description);

            static Field FindByName(String name) => Meta.Table.FindByName(name);
        }

        /// <summary>取得节点版本字段名称的快捷方式</summary>
        public partial class __
        {
            /// <summary>编号</summary>
            public const String ID = "ID";

            /// <summary>版本号</summary>
            public const String Version = "Version";

            /// <summary>启用。启用/停用</summary>
            public const String Enable = "Enable";

            /// <summary>强制。强制升级</summary>
            public const String Force = "Force";

            /// <summary>升级通道</summary>
            public const String Channel = "Channel";

            /// <summary>策略。升级策略</summary>
            public const String Strategy = "Strategy";

            /// <summary>升级源</summary>
            public const String Source = "Source";

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

    /// <summary>节点版本。发布更新接口</summary>
    public partial interface INodeVersion
    {
        #region 属性
        /// <summary>编号</summary>
        Int32 ID { get; set; }

        /// <summary>版本号</summary>
        String Version { get; set; }

        /// <summary>启用。启用/停用</summary>
        Boolean Enable { get; set; }

        /// <summary>强制。强制升级</summary>
        Boolean Force { get; set; }

        /// <summary>升级通道</summary>
        NodeChannels Channel { get; set; }

        /// <summary>策略。升级策略</summary>
        String Strategy { get; set; }

        /// <summary>升级源</summary>
        String Source { get; set; }

        /// <summary>执行命令。空格前后为文件名和参数</summary>
        String Executor { get; set; }

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

        /// <summary>描述</summary>
        String Description { get; set; }
        #endregion

        #region 获取/设置 字段值
        /// <summary>获取/设置 字段值</summary>
        /// <param name="name">字段名</param>
        /// <returns></returns>
        Object this[String name] { get; set; }
        #endregion
    }
}