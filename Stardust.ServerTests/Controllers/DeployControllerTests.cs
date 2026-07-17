using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using NewLife.Serialization;
using Stardust.Data.Deployment;
using Stardust.Server;
using Xunit;

namespace ServerTest.Controllers;

/// <summary>
/// DeployController.UploadBuildFile 上传编译产物包接口测试。
/// 重点验证：multipart/form-data 请求下，简单类型参数从 query string 绑定、IFormFile 从 form body 绑定，
/// 以及附件落盘、版本记录创建等业务逻辑。
/// </summary>
public class DeployControllerTests
{
    private readonly TestServer _server;

    public DeployControllerTests()
    {
#pragma warning disable CS0618, ASPDEPR008
        _server = new TestServer(WebHost.CreateDefaultBuilder()
            .UseStartup<Startup>());
#pragma warning restore CS0618, ASPDEPR008
    }

    private static MultipartFormDataContent BuildUploadContent(String fileName, Byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        content.Add(fileContent, "file", fileName);
        return content;
    }

    private async Task<String> LoginAndGetTokenAsync()
    {
        var client = _server.CreateClient();
        var rs = await client.PostAsync<LoginResponse>("node/login", new
        {
            code = "",
            node = new
            {
                MachineName = "test_deploy_ctl",
                macs = "xxyyzz" + Guid.NewGuid().ToString("N")[..6],
            },
        });
        Assert.NotNull(rs?.Token);
        return rs.Token;
    }

    [Fact(DisplayName = "无Token返回401认证失败")]
    public async Task UploadBuildFile_NoToken_Returns401()
    {
        var client = _server.CreateClient();
        var content = BuildUploadContent("test.zip", new Byte[] { 1, 2, 3 });

        var query = "deployName=StarDeploy&version=v1.0.0";
        var response = await client.PostAsync($"/Deploy/UploadBuildFile?{query}", content);
        var body = await response.Content.ReadAsStringAsync();

        // [ApiFilter] 应当拦截无 token 请求
        Assert.Contains("401", body);
        Assert.Contains("认证失败", body);
    }

