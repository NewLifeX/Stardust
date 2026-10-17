using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using Stardust.Data.Nodes;
using Xunit;

namespace ServerTest.Communication;

/// <summary>原生 Agent 到真实 StarServer 的端到端测试。仅在设置 STARAGENT_NATIVE_BIN 时运行。</summary>
/// <remarks>
/// 该测试启动真实 Kestrel 服务端，再启动外部原生 Agent 进程，验证登录、身份持久化与在线状态。
/// 通过 STARAGENT_NATIVE_PRODUCT 指定产品编码（默认 StarAgent-Go），因此可分别验证 Go/Rust/C++。
/// CI 中若未构建原生 Agent 可跳过；本地可用：
/// <code>$env:STARAGENT_NATIVE_BIN='C:\path\to\staragent.exe'; $env:STARAGENT_NATIVE_PRODUCT='StarAgent-Rust'; dotnet test ...</code>
/// </remarks>
[Collection("StarServer")]
public class NativeAgentIntegrationTests
{
    private readonly StarServerFixture _fixture;

    public NativeAgentIntegrationTests(StarServerFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "原生代理-真实服务端登录并上线")]
    public async Task NativeAgent_Login_And_Online()
    {
        var bin = Environment.GetEnvironmentVariable("STARAGENT_NATIVE_BIN");
        if (bin.IsNullOrEmpty()) bin = Environment.GetEnvironmentVariable("STARAGENT_GO_BIN");
        if (bin.IsNullOrEmpty() || !File.Exists(bin))
        {
            // 未提供原生 Agent 时跳过，不把环境缺失当作失败
            return;
        }

        var productCode = Environment.GetEnvironmentVariable("STARAGENT_NATIVE_PRODUCT");
        if (productCode.IsNullOrEmpty()) productCode = "StarAgent-Go";

        var dir = Path.Combine(Path.GetTempPath(), "staragent-go-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        var configPath = Path.Combine(dir, "staragent.json");
        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(new
        {
            Server = _fixture.ServerUrl,
            ProductCode = productCode,
            Code = "",
            Secret = "",
            Project = "",
            Channel = "Release",
            DataDir = dir,
            LogLevel = "info",
            MetricsInterval = 5,
            AllowExec = false,
            AllowedCommands = Array.Empty<String>(),
            CommandTimeout = 30,
            TlsVerify = true,
        }));

        Node? node = null;
        var output = new StringBuilder();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(bin)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };
        process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
        process.StartInfo.ArgumentList.Add("run");
        process.StartInfo.ArgumentList.Add("--config");
        process.StartInfo.ArgumentList.Add(configPath);

        try
        {
            Assert.True(process.Start(), "无法启动原生 Agent");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Go Agent 登录成功后把服务端下发的 Code/Secret 写回配置文件
            String? code = null;
            for (var i = 0; i < 80 && code.IsNullOrEmpty(); i++)
            {
                await Task.Delay(250);
                try
                {
                    using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(configPath));
                    if (doc.RootElement.TryGetProperty("Code", out var value)) code = value.GetString();
                }
                catch (IOException)
                {
                    // 原子写入期间可能短暂读不到，继续重试
                }
            }

            String logs;
            lock (output) logs = output.ToString();
            Assert.False(code.IsNullOrEmpty(), "原生 Agent 未在超时时间内完成登录并持久化 Code。输出：\n" + logs);

            node = Node.FindByCode(code);
            Assert.NotNull(node);
            Assert.Equal(productCode, node!.ProductCode);
            Assert.True(node.Enable, "自动注册的节点应启用");

            NodeOnline? online = null;
            for (var i = 0; i < 40 && online == null; i++)
            {
                await Task.Delay(250);
                online = NodeOnline.FindByNodeId(node.ID);
            }
            Assert.NotNull(online);
        }
        finally
        {
            try
            {
                if (!process.HasExited) process.Kill(true);
            }
            catch { }

            if (node != null)
            {
                NodeOnline.FindByNodeId(node.ID)?.Delete();
                node.Delete();
            }

            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
