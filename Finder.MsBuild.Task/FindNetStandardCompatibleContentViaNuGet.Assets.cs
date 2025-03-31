using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;
using NuGet.Packaging;

namespace Finder.MsBuild.Task;

[Flags]
public enum AssetType
{
    None = 0,
    Compile = 1 << 0,
    Runtime = 1 << 1,
    ContentFiles = 1 << 2,
    Build = 1 << 3,
    BuildMultitargeting = 1 << 4,
    BuildTransitive = 1 << 5,
    Analyzers = 1 << 6,
    Native = 1 << 7,

    All = Compile | Runtime | ContentFiles | Build |
          BuildMultitargeting | BuildTransitive | Analyzers | Native
}

// Asset types as defined in NuGet documentation
public static class AssetTypes
{
    public const string Compile = "compile";
    public const string Runtime = "runtime";
    public const string ContentFiles = "contentfiles";
    public const string Build = "build";
    public const string BuildMultitargeting = "buildmultitargeting";
    public const string BuildTransitive = "buildtransitive";
    public const string Analyzers = "analyzers";
    public const string Native = "native";
    public const string None = "none";
    public const string All = "all";

    // Default private assets if not specified
    public static readonly string[] DefaultPrivateAssets =
        [ContentFiles, Analyzers, Build, BuildMultitargeting, BuildTransitive];

    public static string FromAssetType(AssetType assetType)
    {
        switch (assetType)
        {
            case AssetType.None:
                return None;
            case AssetType.All:
                return All;
        }

        GcHandleSpanList<string> assetNames = stackalloc GCHandle[8];
        if ((assetType & AssetType.Compile) != 0)
            assetNames.Add(Compile);
        if ((assetType & AssetType.Runtime) != 0)
            assetNames.Add(Runtime);
        if ((assetType & AssetType.ContentFiles) != 0)
            assetNames.Add(ContentFiles);
        if ((assetType & AssetType.Build) != 0)
            assetNames.Add(Build);
        if ((assetType & AssetType.BuildMultitargeting) != 0)
            assetNames.Add(BuildMultitargeting);
        if ((assetType & AssetType.BuildTransitive) != 0)
            assetNames.Add(BuildTransitive);
        if ((assetType & AssetType.Analyzers) != 0)
            assetNames.Add(Analyzers);
        if ((assetType & AssetType.Native) != 0)
            assetNames.Add(Native);

        return assetNames.Count switch
        {
            0 => "",
            1 => assetNames[0],
            _ => assetNames.Join(';')
        };
    }

    public static AssetType ToAssetType(string? assetTypeNames, bool single = false)
    {
        var assetTypeNamesLc = assetTypeNames?.ToLowerInvariant();

        return assetTypeNamesLc switch
        {
            null => AssetType.None,
            "" => AssetType.None,
            None => AssetType.None,
            Compile => AssetType.Compile,
            Runtime => AssetType.Runtime,
            ContentFiles => AssetType.ContentFiles,
            Build => AssetType.Build,
            BuildMultitargeting => AssetType.BuildMultitargeting,
            BuildTransitive => AssetType.BuildTransitive,
            Analyzers => AssetType.Analyzers,
            Native => AssetType.Native,
            All => AssetType.All,
            _ => single
                ? throw new ArgumentException($"Unknown asset type: {assetTypeNames}")
                : assetTypeNamesLc.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Aggregate(AssetType.None,
                        static (a, v)
                            => a | ToAssetType(v, true))
        };
    }
}

public sealed partial class FindNetStandardCompatibleContentViaNuGet
{
    // Static mapping of folder names to asset types for efficient lookup
    private static readonly FrozenDictionary<string, AssetType> AssetFolderMapping =
        new Dictionary<string, AssetType>(StringComparer.OrdinalIgnoreCase)
        {
            ["lib"] = AssetType.Compile,
            ["build"] = AssetType.Build,
            ["buildmultitargeting"] = AssetType.BuildMultitargeting,
            ["buildtransitive"] = AssetType.BuildTransitive,
            ["contentfiles"] = AssetType.ContentFiles,
            ["analyzers"] = AssetType.Analyzers,
            ["native"] = AssetType.Native,
            ["runtimes"] = AssetType.Runtime
        }.ToFrozenDictionary();

