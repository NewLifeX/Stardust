using System.Net.Http.Json;
using System.Text.Json;
using NewLife.Cube;
using Stardust.Data.Deployment;
using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Areas;

/// <summary>SSL 证书 PFX 密码明文暴露（OBS-3）的集成回归：证书出参四端点组必须裁剪。</summary>
/// <remarks>
/// 覆盖面（对应裁剪红线：控制器出参投影，禁 [IgnoreDataMember]、禁原地改共享缓存实例）：
/// 1. Gateway/GatewayCert 列表 JSON（Search 透传实体序列化，ListFields 只管 meta 不管 JSON 体）；
/// 2. Gateway/GatewayCert 详情 JSON（Detail=FindData 全字段序列化）；
/// 3. 两个证书控制器的 export json（ExportData→SearchData→Search 全字段导出）；
/// 4. Deployment/SslCertificate 列表/详情 JSON（双宿主目前连 ListFields 都未裁）；
/// 5. Deployment/SslCertificate meta.list 字段集（ListFields 补裁后不得含 PfxPassword）；
/// 6. 写路径守护：列表/详情脱敏不得影响 PUT 保存（不带 pfxPassword 不丢原值，带新值可更新）。
/// </remarks>
[Collection(WebApiCollection.Name)]
public class CertPfxPasswordSecurityTests
{
    /// <summary>红基线标记串：唯一到不会出现在其它字段或全局响应里的密码明文。</summary>
    private const String Secret = "pfx-secret-08a3f6";

    /// <summary>新密码写入标记串：验证写路径在裁剪后仍可用。</summary>
    private const String NewSecret = "pfx-secret-beef01";

    private readonly ApiClient _api;
    private readonly WebApiTestFactory _factory;

    public CertPfxPasswordSecurityTests(WebApiTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _api = new(factory, output);
    }

    #region 种子
    /// <summary>插入一条带 PFX 密码明文的证书，返回实体（调用方负责 Delete 清理）。</summary>
    private static SslCertificate SeedCert()
    {
        var cert = new SslCertificate
        {
            Domain = $"obs3-{Guid.NewGuid():N}".Substring(0, 22) + ".example.com",
            Enable = true,
            PfxPassword = Secret,
            Provider = "manual",
            NotAfter = DateTime.Now.AddYears(1),
        };
        cert.Insert();
        return cert;
    }

    /// <summary>按主键直查数据库（Find=直连查询，绕过实体缓存/单对象缓存）。</summary>
    private static SslCertificate FindInDb(Int32 id) => SslCertificate.Find(SslCertificate._.Id == id);
    #endregion

    #region Gateway/GatewayCert
    [Fact]
    public async Task GatewayList_CropsPfxPassword()
    {
        var cert = SeedCert();
        try
        {
            var resp = await _api.GetAsync($"Gateway/GatewayCert?Q={cert.Domain}");
            var raw = await resp.Content.ReadAsStringAsync();

            Assert.True(resp.IsSuccessStatusCode, $"[Gateway 列表] HTTP {(Int32)resp.StatusCode}：{raw}");
            // 行必须真实返回，防止"空列表"造成假绿
            Assert.Contains(cert.Domain, raw);
            Assert.DoesNotContain(Secret, raw);
        }
        finally { cert.Delete(); }
    }

    [Fact]
    public async Task GatewayDetail_CropsPfxPassword()
    {
        var cert = SeedCert();
        try
        {
            var resp = await _api.GetAsync($"Gateway/GatewayCert/{cert.Id}");
            var raw = await resp.Content.ReadAsStringAsync();

            Assert.True(resp.IsSuccessStatusCode, $"[Gateway 详情] HTTP {(Int32)resp.StatusCode}：{raw}");
            Assert.DoesNotContain(Secret, raw);

            // 详情必须仍能查到该行（脱敏不是删行）
            var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var api = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(raw, opts);
            Assert.NotNull(api);
            Assert.True(api!.Code == 0, $"[Gateway 详情] Code={api.Code}：{raw}");
            Assert.Equal(cert.Id, api.Data.GetProperty("id").GetInt32());
        }
        finally { cert.Delete(); }
    }

