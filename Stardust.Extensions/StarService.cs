using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Hosting;
using NewLife;
using NewLife.Log;
using NewLife.Reflection;

namespace Stardust.Extensions
{
    internal class StarService : IHostedService
    {
        private readonly StarFactory _starFactory;
        private readonly IFeatureCollection _applicationBuilder;

        public StarService(StarFactory starFactory, IFeatureCollection applicationBuilder)
        {
            _starFactory = starFactory;
            _applicationBuilder = applicationBuilder;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var feature = _applicationBuilder.Get<IServerAddressesFeature>();
            var addrs = feature?.Addresses.Join();
            if (!addrs.IsNullOrEmpty())
            {
                var serviceName = _starFactory.ServiceName;
                if (serviceName.IsNullOrEmpty()) serviceName = AssemblyX.Entry.Name;

                // 发布服务到星尘注册中心
                XTrace.WriteLine("发布服务[{0}]到星尘注册中心。", serviceName);
                _starFactory.Dust.Register(serviceName, addrs);
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
