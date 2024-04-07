using System.ComponentModel;
using NewLife;
using NewLife.Cube;

namespace Stardust.Web.Areas.Deployment;

[DisplayName("发布中心")]
[Menu(555, true, LastUpdate = "20240407")]
public class DeploymentArea : AreaBase
{
    public DeploymentArea() : base(nameof(DeploymentArea).TrimEnd("Area")) { }

    static DeploymentArea() => RegisterArea<DeploymentArea>();
}