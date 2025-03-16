using Microsoft.Build.Utilities;

namespace Finder.MsBuild.Task;

public static class TaskHelpers
{
    public static TaskItem WithMetadata(this TaskItem item, string name, string value)
    {
        item.SetMetadata(name, value);
        return item;
    }
}