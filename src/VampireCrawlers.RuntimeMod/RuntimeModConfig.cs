using BepInEx.Configuration;

namespace VampireCrawlers.RuntimeMod;

internal sealed class RuntimeModConfig
{
    private RuntimeModConfig(
        ConfigEntry<bool> enableMod,
        ConfigEntry<bool> enableDebugLogs,
        ConfigEntry<bool> allowUnknownBuild,
        ConfigEntry<bool> enableLifecycleProbe)
    {
        EnableMod = enableMod;
        EnableDebugLogs = enableDebugLogs;
        AllowUnknownBuild = allowUnknownBuild;
        EnableLifecycleProbe = enableLifecycleProbe;
    }

    public ConfigEntry<bool> EnableMod { get; }

    public ConfigEntry<bool> EnableDebugLogs { get; }

    public ConfigEntry<bool> AllowUnknownBuild { get; }

    public ConfigEntry<bool> EnableLifecycleProbe { get; }

    public static RuntimeModConfig Bind(ConfigFile config)
    {
        return new RuntimeModConfig(
            config.Bind("General", "EnableMod", true, "?????????"),
            config.Bind("General", "EnableDebugLogs", false, "????????? BepInEx/LogOutput.log?"),
            config.Bind("Compatibility", "AllowUnknownBuild", false, "? Unity ??? build GUID ?????????????????"),
            config.Bind("Feature", "EnableLifecycleProbe", true, "??????????????"));
    }
}
