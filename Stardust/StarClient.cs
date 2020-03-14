using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using NewLife.Log;
using NewLife.Net;
using NewLife.Reflection;
using NewLife.Remoting;
using NewLife.Serialization;

namespace Stardust
{
    /// <summary>星星客户端</summary>
    public class StarClient : ApiHttpClient
    {
        #region 属性
        /// <summary>用户名</summary>
        public String UserName { get; set; }

        /// <summary>密码</summary>
        public String Password { get; set; }

        /// <summary>是否已登录</summary>
        public Boolean Logined { get; set; }

        /// <summary>最后一次登录成功后的消息</summary>
        public IDictionary<String, Object> Info { get; private set; }
        #endregion

        #region 方法
        /// <summary>实例化</summary>
        public StarClient()
        {
        }

        /// <summary>实例化</summary>
        /// <param name="uri"></param>
        public StarClient(String uri)
        {
            //if (!uri.IsNullOrEmpty())
            //{
            //    var u = new Uri(uri);

            //    Servers = new[] { "{2}://{0}:{1}".F(u.Host, u.Port, u.Scheme) };

            //    var us = u.UserInfo.Split(":");
            //    if (us.Length > 0) UserName = us[0];
            //    if (us.Length > 1) Password = us[1];
            //}
        }
        #endregion

        #region 登录
        ///// <summary>连接后自动登录</summary>
        ///// <param name="client">客户端</param>
        ///// <param name="force">强制登录</param>
        //protected override async Task<Object> OnLoginAsync(ISocketClient client, Boolean force)
        //{
        //    if (Logined && !force) return null;

        //    var asmx = AssemblyX.Entry;

        //    var arg = new
        //    {
        //        user = UserName,
        //        pass = Password.MD5(),
        //        machine = Environment.MachineName,
        //        processid = Process.GetCurrentProcess().Id,
        //        version = asmx?.Version,
        //        compile = asmx?.Compile,
        //    };

        //    var rs = await base.InvokeWithClientAsync<Object>(client, "Login", arg);
        //    if (Setting.Current.Debug) XTrace.WriteLine("登录{0}成功！{1}", client, rs.ToJson());

        //    Logined = true;

        //    return Info = rs as IDictionary<String, Object>;
        //}
        #endregion

        #region 核心方法
        ///// <summary>上报服务</summary>
        ///// <param name="nameSpace"></param>
        ///// <param name="services"></param>
        ///// <returns></returns>
        //public async Task<Boolean> ReportAsync(String nameSpace, String[] services)
        //{
        //    return await InvokeAsync<Boolean>("Report", new { nameSpace, services });
        //}
        #endregion

        #region 辅助
//#if DEBUG
//        /// <summary>创建</summary>
//        /// <param name="svr"></param>
//        /// <returns></returns>
//        protected override ISocketClient OnCreate(String svr)
//        {
//            var client = base.OnCreate(svr);
//            if (client != null) client.Log = Log;

//            return client;
//        }
//#endif
        #endregion
    }
}