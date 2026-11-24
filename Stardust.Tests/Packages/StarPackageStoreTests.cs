using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NewLife;
using NewLife.IO;
using Stardust.Deployment;
using Stardust.Models;
using Xunit;

namespace Stardust.Tests.Packages;

/// <summary>星尘包本地仓库测试。覆盖安装、哈希校验、路径穿越与删除（PUB-25）</summary>
public class StarPackageStoreTests
{
    private static String CreateRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), "star-store-" + Guid.NewGuid().ToString("n")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static String CreateManifest(String name, String version, String appFile = "app.exe")
    {
        return $$"""
        {
          "schema": 1,
          "name": "{{name}}",
          "version": "{{version}}",
          "apps": [
            { "name": "app", "fileName": "{{appFile}}" }
          ]
        }
        """;
    }

    private static String CreatePackageZip(String dir, String name, String version, String? manifest = null)
    {
        var root = Path.Combine(dir, "src-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "star.json"), manifest ?? CreateManifest(name, version));
        File.WriteAllText(Path.Combine(root, "app.exe"), "dummy");

        var zip = Path.Combine(dir, "pkg-" + Guid.NewGuid().ToString("n") + ".zip");
        ZipFile.CreateFromDirectory(root, zip);
        return zip;
    }

    private static String CreateServerPackageZip(String dir)
    {
        var root = Path.Combine(dir, "src-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "app.exe"), "dummy");

        var zip = Path.Combine(dir, "pkg-" + Guid.NewGuid().ToString("n") + ".zip");
        ZipFile.CreateFromDirectory(root, zip);
        return zip;
    }

    private static StarPackage CreateServerManifest(String name = "newlife/starserver", String version = "1.0") => new()
    {
        Schema = 1,
        Name = name,
        Version = version,
        Apps = [new StarPackageApp { Name = "starserver", FileName = "app.exe" }],
    };

    [Fact(DisplayName = "从本地 zip 安装并设置 current")]
    public void InstallFromFile_ExtractsAndSetsCurrent()
    {
        var root = CreateRoot();
        var zip = CreatePackageZip(root, "newlife/star", "1.0");
        var store = new StarPackageStore(root);

        var package = store.InstallFromFile(zip);

        Assert.Equal("newlife/star", package.Name);
        Assert.Equal("1.0", package.Version);
        Assert.True(store.IsInstalled("newlife/star", "1.0"));
        Assert.Equal("1.0", store.GetCurrentVersion("newlife/star"));

        var versionDir = store.GetVersionDir("newlife/star", "1.0");
        Assert.True(File.Exists(Path.Combine(versionDir, "star.json")));
        Assert.True(File.Exists(Path.Combine(versionDir, "app.exe")));
    }

    [Fact(DisplayName = "重复安装已存在版本时也切换 current")]
    public async System.Threading.Tasks.Task InstallAsync_ExistingVersion_UpdatesCurrent()
    {
        var root = CreateRoot();
        var zip1 = CreatePackageZip(root, "newlife/star", "1.0");
        var zip2 = CreatePackageZip(root, "newlife/star", "2.0");
        var store = new StarPackageStore(root);

        await store.InstallAsync("newlife/star", "1.0", zip1);
        await store.InstallAsync("newlife/star", "2.0", zip2);
        Assert.Equal("2.0", store.GetCurrentVersion("newlife/star"));

        // 1.0 已存在，走“已安装直接返回”分支，也必须把 current 切回来
        await store.InstallAsync("newlife/star", "1.0", zip1);
        Assert.Equal("1.0", store.GetCurrentVersion("newlife/star"));
    }

    [Fact(DisplayName = "安装时校验 MD5")]
    public async System.Threading.Tasks.Task InstallAsync_WithHash_Succeeds()
    {
        var root = CreateRoot();
        var zip = CreatePackageZip(root, "newlife/star", "1.0");
        var hash = new FileInfo(zip).MD5().ToHex();
        var store = new StarPackageStore(root);

        var package = await store.InstallAsync("newlife/star", "1.0", zip, hash);

        Assert.Equal("1.0", package.Version);
        Assert.True(store.IsInstalled("newlife/star", "1.0"));
    }

    [Fact(DisplayName = "哈希不匹配时安装失败")]
    public async System.Threading.Tasks.Task InstallAsync_HashMismatch_Throws()
    {
        var root = CreateRoot();
        var zip = CreatePackageZip(root, "newlife/star", "1.0");
        var store = new StarPackageStore(root);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.InstallAsync("newlife/star", "1.0", zip, "00000000000000000000000000000000"));
    }

    [Fact(DisplayName = "包名与清单不一致时安装失败")]
    public void InstallAsync_NameMismatch_Throws()
    {
        var root = CreateRoot();
        var zip = CreatePackageZip(root, "newlife/star", "1.0");
        var store = new StarPackageStore(root);

        Assert.Throws<InvalidOperationException>(() => store.ExtractZip(zip, "other/star", "1.0"));
    }

    [Fact(DisplayName = "zip 路径穿越被拒绝")]
    public void ValidateZip_PathTraversal_Throws()
    {
        var root = CreateRoot();
        var zip = Path.Combine(root, "evil.zip");
        using (var fs = File.Create(zip))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("../evil.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("evil");
        }

        var store = new StarPackageStore(root);

        Assert.Throws<InvalidOperationException>(() => store.ValidateZip(zip));
    }

    [Fact(DisplayName = "删除版本后 current 指向剩余版本")]
    public void Remove_RepointsCurrent()
    {
        var root = CreateRoot();
        var store = new StarPackageStore(root);
        var zip1 = CreatePackageZip(root, "newlife/star", "1.0");
        var zip2 = CreatePackageZip(root, "newlife/star", "2.0");

        store.InstallFromFile(zip1);
        store.InstallFromFile(zip2);
        store.SetCurrentVersion("newlife/star", "2.0");

        Assert.True(store.Remove("newlife/star", "2.0"));
        Assert.Equal("1.0", store.GetCurrentVersion("newlife/star"));
        Assert.False(store.IsInstalled("newlife/star", "2.0"));
        Assert.True(store.IsInstalled("newlife/star", "1.0"));

        Assert.True(store.Remove("newlife/star"));
        Assert.False(store.IsInstalled("newlife/star"));
    }

    [Fact(DisplayName = "列出已安装包并标记 current")]
    public void List_ReturnsInstalledPackages()
    {
        var root = CreateRoot();
        var store = new StarPackageStore(root);
        store.InstallFromFile(CreatePackageZip(root, "newlife/star", "1.0"));
        store.InstallFromFile(CreatePackageZip(root, "redis/redis", "7.2"));

        var list = store.List();

        Assert.Equal(2, list.Count);
        Assert.Contains(list, e => e.Name == "newlife/star" && e.Version == "1.0" && e.IsCurrent);
        Assert.Contains(list, e => e.Name == "redis/redis" && e.Version == "7.2" && e.IsCurrent);
        Assert.All(list, e => Assert.NotNull(e.Manifest));
    }

    [Fact(DisplayName = "重复安装同一版本保持幂等")]
    public void InstallFromFile_Reinstall_IsIdempotent()
    {
        var root = CreateRoot();
        var store = new StarPackageStore(root);
        var zip = CreatePackageZip(root, "newlife/star", "1.0");

        store.InstallFromFile(zip);
        store.InstallFromFile(zip);

        Assert.Single(store.List());
    }

    [Fact(DisplayName = "服务端清单安装：zip 内没有 star.json 也可以")]
    public void InstallFromFile_ServerManifest_WithoutStarJson()
    {
        var root = CreateRoot();
        var zip = CreateServerPackageZip(root);
        var store = new StarPackageStore(root);

        var package = store.InstallFromFile(zip, CreateServerManifest(), null);

        Assert.Equal("newlife/starserver", package.Name);
        Assert.Equal("1.0", package.Version);
        Assert.True(store.IsInstalled("newlife/starserver", "1.0"));
        Assert.Equal("1.0", store.GetCurrentVersion("newlife/starserver"));

        var manifest = File.ReadAllText(Path.Combine(store.GetVersionDir("newlife/starserver", "1.0"), "star.json"));
        Assert.Contains("\"newlife/starserver\"", manifest);
    }

    [Fact(DisplayName = "服务端清单安装时校验 MD5")]
    public async System.Threading.Tasks.Task InstallAsync_ServerManifest_WithHash()
    {
        var root = CreateRoot();
        var zip = CreateServerPackageZip(root);
        var hash = new FileInfo(zip).MD5().ToHex();
        var store = new StarPackageStore(root);

        var package = await store.InstallAsync("newlife/starserver", "1.0", zip, CreateServerManifest(), hash);

        Assert.Equal("1.0", package.Version);
        Assert.True(store.IsInstalled("newlife/starserver", "1.0"));
    }

    [Fact(DisplayName = "本地 zip 安装仍要求 star.json")]
    public void InstallFromFile_WithoutStarJson_Throws()
    {
        var root = CreateRoot();
        var zip = CreateServerPackageZip(root);
        var store = new StarPackageStore(root);

        Assert.Throws<InvalidOperationException>(() => store.InstallFromFile(zip));
    }
}
