using System;
using System.Collections.Generic;
using NewLife.Common;
using NewLife.Log;
using NewLife.Remoting;

namespace Stardust.Monitors
{
    /// <summary>星尘性能跟踪器，跟踪数据提交到星尘平台</summary>
    /// <remarks>其它项目有可能直接使用这个类代码，用于提交监控数据</remarks>
    public class StarTracer : DefaultTracer
    {
        #region 属性
        /// <summary>应用标识</summary>
        public String AppId { get; set; }

        /// <summary>应用名</summary>
        public String AppName { get; set; }

        /// <summary>最大失败数。超过该数时，新的数据将被抛弃，默认120</summary>
        public Int32 MaxFails { get; set; } = 120;

        /// <summary>Api客户端</summary>
        public IApiClient Client { get; set; }

        private Queue<TraceModel> _fails = new Queue<TraceModel>();
        #endregion

        /// <summary>实例化</summary>
        public StarTracer()
        {
            Period = 60;

            var sys = SysConfig.Current;
            AppId = sys.Name;
            AppName = sys.DisplayName;
        }

        /// <summary>指定服务端地址来实例化跟踪器</summary>
        /// <param name="server"></param>
        public StarTracer(String server) : this()
        {
            var http = new ApiHttpClient(server)
            {
                Tracer = this
            };
            Client = http;
        }

        /// <summary>处理Span集合。默认输出日志，可重定义输出控制台</summary>
        protected override void ProcessSpans(ISpanBuilder[] builders)
        {
            if (builders == null) return;

            // 发送，失败后进入队列
            var model = new TraceModel { AppId = AppId, AppName = AppName, Builders = builders };
            try
            {
                var rs = Client.Invoke<TraceResponse>("Trace/Report", model);
                // 处理响应参数
                if (rs != null)
                {
                    if (rs.Period > 0) Period = rs.Period;
                    if (rs.MaxSamples > 0) MaxSamples = rs.MaxSamples;
                    if (rs.MaxErrors > 0) MaxErrors = rs.MaxErrors;
                }
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
                //throw;

                if (_fails.Count < MaxFails) _fails.Enqueue(model);
                return;
            }

            // 如果发送成功，则继续发送以前失败的数据
            while (_fails.Count > 0)
            {
                model = _fails.Dequeue();
                try
                {
                    Client.Invoke<Object>("Trace/Report", model);
                }
                catch
                {
                    _fails.Enqueue(model);
                    break;
                }
            }
        }
    }
}