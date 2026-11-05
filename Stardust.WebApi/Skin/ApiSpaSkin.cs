using System.Reflection;
using Microsoft.Extensions.FileProviders;
using NewLife;
using NewLife.Cube;
using NewLife.Cube.Extensions;

namespace Stardust.Web;

/// <summary>星尘控制台前端皮肤。构建产物编译期嵌入本程序集（wwwroot 惯例），由魔方皮肤系统自动发现并托管</summary>
/// <remarks>
/// 启用链路：`pnpm build:all` 把 vite 产物同步到本工程 wwwroot → csproj 嵌入 wwwroot\**\* →
/// 本皮肤返回 CubeEmbeddedFileProvider(assembly, "Stardust.Web.wwwroot") →
/// UseCube 内 CubeSkinService.RegisterSkins 经 ICubeSkin 插件扫描自动注册 UseStaticFiles 与回退
/// （回退入口页 no-cache；默认 {*path:nonfile} 约束：无扩展名的未匹配路径回退 index.html，
/// 带扩展名的缺失资源与 POST 等方法保持 404/405，机器语义不受影响）；
/// /api 未匹配请求由皮肤回退之前的 MapFallback("/api/{**path}", 404) 保护，接口 404 不会被回退成 HTML。
/// 与官方皮肤包（NewLife.Cube.Vue=魔方自己的管理后台）的差异：本宿主为解耦 API 宿主，前端为星尘控制台，
/// 前后端必然同域，故 config.production.ts 直接 baseUrl=''（无 BUILD_ 占位符，无需部署载体替换）。
/// </remarks>
internal sealed class ApiSpaSkin : ICubeSkin
{
    /// <summary>皮肤标识。本宿主只注册这一套皮肤，未配置 SpaSkin 时按默认规则唯一选中</summary>
    public String Name => "Stardust";

    /// <summary>显示名称</summary>
    public String DisplayName => "星尘控制台 (Vue3 + Element Plus)";

    /// <summary>创建静态资源文件提供者。前端编译产物编译时嵌入程序集，无需物理文件</summary>
    /// <returns></returns>
    public IFileProvider CreateFileProvider()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return new CubeEmbeddedFileProvider(assembly, "Stardust.Web.wwwroot");
    }
}
