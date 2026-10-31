using NewLife.Cube.ViewModels;

namespace NewLife.Cube.Extensions;

/// <summary>星尘列表字段扩展</summary>
public static class StarHelperExtensions
{
    /// <summary>按当前请求语言输出追踪链接文字。立方 TraceUrl 固定使用中文单元格文字且不参与词表翻译</summary>
    /// <param name="fields">字段集合</param>
    /// <param name="fieldName">追踪标识字段名</param>
    /// <returns>追踪链接字段</returns>
    public static ListField? LocalizedTraceUrl(this NewLife.Cube.FieldCollection fields, String fieldName = "TraceId")
    {
        var df = fields.TraceUrl(fieldName);
        if (df == null) return null;

        // 清空固定中文单元格文字，改为按请求语言计算，保留 Url/Title/可见性
        df.Text = null;
        df.GetValue = _ => L.T("追踪"); // ui

        return df;
    }
}
