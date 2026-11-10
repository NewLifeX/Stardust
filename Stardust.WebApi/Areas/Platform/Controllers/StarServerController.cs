using System.ComponentModel;
using Microsoft.AspNetCore.Mvc.Filters;
using NewLife.Cube;
using Stardust.Server;

namespace Stardust.Web.Areas.Platform.Controllers;

/// <summary>平台设置控制器</summary>
[Menu(10, true)]
[DisplayName("平台设置")]
[PlatformArea]
public class StarServerController : ConfigController<StarServerSetting>
{
    /// <summary>执行前。默认语言字段使用词表语言下拉框</summary>
    /// <param name="filterContext">操作执行上下文</param>
    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        var df = GetMembers(typeof(StarServerSetting)).FirstOrDefault(e => e.Name == nameof(StarServerSetting.DefaultLanguage));
        if (df != null)
        {
            df.ItemType = "singleSelect";
            df.DataSource = _ => CubeLocalization.GetLanguageOptions();
        }

        base.OnActionExecuting(filterContext);
    }
}
