namespace Stardust.Models;

/// <summary>浏览器埋点令牌申请模型</summary>
public class BrowserTokenModel
{
    /// <summary>应用标识。必须是已在注册中心启用的应用</summary>
    public String? AppId { get; set; }

    /// <summary>页面版本号。可选，便于区分同一应用的多个前端版本</summary>
    public String? Release { get; set; }
}
