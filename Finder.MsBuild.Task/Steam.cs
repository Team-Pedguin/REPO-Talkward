using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using F23.StringSimilarity;
using Gameloop.Vdf.Linq;
using Microsoft.Win32;

namespace Finder.MsBuild.Task;

public class Steam
{
    private static JaroWinkler _similarity = new();

    private const string WinRegMachineKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam";
    private const string WinRegMachineValue = "InstallPath";
    private const string WinRegUserKey = @"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam";
    private const string WinRegUserValue = "SteamPath";
    private const string WinFallbackPath = @"%ProgramFiles(x86)%\Steam";
    private const string WinDefaultLibraryPath = @"SteamLibrary";
    private const string WinLibrarySubPath = @"steamapps\libraryfolders.vdf";

    private const string UnixSteamPath = ".steam/steam";
    private const string UnixSteamPathAlt = ".local/share/Steam";
    private const string UnixFlatpakPath = ".var/app/com.valvesoftware.Steam/data/Steam";
    private const string UnixLibrarySubPath = "steamapps/libraryfolders.vdf";

    private static readonly string DoubleDirSepChar = new(Path.DirectorySeparatorChar, 2);

    /// <summary>
    /// Finds the Steam library folders file.
    /// </summary>
    /// <returns>The path to the library folders file, or null if not found</returns>
    public static string? FindLibraryFoldersVdf()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var userPath = Registry.GetValue(WinRegUserKey, WinRegUserValue, null)?.ToString()
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            if (userPath is not null && Directory.Exists(userPath))
            {
                var path = Path.Combine(userPath, WinLibrarySubPath);
                if (File.Exists(path))
                    return path;
            }

            var machinePath = Registry.GetValue(WinRegMachineKey, WinRegMachineValue, null)?.ToString()
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            if (machinePath is not null && Directory.Exists(machinePath))
            {
                var path = Path.Combine(machinePath, WinLibrarySubPath);
                if (File.Exists(path))
                    return path;
            }

            var fallbackPath = Environment.ExpandEnvironmentVariables(WinFallbackPath);
            if (Directory.Exists(fallbackPath))
            {
                var path = Path.Combine(fallbackPath, WinLibrarySubPath);
                if (File.Exists(path))
                    return path;
            }

