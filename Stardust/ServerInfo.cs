namespace Stardust;

/// <summary>服务端信息。登录前从匿名接口 /api 取得</summary>
public class ServerInfo
{
    /// <summary>服务端 UTC 毫秒时间戳。用于计算客户端到服务端的时间差</summary>
    public Int64 ServerTime { get; set; }

    /// <summary>平台默认语言。为空表示平台未配置</summary>
    public String? DefaultLanguage { get; set; }
}