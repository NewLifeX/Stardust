using System;
using System.Collections.Generic;
using System.Diagnostics;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;

namespace Stardust
{
    /// <summary>本地星尘客户端。连接本机星尘代理StarAgent</summary>
    public class LocalStarClient
    {
        #region 属性
        private ApiClient _client;
        #endregion

        #region 构造
        #endregion

        #region 方法
        private void Init()
        {
            if (_client != null) return;

            _client = new ApiClient("udp://127.0.0.1:5500")
            {
                Log = XTrace.Log,
            };

            var set = Setting.Current;
            if (set.Debug) _client.EncoderLog = XTrace.Log;
        }

        /// <summary>获取信息</summary>
        /// <returns></returns>
        public IDictionary<String, Object> GetInfo()
        {
            Init();

            return _client.Invoke<Object>("Api/Info") as IDictionary<String, Object>;
        }
        #endregion

        #region 进程控制
        /// <summary>自杀并重启</summary>
        /// <returns></returns>
        public Boolean KillAndRestartMySelf()
        {
            Init();

            var p = Process.GetCurrentProcess();
            var fileName = p.MainModule.FileName;

            // 发起命令
            var rs = _client.Invoke<String>("KillAndStart", new
            {
                processId = p.Id,
                delay = 3,
                fileName = fileName,
                arguments = Environment.CommandLine,
                workingDirectory = Environment.CurrentDirectory,
            });

            // 本进程退出
            //p.Kill();

            return !rs.IsNullOrEmpty();
        }
        #endregion

        #region 安装星尘代理
        /// <summary>探测并安装星尘代理</summary>
        /// <param name="url"></param>
        /// <param name="version"></param>
        public Boolean TestAndInstall(String url, String version)
        {
            if (url.IsNullOrEmpty()) throw new ArgumentNullException(nameof(url));
        }
        #endregion
    }
}