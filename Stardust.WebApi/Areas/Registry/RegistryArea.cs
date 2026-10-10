using System.ComponentModel;
using NewLife;
using NewLife.Cube;
using Stardust.Web.Areas.Deployment;
using XCode;

namespace Stardust.Web.Areas.Registry;

/// <summary>应用中心区域。管理应用、服务、消费关系与性能数据</summary>
[DisplayName("应用中心")]
[Menu(777, true, LastUpdate = "20240727")]
public class RegistryArea : AreaBase
{
    public RegistryArea() : base(nameof(RegistryArea).TrimSuffix("Area")) { }

    static RegistryArea() => RegisterArea<RegistryArea>();
}

[RegistryArea]
public class RegistryEntityController<T> : WebEntityController<T> where T : Entity<T>, new()
{
}
