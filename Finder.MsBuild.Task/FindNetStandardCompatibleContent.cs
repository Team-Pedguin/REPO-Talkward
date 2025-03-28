using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using NuGet.Versioning;

namespace Finder.MsBuild.Task;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

/// <summary>
/// Given a list of NuGet packages and the NuGet package root,
/// finds the .NET Standard compatible content for each,
/// but doesn't count "built in fallback" content.
/// (e.g. where the lib dir for the target has just _._ in it.)
/// </summary>
[PublicAPI]
public sealed class FindNetStandardCompatibleContent : FindNetStandardCompatibleContentBase
{
    protected override bool ProcessPackages(string nuGetPackageRoot, List<TaskItem> contentFiles)
    {
        if (Packages is null || Packages.Length == 0)
        {
            Log.LogWarning("No packages specified to find content for, no content found.");
            return true;
        }
        
        var compatibleVersions = GetCompatibleNetStandardVersions();

        foreach (var package in Packages)
        {
            var version = package.GetMetadata("Version");

            // Try using GeneratePathProperty first
            var packagePath = TryGetPathFromProperty(package);

            if (packagePath == null)
            {
                packagePath = Path.Combine(nuGetPackageRoot, package.ItemSpec.ToLowerInvariant());
                if (version.IndexOfAny(['*', '(', ')', '[', ']']) != -1
                    && VersionRange.TryParse(version, true, out var versionRange))
                {
                    version = Directory
                        .EnumerateDirectories(packagePath, "*", SearchOption.TopDirectoryOnly)
                        .Select(d => Path.GetFileName(d)!)
                        .OrderByDescending(d => d)
                        .FirstOrDefault(d => versionRange.Satisfies(NuGetVersion.Parse(d)));
                    if (version == null)
                    {
                        Log.LogWarning($"No version found for package {package.ItemSpec} matching range {version}");
                        continue;
                    }
                }

                packagePath = Path.Combine(packagePath, version);
            }

            var libPath = Path.Combine(packagePath, "lib");

            if (!Directory.Exists(libPath))
                continue;

            foreach (var netStandardVersion in compatibleVersions)
            {
                var targetFw = $"netstandard{netStandardVersion}";
                var targetFwPath = Path.Combine(libPath, targetFw);
                if (!Directory.Exists(targetFwPath))
                    continue;

                var fc = 0;
                foreach (var file in Directory.EnumerateFiles(targetFwPath, "*", SearchOption.AllDirectories))
                {
                    if (Path.GetFileName(file) == "_._") continue;
                    var item = new TaskItem(file);
                    item.SetMetadata("Package", package.ItemSpec);
                    item.SetMetadata("TargetFramework", targetFw);
                    contentFiles.Add(item);
                    ++fc;
                }

                if (fc != 0)
                    break; // acceptable
            }
        }

        return true;
    }
}
