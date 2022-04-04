using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NewLife.Web;
using Stardust.Data.Monitors;

namespace Stardust.Web.Models
{
    public class TraceViewModel
    {
        public Pager Page { get; set; }

        public IList<SampleData> Data { get; set; }
    }
}