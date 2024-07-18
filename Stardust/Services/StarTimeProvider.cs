using NewLife.Remoting.Clients;

namespace Stardust.Services;

#if !NET40
internal class StarTimeProvider : TimeProvider
{
    public ClientBase Client { get; set; } = null!;

    public override DateTimeOffset GetUtcNow() => Client != null ? Client.GetNow().ToUniversalTime() : base.GetUtcNow();
}
#endif