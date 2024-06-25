using NewLife.Log;
using NewLife;
using NewLife.Remoting.Clients;

namespace Stardust.Services;

/// <summary>追踪服务</summary>
public class TraceService
{
    /// <summary>初始化</summary>
    public void Attach(ICommandClient client)
    {
        client.RegisterCommand("截屏", DoCapture);
        client.RegisterCommand("抓日志", DoGetLog);
    }

    private String? DoCapture(String? command)
    {
#if NET40_OR_GREATER || WINDOWS
        // 获取dpi，需要 app.manifest 打开感知dpi
        //var sys = Graphics.FromHwnd(IntPtr.Zero);
        //var factor = sys.DpiX / 96;

        var screen = System.Windows.Forms.Screen.PrimaryScreen;
        if (screen == null) return null;

        var w = screen.Bounds.Width;
        var h = screen.Bounds.Height;
        //var w = (Int32)(screen.Bounds.Width * factor);
        //var h = (Int32)(screen.Bounds.Height * factor);
        var rect = new System.Drawing.Rectangle(0, 0, w, h);

        var bmp = new System.Drawing.Bitmap(rect.Width, rect.Height);
        //bmp.SetResolution(sys.DpiX, sys.DpiY);

        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.CopyFromScreen(0, 0, 0, 0, rect.Size);
        g.DrawImage(bmp, 0, 0, rect, System.Drawing.GraphicsUnit.Pixel);

        var s = new MemoryStream();
        bmp.Save(s, System.Drawing.Imaging.ImageFormat.Png);

        return s.ToArray().ToBase64();
#else
        return null;
#endif
    }

    private String? DoGetLog(String? arg)
    {
        var logPath = XTrace.LogPath.CombinePath($"{DateTime.Now:yyyy_MM_dd}.log").GetBasePath();

        try
        {
            if (!File.Exists(logPath)) throw new Exception($"日志文件不存在！{logPath}");

            var fi = new FileInfo(logPath);
            Int64 getBytes = 1024 * 1024;
            getBytes = getBytes > fi.Length ? fi.Length : getBytes;
            var buffArr = new Byte[getBytes];
            using (var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                // 读取最后多少字节
                fs.Seek(-getBytes, SeekOrigin.End);
                fs.Read(buffArr, 0, (Int32)getBytes);
            }

            return buffArr.ToStr();
        }
        catch (Exception ex)
        {
            XTrace.WriteLine($"取日志失败！{ex}");
        }

        return null;
    }
}