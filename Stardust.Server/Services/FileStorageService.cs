using NewLife;
using NewLife.Caching;
using NewLife.Log;
using Stardust.Data.Deployment;
using Stardust.Storages;

namespace Stardust.Server.Services;

public class FileStorageService(IFileStorage fileStorage) : IHostedService
{
    async Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        await fileStorage.InitializeAsync(cancellationToken);
    }

    Task IHostedService.StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public static class FileStorageExtensions
{
    public static IServiceCollection AddCubeFileStorage(this IServiceCollection services)
    {
        services.AddSingleton<IFileStorage, CubeFileStorage>();

        services.AddHostedService<FileStorageService>();

        return services;
    }
}

public class CubeFileStorage : DefaultFileStorage
{
    public CubeFileStorage(StarServerSetting setting, IServiceProvider serviceProvider, ICacheProvider cacheProvider, ITracer tracer, ILog log)
    {
        //NodeName = Environment.MachineName;
        RootPath = setting.UploadPath;
        DownloadUri = "/cube/file?id={Id}";

        ServiceProvider = serviceProvider;
        Tracer = tracer;
        Log = log;

        SetEventBus(cacheProvider);
    }

    /// <summary>获取本地文件的元数据</summary>
    protected override IFileInfo GetLocalFileMeta(Int64 attachmentId, String? path)
    {
        //if (path.IsNullOrEmpty()) throw new ArgumentNullException(nameof(path));

        var att = Attachment.FindById(attachmentId);

        return new NewFileInfo
        {
            Id = att.Id,
            Name = att.FileName,
            Path = att.FilePath,
            Hash = att.Hash,
            Length = att.Size,
        };
    }

    protected override Int64[] GetMissingAttachmentIds(Int32 batchSize) => [];
}
