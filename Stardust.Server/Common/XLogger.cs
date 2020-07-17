using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NewLife;
using System;

namespace Stardust.Server.Common
{
    static class XLoggerExtensions
    {
        public static ILoggingBuilder AddXLog(this ILoggingBuilder builder)
        {
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, XLoggerProvider>());

            return builder;
        }
    }

    [ProviderAlias("XLog")]
    class XLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(String categoryName)
        {
            var log = NewLife.Log.XTrace.Log;
            if (log is NewLife.Log.CompositeLog cp)
            {
                var tf = cp.Get<NewLife.Log.TextFileLog>();
                if (tf != null) log = tf;
            }

            return new XLogger { Logger = log };
        }

        public void Dispose() { }
    }

    class XLogger : ILogger
    {
        public NewLife.Log.ILog Logger { get; set; }

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public Boolean IsEnabled(LogLevel logLevel)
        {
            return Logger.Enable;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, String> formatter)
        {
            if (!Logger.Enable) return;

            if (formatter == null) throw new ArgumentNullException(nameof(formatter));

            var txt = formatter(state, exception);
            if (txt.IsNullOrEmpty() && exception == null) return;

            switch (logLevel)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                    Logger.Debug(txt);
                    break;
                case LogLevel.Information:
                    Logger.Info(txt);
                    break;
                case LogLevel.Warning:
                    Logger.Warn(txt);
                    break;
                case LogLevel.Error:
                    Logger.Error(txt);
                    break;
                case LogLevel.Critical:
                    Logger.Fatal(txt);
                    break;
                case LogLevel.None:
                    break;
                default:
                    break;
            }

            if (exception != null) Logger.Error("{0}", exception);
        }
    }
}