using NewLife.Cube;
using NewLife.Cube.ViewModels;
using Stardust.Data;
using XCode.Membership;

namespace Stardust.Web.Areas.Registry.Controllers
{
    [RegistryArea]
    [Menu(80)]
    public class ServiceInfoController : EntityController<Service>
    {
        static ServiceInfoController()
        {
            LogOnChange = true;

            ListFields.RemoveField("Secret", "HealthAddress");
            ListFields.RemoveCreateField()
                .RemoveUpdateField()
                .RemoveRemarkField();

            {
                var df = ListFields.GetField("Providers") as ListField;
                df.DisplayName = "{Providers}";
                df.Url = "AppService?serviceId={Id}";
            }
            {
                var df = ListFields.GetField("Consumers") as ListField;
                df.DisplayName = "{Consumers}";
                df.Url = "AppConsume?serviceId={Id}";
            }
        }
    }
}