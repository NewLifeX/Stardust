using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Data;
using NewLife.Web;
using Stardust.Data.Nodes;
using XCode;
using XCode.Membership;
using static Stardust.Data.Nodes.Node;

namespace Stardust.Web.Areas.Nodes.Controllers
{
    [NodesArea]
    public class NodeController : EntityController<Node>
    {
        static NodeController() => MenuOrder = 90;

        protected override IEnumerable<Node> Search(Pager p)
        {
            var nodeId = p["Id"].ToInt(-1);
            if (nodeId > 0)
            {
                var node = Node.FindByID(nodeId);
                if (node != null) return new[] { node };
            }

            var rids = p["areaId"].SplitAsInt("/");
            var provinceId = rids.Length > 0 ? rids[0] : -1;
            var cityId = rids.Length > 1 ? rids[1] : -1;

            var category = p["category"];
            var version = p["version"];
            var enable = p["enable"]?.ToBoolean();

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return Node.Search(provinceId, cityId, category, version, enable, start, end, p["Q"], p);
        }

        /// <summary>搜索</summary>
        /// <param name="provinceId"></param>
        /// <param name="cityId"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public ActionResult NodeSearch(Int32 provinceId = -1, Int32 cityId = -1, String key = null)
        {
            var page = new PageParameter { PageSize = 20 };

            // 默认排序
            if (page.Sort.IsNullOrEmpty()) page.Sort = _.Name;

            var list = Node.Search(provinceId, cityId, null, null, null, DateTime.MinValue, DateTime.MinValue, key, page);

            return Json(0, null, list.Select(e => new
            {
                e.ID,
                e.Code,
                e.Name,
                e.Category,
            }).ToArray());
        }

        public ActionResult Trace(Int32 id)
        {
            var node = Node.FindByID(id);
            if (node != null)
            {
                NodeCommand.Add(node, "截屏");
                NodeCommand.Add(node, "抓日志");
            }

            return RedirectToAction("Index");
        }

        protected override Boolean Valid(Node entity, DataObjectMethodType type, Boolean post)
        {
            if (!post) return base.Valid(entity, type, post);

            var act = type switch
            {
                DataObjectMethodType.Update => "修改",
                DataObjectMethodType.Insert => "添加",
                DataObjectMethodType.Delete => "删除",
                _ => type + "",
            };

            // 必须提前写修改日志，否则修改后脏数据失效，保存的日志为空
            if (type == DataObjectMethodType.Update && (entity as IEntity).HasDirty)
                LogProvider.Provider.WriteLog(act, entity);

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
                LogProvider.Provider.WriteLog(act, entity, err);
            }
        }
    }
}