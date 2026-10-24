using System.Globalization;
using System.Runtime.CompilerServices;

namespace ServerTest;

/// <summary>测试程序集的进程级初始化</summary>
static class TestAssembly
{
    /// <summary>把默认界面语言固定为 zh-Hans</summary>
    /// <remarks>
    /// 词表以英文为源文、中文为 zh-Hans 译文，本程序集多数响应断言的是中文文案。
    /// 界面语言在未指定请求语言时取宿主语言，en-US 机器（CI 的 ubuntu/windows-latest）会返回英文而失败，
    /// 因此这里在程序集加载时固定为 zh-Hans，保证跨平台结果一致。
    /// </remarks>
    [ModuleInitializer]
    internal static void Init()
    {
        var culture = new CultureInfo("zh-Hans");
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
