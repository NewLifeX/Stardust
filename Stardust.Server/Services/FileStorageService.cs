using System.Diagnostics;
using NewLife;
using NewLife.Caching;
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
    public CubeFileStorage(ICacheProvider cacheProvider)
    {
        var cache = cacheProvider.Cache as Cache;
        if (cache != null)
        {
            var clientId = $"{NetHelper.MyIP()}@{Process.GetCurrentProcess().Id}";
            NewFileBus = cache.GetEventBus<NewFileInfo>("NewFile", clientId);
            FileRequestBus = cache.GetEventBus<FileRequest>("FileRequest", clientId);
        }

        NodeName = Environment.MachineName;
        RootPath = "../Uploads";
        DownloadUri = "/cube/file?id={Id}";
    }

    protected override Int64[] GetMissingAttachmentIds(Int32 batchSize) => [];
}
