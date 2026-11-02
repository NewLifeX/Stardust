using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using NewLife;

namespace Stardust.McpApiTests;

/// <summary>通过真实 WebApi(6681) 平台 API 创建 MCP 令牌（与浏览器 POST /Platform/McpToken 同路径）。</summary>
internal static class McpPlatformApiHelper
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>管理员登录，返回 JWT；失败返回 null（测试应 Skip 或 Assert）。</summary>
    public static async Task<String?> TryLoginAdminAsync()
    {
        using var client = new HttpClient { BaseAddress = new Uri(McpTestDataInitializer.BaseUrl) };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var body = new StringContent(
            JsonSerializer.Serialize(new { username = McpTestDataInitializer.AdminUser, password = McpTestDataInitializer.AdminPassword }),
            Encoding.UTF8,
            "application/json");
        var resp = await client.PostAsync("/Auth/Login", body);
        var text = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) return null;
        return ParseAccessToken(text);
    }

    /// <summary>POST /api/Platform/McpToken，走 McpTokenController.OnInsert（含默认 scope）。</summary>
    public static async Task<(Boolean Ok, String Message, Int32 Id, String Token)> CreateMcpTokenAsync(
        String accessToken,
        String name,
        Int32 userId,
        Int32 projectId,
        String scopesCsv = "")
    {
        using var client = new HttpClient { BaseAddress = new Uri(McpTestDataInitializer.BaseUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var payload = new
        {
            name,
            enable = true,
            userId,
            projectId,
            scopes = scopesCsv,
            remark = "McpApiTests create-flow",
        };
        var content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json");
        var resp = await client.PostAsync("/api/Platform/McpToken", content);
        var text = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            return (false, $"HTTP {(Int32)resp.StatusCode}: {text.Cut(400)}", 0, "");

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        if (root.TryGetProperty("code", out var codeEl) && codeEl.GetInt32() != 0)
            return (false, root.TryGetProperty("message", out var msgEl) ? msgEl.GetString() ?? "code!=0" : "code!=0", 0, "");

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            return (false, "响应缺少 data", 0, "");

        var id = data.TryGetProperty("id", out var idEl) ? idEl.GetInt32()
            : data.TryGetProperty("Id", out var idEl2) ? idEl2.GetInt32() : 0;
        var token = data.TryGetProperty("token", out var tokEl) ? tokEl.GetString() ?? ""
            : data.TryGetProperty("Token", out var tokEl2) ? tokEl2.GetString() ?? "" : "";
        if (id <= 0 || token.IsNullOrEmpty())
            return (false, $"data 缺 Id/Token：{text.Cut(400)}", id, token);

        return (true, "ok", id, token);
    }

    /// <summary>POST /api/Platform/ProjectUser，把用户加入项目（Enable=true）。</summary>
    public static async Task<(Boolean Ok, String Message, Int32 Id)> CreateProjectUserAsync(
        String accessToken, Int32 projectId, Int32 userId)
    {
        using var client = new HttpClient { BaseAddress = new Uri(McpTestDataInitializer.BaseUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var payload = new { projectId, userId, enable = true, remark = "McpApiTests member-flow" };
        var content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json");
        var resp = await client.PostAsync("/api/Platform/ProjectUser", content);
        var text = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            return (false, $"HTTP {(Int32)resp.StatusCode}: {text.Cut(400)}", 0);
        return ParseEntityId(text);
    }

    public static async Task DeleteProjectUserAsync(String accessToken, Int32 id)
    {
        if (id <= 0) return;
        using var client = new HttpClient { BaseAddress = new Uri(McpTestDataInitializer.BaseUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        // Cube 6.15：DELETE 的 id 在查询串。路径 /{id} 会被当成 action，现网返回 405。
        await client.DeleteAsync($"/api/Platform/ProjectUser?id={id}");
    }

    /// <summary>GET /api/Platform/ProjectUser?projectId= ，返回行 Id。用于确认 Search 按项目过滤。</summary>
    public static async Task<(Boolean Ok, String Message, List<Int32> Ids)> ListProjectUserIdsAsync(
        String accessToken, Int32 projectId)
    {
        using var client = new HttpClient { BaseAddress = new Uri(McpTestDataInitializer.BaseUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var resp = await client.GetAsync($"/api/Platform/ProjectUser?projectId={projectId}&PageSize=500");
        var text = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            return (false, $"HTTP {(Int32)resp.StatusCode}: {text.Cut(400)}", []);
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        if (root.TryGetProperty("code", out var codeEl) && codeEl.ValueKind == JsonValueKind.Number && codeEl.GetInt32() != 0)
            return (false, root.TryGetProperty("message", out var msgEl) ? msgEl.GetString() ?? "code!=0" : "code!=0", []);
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return (false, "响应 data 不是数组", []);
        var ids = new List<Int32>();
        foreach (var row in data.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object) continue;
            if (row.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out var id)) ids.Add(id);
            else if (row.TryGetProperty("Id", out var idEl2) && idEl2.TryGetInt32(out var id2)) ids.Add(id2);
        }
        return (true, "ok", ids);
    }

    private static (Boolean Ok, String Message, Int32 Id) ParseEntityId(String text)
    {
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        if (root.TryGetProperty("code", out var codeEl) && codeEl.ValueKind == JsonValueKind.Number && codeEl.GetInt32() != 0)
            return (false, root.TryGetProperty("message", out var msgEl) ? msgEl.GetString() ?? "code!=0" : "code!=0", 0);
        if (!root.TryGetProperty("data", out var data))
            return (false, "响应缺少 data", 0);
        if (data.ValueKind == JsonValueKind.Number && data.TryGetInt32(out var bare))
            return (true, "ok", bare);
        if (data.ValueKind != JsonValueKind.Object)
            return (false, "响应 data 不是实体", 0);
        var id = data.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out var i1) ? i1
            : data.TryGetProperty("Id", out var idEl2) && idEl2.TryGetInt32(out var i2) ? i2 : 0;
        if (id <= 0) return (false, $"data 缺 Id：{text.Cut(400)}", 0);
        return (true, "ok", id);
    }

    public static async Task DeleteMcpTokenAsync(String accessToken, Int32 id)
    {
        if (id <= 0) return;
        using var client = new HttpClient { BaseAddress = new Uri(McpTestDataInitializer.BaseUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        await client.DeleteAsync($"/api/Platform/McpToken/{id}");
    }

    private static String? ParseAccessToken(String json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            return null;
        if (data.TryGetProperty("access_token", out var at1) && !String.IsNullOrEmpty(at1.GetString())) return at1.GetString();
        if (data.TryGetProperty("AccessToken", out var at2) && !String.IsNullOrEmpty(at2.GetString())) return at2.GetString();
        return null;
    }
}
