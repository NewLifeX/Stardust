using System;
using System.Collections.Generic;
using System.IO;
using NewLife;
using NewLife.Cube;
using Stardust.Data.Nodes;
using XCode.Membership;

namespace Stardust.Web.Areas.Nodes.Controllers
{
    [Menu(89)]
    [NodesArea]
    public class NodeVersionController : EntityController<NodeVersion>
    {
        static NodeVersionController()
        {
            LogOnChange = true;

            {
                var df = ListFields.AddListField("Log", "CreateUserID");
                df.DisplayName = "修改日志";
                df.Header = "修改日志";
                df.Url = "/Admin/Log?category=节点版本&linkId={ID}";
            }
        }

        protected override async Task<IList<String>> SaveFiles(NodeVersion entity, String uploadPath = null)
        {
            var rs = await base.SaveFiles(entity, uploadPath);

            // 更新文件哈希
            if (rs.Count > 0 && !entity.Source.IsNullOrEmpty())
            {
                var fi = NewLife.Cube.Setting.Current.UploadPath.CombinePath(entity.Source).AsFile();
                if (fi.Exists)
                {
                    entity.FileHash = fi.ReadBytes().MD5().ToHex();
                }
            }

            return rs;
        }
    }
}