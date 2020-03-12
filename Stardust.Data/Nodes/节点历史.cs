using System;
using System.Collections.Generic;
using System.ComponentModel;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Stardust.Data.Nodes
{
    /// <summary>节点历史</summary>
    [Serializable]
    [DataObject]
    [Description("节点历史")]
    [BindIndex("IX_NodeHistory_NodeID_Action", false, "NodeID,Action")]
    [BindIndex("IX_NodeHistory_CreateTime_NodeID_Action", false, "CreateTime,NodeID,Action")]
    [BindTable("NodeHistory", Description = "节点历史", ConnName = "Node", DbType = DatabaseType.None)]
    public partial class NodeHistory : INodeHistory
    {
        #region 属性
        private Int32 _ID;
        /// <summary>编号</summary>
        [DisplayName("编号")]
        [Description("编号")]
        [DataObjectField(true, true, false, 0)]
        [BindColumn("ID", "编号", "")]
        public Int32 ID { get => _ID; set { if (OnPropertyChanging(__.ID, value)) { _ID = value; OnPropertyChanged(__.ID); } } }

        private Int32 _NodeID;
        /// <summary>节点</summary>
        [DisplayName("节点")]
        [Description("节点")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("NodeID", "节点", "")]
        public Int32 NodeID { get => _NodeID; set { if (OnPropertyChanging(__.NodeID, value)) { _NodeID = value; OnPropertyChanged(__.NodeID); } } }

        private String _Name;
        /// <summary>名称</summary>
        [DisplayName("名称")]
        [Description("名称")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Name", "名称", "", Master = true)]
        public String Name { get => _Name; set { if (OnPropertyChanging(__.Name, value)) { _Name = value; OnPropertyChanged(__.Name); } } }

        private String _Action;
        /// <summary>操作</summary>
        [DisplayName("操作")]
        [Description("操作")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Action", "操作", "")]
        public String Action { get => _Action; set { if (OnPropertyChanging(__.Action, value)) { _Action = value; OnPropertyChanged(__.Action); } } }

        private Boolean _Success;
        /// <summary>成功</summary>
        [DisplayName("成功")]
        [Description("成功")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Success", "成功", "")]
        public Boolean Success { get => _Success; set { if (OnPropertyChanging(__.Success, value)) { _Success = value; OnPropertyChanged(__.Success); } } }

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

        private String _Creator;
        /// <summary>创建者。服务端节点</summary>
        [DisplayName("创建者")]
        [Description("创建者。服务端节点")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Creator", "创建者。服务端节点", "")]
        public String Creator { get => _Creator; set { if (OnPropertyChanging(__.Creator, value)) { _Creator = value; OnPropertyChanged(__.Creator); } } }

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

        private String _Remark;
        /// <summary>内容</summary>
        [DisplayName("内容")]
        [Description("内容")]
        [DataObjectField(false, false, true, 2000)]
        [BindColumn("Content", "内容", "")]
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
                    case __.NodeID: return _NodeID;
                    case __.Name: return _Name;
                    case __.Action: return _Action;
                    case __.Success: return _Success;
                    case __.Version: return _Version;
                    case __.CompileTime: return _CompileTime;
                    case __.Creator: return _Creator;
                    case __.CreateTime: return _CreateTime;
                    case __.CreateIP: return _CreateIP;
                    case __.Remark: return _Remark;
                    default: return base[name];
                }
            }
            set
            {
                switch (name)
                {
                    case __.ID: _ID = value.ToInt(); break;
                    case __.NodeID: _NodeID = value.ToInt(); break;
                    case __.Name: _Name = Convert.ToString(value); break;
                    case __.Action: _Action = Convert.ToString(value); break;
                    case __.Success: _Success = value.ToBoolean(); break;
                    case __.Version: _Version = Convert.ToString(value); break;
                    case __.CompileTime: _CompileTime = value.ToDateTime(); break;
                    case __.Creator: _Creator = Convert.ToString(value); break;
                    case __.CreateTime: _CreateTime = value.ToDateTime(); break;
                    case __.CreateIP: _CreateIP = Convert.ToString(value); break;
                    case __.Remark: _Remark = Convert.ToString(value); break;
                    default: base[name] = value; break;
                }
            }
        }
        #endregion

        #region 字段名
        /// <summary>取得节点历史字段信息的快捷方式</summary>
        public partial class _
        {
            /// <summary>编号</summary>
            public static readonly Field ID = FindByName(__.ID);

            /// <summary>节点</summary>
            public static readonly Field NodeID = FindByName(__.NodeID);

            /// <summary>名称</summary>
            public static readonly Field Name = FindByName(__.Name);

            /// <summary>操作</summary>
            public static readonly Field Action = FindByName(__.Action);

            /// <summary>成功</summary>
            public static readonly Field Success = FindByName(__.Success);

            /// <summary>版本</summary>
            public static readonly Field Version = FindByName(__.Version);

            /// <summary>编译时间</summary>
            public static readonly Field CompileTime = FindByName(__.CompileTime);

            /// <summary>创建者。服务端节点</summary>
            public static readonly Field Creator = FindByName(__.Creator);

            /// <summary>创建时间</summary>
            public static readonly Field CreateTime = FindByName(__.CreateTime);

            /// <summary>创建地址</summary>
            public static readonly Field CreateIP = FindByName(__.CreateIP);

            /// <summary>内容</summary>
            public static readonly Field Remark = FindByName(__.Remark);

            static Field FindByName(String name) => Meta.Table.FindByName(name);
        }

        /// <summary>取得节点历史字段名称的快捷方式</summary>
        public partial class __
        {
            /// <summary>编号</summary>
            public const String ID = "ID";

            /// <summary>节点</summary>
            public const String NodeID = "NodeID";

            /// <summary>名称</summary>
            public const String Name = "Name";

            /// <summary>操作</summary>
            public const String Action = "Action";

            /// <summary>成功</summary>
            public const String Success = "Success";

            /// <summary>版本</summary>
            public const String Version = "Version";

            /// <summary>编译时间</summary>
            public const String CompileTime = "CompileTime";

            /// <summary>创建者。服务端节点</summary>
            public const String Creator = "Creator";

            /// <summary>创建时间</summary>
            public const String CreateTime = "CreateTime";

            /// <summary>创建地址</summary>
            public const String CreateIP = "CreateIP";

            /// <summary>内容</summary>
            public const String Remark = "Remark";
        }
        #endregion
    }

    /// <summary>节点历史接口</summary>
    public partial interface INodeHistory
    {
        #region 属性
        /// <summary>编号</summary>
        Int32 ID { get; set; }

        /// <summary>节点</summary>
        Int32 NodeID { get; set; }

        /// <summary>名称</summary>
        String Name { get; set; }

        /// <summary>操作</summary>
        String Action { get; set; }

        /// <summary>成功</summary>
        Boolean Success { get; set; }

        /// <summary>版本</summary>
        String Version { get; set; }

        /// <summary>编译时间</summary>
        DateTime CompileTime { get; set; }

        /// <summary>创建者。服务端节点</summary>
        String Creator { get; set; }

        /// <summary>创建时间</summary>
        DateTime CreateTime { get; set; }

        /// <summary>创建地址</summary>
        String CreateIP { get; set; }

        /// <summary>内容</summary>
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