using NewLife;

namespace Stardust.Server.Services;

/// <summary>下载地址补全与加签。</summary>
/// <remarks>
/// 升级包、引导安装包（运行时包与完整版包）都遵循同一规则：
/// 相对路径按服务端基础地址补全为绝对地址，然后统一附加短时下载令牌，
/// 避免把相对路径或未加签的地址直接下发给节点。
/// </remarks>
public static class DownloadSourceHelper
{
    /// <summary>把资源相对路径补全为绝对地址，并附加下载令牌</summary>
    /// <param name="source">资源地址，可能是相对路径、绝对地址或空</param>
    /// <param name="baseUri">服务端基础地址，如 https://star.example.com:6600</param>
    /// <param name="sign">加签委托，入参为绝对地址，返回带令牌的地址</param>
    /// <returns>补全并加签后的地址；入参为空时原样返回</returns>
    public static String? Absolutize(String? source, String baseUri, Func<String, String> sign)
    {
        if (source.IsNullOrEmpty()) return source;

        if (!source.StartsWithIgnoreCase("http://", "https://"))
            source = new Uri(new Uri(baseUri), source) + "";

        return sign(source);
    }
}