    [Fact]
    public async Task GatewayExport_CropsPfxPassword()
    {
        var cert = SeedCert();
        try
        {
            var resp = await _api.GetAsync($"Gateway/GatewayCert/export?format=json");
            var raw = await resp.Content.ReadAsStringAsync();

            Assert.True(resp.IsSuccessStatusCode, $"[Gateway 导出] HTTP {(Int32)resp.StatusCode}：{raw}");
            Assert.Contains(cert.Domain, raw);
            Assert.DoesNotContain(Secret, raw);
        }
        finally { cert.Delete(); }
    }

    /// <summary>守护用例：Gateway meta 已裁 PfxPassword（现网既有行为），裁剪实现不得回退。</summary>
    [Fact]
    public async Task GatewayMeta_ExcludesPfxPassword()
    {
        var api = await _api.GetJsonAsync("Gateway/GatewayCert/meta");

        Assert.False(MetaListHasField(api.Data, "PfxPassword"), "Gateway meta.list 不应包含 PfxPassword 字段");
    }

    [Fact]
    public async Task GatewayUpdate_WithoutPfxPassword_KeepsSecret()
    {
        var cert = SeedCert();
        try
        {
            var http = await _factory.CreateAuthenticatedClient();
            var resp = await http.PutAsJsonAsync(ApiClient.NormalizeUrl($"Gateway/GatewayCert/{cert.Id}"),
                new { domain = cert.Domain, enable = true });
            var raw = await resp.Content.ReadAsStringAsync();

            Assert.True(resp.IsSuccessStatusCode, $"[Gateway 更新] HTTP {(Int32)resp.StatusCode}：{raw}");

            var db = FindInDb(cert.Id);
            Assert.NotNull(db);
            Assert.Equal(Secret, db.PfxPassword);
        }
        finally { cert.Delete(); }
    }

    [Fact]
    public async Task GatewayUpdate_WithNewPfxPassword_StoresIt()
    {
        var cert = SeedCert();
        try
        {
            var http = await _factory.CreateAuthenticatedClient();
            var resp = await http.PutAsJsonAsync(ApiClient.NormalizeUrl($"Gateway/GatewayCert/{cert.Id}"),
                new { domain = cert.Domain, enable = true, pfxPassword = NewSecret });
            var raw = await resp.Content.ReadAsStringAsync();

            Assert.True(resp.IsSuccessStatusCode, $"[Gateway 更新密码] HTTP {(Int32)resp.StatusCode}：{raw}");

            var db = FindInDb(cert.Id);
            Assert.NotNull(db);
            Assert.Equal(NewSecret, db.PfxPassword);
        }
        finally { cert.Delete(); }
    }
    #endregion

    #region Deployment/SslCertificate
    [Fact]
    public async Task DeploymentList_CropsPfxPassword()
    {
        var cert = SeedCert();
        try
        {
            var resp = await _api.GetAsync($"Deployment/SslCertificate?Q={cert.Domain}");
            var raw = await resp.Content.ReadAsStringAsync();

            Assert.True(resp.IsSuccessStatusCode, $"[Deployment 列表] HTTP {(Int32)resp.StatusCode}：{raw}");
            Assert.Contains(cert.Domain, raw);
            Assert.DoesNotContain(Secret, raw);
        }
        finally { cert.Delete(); }
    }

    [Fact]
    public async Task DeploymentDetail_CropsPfxPassword()
    {
        var cert = SeedCert();
        try
        {
            var resp = await _api.GetAsync($"Deployment/SslCertificate/{cert.Id}");
            var raw = await resp.Content.ReadAsStringAsync();

            Assert.True(resp.IsSuccessStatusCode, $"[Deployment 详情] HTTP {(Int32)resp.StatusCode}：{raw}");
            Assert.DoesNotContain(Secret, raw);
        }
        finally { cert.Delete(); }
    }

