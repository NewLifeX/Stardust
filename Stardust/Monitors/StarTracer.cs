using System;
using System.Collections.Generic;
using NewLife.Log;
using NewLife.Remoting;

namespace Stardust.Monitors
{
    /// <summary>星尘性能跟踪器，跟踪数据提交到星尘平台</summary>
    /// <remarks>其它项目有可能直接使用这个类代码，用于提交监控数据</remarks>
    public class StarTracer : DefaultTracer
    {
        #region 属性
        /// <summary>Api客户端</summary>
        public IApiClient Client { get; set; }

        private Queue<ISpanBuilder[]> _fails = new Queue<ISpanBuilder[]>();
        #endregion

        /// <summary>实例化</summary>
        public StarTracer()
        {
            Period = 60;
        }

        /// <summary>处理Span集合。默认输出日志，可重定义输出控制台</summary>
        protected override void ProcessSpans(ISpanBuilder[] builders)
        {
            if (builders == null) return;

            // 发送，失败后进入队列
            try
            {
                Client.Invoke<Object>("Trace/Report", builders);
            }
            catch (Exception ex)
            {
                XTrace.WriteException(ex);
                //throw;

                _fails.Enqueue(builders);
                return;
            }

            // 如果发送成功，则继续发送以前失败的数据
            while (_fails.Count > 0)
            {
                builders = _fails.Dequeue();
                try
                {
                    Client.Invoke<Object>("Trace/Report", builders);
                }
                catch
                {
                    _fails.Enqueue(builders);
                    break;
                }
            }
        }
    }
}