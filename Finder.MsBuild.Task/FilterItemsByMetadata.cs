namespace Finder.MsBuild.Task;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

/// <summary>
/// MSBuild task that filters a collection of items based on their some metadata
/// and whether that matches the identity of filter items.
/// </summary>
/// <remarks>
/// This task is useful for filtering Reference items based on PackageReference items,
/// allowing you to include or exclude references from specific NuGet packages.
/// </remarks>
[PublicAPI]
public sealed class FilterItemsByMetadata : Task
{
    /// <summary>
    /// The items to be filtered (typically Reference items).
    /// </summary>
    [Required]
    public ITaskItem[]? ItemsToFilter { get; set; }

    /// <summary>
    /// The items to filter by (typically PackageReference items).
    /// The ItemSpec (identity) of these items will be used to match against 
    /// the "NuGetPackageId" metadata of the ItemsToFilter.
    /// </summary>
    [Required]
    public ITaskItem[]? FilterByItems { get; set; }

    /// <summary>
    /// If true, returns only items that match the filter criteria.
    /// If false, returns only items that do NOT match the filter criteria.
    /// </summary>
    public bool ExclusiveFilter { get; set; } = true;

    /// <summary>
    /// The metadata name to check on each item being filtered.
    /// Defaults to "NuGetPackageId".
    /// </summary>
    public string? MetadataName { get; set; }

    /// <summary>
    /// The items that passed the filter.
    /// </summary>
    [Output]
    public ITaskItem[]? FilteredItems { get; private set; }

    public override bool Execute()
    {
        if (string.IsNullOrWhiteSpace(MetadataName))
        {
            Log.LogError("MetadataName is not set.");
            return false;
        }

        MetadataName = MetadataName.Trim();

        // Handle empty input collections
        if (ItemsToFilter == null || ItemsToFilter.Length == 0)
        {
            Log.LogMessage(MessageImportance.Low, "No items to filter provided.");
            FilteredItems = [];
            return true;
        }

        if (FilterByItems == null || FilterByItems.Length == 0)
        {
            Log.LogMessage(MessageImportance.Low, "No filter items provided.");

            // Return all or none of the original items, based on the ExclusiveFilter flag
            FilteredItems = ExclusiveFilter ? [] : ItemsToFilter;
            return true;
        }

        // Create a HashSet of filter values for efficient lookup
        var filterValues = new HashSet<string>(
            FilterByItems.Select(item => item.ItemSpec),
            StringComparer.OrdinalIgnoreCase
        );

        // Perform the filtering
        var filteredList = new List<ITaskItem>();
        var itemsProcessed = 0;
        var itemsMatched = 0;

        foreach (var item in ItemsToFilter)
        {
            itemsProcessed++;
            var nuGetPackageId = item.GetMetadata(MetadataName);
            var isMatch = !string.IsNullOrEmpty(nuGetPackageId) && filterValues.Contains(nuGetPackageId);

            if (isMatch)
            {
                itemsMatched++;
                Log.LogMessage(MessageImportance.Low,
                    $"Item with {MetadataName}='{nuGetPackageId}' matches a filter item.");
            }

            // Add to result list based on matching condition and ExclusiveFilter setting
            if ((ExclusiveFilter && isMatch) || (!ExclusiveFilter && !isMatch))
                filteredList.Add(item);
        }

        FilteredItems = filteredList.ToArray();

        Log.LogMessage(MessageImportance.Normal,
            $"Processed {itemsProcessed} items, matched {itemsMatched} items, and included {FilteredItems.Length} items in the output.");

        return true;
    }
}