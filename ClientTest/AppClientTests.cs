using System;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Data;
using NewLife.Messaging;
using Stardust;
using Xunit;

namespace ClientTest;

public class AppClientTests
{
    private sealed class TestStringHandler : IEventHandler<String>
    {
        public String Received { get; private set; }

        public Task HandleAsync(String @event, IEventContext context = null, CancellationToken cancellationToken = default)
        {
            Received = @event;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ProcessMessageAsync_DispatchesToEventBus_WhenTopicMatchesAndClientDiffers()
    {
        var client = new AppClient { ClientId = "clientA" };
        var bus = client.GetEventBus<String>("topic1");
        var handler = new TestStringHandler();
        bus.Subscribe(handler, clientId: "h1");

        var message = "event#topic1#clientB#\"hello\""; // JSON string payload
        await client.HandleAsync((ArrayPacket)message.GetBytes());

        Assert.Equal("\"hello\"", handler.Received);
    }

    [Fact]
    public async Task ProcessMessageAsync_DoesNotDispatch_WhenClientIdMatches()
    {
        var client = new AppClient { ClientId = "clientA" };
        var bus = client.GetEventBus<String>("topic1");
        var handler = new TestStringHandler();
        bus.Subscribe(handler, clientId: "h1");

        var message = "event#topic1#clientA#\"hello\""; // same clientId as AppClient
        await client.HandleAsync((ArrayPacket)message.GetBytes());

        Assert.Null(handler.Received);
    }

    [Fact]
    public async Task ProcessMessageAsync_DoesNotDispatch_WhenTopicUnknown()
    {
        var client = new AppClient { ClientId = "clientA" };
        var bus = client.GetEventBus<String>("topic1");
        var handler = new TestStringHandler();
        bus.Subscribe(handler, clientId: "h1");

        var message = "event#otherTopic#clientB#\"hello\""; // no bus registered for otherTopic
        await client.HandleAsync((ArrayPacket)message.GetBytes());

        Assert.Null(handler.Received);
    }
}
