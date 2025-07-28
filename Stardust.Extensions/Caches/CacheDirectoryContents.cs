using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using NewLife;
using NewLife.IO;

namespace Stardust.Extensions.Caches;

class CacheDirectoryContents : IDirectoryContents, IEnumerable<IFileInfo>, IEnumerable
{
    private IEnumerable<IFileInfo>? _entries;

    private readonly String _directory;

    private readonly ExclusionFilters _filters;

    public Boolean Exists => Directory.Exists(_directory);

    /// <summary>索引信息文件。列出扩展显示的文件内容</summary>
    public String? IndexInfoFile { get; set; }

    public CacheDirectoryContents(String directory)
        : this(directory, ExclusionFilters.Sensitive)
    {
    }

    public CacheDirectoryContents(String directory, ExclusionFilters filters)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _filters = filters;
    }

    public IEnumerator<IFileInfo> GetEnumerator()
    {
        EnsureInitialized();
        return _entries.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        EnsureInitialized();
        return _entries.GetEnumerator();
    }

    [MemberNotNull(nameof(_entries))]
    private void EnsureInitialized()
    {
        try
        {
            var entries = (from info in new DirectoryInfo(_directory).EnumerateFileSystemInfos()
                           where !CacheFileProvider.IsExcluded(info, _filters)
                           select info).Select(info =>
                           {
                               if (info is FileInfo fileInfo)
                                   return new PhysicalFileInfo(fileInfo);
                               return info is DirectoryInfo directoryInfo
                                   ? (IFileInfo)new PhysicalDirectoryInfo(directoryInfo)
                                   : throw new InvalidOperationException("UnexpectedFileSystemInfo");
                           }).ToList();

            if (!IndexInfoFile.IsNullOrEmpty())
            {
                var fi = _directory.CombinePath(IndexInfoFile).GetBasePath().AsFile();
                if (fi.Exists)
                {
                    var csv = new CsvDb<FileInfoModel>((x, y) => x != null && y != null && x.Name.EqualIgnoreCase(y.Name))
                    {
                        FileName = fi.FullName
                    };
                    var fis = csv.FindAll();
                    if (fis.Count > 0)
                    {
                        var list = entries.ToList();
                        foreach (var item in fis)
                        {
                            // 把fis里面的项添加到list
                            item.Name = item.Name.TrimEnd('/', '\\');
                            var name2 = item.Name.EnsureEnd(Path.DirectorySeparatorChar + "");
                            if (!list.Any(e => e.Name.EqualIgnoreCase(item.Name, name2)))
                                list.Add(item);
                        }

                        entries = list;
                    }

                    // 剔除索引文件本身
                    entries.RemoveAll(e => e.PhysicalPath == fi.FullName);
                }
            }

            _entries = entries;
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or IOException)
        {
            _entries = [];
        }
    }
}