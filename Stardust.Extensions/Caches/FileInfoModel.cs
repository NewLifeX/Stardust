using System.IO;
using System;
using Microsoft.Extensions.FileProviders;

namespace Stardust.Extensions.Caches;

class FileInfoModel : IFileInfo
{
    public String Name { get; set; } = null!;

    public Boolean Exists { get; set; }

    public Boolean IsDirectory { get; set; }

    public DateTimeOffset LastModified { get; set; }

    public Int64 Length { get; set; }

    public String? PhysicalPath { get; set; }

    public Stream CreateReadStream() => throw new NotImplementedException();
}
