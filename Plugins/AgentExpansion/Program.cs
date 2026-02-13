using NewLife.Log;

namespace AgentExpansion;

internal static class Program
{
    private static void Main(String[] args)
    {
        XTrace.UseConsole();

        var service = new AgentExpansionService { Log = XTrace.Log };
        service.RunOnce();
    }
}
