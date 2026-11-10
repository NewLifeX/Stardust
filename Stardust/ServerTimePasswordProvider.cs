using NewLife;
using NewLife.Security;

namespace Stardust;

/// <summary>服务端时间密码提供者</summary>
/// <remarks>
/// <see cref="SaltPasswordProvider"/> 用本机时钟生成盐值：本机时间与服务端相差超过 SaltTime 时，
/// 服务端会判定盐值过期并拒绝鉴权（日志表现为“密钥校验失败”后反复尝试动态注册）。
/// 这里改用与服务端对齐的时间（客户端 GetNow，由登录/心跳响应的 ServerTime 维护）生成盐值，
/// 让时钟偏差的机器也能正常登录，而不需要修改系统时间——改系统时钟是特权操作，容器内还会影响宿主机。
/// <para>服务端校验逻辑不变，仍按盐值时间判断是否落在 SaltTime 窗口内；服务端时间由匿名接口 /api 提前探测。</para>
/// </remarks>
public class ServerTimePasswordProvider : SaltPasswordProvider
{
    /// <summary>获取与服务端对齐的当前时间。为空时退回本机时间</summary>
    public Func<DateTime>? Now { get; set; }

    /// <summary>实例化</summary>
    /// <param name="now">获取与服务端对齐的当前时间</param>
    public ServerTimePasswordProvider(Func<DateTime> now) => Now = now;

    /// <summary>创建盐值。使用与服务端对齐的时间，避免本机时钟偏差导致盐值过期</summary>
    /// <returns>Unix 秒字符串</returns>
    protected override String CreateSalt()
    {
        // SaltTime 为 0 时盐值是随机字符串，与时间无关，保持基类行为
        if (SaltTime <= 0) return base.CreateSalt();

        var now = Now?.Invoke() ?? DateTime.Now;

        return now.ToUniversalTime().ToInt().ToString();
    }
}
