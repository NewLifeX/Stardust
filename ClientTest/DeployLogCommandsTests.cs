using NewLife.Remoting.Models;
using NewLife.Serialization;
using Stardust.Managers;
using Xunit;

namespace ClientTest;

public class DeployLogCommandsTests
{
    [Fact]
    public void ResolveLogRoot_Absolute()
    {
        var root = DeployLogCommands.ResolveLogRoot(@"D:\app\logs", @"D:\app");
        Assert.Equal(Path.GetFullPath(@"D:\app\logs"), root);
    }

    [Fact]
    public void ResolveLogRoot_RelativeToWorkDir()
    {
        var root = DeployLogCommands.ResolveLogRoot("logs", @"D:\app");
        Assert.Equal(Path.GetFullPath(@"D:\app\logs"), root);
    }

    [Fact]
    public void ResolveLogRoot_EmptyReturnsNull()
    {
        Assert.Null(DeployLogCommands.ResolveLogRoot(null, @"D:\app"));
    }

    [Fact]
    public async Task List_DirectoryNotFound_ReturnsError()
    {
        var listCmd = new CommandModel
        {
            Command = "deploy/log/list",
            Argument = new DeployLogListArg { LogDirectory = @"C:\no-such-stardust-log-dir", Pattern = "*.log" }.ToJson(),
        };
        var reply = await DeployLogCommands.ListAsync(listCmd);
        Assert.Equal(CommandStatus.错误, reply.Status);
        Assert.Contains("not found", reply.Data?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Tail_InvalidFileName_ReturnsError()
    {
        var dir = Path.Combine(Path.GetTempPath(), "stardust-deploy-log-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var tailCmd = new CommandModel
            {
                Command = "deploy/log/tail",
                Argument = new DeployLogTailArg { LogDirectory = dir, File = "..\\secret.log" }.ToJson(),
            };
            var tailReply = await DeployLogCommands.TailAsync(tailCmd);
            Assert.Equal(CommandStatus.错误, tailReply.Status);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public async Task Tail_FileOutsideRoot_ReturnsAccessDenied()
    {
        var root = Path.Combine(Path.GetTempPath(), "stardust-log-root-" + Guid.NewGuid().ToString("N"));
        var outside = Path.Combine(Path.GetTempPath(), "stardust-log-out-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        var outsideFile = Path.Combine(outside, "leak.log");
        await File.WriteAllTextAsync(outsideFile, "x");
        try
        {
            // 仅用文件名但解析后需落在 root 内；用绝对路径作为 file 应被拒
            var tailCmd = new CommandModel
            {
                Command = "deploy/log/tail",
                Argument = new DeployLogTailArg { LogDirectory = root, File = outsideFile }.ToJson(),
            };
            var tailReply = await DeployLogCommands.TailAsync(tailCmd);
            Assert.Equal(CommandStatus.错误, tailReply.Status);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
            try { Directory.Delete(outside, true); } catch { }
        }
    }

    [Fact]
    public async Task ListAndTail_LocalLogDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "stardust-deploy-log-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "smoke-app.log");
        await File.WriteAllTextAsync(file, "deploy-log-smoke-ok\nline2\n");

        try
        {
            var listCmd = new CommandModel
            {
                Command = "deploy/log/list",
                Argument = new DeployLogListArg { LogDirectory = dir, Pattern = "*.log" }.ToJson(),
            };
            var listReply = await DeployLogCommands.ListAsync(listCmd);
            Assert.Equal(CommandStatus.已完成, listReply.Status);
            var list = listReply.Data!.ToString()!.ToJsonEntity<DeployLogListResult>();
            Assert.NotNull(list);
            Assert.Contains(list!.Files, f => f.Name == "smoke-app.log");

            var tailCmd = new CommandModel
            {
                Command = "deploy/log/tail",
                Argument = new DeployLogTailArg { LogDirectory = dir, File = "smoke-app.log", MaxBytes = 65536 }.ToJson(),
            };
            var tailReply = await DeployLogCommands.TailAsync(tailCmd);
            Assert.Equal(CommandStatus.已完成, tailReply.Status);
            var tail = tailReply.Data!.ToString()!.ToJsonEntity<DeployLogTailResult>();
            Assert.NotNull(tail);
            Assert.Contains("deploy-log-smoke-ok", tail!.Content);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
