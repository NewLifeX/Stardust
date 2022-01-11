using System;
using System.Collections.Generic;
using System.Linq;
using NewLife;
using NewLife.Configuration;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Serialization;
using Stardust.Models;

namespace Stardust.Configs
{
    class StarHttpConfigProvider : HttpConfigProvider
    {
        public StarFactory Factory { get; set; }

        private IApiClient GetClient() => Client ??= new ApiHttpClient(Server) { Timeout = 3_000 };

        private Int32 _version = -1;
        protected override IDictionary<String, Object> GetAll()
        {
            var client = GetClient() as ApiHttpClient;

            var rs = client.Post<IDictionary<String, Object>>(Action, new
            {
                appId = AppId,
                secret = Secret,
                clientId = Factory.ClientId,
                scope = Scope,
                version = _version,
                usedKeys = UsedKeys.Join(),
                missedKeys = MissedKeys.Join(),
            });
            Info = rs;

            // 增强版返回
            if (rs.TryGetValue("configs", out var obj))
            {
                var ver = rs["version"].ToInt(-1);
                if (ver > 0) _version = ver;

                if (obj is not IDictionary<String, Object> configs) return null;

                rs = configs;
            }

            var inf = Info;
            if (inf != null && inf.TryGetValue("version", out var v) && v + "" != _version + "")
            {
                Factory.ConfigInfo = JsonHelper.Convert<ConfigInfo>(inf);

                var dic = new Dictionary<String, Object>(inf);
                dic.Remove("configs");
                XTrace.WriteLine("从配置中心加载：{0}", dic.ToJson());

                _version = v.ToInt();
            }

            return rs;
        }

        /// <summary>设置配置项，保存到服务端</summary>
        /// <param name="configs"></param>
        /// <returns></returns>
        protected override Int32 SetAll(IDictionary<String, Object> configs)
        {
            XTrace.WriteLine("保存到配置中心：{0}", configs.Keys.Join());

            var client = GetClient() as ApiHttpClient;

            return client.Post<Int32>("Config/SetAll", new
            {
                appId = AppId,
                secret = Secret,
                clientId = Factory.ClientId,
                configs,
            });
        }
    }
}