            foreach (var path in DriveInfo.GetDrives()
                         .Select(d => d.Name)
                         .Where(d => d is not null)
                         .Select(d => Path.Combine(d, WinDefaultLibraryPath, WinLibrarySubPath))
                         .Where(Directory.Exists))
            {
                if (File.Exists(path))
                    return path;
            }
        }
        else
        {
            var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            string?[] possiblePaths =
            [
                Path.Combine(homePath, UnixSteamPath, UnixLibrarySubPath),
                Path.Combine(homePath, UnixSteamPathAlt, UnixLibrarySubPath),
                Path.Combine(homePath, UnixFlatpakPath, UnixLibrarySubPath)
            ];

            foreach (var path in possiblePaths)
                if (File.Exists(path))
                    return path;

            // Check for custom libraries in common locations (optional)
            var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME")
                              ?? Path.Combine(homePath, ".local/share");
            var steamPath = Path.Combine(xdgDataHome, "Steam", UnixLibrarySubPath);
            if (File.Exists(steamPath))
                return steamPath;
        }

        return null;
    }

    /// <summary>
    /// Parses the Steam library folders file to get the library paths.
    /// </summary>
    /// <param name="libraryFoldersVdfPath">The path to the library folders file</param>
    /// <returns>A list of library paths, empty if none found</returns>
    public static IReadOnlyList<string> ParseLibraryFoldersVdf(string libraryFoldersVdfPath)
    {
        using var reader = new StreamReader(libraryFoldersVdfPath);
        var szr = new Gameloop.Vdf.VdfSerializer();
        var rootProp = szr.Deserialize(reader);
        if (rootProp.Key != "libraryfolders")
            return [];
        var i = 0;
        var rootObj = (VObject) rootProp.Value;
        var libPaths = new string?[rootObj.Count];
        var nulls = 0;
        foreach (var childProp in rootObj.Children<VProperty>())
        {
            var childObj = (VObject) childProp.Value;
            if (!childObj.TryGetValue("path", out var pathTok))
                continue;
            var pathValue = (VValue) pathTok;
            var path = pathValue.Value?.ToString()
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            if (string.IsNullOrEmpty(path))
            {
                nulls++;
                continue;
            }

            if (Directory.Exists(path))
                libPaths[i++] = path;
        }


        // normalize all the paths (e.g. \\ to /, dedupe slashes, etc.)
        for (var j = 0; j < libPaths.Length; j++)
            Normalize(ref libPaths[j]!);
        
        if (nulls <= 0)
            return libPaths!;

        var oldLibPaths = libPaths;
        libPaths = new string[oldLibPaths.Length - nulls];
        foreach (var path in oldLibPaths)
            if (path is not null)
                libPaths[i++] = path;

        return libPaths!;
    }

    private static void Normalize(ref string libPath)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (libPath is null) return;
        
        libPath = libPath
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        var oldPath = libPath;
        for (;;)
        {
            libPath = libPath
                .Replace(DoubleDirSepChar, Path.DirectorySeparatorChar.ToString());
            if (libPath == oldPath)
                break;
            oldPath = libPath;
        }

        libPath = libPath
            .TrimEnd(Path.DirectorySeparatorChar);
    }

    /// <summary>
    /// Finds and parses the Steam library folders file to get the library paths.
    /// </summary>
    /// <returns>A list of library paths</returns>
    public static IReadOnlyList<string> FindAndParseLibraryFolders()
    {
        var libraryFoldersVdfPath = FindLibraryFoldersVdf();
        return libraryFoldersVdfPath is not null
            ? ParseLibraryFoldersVdf(libraryFoldersVdfPath)
            : [];
    }

    /// <summary>
    /// Finds where a specific game is installed by searching for its appmanifest file.
    /// </summary>
    /// <param name="appId">The Steam AppID of the game</param>
    /// <returns>The full path to the game installation directory, or null if not found</returns>
    public static string? GetAppInstallDirectory(long appId)
    {
        var libraries = FindAndParseLibraryFolders();

        foreach (var library in libraries)
        {
            var appManifestPath = Path.Combine(library, "steamapps", $"appmanifest_{appId}.acf");

            if (!File.Exists(appManifestPath))
                continue;

            // Parse the manifest to get the actual installation directory name
            using var reader = new StreamReader(appManifestPath);
            var szr = new Gameloop.Vdf.VdfSerializer();
            var rootProp = szr.Deserialize(reader);

            if (rootProp.Value is not VObject appManifest)
                continue;

            if (!appManifest.TryGetValue("installdir", out var installDirToken))
                continue;

            var installDir = ((VValue) installDirToken).Value?.ToString();
            if (string.IsNullOrEmpty(installDir))
                continue;

            var path = Path.Combine(library, "steamapps", "common", installDir);

            Normalize(ref path);
            
            return path;
        }

        return null; // Game not found in any library
    }

    private static Regex _alphanumericRegex
        = new(@"[^a-zA-Z0-9]", RegexOptions.Compiled);

    public static IReadOnlyList<SteamAppMatch> SearchForApps(Regex gameNamePattern, string? gameNameGuess = null)
    {
        if (string.IsNullOrEmpty(gameNameGuess))
        {
            // remove all non-alphanumeric characters
            gameNameGuess = _alphanumericRegex.Replace(gameNamePattern.ToString(), "");
        }

        var libraries = FindAndParseLibraryFolders();

        if (libraries.Count == 0)
            return [];

        var list = new List<SteamAppMatch>();

        foreach (var library in libraries)
        {
            var steamAppsDir = Path.Combine(library, "steamapps");
            if (!Directory.Exists(steamAppsDir))
                continue;

            foreach (var manifestFile in Directory.GetFiles(steamAppsDir, "appmanifest_*.acf"))
            {
                try
                {
                    using var reader = new StreamReader(manifestFile);
                    var szr = new Gameloop.Vdf.VdfSerializer();
                    var rootProp = szr.Deserialize(reader);

                    if (rootProp.Value is not VObject appManifest)
                        continue;

                    if (!appManifest.TryGetValue("name", out var nameToken))
                        continue;

                    var name = ((VValue) nameToken).Value?.ToString();

                    if (string.IsNullOrEmpty(name))
                        continue;

                    if (!gameNamePattern.IsMatch(name))
                        continue;

                    if (!appManifest.TryGetValue("appid", out var appIdToken))
                        continue;

                    var appIdStr = ((VValue) appIdToken).Value?.ToString();
                    if (!long.TryParse(appIdStr, out var appId))
                        continue;

                    if (!appManifest.TryGetValue("installdir", out var installDirToken))
                        continue;

                    var installDir = ((VValue) installDirToken).Value?.ToString();
                    if (string.IsNullOrEmpty(installDir))
                        continue;

                    var gamePath = Path.Combine(library, "steamapps", "common", installDir);

                    Normalize(ref gamePath);

                    if (!Directory.Exists(gamePath))
                        continue;

                    var score = _similarity.Similarity(gameNameGuess, name);

                    list.Add(new SteamAppMatch(appId, gamePath, name, score));
                }
                catch
                {
                    // Skip files that can't be parsed
                    //continue;
                }
            }
        }

        list.Sort(static (a, b) => b.Score.CompareTo(a.Score));

        return list;
    }

    public static IReadOnlyList<SteamAppMatch> SearchForApps(string gameNamePattern, bool exactMatch = false)
    {
        if (string.IsNullOrEmpty(gameNamePattern))
            return [];

        var pattern = exactMatch
            ? $"^{Regex.Escape(gameNamePattern)}$"
            : string.Join(".*?", gameNamePattern
                .Select(c => Regex.Escape(c.ToString())));

        return SearchForApps(new Regex(pattern, RegexOptions.IgnoreCase));
    }
}