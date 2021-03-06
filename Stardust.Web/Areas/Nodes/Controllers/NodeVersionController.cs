﻿using System;
using System.ComponentModel;
using NewLife.Cube;
using Stardust.Data.Nodes;
using XCode;
using XCode.Membership;

namespace Stardust.Web.Areas.Nodes.Controllers
{
    [NodesArea]
    public class NodeVersionController : EntityController<NodeVersion>
    {
        static NodeVersionController()
        {
            MenuOrder = 89;

            {
                var df = ListFields.AddDataField("Log", "CreateUserID");
                df.DisplayName = "修改日志";
                df.Header = "修改日志";
                df.Url = "/Admin/Log?category=节点版本&linkId={ID}";
            }
        }

        protected override Boolean Valid(NodeVersion entity, DataObjectMethodType type, Boolean post)
        {
            if (!post) return base.Valid(entity, type, post);

            // 必须提前写修改日志，否则修改后脏数据失效，保存的日志为空
            if (type == DataObjectMethodType.Update && (entity as IEntity).HasDirty)
                LogProvider.Provider.WriteLog(type + "", entity);

            var err = "";
            try
            {
                return base.Valid(entity, type, post);
            }
            catch (Exception ex)
            {
                err = ex.Message;
                throw;
            }
            finally
            {
                if (type != DataObjectMethodType.Update) LogProvider.Provider.WriteLog(type + "", entity, err);
            }
        }
    }
}