using System;

namespace Finder.MsBuild.Task;

public interface IPacker : IDisposable
{
    bool AddFile(string path, string? renamed = null);
}