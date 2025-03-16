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
        Assert.IsNotNull(dirs);
    }

    [Test]
    public void FindGameTest()
    {
        var apps = Steam.SearchForApps("R.E.P.O");
        Assert.IsNotNull(apps);
        var app = apps.FirstOrDefault();
        Assert.That(app.Id, Is.EqualTo(3241660));
        TestContext.WriteLine("{0}: {1} @ {2} ({3})",
            app.Id, app.Name, app.Path, app.Score);
    }

    [Test]
    public void GetGameDirTest()
    {
        var dir = Steam.GetAppInstallDirectory(3241660);
        Assert.IsNotNull(dir);
        TestContext.WriteLine(dir);
    }
}