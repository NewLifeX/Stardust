using System.Diagnostics;
using NewLife.Remoting.Clients;
using Stardust.Models;

namespace Stardust.Managers;

public interface IServiceController
{
    Int32 Delay { get; set; }
    DeployInfo? DeployInfo { get; set; }
    IEventProvider? EventProvider { get; set; }
    IList<IServiceHandler> Handlers { get; set; }
    Int32 Id { get; }
    ServiceInfo? Info { get; }
    Int32 MaxFails { get; set; }
    Int32 MonitorPeriod { get; set; }
    String Name { get; set; }
    Process? Process { get; set; }
    Int32 ProcessId { get; set; }
    String? ProcessName { get; set; }
    Boolean Running { get; set; }
    DateTime StartTime { get; set; }

    void SetInfo(ServiceInfo info);
    void SetProcess(Process? process);
}