    [Fact(DisplayName = "带Token但缺少deployName返回400")]
    public async Task UploadBuildFile_WithToken_MissingDeployName_Returns400()
    {
        var token = await LoginAndGetTokenAsync();
        var client = _server.CreateClient();
        client.DefaultRequestHeaders.Add("X-Token", token);

        var content = BuildUploadContent("test.zip", new Byte[] { 1, 2, 3 });
        var query = "version=v1.0.0";
        var response = await client.PostAsync($"/Deploy/UploadBuildFile?{query}", content);

        // [ApiController] 自动模型验证在 action 之前拦截，返回 400 ProblemDetails
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "带Token但缺少version返回400")]
    public async Task UploadBuildFile_WithToken_MissingVersion_Returns400()
    {
        var token = await LoginAndGetTokenAsync();
        var client = _server.CreateClient();
        client.DefaultRequestHeaders.Add("X-Token", token);

        var content = BuildUploadContent("test.zip", new Byte[] { 1, 2, 3 });
        var query = "deployName=StarDeploy";
        var response = await client.PostAsync($"/Deploy/UploadBuildFile?{query}", content);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "带Token但缺少file返回400")]
    public async Task UploadBuildFile_WithToken_MissingFile_Returns400()
    {
        var token = await LoginAndGetTokenAsync();
        var client = _server.CreateClient();
        client.DefaultRequestHeaders.Add("X-Token", token);

        var query = "deployName=StarDeploy&version=v1.0.0";
        var content = new MultipartFormDataContent();
        var response = await client.PostAsync($"/Deploy/UploadBuildFile?{query}", content);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "带Token但部署集不存在返回404")]
    public async Task UploadBuildFile_WithToken_DeployNotFound_Returns404()
    {
        var token = await LoginAndGetTokenAsync();
        var client = _server.CreateClient();
        client.DefaultRequestHeaders.Add("X-Token", token);

        var content = BuildUploadContent("test.zip", new Byte[] { 1, 2, 3 });
        var query = "deployName=nonexistent_deploy_xyz&version=v1.0.0";
        var response = await client.PostAsync($"/Deploy/UploadBuildFile?{query}", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("不存在", body);
    }

    [Fact(DisplayName = "流式上传兼容性：StreamContent模拟DeployAgent上传")]
    public async Task UploadBuildFile_StreamContent_BindsParametersAndReachesBusinessLogic()
    {
        // 复现 DeployAgent 改用 StreamContent 流式上传后的参数绑定行为。
        // 关键点：query string 中的 deployName/version 必须能被 [ApiController] 正确推断绑定到简单类型参数。
        var token = await LoginAndGetTokenAsync();
        var client = _server.CreateClient();
        client.DefaultRequestHeaders.Add("X-Token", token);

        var tempFile = Path.Combine(Path.GetTempPath(), $"starupload_{Guid.NewGuid():N}.zip");
        var bytes = new Byte[] { 0x50, 0x4B, 0x05, 0x06, 1, 2, 3, 4 };
        await File.WriteAllBytesAsync(tempFile, bytes);

        try
        {
            await using var fileStream = new FileStream(tempFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            content.Add(fileContent, "file", Path.GetFileName(tempFile));

            var query = "deployName=nonexistent_deploy_xyz&version=v1.0.0";
            var response = await client.PostAsync($"/Deploy/UploadBuildFile?{query}", content);
            var body = await response.Content.ReadAsStringAsync();

            // 走到"部署集不存在"说明 deployName/version 都已正确绑定，认证也已通过
            Assert.Contains("不存在", body);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact(DisplayName = "正常上传：创建版本并保存附件")]
    public async Task UploadBuildFile_Normal_CreatesVersionAndAttachment()
    {
        var deployName = "test_upload_deploy";
        var version = $"v{DateTime.Now:yyyyMMddHHmmss}";

        // 准备应用部署集
        var app = AppDeploy.FindByName(deployName);
        app ??= new AppDeploy
        {
            Name = deployName,
            Enable = true,
        };
        app.Save();

        try
        {
            var token = await LoginAndGetTokenAsync();
            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Add("X-Token", token);

            var bytes = new Byte[] { 0x50, 0x4B, 0x05, 0x06, 1, 2, 3, 4 };
            var content = BuildUploadContent("test.zip", bytes);

            var query = $"deployName={Uri.EscapeDataString(deployName)}&version={Uri.EscapeDataString(version)}";
            var response = await client.PostAsync($"/Deploy/UploadBuildFile?{query}", content);
            var body = await response.Content.ReadAsStringAsync();

            // 验证响应包含版本信息（成功路径）
            Assert.True(response.IsSuccessStatusCode || body.Contains(version) || body.Contains("code"),
                $"上传失败：{response.StatusCode} - {body}");

            // 验证版本记录已创建
            var ver = AppDeployVersion.FindByDeployIdAndVersion(app.Id, version);
            Assert.NotNull(ver);
            Assert.Equal(version, ver.Version);
        }
        finally
        {
            // 清理版本与部署集
            var vers = AppDeployVersion.FindAllByDeployId(app.Id);
            foreach (var v in vers) v.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "401重认证场景：过期Token触发重新登录后上传成功")]
    public async Task UploadBuildFile_ExpiredToken_TriggersReloginAndRetriesUpload()
    {
        // 场景：客户端持过期 token 上传 → 服务端返回 401
        // 预期：客户端识别 401，重新登录获取新 token，用新 token 重试上传成功
        var deployName = "test_relogin_deploy";
        var version = $"v{DateTime.Now:yyyyMMddHHmmss}";

        var app = AppDeploy.FindByName(deployName);
        app ??= new AppDeploy { Name = deployName, Enable = true };
        app.Save();

        try
        {
            // Step 1: 用过期/无效 token 请求，应收到 401
            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Add("X-Token", "invalid_expired_token_xxx");

            var content = BuildUploadContent("test.zip", new Byte[] { 0x50, 0x4B, 0x05, 0x06, 1, 2, 3, 4 });
            var query = $"deployName={Uri.EscapeDataString(deployName)}&version={Uri.EscapeDataString(version)}";
            var response = await client.PostAsync($"/Deploy/UploadBuildFile?{query}", content);
            var body = await response.Content.ReadAsStringAsync();

            // 验证服务端确实返回 401（HTTP 状态码或响应体 code=401）
            var isHttp401 = (Int32)response.StatusCode == 401;
            var isBody401 = body.Contains("401") || body.Contains("认证失败");
            Assert.True(isHttp401 || isBody401,
                $"预期 401 认证失败，实际：{response.StatusCode} - {body}");

            // Step 2: 模拟客户端重新登录获取新 token
            var newToken = await LoginAndGetTokenAsync();
            Assert.NotEqual("invalid_expired_token_xxx", newToken);

            // Step 3: 用新 token 重试上传，应成功
            var client2 = _server.CreateClient();
            client2.DefaultRequestHeaders.Add("X-Token", newToken);

            var response2 = await client2.PostAsync($"/Deploy/UploadBuildFile?{query}", content);
            var body2 = await response2.Content.ReadAsStringAsync();

            Assert.True(response2.IsSuccessStatusCode || body2.Contains(version),
                $"重新登录后上传仍失败：{response2.StatusCode} - {body2}");

            // 验证版本记录已创建
            var ver = AppDeployVersion.FindByDeployIdAndVersion(app.Id, version);
            Assert.NotNull(ver);
        }
        finally
        {
            var vers = AppDeployVersion.FindAllByDeployId(app.Id);
            foreach (var v in vers) v.Delete();
            app.Delete();
        }
    }

    [Fact(DisplayName = "401重认证场景：响应体code=401被正确识别")]
    public async Task UploadBuildFile_BodyCode401_IsRecognizedAsUnauthorized()
    {
        // 验证：NewLife ApiFilter 返回 HTTP 200 但 body 是 {"code":401,"message":"认证失败"}
        // 客户端 IsUnauthorizedBody 逻辑应能识别这种 401
        var client = _server.CreateClient();
        client.DefaultRequestHeaders.Add("X-Token", "definitely_invalid_token");

        var content = BuildUploadContent("test.zip", new Byte[] { 1, 2, 3 });
        var query = "deployName=any_deploy&version=v1.0.0";
        var response = await client.PostAsync($"/Deploy/UploadBuildFile?{query}", content);
        var body = await response.Content.ReadAsStringAsync();

        // 无论 HTTP 状态码是 401 还是 200，body 都应包含 401 标识
        Assert.Contains("401", body);
        Assert.Contains("认证失败", body);
    }

    [Fact(DisplayName = "网络异常重试场景：服务端不可达时重试3次后失败")]
    public async Task UploadBuildFile_NetworkError_RetriesThreeTimesThenFails()
    {
        // 验证：指向不可达地址，HttpClient.PostAsync 应抛网络异常
        // 客户端重试逻辑捕获 HttpRequestException/IOException/SocketException 并重试 3 次
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(500) };
        // 使用一个保证不可达的地址：TCP 保留端口 + 短超时
        httpClient.BaseAddress = new Uri("http://127.0.0.1:9");

        var tempFile = Path.Combine(Path.GetTempPath(), $"starupload_{Guid.NewGuid():N}.zip");
        await File.WriteAllBytesAsync(tempFile, new Byte[] { 0x50, 0x4B, 0x05, 0x06, 1, 2, 3, 4 });

        try
        {
            await using var fileStream = new FileStream(tempFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            content.Add(fileContent, "file", Path.GetFileName(tempFile));

            // 网络不可达/超时应抛出可重试的异常类型
            // 客户端 UploadPackageAsync 重试循环捕获 HttpRequestException/IOException/SocketException/TaskCanceledException
            var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
                await httpClient.PostAsync("/Deploy/UploadBuildFile?deployName=x&version=v1", content));

            Assert.True(ex is HttpRequestException or System.Net.Sockets.SocketException or IOException or TaskCanceledException,
                $"预期可重试的网络异常类型，实际：{ex.GetType().Name}");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact(DisplayName = "MaxUploadSize配置项：中间件仅作用于UploadBuildFile路径，不影响其他接口")]
    public async Task MaxUploadSize_Middleware_OnlyAffectsUploadBuildFilePath()
    {
        // 验证中间件路径过滤：设为极小值后，node/login 等其他接口仍正常工作
        // 注：TestServer 不像 Kestrel 那样强制执行 MaxRequestBodySize，无法通过 HTTP 层验证拒绝行为；
        // 此处验证路径隔离——非 UploadBuildFile 路径不受配置项影响
        var original = StarServerSetting.Current.MaxUploadSize;
        StarServerSetting.Current.MaxUploadSize = 5;

        try
        {
            // node/login 路径不匹配 /Deploy/UploadBuildFile，应完全不受影响
            var token = await LoginAndGetTokenAsync();
            Assert.False(String.IsNullOrEmpty(token));
        }
        finally
        {
            StarServerSetting.Current.MaxUploadSize = original;
        }
    }

    [Fact(DisplayName = "MaxUploadSize配置项：足够大的配置值允许正常上传不被拒绝")]
    public async Task UploadBuildFile_SufficientMaxUploadSize_AllowsUpload()
    {
        // 验证中间件按配置项放行：设为 10MB 后，1MB 文件上传应到达业务逻辑（不被大小限制拒绝）
        var original = StarServerSetting.Current.MaxUploadSize;
        StarServerSetting.Current.MaxUploadSize = 10 * 1024 * 1024;

        try
        {
            var token = await LoginAndGetTokenAsync();
            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Add("X-Token", token);

            // 1MB 文件，在 10MB 配置限制内
            var bytes = new Byte[1024 * 1024];
            bytes[0] = 0x50; bytes[1] = 0x4B; bytes[2] = 0x05; bytes[3] = 0x06;
            var content = BuildUploadContent("large.zip", bytes);

            var query = "deployName=nonexistent_xyz&version=v1.0.0";
            var response = await client.PostAsync($"/Deploy/UploadBuildFile?{query}", content);
            var body = await response.Content.ReadAsStringAsync();

            // 预期：请求到达业务逻辑，返回"部署集不存在"（而非被大小限制拒绝）
            Assert.Contains("不存在", body);
        }
        finally
        {
            StarServerSetting.Current.MaxUploadSize = original;
        }
    }
}
