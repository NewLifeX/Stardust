using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.Http;
using NewLife.Log;
using NewLife.Serialization;
using Stardust.Models;

namespace Stardust.Deployment;

/// <summary>星尘包本地仓库。负责下载、解压、版本目录与 current 指针</summary>
/// <remarks>
/// 目录结构：{Root}/{发布者}/{包名}/{版本}/star.json
/// current 文件记录当前版本，放在 {Root}/{发布者}/{包名}/current。
/// </remarks>
public class StarPackageStore
{
    #region 属性
    /// <summary>包根目录</summary>
    public String Root { get; set; } = null!;

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>链路追踪</summary>
    public ITracer? Tracer { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public StarPackageStore() { }

    /// <summary>实例化</summary>
    /// <param name="root">包根目录</param>
    public StarPackageStore(String root) => Root = root;
    #endregion

    #region 目录
    /// <summary>获取包目录</summary>
    /// <param name="name">包名，发布者/包名</param>
    /// <returns>包目录</returns>
    public virtual String GetPackageDir(String name)
    {
        var (publisher, shortName) = SplitName(name);

        return Path.Combine(GetRoot(), publisher, shortName);
    }

    /// <summary>获取版本目录</summary>
    /// <param name="name">包名</param>
    /// <param name="version">版本</param>
    /// <returns>版本目录</returns>
    public virtual String GetVersionDir(String name, String version) => Path.Combine(GetPackageDir(name), version);

    /// <summary>获取 current 文件路径</summary>
    /// <param name="name">包名</param>
    /// <returns>文件路径</returns>
    public virtual String GetCurrentFile(String name) => Path.Combine(GetPackageDir(name), "current");

    /// <summary>获取当前版本</summary>
    /// <param name="name">包名</param>
    /// <returns>当前版本，未安装返回 null</returns>
    public virtual String? GetCurrentVersion(String name)
    {
        var file = GetCurrentFile(name);
        if (!File.Exists(file)) return null;

        var version = File.ReadAllText(file).Trim();
        return version.IsNullOrEmpty() ? null : version;
    }

    /// <summary>设置当前版本</summary>
    /// <param name="name">包名</param>
    /// <param name="version">版本</param>
    public virtual void SetCurrentVersion(String name, String version)
    {
        var file = GetCurrentFile(name);
        file.EnsureDirectory(true);
        File.WriteAllText(file, version);
    }

    /// <summary>获取包根目录，未配置时使用用户缓存目录</summary>
    /// <returns>包根目录</returns>
    protected virtual String GetRoot()
    {
        if (!Root.IsNullOrEmpty()) return Root.GetFullPath();

        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (baseDir.IsNullOrEmpty()) baseDir = Path.GetTempPath();

        return Path.Combine(baseDir, ".stardust", "packages");
    }

    private static (String publisher, String shortName) SplitName(String name)
    {
        if (name.IsNullOrEmpty()) throw new ArgumentNullException(nameof(name));

        var index = name.LastIndexOf('/');
        if (index <= 0 || index >= name.Length - 1) throw new ArgumentException($"Invalid package name [{name}], expected publisher/name.", nameof(name));

        var publisher = name[..index];
        var shortName = name[(index + 1)..];

        if (!StarPackage.IsSafeRelativePath(publisher) || !StarPackage.IsSafeRelativePath(shortName))
            throw new ArgumentException($"Invalid package name [{name}].", nameof(name));

        return (publisher, shortName);
    }
    #endregion

    #region 查询
    /// <summary>包是否已安装</summary>
    /// <param name="name">包名</param>
    /// <param name="version">版本，为空时判断是否有任意版本</param>
    /// <returns>是否已安装</returns>
    public virtual Boolean IsInstalled(String name, String? version = null)
    {
        if (version.IsNullOrEmpty()) return List().Any(e => e.Name.EqualIgnoreCase(name));

        var dir = GetVersionDir(name, version);
        return File.Exists(Path.Combine(dir, "star.json"));
    }

    /// <summary>加载已安装的包清单</summary>
    /// <param name="name">包名</param>
    /// <param name="version">版本，为空时使用 current 或最后一个版本</param>
    /// <returns>包清单，未安装返回 null</returns>
    public virtual StarPackage? LoadManifest(String name, String? version = null)
    {
        version ??= GetCurrentVersion(name) ?? FindLastVersion(name);
        if (version.IsNullOrEmpty()) return null;

        var file = Path.Combine(GetVersionDir(name, version), "star.json");
        if (!File.Exists(file)) return null;

        return StarPackage.Load(file);
    }

    /// <summary>查找最后一个版本目录</summary>
    /// <param name="name">包名</param>
    /// <returns>版本，未找到返回 null</returns>
    public virtual String? FindLastVersion(String name)
    {
        var dir = GetPackageDir(name);
        if (!Directory.Exists(dir)) return null;

        return Directory.GetDirectories(dir)
            .Select(Path.GetFileName)
            .Where(e => !e.IsNullOrEmpty() && e != ".tmp")
            .OrderByDescending(e => e)
            .FirstOrDefault();
    }

    /// <summary>列出已安装的包</summary>
    /// <returns>包列表</returns>
    public virtual IList<StarPackageInfo> List()
    {
        var list = new List<StarPackageInfo>();
        var root = GetRoot();
        if (!Directory.Exists(root)) return list;

        foreach (var publisherDir in Directory.GetDirectories(root))
        {
            var publisher = Path.GetFileName(publisherDir);
            if (publisher.IsNullOrEmpty() || publisher.StartsWith(".")) continue;

            foreach (var packageDir in Directory.GetDirectories(publisherDir))
            {
                var shortName = Path.GetFileName(packageDir);
                if (shortName.IsNullOrEmpty() || shortName.StartsWith(".")) continue;

                var name = $"{publisher}/{shortName}";
                var current = GetCurrentVersion(name);
                foreach (var versionDir in Directory.GetDirectories(packageDir))
                {
                    var version = Path.GetFileName(versionDir);
                    if (version.IsNullOrEmpty() || version == ".tmp") continue;

                    var file = Path.Combine(versionDir, "star.json");
                    if (!File.Exists(file)) continue;

                    StarPackage? package = null;
                    try
                    {
                        package = StarPackage.Load(file);
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("Failed to load package manifest [{0}]: {1}", file, ex.Message);
                        continue;
                    }

                    list.Add(new StarPackageInfo
                    {
                        Name = name,
                        Version = version,
                        Path = versionDir,
                        IsCurrent = version.EqualIgnoreCase(current),
                        InstallTime = Directory.GetCreationTime(versionDir),
                        Manifest = package,
                    });
                }
            }
        }

        return list.OrderBy(e => e.Name).ThenByDescending(e => e.Version).ToList();
    }
    #endregion

    #region 安装
    /// <summary>安装包。已存在同一版本时直接返回</summary>
    /// <param name="name">包名</param>
    /// <param name="version">版本</param>
    /// <param name="source">下载地址或本地 zip 文件</param>
    /// <param name="hash">期望 MD5，可为空</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>包清单</returns>
    public virtual async Task<StarPackage> InstallAsync(String name, String version, String source, String? hash = null, CancellationToken cancellationToken = default)
    {
        if (name.IsNullOrEmpty()) throw new ArgumentNullException(nameof(name));
        if (version.IsNullOrEmpty()) throw new ArgumentNullException(nameof(version));
        if (source.IsNullOrEmpty()) throw new ArgumentNullException(nameof(source));

        var versionDir = GetVersionDir(name, version);
        var manifestFile = Path.Combine(versionDir, "star.json");
        if (File.Exists(manifestFile))
        {
            var installed = StarPackage.Load(manifestFile);
            if (installed.Name.EqualIgnoreCase(name) && installed.Version == version)
            {
                // 已安装的版本也要切到 current，否则回滚后 current 仍指向旧版本
                SetCurrentVersion(name, version);
                return installed;
            }
        }

        var zipFile = await PrepareZipAsync(source, hash, cancellationToken).ConfigureAwait(false);
        try
        {
            return ExtractZip(zipFile, name, version);
        }
        finally
        {
            TryDelete(zipFile);
        }
    }

    /// <summary>按服务端提供的清单安装包。zip 内可以不包含 star.json</summary>
    /// <param name="name">包名</param>
    /// <param name="version">版本</param>
    /// <param name="source">下载地址或本地 zip 文件</param>
    /// <param name="manifest">服务端清单</param>
    /// <param name="hash">期望 MD5，可为空</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>包清单</returns>
    public virtual async Task<StarPackage> InstallAsync(String name, String version, String source, StarPackage manifest, String? hash = null, CancellationToken cancellationToken = default)
    {
        if (manifest == null) throw new ArgumentNullException(nameof(manifest));
        if (!manifest.Name.EqualIgnoreCase(name)) throw new InvalidOperationException($"Package name mismatch: {manifest.Name} != {name}.");
        if (!manifest.Version.EqualIgnoreCase(version)) throw new InvalidOperationException($"Package version mismatch: {manifest.Version} != {version}.");

        var versionDir = GetVersionDir(name, version);
        var manifestFile = Path.Combine(versionDir, "star.json");
        if (File.Exists(manifestFile))
        {
            // 已安装也要用服务端清单覆盖，保证运行参数与发布中心一致
            File.WriteAllText(manifestFile, manifest.ToJson());
            SetCurrentVersion(name, version);
            return manifest;
        }

        var zipFile = await PrepareZipAsync(source, hash, cancellationToken).ConfigureAwait(false);
        try
        {
            return ExtractZip(zipFile, manifest, hash);
        }
        finally
        {
            TryDelete(zipFile);
        }
    }

    /// <summary>下载包 zip 到本地临时目录，不做安装</summary>
    /// <param name="source">下载地址或本地 zip 文件</param>
    /// <param name="hash">期望 MD5，可为空</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>临时 zip 路径</returns>
    public virtual Task<String> DownloadAsync(String source, String? hash = null, CancellationToken cancellationToken = default) => PrepareZipAsync(source, hash, cancellationToken);

    /// <summary>从本地 zip 安装包。版本从 star.json 读取</summary>
    /// <param name="zipFile">本地 zip 文件</param>
    /// <param name="hash">期望 MD5，可为空</param>
    /// <returns>包清单</returns>
    public virtual StarPackage InstallFromFile(String zipFile, String? hash = null)
    {
        if (zipFile.IsNullOrEmpty()) throw new ArgumentNullException(nameof(zipFile));
        if (!File.Exists(zipFile)) throw new FileNotFoundException("Package file not found.", zipFile);

        if (!hash.IsNullOrEmpty())
        {
            var actual = new FileInfo(zipFile).MD5().ToHex();
            if (!actual.EqualIgnoreCase(hash)) throw new InvalidOperationException($"Package hash mismatch: {actual} != {hash}.");
        }

        // 先解压到临时目录，读取清单后再确定最终目录
        var temp = CreateTempDir();
        try
        {
            ExtractZipTo(zipFile, temp);
            var manifest = LoadLocalManifest(temp);
            var errors = manifest.Validate();
            if (errors.Count > 0) throw new InvalidOperationException($"Invalid package manifest: {errors.Join("; ")}");

            var name = manifest.Name!;
            var version = manifest.Version!;
            var versionDir = GetVersionDir(name, version);
            if (Directory.Exists(versionDir)) Directory.Delete(versionDir, true);

            versionDir.EnsureDirectory(true);
            MoveDirectory(temp, versionDir);
            SetCurrentVersion(name, version);

            return StarPackage.Load(Path.Combine(versionDir, "star.json"));
        }
        finally
        {
            TryDelete(temp);
        }
    }

    /// <summary>按服务端清单从本地 zip 安装包。zip 内可以不包含 star.json</summary>
    /// <param name="zipFile">本地 zip 文件</param>
    /// <param name="manifest">服务端清单</param>
    /// <param name="hash">期望 MD5，可为空</param>
    /// <returns>包清单</returns>
    public virtual StarPackage InstallFromFile(String zipFile, StarPackage manifest, String? hash = null)
    {
        if (zipFile.IsNullOrEmpty()) throw new ArgumentNullException(nameof(zipFile));
        if (!File.Exists(zipFile)) throw new FileNotFoundException("Package file not found.", zipFile);
        if (manifest == null) throw new ArgumentNullException(nameof(manifest));

        if (!hash.IsNullOrEmpty())
        {
            var actual = new FileInfo(zipFile).MD5().ToHex();
            if (!actual.EqualIgnoreCase(hash)) throw new InvalidOperationException($"Package hash mismatch: {actual} != {hash}.");
        }

        var temp = CreateTempDir();
        try
        {
            ExtractZipTo(zipFile, temp);

            return InstallExtracted(temp, manifest);
        }
        finally
        {
            TryDelete(temp);
        }
    }

    /// <summary>解压 zip 到版本目录并校验清单</summary>
    /// <param name="zipFile">zip 文件</param>
    /// <param name="name">期望包名，为空时使用清单包名</param>
    /// <param name="version">期望版本，为空时使用清单版本</param>
    /// <returns>包清单</returns>
    public virtual StarPackage ExtractZip(String zipFile, String? name = null, String? version = null)
    {
        if (zipFile.IsNullOrEmpty()) throw new ArgumentNullException(nameof(zipFile));
        if (!File.Exists(zipFile)) throw new FileNotFoundException("Package file not found.", zipFile);

        var temp = CreateTempDir();
        try
        {
            ExtractZipTo(zipFile, temp);

            var manifest = LoadLocalManifest(temp);
            var errors = manifest.Validate();
            if (errors.Count > 0) throw new InvalidOperationException($"Invalid package manifest: {errors.Join("; ")}");

            if (!name.IsNullOrEmpty() && !manifest.Name.EqualIgnoreCase(name))
                throw new InvalidOperationException($"Package name mismatch: {manifest.Name} != {name}.");
            if (!version.IsNullOrEmpty() && manifest.Version != version)
                throw new InvalidOperationException($"Package version mismatch: {manifest.Version} != {version}.");

            name = manifest.Name!;
            version = manifest.Version!;

            var versionDir = GetVersionDir(name, version);
            if (Directory.Exists(versionDir)) Directory.Delete(versionDir, true);

            versionDir.EnsureDirectory(true);
            MoveDirectory(temp, versionDir);
            SetCurrentVersion(name, version);

            Log.Info("Installed package {0} {1} to {2}", name, version, versionDir);

            return StarPackage.Load(Path.Combine(versionDir, "star.json"));
        }
        finally
        {
            TryDelete(temp);
        }
    }

    /// <summary>按服务端清单解压 zip 到版本目录。zip 内可以不包含 star.json</summary>
    /// <param name="zipFile">zip 文件</param>
    /// <param name="manifest">服务端清单</param>
    /// <param name="hash">期望 MD5，可为空</param>
    /// <returns>包清单</returns>
    public virtual StarPackage ExtractZip(String zipFile, StarPackage manifest, String? hash = null)
    {
        if (zipFile.IsNullOrEmpty()) throw new ArgumentNullException(nameof(zipFile));
        if (!File.Exists(zipFile)) throw new FileNotFoundException("Package file not found.", zipFile);
        if (manifest == null) throw new ArgumentNullException(nameof(manifest));

        if (!hash.IsNullOrEmpty())
        {
            var actual = new FileInfo(zipFile).MD5().ToHex();
            if (!actual.EqualIgnoreCase(hash)) throw new InvalidOperationException($"Package hash mismatch: {actual} != {hash}.");
        }

        var temp = CreateTempDir();
        try
        {
            ExtractZipTo(zipFile, temp);

            return InstallExtracted(temp, manifest);
        }
        finally
        {
            TryDelete(temp);
        }
    }

    /// <summary>读取本地 zip 根目录的 star.json。服务端清单安装路径不调用本方法</summary>
    /// <param name="temp">解压后的临时目录</param>
    /// <returns>包清单</returns>
    private static StarPackage LoadLocalManifest(String temp)
    {
        var file = Path.Combine(temp, "star.json");
        if (!File.Exists(file)) throw new InvalidOperationException("Local package must contain star.json in its root.");

        return StarPackage.Load(file);
    }

    /// <summary>把临时目录安装为版本目录，并写入服务端清单</summary>
    /// <param name="temp">临时目录</param>
    /// <param name="manifest">服务端清单</param>
    /// <returns>包清单</returns>
    private StarPackage InstallExtracted(String temp, StarPackage manifest)
    {
        var errors = manifest.Validate();
        if (errors.Count > 0) throw new InvalidOperationException($"Invalid package manifest: {errors.Join("; ")}");

        var name = manifest.Name!;
        var version = manifest.Version!;
        File.WriteAllText(Path.Combine(temp, "star.json"), manifest.ToJson());

        var versionDir = GetVersionDir(name, version);
        if (Directory.Exists(versionDir)) Directory.Delete(versionDir, true);

        versionDir.EnsureDirectory(true);
        MoveDirectory(temp, versionDir);
        SetCurrentVersion(name, version);

        Log.Info("Installed package {0} {1} to {2}", name, version, versionDir);

        return manifest;
    }

    /// <summary>下载或复制 zip 到临时文件</summary>
    /// <param name="source">下载地址或本地文件</param>
    /// <param name="hash">期望 MD5</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>临时 zip 文件路径</returns>
    protected virtual async Task<String> PrepareZipAsync(String source, String? hash, CancellationToken cancellationToken)
    {
        var tempDir = Path.Combine(GetRoot(), ".tmp");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, $"{Guid.NewGuid():n}.zip");

        if (File.Exists(source))
        {
            File.Copy(source, tempFile, true);
            if (!hash.IsNullOrEmpty())
            {
                var actual = new FileInfo(tempFile).MD5().ToHex();
                if (!actual.EqualIgnoreCase(hash))
                {
                    TryDelete(tempFile);
                    throw new InvalidOperationException($"Package hash mismatch: {actual} != {hash}.");
                }
            }

            return tempFile;
        }

        using var span = Tracer?.NewSpan("StarPackage-Download", source);
        Log.Info("Download package: {0}", source);

        var options = new HttpDownloadOptions
        {
            HeaderTimeout = TimeSpan.FromSeconds(15),
            DownloadTimeout = TimeSpan.FromMinutes(10),
            RetryCount = 2,
            RetryDelay = TimeSpan.FromSeconds(2),
            ExpectedHash = hash,
            UseContentDispositionFileName = false,
            ClientFactory = () => new HttpClient(),
        };

        try
        {
            using var http = new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
            await http.DownloadFileAsync(source, tempFile, options).ConfigureAwait(false);

            return tempFile;
        }
        catch
        {
            TryDelete(tempFile);
            throw;
        }
    }

