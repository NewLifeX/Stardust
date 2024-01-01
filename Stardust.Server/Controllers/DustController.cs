using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Data;
using NewLife.Remoting;
using NewLife.Serialization;
using Stardust.Data;
using Stardust.Data.Configs;
using Stardust.Models;
using Stardust.Server.Common;
using Stardust.Server.Services;

namespace Stardust.Server.Controllers;

[Route("[action]")]
public class DustController : ControllerBase
{
    /// <summary>用户主机</summary>
    public String UserHost => HttpContext.GetUserHost();

    private readonly TokenService _tokenService;
    private readonly StarServerSetting _setting;

    public DustController(TokenService tokenService, StarServerSetting setting)
    {
        _tokenService = tokenService;
        _setting = setting;
    }

    #region 发布、消费
    private Service GetService(String serviceName)
    {
        var info = Service.FindByName(serviceName);
        if (info == null)
        {
            info = new Service { Name = serviceName, Enable = true };
            info.Insert();
        }
        if (!info.Enable) throw new ApiException(403, $"服务[{serviceName}]已停用！");

        return info;
    }

    [Obsolete]
    [ApiFilter]
    [HttpPost]
    public AppService RegisterService([FromBody] PublishServiceInfo service, String token)
    {
        var (_, app) = _tokenService.DecodeToken(token, _setting.TokenSecret);
        var info = GetService(service.ServiceName);

        // 所有服务
        var services = AppService.FindAllByService(info.Id);
        var svc = services.FirstOrDefault(e => e.AppId == app.Id && e.Client == service.ClientId);
        if (svc == null)
        {
            svc = new AppService
            {
                AppId = app.Id,
                ServiceId = info.Id,
                ServiceName = service.ServiceName,
                Client = service.ClientId,

                //Enable = app.AutoActive,

                CreateIP = UserHost,
            };
            services.Add(svc);

            var history = AppHistory.Create(app, "RegisterService", true, $"注册服务[{service.ServiceName}] {service.ClientId}", null, Environment.MachineName, UserHost);
            history.Client = service.ClientId;
            history.Insert();
        }

        // 作用域
        if (info.UseScope)
            svc.Scope = AppRule.CheckScope(-1, UserHost, service.ClientId);

        // 地址处理。本地任意地址，更换为IP地址
        var ip = service.IP;
        if (ip.IsNullOrEmpty()) ip = service.ClientId.Substring(null, ":");
        if (ip.IsNullOrEmpty()) ip = UserHost;
        var addrs = service.Address
            ?.Replace("://*", $"://{ip}")
            .Replace("://0.0.0.0", $"://{ip}")
            .Replace("://[::]", $"://{ip}");

        svc.Enable = app.AutoActive;
        svc.PingCount++;
        svc.Tag = service.Tag;
        svc.Version = service.Version;
        svc.Address = addrs;

        svc.Save();

        info.Providers = services.Count;
        info.Save();

        return svc;
    }

    [Obsolete]
    [ApiFilter]
    [HttpPost]
    public AppService UnregisterService([FromBody] PublishServiceInfo service, String token)
    {
        var (_, app) = _tokenService.DecodeToken(token, _setting.TokenSecret);
        var info = GetService(service.ServiceName);

        // 所有服务
        var services = AppService.FindAllByService(info.Id);
        var svc = services.FirstOrDefault(e => e.AppId == app.Id && e.Client == service.ClientId);
        if (svc != null)
        {
            //svc.Delete();
            svc.Enable = false;
            svc.Update();

            services.Remove(svc);

            var history = AppHistory.Create(app, "UnregisterService", true, $"服务[{service.ServiceName}]下线 {svc.Client}", null, Environment.MachineName, UserHost);
            history.Client = svc.Client;
            history.Insert();
        }

        info.Providers = services.Count;
        info.Save();

        return svc;
    }

    [Obsolete]
    [ApiFilter]
    [HttpPost]
    public ServiceModel[] ResolveService([FromBody] ConsumeServiceInfo model, String token)
    {
        var (_, app) = _tokenService.DecodeToken(token, _setting.TokenSecret);
        var info = GetService(model.ServiceName);

        // 所有消费
        var consumes = AppConsume.FindAllByService(info.Id);
        var svc = consumes.FirstOrDefault(e => e.AppId == app.Id && e.Client == model.ClientId);
        if (svc == null)
        {
            svc = new AppConsume
            {
                AppId = app.Id,
                ServiceId = info.Id,
                ServiceName = model.ServiceName,
                Client = model.ClientId,

                Enable = true,

                CreateIP = UserHost,
            };
            consumes.Add(svc);

            var history = AppHistory.Create(app, "ResolveService", true, $"消费服务[{model.ServiceName}] {model.ToJson()}", null, Environment.MachineName, UserHost);
            history.Client = svc.Client;
            history.Insert();
        }

        // 作用域
        svc.Scope = AppRule.CheckScope(-1, UserHost, model.ClientId);
        svc.PingCount++;
        svc.Tag = model.Tag;
        svc.MinVersion = model.MinVersion;

        svc.Save();

        info.Consumers = consumes.Count;
        info.Save();

        // 该服务所有生产
        var services = AppService.FindAllByService(info.Id);
        services = services.Where(e => e.Enable).ToList();

        // 匹配minversion和tag
        services = services.Where(e => e.Match(model.MinVersion, svc.Scope, model.Tag?.Split(","))).ToList();

        return services.Select(e => new ServiceModel
        {
            ServiceName = e.ServiceName,
            DisplayName = info.DisplayName,
            Client = e.Client,
            Version = e.Version,
            Address = e.Address,
            Scope = e.Scope,
            Tag = e.Tag,
            Weight = e.Weight,
            CreateTime = e.CreateTime,
            UpdateTime = e.UpdateTime,
        }).ToArray();
    }
    #endregion

    [Obsolete]
    [ApiFilter]
    public IList<AppService> SearchService(String serviceName, String key)
    {
        var svc = Service.FindByName(serviceName);
        if (svc == null) return null;

        return AppService.Search(-1, svc.Id, null, true, key, new PageParameter { PageSize = 100 });
    }
}