using NewLife.Cube;
using NewLife.Cube.ViewModels;
using XCode;

namespace Stardust.Web;

/// <summary>
/// WebApi 兼容 shim：MVC 版 NewLife.Cube（NewLife.Cube.Core）提供 StarHelper.TraceUrl 扩展
/// （依赖 StarUrlExtend / 链路追踪 Url），而 API 版 NewLife.Cube 不含该扩展。
/// 这里提供最小降级实现，仅定位/返回对应字段，使从 Stardust.Web 复制的控制器无需逐行删除
/// TraceUrl() 调用即可编译通过。纯 API 场景不需要链路追踪跳转 Url。
/// </summary>
public static class WebApiCompatExtensions
{
    public static DataField? TraceUrl(this FieldCollection fields) => fields.GetField("TraceId");

    public static DataField? TraceUrl(this FieldCollection fields, String fieldName = "TraceId", String display = "追踪", Boolean newWindow = false) =>
        fields.GetField(fieldName);
}
