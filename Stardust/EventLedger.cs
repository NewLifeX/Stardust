using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using NewLife;
using NewLife.IO;
using NewLife.Log;
using NewLife.Remoting.Clients;
using NewLife.Remoting.Models;

namespace Stardust;

/// <summary>本地事件台账</summary>
/// <remarks>
/// 事件以 CSV 存放在 Data 目录的台账文件中，每行四列 Time,Type,Name,Remark，无表头。
/// 客户端把上报失败的事件追加进台账；心跳成功后取走台账上报，成功删除，失败保留等待下一次重试。
/// 节点与应用各用各的台账文件，避免同一目录下互相串扰；台账允许其它程序按同样格式追加，本类不区分来源。
/// </remarks>
public static class EventLedger
{
    /// <summary>节点事件台账文件名</summary>
    public const String NodeEvents = "NodeEvents.csv";

    /// <summary>应用事件台账文件名</summary>
    public const String AppEvents = "AppEvents.csv";

    /// <summary>获取节点台账目录</summary>
    /// <returns>代理目录下的 Data 目录</returns>
    public static String GetDirectory() => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");

    /// <summary>读取台账。忽略列数不足 4、Time 非数字、Type/Name 为空的行</summary>
    /// <param name="file">台账文件路径</param>
    /// <param name="max">单次最多读取条数</param>
    /// <returns>事件列表，文件不存在时返回空列表</returns>
    public static List<EventModel> Read(String file, Int32 max = Int32.MaxValue)
    {
        var list = new List<EventModel>();
        if (file.IsNullOrEmpty() || !File.Exists(file) || max <= 0) return list;

        using var csv = new CsvFile(file);
        foreach (var fields in csv.ReadAll())
        {
            if (list.Count >= max) break;
            if (fields == null || fields.Length < 4) continue;

            // CsvFile 写长整数时会加前导制表符，读取方统一去掉
            var timeText = fields[0].TrimStart('\t').Trim();
            if (!Int64.TryParse(timeText, out var time) || time <= 0) continue;

            var type = fields[1]?.Trim();
            var name = fields[2]?.Trim();
            if (type.IsNullOrEmpty() || name.IsNullOrEmpty()) continue;

            list.Add(new EventModel { Time = time, Type = type, Name = name, Remark = fields[3] });
        }

        return list;
    }

