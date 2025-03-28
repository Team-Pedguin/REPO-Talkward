using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using NuGet.Versioning;
using ILogger = NuGet.Common.ILogger;

namespace Finder.MsBuild.Task;

[PublicAPI]
public sealed class FindNetStandardCompatibleContentViaNuGet : FindNetStandardCompatibleContentBase
{
    public bool FullDependencyGraph { get; set; } = true;
    
    /// <summary>
    /// Custom NuGet logger for unit testing and debugging.
    /// If null, NullLogger.Instance will be used.
    /// </summary>
    public ILogger? CustomNuGetLogger { get; set; }
    
    protected override bool ProcessPackages(string nuGetPackageRoot, List<TaskItem> contentFiles)
    {
        if (Packages is null || Packages.Length == 0)
        {
            Log.LogWarning("No packages specified to find content for, no content found.");
            return true;
        }

        // Get the compatible NetStandard frameworks
        var compatibleVersions = GetCompatibleNetStandardVersions();
        var targetFrameworks = compatibleVersions
            .Select(v => NuGetFramework.Parse($"netstandard{v}"))
            .ToList();

        var logger = CustomNuGetLogger ?? NullLogger.Instance;
        var sourceCacheContext = new SourceCacheContext();
        
        try
        {
            // Log diagnostic information
            Log.LogMessage($"Starting package processing with NuGet root: {nuGetPackageRoot}", MessageImportance.Normal);
            Log.LogMessage($"Maximum NetStandard version: {MaximumNetStandard}", MessageImportance.Normal);
            Log.LogMessage($"Target frameworks: {string.Join(", ", targetFrameworks.Select(f => f.GetShortFolderName()))}", MessageImportance.Normal);
            Log.LogMessage($"Packages to process: {string.Join(", ", Packages.Select(p => $"{p.ItemSpec} {p.GetMetadata("Version")}"))}", MessageImportance.Normal);

            using (Log.LogOperation("Setting up NuGet repository"))
            {
                // Set up the package source repository
                var repository = Repository.Factory.GetCoreV3(new PackageSource(nuGetPackageRoot));
                var findPackageByIdResource = repository.GetResourceAsync<FindPackageByIdResource>().Result;

                // Create a list to store all packages to process
                var packagesToProcess = new List<PackageIdentity>();
                var processedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                
                // Convert initial packages to PackageIdentity objects
                using (Log.LogOperation("Resolving package versions"))
                {
                    foreach (var package in Packages)
                    {
                        var packageId = package.ItemSpec;
                        var versionString = package.GetMetadata("Version");
                        
                        try
                        {
                            // Handle version ranges
                            if (versionString.IndexOfAny(['*', '(', ')', '[', ']']) != -1
                                && VersionRange.TryParse(versionString, true, out var versionRange))
                            {
                                // Try to find available versions from the filesystem
                                var availableVersions = new List<NuGetVersion>();
                                var packagePath = Path.Combine(nuGetPackageRoot, packageId.ToLowerInvariant());
                                
                                if (Directory.Exists(packagePath))
                                {
                                    foreach (var versionDir in Directory.GetDirectories(packagePath))
                                    {
                                        var dirName = Path.GetFileName(versionDir);
                                        if (NuGetVersion.TryParse(dirName, out var dirVersion))
                                        {
                                            availableVersions.Add(dirVersion);
                                        }
                                    }
                                }

                                // If no versions found locally, try the repository
                                if (availableVersions.Count == 0)
                                {
                                    availableVersions = findPackageByIdResource
                                        .GetAllVersionsAsync(packageId, sourceCacheContext, logger, CancellationToken.None)
                                        .Result.ToList();
                                }

                                var bestMatch = availableVersions
                                    .Where(v => versionRange.Satisfies(v))
                                    .OrderByDescending(v => v)
                                    .FirstOrDefault();

                                if (bestMatch == null)
                                {
                                    Log.LogWarning(
                                        $"No version found for package {packageId} matching range {versionString}");
                                    continue;
                                }

                                Log.LogMessage($"Resolved version range {versionString} to {bestMatch} for package {packageId}", MessageImportance.Low);
                                packagesToProcess.Add(new PackageIdentity(packageId, bestMatch));
                            }
                            else if (NuGetVersion.TryParse(versionString, out var parsedVersion))
                            {
                                packagesToProcess.Add(new PackageIdentity(packageId, parsedVersion));
                            }
                            else
                            {
                                Log.LogWarning($"Invalid version format for package {packageId}: {versionString}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.LogWarning($"Error processing package {packageId}: {ex.Message}");
                        }
                    }
                }
                
                // If FullDependencyGraph is enabled, resolve all dependencies
                if (FullDependencyGraph && packagesToProcess.Count > 0)
                {
                    using (Log.LogOperation("Resolving full dependency graph"))
                    {
                        Log.LogMessage("Resolving full dependency graph for packages...", MessageImportance.Normal);
                        var expandedPackages = ResolveDependencies(repository, packagesToProcess, targetFrameworks.First(), logger, sourceCacheContext);
                        
                        // Replace our initial list with the full dependency graph
                        packagesToProcess = expandedPackages;
                        Log.LogMessage($"Resolved {packagesToProcess.Count} packages in dependency graph.", MessageImportance.Normal);
                    }
                }

                // Process each package in the final list
                using (Log.LogOperation("Processing packages"))
                {
                    Log.LogMessage($"Processing {packagesToProcess.Count} packages for content", MessageImportance.Normal);
                    int filesFound = 0;

                    foreach (var packageId in packagesToProcess)
                    {
                        // Skip if we've already processed this exact package+version
                        var packageKey = $"{packageId.Id}|{packageId.Version}";
                        if (!processedPackages.Add(packageKey))
                            continue;
                        
                        string? libPath;
                        
                        // Check if there's a metadata item for this package in the original list
                        var originalPackageItem = Packages?.FirstOrDefault(p => 
                            string.Equals(p.ItemSpec, packageId.Id, StringComparison.OrdinalIgnoreCase));
                        
                        // Check if GeneratePathProperty is used (only if it's one of the original packages)
                        if (originalPackageItem != null)
                        {
                            var origPackagePath = TryGetPathFromProperty(originalPackageItem);
                            if (origPackagePath != null)
                            {
                                libPath = Path.Combine(origPackagePath, "lib");
                                Log.LogMessage($"Using path from property for {packageId.Id}: {origPackagePath}", MessageImportance.Low);

                                // Process content directly from the path we found
                                int packageFiles = ProcessPackageContent(packageId.Id, origPackagePath, libPath, targetFrameworks, contentFiles);
                                filesFound += packageFiles;
                                Log.LogMessage($"Found {packageFiles} files for {packageId.Id} from property path", MessageImportance.Low);
                                continue;
                            }
                        }

                        var packagePath = Path.Combine(nuGetPackageRoot, packageId.Id.ToLowerInvariant());
                        
                        try
                        {
                            // Get package reader
                            var fullPackagePath = Path.Combine(packagePath, packageId.Version.ToString());
                            libPath = Path.Combine(fullPackagePath, "lib");

                            if (!Directory.Exists(fullPackagePath))
                            {
                                Log.LogWarning($"Package directory not found: {fullPackagePath}");
                                continue;
                            }

                            int packageFiles = ProcessPackageContent(packageId.Id, fullPackagePath, libPath, targetFrameworks, contentFiles);
                            filesFound += packageFiles;
                            Log.LogMessage($"Found {packageFiles} files for {packageId.Id} v{packageId.Version}", MessageImportance.Low);
                        }
                        catch (Exception ex)
                        {
                            Log.LogWarning($"Error processing package {packageId.Id}: {ex.Message}");
                        }
                    }

                    Log.LogMessage($"Total content files found: {filesFound}", MessageImportance.Normal);
                }
            }
        }
        catch (Exception ex)
        {
            Log.LogError($"Error accessing NuGet repository: {ex.Message}");
            logger.LogError($"Error accessing NuGet repository: {ex.Message}");
            if (ex.InnerException != null)
            {
                logger.LogError($"Inner exception: {ex.InnerException.Message}");
            }
            return false;
        }

        return true;
    }

    private List<PackageIdentity> ResolveDependencies(
        SourceRepository repository, 
        List<PackageIdentity> packages, 
        NuGetFramework targetFramework, 
        ILogger logger,
        SourceCacheContext cacheContext)
    {
        var dependencyInfoResource = repository.GetResourceAsync<DependencyInfoResource>().Result;
        var packageMetadataResource = repository.GetResourceAsync<PackageMetadataResource>().Result;
        
        var resolvedPackages = new HashSet<PackageIdentity>(PackageIdentityComparer.Default);
        var pendingPackages = new Queue<PackageIdentity>(packages);
        
        while (pendingPackages.Count > 0)
        {
            var package = pendingPackages.Dequeue();
            
            // Skip if we've already processed this package
            if (!resolvedPackages.Add(package))
                continue;

            Log.LogMessage(MessageImportance.Low, $"Resolving dependencies for package {package.Id} {package.Version}");
            
            try
            {
                // Get dependencies for this package
                var dependencyInfo = dependencyInfoResource.ResolvePackage(
                    package, 
                    targetFramework, 
                    cacheContext,
                    logger, 
                    CancellationToken.None).Result;
                
                if (dependencyInfo == null)
                {
                    Log.LogWarning($"Could not resolve dependencies for package {package.Id} {package.Version} using NuGet API - trying fallback method");
                    
                    // Fallback to direct nuspec parsing for tests
                    var fallbackDependencies = TryGetDependenciesFromNuspec(repository, package, targetFramework);
                    if (fallbackDependencies.Count > 0)
                    {
                        Log.LogMessage(MessageImportance.Normal, $"Found {fallbackDependencies.Count} dependencies via nuspec fallback for {package.Id}");
                        
                        foreach (var dependency in fallbackDependencies)
                        {
                            try
                            {
                                // For each dependency, try to find its best version
                                if (dependency.VersionRange != null)
                                {
                                    var bestMatch = ResolvePackageVersion(repository, dependency.Id, dependency.VersionRange, 
                                        packageMetadataResource, cacheContext, logger);
                                    
                                    if (bestMatch != null)
                                    {
                                        pendingPackages.Enqueue(bestMatch);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.LogWarning($"Error resolving fallback dependency {dependency.Id}: {ex.Message}");
                            }
                        }
                    }
                    continue;
                }
                
                // Queue dependencies for processing
                foreach (var dependency in dependencyInfo.Dependencies)
                {
                    // If dependency has a version range, resolve the best version
                    if (dependency.VersionRange != null)
                    {
                        try
                        {
                            var bestMatch = ResolvePackageVersion(repository, dependency.Id, dependency.VersionRange, 
                                packageMetadataResource, cacheContext, logger);
                            
                            if (bestMatch != null)
                            {
                                pendingPackages.Enqueue(bestMatch);
                            }
                            else
                            {
                                Log.LogWarning($"Could not find version of {dependency.Id} matching {dependency.VersionRange}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.LogWarning($"Error resolving dependency {dependency.Id}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Error resolving dependencies for {package.Id}: {ex.Message}");
            }
        }
        
        return resolvedPackages.ToList();
    }
    
    private PackageIdentity? ResolvePackageVersion(
        SourceRepository repository,
        string packageId,
        VersionRange versionRange,
        PackageMetadataResource packageMetadataResource,
        SourceCacheContext cacheContext,
        ILogger logger)
    {
        try
        {
            var dependencyMetadata = packageMetadataResource.GetMetadataAsync(
                packageId,
                includePrerelease: false,
                includeUnlisted: false,
                cacheContext,
                logger,
                CancellationToken.None).Result;
                
            var bestMatch = dependencyMetadata
                .Where(m => versionRange.Satisfies(m.Identity.Version))
                .OrderByDescending(m => m.Identity.Version)
                .FirstOrDefault();
                
            if (bestMatch != null)
            {
                return bestMatch.Identity;
            }
            
            // If no metadata found, try finding versions directly from the filesystem
            var availableVersions = new List<NuGetVersion>();
            var packagePath = Path.Combine(repository.PackageSource.Source, packageId.ToLowerInvariant());
            
            if (Directory.Exists(packagePath))
            {
                foreach (var versionDir in Directory.GetDirectories(packagePath))
                {
                    var dirName = Path.GetFileName(versionDir);
                    if (NuGetVersion.TryParse(dirName, out var dirVersion))
                    {
                        availableVersions.Add(dirVersion);
                    }
                }
                
                var bestDirectMatch = availableVersions
                    .Where(v => versionRange.Satisfies(v))
                    .OrderByDescending(v => v)
                    .FirstOrDefault();
                    
                if (bestDirectMatch != null)
                {
                    return new PackageIdentity(packageId, bestDirectMatch);
                }
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Error resolving version for {packageId}: {ex.Message}");
        }
        
        return null;
    }
    
    private List<PackageDependency> TryGetDependenciesFromNuspec(
        SourceRepository repository, 
        PackageIdentity package,
        NuGetFramework targetFramework)
    {
        var dependencies = new List<PackageDependency>();
        var packagePath = Path.Combine(repository.PackageSource.Source, package.Id.ToLowerInvariant(), package.Version.ToString());
        var nuspecPath = Path.Combine(packagePath, $"{package.Id}.nuspec");
        
        // Check for package folder nuspec file
        if (!File.Exists(nuspecPath))
        {
            nuspecPath = Path.Combine(packagePath, $"{package.Id.ToLowerInvariant()}.nuspec");
            if (!File.Exists(nuspecPath))
            {
                Log.LogMessage(MessageImportance.Low, $"No nuspec file found for {package.Id} at {packagePath}");
                return dependencies;
            }
        }
        
        try
        {
            // Simple XML parsing to extract dependencies
            var nuspecContent = File.ReadAllText(nuspecPath);
            var targetFrameworkString = targetFramework.GetShortFolderName();
            
            // Use a simple approach to extract dependencies from the nuspec XML
            // This is a fallback for testing and may not handle all nuspec formats
            using (var reader = new StringReader(nuspecContent))
            {
                var inDependencies = false;
                var inTargetGroup = false;
                
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    var trimmedLine = line.Trim();
                    
                    if (trimmedLine.Contains("<dependencies>"))
                    {
                        inDependencies = true;
                    }
                    else if (trimmedLine.Contains("</dependencies>"))
                    {
                        inDependencies = false;
                        inTargetGroup = false;
                    }
                    else if (inDependencies && trimmedLine.Contains($"targetFramework=\"{targetFrameworkString}\""))
                    {
                        inTargetGroup = true;
                    }
                    else if (inDependencies && trimmedLine.Contains("</group>"))
                    {
                        inTargetGroup = false;
                    }
                    else if ((inDependencies && !inTargetGroup && !trimmedLine.Contains("<group ")) || 
                            (inDependencies && inTargetGroup))
                    {
                        if (trimmedLine.Contains("<dependency "))
                        {
                            // Extract id and version from the dependency element
                            var idMatch = System.Text.RegularExpressions.Regex.Match(trimmedLine, "id=\"([^\"]+)\"");
                            var versionMatch = System.Text.RegularExpressions.Regex.Match(trimmedLine, "version=\"([^\"]+)\"");
                            
                            if (idMatch.Success)
                            {
                                var depId = idMatch.Groups[1].Value;
                                var depVersion = versionMatch.Success ? versionMatch.Groups[1].Value : "*";
                                
                                if (VersionRange.TryParse(depVersion, out var versionRange))
                                {
                                    dependencies.Add(new PackageDependency(depId, versionRange));
                                    Log.LogMessage(MessageImportance.Low, $"Found dependency in nuspec: {depId} {depVersion}");
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Error parsing nuspec file for {package.Id}: {ex.Message}");
        }
        
        return dependencies;
    }

    private int ProcessPackageContent(string packageId, string packagePath, string libPath,
        List<NuGetFramework> targetFrameworks, List<TaskItem> libraryContentFiles)
    {
        if (!Directory.Exists(libPath))
            return 0;

        int filesFound = 0;
        
        // Try to get package reader from nupkg or expanded folder
        var nupkgPath = Directory.EnumerateFiles(packagePath, "*.nupkg").FirstOrDefault();

        try
        {
            PackageReaderBase packageReader;
            if (nupkgPath != null)
            {
                packageReader = new PackageArchiveReader(nupkgPath);
                Log.LogMessage($"Using package archive reader for {packageId} from {nupkgPath}", MessageImportance.Low);
            }
            else
            {
                packageReader = new PackageFolderReader(packagePath);
                Log.LogMessage($"Using package folder reader for {packageId} from {packagePath}", MessageImportance.Low);
            }

            using (packageReader)
            {
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
                    Log.LogMessage($"Best framework match for {packageId}: {bestMatch.TargetFramework.GetShortFolderName()} for target {targetFramework.GetShortFolderName()}", MessageImportance.Low);

                    // Skip if it's just a fallback marker
                    if (bestMatch.Items.Count() == 1 &&
                        Path.GetFileName(bestMatch.Items.First()) == "_._")
                    {
                        Log.LogMessage($"Skipping fallback marker in {packageId} for {bestMatch.TargetFramework.GetShortFolderName()}", MessageImportance.Low);
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
                        libraryContentFiles.Add(taskItem);
                        packageFilesFound = true;
                        filesFound++;
                    }

                    if (packageFilesFound)
                    {
                        Log.LogMessage($"Added files from {packageId} framework {frameworkFolder}", MessageImportance.Low);
                        break; // Found files for this package, move to next
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Error reading package {packageId}: {ex.Message}");
        }
        
        return filesFound;
    }
}
