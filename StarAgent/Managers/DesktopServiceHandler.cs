using System;
using System.Collections.Generic;
using System.Text;
using NewLife;
using Stardust.Managers;

namespace StarAgent;

/// <summary>桌面应用处理器。专门处理需要在前台桌面打开的应用</summary>
internal class DesktopServiceHandler : IServiceHandler
{
    public bool Start(ServiceInfo service)
    {
        if (!Runtime.Windows) return false;
        if (service.UserName != "$") return false;

        return true;
    }

    public void Stop(string reason)
    {
    }

    public bool Check()
    {
        return true;
    }
}