    // Framework folder name prefixes
    private static readonly string[] FrameworkFolderPrefixes =
    {
        "net",
        "netstandard",
        "netcoreapp",
        "netframework"
    };

    // Parses the asset specification string (e.g., "compile;runtime") into a HashSet
    private HashSet<string> ParseAssetSpecification(string assetSpec)
    {
        if (string.IsNullOrWhiteSpace(assetSpec))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var assets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var parts = assetSpec.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var trimmedPart = part.Trim().ToLowerInvariant();

            switch (trimmedPart)
            {
                case AssetTypes.All:
                    // "all" means all asset types except "none"
                    assets.Add(AssetTypes.Compile);
                    assets.Add(AssetTypes.Runtime);
                    assets.Add(AssetTypes.ContentFiles);
                    assets.Add(AssetTypes.Build);
                    assets.Add(AssetTypes.BuildMultitargeting);
                    assets.Add(AssetTypes.BuildTransitive);
                    assets.Add(AssetTypes.Analyzers);
                    assets.Add(AssetTypes.Native);
                    return assets;
                case AssetTypes.None:
                    // "none" means no assets - clear any previously added
                    assets.Clear();
                    return assets;
                default:
                    assets.Add(trimmedPart);
                    break;
            }
        }

        return assets;
    }

    // Get asset specifications from an ITaskItem
    private (HashSet<string> includeAssets, HashSet<string> excludeAssets, HashSet<string> privateAssets)
        GetAssetSpecifications(ITaskItem package)
    {
        // Default values
        var includeAssets = ParseAssetSpecification(AssetTypes.All);
        var excludeAssets = ParseAssetSpecification(string.Empty);

        // Get metadata values if available
        var includeAssetsMetadata = package.GetMetadata("IncludeAssets");
        var excludeAssetsMetadata = package.GetMetadata("ExcludeAssets");
        var privateAssetsMetadata = package.GetMetadata("PrivateAssets");

        // Parse provided values
        if (!string.IsNullOrWhiteSpace(includeAssetsMetadata))
            includeAssets = ParseAssetSpecification(includeAssetsMetadata);

        if (!string.IsNullOrWhiteSpace(excludeAssetsMetadata))
            excludeAssets = ParseAssetSpecification(excludeAssetsMetadata);

        // For private assets, use default if not specified
        var privateAssets = string.IsNullOrWhiteSpace(privateAssetsMetadata)
            ? new HashSet<string>(AssetTypes.DefaultPrivateAssets, StringComparer.OrdinalIgnoreCase)
            : ParseAssetSpecification(privateAssetsMetadata);

        // Exclude assets take precedence over included assets
        foreach (var asset in excludeAssets)
            includeAssets.Remove(asset);

        return (includeAssets, excludeAssets, privateAssets);
    }

    // Determine if the file should be included based on asset specifications
    private bool ShouldIncludeFile(string filePath, string packagePath, HashSet<string> includeAssets,
        HashSet<string> privateAssets)
    {
        // If no assets are included, exclude everything
        if (includeAssets.Count == 0)
            return false;

        // Determine asset type based on file path and package root
        var assetType = DetermineAssetType(filePath, packagePath);

        // Check if asset type is included
        if (!includeAssets.Contains(assetType))
            return false;
        
        return true;
    }

    // Determine the asset type based on file path relative to package root
    private string DetermineAssetType(string filePath, string packageRootPath)
    {
        // Normalize paths to handle different path separators
        filePath = Path.GetFullPath(filePath);
        packageRootPath = Path.GetFullPath(packageRootPath);

        // Check if the file is inside the package path
        if (!filePath.StartsWith(packageRootPath, StringComparison.OrdinalIgnoreCase))
        {
            Log.LogWarning($"File path {filePath} is not within package root {packageRootPath}");
            return AssetTypes.None;
        }

        // Get the relative path from package root
        var relativePath = filePath.Substring(packageRootPath.Length)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var pathParts = relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        // Empty path or too short
        if (pathParts.Length < 1)
            return AssetTypes.None;

        // Determine asset type based on the top-level folder
        var topFolder = pathParts[0].ToLowerInvariant();

        // Use the static mapping for better performance
        if (AssetFolderMapping.TryGetValue(topFolder, out var assetType))
        {
            return AssetTypes.FromAssetType(assetType);
        }

        return AssetTypes.None;
    }

    // Modified version of ProcessPackageContent to use asset type enum where appropriate
    private int ProcessPackageContent(string packageId, string packagePath, string libPath,
        List<NuGetFramework> targetFrameworks, List<TaskItem> libraryContentFiles)
    {
        var filesFound = 0;

        // Find the original package item to get asset specifications
        var originalPackageItem = Packages?.FirstOrDefault(p =>
            string.Equals(p.ItemSpec, packageId, StringComparison.OrdinalIgnoreCase));

        // Default to all assets if the package wasn't in original list (dependency)
        var (includeAssets, _, privateAssets) = originalPackageItem != null
            ? GetAssetSpecifications(originalPackageItem)
            : (ParseAssetSpecification(AssetTypes.All), new HashSet<string>(),
                new HashSet<string>(AssetTypes.DefaultPrivateAssets));

        // Log asset specifications for debugging
        Log.LogMessage($"Package {packageId}: Include assets: {string.Join(";", includeAssets)}",
            MessageImportance.Low);
        Log.LogMessage($"Package {packageId}: Private assets: {string.Join(";", privateAssets)}",
            MessageImportance.Low);

        try
        {
            PackageReaderBase? packageReader = null;
            var nupkgPath = Directory.EnumerateFiles(packagePath, "*.nupkg").FirstOrDefault();

            try
            {
                if (nupkgPath != null)
                {
                    packageReader = new PackageArchiveReader(nupkgPath);
                    Log.LogMessage($"Using package archive reader for {packageId} from {nupkgPath}",
                        MessageImportance.Low);
                }
                else
                {
                    packageReader = new PackageFolderReader(packagePath);
                    Log.LogMessage($"Using package folder reader for {packageId} from {packagePath}",
                        MessageImportance.Low);
                }

                // For compile assets (lib folder), process with framework compatibility
                if (includeAssets.Contains(AssetTypes.Compile))
                {
                    // Process 'lib' folder (compile assets) with framework compatibility
                    filesFound += ProcessLibFolder(packageId, packagePath, libPath, targetFrameworks,
                        libraryContentFiles, packageReader);
                }
            }
            finally
            {
                // Dispose the package reader if it was created
                packageReader?.Dispose();
            }

            // Process non-compile asset folders directly from the filesystem
            foreach (var (folder, assetTypeEnum) in AssetFolderMapping)
            {
                // Skip lib folder as it was already processed
                if (folder == "lib")
                    continue;

                var assetTypeString = AssetTypes.FromAssetType(assetTypeEnum);

                // Skip folders that are excluded by asset specifications
                if (!includeAssets.Contains(assetTypeString))
                {
                    Log.LogMessage($"Skipping folder {folder} because {assetTypeString} is not in the included assets",
                        MessageImportance.Low);
                    continue;
                }

                var folderPath = Path.Combine(packagePath, folder);
                if (!Directory.Exists(folderPath))
                    continue;

                // Process each file in the folder and its subfolders
                foreach (var file in Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories))
                {
                    // Skip fallback marker files
                    if (Path.GetFileName(file) == "_._")
                        continue;

                    // Add the file as a task item
                    var taskItem = new TaskItem(file);
                    taskItem.SetMetadata("Package", packageId);
                    taskItem.SetMetadata("AssetType", assetTypeString);

                    // Always set the IsPrivate metadata consistently regardless of inclusion
                    bool isPrivate = privateAssets.Contains(assetTypeString);
                    taskItem.SetMetadata("IsPrivate", isPrivate.ToString());

                    // For target-specific folders, try to extract target framework info
                    var relativePath = file.Substring(folderPath.Length).TrimStart(Path.DirectorySeparatorChar);
                    var pathParts = relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                        StringSplitOptions.RemoveEmptyEntries);
                    if (pathParts.Length > 0 && IsFrameworkFolder(pathParts[0]))
                    {
                        taskItem.SetMetadata("TargetFramework", pathParts[0]);
                    }
                    else if (folder == "contentfiles" && pathParts.Length > 1)
                    {
                        // contentfiles folder structure is typically contentfiles/any/targetframework
                        if (pathParts.Length > 2 && IsFrameworkFolder(pathParts[2]))
                        {
                            taskItem.SetMetadata("TargetFramework", pathParts[2]);
                        }
                    }

                    libraryContentFiles.Add(taskItem);
                    filesFound++;
                }
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Error reading package {packageId}: {ex.Message}");
        }

        return filesFound;
    }

    // Helper to check if a folder name looks like a framework moniker
    private bool IsFrameworkFolder(string folder)
    {
        return FrameworkFolderPrefixes.Any(prefix =>
            folder.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    // Process lib folder with consistent IsPrivate metadata
    private int ProcessLibFolder(
        string packageId,
        string packagePath,
        string libPath,
        List<NuGetFramework> targetFrameworks,
        List<TaskItem> libraryContentFiles,
        PackageReaderBase packageReader)
    {
        if (!Directory.Exists(libPath))
            return 0;

        var filesFound = 0;

        // Find the original package item to get private assets info
        var originalPackageItem = Packages?.FirstOrDefault(p =>
            string.Equals(p.ItemSpec, packageId, StringComparison.OrdinalIgnoreCase));

        // Get private assets information
        var (_, _, privateAssets) = originalPackageItem != null
            ? GetAssetSpecifications(originalPackageItem)
            : (new HashSet<string>(), new HashSet<string>(),
                new HashSet<string>(AssetTypes.DefaultPrivateAssets));

        // Get all library items
        var libItems = packageReader.GetLibItems().ToList();
        Log.LogMessage($"Found {libItems.Count} lib item groups in package {packageId}", MessageImportance.Low);

        // Check each framework in order of preference
        foreach (var targetFramework in targetFrameworks)
        {
            // Find compatible frameworks in the package
            var compatibleFrameworks = libItems
                .Where(li => DefaultCompatibilityProvider.Instance.IsCompatible(
                    targetFramework, li.TargetFramework))
                .OrderByDescending(li => li.TargetFramework.Version)
                .ToList();

            if (!compatibleFrameworks.Any())
                continue;

            var bestMatch = compatibleFrameworks.First();
            Log.LogMessage(
                $"Best framework match for {packageId}: {bestMatch.TargetFramework.GetShortFolderName()} for target {targetFramework.GetShortFolderName()}",
                MessageImportance.Low);

            // Skip if it's just a fallback marker
            if (bestMatch.Items.Count() == 1 &&
                Path.GetFileName(bestMatch.Items.First()) == "_._")
            {
                Log.LogMessage(
                    $"Skipping fallback marker in {packageId} for {bestMatch.TargetFramework.GetShortFolderName()}",
                    MessageImportance.Low);
                continue;
            }

            var frameworkFolder = bestMatch.TargetFramework.GetShortFolderName();
            var frameworkLibPath = Path.Combine(libPath, frameworkFolder);

            if (!Directory.Exists(frameworkLibPath))
                continue;

            var packageFilesFound = false;
            foreach (var file in Directory.EnumerateFiles(frameworkLibPath, "*.*",
                         SearchOption.TopDirectoryOnly))
            {
                if (Path.GetFileName(file) == "_._")
                    continue;

                var taskItem = new TaskItem(file);
                taskItem.SetMetadata("Package", packageId);
                taskItem.SetMetadata("TargetFramework", frameworkFolder);

                // Add metadata about asset privacy - mark as private if in privateAssets list
                taskItem.SetMetadata("AssetType", AssetTypes.Compile);
                bool isPrivate = privateAssets.Contains(AssetTypes.Compile);
                taskItem.SetMetadata("IsPrivate", isPrivate.ToString());

                libraryContentFiles.Add(taskItem);
                packageFilesFound = true;
                filesFound++;
            }

            if (packageFilesFound)
            {
                Log.LogMessage($"Added files from {packageId} framework {frameworkFolder}",
                    MessageImportance.Low);
                break; // Found files for this package, move to next
            }
        }

        return filesFound;
    }
}