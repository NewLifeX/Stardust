using System.ComponentModel;
using System.Text;
using Stardust.Managers;
using Xunit;

namespace ClientTest;

/// <summary>应用实例日志读取与上报指令的单元测试</summary>
public class AppLogCommandsTests
{
    private static String CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "stardust-app-log-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    [DisplayName("解析当日日志文件：优先使用 yyyy_MM_dd.log")]
    public void ResolveLogFile_PrefersTodayFile()
    {
        var dir = CreateTempDir();
        try
        {
            var now = new DateTime(2026, 11, 23, 10, 0, 0);
            var today = Path.Combine(dir, "2026_11_23.log");
            File.WriteAllText(today, "today");
            File.WriteAllText(Path.Combine(dir, "2026_11_22.log"), "yesterday");

            Assert.Equal(today, AppLogCommands.ResolveLogFile(dir, now));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    [DisplayName("解析日志文件：当日文件缺失时退化为目录内最新日志")]
    public void ResolveLogFile_FallsBackToNewest()
    {
        var dir = CreateTempDir();
        try
        {
            var old = Path.Combine(dir, "2026_11_22.log");
            File.WriteAllText(old, "old");
            File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddMinutes(-10));

            var newest = Path.Combine(dir, "2026_11_21.log");
            File.WriteAllText(newest, "new");
            File.SetLastWriteTimeUtc(newest, DateTime.UtcNow);

            Assert.Equal(newest, AppLogCommands.ResolveLogFile(dir, new DateTime(2026, 11, 23)));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    [DisplayName("解析日志文件：单文件路径直接返回")]
    public void ResolveLogFile_SingleFile()
    {
        var dir = CreateTempDir();
        try
        {
            var file = Path.Combine(dir, "app.log");
            File.WriteAllText(file, "x");

            Assert.Equal(file, AppLogCommands.ResolveLogFile(file, DateTime.Now));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    [DisplayName("解析日志文件：目录不存在返回空")]
    public void ResolveLogFile_MissingDirectory_ReturnsNull()
    {
        var dir = Path.Combine(Path.GetTempPath(), "stardust-app-log-missing-" + Guid.NewGuid().ToString("N"));

        Assert.Null(AppLogCommands.ResolveLogFile(dir, DateTime.Now));
        Assert.Null(AppLogCommands.ResolveLogFile(null, DateTime.Now));
        Assert.Null(AppLogCommands.ResolveLogFile("", DateTime.Now));
    }

    [Fact]
    [DisplayName("读取日志尾部：文件不存在返回空")]
    public void ReadTail_MissingFile_ReturnsNull()
    {
        Assert.Null(AppLogCommands.ReadTail(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".log"), 1024));
        Assert.Null(AppLogCommands.ReadTail(null, 1024));
    }

    [Fact]
    [DisplayName("读取日志尾部：按上限截取最后字节")]
    public void ReadTail_CapsMaxBytes()
    {
        var dir = CreateTempDir();
        try
        {
            var file = Path.Combine(dir, "app.log");
            var lines = String.Join("\n", Enumerable.Range(0, 1000).Select(e => $"line-{e:D4}"));
            File.WriteAllText(file, lines, Encoding.UTF8);

            var tail = AppLogCommands.ReadTail(file, 64);

            Assert.NotNull(tail);
            Assert.True(Encoding.UTF8.GetByteCount(tail) <= 64);
            Assert.EndsWith("line-0999", tail);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    [DisplayName("读取日志尾部：空文件返回空字符串而不是空引用")]
    public void ReadTail_EmptyFile_ReturnsEmpty()
    {
        var dir = CreateTempDir();
        try
        {
            var file = Path.Combine(dir, "empty.log");
            File.WriteAllText(file, "");

            Assert.Equal("", AppLogCommands.ReadTail(file, 1024));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    [DisplayName("构造上报请求：POST /Log 并携带应用、实例与令牌")]
    public async Task BuildRequest_SetsHeadersAndBody()
    {
        using var req = AppLogCommands.BuildRequest("http://127.0.0.1:6600", "TestApp", "10.0.0.1@123", "token-1", "hello-log");

        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("http://127.0.0.1:6600/Log", req.RequestUri + "");
        Assert.Equal("TestApp", req.Headers.GetValues("X-AppId").First());
        Assert.Equal("10.0.0.1@123", req.Headers.GetValues("X-ClientId").First());
        Assert.Equal("token-1", req.Headers.GetValues("X-Token").First());
        Assert.Equal("text/plain", req.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("hello-log", await req.Content.ReadAsStringAsync());
    }

    [Fact]
    [DisplayName("解析上报响应：取 parsed 行数，非 JSON 归零")]
    public void ParseParsedCount_HandlesJsonAndPlainText()
    {
        Assert.Equal(12, AppLogCommands.ParseParsedCount("{\"parsed\":12}"));
        Assert.Equal(0, AppLogCommands.ParseParsedCount("OK"));
        Assert.Equal(0, AppLogCommands.ParseParsedCount(null));
    }
}
