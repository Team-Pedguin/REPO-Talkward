using System;
using System.IO;
using System.IO.Compression;

namespace Finder.MsBuild.Task;

public class ZipPacker : IPacker
{
    private readonly ZipArchive _zipArchive;

    public ZipPacker(string outputFile)
    {
        var fs = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None,
            2097152, FileOptions.SequentialScan | FileOptions.Asynchronous | FileOptions.WriteThrough);
        _zipArchive = new ZipArchive(fs, ZipArchiveMode.Create);
    }

    public bool AddFile(string path, string? renamed = null)
    {
        if (string.IsNullOrEmpty(renamed))
            renamed = Path.GetFileName(path);
        if (!File.Exists(path))
            return false;
        _zipArchive.CreateEntryFromFile(path, renamed, CompressionLevel.Optimal);
        return true;
    }

    public void Dispose()
        => _zipArchive.Dispose();
}