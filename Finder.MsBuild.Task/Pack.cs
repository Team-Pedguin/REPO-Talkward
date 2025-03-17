using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Finder.MsBuild.Task;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

/// <summary>
/// You provide a selection of MSBuild <see cref="ITaskItem"/>s to pack into a compressed archive.
/// You specify what type of compression (default is zip) and the output file name.
/// You can also provide a Path metadata property on each item to rename each item in the archive. 
/// </summary>
public class Pack : Task
{
    [Required]
    public string OutputFile { get; set; } = string.Empty;

    [Required]
    public string Type { get; set; } = "zip";

    public bool Overwrite { get; set; } = true;

    public bool Verbose { get; set; } = false;

    [Required]
    public ITaskItem[]? Items { get; set; }

    private static readonly Dictionary<string, Func<string, IPacker>> Packers = new()
    {
        {"zip", f => new ZipPacker(f)}
    };

    public override bool Execute()
    {
        if (Items is null || Items.Length == 0)
        {
            Log.LogWarning("No items specified to pack, no packed file created.");
            return true;
        }

        if (string.IsNullOrWhiteSpace(OutputFile))
        {
            Log.LogError("OutputFile is not specified.");
            return false;
        }

        var outputAlreadyExists = File.Exists(OutputFile);
        if (Overwrite)
        {
            if (outputAlreadyExists)
                try
                {
                    File.Delete(OutputFile);
                }
                catch (Exception e)
                {
                    Log.LogError($"Failed to delete existing file '{OutputFile}': {e.Message}");
                    return false;
                }
        }
        else if (outputAlreadyExists)
        {
            Log.LogError($"Output file '{OutputFile}' already exists.");
            return false;
        }

        var packerType = Type.ToLowerInvariant();

        using var packer = Packers.TryGetValue(packerType, out var packerFactory)
            ? packerFactory(OutputFile)
            : null;

        if (packer is null)
        {
            Log.LogError(
                $"Unsupported type '{packerType}'. Supported types are currently: {string.Join(' ', Packers.Keys)}.");
            return false;
        }

        foreach (var item in Items)
        {
            var renamed = item.GetMetadata("PackPath");
            var path = item.ItemSpec;
            if (string.IsNullOrWhiteSpace(renamed))
                renamed = null;

            if (!packer.AddFile(path, renamed))
                Log.LogError($"Failed to add file '{path}' to archive.");
        }

        return true;
    }
}