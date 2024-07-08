using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stardust.Managers;

/// <summary>服务事件参数</summary>
public class ServiceEventArgs : EventArgs
{
    public IServiceController? Controller { get; set; }
}
