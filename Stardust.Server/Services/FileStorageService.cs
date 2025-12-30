using NewLife;
using NewLife.Caching;
using NewLife.Data;
using NewLife.Log;
using Stardust.Data.Deployment;
using Stardust.Registry;
using Stardust.Services;
using Stardust.Storages;
using XCode;

namespace Stardust.Server.Services;

public class FileStorageService(IFileStorage fileStorage) : IHostedService
{
    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        // 延迟10秒后初始化，避免和其它服务争抢资源
        // 不阻塞 Host 启动：在后台执行
        _ = InitializeLaterAsync(cancellationToken);

        return Task.CompletedTask;
    }

    Task IHostedService.StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task InitializeLaterAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(10_000, cancellationToken);
        await fileStorage.InitializeAsync(cancellationToken);
    }
}

public static class FileStorageExtensions
{
    /// <summary>注册魔方版文件存储</summary>
    /// <param name="services"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static IServiceCollection AddCubeFileStorage(this IServiceCollection services, String name = null)
    {
        //services.AddSingleton<IFileStorage, CubeFileStorage>();
        //services.AddSingleton<IFileStorage>(new CubeFileStorage { Name = name });
        services.AddSingleton<CubeFileStorage>();
        services.AddSingleton<IFileStorage>(sp =>
        {
            var storage = sp.GetRequiredService<CubeFileStorage>();
            storage.Name = name;
            return storage;
        });

        services.AddHostedService<FileStorageService>();

        return services;
    }
}

public class CubeFileStorage : DefaultFileStorage
{
    private ICacheProvider _cacheProvider;
    private IRegistry _registry;

    public CubeFileStorage(StarServerSetting setting, IRegistry registry, IServiceProvider serviceProvider, ICacheProvider cacheProvider, ITracer tracer, ILog log)
    {
        //NodeName = Environment.MachineName;
        RootPath = setting.UploadPath;
        DownloadUri = "/cube/file?id={Id}";

        ServiceProvider = serviceProvider;
        Tracer = tracer;
        Log = log;

        _cacheProvider = cacheProvider;
        _registry = registry;
    }

    protected override Task OnInitializedAsync(CancellationToken cancellationToken)
    {
        // 优先Redis队列作为事件总线，其次使用星尘事件总线
        if (_cacheProvider.Cache is Redis)
            SetEventBus(_cacheProvider);
        else if (_registry is AppClient client)
            SetEventBus(client);
        else
            SetEventBus(_cacheProvider);

        return base.OnInitializedAsync(cancellationToken);
    }

    /// <summary>获取本地文件的元数据</summary>
    protected override IFileInfo GetLocalFileMeta(Int64 attachmentId, String path)
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

    public override async Task<Int32> ScanFilesAsync(DateTime startTime, CancellationToken cancellationToken = default)
    {
        if (FileRequestBus is StarEventBus<FileRequest> bus && !bus.IsReady) return -1;

        return await base.ScanFilesAsync(startTime, cancellationToken);
    }

    /// <summary>获取本地不存在的附件列表。用于文件同步</summary>
    /// <param name="startTime">从指定时间开始遍历</param>
    /// <returns></returns>
    protected override IEnumerable<IFileInfo> GetMissingAttachments(DateTime startTime)
    {
        var exp = new WhereExpression();
        if (startTime.Year > 2000) exp &= Attachment._.CreateTime >= startTime;

        var page = new PageParameter { PageIndex = 1, PageSize = 100, Sort = Attachment._.Id, Desc = true };

        while (true)
        {
            var list = Attachment.FindAll(exp, page);
            if (list == null || list.Count == 0) break;

            foreach (var att in list)
            {
                var filePath = att.GetFilePath(RootPath);
                if (!filePath.IsNullOrEmpty() && !File.Exists(filePath))
                {
                    yield return new NewFileInfo
                    {
                        Id = att.Id,
                        Name = att.FileName,
                        Path = att.FilePath,
                        Hash = att.Hash,
                        Length = att.Size,
                    };
                }
            }

            if (list.Count < page.PageSize) break;
            page.PageIndex++;
        }
    }
}
