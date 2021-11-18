using System;
using System.IO;
using NewLife.Log;
using Stardust.Models;
#if NET40_OR_GREATER
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
#endif

namespace Stardust.Services
{
    /// <summary>追踪服务</summary>
    public class TraceService
    {
        /// <summary>初始化</summary>
        public void Attach(IQueueService<CommandModel, Byte[]> queue)
        {
            queue.Subscribe("截屏", DoCapture);
            queue.Subscribe("抓日志", DoGetLog);
        }

        private Byte[] DoCapture(CommandModel command)
        {
#if NET40_OR_GREATER
            if (command.Expire.Year < 2000 || command.Expire > DateTime.Now)
            {
                return GetScreenCapture();
            }
#endif

            return null;
        }

        private Byte[] DoGetLog(CommandModel command)
        {
            if (command.Expire.Year < 2000 || command.Expire > DateTime.Now)
            {
                return GetLog();
            }

            return null;
        }

#if NET40_OR_GREATER
        private Byte[] GetScreenCapture()
        {
            // 获取dpi，需要 app.manifest 打开感知dpi
            //var sys = Graphics.FromHwnd(IntPtr.Zero);
            //var factor = sys.DpiX / 96;

            var screen = Screen.PrimaryScreen;
            var w = screen.Bounds.Width;
            var h = screen.Bounds.Height;
            //var w = (Int32)(screen.Bounds.Width * factor);
            //var h = (Int32)(screen.Bounds.Height * factor);
            var rect = new Rectangle(0, 0, w, h);

            var bmp = new Bitmap(rect.Width, rect.Height);
            //bmp.SetResolution(sys.DpiX, sys.DpiY);

            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(0, 0, 0, 0, rect.Size);
            g.DrawImage(bmp, 0, 0, rect, GraphicsUnit.Pixel);

            var s = new MemoryStream();
            bmp.Save(s, ImageFormat.Png);

            return s.ToArray();
        }
#endif

        #region 辅助
        private static Byte[] GetLog(Int64 getBytes = 1024 * 1024)
        {
            var logPath = XTrace.LogPath.CombinePath($"{DateTime.Now:yyyy_MM_dd}.log").GetBasePath();

            try
            {
                if (!File.Exists(logPath)) throw new Exception($"日志文件不存在！{logPath}");

                var fi = new FileInfo(logPath);
                getBytes = getBytes > fi.Length ? fi.Length : getBytes;
                var buffArr = new Byte[getBytes];
                using (var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    // 读取最后多少字节
                    fs.Seek(-getBytes, SeekOrigin.End);
                    fs.Read(buffArr, 0, (Int32)getBytes);
                }

                return buffArr;
            }
            catch (Exception ex)
            {
                XTrace.WriteLine($"取日志失败！{ex}");
            }

            return new Byte[0];
        }
        #endregion
    }
}