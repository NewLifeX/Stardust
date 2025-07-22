#if NET5_0_OR_GREATER
using NewLife;
using NewLife.Log;
using NewLife.Reflection;

namespace Stardust.Monitors;

/// <summary>MongoDb诊断监听器</summary>
public class MongoDbDiagnosticListener : TraceDiagnosticListener
{
    /// <summary>实例化</summary>
    public MongoDbDiagnosticListener() => Name = "MongoDB.Driver";

    /// <summary>下一步</summary>
    /// <param name="value"></param>
    public override void OnNext(KeyValuePair<String, Object?> value)
    {
        if (Tracer == null) return;

        // 前缀可能是 Microsoft.Data.SqlClient. 或 System.Data.SqlClient.
        var name = value.Key.Split(".").LastOrDefault();

        var span = DefaultSpan.Current;
        var spanName = span?.Name;

        switch (name)
        {
            case "CommandStart":
                if (value.Value != null && value.Value.GetValue("CommandName") is String commandName)
                {
                    var dbName = value.Value.GetValue("DatabaseNamespace")?.GetValue("DatabaseName")?.ToString();
                    var traceName = $"mongo:{dbName}:{commandName}";

                    var command = value.Value.GetValue("Command")?.ToString();

                    Tracer.NewSpan(traceName, command);
                }

                break;

            case "CommandEnd":
                if (span != null && !spanName.IsNullOrEmpty() && spanName.StartsWith("mongo:"))
                {
                    span.Dispose();
                }

                break;

            case "CommandFail":
                if (span != null && !spanName.IsNullOrEmpty() && spanName.StartsWith("mongo:"))
                {
                    if (value.Value != null && value.Value.GetValue("Exception") is Exception ex) span.SetError(ex, null);

                    span.Dispose();
                }
                break;
        }
    }
}
#endif