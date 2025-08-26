using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Remoting;
using Stardust.Data.Deployment;

namespace Stardust.Server.Controllers;

/// <summary>魔方前端数据接口</summary>
[DisplayName("数据接口")]
[ApiController]
[Route("{controller}/{action}")]
public class CubeController(StarServerSetting setting) : ControllerBase
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

    /// <summary>
    /// 访问图片附件
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [AllowAnonymous]
    public async Task<ActionResult> Image(String id)
    {
        if (id.IsNullOrEmpty()) return NotFound("非法附件编号");

        try
        {
            var (att, filePath) = await GetFile(id);

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

    /// <summary>
    /// 访问附件
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [AllowAnonymous]
    public async Task<ActionResult> File(String id)
    {
        if (id.IsNullOrEmpty()) return NotFound("非法附件编号");

        try
        {
            var (att, filePath) = await GetFile(id);

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