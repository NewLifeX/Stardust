namespace Stardust.Web.Mcp;

/// <summary>资源Provider接口。为get_resource工具实现6类资源的详情查询</summary>
public interface IResourceProvider
{
    /// <summary>资源类型（project/node/app/deploy/pipeline/service）</summary>
    String ResourceType { get; }

    /// <summary>按ID获取资源详情</summary>
    /// <param name="id">资源ID</param>
    /// <returns>资源详情对象，null表示未找到</returns>
    Task<Object> GetAsync(Int32 id);
}
