using FluentAssertions;

namespace Finder.MsBuild.Task.Tests;

public class SteamTests
{
    [Test]
    public void FindAndParseLibraryFoldersTest()
    {
        var dirs = Steam.FindAndParseLibraryFolders();
        if (dirs.Count == 0)
            TestContext.WriteLine("No directories found.");
        foreach (var dir in dirs)
            TestContext.WriteLine(dir);
        dirs.Should().NotBeNull();
    }

    [Test]
    public void FindGameTest()
    {
        var apps = Steam.SearchForApps("R.E.P.O");
        AssertionExtensions.Should((object) apps).NotBeNull();
        var app = apps.FirstOrDefault();
        app.Id.Should().Be(3241660);
        TestContext.WriteLine("{0}: {1} @ {2} ({3})",
            app.Id, app.Name, app.Path, app.Score);
    }

    [Test]
    public void GetGameDirTest()
    {
        var dir = Steam.GetAppInstallDirectory(3241660);
        dir.Should().NotBeNull();
        TestContext.WriteLine(dir);
    }
}