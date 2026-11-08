using System.ComponentModel;
using NewLife.Remoting.Clients;
using NewLife.Remoting.Models;
using Xunit;

namespace Stardust.Tests;

/// <summary>本地事件台账测试</summary>
/// <remarks>
/// 事件以 CSV 存放在 Data 目录的台账文件（Time,Type,Name,Remark，无表头），节点用 NodeEvents.csv，应用用 AppEvents.csv；
/// 客户端取走台账上报，成功删除，失败保留等待下一次重试。
/// </remarks>
public class EventLedgerTests : IDisposable
{
    private readonly String _dir;

    public EventLedgerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "event-ledger-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private sealed class FakeClient : ClientBase
    {
        public Int32 Accepted { get; set; } = Int32.MaxValue;
        public Exception? Error { get; set; }
        public List<EventModel[]> Batches { get; } = [];

        public override Task<Int32> PostEvents(params EventModel[] events)
        {
            if (Error != null) throw Error;

            Batches.Add(events);
            return Task.FromResult(Math.Min(Accepted, events.Length));
        }
    }

    private static FakeClient CreateClient() => new()
    {
        Status = LoginStatus.LoggedIn,
        Features = Features.PostEvent,
    };

    #region 台账读写
    [Fact]
    [DisplayName("Read_读取CSV并处理引号换行与制表符")]
    public void Read_CsvWithQuotedFields()
    {
        var file = Path.Combine(_dir, EventLedger.NodeEvents);
        File.WriteAllText(file,
            "1762000000000,alert,AgentRestarted,\"代理已重启\"\n" +
            "\t1762000001000,info,RuntimeEvent,\"含逗号,引号和\"\"双引号\"\"\"\n" +
            "1762000002000,error,ReportFailed,\"第一行\n第二行\"\n");

        var list = EventLedger.Read(file);

        Assert.Equal(3, list.Count);
        Assert.Equal(1762000000000L, list[0].Time);
        Assert.Equal("alert", list[0].Type);
        Assert.Equal("代理已重启", list[0].Remark);
        Assert.Equal(1762000001000L, list[1].Time);
        Assert.Contains("含逗号", list[1].Remark);
        Assert.Contains("\n", list[2].Remark);
    }

    [Fact]
    [DisplayName("Read_忽略列数不足与非法Time")]
    public void Read_InvalidRowsSkipped()
    {
        var file = Path.Combine(_dir, EventLedger.AppEvents);
        File.WriteAllText(file,
            "not-time,info,Name,Remark\n" +
            "123,info,Name\n" +
            "123,,Name,Remark\n" +
            "123,info,,Remark\n" +
            "123,info,Good,Remark\n");

        var list = EventLedger.Read(file);

        Assert.Single(list);
        Assert.Equal("Good", list[0].Name);
    }

    [Fact]
    [DisplayName("Read_超过上限只取前N条")]
    public void Read_MaxLimit()
    {
        var file = Path.Combine(_dir, EventLedger.NodeEvents);
        var lines = new List<String>();
        for (var i = 0; i < 5; i++) lines.Add($"{1762000000000L + i},info,Event{i},Remark");
        File.WriteAllText(file, String.Join("\n", lines));

        var list = EventLedger.Read(file, 2);

        Assert.Equal(2, list.Count);
        Assert.Equal("Event0", list[0].Name);
        Assert.Equal("Event1", list[1].Name);
    }

    [Fact]
    [DisplayName("Read_文件不存在返回空")]
    public void Read_MissingFile_Empty()
    {
        Assert.Empty(EventLedger.Read(Path.Combine(_dir, "not-exists.csv")));
        Assert.Empty(EventLedger.Read(""));
    }

    [Fact]
    [DisplayName("TakePending_把当前台账改名为待上报")]
    public void TakePending_MovesCurrent()
    {
        var events = Path.Combine(_dir, EventLedger.NodeEvents);
        File.WriteAllText(events, "1762000000000,info,Event,Remark\n");

        var file = EventLedger.TakePending(_dir, EventLedger.NodeEvents);

        Assert.Equal(Path.Combine(_dir, "NodeEvents.reporting.csv"), file);
        Assert.False(File.Exists(events));
        Assert.True(File.Exists(file!));

        EventLedger.Complete(file!);
        Assert.False(File.Exists(file!));
    }

    [Fact]
    [DisplayName("TakePending_优先遗留的待上报台账")]
    public void TakePending_PrefersReporting()
    {
        var reporting = Path.Combine(_dir, "AppEvents.reporting.csv");
        var events = Path.Combine(_dir, EventLedger.AppEvents);
        File.WriteAllText(reporting, "1762000000000,info,Old,Remark\n");
        File.WriteAllText(events, "1762000001000,info,New,Remark\n");

        var file = EventLedger.TakePending(_dir, EventLedger.AppEvents);

        Assert.Equal(reporting, file);
        Assert.True(File.Exists(events));
    }

    [Fact]
    [DisplayName("TakePending_没有台账返回空")]
    public void TakePending_NoFile_ReturnsNull()
    {
        Assert.Null(EventLedger.TakePending(_dir, EventLedger.NodeEvents));
        Assert.Null(EventLedger.TakePending(_dir, EventLedger.AppEvents));
        Assert.Null(EventLedger.TakePending("", EventLedger.NodeEvents));
    }

