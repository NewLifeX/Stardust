using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Stardust.Data.ConfigCenter
{
    /// <summary>配置历史。记录配置变更历史</summary>
    [Serializable]
    [DataObject]
    [Description("配置历史。记录配置变更历史")]
    [BindIndex("IX_ConfigHistory_ConfigID", false, "ConfigID")]
    [BindTable("ConfigHistory", Description = "配置历史。记录配置变更历史", ConnName = "ConfigCenter", DbType = DatabaseType.None)]
    public partial class ConfigHistory
    {
        #region 属性
        private Int32 _Id;
        /// <summary>编号</summary>
        [DisplayName("编号")]
        [Description("编号")]
        [DataObjectField(true, true, false, 0)]
        [BindColumn("Id", "编号", "")]
        public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

        private Int32 _ConfigId;
        /// <summary>配置</summary>
        [DisplayName("配置")]
        [Description("配置")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("ConfigId", "配置", "")]
        public Int32 ConfigId { get => _ConfigId; set { if (OnPropertyChanging("ConfigId", value)) { _ConfigId = value; OnPropertyChanged("ConfigId"); } } }

        private String _Action;
        /// <summary>操作</summary>
        [DisplayName("操作")]
        [Description("操作")]
        [DataObjectField(false, false, false, 50)]
        [BindColumn("Action", "操作", "")]
        public String Action { get => _Action; set { if (OnPropertyChanging("Action", value)) { _Action = value; OnPropertyChanged("Action"); } } }

        private String _Field;
        /// <summary>字段。变更的字段名</summary>
        [DisplayName("字段")]
        [Description("字段。变更的字段名")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Field", "字段。变更的字段名", "")]
        public String Field { get => _Field; set { if (OnPropertyChanging("Field", value)) { _Field = value; OnPropertyChanged("Field"); } } }

        private String _Value;
        /// <summary>数值。变更前数值</summary>
        [DisplayName("数值")]
        [Description("数值。变更前数值")]
        [DataObjectField(false, false, true, 2000)]
        [BindColumn("Value", "数值。变更前数值", "")]
        public String Value { get => _Value; set { if (OnPropertyChanging("Value", value)) { _Value = value; OnPropertyChanged("Value"); } } }

        private Int32 _Version;
        /// <summary>版本。变更前版本</summary>
        [DisplayName("版本")]
        [Description("版本。变更前版本")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Version", "版本。变更前版本", "")]
        public Int32 Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

        private Int32 _CreateUserID;
        /// <summary>创建者</summary>
        [DisplayName("创建者")]
        [Description("创建者")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("CreateUserID", "创建者", "")]
        public Int32 CreateUserID { get => _CreateUserID; set { if (OnPropertyChanging("CreateUserID", value)) { _CreateUserID = value; OnPropertyChanged("CreateUserID"); } } }

        private DateTime _CreateTime;
        /// <summary>创建时间</summary>
        [DisplayName("创建时间")]
        [Description("创建时间")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("CreateTime", "创建时间", "")]
        public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

        private String _CreateIP;
        /// <summary>创建地址</summary>
        [DisplayName("创建地址")]
        [Description("创建地址")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("CreateIP", "创建地址", "")]
        public String CreateIP { get => _CreateIP; set { if (OnPropertyChanging("CreateIP", value)) { _CreateIP = value; OnPropertyChanged("CreateIP"); } } }
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
                    case "Id": return _Id;
                    case "ConfigId": return _ConfigId;
                    case "Action": return _Action;
                    case "Field": return _Field;
                    case "Value": return _Value;
                    case "Version": return _Version;
                    case "CreateUserID": return _CreateUserID;
                    case "CreateTime": return _CreateTime;
                    case "CreateIP": return _CreateIP;
                    default: return base[name];
                }
            }
            set
            {
                switch (name)
                {
                    case "Id": _Id = value.ToInt(); break;
                    case "ConfigId": _ConfigId = value.ToInt(); break;
                    case "Action": _Action = Convert.ToString(value); break;
                    case "Field": _Field = Convert.ToString(value); break;
                    case "Value": _Value = Convert.ToString(value); break;
                    case "Version": _Version = value.ToInt(); break;
                    case "CreateUserID": _CreateUserID = value.ToInt(); break;
                    case "CreateTime": _CreateTime = value.ToDateTime(); break;
                    case "CreateIP": _CreateIP = Convert.ToString(value); break;
                    default: base[name] = value; break;
                }
            }
        }
        #endregion

        #region 字段名
        /// <summary>取得配置历史字段信息的快捷方式</summary>
        public partial class _
        {
            /// <summary>编号</summary>
            public static readonly Field Id = FindByName("Id");

            /// <summary>配置</summary>
            public static readonly Field ConfigId = FindByName("ConfigId");

            /// <summary>操作</summary>
            public static readonly Field Action = FindByName("Action");

            /// <summary>字段。变更的字段名</summary>
            public static readonly Field Field = FindByName("Field");

            /// <summary>数值。变更前数值</summary>
            public static readonly Field Value = FindByName("Value");

            /// <summary>版本。变更前版本</summary>
            public static readonly Field Version = FindByName("Version");

            /// <summary>创建者</summary>
            public static readonly Field CreateUserID = FindByName("CreateUserID");

            /// <summary>创建时间</summary>
            public static readonly Field CreateTime = FindByName("CreateTime");

            /// <summary>创建地址</summary>
            public static readonly Field CreateIP = FindByName("CreateIP");

            static Field FindByName(String name) => Meta.Table.FindByName(name);
        }

        /// <summary>取得配置历史字段名称的快捷方式</summary>
        public partial class __
        {
            /// <summary>编号</summary>
            public const String Id = "Id";

            /// <summary>配置</summary>
            public const String ConfigId = "ConfigId";

            /// <summary>操作</summary>
            public const String Action = "Action";

            /// <summary>字段。变更的字段名</summary>
            public const String Field = "Field";

            /// <summary>数值。变更前数值</summary>
            public const String Value = "Value";

            /// <summary>版本。变更前版本</summary>
            public const String Version = "Version";

            /// <summary>创建者</summary>
            public const String CreateUserID = "CreateUserID";

            /// <summary>创建时间</summary>
            public const String CreateTime = "CreateTime";

            /// <summary>创建地址</summary>
            public const String CreateIP = "CreateIP";
        }
        #endregion
    }
}