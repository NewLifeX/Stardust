using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Stardust.Data.Monitors
{
    /// <summary>告警历史。记录告警内容</summary>
    [Serializable]
    [DataObject]
    [Description("告警历史。记录告警内容")]
    [BindIndex("IX_AlarmHistory_GroupId_Id", false, "GroupId,Id")]
    [BindTable("AlarmHistory", Description = "告警历史。记录告警内容", ConnName = "StardustData", DbType = DatabaseType.None)]
    public partial class AlarmHistory
    {
        #region 属性
        private Int64 _Id;
        /// <summary>编号</summary>
        [DisplayName("编号")]
        [Description("编号")]
        [DataObjectField(true, false, false, 0)]
        [BindColumn("Id", "编号", "")]
        public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

        private String _Name;
        /// <summary>名称</summary>
        [DisplayName("名称")]
        [Description("名称")]
        [DataObjectField(false, false, false, 50)]
        [BindColumn("Name", "名称", "", Master = true)]
        public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

        private Int32 _GroupId;
        /// <summary>告警组</summary>
        [DisplayName("告警组")]
        [Description("告警组")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("GroupId", "告警组", "")]
        public Int32 GroupId { get => _GroupId; set { if (OnPropertyChanging("GroupId", value)) { _GroupId = value; OnPropertyChanged("GroupId"); } } }

        private String _Category;
        /// <summary>类别。钉钉、企业微信</summary>
        [DisplayName("类别")]
        [Description("类别。钉钉、企业微信")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Category", "类别。钉钉、企业微信", "")]
        public String Category { get => _Category; set { if (OnPropertyChanging("Category", value)) { _Category = value; OnPropertyChanged("Category"); } } }

        private String _Action;
        /// <summary>操作</summary>
        [DisplayName("操作")]
        [Description("操作")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Action", "操作", "")]
        public String Action { get => _Action; set { if (OnPropertyChanging("Action", value)) { _Action = value; OnPropertyChanged("Action"); } } }

        private Boolean _Success;
        /// <summary>成功</summary>
        [DisplayName("成功")]
        [Description("成功")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Success", "成功", "")]
        public Boolean Success { get => _Success; set { if (OnPropertyChanging("Success", value)) { _Success = value; OnPropertyChanged("Success"); } } }

        private String _Content;
        /// <summary>内容</summary>
        [DisplayName("内容")]
        [Description("内容")]
        [DataObjectField(false, false, true, 2000)]
        [BindColumn("Content", "内容", "")]
        public String Content { get => _Content; set { if (OnPropertyChanging("Content", value)) { _Content = value; OnPropertyChanged("Content"); } } }

        private String _Creator;
        /// <summary>创建者。服务端节点</summary>
        [Category("扩展")]
        [DisplayName("创建者")]
        [Description("创建者。服务端节点")]
        [DataObjectField(false, false, true, 50)]
        [BindColumn("Creator", "创建者。服务端节点", "")]
        public String Creator { get => _Creator; set { if (OnPropertyChanging("Creator", value)) { _Creator = value; OnPropertyChanged("Creator"); } } }

        private DateTime _CreateTime;
        /// <summary>创建时间</summary>
        [Category("扩展")]
        [DisplayName("创建时间")]
        [Description("创建时间")]
        [DataObjectField(false, false, true, 0)]
        [BindColumn("CreateTime", "创建时间", "")]
        public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }
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
                    case "Name": return _Name;
                    case "GroupId": return _GroupId;
                    case "Category": return _Category;
                    case "Action": return _Action;
                    case "Success": return _Success;
                    case "Content": return _Content;
                    case "Creator": return _Creator;
                    case "CreateTime": return _CreateTime;
                    default: return base[name];
                }
            }
            set
            {
                switch (name)
                {
                    case "Id": _Id = value.ToLong(); break;
                    case "Name": _Name = Convert.ToString(value); break;
                    case "GroupId": _GroupId = value.ToInt(); break;
                    case "Category": _Category = Convert.ToString(value); break;
                    case "Action": _Action = Convert.ToString(value); break;
                    case "Success": _Success = value.ToBoolean(); break;
                    case "Content": _Content = Convert.ToString(value); break;
                    case "Creator": _Creator = Convert.ToString(value); break;
                    case "CreateTime": _CreateTime = value.ToDateTime(); break;
                    default: base[name] = value; break;
                }
            }
        }
        #endregion

        #region 字段名
        /// <summary>取得告警历史字段信息的快捷方式</summary>
        public partial class _
        {
            /// <summary>编号</summary>
            public static readonly Field Id = FindByName("Id");

            /// <summary>名称</summary>
            public static readonly Field Name = FindByName("Name");

            /// <summary>告警组</summary>
            public static readonly Field GroupId = FindByName("GroupId");

            /// <summary>类别。钉钉、企业微信</summary>
            public static readonly Field Category = FindByName("Category");

            /// <summary>操作</summary>
            public static readonly Field Action = FindByName("Action");

            /// <summary>成功</summary>
            public static readonly Field Success = FindByName("Success");

            /// <summary>内容</summary>
            public static readonly Field Content = FindByName("Content");

            /// <summary>创建者。服务端节点</summary>
            public static readonly Field Creator = FindByName("Creator");

            /// <summary>创建时间</summary>
            public static readonly Field CreateTime = FindByName("CreateTime");

            static Field FindByName(String name) => Meta.Table.FindByName(name);
        }

        /// <summary>取得告警历史字段名称的快捷方式</summary>
        public partial class __
        {
            /// <summary>编号</summary>
            public const String Id = "Id";

            /// <summary>名称</summary>
            public const String Name = "Name";

            /// <summary>告警组</summary>
            public const String GroupId = "GroupId";

            /// <summary>类别。钉钉、企业微信</summary>
            public const String Category = "Category";

            /// <summary>操作</summary>
            public const String Action = "Action";

            /// <summary>成功</summary>
            public const String Success = "Success";

            /// <summary>内容</summary>
            public const String Content = "Content";

            /// <summary>创建者。服务端节点</summary>
            public const String Creator = "Creator";

            /// <summary>创建时间</summary>
            public const String CreateTime = "CreateTime";
        }
        #endregion
    }
}