    [Fact]
    [DisplayName("Append_追加后可读回且不覆盖已有行")]
    public void Append_AppendsRow()
    {
        var file = Path.Combine(_dir, EventLedger.NodeEvents);
        File.WriteAllText(file, "1762000000000,info,Existing,Remark\n");

        EventLedger.Append(_dir, EventLedger.NodeEvents, "error", "StarAgentReportFailed", "服务端不可用, 含\"引号\"");

        var list = EventLedger.Read(file);
        Assert.Equal(2, list.Count);
        Assert.Equal("Existing", list[0].Name);
        Assert.True(list[1].Time > 0);
        Assert.Equal("error", list[1].Type);
        Assert.Equal("StarAgentReportFailed", list[1].Name);
        Assert.Contains("服务端不可用", list[1].Remark);
    }

    [Fact]
    [DisplayName("节点与应用台账互不影响")]
    public void NodeAndAppLedgers_AreIsolated()
    {
        var node = Path.Combine(_dir, EventLedger.NodeEvents);
        var app = Path.Combine(_dir, EventLedger.AppEvents);
        File.WriteAllText(node, "1762000000000,info,Node,Remark\n");

        Assert.NotNull(EventLedger.TakePending(_dir, EventLedger.NodeEvents));
        Assert.Null(EventLedger.TakePending(_dir, EventLedger.AppEvents));
        Assert.False(File.Exists(node));
        Assert.False(File.Exists(app));
    }

    [Fact]
    [DisplayName("GetDirectory_取代理目录下的Data")]
    public void GetDirectory_UnderBaseDirectory()
    {
        var dir = EventLedger.GetDirectory();

        Assert.Equal(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data"), dir);
    }
    #endregion

    #region 上报
    [Fact]
    [DisplayName("ReportAsync_分批上报后删除台账")]
    public async Task ReportAsync_UploadsAll()
    {
        var file = Path.Combine(_dir, EventLedger.NodeEvents);
        var lines = new List<String>();
        for (var i = 0; i < 150; i++) lines.Add($"{1762000000000L + i},info,Event{i},Remark");
        File.WriteAllText(file, String.Join("\n", lines));

        using var client = CreateClient();
        var n = await EventLedger.ReportAsync(client, _dir, EventLedger.NodeEvents, "StarAgentReportFailed");

        Assert.Equal(150, n);
        Assert.Equal(2, client.Batches.Count);
        Assert.Equal(100, client.Batches[0].Length);
        Assert.Equal(50, client.Batches[1].Length);
        Assert.False(File.Exists(file));
        Assert.False(File.Exists(Path.Combine(_dir, "NodeEvents.reporting.csv")));
    }

    [Fact]
    [DisplayName("ReportAsync_服务端拒收时保留台账并追加失败事件")]
    public async Task ReportAsync_Rejected_KeepsLedger()
    {
        var file = Path.Combine(_dir, EventLedger.NodeEvents);
        File.WriteAllText(file, "1762000000000,info,Event,Remark\n");

        using var client = CreateClient();
        client.Accepted = 0;
        var n = await EventLedger.ReportAsync(client, _dir, EventLedger.NodeEvents, "StarAgentReportFailed");

        Assert.Equal(0, n);
        Assert.True(File.Exists(Path.Combine(_dir, "NodeEvents.reporting.csv")));

        var fails = EventLedger.Read(file);
        Assert.Single(fails);
        Assert.Equal("StarAgentReportFailed", fails[0].Name);
        Assert.Equal("error", fails[0].Type);
    }

    [Fact]
    [DisplayName("ReportAsync_上报异常时保留台账并追加失败事件")]
    public async Task ReportAsync_Throws_KeepsLedger()
    {
        var file = Path.Combine(_dir, EventLedger.AppEvents);
        File.WriteAllText(file, "1762000000000,info,Event,Remark\n");

        using var client = CreateClient();
        client.Error = new InvalidOperationException("boom");
        var n = await EventLedger.ReportAsync(client, _dir, EventLedger.AppEvents, "AppReportFailed");

        Assert.Equal(0, n);
        Assert.True(File.Exists(Path.Combine(_dir, "AppEvents.reporting.csv")));

        var fails = EventLedger.Read(file);
        Assert.Single(fails);
        Assert.Equal("AppReportFailed", fails[0].Name);
        Assert.Contains("boom", fails[0].Remark);
    }

    [Fact]
    [DisplayName("ReportAsync_未登录不上报也不动台账")]
    public async Task ReportAsync_NotLoggedIn_KeepsLedger()
    {
        var file = Path.Combine(_dir, EventLedger.NodeEvents);
        File.WriteAllText(file, "1762000000000,info,Event,Remark\n");

        using var client = new FakeClient { Features = Features.PostEvent };
        var n = await EventLedger.ReportAsync(client, _dir, EventLedger.NodeEvents, "StarAgentReportFailed");

        Assert.Equal(0, n);
        Assert.Empty(client.Batches);
        Assert.True(File.Exists(file));
        Assert.False(File.Exists(Path.Combine(_dir, "NodeEvents.reporting.csv")));
    }

    [Fact]
    [DisplayName("ReportAsync_没有台账时直接返回")]
    public async Task ReportAsync_NoLedger_ReturnsZero()
    {
        using var client = CreateClient();
        var n = await EventLedger.ReportAsync(client, _dir, EventLedger.NodeEvents, "StarAgentReportFailed");

        Assert.Equal(0, n);
        Assert.Empty(client.Batches);
    }
    #endregion
}
