using System.Linq;
using JetBrains.Annotations;
using Microsoft.Build.Framework;

namespace Finder.MsBuild.Task;

[PublicAPI]
public class SteamFindBestMatchingAppTask : Microsoft.Build.Utilities.Task
{
    [Required]
    public string SearchTerm { get; set; } = string.Empty;

    [Output]
    public long AppId { get; set; }

    [Output]
    public string? Path { get; set; }

    public override bool Execute()
    {
        var app = Steam.SearchForApps(SearchTerm).FirstOrDefault();
        if (app == null)
            return false;
        AppId = app.Id;
        Path = app.Path;
        return true;
    }
}