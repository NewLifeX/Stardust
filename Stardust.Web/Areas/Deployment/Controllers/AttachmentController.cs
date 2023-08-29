using Stardust.Data.Deployment;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;
using NewLife.Cube.ViewModels;
using NewLife.Web;
using XCode.Membership;

namespace Stardust.Web.Areas.Deployment.Controllers
{
    /// <summary>附件。用于记录各系统模块使用的文件，可以是Local/NAS/OSS等</summary>
    [Menu(10, true, Icon = "fa-table")]
    [DeploymentArea]
    public class AttachmentController : EntityController<Attachment>
    {
        static AttachmentController()
        {
            //LogOnChange = true;

            //ListFields.RemoveField("Id", "Creator");
            ListFields.RemoveCreateField();

            //{
            //    var df = ListFields.GetField("Code") as ListField;
            //    df.Url = "?code={Code}";
            //}
            //{
            //    var df = ListFields.AddListField("devices", null, "Onlines");
            //    df.DisplayName = "查看设备";
            //    df.Url = "Device?groupId={Id}";
            //    df.DataVisible = e => (e as Attachment).Devices > 0;
            //}
            //{
            //    var df = ListFields.GetField("Kind") as ListField;
            //    df.GetValue = e => ((Int32)(e as Attachment).Kind).ToString("X4");
            //}
            //ListFields.TraceUrl("TraceId");
        }

        /// <summary>高级搜索。列表页查询、导出Excel、导出Json、分享页等使用</summary>
        /// <param name="p">分页器。包含分页排序参数，以及Http请求参数</param>
        /// <returns></returns>
        protected override IEnumerable<Attachment> Search(Pager p)
        {
            //var deviceId = p["deviceId"].ToInt(-1);

            var start = p["dtStart"].ToDateTime();
            var end = p["dtEnd"].ToDateTime();

            return Attachment.Search(start, end, p["Q"], p);
        }
    }
}