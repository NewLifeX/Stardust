using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
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
        private readonly StarFactory _starFactory;

        static NodeController()
        {
            MenuOrder = 90;

            //{
            //    var df = ListFields.AddDataField("Log", "UpdateTime");
            //    df.DisplayName = "修改日志";
            //    df.Header = "修改日志";
            //    df.Url = "/Admin/Log?category=节点&linkId={Id}";
            //}
        }

        public NodeController(StarFactory starFactory) => _starFactory = starFactory;

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
        /// <param name="category"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public ActionResult NodeSearch(String category, String key = null)
        {
            var page = new PageParameter { PageSize = 20 };

            // 默认排序
            if (page.Sort.IsNullOrEmpty()) page.Sort = _.Name;

            var list = Node.SearchByCategory(category, true, key, page);

            return Json(0, null, list.Select(e => new
            {
                e.ID,
                e.Code,
                e.Name,
                e.Category,
            }).ToArray());
        }

        public async Task<ActionResult> Trace(Int32 id)
        {
            var node = Node.FindByID(id);
            if (node != null)
            {
                //NodeCommand.Add(node, "截屏");
                //NodeCommand.Add(node, "抓日志");

                await _starFactory.SendNodeCommand(node.Code, "截屏");
                await _starFactory.SendNodeCommand(node.Code, "抓日志");
            }

            return RedirectToAction("Index");
        }

        protected override Boolean Valid(Node entity, DataObjectMethodType type, Boolean post)
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

        [EntityAuthorize(PermissionFlags.Update)]
        public ActionResult SetAlarm(Boolean enable = true)
        {
            foreach (var item in SelectKeys)
            {
                var dt = Node.FindByID(item.ToInt());
                if (dt != null)
                {
                    dt.AlarmOnOffline = enable;
                    dt.Save();
                }
            }

            return JsonRefresh("操作成功！");
        }
        [EntityAuthorize(PermissionFlags.Update)]
        public ActionResult ResetAlarm(Int32 alarmRate = 0)
        {
            foreach (var item in SelectKeys)
            {
                var dt = Node.FindByID(item.ToInt());
                if (dt != null)
                {
                    dt.AlarmCpuRate = alarmRate;
                    dt.AlarmMemoryRate = alarmRate;
                    dt.AlarmDiskRate = alarmRate;
                    dt.Save();
                }
            }

            return JsonRefresh("操作成功！");
        }
    }
}