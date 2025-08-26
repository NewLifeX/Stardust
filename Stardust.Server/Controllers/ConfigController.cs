using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Remoting.Extensions;
using NewLife.Remoting.Services;
using Stardust.Data;
using Stardust.Data.Configs;
using Stardust.Models;
using Stardust.Server.Models;
using Stardust.Server.Services;

namespace Stardust.Server.Controllers;

/// <summary>配置中心服务。向应用提供配置服务</summary>
[Route("[controller]/[action]")]
public class ConfigController(ConfigService configService, ITokenService tokenService, AppTokenService appTokenService, AppOnlineService appOnline, StarServerSetting setting) : ControllerBase
{
    [ApiFilter]
    public ConfigInfo GetAll(String appId, String secret, String clientId, String token, String scope, Int32 version)
    {
        if (appId.IsNullOrEmpty() && token.IsNullOrEmpty()) throw new ArgumentNullException(nameof(appId));

        // 验证
        var (app, online) = Valid(appId, secret, clientId, token);
        var ip = HttpContext.GetUserHost();

        // 作用域为空时重写
        scope = scope.IsNullOrEmpty() ? AppRule.CheckScope(app.Id, ip, clientId) : scope;

        // 作用域有改变时，也要返回配置数据
        var change = online.Scope != scope;
        online.Scope = scope;
        online.SaveAsync(3_000);

        // 版本没有变化时，不做计算处理，不返回配置数据
        if (!change && version > 0 && version >= app.Version) return new ConfigInfo { Version = app.Version, UpdateTime = app.UpdateTime };
        var dic = configService.GetConfigs(app, scope);

        // 返回WorkerId
        if (app.EnableWorkerId && dic.ContainsKey(configService.WorkerIdName))
            dic[configService.WorkerIdName] = online.WorkerId + "";

        return new ConfigInfo
        {
            Version = app.Version,
            Scope = scope,
            SourceIP = ip,
            NextVersion = app.NextVersion,
            NextPublish = app.PublishTime.ToFullString(""),
            UpdateTime = app.UpdateTime,
            Configs = dic,
        };
    }

    [ApiFilter]
    [HttpPost]
    public ConfigInfo GetAll([FromBody] ConfigInModel model, String token)
    {
        if (model.AppId.IsNullOrEmpty() && token.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.AppId));

        // 验证
        var (app, online) = Valid(model.AppId, model.Secret, model.ClientId, token);
        var ip = HttpContext.GetUserHost();

        // 使用键和缺失键
        if (!model.UsedKeys.IsNullOrEmpty()) app.UsedKeys = model.UsedKeys;
        if (!model.MissedKeys.IsNullOrEmpty()) app.MissedKeys = model.MissedKeys;
        app.Update();

        // 作用域为空时重写
        var scope = model.Scope;
        scope = scope.IsNullOrEmpty() ? AppRule.CheckScope(app.Id, ip, model.ClientId) : scope;

        // 作用域有改变时，也要返回配置数据
        var change = online.Scope != scope;
        online.Scope = scope;
        online.SaveAsync(3_000);

        // 版本没有变化时，不做计算处理，不返回配置数据
        if (!change && model.Version > 0 && model.Version >= app.Version)
            return new ConfigInfo
            {
                Version = app.Version,
                Scope = scope,
                SourceIP = ip,
                NextVersion = app.NextVersion,
                NextPublish = app.PublishTime.ToFullString(""),
                UpdateTime = app.UpdateTime
            };

        var dic = configService.GetConfigs(app, scope);

        // 返回WorkerId
        if (app.EnableWorkerId && dic.ContainsKey(configService.WorkerIdName))
            dic[configService.WorkerIdName] = online.WorkerId + "";

        return new ConfigInfo
        {
            Version = app.Version,
            Scope = scope,
            SourceIP = ip,
            NextVersion = app.NextVersion,
            NextPublish = app.PublishTime.ToFullString(""),
            UpdateTime = app.UpdateTime,
            Configs = dic,
        };
    }

    private (AppConfig, AppOnline) Valid(String appId, String secret, String clientId, String token)
    {
        // 优先令牌解码
        App app = null;
        if (!token.IsNullOrEmpty())
        {
            var (jwt, ex) = tokenService.DecodeToken(token);
            if (ex != null) throw ex;

            var ap1 = App.FindByName(jwt?.Subject);
            if (appId.IsNullOrEmpty()) appId = ap1?.Name;
            if (clientId.IsNullOrEmpty()) clientId = jwt.Id;

            app = ap1;
        }

        var ip = HttpContext.GetUserHost();
        app ??= appTokenService.Authorize(appId, secret, setting.AppAutoRegister, ip);

        // 新建应用配置
        var config = AppConfig.FindByName(appId);
        config ??= AppConfig.Find(AppConfig._.Name == appId);
        if (config == null)
        {
            var obj = AppConfig.Meta.Table;
            lock (obj)
            {
                config = AppConfig.FindByName(appId);
                if (config == null)
                {
                    config = new AppConfig
                    {
                        Name = app.Name,
                        AppId = app.Id,
                        Enable = app.Enable,
                    };

                    config.Insert();
                }
            }
        }

        if (app != null)
        {
            // 双向同步应用分类
            if (!app.Category.IsNullOrEmpty())
                config.Category = app.Category;
            else if (!config.Category.IsNullOrEmpty())
            {
                app.Category = config.Category;
                app.Update();
            }

            if (config.AppId == 0) config.AppId = app.Id;
            config.Update();
        }

        //var ip = HttpContext.GetUserHost();
        if (clientId.IsNullOrEmpty()) clientId = ip;

        // 更新心跳信息
        var online = appOnline.UpdateOnline(app, clientId, ip, token);

        // 检查应用有效性
        if (!config.Enable) throw new ArgumentOutOfRangeException(nameof(appId), $"应用[{appId}]已禁用！");

        // 刷新WorkerId
        if (config.EnableWorkerId) configService.RefreshWorkerId(config, online);

        return (config, online);
    }

    [ApiFilter]
    [HttpPost]
    public Int32 SetAll([FromBody] SetConfigModel model, String token)
    {
        if (model.AppId.IsNullOrEmpty() && token.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.AppId));

        // 验证
        var (app, _) = Valid(model.AppId, model.Secret, model.ClientId, token);
        if (app.Readonly) throw new Exception($"应用[{app}]处于只读模式，禁止修改");

        return configService.SetConfigs(app, model.Configs);
    }
}