    [Fact]
    public async Task DeploymentExport_CropsPfxPassword()
    {
        var cert = SeedCert();
        try
        {
            var resp = await _api.GetAsync($"Deployment/SslCertificate/export?format=json");
            var raw = await resp.Content.ReadAsStringAsync();

            Assert.True(resp.IsSuccessStatusCode, $"[Deployment 导出] HTTP {(Int32)resp.StatusCode}：{raw}");
            Assert.Contains(cert.Domain, raw);
            Assert.DoesNotContain(Secret, raw);
        }
        finally { cert.Delete(); }
    }

    /// <summary>Deployment meta 目前连 ListFields 都未裁 PfxPassword，补裁后 meta.list 不得含该字段。</summary>
    [Fact]
    public async Task DeploymentMeta_ExcludesPfxPassword()
    {
        var api = await _api.GetJsonAsync("Deployment/SslCertificate/meta");

        Assert.False(MetaListHasField(api.Data, "PfxPassword"), "Deployment meta.list 不应包含 PfxPassword 字段");
    }

    [Fact]
    public async Task DeploymentUpdate_WithoutPfxPassword_KeepsSecret()
    {
        var cert = SeedCert();
        try
        {
            var http = await _factory.CreateAuthenticatedClient();
            var resp = await http.PutAsJsonAsync(ApiClient.NormalizeUrl($"Deployment/SslCertificate/{cert.Id}"),
                new { domain = cert.Domain, enable = true });
            var raw = await resp.Content.ReadAsStringAsync();

            Assert.True(resp.IsSuccessStatusCode, $"[Deployment 更新] HTTP {(Int32)resp.StatusCode}：{raw}");

            var db = FindInDb(cert.Id);
            Assert.NotNull(db);
            Assert.Equal(Secret, db.PfxPassword);
        }
        finally { cert.Delete(); }
    }
    #endregion

    #region 写路径响应投影（OBS-3 扩围 task-68）
    // j-config 04:5x 裁定：写路径响应语义=裁剪（调用方无回显需求；响应体入日志/DevTools=次生泄漏落盘；
    // DELETE 回显存量密码流向非创建者=最实质）。三写路径动作响应不得含 PfxPassword 明文，
    // 且两端点（Deployment/Gateway 同实体同基类）统一裁不留豁口。只裁响应快照，不改持久化。

