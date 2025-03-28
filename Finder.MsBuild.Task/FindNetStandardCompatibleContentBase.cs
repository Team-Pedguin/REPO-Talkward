using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Finder.MsBuild.Task;

[PublicAPI]
public abstract class FindNetStandardCompatibleContentBase : Microsoft.Build.Utilities.Task
{
    protected static readonly string[] NetStandardVersions
        = ["2.1", "2.0", "1.6", "1.5", "1.4", "1.3", "1.2", "1.1", "1.0"];
    
    [Required]
    public string NuGetPackageRoot { get; set; } = string.Empty;

    [Required]
    public string MaximumNetStandard { get; set; } = "2.1";

    [Required]
    public ITaskItem[]? Packages { get; set; }

    [Output]
    public TaskItem[]? LibraryContentFiles { get; set; } = [];
    
    protected ArraySegment<string> GetCompatibleNetStandardVersions()
    {
        var maxVersion = Array.IndexOf(NetStandardVersions, MaximumNetStandard);
        if (maxVersion == -1)
            throw new ArgumentException($"Invalid NetStandard version: {MaximumNetStandard}");

        return new ArraySegment<string>(NetStandardVersions, maxVersion,
            NetStandardVersions.Length - maxVersion);
    }
    
    public override bool Execute()
    {
        if (Packages is null || Packages.Length == 0)
        {
            Log.LogWarning("No packages specified to find content for, no content found.");
            return true;
        }

        var nuGetPackageRoot = NuGetPackageRoot;
        if (!Directory.Exists(nuGetPackageRoot))
        {
            Log.LogError($"NuGet package root directory does not exist: {nuGetPackageRoot}");
            return false;
        }

        if (Directory.Exists(Path.Combine(nuGetPackageRoot, "packages")))
            nuGetPackageRoot = Path.Combine(nuGetPackageRoot, "packages");
        
        var contentFiles = new List<TaskItem>();
        
        if (!ProcessPackages(nuGetPackageRoot, contentFiles))
            return false;
            
        LibraryContentFiles = contentFiles.ToArray();
        return true;
    }
    
    protected abstract bool ProcessPackages(string nuGetPackageRoot, List<TaskItem> contentFiles);
    
    protected string? TryGetPathFromProperty(ITaskItem package)
    {
        if (package.GetMetadata("GeneratePathProperty").ToLowerInvariant() == "true")
        {
            // GeneratePathProperty causes NuGet to create a Property in the project;
            // $(Pkg<PackageNameWithDotsAsUnderscores>) that contains the path to the package.
            // NuGet only replaces dots with underscores, preserving the original case
            var pkgPropertyName = "Pkg" + package.ItemSpec.Replace('.', '_');

            // read the property from the project
            var props = BuildEngine6.GetGlobalProperties();
            if (props.TryGetValue(pkgPropertyName, out var path))
            {
                if (Directory.Exists(path))
                {
                    return path;
                }
                else
                {
                    Log.LogWarning($"Path property {pkgPropertyName} exists but directory not found: {path}");
                }
            }
            else
            {
                Log.LogWarning($"GeneratePathProperty is true for package {package.ItemSpec} but property {pkgPropertyName} was not found. " +
                              "This may happen if called before NuGet's task to generate the property.");
            }
        }
        
        return null;
    }
}
