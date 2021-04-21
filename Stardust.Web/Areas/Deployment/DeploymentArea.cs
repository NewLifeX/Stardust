using System;
using System.ComponentModel;
using NewLife;
using NewLife.Cube;

namespace Stardust.Web.Areas.Deployment
{
    [DisplayName("发布中心")]
    public class DeploymentArea : AreaBase
    {
        public DeploymentArea() : base(nameof(DeploymentArea).TrimEnd("Area")) { }

        static DeploymentArea() => RegisterArea<DeploymentArea>();
    }
}