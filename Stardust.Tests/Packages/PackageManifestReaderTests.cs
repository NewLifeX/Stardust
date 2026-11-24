using System;
using System.IO;
using System.IO.Compression;
using Stardust.Data.Deployment;
using Xunit;

namespace Stardust.Tests.Packages;

/// <summary>应用包清单读取器测试。覆盖根目录清单、缺失与非法清单（PUB-25）</summary>
public class PackageManifestReaderTests
{
    private const String ValidManifest = """
    {
      "schema": 1,
      "name": "newlife/star",
      "version": "1.0",
      "apps": [ { "name": "star", "fileName": "star.exe" } ]
    }
    """;

    private static MemoryStream CreateZip(String? manifest, String entryName = "star.json")
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            if (manifest != null)
            {
                var entry = zip.CreateEntry(entryName);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(manifest);
            }
        }

        stream.Position = 0;
        return stream;
    }

    [Fact(DisplayName = "读取根目录 star.json")]
    public void Read_ValidManifest_Succeeds()
    {
        using var stream = CreateZip(ValidManifest);

        var (manifest, json) = PackageManifestReader.Read(stream);

        Assert.Equal("newlife/star", manifest.Name);
        Assert.Equal("1.0", manifest.Version);
        Assert.Contains("\"schema\"", json);
    }

    [Fact(DisplayName = "缺少 star.json 时失败")]
    public void Read_MissingManifest_Throws()
    {
        using var stream = CreateZip(null);

        var ex = Assert.Throws<InvalidOperationException>(() => PackageManifestReader.Read(stream));
        Assert.Contains("star.json", ex.Message);
    }

    [Fact(DisplayName = "可选读取：缺少 star.json 时返回空")]
    public void TryRead_MissingManifest_ReturnsNull()
    {
        using var stream = CreateZip(null);

        Assert.Null(PackageManifestReader.TryRead(stream));
    }

    [Fact(DisplayName = "可选读取：有效 star.json 返回清单与原文")]
    public void TryRead_ValidManifest_ReturnsResult()
    {
        using var stream = CreateZip(ValidManifest);

        var result = PackageManifestReader.TryRead(stream);

        Assert.NotNull(result);
        Assert.Equal("newlife/star", result.Value.manifest.Name);
        Assert.Contains("\"schema\"", result.Value.json);
    }

    [Fact(DisplayName = "子目录中的 star.json 不被接受")]
    public void Read_NestedManifest_Throws()
    {
        using var stream = CreateZip(ValidManifest, "pkg/star.json");

        var ex = Assert.Throws<InvalidOperationException>(() => PackageManifestReader.Read(stream));
        Assert.Contains("star.json", ex.Message);
    }

    [Fact(DisplayName = "非法清单被拒绝")]
    public void Read_InvalidManifest_Throws()
    {
        using var stream = CreateZip("""{ "schema": 1, "name": "star", "version": "1.0", "apps": [] }""");

        var ex = Assert.Throws<InvalidOperationException>(() => PackageManifestReader.Read(stream));
        Assert.Contains("Invalid package name", ex.Message);
    }
}
