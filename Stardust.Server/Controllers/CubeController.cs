using System.ComponentModel;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Remoting;
using NewLife.Remoting.Extensions;
using Stardust.Data.Deployment;
using Stardust.Extensions;
using Stardust.Server.Services;
using Stardust.Storages;

namespace Stardust.Server.Controllers;

/// <summary>魔方前端数据接口</summary>
[DisplayName("数据接口")]
[ApiController]
[ApiFilter]
[Route("{controller}/{action}")]
public class CubeController(IFileStorage fileStorage, StarServerSetting setting, DownloadTokenService downloadTokenService) : ControllerBase
{
    #region 附件
    /// <summary>校验附件访问。ValidateAttachment开启时，公开分类可匿名，其余需携带短时下载令牌（安全V-11）</summary>
    /// <param name="att">附件</param>
    private void CheckAttachment(Attachment att)
    {
        if (!setting.ValidateAttachment) return;

        var cat = att.Category;
        if (!cat.IsNullOrEmpty())
        {
            var cats = (setting.PublicAttachmentCategories + "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (cats.Any(e => e.EqualIgnoreCase(cat))) return;
        }

        var token = HttpContext.Request.Query["token"] + "";
        if (!downloadTokenService.Valid(att.Id, token))
            throw new ApiException(ApiCode.Unauthorized, L.T("Not authorized to download the attachment, or the download token is invalid or expired."));
    }

    private async Task<(Attachment att, String filePath)> GetFile(String id)
    {
        if (id.IsNullOrEmpty()) throw new ApiException(ApiCode.NotFound, L.T("Invalid attachment ID"));

        // 去掉仅用于装饰的后缀名
        var p = id.IndexOf('.');
        if (p > 0) id = id[..p];

        var att = Attachment.FindById(id.ToLong());
        if (att == null) throw new ApiException(ApiCode.NotFound, L.T("Attachment information not found"));

        // 安全V-11：附件下载鉴权（公开分类 / 短时下载令牌）
        CheckAttachment(att);

        //var set = StarServerSetting.Current;

        // 如果附件不存在，则抓取
        var filePath = att.GetFilePath(setting.UploadPath);
        if (!filePath.IsNullOrEmpty() && !System.IO.File.Exists(filePath) && setting.FileStorageFetch)
        {
            // 如果本地文件不存在，则从分布式文件存储获取
            await fileStorage.RequestFileAsync(att.Id, att.FilePath, "file not found");
            await Task.Delay(5_000);
        }
        if (filePath.IsNullOrEmpty() || !System.IO.File.Exists(filePath))
        {
            var url = att.Source;
            if (url.IsNullOrEmpty()) throw new ApiException(ApiCode.NotFound, L.T("Attachment file not found"));

            var rs = await att.Fetch(url, setting.UploadPath);
            if (!rs) throw new ApiException(ApiCode.NotFound, L.T("Failed to fetch the attachment remotely"));

            filePath = att.GetFilePath(setting.UploadPath);
        }
        if (filePath.IsNullOrEmpty() || !System.IO.File.Exists(filePath)) throw new ApiException(ApiCode.NotFound, L.T("Attachment file not found"));

        return (att, filePath);
    }

    /// <summary>访问图片</summary>
    /// <param name="id">附件编号</param>
    /// <returns></returns>
    [AllowAnonymous]
    public async Task<ActionResult> Image(String id)
    {
        if (id.IsNullOrEmpty()) return NotFound(L.T("Invalid attachment ID"));

        try
        {
            var (att, filePath) = await GetFile(id);

            att.Downloads++;
            att.LastDownload = DateTime.Now;
            att.SaveAsync(5_000);

            // 设置文件哈希相关响应头
            Response.SetFileHashHeaders(att.Hash);

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
        if (id.IsNullOrEmpty()) return NotFound(L.T("Invalid attachment ID"));

        try
        {
            var (att, filePath) = await GetFile(id);

            att.Downloads++;
            att.LastDownload = DateTime.Now;
            att.SaveAsync(5_000);

            // 设置文件哈希相关响应头
            Response.SetFileHashHeaders(att.Hash);

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