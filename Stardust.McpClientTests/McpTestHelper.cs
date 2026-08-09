using NewLife.Data;
using Stardust.Data;
using Stardust.Data.Nodes;
using Stardust.Data.Platform;
using System.Threading;
using XCode;

namespace Stardust.McpClientTests;

/// <summary>MCP测试数据辅助类。创建Token、资源授权、测试数据</summary>
public static class McpTestHelper
{
    /// <summary>WAL模式只需强制一次（避免清理/并发读写时出现 database is locked）。
    /// 服务端 TestServer 使用 Stardust.Web 的连接串，未设 WAL 时读会被写阻塞；
    /// 显式执行 PRAGMA 保证数据库进入 WAL 模式（读不阻塞写、写不阻塞读）。</summary>
    private static Int32 _walReady;

    private static void EnsureWalMode()
    {
        if (Interlocked.Exchange(ref _walReady, 1) == 1) return;
        try
        {
            // 通过实体自带的 DAL 执行 PRAGMA（避免直接依赖 XCode.DAL 命名空间）
            var dal = McpToken.Meta.Session.Dal;
            dal.Execute("PRAGMA journal_mode=WAL;");
            dal.Execute("PRAGMA busy_timeout=30000;");
            Console.WriteLine("✅ MCP测试数据库已强制 WAL 模式（避免 database is locked）");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ EnsureWalMode 失败：{ex.Message}");
        }
    }

    /// <summary>判断是否为 SQLite 数据库锁定异常（并发读写竞争，可重试）</summary>
    private static Boolean IsDatabaseLocked(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
            if (e.Message != null && e.Message.Contains("database is locked", StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    /// <summary>创建测试Token（启用、永不过期）</summary>
    public static (McpToken token, String tokenString) CreateTestToken(String name = "test-token")
    {
        EnsureWalMode();

        // 删除同名旧Token
        var existing = McpToken.Search(null, null, DateTime.MinValue, DateTime.MinValue, name, new PageParameter { PageIndex = 1, PageSize = 100 });
        foreach (var e in existing) e.Delete();

        var token = new McpToken
        {
            Name = name,
            Token = McpToken.GenerateToken(),
            Enable = true,
            ExpireTime = DateTime.MinValue,
        };
        token.Insert();
        return (token, token.Token);
    }

    /// <summary>为Token添加全部项目授权</summary>
    public static void AuthorizeAllProjects(Int32 tokenId)
    {
        EnsureWalMode();
        var r = new McpTokenResource
        {
            TokenId = tokenId,
            ResourceType = McpResourceType.Project.ToStorageName(),
            IsAll = true,
            Enable = true,
        };
        r.Insert();
    }

    /// <summary>为Token添加全部节点授权</summary>
    public static void AuthorizeAllNodes(Int32 tokenId)
    {
        EnsureWalMode();
        var r = new McpTokenResource
        {
            TokenId = tokenId,
            ResourceType = McpResourceType.Node.ToStorageName(),
            IsAll = true,
            Enable = true,
        };
        r.Insert();
    }

    /// <summary>为Token添加全部应用授权</summary>
    public static void AuthorizeAllApps(Int32 tokenId)
    {
        EnsureWalMode();
        var r = new McpTokenResource
        {
            TokenId = tokenId,
            ResourceType = McpResourceType.App.ToStorageName(),
            IsAll = true,
            Enable = true,
        };
        r.Insert();
    }

    /// <summary>为Token授权特定项目</summary>
    public static void AuthorizeProject(Int32 tokenId, Int32 projectId)
    {
        EnsureWalMode();
        var r = new McpTokenResource
        {
            TokenId = tokenId,
            ResourceType = McpResourceType.Project.ToStorageName(),
            ResourceId = projectId,
            IsAll = false,
            Enable = true,
        };
        r.Insert();
    }

    /// <summary>清理Token及其授权。
    /// 清理失败（如并发 database is locked）不应掩盖测试断言结果，故对锁定异常做有限重试，
    /// 重试耗尽仅记录告警并跳过，不抛异常。残留数据由下次 CreateTestToken 按名称清理。</summary>
    public static void CleanupToken(Int32 tokenId)
    {
        const Int32 maxRetry = 25;
        for (var i = 0; i < maxRetry; i++)
        {
            try
            {
                var resources = McpTokenResource.FindAllByToken(tokenId);
                foreach (var r in resources) r.Delete();
                var token = McpToken.FindById(tokenId);
                token?.Delete();
                return;
            }
            catch (Exception ex) when (IsDatabaseLocked(ex))
            {
                if (i == maxRetry - 1)
                {
                    Console.WriteLine($"⚠ CleanupToken 跳过（数据库锁定，已重试{maxRetry}次）：{ex.Message}");
                    return;
                }
                Thread.Sleep(150);
            }
        }
    }
}
