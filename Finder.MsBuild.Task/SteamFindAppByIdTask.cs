using JetBrains.Annotations;

namespace Finder.MsBuild.Task;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

[PublicAPI]
public class SteamFindAppByIdTask : Task
{
    [Required]
    public long AppId { get; set; }

    [Output]
    public string? Path { get; set; }

    public override bool Execute()
    {
        return (Path = Steam.GetAppInstallDirectory(AppId)) != null;
    }
}