    /// <summary>取走待上报台账</summary>
    /// <remarks>
    /// 优先返回上次取走但尚未上报成功的台账；否则加锁把台账改名为待上报文件。
    /// 加锁后只做改名，解锁后再读取上报，避免长时间持锁阻塞其它写入方。取不到锁时返回 null，下次心跳重试。
    /// </remarks>
    /// <param name="dir">台账目录</param>
    /// <param name="fileName">台账文件名</param>
    /// <returns>待上报台账路径；没有可取文件时返回 null</returns>
    public static String? TakePending(String dir, String fileName)
    {
        if (dir.IsNullOrEmpty() || fileName.IsNullOrEmpty()) return null;

        var reporting = Path.Combine(dir, GetReportingName(fileName));
        if (File.Exists(reporting)) return reporting;

        var file = Path.Combine(dir, fileName);
        if (!File.Exists(file)) return null;

        using var guard = EventFileLock.Acquire(Path.Combine(dir, GetLockName(fileName)));
        if (guard == null) return null;

        try
        {
            if (File.Exists(file))
            {
                Move(file, reporting);
                return reporting;
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }

        return null;
    }

    /// <summary>删除已成功上报的台账</summary>
    /// <param name="file">台账路径</param>
    public static void Complete(String file)
    {
        try
        {
            if (!file.IsNullOrEmpty() && File.Exists(file)) File.Delete(file);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
    }

    /// <summary>向台账追加一条事件</summary>
    /// <param name="dir">台账目录</param>
    /// <param name="fileName">台账文件名</param>
    /// <param name="type">事件类型，如 info/error</param>
    /// <param name="name">事件名称</param>
    /// <param name="remark">事件内容</param>
    public static void Append(String dir, String fileName, String type, String name, String remark)
    {
        if (dir.IsNullOrEmpty() || fileName.IsNullOrEmpty() || type.IsNullOrEmpty() || name.IsNullOrEmpty()) return;

        try
        {
            Directory.CreateDirectory(dir);
            using var guard = EventFileLock.Acquire(Path.Combine(dir, GetLockName(fileName)));
            if (guard == null) return;

            var time = (Int64)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            using var stream = new FileStream(Path.Combine(dir, fileName), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using (var csv = new CsvFile(stream, leaveOpen: true))
            {
                csv.WriteLine(time, type, name, remark);
            }
            stream.Flush(true);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
    }

    /// <summary>上报本地事件台账</summary>
    /// <remarks>
    /// 未登录或服务端不支持事件时保留台账；取走台账后分批上报，全部成功则删除，
    /// 失败时保留待上报文件，并把失败事件追加回台账，等待下一次心跳重试。
    /// </remarks>
    /// <param name="client">星尘客户端，用于判断登录状态与上报事件</param>
    /// <param name="dir">台账目录</param>
    /// <param name="fileName">台账文件名</param>
    /// <param name="failName">上报失败时写入台账的事件名</param>
    /// <returns>成功上报的事件数；未登录、无事件能力或没有台账时返回 0</returns>
    public static async Task<Int32> ReportAsync(ClientBase client, String dir, String fileName, String failName)
    {
        if (client == null || dir.IsNullOrEmpty() || fileName.IsNullOrEmpty()) return 0;

        // 未登录或服务端不支持事件时保留台账，等下一次心跳
        if (!client.Logined || !client.Features.HasFlag(Features.PostEvent)) return 0;

        var log = client.Log;
        try
        {
            var file = TakePending(dir, fileName);
            if (file.IsNullOrEmpty()) return 0;

            var list = Read(file);
            if (list.Count == 0)
            {
                Complete(file);
                log?.Info("Local event ledger contained no valid rows: {0}", file);
                return 0;
            }

            const Int32 BatchSize = 100;
            var total = 0;
            for (var i = 0; i < list.Count; i += BatchSize)
            {
                var count = Math.Min(BatchSize, list.Count - i);
                var batch = new EventModel[count];
                list.CopyTo(i, batch, 0, count);

                var n = await client.PostEvents(batch).ConfigureAwait(false);
                if (n <= 0)
                {
                    log?.Info("Server accepted 0 of {0} event(s) in batch {1}; ledger kept for retry", count, i / BatchSize + 1);
                    AppendFail(dir, fileName, failName, "server accepted 0");
                    return total;
                }
                total += n;
            }

            Complete(file);
            log?.Info("Reported {0} event(s) from local ledger", total);
            if (total < list.Count) log?.Info("Server accepted {0}/{1} event(s)", total, list.Count);

            return total;
        }
        catch (Exception ex)
        {
            log?.Info("Failed to report local events: {0}", ex.Message);
            AppendFail(dir, fileName, failName, ex.Message);
            return 0;
        }
    }

    private static readonly ConcurrentDictionary<String, DateTime> _lastFails = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>上报失败时把失败事件写回台账，随下一次成功心跳补报</summary>
    /// <param name="dir">台账目录</param>
    /// <param name="fileName">台账文件名</param>
    /// <param name="failName">事件名</param>
    /// <param name="reason">失败原因</param>
    private static void AppendFail(String dir, String fileName, String failName, String reason)
    {
        // 服务端长期不可用时，避免每轮心跳都追加失败事件把台账撑大
        var key = Path.Combine(dir, fileName);
        var now = DateTime.Now;
        if (_lastFails.TryGetValue(key, out var last) && (now - last).TotalMinutes < 10) return;
        _lastFails[key] = now;

        Append(dir, fileName, "error", failName.IsNullOrEmpty() ? "ReportFailed" : failName, reason);
    }

    private static String GetReportingName(String fileName) => Path.GetFileNameWithoutExtension(fileName) + ".reporting" + Path.GetExtension(fileName);

    private static String GetLockName(String fileName) => Path.GetFileNameWithoutExtension(fileName) + ".lock";

    private static void Move(String src, String dst)
    {
        if (File.Exists(dst)) File.Delete(dst);
        File.Move(src, dst);
    }
}

/// <summary>台账文件排他锁。Linux 用 flock 锁文件，跨进程生效</summary>
internal sealed class EventFileLock : IDisposable
{
    private FileStream? _stream;
    private SafeFileHandle? _handle;
    private Int32 _fd;
    private Boolean _flocked;

    /// <summary>取锁，最多等待 1 秒</summary>
    /// <param name="path">锁文件路径</param>
    /// <returns>锁对象；取不到时返回 null</returns>
    public static EventFileLock? Acquire(String path)
    {
        try
        {
            if (Runtime.Linux)
            {
#if NET6_0_OR_GREATER
                var handle = File.OpenHandle(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                var fd = handle.DangerousGetHandle().ToInt32();
                for (var i = 0; i < 20; i++)
                {
                    if (Flock(fd, LOCK_EX | LOCK_NB) == 0)
                        return new EventFileLock { _handle = handle, _fd = fd, _flocked = true };

                    Thread.Sleep(50);
                }

                handle.Dispose();
                return null;
#else
                return null;
#endif
            }

            var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return new EventFileLock { _stream = stream };
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return null;
        }
    }

    /// <summary>释放锁</summary>
    public void Dispose()
    {
#if NET6_0_OR_GREATER
        if (_flocked) Flock(_fd, LOCK_UN);
#endif
        _flocked = false;

        _handle?.Dispose();
        _handle = null;
        _stream?.Dispose();
        _stream = null;
    }

#if NET6_0_OR_GREATER
    private const Int32 LOCK_EX = 2;
    private const Int32 LOCK_NB = 4;
    private const Int32 LOCK_UN = 8;

    [DllImport("libc", EntryPoint = "flock", SetLastError = true)]
    private static extern Int32 Flock(Int32 fd, Int32 operation);
#endif
}
