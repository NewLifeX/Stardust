namespace Stardust;

/// <summary>星尘工厂启动选项</summary>
/// <remarks>
/// 包含连接信息（服务端地址、应用标识、密钥）、启动方式与客户端行为。
/// 为空的值沿用原有补全链：命令行、环境变量、appsettings、本地 StarAgent、star.config。
/// 传入工厂后会复制一份，工厂补全后的实际值可从 <see cref="StarFactory.Options"/> 查看。
/// </remarks>
public class StarOptions
{
    /// <summary>服务端地址。为空时沿用原有补全链</summary>
    public String? Server { get; set; }

    /// <summary>应用标识。为空时沿用原有补全链，最终回退入口程序集名称</summary>
    public String? AppId { get; set; }

    /// <summary>应用密钥。为空时沿用原有补全链</summary>
    public String? Secret { get; set; }

    /// <summary>应用类型。客户端声明的应用形态，服务端仅在管理员未设置时采用</summary>
    public Models.AppKinds Kind { get; set; }

    /// <summary>延迟启动。为 true 时不立即登录服务端，由宿主在就绪后调用 <see cref="StarFactory.Open"/></summary>
    public Boolean DelayStart { get; set; }

    /// <summary>接受星尘平台下发的默认语言。默认true</summary>
    /// <remarks>
    /// 控制台应用与后台服务没有面向用户的界面，跟随平台语言便于统一日志与提示；
    /// 桌面应用的语言属于本机用户，设为 false 后忽略平台下发的默认语言，使用系统或本地配置。
    /// 服务端对 Desktop/Mobile/Frontend 类型默认不下发语言；本开关用于节点通道或服务端尚未识别类型时兜底。
    /// 登录与心跳下发的语言会保存到核心配置 <see cref="NewLife.Setting.DefaultLanguage"/>，匿名 /api 的语言只影响当前进程。
    /// </remarks>
    public Boolean AcceptPlatformLanguage { get; set; } = true;

    /// <summary>复制选项</summary>
    /// <returns>复制后的选项</returns>
    internal StarOptions Clone() => (StarOptions)MemberwiseClone();
}