    /// <summary>解压 zip 到目录，并校验路径穿越</summary>
    /// <param name="zipFile">zip 文件</param>
    /// <param name="target">目标目录</param>
    protected virtual void ExtractZipTo(String zipFile, String target)
    {
        ValidateZip(zipFile);

        Directory.CreateDirectory(target);

        // 先解压到目标目录，调用方负责最终移动
        ZipFile.ExtractToDirectory(zipFile, target);

        // 解压后再次确认所有文件都在目标目录内
        var root = Path.GetFullPath(target);
        foreach (var file in Directory.GetFiles(target, "*", SearchOption.AllDirectories))
        {
            var full = Path.GetFullPath(file);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Unsafe zip entry detected: {file}");
        }
    }

    /// <summary>校验 zip 条目路径，阻止路径穿越</summary>
    /// <param name="zipFile">zip 文件</param>
    public virtual void ValidateZip(String zipFile)
    {
        using var zip = ZipFile.OpenRead(zipFile);
        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (name.StartsWith("/") || name.Contains(':')) throw new InvalidOperationException($"Unsafe zip entry detected: {entry.FullName}");

            foreach (var item in name.Split('/'))
            {
                if (item == "..") throw new InvalidOperationException($"Unsafe zip entry detected: {entry.FullName}");
            }
        }
    }

    #endregion
    #region 删除
    /// <summary>删除包或指定版本</summary>
    /// <param name="name">包名</param>
    /// <param name="version">版本，为空时删除整个包</param>
    /// <returns>是否删除成功</returns>
    public virtual Boolean Remove(String name, String? version = null)
    {
        var packageDir = GetPackageDir(name);
        if (!Directory.Exists(packageDir)) return false;

        if (!version.IsNullOrEmpty())
        {
            var versionDir = GetVersionDir(name, version);
            if (!Directory.Exists(versionDir)) return false;

            DeleteDirectory(versionDir);

            if (GetCurrentVersion(name).EqualIgnoreCase(version))
            {
                var last = FindLastVersion(name);
                if (last.IsNullOrEmpty())
                    TryDelete(GetCurrentFile(name));
                else
                    SetCurrentVersion(name, last);
            }
        }
        else
        {
            DeleteDirectory(packageDir);
        }

        return true;
    }

    /// <summary>删除目录。进程刚退出时 Windows 可能短暂占用目录，重试一小段时间</summary>
    /// <param name="path">目录路径</param>
    private static void DeleteDirectory(String path)
    {
        for (var i = 0; ; i++)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
                return;
            }
            catch (IOException) when (i < 24) { }
            catch (UnauthorizedAccessException) when (i < 24) { }

            Thread.Sleep(200);
        }
    }
    #endregion

    #region 辅助
    /// <summary>创建临时目录</summary>
    /// <returns>临时目录</returns>
    protected virtual String CreateTempDir()
    {
        var dir = Path.Combine(GetRoot(), ".tmp", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);

        return dir;
    }

    private static void MoveDirectory(String source, String target)
    {
        // 目标目录已存在时先删除，保证移动成功
        if (Directory.Exists(target)) Directory.Delete(target, true);

        Directory.Move(source, target);
    }

    private void TryDelete(String path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
            else if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch (Exception ex)
        {
            Log.Warn("Failed to delete [{0}]: {1}", path, ex.Message);
        }
    }
    #endregion
}

/// <summary>已安装包信息</summary>
public class StarPackageInfo
{
    #region 属性
    /// <summary>包名</summary>
    public String Name { get; set; } = null!;

    /// <summary>版本</summary>
    public String Version { get; set; } = null!;

    /// <summary>版本目录</summary>
    public String Path { get; set; } = null!;

    /// <summary>是否当前版本</summary>
    public Boolean IsCurrent { get; set; }

    /// <summary>安装时间</summary>
    public DateTime InstallTime { get; set; }

    /// <summary>包清单</summary>
    public StarPackage? Manifest { get; set; }
    #endregion
}
