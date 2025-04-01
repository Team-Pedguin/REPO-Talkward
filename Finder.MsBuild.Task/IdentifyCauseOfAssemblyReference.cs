using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using NuGet.Versioning;

namespace Finder.MsBuild.Task;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

[PublicAPI]
public partial class IdentifyCauseOfAssemblyReference : Task
{
    [Required]
    public ITaskItem[]? SourceAssemblyPaths { get; set; }

    [Required]
    public ITaskItem[]? ReferenceAssemblyNames { get; set; }

    public bool Verbose { get; set; }

    public string? MessageImportance { get; set; } = "low";

    [Output]
    public ITaskItem[]? Causes { get; set; }

    private MessageImportance _messageImportance;

    public override bool Execute()
    {
        // Validate input parameters.
        if (SourceAssemblyPaths == null || SourceAssemblyPaths.Length == 0)
        {
            LogError("At least one SourceAssemblyPaths value must be provided.");
            return false;
        }

        if (ReferenceAssemblyNames == null || ReferenceAssemblyNames.Length == 0)
        {
            LogError("At least one ReferenceAssemblyNames value must be provided.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(MessageImportance)
            || !Enum.TryParse(MessageImportance, true, out _messageImportance))
        {
            MessageImportance = "low";
            _messageImportance = Microsoft.Build.Framework.MessageImportance.Low;
        }

        LogVerbose($"Message importance set to: {_messageImportance}");

        // Parse reference assembly names into a list of (AssemblyName, requireVersion)
        var targetRefs = ReferenceAssemblyNames
            .Select(refItem => new AssemblyRefMatch(refItem.ItemSpec, refItem.GetMetadata("Version")))
            .ToArray();

        LogVerbose($"Searching for {targetRefs.Length} assembly references: {string.Join(", ", targetRefs.Select(r => r.ToString()))}");

        var foundCauses = new List<ITaskItem>();
        try
        {
            LogVerbose($"Processing {SourceAssemblyPaths.Length} source assemblies");
            
            // Process each source assembly.
            foreach (var sourceItem in SourceAssemblyPaths)
            {
                var sourcePath = sourceItem.ItemSpec;
                LogMessage($"Loading assembly: {sourcePath}");
                try
                {
                    using var module =
                        ModuleDefMD.Load(sourcePath, new ModuleCreationOptions {TryToLoadPdbFromDisk = true});
                    
                    LogVerbose($"Successfully loaded module: {module.Name} with {module.Types.Count} types");
                    LogVerbose($"PDB loaded: {module.PdbState != null}");
                    
                    // check if the assembly actually has an assembly reference to the target
                    var assemblyRefs = module.GetAssemblyRefs().ToArray();
                    if (assemblyRefs.Length == 0)
                    {
                        LogVerbose($"No assembly references found in {module.Name}");
                        continue;
                    }

                    var matchedAssemblyRefs = 0;
                    foreach (var asmRef in assemblyRefs)
                    {
                        foreach (var targetRef in targetRefs)
                        {
                            if (!targetRef.IsMatch(asmRef.Name, new NuGetVersion(asmRef.Version)))
                            {
                                LogVerbose($"Reference: {asmRef.Name} v{asmRef.Version}");
                                continue;
                            }

                            LogVerbose($"Reference: {asmRef.Name} v{asmRef.Version} *MATCH*");
                            ++matchedAssemblyRefs;
                        }
                    }
                    
                    if (matchedAssemblyRefs == 0)
                    {
                        LogVerbose($"No matching assembly references found in {module.Name}");
                        continue;
                    }

                    LogVerbose($"Found {matchedAssemblyRefs} matching assembly references in {module.Name}");

                    // Iterate over types in the module.
                    var foundCount = foundCauses.Count;
                    foreach (var type in module.Types)
                    {
                        ProcessType(type, targetRefs, foundCauses, sourcePath);
                    }
                    if (foundCauses.Count == foundCount)
                    {
                        LogError($"Failed to find causes for assembly references in {module.Name}!");
                    }
                    else
                    {
                        LogMessage($"Found {foundCauses.Count - foundCount} causes for assembly references in {module.Name}");
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue with other assemblies
                    LogWarning($"Error processing assembly {sourcePath}: {ex.Message}");
                    LogVerbose($"Exception details: {ex}");
                }
            }

            Causes = foundCauses.ToArray();
            LogMessage($"Found {foundCauses.Count} cause(s) for the specified assembly reference(s).");
            
            if (foundCauses.Count > 0)
            {
                LogVerbose("Reference causes found by type:");
                var byType = foundCauses.GroupBy(c => c.GetMetadata("ReferenceType"));
                foreach (var group in byType)
                {
                    LogVerbose($"  {group.Key}: {group.Count()}");
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"Error processing assemblies: {ex.Message}");
            LogVerbose($"Exception details: {ex}");
            return false;
        }

        return true;
    }
}
