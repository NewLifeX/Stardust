using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using XCode;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace Stardust.Data.Deployment
{
    /// <summary>部署节点。应用和节点服务器的依赖关系</summary>
    [Serializable]
    [DataObject]
    [Description("部署节点。应用和节点服务器的依赖关系")]
    [BindIndex("IX_AppDeployNode_AppId", false, "AppId")]
    [BindIndex("IX_AppDeployNode_DeployId", false, "DeployId")]
    [BindIndex("IX_AppDeployNode_NodeId", false, "NodeId")]
    [BindTable("AppDeployNode", Description = "部署节点。应用和节点服务器的依赖关系", ConnName = "Stardust", DbType = DatabaseType.None)]
    public partial class AppDeployNode
    {
        #region 属性
        private Int32 _Id;
        /// <summary>编号</summary>
        [DisplayName("编号")]
        [Description("编号")]
        [DataObjectField(true, true, false, 0)]
        [BindColumn("Id", "编号", "")]
        public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

        private Int32 _AppId;
        /// <summary>应用。原始应用</summary>
        [DisplayName("应用")]
        [Description("应用。原始应用")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("AppId", "应用。原始应用", "")]
        public Int32 AppId { get => _AppId; set { if (OnPropertyChanging("AppId", value)) { _AppId = value; OnPropertyChanged("AppId"); } } }

        private Int32 _DeployId;
        /// <summary>部署集。应用部署集</summary>
        [DisplayName("部署集")]
        [Description("部署集。应用部署集")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("DeployId", "部署集。应用部署集", "")]
        public Int32 DeployId { get => _DeployId; set { if (OnPropertyChanging("DeployId", value)) { _DeployId = value; OnPropertyChanged("DeployId"); } } }

        private Int32 _NodeId;
        /// <summary>节点。节点服务器</summary>
        [DisplayName("节点")]
        [Description("节点。节点服务器")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("NodeId", "节点。节点服务器", "")]
        public Int32 NodeId { get => _NodeId; set { if (OnPropertyChanging("NodeId", value)) { _NodeId = value; OnPropertyChanged("NodeId"); } } }

        private Int32 _Sort;
        /// <summary>顺序。较大在前</summary>
        [DisplayName("顺序")]
        [Description("顺序。较大在前")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("Sort", "顺序。较大在前", "")]
        public Int32 Sort { get => _Sort; set { if (OnPropertyChanging("Sort", value)) { _Sort = value; OnPropertyChanged("Sort"); } } }

        private Int32 _CreateUserId;
        /// <summary>创建人</summary>
        [DisplayName("创建人")]
        [Description("创建人")]
        [DataObjectField(false, false, false, 0)]
        [BindColumn("CreateUserId", "创建人", "")]
        public Int32 CreateUserId { get => _CreateUserId; set { if (OnPropertyChanging("CreateUserId", value)) { _CreateUserId = value; OnPropertyChanged("CreateUserId"); } } }

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
                    case "AppId": return _AppId;
                    case "DeployId": return _DeployId;
                    case "NodeId": return _NodeId;
                    case "Sort": return _Sort;
                    case "CreateUserId": return _CreateUserId;
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
                    case "AppId": _AppId = value.ToInt(); break;
                    case "DeployId": _DeployId = value.ToInt(); break;
                    case "NodeId": _NodeId = value.ToInt(); break;
                    case "Sort": _Sort = value.ToInt(); break;
                    case "CreateUserId": _CreateUserId = value.ToInt(); break;
                    case "CreateTime": _CreateTime = value.ToDateTime(); break;
                    case "CreateIP": _CreateIP = Convert.ToString(value); break;
                    default: base[name] = value; break;
                }
            }
        }
        #endregion

        #region 字段名
        /// <summary>取得部署节点字段信息的快捷方式</summary>
        public partial class _
        {
            /// <summary>编号</summary>
            public static readonly Field Id = FindByName("Id");

            /// <summary>应用。原始应用</summary>
            public static readonly Field AppId = FindByName("AppId");

            /// <summary>部署集。应用部署集</summary>
            public static readonly Field DeployId = FindByName("DeployId");

            /// <summary>节点。节点服务器</summary>
            public static readonly Field NodeId = FindByName("NodeId");

            /// <summary>顺序。较大在前</summary>
            public static readonly Field Sort = FindByName("Sort");

            /// <summary>创建人</summary>
            public static readonly Field CreateUserId = FindByName("CreateUserId");

            /// <summary>创建时间</summary>
            public static readonly Field CreateTime = FindByName("CreateTime");

            /// <summary>创建地址</summary>
            public static readonly Field CreateIP = FindByName("CreateIP");

            static Field FindByName(String name) => Meta.Table.FindByName(name);
        }

        /// <summary>取得部署节点字段名称的快捷方式</summary>
        public partial class __
        {
            /// <summary>编号</summary>
            public const String Id = "Id";

            /// <summary>应用。原始应用</summary>
            public const String AppId = "AppId";

            /// <summary>部署集。应用部署集</summary>
            public const String DeployId = "DeployId";

            /// <summary>节点。节点服务器</summary>
            public const String NodeId = "NodeId";

            /// <summary>顺序。较大在前</summary>
            public const String Sort = "Sort";

            /// <summary>创建人</summary>
            public const String CreateUserId = "CreateUserId";

            /// <summary>创建时间</summary>
            public const String CreateTime = "CreateTime";

            /// <summary>创建地址</summary>
            public const String CreateIP = "CreateIP";
        }
        #endregion
    }
}