using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Remoting;
using Stardust.Data.Deployment;
using Stardust.Storages;

namespace Stardust.Server.Controllers;

/// <summary>魔方前端数据接口</summary>
[DisplayName("数据接口")]
[ApiController]
[Route("{controller}/{action}")]
public class CubeController(IFileStorage fileStorage, StarServerSetting setting) : ControllerBase
{
    #region 附件
    private async Task<(Attachment att, String filePath)> GetFile(String id)
    {
        if (id.IsNullOrEmpty()) throw new ApiException(ApiCode.NotFound, "非法附件编号");

        // 去掉仅用于装饰的后缀名
        var p = id.IndexOf('.');
        if (p > 0) id = id[..p];

        var att = Attachment.FindById(id.ToLong());
        if (att == null) throw new ApiException(ApiCode.NotFound, "找不到附件信息");

        //var set = StarServerSetting.Current;

        // 如果附件不存在，则抓取
        var filePath = att.GetFilePath(setting.UploadPath);
        if (!filePath.IsNullOrEmpty() && !System.IO.File.Exists(filePath))
        {
            // 如果本地文件不存在，则从分布式文件存储获取
            await fileStorage.RequestFileAsync(att.Id, att.FilePath, "file not found");
            await Task.Delay(5_000);
        }
        if (filePath.IsNullOrEmpty() || !System.IO.File.Exists(filePath))
        {
            var url = att.Source;
            if (url.IsNullOrEmpty()) throw new ApiException(ApiCode.NotFound, "找不到附件文件");

            var rs = await att.Fetch(url, setting.UploadPath);
            if (!rs) throw new ApiException(ApiCode.NotFound, "附件远程抓取失败");

            filePath = att.GetFilePath(setting.UploadPath);
        }
        if (filePath.IsNullOrEmpty() || !System.IO.File.Exists(filePath)) throw new ApiException(ApiCode.NotFound, "附件文件不存在");

        return (att, filePath);
    }

    /// <summary>设置文件哈希相关的响应头</summary>
    /// <param name="hash">文件哈希值，格式：[算法名$]哈希值，如MD5$abc123或abc123</param>
    private void SetFileHashHeaders(String hash)
    {
        if (hash.IsNullOrEmpty()) return;

        // 解析哈希算法名称和哈希值
        var algorithm = "MD5";
        var hashValue = hash;

        var dollarIndex = hash.IndexOf('$');
        if (dollarIndex > 0)
        {
            algorithm = hash[..dollarIndex];
            hashValue = hash[(dollarIndex + 1)..];
        }

        // 1. RFC 3230 标准 Digest 头
        Response.Headers["Digest"] = $"{algorithm}={hashValue}";

        // 2. X-Content-MD5（兼容某些客户端，总是用MD5）
        if (algorithm.EqualIgnoreCase("MD5"))
            Response.Headers["X-Content-MD5"] = hashValue;

        // 3. ETag（用于缓存验证）
        Response.Headers["ETag"] = $"\"{hashValue}\"";

        // 4. 自定义头（易于识别）
        Response.Headers["X-File-Hash"] = $"{algorithm}:{hashValue}";
    }

    /// <summary>访问图片</summary>
    /// <param name="id">附件编号</param>
    /// <returns></returns>
    [AllowAnonymous]
    public async Task<ActionResult> Image(String id)
    {
        if (id.IsNullOrEmpty()) return NotFound("非法附件编号");

        try
        {
            var (att, filePath) = await GetFile(id);

            att.Downloads++;
            att.LastDownload = DateTime.Now;
            att.SaveAsync(5_000);

            // 设置文件哈希相关响应头
            SetFileHashHeaders(att.Hash);

            if (!att.ContentType.IsNullOrEmpty())
                return PhysicalFile(filePath, att.ContentType, att.FileName);
            else
                return PhysicalFile(filePath, "image/png", att.FileName);
        }
        catch (ApiException ex) when (ex.Code == 404)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>访问附件</summary>
    /// <param name="id">附件编号</param>
    /// <returns></returns>
    [AllowAnonymous]
    public async Task<ActionResult> File(String id)
    {
        if (id.IsNullOrEmpty()) return NotFound("非法附件编号");

        try
        {
            var (att, filePath) = await GetFile(id);

            att.Downloads++;
            att.LastDownload = DateTime.Now;
            att.SaveAsync(5_000);

            // 设置文件哈希相关响应头
            SetFileHashHeaders(att.Hash);

            if (!att.ContentType.IsNullOrEmpty() && !att.ContentType.EqualIgnoreCase("application/octet-stream"))
                return PhysicalFile(filePath, att.ContentType, att.FileName);
            else
                return PhysicalFile(filePath, "application/octet-stream", att.FileName, true);
        }
        catch (ApiException ex) when (ex.Code == 404)
        {
            return NotFound(ex.Message);
        }
    }
    #endregion
}