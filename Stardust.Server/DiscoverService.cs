using NewLife.Net;
using NewLife.Remoting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stardust.Server
{
    [Api(null)]
    class DiscoverService
    {
        public String Name { get; set; }

        public NetUri Local { get; set; }

        [Api(nameof(Discover))]
        public Object Discover(String state)
        {
            return new
            {
                Name = Name,
                Server = Local + "",
                State = state,
            };
        }
    }
}