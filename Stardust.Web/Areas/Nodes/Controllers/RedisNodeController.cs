using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Cube;
using NewLife.Data;
using NewLife.Web;
using Stardust.Data.Nodes;

namespace Stardust.Web.Areas.Nodes.Controllers
{
    [NodesArea]
    public class RedisNodeController : EntityController<RedisNode>
    {
        static RedisNodeController() => MenuOrder = 50;

        protected override IEnumerable<RedisNode> Search(Pager p)
        {
            var nodeId = p["Id"].ToInt(-1);
            if (nodeId > 0)
            {
                var node = RedisNode.FindById(nodeId);
                if (node != null) return new[] { node };
            }

            var category = p["category"];
            var server = p["server"];
            var enable = p["enable"]?.ToBoolean();

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return RedisNode.Search(server, category, enable, start, end, p["Q"], p);
        }

        /// <summary>搜索</summary>
        /// <param name="provinceId"></param>
        /// <param name="cityId"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public ActionResult NodeSearch(String category, String key = null)
        {
            var page = new PageParameter { PageSize = 20 };

            // 默认排序
            if (page.Sort.IsNullOrEmpty()) page.Sort = RedisNode._.Name;

            var list = RedisNode.Search(null, category, true, DateTime.MinValue, DateTime.MinValue, key, page);

            return Json(0, null, list.Select(e => new
            {
                e.Id,
                e.Name,
                e.Server,
                e.Category,
            }).ToArray());
        }
    }
}