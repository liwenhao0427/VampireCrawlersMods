using System.Diagnostics;
using System.Reflection;

namespace VampireCrawlers.RuntimeMod;

internal sealed class GameCompatibility
{
    private const string ExpectedUnityVersion = "6000.0.62f1";
    private const string ExpectedBuildGuid = "8a901aaee48543148e76ec2bc34b2547";

    private GameCompatibility(string gameRoot, string unityVersion, string buildGuid, bool isExpectedBuild)
    {
        GameRoot = gameRoot;
        UnityVersion = unityVersion;
        BuildGuid = buildGuid;
        IsExpectedBuild = isExpectedBuild;
    }

    public string GameRoot { get; }

    public string UnityVersion { get; }

    public string BuildGuid { get; }

    public bool IsExpectedBuild { get; }

    public static GameCompatibility Probe()
    {
        var pluginPath = Assembly.GetExecutingAssembly().Location;
        var gameRoot = ResolveGameRoot(pluginPath);
        var unityPlayer = Path.Combine(gameRoot, "UnityPlayer.dll");
        var bootConfig = Path.Combine(gameRoot, "Vampire Crawlers_Data", "boot.config");

        var unityVersion = File.Exists(unityPlayer)
            ? FileVersionInfo.GetVersionInfo(unityPlayer).ProductVersion ?? "unknown"
            : "missing";

        var buildGuid = ReadBootConfigValue(bootConfig, "build-guid") ?? "missing";
        var normalizedUnityVersion = unityVersion.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? unityVersion;
        var isExpectedBuild = string.Equals(normalizedUnityVersion, ExpectedUnityVersion, StringComparison.OrdinalIgnoreCase)
            && string.Equals(buildGuid, ExpectedBuildGuid, StringComparison.OrdinalIgnoreCase);

        return new GameCompatibility(gameRoot, normalizedUnityVersion, buildGuid, isExpectedBuild);
    }

    public override string ToString()
    {
        return $"GameRoot='{GameRoot}', Unity='{UnityVersion}', BuildGuid='{BuildGuid}', Expected={IsExpectedBuild}";
    }

    private static string ResolveGameRoot(string pluginPath)
    {
        // BepInEx ?????? BepInEx/plugins ????? DLL ?????? exe?
        // ????????????????????????
        var directory = new DirectoryInfo(Path.GetDirectoryName(pluginPath) ?? AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Vampire Crawlers.exe")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return AppContext.BaseDirectory;
    }

    private static string ReadBootConfigValue(string path, string key)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        foreach (var line in File.ReadLines(path))
        {
            // boot.config ??? key=value ???????????????????????
            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            if (string.Equals(line[..separatorIndex], key, StringComparison.OrdinalIgnoreCase))
            {
                return line[(separatorIndex + 1)..].Trim();
            }
        }

        return null;
    }
}
