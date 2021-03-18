using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Stardust.Data;
using Stardust.Models;
using Stardust.Server.Common;
using Stardust.Server.Services;

namespace Stardust.Server.Controllers
{
    [Route("[controller]/[action]")]
    public class DustController : ControllerBase
    {
        /// <summary>用户主机</summary>
        public String UserHost => HttpContext.GetUserHost();

        private readonly TokenService _tokenService;

        public DustController(TokenService tokenService)
        {
            _tokenService = tokenService;
        }

        #region 心跳
        [ApiFilter]
        [HttpPost]
        public PingResponse Ping(PingInfo inf)
        {
            var rs = new PingResponse
            {
                Time = inf.Time,
                ServerTime = DateTime.UtcNow,
            };

            //if (Session["Node"] is Node node)
            //{
            //    var code = node.Code;
            //    node.FixArea();
            //    node.SaveAsync();

            //    rs.Period = node.Period;

            //    //var olt = GetOnline(code, node) ?? CreateOnline(code, node);
            //    //olt.Name = node.Name;
            //    //olt.Category = node.Category;
            //    //olt.Save(null, inf, Token);

            //    //// 拉取命令
            //    //rs.Commands = AcquireCommands(node.ID);
            //}

            return rs;
        }
        #endregion

        #region 发布、消费
        [ApiFilter]
        [HttpPost]
        public AppService Publish([FromBody] PublishServiceInfo service, String token)
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

                    Enable = app.AutoActive,

                    CreateIP = UserHost,
                };
            }

            svc.PingCount++;
            svc.Tag = service.Tag;
            svc.Version = service.Version;
            svc.Address = service.Address;

            svc.Save();

            return svc;
        }

        [ApiFilter]
        [HttpPost]
        public ServiceModel[] Consume([FromBody] ConsumeServiceInfo service, String token)
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
    }
}