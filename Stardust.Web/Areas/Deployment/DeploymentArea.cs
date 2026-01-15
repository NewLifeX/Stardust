using System.ComponentModel;
using NewLife;
using NewLife.Cube;
using XCode;

namespace Stardust.Web.Areas.Deployment;

[DisplayName("发布中心")]
[Menu(555, true, LastUpdate = "20260115")]
public class DeploymentArea : AreaBase
{
    public DeploymentArea() : base(nameof(DeploymentArea).TrimEnd("Area")) { }

    static DeploymentArea() => RegisterArea<DeploymentArea>();
}

[DeploymentArea]
public class DeploymentEntityController<T> : WebEntityController<T> where T : Entity<T>, new()
{
}