    /// <summary>提取写路径响应 data.id（用于 POST 创建后的清理）。</summary>
    private static Int32 GetIdFromResponse(String raw, String tag)
    {
        var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var api = JsonSerializer.Deserialize<ApiResponse<JsonElement>>(raw, opts);
        Assert.NotNull(api);
        Assert.True(api!.Code == 0, $"[{tag}] Code={api.Code}：{raw}");
        return api.Data.GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task InsertResponses_CropPfxPassword()
    {
        var http = await _factory.CreateAuthenticatedClient();
        foreach (var url in new[] { "Deployment/SslCertificate", "Gateway/GatewayCert" })
        {
            var resp = await http.PostAsJsonAsync(ApiClient.NormalizeUrl(url),
                new { domain = $"obs3w-{Guid.NewGuid():N}".Substring(0, 22) + ".example.com", enable = true, pfxPassword = Secret });
            var raw = await resp.Content.ReadAsStringAsync();

            Assert.True(resp.IsSuccessStatusCode, $"[{url} 创建] HTTP {(Int32)resp.StatusCode}：{raw}");
            // 行必须真实返回（防"空响应"假绿）
            Assert.True(GetIdFromResponse(raw, url) > 0, $"[{url} 创建] 响应缺少 data.id：{raw}");
            Assert.DoesNotContain(Secret, raw);

            FindInDb(GetIdFromResponse(raw, url))?.Delete();
        }
    }

    [Fact]
    public async Task UpdateResponses_CropPfxPassword()
    {
        var cert = SeedCert();
        try
        {
            var http = await _factory.CreateAuthenticatedClient();
            foreach (var url in new[] { "Deployment/SslCertificate", "Gateway/GatewayCert" })
            {
                var resp = await http.PutAsJsonAsync(ApiClient.NormalizeUrl($"{url}/{cert.Id}"),
                    new { domain = cert.Domain, enable = true, pfxPassword = NewSecret });
                var raw = await resp.Content.ReadAsStringAsync();

                Assert.True(resp.IsSuccessStatusCode, $"[{url} 更新] HTTP {(Int32)resp.StatusCode}：{raw}");
                // 新提交值与旧存量值都不得回显（响应快照裁剪）；写路径功能不破坏（两 URL 各写一次 NewSecret，终值不变）
                Assert.DoesNotContain(NewSecret, raw);
                Assert.DoesNotContain(Secret, raw);
            }

            var db = FindInDb(cert.Id);
            Assert.NotNull(db);
            Assert.Equal(NewSecret, db.PfxPassword);
        }
        finally { cert.Delete(); }
    }

    [Fact]
    public async Task DeleteResponses_CropPfxPassword()
    {
        foreach (var url in new[] { "Deployment/SslCertificate", "Gateway/GatewayCert" })
        {
            var cert = SeedCert();
            try
            {
                var http = await _factory.CreateAuthenticatedClient();
                var resp = await http.DeleteAsync(ApiClient.NormalizeUrl($"{url}/{cert.Id}"));
                var raw = await resp.Content.ReadAsStringAsync();

                Assert.True(resp.IsSuccessStatusCode, $"[{url} 删除] HTTP {(Int32)resp.StatusCode}：{raw}");
                // 存量密码随删除回显=最实质泄漏（流向非创建者），不得出现；行删除语义不变
                Assert.DoesNotContain(Secret, raw);
                Assert.Null(FindInDb(cert.Id));
            }
            finally { FindInDb(cert.Id)?.Delete(); }
        }
    }
    #endregion

    #region meta form 区段守护（OBS-3 task-68 appendix：防「只查 list 区段」类漏网再犯）
    /// <summary>守护用例：两控制器 meta form 区段（编辑表单回显）不得包含 PfxPassword；新增录入（add 区段）保留不动。</summary>
    [Fact]
    public async Task MetaFormSections_ExcludePfxPassword()
    {
        var gw = await _api.GetJsonAsync("Gateway/GatewayCert/meta");
        Assert.False(MetaFormHasField(gw.Data, "PfxPassword"), "Gateway meta.form 不应包含 PfxPassword 字段");

        var dpl = await _api.GetJsonAsync("Deployment/SslCertificate/meta");
        Assert.False(MetaFormHasField(dpl.Data, "PfxPassword"), "Deployment meta.form 不应包含 PfxPassword 字段");
    }
    #endregion

    #region 辅助断言
    /// <summary>检查 meta 响应 list 区段字段名集合是否包含指定字段（大小写不敏感）。</summary>
    private static Boolean MetaListHasField(JsonElement data, String fieldName)
    {
        Assert.Equal(JsonValueKind.Object, data.ValueKind);
        Assert.True(data.TryGetProperty("list", out var fields), $"meta 响应缺少 list 区段：{data}");

        foreach (var field in fields.EnumerateArray())
        {
            foreach (var prop in field.EnumerateObject())
            {
                if (String.Equals(prop.Name, "Name", StringComparison.OrdinalIgnoreCase) &&
                    String.Equals(prop.Value.GetString(), fieldName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    /// <summary>检查 meta 响应 form 区段（编辑表单）字段名集合是否包含指定字段（大小写不敏感）。</summary>
    private static Boolean MetaFormHasField(JsonElement data, String fieldName)
    {
        Assert.Equal(JsonValueKind.Object, data.ValueKind);
        Assert.True(data.TryGetProperty("form", out var fields), $"meta 响应缺少 form 区段：{data}");

        foreach (var field in fields.EnumerateArray())
        {
            foreach (var prop in field.EnumerateObject())
            {
                if (String.Equals(prop.Name, "Name", StringComparison.OrdinalIgnoreCase) &&
                    String.Equals(prop.Value.GetString(), fieldName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }
    #endregion
}
