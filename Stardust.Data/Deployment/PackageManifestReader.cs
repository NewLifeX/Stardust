using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NewLife;
using Stardust.Models;

namespace Stardust.Data.Deployment;

/// <summary>应用包清单读取器。从 zip 流读取根目录的 star.json 并校验</summary>
public static class PackageManifestReader
{
    /// <summary>读取并校验包清单</summary>
    /// <param name="stream">zip 数据流</param>
    /// <returns>包清单与原始 Json</returns>
    public static (StarPackage manifest, String json) Read(Stream stream) => TryRead(stream) ?? throw new InvalidOperationException("star.json was not found at the package root.");

    /// <summary>尝试读取包清单。zip 根目录没有 star.json 时返回 null</summary>
    /// <param name="stream">zip 数据流</param>
    /// <returns>包清单与原始 Json，未找到时返回 null</returns>
    public static (StarPackage manifest, String json)? TryRead(Stream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, true);
        var entry = zip.Entries.FirstOrDefault(e => e.FullName.EqualIgnoreCase("star.json"));
        if (entry == null) return null;

        using var reader = new StreamReader(entry.Open());
        var json = reader.ReadToEnd();

        var manifest = StarPackage.Parse(json);
        var errors = manifest.Validate();
        if (errors.Count > 0) throw new InvalidOperationException(String.Join("; ", errors));

        return (manifest, json);
    }
}
