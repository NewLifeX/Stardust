using System;
using System.Threading;
using System.Threading.Tasks;
using NewLife.Remoting.Models;
using Xunit;

namespace Stardust.Tests;

/// <summary>客户端升级测试。覆盖并发升级保护</summary>
public class StarClientUpgradeTests
{
    private sealed class SlowUpgradeClient : StarClient
    {
        public Int32 Calls;

        public TaskCompletionSource<Boolean> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public SlowUpgradeClient(StarSetting setting) : base(setting) { }

        protected override async Task<IUpgradeInfo?> UpgradeAsync(String? channel, String? version, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            Started.TrySetResult(true);

            await Task.Delay(300, cancellationToken).ConfigureAwait(false);

            return null;
        }
    }

    [Fact(DisplayName = "并发升级请求只执行一次")]
    public async Task ConcurrentUpgrade_OnlyRunsOnce()
    {
        var set = new StarSetting { Server = "http://127.0.0.1:1" };
        using var client = new SlowUpgradeClient(set);

        var first = client.Upgrade("Release");
        await client.Started.Task;

        var second = await client.Upgrade("Release");

        Assert.Null(second);
        Assert.Null(await first);
        Assert.Equal(1, client.Calls);
    }
}
