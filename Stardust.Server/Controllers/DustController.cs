using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NewLife.Data;
using Stardust.Data;
using Stardust.Models;
using Stardust.Server.Common;
using Stardust.Server.Services;

namespace Stardust.Server.Controllers
{
    [Route("[action]")]
    public class DustController : ControllerBase
    {
        /// <summary>用户主机</summary>
        public String UserHost => HttpContext.GetUserHost();

        private readonly TokenService _tokenService;

        public DustController(TokenService tokenService) => _tokenService = tokenService;

        #region 发布、消费
        [ApiFilter]
        [HttpPost]
        public AppService RegisterService([FromBody] PublishServiceInfo service, String token)
        {
            var app = _tokenService.DecodeToken(token, Setting.Current);

            // 该应用所有服务
            var services = AppService.FindAllByAppId(app.Id);
            var svc = services.FirstOrDefault(e => e.ServiceName == service.ServiceName && e.Client == service.Client);
            if (svc == null)
            {
                svc = new AppService
                {
                    AppId = app.Id,
                    ServiceName = service.ServiceName,
                    Client = service.Client,

                    //Enable = app.AutoActive,

                    CreateIP = UserHost,
                };

                var history = AppHistory.Create(app, "RegisterService", true, $"注册服务[{service.ServiceName}] {service.Client}", Environment.NewLine, UserHost);
                history.Client = service.Client;
                history.SaveAsync();
            }

            svc.Enable = app.AutoActive;
            svc.PingCount++;
            svc.Tag = service.Tag;
            svc.Version = service.Version;
            svc.Address = service.Address?.Replace("://*", $"://{UserHost}").Replace("://[::]", $"://{UserHost}");

            svc.Save();

            return svc;
        }

        [ApiFilter]
        [HttpPost]
        public AppService UnregisterService([FromBody] PublishServiceInfo service, String token)
        {
            var app = _tokenService.DecodeToken(token, Setting.Current);

            // 该应用所有服务
            var services = AppService.FindAllByAppId(app.Id);
            var svc = services.FirstOrDefault(e => e.ServiceName == service.ServiceName && e.Client == service.Client);
            if (svc != null)
            {
                svc.Delete();

                var history = AppHistory.Create(app, "UnregisterService", true, $"服务[{service.ServiceName}]下线 {svc.Client}", Environment.NewLine, UserHost);
                history.Client = svc.Client;
                history.SaveAsync();
            }

            return svc;
        }

        [ApiFilter]
        [HttpPost]
        public ServiceModel[] ResolveService([FromBody] ConsumeServiceInfo service, String token)
        {
            var app = _tokenService.DecodeToken(token, Setting.Current);

            // 该服务所有消费
            var services = AppConsume.FindAllByService(service.ServiceName);
            var svc = services.FirstOrDefault(e => e.ServiceName == service.ServiceName && e.Client == service.Client);
            if (svc == null)
            {
                svc = new AppConsume
                {
                    AppId = app.Id,
                    ServiceName = service.ServiceName,
                    Client = service.Client,

                    Enable = true,

                    CreateIP = UserHost,
                };
            }

            svc.PingCount++;
            svc.Tag = service.Tag;
            svc.MinVersion = service.MinVersion;

            svc.Save();

            // 该服务所有生产
            var services2 = AppService.FindAllByService(service.ServiceName);
            services2 = services2.Where(e => e.Enable).ToList();

            //todo 匹配minversion和tag

            return services2.Select(e => new ServiceModel
            {
                ServiceName = e.ServiceName,
                Client = e.Client,
                Version = e.Version,
                Address = e.Address,
                LastActive = e.UpdateTime,
            }).ToArray();
        }
        #endregion

        [ApiFilter]
        public IList<AppService> SearchService(String serviceName, String key) => AppService.Search(-1, serviceName, true, key, new PageParameter { PageSize = 100 });
    }
}