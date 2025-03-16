using System.Globalization;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Finder.MsBuild.Task;

public class SteamFindMatchingAppsTask : Microsoft.Build.Utilities.Task
{
    [Required]
    public string SearchTerm { get; set; } = string.Empty;

    [Output]
    public TaskItem[]? Apps { get; set; }

    public override bool Execute()
    {
        var apps = Steam.SearchForApps(SearchTerm);
        if (apps.Count == 0)
            return false;
        Apps = apps.Select(app => new TaskItem(app.Path)
                .WithMetadata("Id", app.Id.ToString())
                .WithMetadata("Name", app.Name)
                .WithMetadata("Score", app.Score.ToString("F3", CultureInfo.InvariantCulture)))
            .ToArray();
        return true;
    }
}