using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Hosting;
using NewLife;
using NewLife.Caching;
using Stardust.Data.Nodes;
using Stardust.Models;
using Stardust.Server.Services;
using Xunit;

namespace ServerTest.Nodes;

/// <summary>dotNet 运行时自动推送（NodeService.CheckDotNet）回归测试。
/// 语义：自动推送只由 NodeVersion 的 dotNet 升级策略驱动；DotNetPackage 安装包表仅用于手工推送，
/// 即使其中存在更新的启用安装包，也不得触发自动推送。</summary>
public class NodeServiceCheckDotNetTests
{
    #region 假的会话管理器基础设施
    /// <summary>最小服务提供器：仅满足 NodeSessionManager 构造时对 IHostApplicationLifetime 的依赖，其余一律返回 null。</summary>
    private sealed class FakeServiceProvider : IServiceProvider
    {
        public Object GetService(Type serviceType) =>
            serviceType == typeof(IHostApplicationLifetime) ? new FakeHostApplicationLifetime() : null;
    }

    /// <summary>最小化的宿主生命周期，避免 NodeSessionManager 构造时空引用。</summary>
    private sealed class FakeHostApplicationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => default;
        public CancellationToken ApplicationStopping => default;
        public CancellationToken ApplicationStopped => default;
        public void StopApplication() { }
    }

    /// <summary>创建 NodeService 实例并注入假的会话管理器与独立缓存：timeout=0 的 PublishAsync 立即返回，SendCommand 不依赖真实节点会话。</summary>
    private static NodeService CreateService()
    {
        var svc = (NodeService)RuntimeHelpers.GetUninitializedObject(typeof(NodeService));

        var type = typeof(NodeService);
        type.GetField("_sessionManager", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(svc, new NodeSessionManager(new FakeServiceProvider()));

        // CheckDotNet 使用缓存做 600 秒节流，使用独立内存缓存避免测试间互相影响
        var cache = new CacheProvider();
        cache.Cache = new MemoryCache();
        cache.InnerCache = cache.Cache;
        type.GetField("_cacheProvider", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(svc, cache);

        return svc;
    }
    #endregion

    #region 测试数据
    /// <summary>测试期间暂时禁用其它已启用的 dotNet 策略，避免共享测试库中的遗留数据干扰断言；结束后恢复。</summary>
    private sealed class DotNetPolicyIsolation : IDisposable
    {
        private readonly List<NodeVersion> _items = [];

        public DotNetPolicyIsolation()
        {
            var list = NodeVersion.FindAll()
                .Where(e => e.ProductCode.EqualIgnoreCase("dotNet") && e.Enable).ToList();
            foreach (var item in list)
            {
                item.Enable = false;
                item.Update();
                _items.Add(item);
            }
        }

        public void Dispose()
        {
            foreach (var item in _items)
            {
                item.Enable = true;
                item.Update();
            }
        }
    }

    /// <summary>新建测试节点。Framework 取 8.0.6（低于安装包表中的 10.0.12），保留“包表路径若存在将会命中”的回归条件。</summary>
    private static Node NewNode(String code) => new()
    {
        Code = code,
        Name = code,
        ProductCode = "StarAgent",
        Version = "3.6.2026.0104",
        Framework = "8.0.6",
        OSKind = OSKinds.Ubuntu,
        Architecture = "Arm64",
        Enable = true,
    };

    /// <summary>确保安装包表存在启用的 10.0.12-aspnet（Linux/Arm64）安装包，返回是否为本测试新建。</summary>
    private static (DotNetPackage package, Boolean created) EnsureEnabledPackage()
    {
        var pkg = DotNetPackage.FindAll(DotNetPackage._.Kind == "aspnet" & DotNetPackage._.Version == "10.0.12")
            .FirstOrDefault(e => e.Enable && e.OSKind == OSKind.Linux && e.Architecture == CpuArch.Arm64);
        if (pkg != null) return (pkg, false);

        pkg = new DotNetPackage
        {
            Version = "10.0.12",
            Kind = "aspnet",
            OSKind = OSKind.Linux,
            Architecture = CpuArch.Arm64,
            FileName = "aspnetcore-runtime-10.0.12-linux-arm64.tar.gz",
            Source = "https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/10.0.12/aspnetcore-runtime-10.0.12-linux-arm64.tar.gz",
            Enable = true,
            Channel = NodeChannels.Release,
        };
        pkg.Insert();
        return (pkg, true);
    }

    private static List<NodeCommand> FindInstallCommands(Int32 nodeId) =>
        NodeCommand.FindAll(NodeCommand._.NodeID == nodeId)
            .Where(e => e.Command == "framework/install").ToList();

    private static List<NodeHistory> FindPushHistories(Int32 nodeId) =>
        NodeHistory.FindAll(NodeHistory._.NodeID == nodeId)
            .Where(e => e.Action == "推送dotNet").ToList();

    private static void CleanupNode(Int32 nodeId)
    {
        if (nodeId <= 0) return;
        foreach (var e in NodeCommand.FindAll(NodeCommand._.NodeID == nodeId)) e.Delete();
        // 节点历史属于只写日志型数据，禁止删除，保留审计记录
        foreach (var e in Node.FindAll(Node._.ID == nodeId)) e.Delete();
    }
    #endregion

    /// <summary>启用一条 dotNet 策略后，推送目标必须是策略记录的版本；
    /// 即使安装包表存在更新的启用包（10.0.12-aspnet），也不得推送包表版本。</summary>
    [Fact]
    [Trait("Category", "Nodes")]
    public void PushPolicyVersion_NotPackageVersion()
    {
        var code = "test-net-" + Guid.NewGuid().ToString("n")[..10];
        const String ver = "v9.9.99-aspnet";
        NodeVersion record = null;
        Node node = null;
        var (pkg, pkgCreated) = EnsureEnabledPackage();
        using var isolation = new DotNetPolicyIsolation();

        try
        {
            // 清理上次异常退出可能遗留的同名策略
            foreach (var item in NodeVersion.FindAll(NodeVersion._.Version == ver)) item.Delete();

            record = new NodeVersion
            {
                Version = ver,
                ProductCode = "dotNet",
                Enable = true,
                Force = false,
                Channel = NodeChannels.Release,
                Strategy = $"node={code}",
                Source = "http://127.0.0.1/files/dotnet/",
            };
            record.Insert();

            node = NewNode(code);
            node.Insert();

            var svc = CreateService();
            var rs = svc.CheckDotNet(node, new Uri("http://127.0.0.1:6600"), "127.0.0.1");

            // 推送的是策略记录版本，而不是安装包表的 10.0.12
            Assert.NotNull(rs);
            Assert.Equal(ver, rs.Version);

            var cmds = FindInstallCommands(node.ID);
            Assert.Single(cmds);
            Assert.Contains(ver, cmds[0].Argument);
            Assert.DoesNotContain("10.0.12", cmds[0].Argument);

            var histories = FindPushHistories(node.ID);
            Assert.Single(histories);
            Assert.Contains(ver, histories[0].Remark);
        }
        finally
        {
            if (node != null) CleanupNode(node.ID);
            record?.Delete();
            if (pkgCreated) pkg.Delete();
        }
    }

    /// <summary>节流生效：同一节点在节流窗口内第二次执行检查，不再重复下发安装指令。</summary>
    [Fact]
    [Trait("Category", "Nodes")]
    public void SecondCheck_NoRepeatPush()
    {
        var code = "test-net-" + Guid.NewGuid().ToString("n")[..10];
        const String ver = "v9.9.98-aspnet";
        NodeVersion record = null;
        Node node = null;
        using var isolation = new DotNetPolicyIsolation();

        try
        {
            foreach (var item in NodeVersion.FindAll(NodeVersion._.Version == ver)) item.Delete();

            record = new NodeVersion
            {
                Version = ver,
                ProductCode = "dotNet",
                Enable = true,
                Force = false,
                Channel = NodeChannels.Release,
                Strategy = $"node={code}",
                Source = "http://127.0.0.1/files/dotnet/",
            };
            record.Insert();

            node = NewNode(code);
            node.Insert();

            var svc = CreateService();
            var rs1 = svc.CheckDotNet(node, new Uri("http://127.0.0.1:6600"), "127.0.0.1");
            Assert.NotNull(rs1);

            var count = FindInstallCommands(node.ID).Count;
            Assert.Equal(1, count);

            // 第二次检查命中节流缓存，不重复推送
            var rs2 = svc.CheckDotNet(node, new Uri("http://127.0.0.1:6600"), "127.0.0.1");
            Assert.Null(rs2);
            Assert.Equal(count, FindInstallCommands(node.ID).Count);
        }
        finally
        {
            if (node != null) CleanupNode(node.ID);
            record?.Delete();
        }
    }

    /// <summary>没有任何启用的 dotNet 策略时，即使安装包表存在启用的新版安装包，也不得自动推送。</summary>
    [Fact]
    [Trait("Category", "Nodes")]
    public void NoEnabledPolicy_NoPush()
    {
        var code = "test-net-" + Guid.NewGuid().ToString("n")[..10];
        Node node = null;
        var (pkg, pkgCreated) = EnsureEnabledPackage();
        using var isolation = new DotNetPolicyIsolation();

        try
        {
            node = NewNode(code);
            node.Insert();

            var svc = CreateService();
            var rs = svc.CheckDotNet(node, new Uri("http://127.0.0.1:6600"), "127.0.0.1");

            Assert.Null(rs);
            Assert.Empty(FindInstallCommands(node.ID));
            Assert.Empty(FindPushHistories(node.ID));
        }
        finally
        {
            if (node != null) CleanupNode(node.ID);
            if (pkgCreated) pkg.Delete();
        }
    }

    /// <summary>原生 Agent（Go/Rust/C++）没有 .NET 运行时，即使存在匹配的 dotNet 策略也不得推送 framework/install。</summary>
    [Theory]
    [InlineData("StarAgent-Go")]
    [InlineData("StarAgent-Rust")]
    [InlineData("StarAgent-Cpp")]
    [Trait("Category", "Nodes")]
    public void NativeAgent_NoPush(String productCode)
    {
        var code = "test-net-" + Guid.NewGuid().ToString("n")[..10];
        const String ver = "v9.9.97-aspnet";
        NodeVersion record = null;
        Node node = null;
        using var isolation = new DotNetPolicyIsolation();

        try
        {
            foreach (var item in NodeVersion.FindAll(NodeVersion._.Version == ver)) item.Delete();

            record = new NodeVersion
            {
                Version = ver,
                ProductCode = "dotNet",
                Enable = true,
                Force = false,
                Channel = NodeChannels.Release,
                Strategy = $"node={code}",
                Source = "http://127.0.0.1/files/dotnet/",
            };
            record.Insert();

            node = NewNode(code);
            node.ProductCode = productCode;
            node.Insert();

            var svc = CreateService();
            var rs = svc.CheckDotNet(node, new Uri("http://127.0.0.1:6600"), "127.0.0.1");

            Assert.Null(rs);
            Assert.Empty(FindInstallCommands(node.ID));
            Assert.Empty(FindPushHistories(node.ID));
        }
        finally
        {
            if (node != null) CleanupNode(node.ID);
            record?.Delete();
        }
    }
}
