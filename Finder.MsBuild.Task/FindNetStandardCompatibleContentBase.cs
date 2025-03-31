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
    public TaskItem[] LibraryContentFiles { get; set; } = [];
    
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
        if (package.GetMetadata("GeneratePathProperty").ToLowerInvariant() != "true")
            return null;

        // GeneratePathProperty causes NuGet to create a Property in the project;
        // $(Pkg<PackageNameWithDotsAsUnderscores>) that contains the path to the package.
        // NuGet only replaces dots with underscores, preserving the original case
        var pkgPropertyName = $"Pkg{package.ItemSpec.Replace('.', '_')}";

        // Try to get the property from available sources
        string? propertyValue = null;

        // Try from BuildEngine.ProjectFileOfTaskNode (may contain properties for some engine implementations)
        if (!string.IsNullOrEmpty(BuildEngine.ProjectFileOfTaskNode))
        {
            try
            {
                // Try using the Microsoft.Build.Evaluation API if available at runtime
                // This is a separate approach that requires assembly binding
                var projectPath = BuildEngine.ProjectFileOfTaskNode;
                propertyValue = GetPropertyFromProjectFile(projectPath, pkgPropertyName);
                
                if (propertyValue != null)
                {
                    Log.LogMessage($"Found package path from project file: {pkgPropertyName}={propertyValue}", MessageImportance.Low);
                }
            }
            catch (Exception ex)
            {
                Log.LogMessage($"Error trying to access property from project file: {ex.Message}", MessageImportance.Low);
            }
        }

        // Check if the property value exists and points to a valid directory
        if (!string.IsNullOrEmpty(propertyValue))
        {
            if (Directory.Exists(propertyValue))
            {
                return propertyValue;
            }
            else
            {
                Log.LogWarning($"Path property {pkgPropertyName} exists but directory not found: {propertyValue}");
            }
        }
        else
        {
            Log.LogMessage($"GeneratePathProperty is true for package {package.ItemSpec} but property {pkgPropertyName} was not found. " +
                           "This may happen if called before NuGet's task to generate the property.", MessageImportance.Low);
        }

        return null;
    }
    
    /// <summary>
    /// Attempts to get a property value from a project file using the Evaluation API
    /// </summary>
    /// <remarks>
    /// This method uses reflection to avoid a hard dependency on Microsoft.Build.Evaluation
    /// which may not be available in all MSBuild contexts.
    /// </remarks>
    private string? GetPropertyFromProjectFile(string projectPath, string propertyName)
    {
        try
        {
            // We'll use reflection to access Microsoft.Build.Evaluation.ProjectCollection
            // to avoid a hard dependency which might not be available at runtime
            var projectCollectionType = Type.GetType("Microsoft.Build.Evaluation.ProjectCollection, Microsoft.Build, Version=15.1.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            if (projectCollectionType == null)
            {
                Log.LogMessage("Microsoft.Build.Evaluation.ProjectCollection type not found", MessageImportance.Low);
                return null;
            }
            
            // Get the GlobalProjectCollection static property
            var globalProjectCollectionProperty = projectCollectionType.GetProperty("GlobalProjectCollection");
            if (globalProjectCollectionProperty == null)
            {
                Log.LogMessage("GlobalProjectCollection property not found", MessageImportance.Low);
                return null;
            }
            
            var globalProjectCollection = globalProjectCollectionProperty.GetValue(null);
            if (globalProjectCollection == null)
            {
                Log.LogMessage("GlobalProjectCollection instance is null", MessageImportance.Low);
                return null;
            }
            
            // Try to get the loaded project
            var loadedProjectsProperty = projectCollectionType.GetProperty("LoadedProjects");
            if (loadedProjectsProperty == null)
            {
                Log.LogMessage("LoadedProjects property not found", MessageImportance.Low);
                return null;
            }
            
            var loadedProjects = loadedProjectsProperty.GetValue(globalProjectCollection) as System.Collections.IEnumerable;
            if (loadedProjects == null)
            {
                Log.LogMessage("LoadedProjects collection is null", MessageImportance.Low);
                return null;
            }
            
            // Look for our project in the loaded projects
            foreach (var project in loadedProjects)
            {
                var fullPathProperty = project.GetType().GetProperty("FullPath");
                if (fullPathProperty == null)
                    continue;
                
                var fullPath = fullPathProperty.GetValue(project) as string;
                if (string.IsNullOrEmpty(fullPath) || !string.Equals(fullPath, projectPath, StringComparison.OrdinalIgnoreCase))
                    continue;
                
                // Found our project, now get the property
                var getPropertyMethod = project.GetType().GetMethod("GetPropertyValue", [typeof(string)]);
                if (getPropertyMethod == null)
                    continue;
                
                var propertyValue = getPropertyMethod.Invoke(project, [propertyName]) as string;
                return propertyValue;
            }
            
            Log.LogMessage($"Project file {projectPath} not found in loaded projects", MessageImportance.Low);
            return null;
        }
        catch (Exception ex)
        {
            Log.LogMessage($"Error trying to get property from project file: {ex.Message}", MessageImportance.Low);
            return null;
        }
    }
}
